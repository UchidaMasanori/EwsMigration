namespace Ews.Analysis;

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
/// 【定格キー表】toku/include/sin/FySinTkakt.h
///   - TKAK_T{symbol[5],len,d_len,num,flag}   → <see cref="RatingKeySpec"/>
///   - TKAK_TBL{yoyaku[8],flag,tkak_t*}        → <see cref="RatingKeyTables"/> の辞書エントリ
///   - t_mcb[]/t_elb[]… 約100表(予約語→定格キー表)。
///
/// フェーズE.1で <b>型非依存の中核パーサ(構造検証)</b> を、フェーズE.2で
/// <b>値格納・範囲検証(key_check)</b> を忠実移植する。
/// 個々の値は <see cref="RatingValues"/>(【C原典】union key_tbl / fyrt811)へ格納し、
/// <see cref="KeyCheckMain"/> が型別ルール(<see cref="KeyCheckRules"/>)で重複・範囲を検証する。
/// E.2では MCB/MC/MG/THR/MCDT/CSDT/SC を収録(いずれも走査単位が単純な機種)。
/// MA[3][3] 等の inum 索引配列を持つ ELB/R* や、奇数丸め特殊処理の NT、
/// および TR(変圧器)専用パーサ TR_check_main()・CT/VT付き('/')・特殊展開(VM/TM/WH)は
/// 後続フェーズで対応する。
/// </summary>
public sealed class ElectricalParameterChecker
{
    /// <summary>
    /// 定格キー1記号の仕様。【C原典】FySinTkakt.h の <c>TKAK_T</c> 構造体。
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
    /// 予約語 → 定格キー表 の対応辞書。【C原典】FySinTkakt.h の <c>tkak_tbl[]</c> と各 <c>t_xxx[]</c>。
    /// 値はメンバ表(TKAK_T[])。末尾の NULL 番兵({NULL,0,0,0,0})は移植しない(配列長で判定)。
    ///
    /// 本フェーズ(E.1)では遮断器・電磁接触器など基本機種の第1バッチのみ収録する。
    /// 残り(約90種)および特殊展開(VM/TM/WH の '/' を含む表)・TR は後続フェーズで追加する。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, RatingKeySpec[]> RatingKeyTables =
        new Dictionary<string, RatingKeySpec[]>(StringComparer.Ordinal)
        {
            // t_mcb[] … MCB
            ["MCB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 4, 0, 1, 0),
                new("AT", 4, 0, 1, 0),
                new("V", 3, 0, 1, 1),
                new("VAC", 3, 0, 1, 1),
                new("VDC", 3, 0, 1, 1),
            ],
            // t_elb[] … ELB
            ["ELB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 4, 0, 1, 0),
                new("AT", 4, 0, 1, 0),
                new("MA", 3, 0, 3, 0),
                new("VAC", 3, 0, 4, 0),
            ],
            // t_mmcb[] … MMCB
            ["MMCB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 3, 0, 1, 0),
                new("AT", 5, 2, 1, 0),
                new("VAC", 3, 0, 1, 0),
            ],
            // t_elmb[] … ELMB
            ["ELMB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 3, 0, 1, 0),
                new("AT", 5, 2, 1, 0),
                new("MA", 3, 0, 3, 0),
                new("VAC", 3, 0, 4, 0),
            ],
            // t_sb[] … SB
            ["SB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 2, 0, 1, 0),
                new("V", 3, 0, 1, 1),
                new("VAC", 3, 0, 1, 1),
                new("VDC", 3, 0, 1, 1),
            ],
            // t_rmcb[] … 予約語 "RECB"(tkak_tbl 上の予約語名は RECB)
            ["RECB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 2, 0, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 2, 0),
            ],
            // t_relb[] … 予約語 "RELB"
            ["RELB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 2, 0, 1, 0),
                new("MA", 3, 0, 3, 0),
                new("VAC", 3, 0, 4, 0),
                new("VCAC", 3, 0, 2, 0),
            ],
            // t_rmmcb[] … 予約語 "RMMCB"
            ["RMMCB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 4, 2, 1, 0),
                new("VAC", 3, 0, 1, 0),
                new("VCAC", 3, 0, 2, 0),
            ],
            // t_relmb[] … 予約語 "RELMB"
            ["RELMB"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("AF", 2, 0, 1, 0),
                new("AT", 4, 2, 1, 0),
                new("MA", 3, 0, 3, 0),
                new("VAC", 3, 0, 4, 0),
                new("VCAC", 3, 0, 2, 0),
            ],
            // t_mc[] … MC(AC/BC は特殊コメント ########## 付きだが構造検証上は通常記号)
            ["MC"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 1),
                new("VAC", 3, 0, 1, 1),
                new("VDC", 3, 0, 1, 1),
                new("VC", 3, 0, 2, 1),
                new("VCAC", 3, 0, 2, 1),
                new("VCDC", 3, 0, 2, 1),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
            ],
            // t_thr[] … THR
            ["THR"] =
            [
                new("E", 1, 0, 1, 0),
                new("AF", 5, 2, 1, 0),
                new("AT", 5, 2, 2, 0),
                new("VAC", 3, 0, 1, 0),
            ],
            // t_mg[] … MG
            ["MG"] =
            [
                new("P", 1, 0, 1, 0),
                new("E", 1, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("AF", 5, 2, 1, 0),
                new("AT", 5, 2, 2, 0),
                new("VAC", 3, 0, 1, 0),
                new("VC", 3, 0, 2, 1),
                new("VCAC", 3, 0, 2, 1),
                new("VCDC", 3, 0, 2, 1),
                new("AC", 1, 0, 1, 1),
                new("BC", 1, 0, 1, 1),
            ],
            // t_sc[] … SC
            ["SC"] =
            [
                new("P", 1, 0, 1, 0),
                new("KVAR", 5, 2, 1, 0),
                new("UF", 5, 1, 1, 0),
                new("VAC", 3, 0, 2, 0),
                new("HZ", 2, 0, 1, 0),
            ],
            // t_nt[] … NT
            ["NT"] =
            [
                new("P", 3, 0, 1, 0),
                new("A", 2, 0, 1, 0),
                new("V", 3, 0, 1, 1),
                new("VAC", 3, 0, 1, 1),
                new("VDC", 3, 0, 1, 1),
            ],
            // t_mcdt[] … MCDT(電源切替開閉器 / Ele_Equal_Check step3 対象)
            ["MCDT"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 1),
                new("VAC", 3, 0, 1, 1),
                new("VDC", 3, 0, 1, 1),
                new("VC", 3, 0, 2, 1),
                new("VCAC", 3, 0, 2, 1),
                new("VCDC", 3, 0, 2, 1),
            ],
            // t_csdt[] … CSDT(切替カバースイッチ / Ele_Equal_Check step3 対象)
            ["CSDT"] =
            [
                new("P", 1, 0, 1, 0),
                new("A", 3, 0, 1, 0),
                new("V", 3, 0, 1, 1),
                new("VAC", 3, 0, 1, 1),
                new("VDC", 3, 0, 1, 1),
            ],
        };

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
    /// TR(変圧器)は専用パーサ <c>TR_check_main()</c> へ分岐する(本フェーズ未実装 → TODO)。
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
            // TODO(E.2続き): TR_check_main() の移植。
            // TR は独自の多スロット構造(p1/p2/p3, w1/w2/w3, v1[][], v2[][], v3[][], va,
            // sw_kugiri/sw_v2v3 状態)+ key_check_TR + t_tr(flag 0/1/2) を持つため、
            // 本フェーズの単純フィールド辞書モデルには収まらない。別途対応。
            // 参照: Fyss1d.c TR_check_main@873 / key_check_TR@3329 / FySinTkakt.h t_tr。
            return 0;
        }

        if (!RatingKeyTables.TryGetValue(reservedWord, out RatingKeySpec[]? table))
        {
            // 本フェーズ未収録の予約語は構造検証をスキップ(後続フェーズで表を追加)。
            // TODO(E.1続き): 残りの定格キー表(約90種)を FySinTkakt.h から移植。
            return 0;
        }

        string parm = parameter ?? string.Empty;
        int p = 0; // 【C原典】p = d_parm

        // 【C原典】while( *p != '\0' )
        while (CharAt(parm, p) != '\0')
        {
            short irc = GetOneGroup(parm, p, out int keta1, out int ketak, out errorCode);
            if (irc == -1)
            {
                return -1; // 取得不可
            }

            irc = CheckOneGroup(parm, p, keta1, table, reservedWord, values, out errorCode);
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
    /// 1グループ(数値部＋記号部)を切り出し桁数を数える。
    /// 【C原典】<c>Get_1_Group(P_CHAR p_top, SHORT *keta1, SHORT *ketak, P_CHAR ErrNo)</c>(Fyss1d.c:600)。
    /// </summary>
    /// <param name="s">パラメータ全体。</param>
    /// <param name="top">グループ先頭位置(【C原典】p_top)。</param>
    /// <param name="keta1">数値部(整数/ピリオド/小数/区切り)の合計桁数(【C原典】keta1)。</param>
    /// <param name="ketak">記号部の桁数(【C原典】ketak)。</param>
    /// <param name="errorCode">エラーNo.(【C原典】ErrNo)。</param>
    /// <returns>0=正常 / -1=エラー。</returns>
    private static short GetOneGroup(string s, int top, out int keta1, out int ketak, out string errorCode)
    {
        keta1 = 0;
        ketak = 0;
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
            // CT/VT付きデータ('/')。【C原典】not_digit_skip + next_1_get(++p)。
            // TODO(E.2): next_1_get()(n_kigo 設定)の移植。本フェーズは記号長のみ計上。
            int ketaN0 = NotDigitSkip(s, p);
            ketak = ketaN0;
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
    /// 切り出した1グループを定格キー表と照合し桁・繰返数を検証する。
    /// 【C原典】<c>Check_1_Group(P_CHAR p_top, SHORT keta1, SHORT ketak, SHORT iNo, P_CHAR ErrNo)</c>(Fyss1d.c:723)。
    /// </summary>
    /// <param name="s">パラメータ全体。</param>
    /// <param name="top">グループ先頭位置(【C原典】p_top)。</param>
    /// <param name="keta1">数値部桁数(Get_1_Group の結果)。</param>
    /// <param name="table">当該予約語の定格キー表(【C原典】fyak_tbl[iNo].tkak_t)。</param>
    /// <param name="reservedWord">解決済み予約語(【C原典】s_yoyaku)。key_check の型分岐に用いる。</param>
    /// <param name="values">定格値の格納先(【C原典】key_tbl)。</param>
    /// <param name="errorCode">エラーNo.(【C原典】ErrNo)。</param>
    /// <returns>0=正常 / -1=エラー。</returns>
    private short CheckOneGroup(string s, int top, int keta1, RatingKeySpec[] table, string reservedWord, RatingValues values, out string errorCode)
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
            short irc = KeyCheckMain(reservedWord, tbl.Symbol, val, inum, values, out errorCode);
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
    /// (<see cref="KeyCheckRules"/>)へ集約する。E.2 収録型: MCB/MC/MG/THR/MCDT/CSDT/SC。
    /// 未収録の予約語は構造検証のみ(値格納なし)で正常扱いとする。
    /// </summary>
    /// <param name="reservedWord">予約語(【C原典】s_yoyaku)。型別 key_check の選択に用いる。</param>
    /// <param name="symbol">照合済みの記号(【C原典】s_kigo)。</param>
    /// <param name="val">値文字列(【C原典】val)。</param>
    /// <param name="index">繰返し添字(【C原典】inum)。E.2 収録型では未使用。</param>
    /// <param name="values">格納先(【C原典】key_tbl)。</param>
    /// <param name="errorCode">エラーNo.(【C原典】ErrNo)。</param>
    /// <returns>0=正常 / -1=エラー。</returns>
    private short KeyCheckMain(string reservedWord, string symbol, string val, int index, RatingValues values, out string errorCode)
    {
        errorCode = string.Empty;
        _ = index; // E.2 収録型は inum 非依存(ELB/R* の ma[3][3] は後続フェーズ)。

        if (!KeyCheckRules.TryGetValue(reservedWord, out KeyCheckRule[]? rules))
        {
            // 本フェーズ未収録の型(MMCB/ELMB/SB/R*/NT 等)は構造検証のみ。
            // TODO(E.2続き): ELB/R*(ma[3][3] inum 添字配列)・NT(奇数丸め)・TR を移植。
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

/// <summary>
/// 1機器分の定格値ホルダ。【C原典】<c>union fyrt811</c> / グローバル <c>key_tbl</c>。
///
/// C は約90型を union で重ねるが、1機器につき有効な型は <c>s_yoyaku</c> で定まる1つのみ。
/// 本移植では「フィールド名 → 格納値文字列」の辞書で表現する。
/// C の未登録判定 <c>field[0] != '\0'</c> は <see cref="Has"/> に対応する。
/// 交流/直流 区分 fv/fvc も通常フィールド("fv"/"fvc")として格納する。
/// この表現により Ele_Equal_Check(step3)の型別フィールド比較を素直に実装できる。
/// </summary>
public sealed class RatingValues
{
    private readonly Dictionary<string, string> _fields = new(StringComparer.Ordinal);

    /// <summary>予約語(型名)を指定して生成する。【C原典】s_yoyaku。</summary>
    public RatingValues(string typeName) => TypeName = typeName;

    /// <summary>予約語(型名)。【C原典】s_yoyaku。</summary>
    public string TypeName { get; }

    /// <summary>格納済みフィールドの読み取り専用ビュー。</summary>
    public IReadOnlyDictionary<string, string> Fields => _fields;

    /// <summary>フィールドが登録済みか。【C原典】<c>field[0] != '\0'</c>。</summary>
    public bool Has(string field) => _fields.TryGetValue(field, out string? v) && v.Length > 0;

    /// <summary>フィールド値を取得する(未登録は null)。</summary>
    public string? Get(string field) => _fields.TryGetValue(field, out string? v) ? v : null;

    /// <summary>フィールド値を格納する。【C原典】<c>memcpy( field, val, n )</c>。</summary>
    public void Set(string field, string value) => _fields[field] = value;
}

