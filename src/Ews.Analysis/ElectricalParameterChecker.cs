namespace Ews.Analysis;

using Ews.Domain.Analysis;

/// <summary>
/// 電気パラメータ(定格キー)チェックエンジン。
///
/// 【C原典】toku/sekkei/src/Fyss1d.c(電気パラメータ解析部)
///   - 入口  : Parm_Check_Main()   … 予約語ごとの電気パラメータ文字列を1グループずつ解析する。
///   - 抽出  : Get_1_Group()       … 型非依存で「数値部＋記号部」を1グループ切り出し桁数を数える。
///   - 検証  : Check_1_Group()     … 定格キー表(fyak_tbl[iNo].tkak_t)と照合し桁・繰返数を検証。
///   - 記号  : change_parm_data()  … 記号部文字列を切り出す。
///   - 補助  : digit_skip/not_digit_skip/delimit_skip/piriod_skip(Fyss1d.c:10332~)。
///
/// 【定格キー表】toku/include/sekkei/fyrt810.h(検証の正典。表示展開用の FySinTkakt.h t_* とは別)
///   - FYAK_T{symbol[5],len,d_len,num,flag}    → <see cref="RatingKeySpec"/>
///   - FYAK_TBL{yoyaku[8],flag,tkak_t*}         → <see cref="RatingKeyTables"/> の辞書エントリ
///   - ft_mcb[]/ft_elb[]… 約130表(予約語→定格キー表)。
///
/// フェーズE.1で <b>型非依存の中核パーサ(構造検証)</b> を、フェーズE.2で
/// <b>値格納・範囲検証(key_check)</b> を忠実移植する。
/// 個々の値は <see cref="RatingValues"/>(【C原典】union key_tbl / fyrt811)へ格納し、
/// <see cref="KeyCheckMain"/> が型別ルール(<see cref="KeyCheckRules"/>)で重複・範囲を検証する。
/// E.2では MCB/MC/MG/THR/MCDT/CSDT/SC を収録(いずれも走査単位が単純な機種)。
/// MA[3][3] 等の inum 索引配列を持つ ELB/R* や、奇数丸め特殊処理の NT、
/// および特殊展開プレースホルダ(PT/BP)は後続フェーズで対応する。
/// CT/VT付き('/')の構造検証(next_1_get/n_kigo 含む)と VM/TM は通常構造として移植済み。
/// TR(変圧器)は専用パーサ TR_check_main()(多スロット/状態付き)として <see cref="TrCheckMain"/> へ移植済み。
/// </summary>
public sealed class ElectricalParameterChecker
{
    /// <summary>
    /// 定格キー1記号の仕様。【C原典】fyrt810.h の <c>FYAK_T</c> 構造体。
    /// </summary>
    /// <param name="Symbol">記号名(例 "AF","AT","VAC")。【C原典】symbol[5]。</param>
    /// <param name="Length">値全体の許容桁数(整数＋小数)。【C原典】len。</param>
    /// <param name="DecimalLength">小数部の許容桁数。【C原典】d_len。</param>
    /// <param name="Count">繰返し(区切り '･' 連結)の許容個数。【C原典】num。</param>
    /// <param name="Flag">展開フラグ(0=必須 / 1=任意展開 / 2=任意非展開)。【C原典】flag。</param>
    public readonly record struct RatingKeySpec(
        string Symbol,
        int Length,
        int DecimalLength,
        int Count,
        int Flag);

    /// <summary>
    /// 予約語 → 定格キー表 の対応辞書。【C原典】fyrt810.h(FyInspecTkakt.h)の <c>fyak_tbl[]</c> と各 <c>ft_xxx[]</c>。
    /// これがパラメータ検証(Parm_Check_Main → Check_1_Group が参照する <c>fyak_tbl[iNo].tkak_t</c>)の正典。
    /// 値はメンバ表(FYAK_T[])。末尾の NULL 番兵({"",0,0,0,0})は移植しない(配列長で判定)。
    ///
    /// 注意: 定格キー"展開"(表示文字列生成 FySinTkaktKeyTenkai)用の FySinTkakt.h <c>t_xxx[]</c> とは
    ///       別実体であり値も異なる。検証には必ず本 fyrt810.h の <c>ft_xxx[]</c> を用いる。
    ///
    /// '/'(CT/VT付き)を含む表(WH/VM/AM/VT/CT/RTR/BLTR/PLTR/THSW)は通常パーサで検証できる
    /// (Get_1_Group が '/' を記号として切り出し、NextOneGet が副記号を取得)。
    /// 空表(検証記号なし)は C 原典に忠実に空配列で収録し、任意パラメータは FY-699E とする:
    ///   STM/SIR/C/R/D/NICA/RE/VVVF/TVZ/TVB/TVH/TVK/SPACE/AL。
    /// 通常検証に載らない特殊展開は本辞書に含めない: TR(専用パーサ <see cref="TrCheckMain"/> へ分岐、ft_tr は <see cref="TransformerKeyTable"/> に別保持)、
    ///   PT/BP(空記号プレースホルダ len25、fyak_tbl の flag 非0)。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, RatingKeySpec[]> RatingKeyTables =
        new Dictionary<string, RatingKeySpec[]>(StringComparer.Ordinal)
        {
            // ==== 遮断器・開閉器・計器系(fyak_tbl 順) ====
            // ft_mcb[] … MCB
            ["MCB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 4, 0, 1, 0),
                new("AT", 4, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("A", 4, 0, 1, 0),
            ],
            // ft_elb[] … ELB
            ["ELB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 4, 0, 1, 0),
                new("AT", 4, 0, 1, 0),
                new("MA", 3, 0, 3, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("A", 4, 0, 1, 0),
            ],
            // ft_mmcb[] … MMCB
            ["MMCB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 3, 0, 1, 0),
                new("AT", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("A", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
            ],
            // ft_elmb[] … ELMB
            ["ELMB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 3, 0, 1, 0),
                new("AT", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("MA", 3, 0, 3, 0),
                new("VAC", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("A", 5, 2, 1, 0),
            ],
            // ft_sb[] … SB
            ["SB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 2, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("A", 2, 0, 1, 0),
            ],
            // ft_rmcb[] … RMCB(【C原典】fyak_tbl の予約語名は "RMCB"。旧移植の "RECB" は誤り)
            ["RMCB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 2, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("A", 2, 0, 1, 0),
            ],
            // ft_relb[] … RELB
            ["RELB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 2, 0, 1, 0),
                new("MA", 3, 0, 3, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("A", 2, 0, 1, 0),
            ],
            // ft_rmmcb[] … RMMCB
            ["RMMCB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 4, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("A", 4, 2, 1, 0),
            ],
            // ft_relmb[] … RELMB
            ["RELMB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 4, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("MA", 3, 0, 3, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("A", 4, 2, 1, 0),
            ],
            // ft_mc[] … MC(AC/BC は flag1)
            ["MC"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
            ],
            // ft_thr[] … THR
            ["THR"] =
            [
                new("E", 1, 0, 1, 0),
                new("AT", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
            ],
            // ft_mg[] … MG(AC/BC は flag1)
            ["MG"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("AT", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
            ],
            // ft_sc[] … SC
            ["SC"] =
            [
                new("P", 1, 0, 1, 0),
                new("KVAR", 5, 2, 1, 0),
                new("UF", 5, 1, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("HZ", 2, 0, 1, 0),
            ],
            // ft_nt[] … NT
            ["NT"] =
            [
                new("P", 3, 0, 1, 0),
                new("A", 2, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // ft_wh[] … WH('/' は flag1。副記号 A/V を NextOneGet で判別 → key_check_WH は E.2)
            ["WH"] =
            [
                new("P", 1, 0, 1, 0),
                new("W", 1, 0, 1, 0),
                new("/", 3, 0, 1, 1),
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("HZ", 2, 0, 1, 0),
            ],
            // ft_vm[] … VM(【C原典】fyak_tbl flag=FY_SY_VM だが表は '/'含の通常構造で検証可)
            ["VM"] =
            [
                new("/", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // ft_am[] … AM('/' は len4/flag1)
            ["AM"] =
            [
                new("/", 4, 0, 1, 1),
                new("A", 4, 0, 1, 0),
            ],
            // ft_vt[] … VT
            ["VT"] =
            [
                new("/", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VA", 3, 0, 1, 0),
            ],
            // ft_ct[] … CT
            ["CT"] =
            [
                new("/", 4, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("VA", 2, 0, 1, 0),
            ],
            // ft_vs[] … VS
            ["VS"] = [new("P", 1, 0, 1, 0), new("W", 1, 0, 1, 0)],
            // ft_as[] … AS
            ["AS"] = [new("P", 1, 0, 1, 0), new("W", 1, 0, 1, 0)],
            // ft_tb[] … TB
            ["TB"] =
            [
                new("P", 3, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("SQ", 5, 2, 1, 0),
            ],
            // ft_con[] … CON
            ["CON"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // (TR は fyak_tbl flag=FY_SY_TR。専用パーサ TrCheckMain へ分岐し ft_tr は TransformerKeyTable に別保持)

            // ==== リレー・接地・保護・付属機器系 ====
            // ft_zct[] … ZCT
            ["ZCT"] =
            [
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("P", 3, 0, 1, 0),
            ],
            // ft_lgr[] … LGR
            ["LGR"] =
            [
                new("K", 2, 0, 1, 0),
                new("MA", 4, 0, 4, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
            ],
            // ft_elr[] … ELR
            ["ELR"] = [new("MA", 3, 0, 3, 0), new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            // ft_hpsb[] … HPSB
            ["HPSB"] =
            [
                new("P", 1, 0, 1, 0),
                new("AF", 3, 0, 1, 0),
                new("AT", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("AM", 3, 0, 1, 0),
            ],
            // ft_hsb[] … HSB
            ["HSB"] =
            [
                new("P", 1, 0, 1, 0),
                new("AF", 3, 0, 1, 0),
                new("AT", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("AM", 3, 0, 1, 0),
            ],
            // ft_rry[] … RRY
            ["RRY"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 2, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 2, 0, 1, 0),
                new("VCAC", 2, 0, 1, 0),
            ],
            // ft_rtr[] … RTR('/' 先頭)
            ["RTR"] =
            [
                new("/", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VA", 2, 0, 1, 0),
            ],
            // ft_mcdt[] … MCDT(電源切替開閉器 / Ele_Equal_Check step3 対象)
            ["MCDT"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
            ],
            // ft_f[] … F
            ["F"] =
            [
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // ft_la[] … LA
            ["LA"] =
            [
                new("P", 1, 0, 1, 0),
                new("W", 1, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
            ],
            // ft_dcpw[] … DCPW
            ["DCPW"] =
            [
                new("A", 5, 2, 1, 0),
                new("W", 4, 1, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // ft_cr[] … CR(AC/BC/CC は flag1)
            ["CR"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
                new("CC", 1, 0, 1, 1),
            ],
            // ft_tm[] … TM(【C原典】fyak_tbl flag=FY_SY_TM だが通常構造で検証可。
            //          SSET/MSET/HSET/S//M//H//S/M/H はタイマ設定値)
            ["TM"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("SSET", 8, 3, 1, 0),
                new("MSET", 8, 3, 1, 0),
                new("HSET", 8, 3, 1, 0),
                new("S/", 8, 3, 1, 0),
                new("M/", 8, 3, 1, 0),
                new("H/", 8, 3, 1, 0),
                new("S", 8, 3, 1, 0),
                new("M", 8, 3, 1, 0),
                new("H", 8, 3, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
                new("CC", 1, 0, 1, 1),
            ],
            // ft_ts[] … TS(AC/BC/CC は flag1)
            ["TS"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
                new("CC", 1, 0, 1, 1),
            ],
            // ft_g1[] … G/G1(【C原典】fyak_tbl: G→ft_g1, G1→ft_g1)
            ["G"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            ["G1"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            // ft_g2[]/ft_g3[]/ft_g4[] … G2/G3/G4
            ["G2"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            ["G3"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            ["G4"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            // ft_i[]/ft_p[]/ft_n[] … GI/GP/GPN(【C原典】fyak_tbl: GI→ft_i, GP→ft_p, GPN→ft_n)
            ["GI"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            ["GP"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            ["GPN"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            // ft_gl[]/ft_rl[]/ft_ol[]/ft_bl[]/ft_wl[] … 表示灯 GL/RL/OL/BL/WL(同一構造)
            ["GL"] =
            [
                new("V", 4, 1, 1, 0),
                new("VAC", 4, 1, 1, 0),
                new("VDC", 4, 1, 1, 0),
                new("W", 3, 2, 1, 0),
                new("P", 3, 1, 1, 0),
            ],
            ["RL"] =
            [
                new("V", 4, 1, 1, 0),
                new("VAC", 4, 1, 1, 0),
                new("VDC", 4, 1, 1, 0),
                new("W", 3, 2, 1, 0),
                new("P", 3, 1, 1, 0),
            ],
            ["OL"] =
            [
                new("V", 4, 1, 1, 0),
                new("VAC", 4, 1, 1, 0),
                new("VDC", 4, 1, 1, 0),
                new("W", 3, 2, 1, 0),
                new("P", 3, 1, 1, 0),
            ],
            ["BL"] =
            [
                new("V", 4, 1, 1, 0),
                new("VAC", 4, 1, 1, 0),
                new("VDC", 4, 1, 1, 0),
                new("W", 3, 2, 1, 0),
                new("P", 3, 1, 1, 0),
            ],
            ["WL"] =
            [
                new("V", 4, 1, 1, 0),
                new("VAC", 4, 1, 1, 0),
                new("VDC", 4, 1, 1, 0),
                new("W", 3, 2, 1, 0),
                new("P", 3, 1, 1, 0),
            ],
            // ft_cos[]/ft_pbs[] … COS/PBS(同一構造)
            ["COS"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("P", 3, 1, 1, 0),
            ],
            ["PBS"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("P", 3, 1, 1, 0),
            ],
            // ft_ssw[]/ft_tsw[] … SSW/TSW(同一構造)
            ["SSW"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            ["TSW"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // ft_bz[] … BZ(全記号 flag1)
            ["BZ"] =
            [
                new("VC", 3, 0, 1, 1),
                new("VCAC", 3, 0, 1, 1),
                new("VCDC", 3, 0, 1, 1),
                new("W", 3, 2, 1, 1),
                new("VA", 3, 2, 1, 1),
            ],
            // ft_bel[] … BEL
            ["BEL"] =
            [
                new("VC", 3, 0, 1, 1),
                new("VCAC", 3, 0, 1, 1),
                new("VCDC", 3, 0, 1, 1),
                new("W", 3, 2, 1, 1),
                new("VA", 3, 2, 1, 1),
                new("P", 3, 0, 1, 0),
            ],
            // ft_cp[] … CP
            ["CP"] =
            [
                new("P", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 2, 0, 1, 0),
                new("A", 2, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // ft_rsw[] … RSW
            ["RSW"] = [new("K", 3, 0, 1, 0), new("VC", 2, 0, 1, 0), new("VCAC", 2, 0, 1, 0)],
            // ft_ee[] … EE
            ["EE"] = [new("A", 2, 0, 1, 0), new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            // ft_hm[] … HM
            ["HM"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0), new("HZ", 2, 0, 1, 0)],
            // ft_2ery[]/ft_3ery[]/ft_4ery[] … 2ERY/3ERY/4ERY(先頭数字予約語, 同一構造)
            ["2ERY"] =
            [
                new("AF", 5, 2, 1, 0),
                new("AT", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
            ],
            ["3ERY"] =
            [
                new("AF", 5, 2, 1, 0),
                new("AT", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
            ],
            ["4ERY"] =
            [
                new("AF", 5, 2, 1, 0),
                new("AT", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
            ],
            // ft_cks[] … CKS
            ["CKS"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // ft_csdt[] … CSDT(切替カバースイッチ / Ele_Equal_Check step3 対象)
            ["CSDT"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // ft_cu[] … CU
            ["CU"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            // ft_tu[] … TU
            ["TU"] = [new("K", 1, 0, 1, 0), new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            // ft_nhmb[] … NHMB
            ["NHMB"] =
            [
                new("P", 1, 0, 1, 0),
                new("AT", 4, 2, 1, 0),
                new("KW", 3, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("A", 4, 2, 1, 0),
            ],
            // ft_apn[] … APN
            ["APN"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            // ft_sl23[]/ft_sl32[]/ft_sl42[]/ft_sl43[] … SL23/SL32/SL42/SL43(同一構造)
            ["SL23"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            ["SL32"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            ["SL42"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            ["SL43"] = [new("VC", 3, 0, 1, 0), new("VCAC", 3, 0, 1, 0)],
            // ft_lgt[] … LGT
            ["LGT"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 4, 0, 1, 0),
                new("T", 3, 1, 1, 0),
                new("W", 2, 0, 1, 0),
            ],
            // ft_bltr[] … BLTR('/' 先頭)
            ["BLTR"] =
            [
                new("/", 3, 0, 1, 0),
                new("V", 2, 0, 1, 0),
                new("VAC", 2, 0, 1, 0),
                new("VA", 2, 0, 1, 0),
            ],
            // ft_pltr[] … PLTR('/' 先頭)
            ["PLTR"] =
            [
                new("/", 3, 0, 1, 0),
                new("V", 2, 0, 1, 0),
                new("VAC", 2, 0, 1, 0),
                new("VA", 2, 0, 1, 0),
            ],
            // ft_fl[] … FL
            ["FL"] = [new("V", 3, 0, 1, 0), new("VAC", 3, 0, 1, 0), new("W", 2, 0, 1, 0)],
            // ft_lsw[] … LSW
            ["LSW"] =
            [
                new("A", 5, 3, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // ft_dsw[] … DSW
            ["DSW"] =
            [
                new("A", 5, 3, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
            ],
            // ft_sv[] … SV
            ["SV"] = [new("V", 3, 0, 1, 0), new("VAC", 3, 0, 1, 0), new("VA", 2, 0, 1, 0)],
            // ft_mv[] … MV(VAC/VDC/VA/W は flag1)
            ["MV"] =
            [
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 1),
                new("VDC", 3, 0, 1, 1),
                new("VA", 3, 0, 1, 1),
                new("W", 3, 0, 1, 1),
            ],
            // ft_kpry[] … KPRY(【C原典】AC/BC/CC は flag0)
            ["KPRY"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 2, 0, 1, 0),
                new("AC", 1, 0, 1, 0),
                new("BC", 1, 0, 1, 0),
                new("CC", 1, 0, 1, 0),
            ],
            // ft_thsw[] … THSW('C/' は英字始まりのため通常走査で記号として切り出される)
            ["THSW"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("C/", 3, 0, 1, 0),
                new("C", 3, 0, 1, 0),
                new("CSET", 3, 0, 1, 0),
            ],
            // ft_l[] … L
            ["L"] = [new("P", 1, 0, 1, 0), new("W", 1, 0, 1, 0), new("A", 3, 0, 1, 0)],
            // ft_idf[] … IDF
            ["IDF"] = [new("P", 3, 0, 1, 0)],
            // ft_hdf[] … HDF
            ["HDF"] = [new("P", 3, 0, 1, 0)],
            // ft_mdf[] … MDF
            ["MDF"] = [new("P", 3, 0, 1, 0)],
            // ft_tvz[]/ft_tvb[]/ft_tvh[]/ft_tvk[] … TVZ/TVB/TVH/TVK(空表: 任意パラメータは FY-699E)
            ["TVZ"] = [],
            ["TVB"] = [],
            ["TVH"] = [],
            ["TVK"] = [],
            // ft_wdp[] … WDP
            ["WDP"] = [new("T", 2, 0, 1, 0)],
            // ft_mcfr[] … MCFR(AC/BC は flag1)
            ["MCFR"] =
            [
                new("A", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
            ],
            // ft_mgfr[] … MGFR(AC/BC は flag1)
            ["MGFR"] =
            [
                new("E", 1, 0, 1, 0),
                new("A", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
                new("AT", 5, 2, 1, 0),
            ],
            // ft_mcsd[] … MCSD
            ["MCSD"] =
            [
                new("A", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
            ],
            // ft_mgsd[] … MGSD
            ["MGSD"] =
            [
                new("E", 1, 0, 1, 0),
                new("A", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AT", 5, 2, 1, 0),
            ],
            // ft_mgld[] … MGLD
            ["MGLD"] =
            [
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
            ],
            // ft_mgcs[] … MGCS
            ["MGCS"] =
            [
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
            ],
            // ft_inv[] … INV
            ["INV"] =
            [
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
            ],
            // ft_flt1[]/ft_flt2[]/ft_flt3[]/ft_flt4[]/ft_flti[] … FLT1..4/FLTI(同一構造, AC/BC/CC flag1)
            ["FLT1"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
                new("CC", 1, 0, 1, 1),
            ],
            ["FLT2"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
                new("CC", 1, 0, 1, 1),
            ],
            ["FLT3"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
                new("CC", 1, 0, 1, 1),
            ],
            ["FLT4"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
                new("CC", 1, 0, 1, 1),
            ],
            ["FLTI"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
                new("CC", 1, 0, 1, 1),
            ],
            // ft_dcsir[] … DCSIR
            ["DCSIR"] =
            [
                new("A", 5, 2, 1, 0),
                new("W", 4, 1, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 2, 0, 1, 0),
            ],
            // ft_dcni[] … DCNI
            ["DCNI"] =
            [
                new("A", 5, 2, 1, 0),
                new("W", 4, 1, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 1, 1, 0),
                new("MAH", 5, 0, 1, 0),
            ],
            // ft_mcfrsd[] … MCFRSD
            ["MCFRSD"] =
            [
                new("A", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
            ],
            // ft_mgfrsd[] … MGFRSD
            ["MGFRSD"] =
            [
                new("E", 1, 0, 1, 0),
                new("A", 5, 2, 1, 0),
                new("KW", 5, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("AT", 5, 2, 1, 0),
            ],
            // ft_stm[]/ft_sir[]/ft_c[]/ft_r[]/ft_d[]/ft_nica[]/ft_re[]/ft_vvvf[] …
            //   STM/SIR/C/R/D/NICA/RE/VVVF(空表: 任意パラメータは FY-699E)
            ["STM"] = [],
            ["SIR"] = [],
            ["C"] = [],
            ["R"] = [],
            ["D"] = [],
            ["NICA"] = [],
            ["RE"] = [],
            ["VVVF"] = [],
            // ft_space[] … SPACE(空表)
            ["SPACE"] = [],
            // (PT/BP は fyak_tbl flag=FY_SY_PT/FY_SY_BP、空記号 len25 の特殊展開プレースホルダの
            //  ため通常検証不可。本辞書に含めず後続フェーズ)
            // ft_tsu[] … TSU
            ["TSU"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("K", 2, 0, 1, 0),
            ],
            // ft_sswu[] … SSWU(自動点滅増設器)
            ["SSWU"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("K", 2, 0, 1, 0),
            ],
            // ft_pbsu[] … PBSU(押ボタンユニット)
            ["PBSU"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("K", 2, 0, 1, 0),
            ],
            // ft_cosu[] … COSU(セレクタースイッチユニット)
            ["COSU"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("K", 2, 0, 1, 0),
            ],
            // ft_2cosu[] … 2COSU(交互用セレクタースイッチユニット)
            ["2COSU"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("K", 2, 0, 1, 0),
            ],
            // ft_olu[] … OLU(ランプユニット)
            ["OLU"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("K", 2, 0, 1, 0),
            ],
            // SMTKP/SMTSS/SMTRY … 【C原典】fyak_tbl で ft_tsu を共用(改訂<1>)
            ["SMTKP"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("K", 2, 0, 1, 0),
            ],
            ["SMTSS"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("K", 2, 0, 1, 0),
            ],
            ["SMTRY"] =
            [
                new("A", 4, 2, 1, 0),
                new("V", 3, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VDC", 3, 0, 1, 0),
                new("VC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 1, 0),
                new("VCDC", 3, 0, 1, 0),
                new("K", 2, 0, 1, 0),
            ],
            // ft_al[] … AL(改訂<3>, 空表)
            ["AL"] = [],
        };

    /// <summary>
    /// TR(変圧器)専用の定格キー表。【C原典】fyrt810.h <c>ft_tr[]</c>(予約語 TR, fyak_tbl flag=FY_SY_TR)。
    ///
    /// 通常の <see cref="RatingKeyTables"/> とは別に、専用パーサ <see cref="TrCheckMain"/> が
    /// C の <c>fyak_tbl[iNo].tkak_t</c> として順次(シーケンシャルに)走査する。
    /// 一次側 P/W → '/'(定格電圧1) → 二次側 P/W/V/VAC → 三次側 P/W/V/VAC → VA/KVA の順。
    /// flag: 0=通常 / 1=必須(未入力で FY-889E) / 2=いずれか1つ必須(受理で ior1=1)。
    /// 末尾の空記号エントリ({"",0,0,0,0})は C の終端判定 <c>p_tbl->symbol[0]=='\0'</c> を再現するため保持する。
    /// </summary>
    private static readonly RatingKeySpec[] TransformerKeyTable =
    [
        new("P", 1, 0, 1, 0),
        new("W", 1, 0, 1, 0),
        new("/", 3, 0, 3, 0),      // 950522 3,0,3,1 -> 3,0,3,0
        new("P", 1, 0, 1, 0),
        new("W", 1, 0, 1, 0),
        new("V", 3, 0, 3, 2),
        new("VAC", 3, 0, 3, 2),
        new("P", 1, 0, 1, 0),
        new("W", 1, 0, 1, 0),
        new("V", 3, 0, 3, 0),      // 940727
        new("VAC", 3, 0, 2, 0),
        new("VA", 3, 0, 1, 0),
        new("KVA", 5, 2, 1, 0),
        new("", 0, 0, 0, 0),       // 終端番兵(【C原典】{"",0,0,0,0})
    ];

    /// <summary>本フェーズで構造検証を提供できる予約語かどうか(定格キー表を収録済みか)。</summary>
    public bool IsSupported(string reservedWord) => RatingKeyTables.ContainsKey(reservedWord);

    /// <summary>
    /// 電気パラメータ文字列を予約語の定格キー表に照らして検証する(構造検証のみ・値は破棄)。
    /// 既存呼び出し互換のためのラッパー。値が必要な場合は
    /// <see cref="CheckParameters(string,string,out RatingValues,out string)"/> を用いる。
    /// </summary>
    public short CheckParameters(string reservedWord, string parameter, out string errorCode)
        => CheckParameters(reservedWord, parameter, out _, out errorCode);

    /// <summary>
    /// 電気パラメータ文字列を予約語の定格キー表に照らして検証し、値を <paramref name="values"/> へ格納する。
    /// 【C原典】<c>Parm_Check_Main(P_CHAR d_parm, SHORT iNo, P_CHAR ErrNo)</c>(Fyss1d.c:558)。
    ///
    /// C では <c>iNo</c>(= fyak_tbl 添字)で定格キー表を引くが、本移植では予約語名で引く。
    /// C の union <c>key_tbl</c>(1機器=1型)に相当するのが <paramref name="values"/>。
    /// TR(変圧器)は専用パーサ <see cref="TrCheckMain"/> へ分岐する(移植済み)。
    /// </summary>
    /// <param name="reservedWord">解決済み予約語(【C原典】s_yoyaku)。</param>
    /// <param name="parameter">電気パラメータ文字列(【C原典】d_parm)。</param>
    /// <param name="values">解析した定格値の格納先(【C原典】key_tbl)。</param>
    /// <param name="errorCode">エラー時に FY-xxx を格納(【C原典】ErrNo)。正常時は空。</param>
    /// <returns>0=正常 / -1=エラー(【C原典】irc)。</returns>
    public short CheckParameters(string reservedWord, string parameter, out RatingValues values, out string errorCode)
    {
        errorCode = string.Empty;
        values = new RatingValues(reservedWord);

        // 【C原典】if( strcmp( s_yoyaku, "TR" ) != 0 ) … else TR_check_main()
        if (string.Equals(reservedWord, "TR", StringComparison.Ordinal))
        {
            // 【C原典】TR_check_main() 専用パーサへ分岐(多スロット/状態付き)。
            return TrCheckMain(parameter ?? string.Empty, values, out errorCode);
        }

        if (!RatingKeyTables.TryGetValue(reservedWord, out RatingKeySpec[]? table))
        {
            // 【C原典】fyak_tbl に未登録の予約語は構造検証をスキップする。
            // 本辞書に含めていないのは特殊展開の PT/BP(空記号 len25 プレースホルダ)と
            // TR(上の専用パーサ分岐で処理)のみ。VM/TM は通常構造で検証可のため収録済み。
            // 空表(STM/SIR/C/R/D/NICA/RE/VVVF/TVZ系/SPACE/AL)は収録済みで、
            // 非空パラメータに対しては Check_1_Group の記号不一致で FY-699E となる。
            return 0;
        }

        string parm = parameter ?? string.Empty;
        int p = 0; // 【C原典】p = d_parm

        // 【C原典】while( *p != '\0' )
        while (CharAt(parm, p) != '\0')
        {
            short irc = GetOneGroup(parm, p, out int keta1, out int ketak, out string nextSymbol, out errorCode);
            if (irc == -1)
            {
                return -1; // 取得不可
            }

            irc = CheckOneGroup(parm, p, keta1, table, reservedWord, values, nextSymbol, out errorCode);
            if (irc == -1)
            {
                return -1; // チェックエラー
            }

            int length = keta1 + ketak;
            if (length <= 0)
            {
                // 無限ループ防御(C では keta1>0 が保証されるが移植上の安全策)。
                break;
            }

            p += length; // 【C原典】p += ( Length * sizeof( CHAR ))
        }

        return 0;
    }

    /// <summary>
    /// TR(変圧器)専用パーサ。【C原典】<c>TR_check_main(P_CHAR p_top, SHORT iNo, P_CHAR ErrNo)</c>(Fyss1d.c:872)。
    ///
    /// 通常の Get_1_Group/Check_1_Group ループとは別構造で、独自の走査を行う:
    ///   1グループ = 整数部＋ピリオド＋小数部＋'T'(タップ)を <c>value[irep]</c> に蓄積し、
    ///   '-'(一次)/'･'(二次以降)の区切りで繰返しスロット irep を進める。
    ///   続く記号部を定格キー表 <see cref="TransformerKeyTable"/>(ft_tr)に対しシーケンシャル照合し、
    ///   スロット0..irep の各値を <see cref="KeyCheckTr"/> で範囲検証・格納する。
    /// 状態: <c>sw_kugiri</c>(0='-'区切り / 1='･'区切り)、<c>sw_v2v3</c>(0=二次 V→v2 / 1=三次 V→v3)、
    ///   <c>ior1</c>(flag2 記号を1つ以上受理したか。0 のままなら FY-889E)。
    /// </summary>
    /// <param name="parm">電気パラメータ文字列(【C原典】p_top = d_parm)。</param>
    /// <param name="values">定格値の格納先(【C原典】key_tbl.tr)。</param>
    /// <param name="errorCode">エラーNo.(【C原典】ErrNo)。正常時は空。</param>
    /// <returns>0=正常 / -1=エラー(【C原典】irc)。</returns>
    private static short TrCheckMain(string parm, RatingValues values, out string errorCode)
    {
        errorCode = string.Empty;

        RatingKeySpec[] table = TransformerKeyTable;
        int tblIdx = 0;               // 【C原典】p_tbl = fyak_tbl[iNo].tkak_t(シーケンシャルに前進)
        int p = 0;                    // 【C原典】p = p_top
        int swKugiri = 0;             // 【C原典】sw_kugiri
        int swV2v3 = 0;               // 【C原典】sw_v2v3
        int ior1 = 0;                 // 【C原典】ior1(flag2 記号を1つ以上受理したか)
        int irep = 0;                 // 【C原典】irep(繰返し値スロット添字)
        string[] value = NewValueSlots(); // 【C原典】CHAR value[10][20]

        // 【C原典】while( *p != '\0' )
        while (CharAt(parm, p) != '\0')
        {
            int jrep = 0;             // 【C原典】現スロット内の文字数(value[irep] の長さ)
            int ketaA = 0, ketaB = 0, ketaP = 0, ketaT = 0, ketaM = 0;

            // 【C原典】整数部 スキップ(value に蓄積)
            while (IsDigit(CharAt(parm, p)))
            {
                value[irep] += CharAt(parm, p);
                p++; jrep++; ketaA++;
                if (jrep >= 20) { errorCode = "FY-886E"; return -1; } // 配列文字数 over
            }
            // 【C原典】ピリオド
            while (CharAt(parm, p) == '.')
            {
                value[irep] += CharAt(parm, p);
                p++; ketaP++;
                if (ketaP > 1) { errorCode = "FY-880E"; return -1; }  // 小数点 over
                jrep++;
                if (jrep >= 20) { errorCode = "FY-886E"; return -1; }
            }
            // 【C原典】小数部
            while (IsDigit(CharAt(parm, p)))
            {
                value[irep] += CharAt(parm, p);
                p++; jrep++; ketaB++;
                if (jrep >= 20) { errorCode = "FY-886E"; return -1; }
            }
            // 【C原典】'T'(タップ)
            while (CharAt(parm, p) == 'T')
            {
                value[irep] += CharAt(parm, p);
                p++; ketaT++;
                if (ketaT > 1) { errorCode = "FY-887E"; return -1; }  // T 連続エラー
                jrep++;
                if (jrep >= 20) { errorCode = "FY-886E"; return -1; }
            }
            // 【C原典】if( keta_a + keta_b == 0 ) FY-695E(対象外文字あり)
            if (ketaA + ketaB == 0) { errorCode = "FY-695E"; return -1; }

            // 【C原典】'-' 区切り(sw_kugiri==0)。irep を進める。
            while (CharAt(parm, p) == '-' && swKugiri == 0)
            {
                p++; ketaM++;
                if (ketaM > 1) { errorCode = "FY-888E"; return -1; }  // '-' 連続エラー
                irep++;
                if (irep >= 10) { errorCode = "FY-886E"; return -1; } // 配列数 over
            }
            // 【C原典】'･' 区切り(sw_kugiri==1)。irep を進める。
            while (CharAt(parm, p) == '･' && swKugiri == 1)
            {
                p++; ketaM++;
                if (ketaM > 1) { errorCode = "FY-881E"; return -1; }  // 区切り 連続エラー
                irep++;
                if (irep >= 10) { errorCode = "FY-886E"; return -1; }
            }

            // 【C原典】not_digit_skip( p, &keta_n ); if( keta_n == 0 ) continue;
            //   記号がまだ現れない(次に数値が続く)場合は次スロットの値取得へ戻る。
            int ketaN = NotDigitSkip(parm, p);
            if (ketaN == 0) continue;

            // 【C原典】記号照合(p_tbl はシーケンシャルに前進)。
            string kigo = Substr(parm, p, ketaN);
            while (table[tblIdx].Symbol.Length != 0)
            {
                if (kigo == table[tblIdx].Symbol)
                {
                    if (table[tblIdx].Flag == 2) ior1 = 1; // いずれか1つ必須の記号を受理
                    break;
                }
                if (table[tblIdx].Flag == 1) { errorCode = "FY-889E"; return -1; } // 必須未入力
                tblIdx++;
            }
            // 【C原典】if( p_tbl->symbol[0] == '\0' ) FY-699E(テーブルに記号なし)
            if (table[tblIdx].Symbol.Length == 0) { errorCode = "FY-699E"; return -1; }
            // 【C原典】if( p_tbl->num - 1 < irep ) FY-885E(繰返し数 over)
            if (table[tblIdx].Count - 1 < irep) { errorCode = "FY-885E"; return -1; }

            // 【C原典】strncpy( s_kigo, p, keta_n ); p += keta_n;
            p += ketaN;

            // 【C原典】for( i=0; i<irep+1; i++ ){ 桁数 check → key_check_TR }
            for (int i = 0; i < irep + 1; i++)
            {
                int len = value[i].Length;
                int mlen = table[tblIdx].Length + 1;            // +1 余裕(T)
                if (table[tblIdx].DecimalLength > 0) mlen++;
                if (mlen < len) { errorCode = "FY-882E"; return -1; } // 桁数 over
                short irc = KeyCheckTr(kigo, value[i], i, values, ref swKugiri, ref swV2v3, out errorCode);
                if (irc == -1) return -1;
            }

            // 【C原典】if( *p == '-' ){ sw_kugiri=1; sw_v2v3=1; p++; }(一次⇔二次の区切り)
            if (CharAt(parm, p) == '-')
            {
                swKugiri = 1;
                swV2v3 = 1;
                p++;
            }
            // 【C原典】if( p_tbl->symbol[0] != '\0' ) p_tbl++;
            if (table[tblIdx].Symbol.Length != 0) tblIdx++;
            irep = 0;
            value = NewValueSlots();
        }

        // 【C原典】if( ior1 == 0 ) FY-889E(V/VAC 等の必須項目 未入力)
        if (ior1 == 0) { errorCode = "FY-889E"; return -1; }

        // 【C原典】残りの p_tbl に flag==1(必須)があれば FY-889E
        if (table[tblIdx].Symbol.Length != 0)
        {
            tblIdx++;
            while (table[tblIdx].Symbol.Length != 0)
            {
                if (table[tblIdx].Flag == 1) { errorCode = "FY-889E"; return -1; }
                tblIdx++;
            }
        }

        return 0;
    }

    /// <summary>
    /// TR(変圧器)1値の範囲検証・格納。【C原典】<c>key_check_TR(CHAR val[], SHORT inum, P_CHAR ErrNo)</c>(Fyss1d.c:3329)。
    ///
    /// <c>key_tbl.tr</c>(fyrt811.h struct TR_F)の多スロットへ格納する:
    ///   p1/w1(一次) / v1[0..2]('/'定格電圧1) / p2,p3・w2,w3(二次・三次) /
    ///   fv2,v2[0..2](二次電圧) / fv3,v3[0..1](三次電圧) / va(容量, VA はそのまま・KVA は×1000)。
    /// 一次/二次の切替は <c>v1[0]</c> 登録済みか(= '/' を通過したか)で判定する(【C原典】key_tbl.tr.v1[0][0]!='\0')。
    /// </summary>
    /// <param name="symbol">照合済み記号(【C原典】s_kigo)。</param>
    /// <param name="val">値文字列(【C原典】val。'T' タップ付きを含む)。</param>
    /// <param name="inum">繰返し添字(【C原典】inum)。'/'/V/VAC の配列添字。</param>
    /// <param name="values">格納先(【C原典】key_tbl.tr)。</param>
    /// <param name="swKugiri">区切り状態(【C原典】sw_kugiri)。W 判定で 1 に更新することがある。</param>
    /// <param name="swV2v3">二次/三次 電圧の格納先切替(【C原典】sw_v2v3)。</param>
    /// <param name="errorCode">エラーNo.(【C原典】ErrNo)。</param>
    /// <returns>0=正常 / -1=エラー。</returns>
    private static short KeyCheckTr(string symbol, string val, int inum, RatingValues values, ref int swKugiri, ref int swV2v3, out string errorCode)
    {
        errorCode = string.Empty;
        int iVal = AtoiC(val);
        double fVal = AtofC(val);
        int n = val.Length; // 【C原典】n = strlen(val)(桁確認用。値は文字列で保持)
        _ = n;

        // 【C原典】if( key_tbl.tr.v1[0][0] == '\0' ) … '/' 未通過(一次側 P/W)
        if (!values.Has("v1[0]"))
        {
            if (symbol == "P")
            {
                if (iVal != 1 && iVal != 3) { errorCode = "FY-890E"; return -1; }
                if (values.Has("p1")) { errorCode = "FY-891E"; return -1; }  // 登録済み
                values.Set("p1", val);
            }
            if (symbol == "W")
            {
                if (iVal < 2 || iVal > 4) { errorCode = "FY-830E"; return -1; }
                if (values.Has("w1")) { errorCode = "FY-829E"; return -1; }
                values.Set("w1", val);
            }
        }
        else
        {
            if (symbol == "P")
            {
                if (iVal != 1 && iVal != 3) { errorCode = "FY-890E"; return -1; }
                if (!values.Has("p2"))
                {
                    values.Set("p2", val);
                }
                else
                {
                    if (values.Has("p3")) { errorCode = "FY-891E"; return -1; }
                    values.Set("p3", val);
                }
            }
            if (symbol == "W")
            {
                if (iVal < 2 || iVal > 4) { errorCode = "FY-830E"; return -1; }
                if (!values.Has("w2"))
                {
                    values.Set("w2", val);
                    if (FieldChar(values, "p2") == '1' && FieldChar(values, "w2") == '3') swKugiri = 1;
                }
                else
                {
                    if (values.Has("w3")) { errorCode = "FY-829E"; return -1; }
                    values.Set("w3", val);
                    if (FieldChar(values, "p3") == '1' && FieldChar(values, "w3") == '3') swKugiri = 1;
                }
            }
        }

        // 【C原典】'/'(定格電圧1)
        if (symbol == "/")
        {
            if (iVal < 1 || iVal > 999) { errorCode = "FY-834E"; return -1; }
            values.Set($"v1[{inum}]", val);
        }
        // 【C原典】V / VAC(二次=v2 / 三次=v3。処理は同一)
        if (symbol == "V" || symbol == "VAC")
        {
            if (iVal < 1 || iVal > 999) { errorCode = "FY-802E"; return -1; }
            if (swV2v3 == 0)
            {
                if (values.Has($"v2[{inum}]")) { errorCode = "FY-801E"; return -1; }
                values.Set("fv2", "A");
                values.Set($"v2[{inum}]", val);
            }
            else
            {
                if (values.Has($"v3[{inum}]")) { errorCode = "FY-801E"; return -1; }
                values.Set("fv3", "A");
                values.Set($"v3[{inum}]", val);
            }
        }
        // 【C原典】VA(定格容量、そのまま)
        if (symbol == "VA")
        {
            if (values.Has("va")) { errorCode = "FY-835E"; return -1; }
            if (iVal < 1 || iVal > 999) { errorCode = "FY-836E"; return -1; }
            values.Set("va", val);
        }
        // 【C原典】KVA(定格容量、×1000 して VA へ格納)
        if (symbol == "KVA")
        {
            if (values.Has("va")) { errorCode = "FY-839E"; return -1; }
            if (fVal < 0.01 || fVal > 999.99) { errorCode = "FY-840E"; return -1; }
            long lVal = (long)(fVal * 1000); // 【C原典】l_val = f_val * 1000
            values.Set("va", lVal.ToString());
        }

        return 0;
    }

    /// <summary>TR の値スロット配列(【C原典】CHAR value[10][20])を空文字で初期化して生成する。</summary>
    private static string[] NewValueSlots()
    {
        string[] slots = new string[10];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = string.Empty;
        }
        return slots;
    }

    /// <summary>格納済みフィールドの先頭文字(未登録は '\0')。【C原典】1バイトフィールド key_tbl.tr.pN/wN の参照。</summary>
    private static char FieldChar(RatingValues values, string field)
    {
        string? s = values.Get(field);
        return string.IsNullOrEmpty(s) ? '\0' : s[0];
    }

    /// <summary>
    /// 1グループ(数値部＋記号部)を切り出し桁数を数える。
    /// 【C原典】<c>Get_1_Group(P_CHAR p_top, SHORT *keta1, SHORT *ketak, P_CHAR ErrNo)</c>(Fyss1d.c:600)。
    /// </summary>
    /// <param name="s">パラメータ全体。</param>
    /// <param name="top">グループ先頭位置(【C原典】p_top)。</param>
    /// <param name="keta1">数値部(整数/ピリオド/小数/区切り)の合計桁数(【C原典】keta1)。</param>
    /// <param name="ketak">記号部の桁数(【C原典】ketak)。</param>
    /// <param name="nextSymbol">CT/VT付き('/')の副記号(【C原典】n_kigo)。'/' 以外では空。</param>
    /// <param name="errorCode">エラーNo.(【C原典】ErrNo)。</param>
    /// <returns>0=正常 / -1=エラー。</returns>
    private static short GetOneGroup(string s, int top, out int keta1, out int ketak, out string nextSymbol, out string errorCode)
    {
        keta1 = 0;
        ketak = 0;
        nextSymbol = string.Empty;
        errorCode = string.Empty;

        int p = top;
        int c = CharAt(s, p);

        // 【C原典】while( isupper( c ) == 0 ) … 英字(大文字)以外のときループ
        while (!IsUpper(c))
        {
            if (CharAt(s, p) == '/')
            {
                break;
            }

            int ketaA = DigitSkip(s, p);      // 整数部
            keta1 += ketaA;
            p += ketaA;

            int ketaP = PiriodSkip(s, p);     // ピリオド
            if (ketaP > 1)
            {
                errorCode = "FY-880E"; // ピリオド記号2個以上あり
                return -1;
            }
            if (ketaP > 0)
            {
                keta1 += ketaP;
            }
            p += ketaP;

            int ketaB = DigitSkip(s, p);      // 小数部
            keta1 += ketaB;
            p += ketaB;

            int ketaD = DelimitSkip(s, p);    // 区切り
            if (ketaD > 1)
            {
                errorCode = "FY-881E"; // 区切り2個以上あり
                return -1;
            }
            if (ketaD > 0)
            {
                keta1 += ketaD;
            }
            p += ketaD;

            if (ketaA + ketaP + ketaB + ketaD == 0)
            {
                errorCode = "FY-695E"; // 対象外文字あり
                return -1;
            }

            c = CharAt(s, p);
        }

        if (keta1 == 0)
        {
            errorCode = "FY-696E"; // 数字部なし
            return -1;
        }

        if (CharAt(s, p) == '/')
        {
            // CT/VT付きデータ('/')。【C原典】not_digit_skip( p, &ketak ); next_1_get( ++p );
            //   ketak は '/'(=記号)の桁数。'/' 直後の副記号は NextOneGet で n_kigo に取得する。
            int ketaN0 = NotDigitSkip(s, p);
            ketak = ketaN0;
            nextSymbol = NextOneGet(s, p + 1); // 【C原典】next_1_get( ++p )
        }
        else
        {
            int ketaN = NotDigitSkip(s, p);
            if (ketaN == 0)
            {
                errorCode = "FY-698E"; // 記号なし
                return -1;
            }
            ketak = ketaN;
        }

        return 0;
    }

    /// <summary>
    /// CT付き/VT付きデータの副記号取得。【C原典】<c>next_1_get(P_CHAR p_top)</c>(Fyss1d.c:677)。
    /// '/' の直後から数値部を読み飛ばし、続く記号部(非数字)を n_kigo として返す。
    /// 現状の消費先は WH の値検証 <c>key_check_WH</c>(n_kigo[0]=='A'/'V' で副定格 sa/sv を判別)であり、
    /// 同関数は E.2 で移植予定のため、本フェーズでは取得のみ行う(構造検証には影響しない)。
    /// </summary>
    /// <param name="s">パラメータ全体。</param>
    /// <param name="top">'/' の直後位置(【C原典】p_top = ++p)。</param>
    /// <returns>副記号(【C原典】n_kigo)。記号が無い場合は空文字列。</returns>
    private static string NextOneGet(string s, int top)
    {
        int p = top;
        int c = CharAt(s, p);

        // 【C原典】while( isupper( c ) == 0 ){ digit_skip; if(keta==0) break; p+=keta; c=*p; }
        while (!IsUpper(c))
        {
            int keta = DigitSkip(s, p);
            if (keta == 0)
            {
                break;
            }
            p += keta;
            c = CharAt(s, p);
        }

        // 【C原典】not_digit_skip( p, &keta ); if(keta==0) return; strncpy( n_kigo, p, keta );
        int ketaN = NotDigitSkip(s, p);
        if (ketaN == 0)
        {
            return string.Empty; // 記号なし
        }
        return Substr(s, p, ketaN);
    }

    /// <summary>
    /// 切り出した1グループを定格キー表と照合し桁・繰返数を検証する。
    /// 【C原典】<c>Check_1_Group(P_CHAR p_top, SHORT keta1, SHORT ketak, SHORT iNo, P_CHAR ErrNo)</c>(Fyss1d.c:723)。
    /// </summary>
    /// <param name="s">パラメータ全体。</param>
    /// <param name="top">グループ先頭位置(【C原典】p_top)。</param>
    /// <param name="keta1">数値部桁数(Get_1_Group の結果)。</param>
    /// <param name="table">当該予約語の定格キー表(【C原典】fyak_tbl[iNo].tkak_t)。</param>
    /// <param name="reservedWord">解決済み予約語(【C原典】s_yoyaku)。key_check の型分岐に用いる。</param>
    /// <param name="values">定格値の格納先(【C原典】key_tbl)。</param>
    /// <param name="nextSymbol">CT/VT付き('/')の副記号(【C原典】n_kigo)。値検証 key_check へ伝搬する。</param>
    /// <param name="errorCode">エラーNo.(【C原典】ErrNo)。</param>
    /// <returns>0=正常 / -1=エラー。</returns>
    private short CheckOneGroup(string s, int top, int keta1, RatingKeySpec[] table, string reservedWord, RatingValues values, string nextSymbol, out string errorCode)
    {
        errorCode = string.Empty;

        // 【C原典】p_pos = p_top + keta1; change_parm_data(ketak, p_pos, &keta)
        //          → 記号部(p_kigo)と桁数(keta)を得る。
        int symbolPos = top + keta1;
        string symbol = ChangeParmData(s, symbolPos, out int keta);

        // 【C原典】while( p_tbl->symbol[0] != '\0' ){ if(keta==n && strncmp(...)==0) break; p_tbl++; }
        RatingKeySpec? matched = null;
        foreach (RatingKeySpec spec in table)
        {
            int n = spec.Symbol.Length;
            if (keta == n && string.CompareOrdinal(symbol, 0, spec.Symbol, 0, n) == 0)
            {
                matched = spec;
                break;
            }
        }

        if (matched is null)
        {
            errorCode = "FY-699E"; // テーブルに記号なし
            return -1;
        }

        RatingKeySpec tbl = matched.Value;

        // 【C原典】s_kigo に記号を保存(本移植では以降未使用のため保持のみ省略)。

        int p = top;
        int c = CharAt(s, p);
        int inum = 0;

        // 【C原典】while( isupper( c ) == 0 ) … 記号(大文字)手前までの値群を繰り返し検証
        while (!IsUpper(c))
        {
            if (CharAt(s, p) == '/')
            {
                break;
            }

            int allKeta = 0;              // 桁数
            int valueStart = p;           // 【C原典】p_str

            int intKeta = DigitSkip(s, p); // 整数部 check
            p += intKeta;
            allKeta += intKeta;
            if (intKeta > tbl.Length - tbl.DecimalLength)
            {
                errorCode = "FY-882E"; // 桁数 over
                return -1;
            }

            int dotKeta = PiriodSkip(s, p); // ピリオド check
            p += dotKeta;
            if (dotKeta == 0)
            {
                // 【C原典】ピリオドなし: 現行はエラーとしない(原典もコメントアウト)。
            }
            else if (dotKeta == 1)
            {
                if (tbl.DecimalLength == 0)
                {
                    errorCode = "FY-883E"; // ピリオドあり(小数桁定義なし)
                    return -1;
                }
                allKeta += dotKeta;
            }
            else // dotKeta > 1
            {
                errorCode = "FY-880E"; // ピリオド over
                return -1;
            }

            int decKeta = DigitSkip(s, p); // 小数部 check
            p += decKeta;
            allKeta += decKeta;
            if (decKeta > tbl.DecimalLength)
            {
                errorCode = "FY-884E"; // 小数部桁数 over
                return -1;
            }

            if (tbl.Count <= inum)
            {
                errorCode = "FY-885E"; // 繰返し数 over
                return -1;
            }

            // 【C原典】strncpy(val, p_str, all_keta); key_check_main(val, inum, ErrNo)
            string val = Substr(s, valueStart, allKeta);
            short irc = KeyCheckMain(reservedWord, tbl.Symbol, val, inum, values, nextSymbol, out errorCode);
            if (irc == -1)
            {
                return -1;
            }

            int delimKeta = DelimitSkip(s, p);
            p += delimKeta;

            c = CharAt(s, p);
            inum++;
        }

        return 0;
    }

    /// <summary>
    /// 記号部文字列の切り出し。【C原典】<c>change_parm_data(SHORT ketak, CHAR *p_pos, SHORT *keta)</c>(Fyss1d.c:840)。
    /// C では malloc した p_kigo に ketak 文字コピーするが、本移植では記号長を末尾/文字列長で判定する。
    /// </summary>
    private static string ChangeParmData(string s, int pos, out int keta)
    {
        // 記号部は「非数字が続く区間」= not_digit_skip 相当。C の ketak と一致する。
        int len = NotDigitSkip(s, pos);
        keta = len;
        return Substr(s, pos, len);
    }

    /// <summary>
    /// 値を <see cref="RatingValues"/>(【C原典】union key_tbl / fyrt811)へ格納し範囲検証する。
    /// 【C原典】<c>key_check_main(P_CHAR val, SHORT inum, P_CHAR ErrNo)</c>(Fyss1d.c:1085)
    ///           → 型別 <c>key_check_&lt;TYPE&gt;()</c>。
    ///
    /// C は型別の巨大な if/else 連鎖(key_check_MCB 等)だが、いずれも
    /// 「記号一致 → 重複チェック(field[0]!='\0' → FY-89xE) → 範囲チェック(→ FY-xxxE)
    ///   → fv/fvc 設定 → memcpy 格納」という同一構造のため、本移植ではデータ駆動
    /// (<see cref="KeyCheckRules"/>)へ集約する。収録型: MCB/MC/MG/THR/MCDT/CSDT/SC/
    /// VM/AM/VT/CT/VS/AS/TB/CON。TR は専用パーサ <see cref="TrCheckMain"/>。
    /// 未収録の予約語は構造検証のみ(値格納なし)で正常扱いとする。
    /// </summary>
    /// <param name="reservedWord">予約語(【C原典】s_yoyaku)。型別 key_check の選択に用いる。</param>
    /// <param name="symbol">照合済みの記号(【C原典】s_kigo)。</param>
    /// <param name="val">値文字列(【C原典】val)。</param>
    /// <param name="index">繰返し添字(【C原典】inum)。E.2 収録型では未使用。</param>
    /// <param name="values">格納先(【C原典】key_tbl)。</param>
    /// <param name="nextSymbol">CT/VT付き('/')の副記号(【C原典】global n_kigo)。key_check_WH が参照する。</param>
    /// <param name="errorCode">エラーNo.(【C原典】ErrNo)。</param>
    /// <returns>0=正常 / -1=エラー。</returns>
    private short KeyCheckMain(string reservedWord, string symbol, string val, int index, RatingValues values, string nextSymbol, out string errorCode)
    {
        errorCode = string.Empty;
        _ = index; // E.2 収録型は inum 非依存(ELB/R* の ma[3][3] は後続フェーズ)。
        _ = nextSymbol; // 【C原典】key_check_main が参照する global n_kigo。消費先 key_check_WH は E.2 のため現状未使用。

        if (!KeyCheckRules.TryGetValue(reservedWord, out KeyCheckRule[]? rules))
        {
            // 本フェーズ未収録の型(MMCB/ELMB/SB/R*/NT/WH 等)は構造検証のみ。
            // TODO(続き): ELB/R*(ma[3][3] inum 添字配列)・NT(奇数丸め)・WH(n_kigo 消費)を移植。
            return 0;
        }

        // 【C原典】if( strcmp(s_kigo,"P")==0 ){ … } else if …
        KeyCheckRule? found = null;
        foreach (KeyCheckRule rule in rules)
        {
            if (Array.IndexOf(rule.Symbols, symbol) >= 0)
            {
                found = rule;
                break;
            }
        }

        if (found is null)
        {
            // 定格キー表は通過したが key_check に分岐が無い記号(防御的・実質デッドコード)。
            return 0;
        }

        KeyCheckRule r = found.Value;

        // 【C原典】if( field[0] != '\0' ){ strcpy(ErrNo,"FY-89xE"); return -1; }  … 登録済み
        if (values.Has(r.Field))
        {
            errorCode = r.DuplicateError;
            return -1;
        }

        // 【C原典】if( 範囲外 ){ strcpy(ErrNo, …); return -1; }
        if (!r.InRange(val))
        {
            errorCode = r.RangeError;
            return -1;
        }

        // 【C原典】key_tbl.xxx.fv = 'A'/'D';(交流/直流 区分)
        if (r.FvField is not null)
        {
            values.Set(r.FvField, r.FvChar.ToString());
        }

        // 【C原典】memcpy( field, val, n );
        values.Set(r.Field, val);
        return 0;
    }

    // ── スキップ補助(【C原典】Fyss1d.c:10332~) ────────────────────────────────

    /// <summary>数字部スキップ。【C原典】<c>digit_skip()</c>。連続する数字の桁数を返す。</summary>
    private static int DigitSkip(string s, int pos)
    {
        int keta = 0;
        while (IsDigit(CharAt(s, pos)))
        {
            pos++;
            keta++;
        }
        return keta;
    }

    /// <summary>
    /// 数字部以外スキップ。【C原典】<c>not_digit_skip()</c>。
    /// 数字が来るまで進むが ' '・終端・'-' で停止する。
    /// </summary>
    private static int NotDigitSkip(string s, int pos)
    {
        int keta = 0;
        while (!IsDigit(CharAt(s, pos)))
        {
            char ch = CharAt(s, pos);
            if (ch == ' ' || ch == '\0' || ch == '-')
            {
                break;
            }
            pos++;
            keta++;
        }
        return keta;
    }

    /// <summary>
    /// 区切り記号スキップ。【C原典】<c>delimit_skip()</c>。
    /// 半角中黒 '･'(CP932)の連続を数える(原典では ':' はコメントアウト)。
    /// </summary>
    private static int DelimitSkip(string s, int pos)
    {
        int keta = 0;
        while (CharAt(s, pos) == '･')
        {
            pos++;
            keta++;
        }
        return keta;
    }

    /// <summary>ピリオド記号スキップ。【C原典】<c>piriod_skip()</c>。連続する '.' を数える。</summary>
    private static int PiriodSkip(string s, int pos)
    {
        int keta = 0;
        while (CharAt(s, pos) == '.')
        {
            pos++;
            keta++;
        }
        return keta;
    }

    // ── 文字ユーティリティ(C の char ポインタ操作を index ベースへ移植) ──────────

    /// <summary>位置 pos の文字。範囲外は C の '\0'(終端)相当。</summary>
    private static char CharAt(string s, int pos) => pos >= 0 && pos < s.Length ? s[pos] : '\0';

    /// <summary>【C原典】isupper() 相当(ASCII 大文字のみ。CP932 の 2バイト目/記号は非該当)。</summary>
    private static bool IsUpper(int c) => c >= 'A' && c <= 'Z';

    /// <summary>【C原典】isdigit() 相当(ASCII 数字のみ)。</summary>
    private static bool IsDigit(int c) => c >= '0' && c <= '9';

    /// <summary>pos から length 文字の部分文字列(範囲外は切り詰め)。</summary>
    private static string Substr(string s, int pos, int length)
    {
        if (pos < 0 || pos >= s.Length || length <= 0)
        {
            return string.Empty;
        }
        int end = Math.Min(pos + length, s.Length);
        return s[pos..end];
    }

    // ── key_check データ駆動ルール(【C原典】key_check_<TYPE>() Fyss1d.c) ──────────

    /// <summary>
    /// 定格キー1記号の格納・検証ルール。【C原典】key_check_&lt;TYPE&gt;() 内の各 if/else 分岐。
    /// </summary>
    /// <param name="Symbols">この規則が受理する記号(【C原典】strcmp(s_kigo, …))。</param>
    /// <param name="Field">格納先フィールド名(【C原典】key_tbl.xxx.field)。重複判定単位。</param>
    /// <param name="InRange">範囲判定(【C原典】i_val/f_val の範囲 if)。true=正常。</param>
    /// <param name="DuplicateError">登録済みエラーNo.(【C原典】FY-89xE)。</param>
    /// <param name="RangeError">範囲外エラーNo.(【C原典】FY-xxxE)。</param>
    /// <param name="FvField">交流/直流 区分の格納先(【C原典】fv/fvc)。不要なら null。</param>
    /// <param name="FvChar">区分文字 'A'(交流)/'D'(直流)(【C原典】key_tbl.xxx.fv='A'/'D')。</param>
    private readonly record struct KeyCheckRule(
        string[] Symbols,
        string Field,
        Func<string, bool> InRange,
        string DuplicateError,
        string RangeError,
        string? FvField = null,
        char FvChar = '\0');

    /// <summary>整数範囲 [lo,hi] 判定。【C原典】i_val = atoi(val); if( i_val &lt; lo || i_val &gt; hi )。</summary>
    private static Func<string, bool> IntRange(int lo, int hi)
        => v => { int i = AtoiC(v); return i >= lo && i <= hi; };

    /// <summary>整数離散値判定。【C原典】if( i_val != a &amp;&amp; i_val != b … )。</summary>
    private static Func<string, bool> IntIn(params int[] allowed)
        => v => Array.IndexOf(allowed, AtoiC(v)) >= 0;

    /// <summary>実数範囲 [lo,hi] 判定。【C原典】f_val = atof(val); if( f_val &lt; lo || f_val &gt; hi )。</summary>
    private static Func<string, bool> FloatRange(double lo, double hi)
        => v => { double f = AtofC(v); return f >= lo && f <= hi; };

    /// <summary>
    /// 予約語別 key_check ルール表。【C原典】key_check_MCB/MC/MG/THR/MCDT/CSDT/SC(Fyss1d.c)。
    /// 範囲値の改訂タグ(改訂&lt;3&gt;/&lt;5&gt;等)や離散許容値は C 原典を忠実に反映する。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, KeyCheckRule[]> KeyCheckRules =
        new Dictionary<string, KeyCheckRule[]>(StringComparer.Ordinal)
        {
            // 【C原典】key_check_MCB(Fyss1d.c:1421)
            ["MCB"] =
            [
                new(["P"], "p", IntRange(1, 4), "FY-890E", "FY-891E"),
                new(["E"], "e", IntRange(0, 4), "FY-892E", "FY-893E"),   // 改訂<3> 0..4
                new(["AF"], "af", IntRange(1, 1200), "FY-894E", "FY-895E"),
                new(["AT", "A"], "at", IntRange(0, 1200), "FY-899E", "FY-800E"),
                new(["VAC", "V"], "v", IntRange(1, 690), "FY-801E", "FY-802E", "fv", 'A'), // 改訂<5> 1..690
                new(["VDC"], "v", IntRange(1, 600), "FY-801E", "FY-802E", "fv", 'D'),      // 改訂<3> 1..600
            ],
            // 【C原典】key_check_MC(Fyss1d.c:2348)
            ["MC"] =
            [
                new(["P"], "p", IntRange(1, 3), "FY-890E", "FY-891E"),
                new(["A"], "a", IntRange(1, 800), "FY-815E", "FY-816E"),
                new(["VAC", "V"], "v", IntRange(1, 550), "FY-801E", "FY-802E", "fv", 'A'),
                new(["VDC"], "v", IntRange(1, 550), "FY-801E", "FY-802E", "fv", 'D'),
                new(["VCAC", "VC"], "vc", IntRange(1, 240), "FY-813E", "FY-814E", "fvc", 'A'),
                new(["VCDC"], "vc", IntRange(1, 120), "FY-813E", "FY-814E", "fvc", 'D'),
                new(["KW"], "kw", FloatRange(0.01, 999.00), "FY-811E", "FY-812E"),
                new(["AC"], "ac", IntRange(1, 3), "FY-817E", "FY-818E"),
                new(["BC"], "bc", IntRange(1, 3), "FY-819E", "FY-820E"),
            ],
            // 【C原典】key_check_THR(Fyss1d.c:2469)
            ["THR"] =
            [
                new(["E"], "e", IntIn(0, 2, 3), "FY-892E", "FY-893E"),
                new(["AT"], "at", FloatRange(0.01, 500.00), "FY-899E", "FY-800E"),
                new(["VAC", "V"], "v", IntRange(1, 999), "FY-801E", "FY-802E", "fv", 'A'),
                new(["KW"], "kw", FloatRange(0.01, 140.00), "FY-811E", "FY-812E"),
            ],
            // 【C原典】key_check_MG(Fyss1d.c:2535)
            ["MG"] =
            [
                new(["P"], "p", IntIn(3), "FY-890E", "FY-891E"),
                new(["E"], "e", IntRange(0, 3), "FY-892E", "FY-893E"),
                new(["AT"], "at", FloatRange(0.01, 500.00), "FY-899E", "FY-800E"),
                new(["VAC", "V"], "v", IntRange(1, 550), "FY-801E", "FY-802E", "fv", 'A'),
                new(["KW"], "kw", FloatRange(0.01, 140.00), "FY-811E", "FY-812E"),
                new(["A"], "a", IntRange(1, 800), "FY-815E", "FY-816E"),
                new(["VCAC", "VC"], "vc", IntRange(1, 240), "FY-813E", "FY-814E", "fvc", 'A'),
                new(["VCDC"], "vc", FloatRange(1, 120), "FY-813E", "FY-814E", "fvc", 'D'), // MG のみ f_val 判定
                new(["AC"], "ac", IntRange(1, 3), "FY-817E", "FY-818E"),
                new(["BC"], "bc", IntRange(1, 3), "FY-819E", "FY-820E"),
            ],
            // 【C原典】key_check_MCDT(Fyss1d.c:3913) … Ele_Equal_Check step3 対象
            ["MCDT"] =
            [
                new(["P"], "p", IntRange(2, 4), "FY-890E", "FY-891E"),
                new(["A"], "a", IntRange(1, 800), "FY-815E", "FY-816E"),
                new(["V", "VAC"], "v", IntRange(1, 600), "FY-801E", "FY-802E", "fv", 'A'),
                new(["VDC"], "v", IntRange(1, 125), "FY-801E", "FY-802E", "fv", 'D'),
                new(["VC", "VCAC"], "vc", IntRange(1, 240), "FY-813E", "FY-814E", "fvc", 'A'),
                new(["VCDC"], "vc", IntRange(1, 125), "FY-813E", "FY-814E", "fvc", 'D'),
            ],
            // 【C原典】key_check_CSDT(Fyss1d.c:5409) … Ele_Equal_Check step3 対象
            ["CSDT"] =
            [
                new(["P"], "p", IntRange(2, 3), "FY-890E", "FY-891E"),
                new(["A"], "a", IntRange(1, 600), "FY-815E", "FY-816E"),
                new(["VAC", "V"], "v", IntRange(1, 600), "FY-801E", "FY-802E", "fv", 'A'),
                new(["VDC"], "v", IntRange(1, 250), "FY-801E", "FY-802E", "fv", 'D'),
            ],
            // 【C原典】key_check_SC(Fyss1d.c:2680 付近)
            ["SC"] =
            [
                new(["P"], "p", IntIn(1, 3), "FY-890E", "FY-891E"),
                new(["HZ"], "hz", IntIn(50, 60), "FY-823E", "FY-824E"),
                new(["VAC", "V"], "v", IntRange(1, 500), "FY-801E", "FY-802E", "fv", 'A'),
                new(["KVAR"], "kvar", FloatRange(0.01, 150.00), "FY-825E", "FY-826E"),
                new(["UF"], "uf", FloatRange(1.0, 3000.0), "FY-827E", "FY-828E"),
            ],
            // 【C原典】key_check_VM(Fyss1d.c:2920) … 電圧計。"/" は二次電圧 sv。
            ["VM"] =
            [
                new(["VAC", "V"], "v", IntRange(1, 999), "FY-801E", "FY-802E", "fv", 'A'),
                new(["VDC"], "v", IntRange(1, 150), "FY-801E", "FY-802E", "fv", 'D'),
                new(["/"], "sv", IntRange(1, 999), "FY-833E", "FY-834E"),
            ],
            // 【C原典】key_check_AM(Fyss1d.c:2973) … 電流計。"/" は二次電流 sa。
            ["AM"] =
            [
                new(["A"], "a", IntRange(1, 999), "FY-815E", "FY-816E"),
                new(["/"], "sa", IntRange(1, 999), "FY-831E", "FY-832E"),
            ],
            // 【C原典】key_check_VT(Fyss1d.c:3011) … 計器用変圧器。
            ["VT"] =
            [
                new(["V", "VAC"], "v", IntRange(1, 110), "FY-801E", "FY-802E", "fv", 'A'),
                new(["/"], "sv", IntRange(1, 440), "FY-833E", "FY-834E"),
                new(["VA"], "va", IntRange(1, 500), "FY-835E", "FY-836E"),
            ],
            // 【C原典】key_check_CT(Fyss1d.c:3062) … 変流器。
            ["CT"] =
            [
                new(["A"], "a", IntRange(1, 999), "FY-815E", "FY-816E"),
                new(["/"], "sa", IntRange(1, 1200), "FY-831E", "FY-832E"),
                new(["VA"], "va", IntRange(1, 40), "FY-835E", "FY-836E"),
            ],
            // 【C原典】key_check_VS(Fyss1d.c:3111) … 電圧切替スイッチ。
            ["VS"] =
            [
                new(["P"], "p", IntIn(1, 3), "FY-890E", "FY-891E"),
                new(["W"], "w", IntIn(3, 4), "FY-829E", "FY-830E"),
            ],
            // 【C原典】key_check_AS(Fyss1d.c:3148) … 電流切替スイッチ(VS と同一構造)。
            ["AS"] =
            [
                new(["P"], "p", IntIn(1, 3), "FY-890E", "FY-891E"),
                new(["W"], "w", IntIn(3, 4), "FY-829E", "FY-830E"),
            ],
            // 【C原典】key_check_TB(Fyss1d.c:3185) … 端子台。
            // 改訂<6><10>: P の下限 i_min は実行時グローバル(G_GYOSYU=="MP"&&G_TYPE_ET==1 または
            // G_TB_800A==1 で 1、それ以外 2)。本移植では業種文脈が無いため既定 i_min=2 を採用。
            ["TB"] =
            [
                new(["P"], "p", IntRange(2, 100), "FY-890E", "FY-891E"),
                new(["A"], "a", IntRange(1, 800), "FY-815E", "FY-816E"),
                new(["V", "VAC"], "v", IntRange(1, 600), "FY-801E", "FY-802E", "fv", 'A'),
                new(["VDC"], "v", IntRange(1, 600), "FY-801E", "FY-802E", "fv", 'D'),
                new(["SQ"], "sq", FloatRange(0.01, 400.00), "FY-837E", "FY-838E"),
            ],
            // 【C原典】key_check_CON(Fyss1d.c:3265) … コンセント。
            ["CON"] =
            [
                new(["P"], "p", IntIn(2, 3), "FY-890E", "FY-891E"),
                new(["A"], "a", IntRange(1, 99), "FY-815E", "FY-816E"),
                new(["V", "VAC"], "v", IntRange(1, 250), "FY-801E", "FY-802E", "fv", 'A'),
                new(["VDC"], "v", IntRange(1, 125), "FY-801E", "FY-802E", "fv", 'D'),
            ],
        };

    // ── 数値変換(C の atoi/atof セマンティクス) ──────────────────────────────

    /// <summary>
    /// C の <c>atoi()</c> 相当。先頭空白/符号を許容し、数字が続く間だけを整数化する
    /// (非数字で打ち切り。'.' 以降は無視)。
    /// </summary>
    private static int AtoiC(string s)
    {
        int i = 0;
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
        int sign = 1;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            if (s[i] == '-') sign = -1;
            i++;
        }
        long value = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            value = value * 10 + (s[i] - '0');
            i++;
        }
        return (int)(sign * value);
    }

    /// <summary>
    /// C の <c>atof()</c> 相当。先頭の数値部(符号・整数・小数)を実数化する。
    /// </summary>
    private static double AtofC(string s)
    {
        int i = 0;
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
        int start = i;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
        if (i < s.Length && s[i] == '.')
        {
            i++;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
        }
        string num = s[start..i];
        return double.TryParse(num, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double f) ? f : 0.0;
    }
}

