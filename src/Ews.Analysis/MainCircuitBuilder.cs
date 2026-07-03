using Ews.Domain.Analysis;
using Ews.Domain.Circuits;

namespace Ews.Analysis;

/// <summary>
/// 主回路生成(系統/行種/仕様/機器テーブル → 主回路設計エリア FYRT800)。
///
/// 【C原典】toku/sekkei/src/Fyss12.c
///   - 入口  : Fyss12_Make_Main()      … <see cref="CircuitStringChecker"/>(Fyss11)が生成した
///             系統(KEITOU)/行種(GYOSYU)/仕様(SPEC)/機器(KIKITABLE)テーブルを受け取り、
///             各種チェック→ランク付け→主回路設計エリア(FYRT800)の生成までを統括する。
///   - 中核  : Fyss12_Make_Main_Sub()  … 主回路ファイルエリア作成/数量分解(段階移植)。
///
/// 本クラスでは <b>統括処理(MakeMain)の骨組みと系統チェック(Keitou_Check)を忠実移植</b>し、
/// 行種関連チェック以降(ランク付け/ソート/グループセット/主回路生成)は段階移植のため
/// TODO として明示する。
/// </summary>
public sealed class MainCircuitBuilder
{
    /// <summary>
    /// 行種シンボル。【C原典】Fyss12.c の typedef enum _SYMBOL(序数はC原典と一致)。
    /// <see cref="FindLineTypeSymbol"/>(Find_Gyosyu_Sym)が行種文字列を分類する。
    /// </summary>
    private enum LineTypeSymbol : short
    {
        /// <summary>その他。【C原典】sym_OTHER。</summary>
        Other = 0,

        /// <summary>盤タイトル銘板文。【C原典】sym_NP。</summary>
        Np = 1,

        /// <summary>盤名称文。【C原典】sym_BN。</summary>
        Bn = 2,

        /// <summary>予約語(O)。【C原典】sym_O。</summary>
        O = 3,

        /// <summary>入線文。【C原典】sym_P。</summary>
        P = 4,

        /// <summary>入線分岐文。【C原典】sym_PS。</summary>
        Ps = 5,

        /// <summary>有電源文。【C原典】sym_UP。</summary>
        Up = 6,

        /// <summary>コントロール文。【C原典】sym_C。</summary>
        C = 7,

        /// <summary>予約語(TM)。【C原典】sym_TM。</summary>
        Tm = 8,

        /// <summary>予約語(M)。【C原典】sym_M。</summary>
        M = 9,

        /// <summary>予約語(SM)。【C原典】sym_SM。</summary>
        Sm = 10,

        /// <summary>予約語(B)。【C原典】sym_B。</summary>
        B = 11,

        /// <summary>予約語(BO)。【C原典】sym_BO。</summary>
        Bo = 12,

        /// <summary>予約語(PM)。【C原典】sym_PM。</summary>
        Pm = 13,

        /// <summary>MP系統文。【C原典】sym_MP。</summary>
        Mp = 14,

        /// <summary>予約語(S)。【C原典】sym_S。</summary>
        S = 15,

        /// <summary>SP系統文。【C原典】sym_SP。</summary>
        Sp = 16,

        /// <summary>セパレーター文。【C原典】sym_SEP。</summary>
        Sep = 17,

        /// <summary>終了。【C原典】sym_END。</summary>
        End = 18,
    }

    /// <summary>
    /// 主回路生成の統括処理。
    /// 【C原典】Fyss12_Make_Main()。系統/行種/仕様/機器テーブルを受け取り、
    /// 17段の処理を順に実行する。エラー検出時は <paramref name="parse"/> の
    /// Errors へ追加し、非0(=2)を返して打ち切る。
    /// </summary>
    /// <param name="parse">Fyss11 が生成した解析結果(各テーブルとエラー領域)。</param>
    /// <param name="designArea">
    /// 回路設計エリア(正規化済の回路内容記述)。【C原典】imagec/imagea(struct FYDF805)。
    /// 改訂&lt;15&gt; の Gyosyu_Check(PM行例外判定)で参照する。未指定時は当該例外を評価しない。
    /// </param>
    /// <returns>0=正常, 2=エラー打切り。【C原典】SHORT ret。</returns>
    public short MakeMain(CircuitParseResult parse, IReadOnlyList<CircuitDescriptionLine>? designArea = null)
    {
        ArgumentNullException.ThrowIfNull(parse);

        short ret;

        // 1. 系統チェック。【C原典】Keitou_Check()。
        ret = CheckSystemComposition(parse);
        if (ret != 0) return ret;

        // 2. 行種関連チェック。【C原典】Gyosyu_Check()。改訂<15>で回路設計エリア(imagea)を参照。
        ret = CheckLineTypeHierarchy(parse, designArea);
        if (ret != 0) return ret;

        // 3. 電気パラメータ同一チェック。【C原典】Ele_Equal_Check()。
        //    C原典では前段(この位置)の呼び出しはコメントアウト(無効化)されており、
        //    実際の実行は後段(step16, ソート・機器追加後)。本移植も後段で実行する。

        // 4. 機器情報関連チェック。【C原典】Yoyakugo_Check_Double()。
        ret = CheckEquipmentInformation(parse);
        if (ret != 0) return ret;

        // 5. 回路区分セット。【C原典】Kairo_Kubun_Set()。機器の K_Kubun を設定する(エラー返却なし)。
        SetCircuitDivision(parse);
        // 6. 機器(SEP,CT,WH,ZCT)の追加。【C原典】Yoyakugo_Add_Main()。TODO。
        // 7. 機器テーブルソート。【C原典】qsort(...,cmp)。TODO。
        // 8. 行種ランクセット。【C原典】Gyosyu_Rank_Set()。TODO。
        // 9. 機器ランクセット。【C原典】Kiki_Rank_Set()。TODO。
        // 10. 機器ランク更新。【C原典】Kiki_Rank_Update()。TODO。
        // 11. 行種ランク更新。【C原典】Gyosyu_Rank_Update()。TODO。
        // 12. パターンのランク更新。【C原典】Pattern_Rank_Update()。TODO。
        // 13. トランスのランク再設定。【C原典】TR_Rank_Set()。TODO。
        // 13.5 WH のランク再設定。【C原典】WH_Rank_Set()(改訂<14>)。TODO。
        // 14. グループセット。【C原典】Kairo_Group_Set()(C原典でも無効化)。
        // 15/16. 同一機器認識番号セット。【C原典】Kiki_Equal_Bangou_Set()(C原典でも無効化)。

        // 16. 電気パラメータ同一チェック。【C原典】Ele_Equal_Check()(有効な後段呼び出し)。
        ret = CheckElectricalParameterEquality(parse);
        if (ret != 0) return ret;

        // 17. 主回路ファイルエリア作成/数量分解。【C原典】Fyss12_Make_Main_Sub()。TODO。
        //     入力順チェック。【C原典】Fyss1m_Input_Check()。TODO。
        // 19. ＩＮＶＢＰの区分設定。【C原典】PropSetInvbpKbn(*Pmainc,Pmaina,imagec,imagea)(改訂<16>)。TODO。

        return 0;
    }

    /// <summary>
    /// 系統チェック(系統内に存在すべき/できる行種の検証)。
    /// 【C原典】Keitou_Check()。系統(K_No)ごとに次を検証する。
    ///   ・盤タイトル(BN)は系統内に1つ以下, 入線直上(FY-671E/FY-672E)
    ///   ・系統終了(SEP)は系統内に1つ以下, 入線直上(FY-673E/FY-674E)
    ///   ・入線分岐(PS)は系統内に2つ(1つ=FY-679E, 3つ以上=FY-678E)
    ///   ・系統種別(K_kind)ごとに許可された行種のみ存在可(FY-675E)
    /// 盤銘板名称(NP)→BANTTLワークファイル出力(Find_NP/fwrite)は出力副作用であり
    /// 検証本体ではないため段階移植とする(TODO)。
    /// </summary>
    private short CheckSystemComposition(CircuitParseResult parse)
    {
        List<LineTypeTableEntry> lineTypes = parse.LineTypes;

        // 【C原典】盤タイトルチェック(BN)。
        //   BN は入線直上に1つ以下。BN 検出後、同一系統内で NP/C 以外が現れると FY-672E。
        short kNo = 0;
        bool existBn = false;
        foreach (LineTypeTableEntry g in lineTypes)
        {
            LineTypeSymbol sym = FindLineTypeSymbol(g.LineType);
            if (kNo != g.SystemNumber) { existBn = false; kNo = g.SystemNumber; }

            if (sym == LineTypeSymbol.Bn)
            {
                if (existBn) { AddError(parse, "FY-671E", g); return 2; }
                existBn = true;
            }
            else if (existBn && sym != LineTypeSymbol.Np && sym != LineTypeSymbol.C)
            {
                AddError(parse, "FY-672E", g);
                return 2;
            }
        }

        // 【C原典】系統終了チェック(SEP)。
        //   SEP は入線直上に1つ以下。SEP 検出後、同一系統内で NP/C/BN 以外が現れると FY-674E。
        kNo = 0;
        bool existSep = false;
        foreach (LineTypeTableEntry g in lineTypes)
        {
            LineTypeSymbol sym = FindLineTypeSymbol(g.LineType);
            if (kNo != g.SystemNumber) { existSep = false; kNo = g.SystemNumber; }

            if (sym == LineTypeSymbol.Sep)
            {
                if (existSep) { AddError(parse, "FY-673E", g); return 2; }
                existSep = true;
            }
            else if (existSep
                     && sym != LineTypeSymbol.Np
                     && sym != LineTypeSymbol.C
                     && sym != LineTypeSymbol.Bn)
            {
                AddError(parse, "FY-674E", g);
                return 2;
            }
        }

        // 【C原典】系統内ＰＳチェック。
        //   PS は系統内に2つ存在できる。1つのみ=FY-679E, 3つ以上=FY-678E(0=許可)。
        //   系統切替時は直前系統の累計を新系統行の行番号で報告する(C原典に忠実)。
        kNo = 0;
        short existPs = 0;
        LineTypeTableEntry? lastGyosyu = null;
        foreach (LineTypeTableEntry g in lineTypes)
        {
            LineTypeSymbol sym = FindLineTypeSymbol(g.LineType);
            if (kNo != g.SystemNumber)
            {
                if (existPs == 1) { AddError(parse, "FY-679E", g); return 2; }
                if (existPs > 2) { AddError(parse, "FY-678E", g); return 2; }
                existPs = 0;
                kNo = g.SystemNumber;
            }
            if (sym == LineTypeSymbol.Ps) { existPs++; }
            lastGyosyu = g;
        }
        if (lastGyosyu is not null)
        {
            if (existPs == 1) { AddError(parse, "FY-679E", lastGyosyu); return 2; }
            if (existPs > 2) { AddError(parse, "FY-678E", lastGyosyu); return 2; }
        }

        // 【C原典】系統内行種チェック。
        //   系統種別(K_kind)ごとに許可された行種のみ存在できる(FY-675E)。
        //   K_kind='1'(P系)は M系行種で exist_M を立てる(S系との順序検査は C原典で無効化 改訂<10>)。
        kNo = 0;
        bool existM = false;
        bool existS = false; // 【C原典】exist_S。S系の順序検査は改訂<10>で無効化のため常に false。
        foreach (LineTypeTableEntry g in lineTypes)
        {
            LineTypeSymbol sym = FindLineTypeSymbol(g.LineType);
            if (kNo != g.SystemNumber) { existM = false; existS = false; kNo = g.SystemNumber; }

            switch (g.DescriptionKind)
            {
                case '1': // 系統種別1(P系)
                    if (sym is not (LineTypeSymbol.Bn or LineTypeSymbol.P or LineTypeSymbol.Tm
                        or LineTypeSymbol.M or LineTypeSymbol.Sm or LineTypeSymbol.Bo
                        or LineTypeSymbol.B or LineTypeSymbol.Pm or LineTypeSymbol.O
                        or LineTypeSymbol.C or LineTypeSymbol.Ps or LineTypeSymbol.Sep
                        or LineTypeSymbol.S or LineTypeSymbol.Np))
                    {
                        AddError(parse, "FY-675E", g);
                        return 2;
                    }
                    if (sym is LineTypeSymbol.Tm or LineTypeSymbol.M or LineTypeSymbol.Sm
                        or LineTypeSymbol.Bo or LineTypeSymbol.B or LineTypeSymbol.Pm
                        or LineTypeSymbol.O or LineTypeSymbol.Ps)
                    {
                        if (existS) { AddError(parse, "FY-675E", g); return 2; }
                        existM = true;
                    }
                    break;

                case '2': // 系統種別2(SP系)
                    if (sym is not (LineTypeSymbol.Bn or LineTypeSymbol.Sp
                        or LineTypeSymbol.Sep or LineTypeSymbol.C or LineTypeSymbol.Np))
                    {
                        AddError(parse, "FY-675E", g);
                        return 2;
                    }
                    break;

                case '3': // 系統種別3(MP系)
                    if (sym is not (LineTypeSymbol.Bn or LineTypeSymbol.Mp
                        or LineTypeSymbol.Sep or LineTypeSymbol.C or LineTypeSymbol.Np))
                    {
                        AddError(parse, "FY-675E", g);
                        return 2;
                    }
                    break;

                case '4': // 系統種別4(UP系)
                    if (sym is not (LineTypeSymbol.Bn or LineTypeSymbol.Up
                        or LineTypeSymbol.Sep or LineTypeSymbol.C or LineTypeSymbol.Np))
                    {
                        AddError(parse, "FY-675E", g);
                        return 2;
                    }
                    break;

                default: // その他
                    if (sym is not (LineTypeSymbol.Np or LineTypeSymbol.C or LineTypeSymbol.Bn))
                    {
                        AddError(parse, "FY-675E", g);
                        return 2;
                    }
                    break;
            }

            _ = existM; // exist_M は C原典で後続検査に使われるが現状未使用(改訂<10>のS系検査無効化に伴う)。
        }

        return 0;
    }

    /// <summary>
    /// 行種関連チェック(系統内の行種の上下関係検証＋親グループ O_No 設定)。
    /// 【C原典】Gyosyu_Check()。同一系統(K_No)内で各行種が正しい親行種の下にあるかを
    /// 検証し、親の行種グループNo(O_No)を設定する。違反は FY-677E。
    ///   ・P : 後方(同一系統内)に M か S が必要
    ///   ・M : 前方に TM か P が必要(親=その G_No)
    ///   ・S/TM : 前方に P が必要(TM 経由可)。TM の上に O があれば FY-677E
    ///   ・SM : 前方の SM(番号=自番号 or 自番号-1)か M(番号1のみ)に連結
    ///   ・B/BO : 前方に TM/M/SM が必要
    ///   ・PM : 後方に M/B/BO/TM/SM/S が必要(親=その O_No, G_kind 継承)
    ///          改訂&lt;15&gt;: 系統内最下段の PM でも回路記述に 27A/27B/27C があれば FY-677E としない
    ///   ・O : 後方に SM/M が必要(B/BO で打切り)
    /// </summary>
    /// <param name="designArea">回路設計エリア。【C原典】imagec/imagea。改訂&lt;15&gt; の PM 行例外判定用。</param>
    private short CheckLineTypeHierarchy(CircuitParseResult parse, IReadOnlyList<CircuitDescriptionLine>? designArea = null)
    {
        List<LineTypeTableEntry> lineTypes = parse.LineTypes;

        // 【C原典】前半ループ: P/M/S/TM/SM/B/BO の上位関係チェック。
        for (int i = 0; i < lineTypes.Count; i++)
        {
            LineTypeTableEntry s = lineTypes[i];
            LineTypeSymbol findgyosyu = FindLineTypeSymbol(s.LineType);
            short kNo = s.SystemNumber;

            // 【C原典】系統行種Ｐ: 同一系統内に後続の M か S が必要。
            if (findgyosyu == LineTypeSymbol.P)
            {
                for (int j = i; j < lineTypes.Count; j++)
                {
                    LineTypeTableEntry w = lineTypes[j];
                    if (kNo != w.SystemNumber) break;
                    findgyosyu = FindLineTypeSymbol(w.LineType);
                    if (findgyosyu is LineTypeSymbol.M or LineTypeSymbol.S) break;
                }
                if (findgyosyu is not (LineTypeSymbol.M or LineTypeSymbol.S))
                {
                    AddError(parse, "FY-677E", s);
                    return 2;
                }
            }
            // 【C原典】系統行種Ｍ: 前方に TM か P が必要(親=その G_No)。
            else if (findgyosyu == LineTypeSymbol.M)
            {
                for (int j = i; j >= 0; j--)
                {
                    LineTypeTableEntry w = lineTypes[j];
                    if (kNo != w.SystemNumber) break;
                    findgyosyu = FindLineTypeSymbol(w.LineType);
                    if (findgyosyu is LineTypeSymbol.Tm or LineTypeSymbol.P)
                    {
                        s.ParentGroupNumber = w.GroupNumber;
                        break;
                    }
                }
                if (findgyosyu is not (LineTypeSymbol.Tm or LineTypeSymbol.P))
                {
                    AddError(parse, "FY-677E", s);
                    return 2;
                }
            }
            // 【C原典】系統行種Ｓ・ＴＭ: 前方に P が必要(TM 経由可)。
            else if (findgyosyu is LineTypeSymbol.S or LineTypeSymbol.Tm)
            {
                LineTypeSymbol savegyosyu = findgyosyu;
                int j;
                for (j = i - 1; j >= 0; j--)
                {
                    LineTypeTableEntry w = lineTypes[j];
                    if (kNo != w.SystemNumber) break;
                    findgyosyu = FindLineTypeSymbol(w.LineType);
                    if (findgyosyu is LineTypeSymbol.S or LineTypeSymbol.Tm or LineTypeSymbol.P)
                    {
                        s.ParentGroupNumber = w.GroupNumber;
                        break;
                    }
                    // 【C原典】950306: TM の直上に O があれば FY-677E。
                    if (savegyosyu == LineTypeSymbol.Tm && findgyosyu == LineTypeSymbol.O)
                    {
                        AddError(parse, "FY-677E", s);
                        return 2;
                    }
                }
                // 【C原典】TM のときはさらに前方の P を探して親に設定。
                if (findgyosyu == LineTypeSymbol.Tm)
                {
                    for (int k = j; k >= 0; k--)
                    {
                        LineTypeTableEntry w = lineTypes[k];
                        if (kNo != w.SystemNumber) break;
                        findgyosyu = FindLineTypeSymbol(w.LineType);
                        if (findgyosyu == LineTypeSymbol.P)
                        {
                            s.ParentGroupNumber = w.GroupNumber;
                            break;
                        }
                    }
                }
                if (findgyosyu != LineTypeSymbol.P)
                {
                    AddError(parse, "FY-677E", s);
                    return 2;
                }
            }
            // 【C原典】系統行種ＳＭ: 前方の SM(番号連続) か M(番号1) に連結。
            else if (findgyosyu == LineTypeSymbol.Sm)
            {
                short gheader = FindNumericPrefix(s.LineTypeRaw);
                for (int j = i - 1; j >= 0; j--)
                {
                    LineTypeTableEntry w = lineTypes[j];
                    if (kNo != w.SystemNumber) break;
                    findgyosyu = FindLineTypeSymbol(w.LineType);
                    if (findgyosyu is LineTypeSymbol.Sm or LineTypeSymbol.M)
                    {
                        if (findgyosyu == LineTypeSymbol.Sm)
                        {
                            if (gheader == FindNumericPrefix(w.LineTypeRaw) + 1)
                            {
                                s.ParentGroupNumber = w.GroupNumber;
                                break;
                            }
                            if (gheader == FindNumericPrefix(w.LineTypeRaw))
                            {
                                s.ParentGroupNumber = w.ParentGroupNumber;
                                break;
                            }
                        }
                        else
                        {
                            if (gheader == 1) s.ParentGroupNumber = w.GroupNumber;
                            break; // 【C原典】brace欠落によりM検出時は無条件 break。
                        }
                    }
                }
                if (findgyosyu is not (LineTypeSymbol.M or LineTypeSymbol.Sm))
                {
                    AddError(parse, "FY-677E", s);
                    return 2;
                }
            }
            // 【C原典】系統行種ＢＯ・Ｂ: 前方に TM/M/SM が必要。
            else if (findgyosyu is LineTypeSymbol.Bo or LineTypeSymbol.B)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    LineTypeTableEntry w = lineTypes[j];
                    if (kNo != w.SystemNumber) break;
                    findgyosyu = FindLineTypeSymbol(w.LineType);
                    if (findgyosyu is LineTypeSymbol.Tm or LineTypeSymbol.M or LineTypeSymbol.Sm)
                    {
                        s.ParentGroupNumber = w.GroupNumber;
                        break;
                    }
                }
                if (findgyosyu is not (LineTypeSymbol.Tm or LineTypeSymbol.M or LineTypeSymbol.Sm))
                {
                    AddError(parse, "FY-677E", s);
                    return 2;
                }
            }
        }

        // 【C原典】後半ループ: PM/O の下位関係チェック。
        for (int i = 0; i < lineTypes.Count; i++)
        {
            LineTypeTableEntry s = lineTypes[i];
            short kNo = s.SystemNumber;
            LineTypeSymbol findgyosyu = FindLineTypeSymbol(s.LineType);

            // 【C原典】系統行種ＰＭ: 後方に M/B/BO/TM/SM/S が必要(親=その O_No, G_kind 継承)。
            if (findgyosyu == LineTypeSymbol.Pm)
            {
                for (int j = i + 1; j < lineTypes.Count; j++)
                {
                    LineTypeTableEntry w = lineTypes[j];
                    if (kNo != w.SystemNumber) break;
                    findgyosyu = FindLineTypeSymbol(w.LineType);
                    if (findgyosyu is LineTypeSymbol.M or LineTypeSymbol.B or LineTypeSymbol.Bo
                        or LineTypeSymbol.Tm or LineTypeSymbol.Sm or LineTypeSymbol.S) // 改訂<10> S 追加
                    {
                        s.ParentGroupNumber = w.ParentGroupNumber;
                        s.CircuitClass = w.CircuitClass;
                        break;
                    }
                }
                if (findgyosyu is not (LineTypeSymbol.B or LineTypeSymbol.M or LineTypeSymbol.Bo
                    or LineTypeSymbol.Tm or LineTypeSymbol.Sm or LineTypeSymbol.S)) // 改訂<10> S 追加
                {
                    // 【C原典】改訂<15>: PM行が系統内最下段でも、回路記述(kairoar)に 27A/27B/27C が
                    //   あればエラーにしない。findgyosyu==sym_PM(=後方に該当行なし)かつ回路設計エリア
                    //   (imagea)の同一行番号(K_Gyo)記述に 27A/27B/27C を含む場合に免除する。
                    bool pmExempt = false; // 【C原典】pm_f
                    if (findgyosyu == LineTypeSymbol.Pm && designArea is not null
                        && int.TryParse(s.DescriptionRow, out int gyoNo))
                    {
                        foreach (CircuitDescriptionLine image in designArea)
                        {
                            // 【C原典】strncmp(S_Gyosyu->K_Gyo, imagea[j].key.gyono, 3)==0
                            if (image.LineNumber != gyoNo) continue;
                            // 【C原典】strstr(kairoar, "27A"/"27B"/"27C")
                            string work = image.CircuitText; // 【C原典】kairoar(最大 KAIROARLEN=200)
                            if (work.Contains("27A", StringComparison.Ordinal)
                                || work.Contains("27B", StringComparison.Ordinal)
                                || work.Contains("27C", StringComparison.Ordinal))
                            {
                                pmExempt = true;
                                break;
                            }
                        }
                    }
                    if (!pmExempt) // 【C原典】if(1 != pm_f)
                    {
                        AddError(parse, "FY-677E", s);
                        return 2;
                    }
                }
            }
            // 【C原典】系統行種Ｏ: 後方に SM/M が必要(B/BO で打切り)。
            else if (findgyosyu == LineTypeSymbol.O)
            {
                for (int j = i + 1; j < lineTypes.Count; j++)
                {
                    LineTypeTableEntry w = lineTypes[j];
                    if (kNo != w.SystemNumber) break;
                    findgyosyu = FindLineTypeSymbol(w.LineType);
                    if (findgyosyu is LineTypeSymbol.Sm or LineTypeSymbol.M)
                    {
                        s.ParentGroupNumber = w.ParentGroupNumber;
                        break;
                    }
                    if (findgyosyu is LineTypeSymbol.B or LineTypeSymbol.Bo) break;
                }
                if (findgyosyu is not (LineTypeSymbol.Sm or LineTypeSymbol.M))
                {
                    AddError(parse, "FY-677E", s);
                    return 2;
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// 語前の数値(先頭の数字プレフィックス)を取り出す。数字が無ければ1。
    /// 【C原典】Find_Numeric(P_CHAR Gyosyu)(Fysscommon.c)。例 "2SM"→2, "SM"→1。
    /// </summary>
    private static short FindNumericPrefix(string gyosyu)
    {
        int charCount = 0;
        while (charCount < gyosyu.Length && char.IsAsciiDigit(gyosyu[charCount]))
        {
            charCount++;
        }
        if (charCount == 0) return 1; // 【C原典】NULLSTRING(numeric) → 1
        return short.TryParse(gyosyu.AsSpan(0, charCount), out short number) ? number : (short)0;
    }

    /// <summary>
    /// エラー登録。【C原典】Error_Proc(errcode, atoi(K_Gyo), 0, "FYMEE80", Perrc, erra)。
    /// </summary>
    private static void AddError(CircuitParseResult parse, string errorCode, LineTypeTableEntry g)
    {
        int gyonoi = int.TryParse(g.DescriptionRow, out int value) ? value : 0; // 【C原典】gyonoi = atoi(K_Gyo)
        parse.Errors.Add(new CircuitParseError(errorCode, gyonoi, 0, "FYMEE80"));
    }

    /// <summary>
    /// 機器情報関連チェック(機器テーブルを走査し予約語種別ごとに重複/位置検証を分岐実行)。
    /// 【C原典】Yoyakugo_Check_Double()(Fyss12.c:1278)。
    ///
    /// 機器(KIKITABLE)を先頭から走査し、予約語(yoyaku)に応じて次のサブチェックへ分岐する。
    ///   ・リモコン機器(RMCB/RELB/RMMCB/RELMB/RRY/RTR) : <see cref="CheckRemoteControlDuplicate"/>(Yoyakugo_Check_RM)
    ///   ・MCDT/CSDT                                     : <see cref="CheckMcdtPair"/>(Yoyakugo_Check_MCDT)
    ///   ・SC                                            : <see cref="CheckShuntPosition"/>(Yoyakugo_Check_SC)
    ///   ・TR                                            : <see cref="CheckTransformerDuplicate"/>(Yoyakugo_Check_TR)
    ///   ・その他                                        : <see cref="CheckOtherDuplicate"/>(Yoyakugo_Check_OTHER)
    ///
    /// 【本フェーズ(E.4)の範囲】現行の機器テーブルモデル(<see cref="EquipmentTableEntry"/>)で
    /// 表現可能な予約語/番号(yoyaku/ysno)ベースの重複・位置検証を移植する。
    /// 次は未モデル化フィールド・未移植 union に依存するため後続フェーズ(E.4b)へ保留する。
    ///   ・入線分岐(TB+PS)の <see cref="CheckShuntPosition"/> 上流 Gyosyu_Check_PS(key_tbl.p/ps 電圧配列 v[] 依存)
    ///   ・2電源TR(key_tbl.tr union 依存)
    ///   ・LGR/ELR の外部取付区分 G( )(Kakko1/Kakko2 未モデル化)
    ///   ・回路番号重複 Kairo_Bangou_Double / 数量文重複 Kosu_Check(DNO/GNO/Kosu/GKosu 未モデル化)
    /// </summary>
    /// <param name="parse">解析結果(機器テーブル P_Kiki / 行種テーブル P_Gyosyu / エラー領域)。</param>
    /// <returns>0=正常, 2=エラー打切り。【C原典】return(ret)。</returns>
    private static short CheckEquipmentInformation(CircuitParseResult parse)
    {
        List<EquipmentTableEntry> kiki = parse.MainEquipment;
        int count = kiki.Count;

        for (int i = 0; i < count; i++)
        {
            EquipmentTableEntry s = kiki[i];
            // 【C原典】S_Gyosyu = Find_Gyosyu((S_Kiki+i)->G_No, ...)。
            LineTypeTableEntry? gyosyu = FindLineType(parse, s.GroupNumber);
            string yoyakugo = s.ReservedWord;
            short ret;

            // 【C原典】入線分岐文関連チェック: TB の直後が PS のとき Gyosyu_Check_PS。
            //   Gyosyu_Check_PS は key_tbl の p/ps union(電圧配列 v[])に依存するため E.4b へ保留。
            if (yoyakugo == "TB" && i + 1 < count && kiki[i + 1].ReservedWord == "PS")
            {
                ret = 0; // 【C原典】ret = Gyosyu_Check_PS(...) は E.4b。
            }
            // 【C原典】リモコン機器。
            else if (IsRemoteControl(yoyakugo))
            {
                ret = CheckRemoteControlDuplicate(parse, i, s);
            }
            // 【C原典】ＭＣＤＴ/ＣＳＤＴチェック。
            else if (yoyakugo == "MCDT" || yoyakugo == "CSDT")
            {
                ret = CheckMcdtPair(parse, s);
            }
            // 【C原典】ＳＣチェック。
            else if (yoyakugo == "SC")
            {
                ret = CheckShuntPosition(parse, i, s, gyosyu);
            }
            // 【C原典】ＴＲチェック。
            else if (yoyakugo == "TR")
            {
                ret = CheckTransformerDuplicate(parse, s, gyosyu);
            }
            // 【C原典】一般機器チェック。
            else
            {
                ret = CheckOtherDuplicate(parse, i, s);
            }

            if (ret != 0) return ret;

            // 【C原典】回路番号重複チェック(!NULLSTRING(DNO) のとき Kairo_Bangou_Double)。
            //   DNO/GNO/GKosu/Kosu が未モデル化のため E.4b へ保留。
            // 【C原典】数量文重複チェック(yoyaku!=SC かつ 1<Kosu のとき Kosu_Check)。
            //   Kosu/GKosu が未モデル化のため E.4b へ保留。
        }

        return 0;
    }

    /// <summary>予約語がリモコン機器か。【C原典】RMCB/RELB/RMMCB/RELMB/RRY/RTR の OR。</summary>
    private static bool IsRemoteControl(string yoyakugo)
        => yoyakugo is "RMCB" or "RELB" or "RMMCB" or "RELMB" or "RRY" or "RTR";

    /// <summary>
    /// 行種テーブルを行種グループNo(G_No)で検索する。【C原典】Find_Gyosyu(G_No, ...)(Fyss1f.c)。
    /// G_No==0 は NULL(該当なし)。
    /// </summary>
    private static LineTypeTableEntry? FindLineType(CircuitParseResult parse, short groupNumber)
    {
        if (groupNumber == 0) return null; // 【C原典】if(G_No==0) return(NULL)
        foreach (LineTypeTableEntry g in parse.LineTypes)
        {
            if (g.GroupNumber == groupNumber) return g;
        }
        return null;
    }

    /// <summary>
    /// 予約語名称重複チェック(リモコン機器)。
    /// 【C原典】Yoyakugo_Check_RM()(Fyss12.c:1572)。
    /// RRY を除くリモコン機器(RMCB/RELB/RMMCB/RELMB/RTR)は、番号(ysno)付きの場合に
    /// 後続で同一予約語かつ同一番号の機器があると重複エラー(FY-682E)。
    /// </summary>
    private static short CheckRemoteControlDuplicate(CircuitParseResult parse, int idx, EquipmentTableEntry s)
    {
        string yoyakugo = s.ReservedWord;
        int ysnoi = AtoiYsno(s.ReservedWordNumber);

        // 【C原典】ＲＲＹは除外。番号が等しい機器の重複は許されません。
        if (yoyakugo is "RMCB" or "RELB" or "RMMCB" or "RELMB" or "RRY" or "RTR")
        {
            if (ysnoi != 0 && yoyakugo != "RRY")
            {
                List<EquipmentTableEntry> kiki = parse.MainEquipment;
                for (int j = idx + 1; j < kiki.Count; j++)
                {
                    int ysnoj = AtoiYsno(kiki[j].ReservedWordNumber);
                    if (kiki[j].ReservedWord == yoyakugo && ysnoi == ysnoj)
                    {
                        AddEquipmentError(parse, "FY-682E", kiki[j]);
                        return 2;
                    }
                }
            }
        }
        return 0;
    }

    /// <summary>
    /// 予約語名称重複チェック(ＭＣＤＴ/ＣＳＤＴ)。
    /// 【C原典】Yoyakugo_Check_MCDT()(Fyss12.c:1792)。
    /// 番号なし(ysno==0)は不可(FY-684E)。番号付きは同一予約語・同一番号が丁度2つでなければ不可(FY-685E)。
    /// </summary>
    private static short CheckMcdtPair(CircuitParseResult parse, EquipmentTableEntry s)
    {
        int ysnoi = AtoiYsno(s.ReservedWordNumber);
        string yoyakugo = s.ReservedWord;

        if (yoyakugo == "MCDT" || yoyakugo == "CSDT")
        {
            // 【C原典】予約語だけは許されません。
            if (ysnoi == 0)
            {
                AddEquipmentError(parse, "FY-684E", s);
                return 2;
            }

            // 【C原典】予約語+番号の場合1つ(=同一番号が丁度2つ)のみ許されます。
            int existMcdt = 0;
            List<EquipmentTableEntry> kiki = parse.MainEquipment;
            for (int j = 0; j < kiki.Count; j++)
            {
                int ysnoj = AtoiYsno(kiki[j].ReservedWordNumber);
                if (kiki[j].ReservedWord == yoyakugo && ysnoi == ysnoj)
                {
                    existMcdt++;
                }
            }
            if (existMcdt != 2)
            {
                AddEquipmentError(parse, "FY-685E", s);
                return 2;
            }
        }
        return 0;
    }

    /// <summary>
    /// 予約語位置チェック(ＳＣ)。
    /// 【C原典】Yoyakugo_Check_SC()(Fyss12.c:1873)。
    /// 行種(gyosyu)が PM のとき、直後機器のグループNoが同一なら不可(FY-656E)。
    /// O/B/S/BO のときは可。いずれでもなければ不可(FY-649E)。
    /// </summary>
    private static short CheckShuntPosition(CircuitParseResult parse, int idx, EquipmentTableEntry s, LineTypeTableEntry? gyosyu)
    {
        string g = gyosyu?.LineType ?? string.Empty; // 【C原典】S_Gyosyu->gyosyu
        short gNo = s.GroupNumber;                     // 【C原典】G_No
        List<EquipmentTableEntry> kiki = parse.MainEquipment;

        if (g == "PM")
        {
            // 【C原典】(S_Kiki+1)->G_No != G_No なら可。次機器が無ければ別グループ扱い(可)。
            short nextGNo = idx + 1 < kiki.Count ? kiki[idx + 1].GroupNumber : (short)-1;
            if (nextGNo != gNo) return 0;
            AddEquipmentError(parse, "FY-656E", s);
            return 2;
        }
        else if (g is "O" or "B" or "S" or "BO")
        {
            return 0;
        }

        AddEquipmentError(parse, "FY-649E", s);
        return 2;
    }

    /// <summary>
    /// トランス重複チェック(ＴＲ)。
    /// 【C原典】Yoyakugo_Check_TR()(Fyss12.c:1638)。
    /// 行種が PS なら対象外。TR は番号付き(ysno!=0)不可(FY-683E)。
    /// 2電源TR(key_tbl.tr union 依存)の検証は E.4b へ保留する。
    /// </summary>
    private static short CheckTransformerDuplicate(CircuitParseResult parse, EquipmentTableEntry s, LineTypeTableEntry? gyosyu)
    {
        string g = gyosyu?.LineType ?? string.Empty; // 【C原典】S_Gyosyu->gyosyu
        int ysnoi = AtoiYsno(s.ReservedWordNumber);
        string yoyakugo = s.ReservedWord;

        // 【C原典】ＴＲチェック(予約語に番号付きは許されません。)。
        if (g == "PS") return 0;

        if (yoyakugo == "TR")
        {
            if (ysnoi != 0)
            {
                AddEquipmentError(parse, "FY-683E", s);
                return 2;
            }
            // 【C原典】else: 2電源ＴＲチェック(key_tbl.tr の p3/w3/v2/v3 union 依存)。E.4b へ保留。
        }
        return 0;
    }

    /// <summary>
    /// 予約語名称重複チェック(リモコン機器以外)。
    /// 【C原典】Yoyakugo_Check_OTHER()(Fyss12.c:1937)。
    ///   ・MC(番号付き) : 同一番号の MG があれば不可(FY-682E)(950206)。
    ///   ・LGR/ELR      : 外部取付区分 G( ) の番号付き不可(FY-658E)。Kakko 未モデル化のため E.4b へ保留。
    ///   ・その他        : 番号付きは後続の同一予約語・同一番号があれば不可(FY-682E)。
    /// </summary>
    private static short CheckOtherDuplicate(CircuitParseResult parse, int idx, EquipmentTableEntry s)
    {
        string yoyakugo = s.ReservedWord;
        int ysnoi = AtoiYsno(s.ReservedWordNumber);
        List<EquipmentTableEntry> kiki = parse.MainEquipment;
        int count = kiki.Count;

        // 【C原典】MC が対象(950206: 同一番号の MG との重複を禁止)。
        if (yoyakugo == "MC")
        {
            if (ysnoi != 0)
            {
                for (int j = 0; j < count; j++)
                {
                    int ysnoj = AtoiYsno(kiki[j].ReservedWordNumber);
                    if (kiki[j].ReservedWord == "MG" && ysnoi == ysnoj)
                    {
                        AddEquipmentError(parse, "FY-682E", kiki[j]);
                        return 2;
                    }
                }
            }
        }
        // 【C原典】LGR/ELR: 外部取付区分 G( )(Kakko1==12 || Kakko2==12)の番号付きは FY-658E。
        //   Kakko1/Kakko2(括弧種別)は未モデル化のため本枝は E.4b へ保留。
        else if (yoyakugo == "LGR" || yoyakugo == "ELR")
        {
            // E.4b。
        }
        // 【C原典】MC,LGR,MCDT,CSDT,TR 以外: 番号付きは後続の同一予約語・同一番号を禁止。
        else
        {
            if (ysnoi != 0)
            {
                for (int j = idx + 1; j < count; j++)
                {
                    int ysnoj = AtoiYsno(kiki[j].ReservedWordNumber);
                    if (kiki[j].ReservedWord == yoyakugo && ysnoi == ysnoj)
                    {
                        AddEquipmentError(parse, "FY-682E", kiki[j]);
                        return 2;
                    }
                }
            }
        }
        return 0;
    }

    /// <summary>
    /// 機器種別(計器種別)。【C原典】typedef enum _TYPE(Fyss12.c, 序数はC原典と一致)。
    /// <see cref="FindEquipmentType"/>(Find_Keiki_Type)が予約語を分類する。
    /// </summary>
    private enum EquipmentType : short
    {
        /// <summary>その他。【C原典】type_OTHER。</summary>
        Other = 0,
        /// <summary>避雷器(LA)。【C原典】type_LA。</summary>
        La,
        /// <summary>電力量計(WH)。【C原典】type_WH。</summary>
        Wh,
        /// <summary>変流器(CT)。【C原典】type_CT。</summary>
        Ct,
        /// <summary>計器用変圧器(VT)。【C原典】type_VT。</summary>
        Vt,
        /// <summary>ヒューズ(F)。【C原典】type_F。</summary>
        F,
        /// <summary>零相変流器(ZCT)。【C原典】type_ZCT。</summary>
        Zct,
        /// <summary>配線用遮断器(MCB)。【C原典】type_MCB。</summary>
        Mcb,
        /// <summary>開閉器(SB)。【C原典】type_SB。</summary>
        Sb,
        /// <summary>表示灯(WL/GL/RL/OL/BL/HM/FL/CR)。【C原典】type_XL。</summary>
        Xl,
        /// <summary>電流計(AM)。【C原典】type_AM。</summary>
        Am,
        /// <summary>電流計切替スイッチ(AS)。【C原典】type_AS。</summary>
        As,
        /// <summary>避雷器(SC)。【C原典】type_SC。</summary>
        Sc,
        /// <summary>電圧計(VM)。【C原典】type_VM。</summary>
        Vm,
        /// <summary>電圧計切替スイッチ(VS)。【C原典】type_VS(96.07.26 追加)。</summary>
        Vs,
    }

    /// <summary>
    /// 計器グループパターン(計器の並びパターン → まとめて回路区分'K')。
    /// 【C原典】typedef struct _PATTERN と静的テーブル def_ptn[](Fyss12.c)。
    /// C原典の string(カンマ連結予約語)・kazu(構成数)・type のみを保持する
    /// (id / retu[] / top_flg は本処理 Kairo_Kubun_Set では未使用)。
    /// </summary>
    /// <param name="PatternString">パターン文字列(カンマ連結)。【C原典】string。</param>
    /// <param name="Count">構成機器数。【C原典】kazu。</param>
    /// <param name="Type">先頭機器種別。【C原典】type。</param>
    private readonly record struct EquipmentPattern(string PatternString, short Count, EquipmentType Type);

    /// <summary>
    /// 計器グループパターン表。【C原典】static PATTERN def_ptn[](Fyss12.c)。
    /// </summary>
    private static readonly EquipmentPattern[] Patterns =
    [
        new("LA", 1, EquipmentType.La),
        new("WH", 1, EquipmentType.Wh),
        new("CT,WH", 2, EquipmentType.Ct),
        new("CT,WH,AM", 3, EquipmentType.Ct),
        new("CT,WH,AS,AM", 4, EquipmentType.Ct),
        new("VT,WH", 2, EquipmentType.Vt),
        new("VT,CT,WH", 3, EquipmentType.Vt),
        new("VT,CT,WH,AM", 4, EquipmentType.Vt),
        new("VT,CT,WH,AS,AM", 5, EquipmentType.Vt),
        new("CT,AM", 2, EquipmentType.Ct),
        new("CT,AS,AM", 3, EquipmentType.Ct),
        new("F,VM", 2, EquipmentType.F),
        new("VT,VM", 2, EquipmentType.Vt),
        new("F,VT,VM", 3, EquipmentType.F),
        new("F,VT,VS,VM", 4, EquipmentType.F),
        new("F,VS,VM,XL", 4, EquipmentType.F),
        new("F,XL", 2, EquipmentType.F),
        new("CT,THR", 2, EquipmentType.Ct),
        new("CT,THR,AM", 3, EquipmentType.Ct),
        new("CT,THR,AS,AM", 4, EquipmentType.Ct),
        new("ZCT,LGR", 2, EquipmentType.Zct),
        new("MCB,LGR", 2, EquipmentType.Mcb),
        new("ZCT,ELR", 2, EquipmentType.Zct),
        new("MCB,ELR", 2, EquipmentType.Mcb),
        new("MCB,ELR", 2, EquipmentType.Mcb),
        new("SB,LGR", 2, EquipmentType.Sb),
        new("SB,ELR", 2, EquipmentType.Sb),
        new("F,ELR", 2, EquipmentType.F),
    ];

    /// <summary>
    /// 予約語(計器種別)の分類。【C原典】Find_Keiki_Type(P_CHAR yoyakugo)(Fyss12.c)。
    /// 表示灯系(WL/GL/RL/OL/BL/HM/FL/CR)はいずれも <see cref="EquipmentType.Xl"/>。
    /// SB は C原典 Find_Keiki_Type では分類されない(常に type_OTHER)。
    /// </summary>
    private static EquipmentType FindEquipmentType(string yoyakugo) => yoyakugo switch
    {
        "LA" => EquipmentType.La,
        "WH" => EquipmentType.Wh,
        "CT" => EquipmentType.Ct,
        "VT" => EquipmentType.Vt,
        "F" => EquipmentType.F,
        "ZCT" => EquipmentType.Zct,
        "MCB" => EquipmentType.Mcb,
        "WL" or "GL" or "RL" or "OL" or "BL" or "HM" or "FL" or "CR" => EquipmentType.Xl,
        "VM" => EquipmentType.Vm,
        "AS" => EquipmentType.As,
        "AM" => EquipmentType.Am,
        "SC" => EquipmentType.Sc,
        "VS" => EquipmentType.Vs,
        _ => EquipmentType.Other,
    };

    /// <summary>
    /// 計器名の正規化。表示灯系(WL/GL/OL/RL/FL/BL)はパターン照合上 "XL" に置換する。
    /// 【C原典】Kairo_Kubun_Set 内の yoyakugo→"XL" 置換(HM/CR は置換対象外)。
    /// </summary>
    private static string MeterName(string yoyaku)
        => yoyaku is "WL" or "GL" or "OL" or "RL" or "FL" or "BL" ? "XL" : yoyaku;

    /// <summary>
    /// 回路区分セット(機器テーブルの K_Kubun を機器種別・行種・計器パターンに応じて設定)。
    /// 【C原典】Kairo_Kubun_Set(Fyss12.c:3425)。エラー返却はなく、機器テーブルを直接更新する。
    ///
    ///   ・基本  : 行種 PM なら'K'、それ以外は'M'。
    ///   ・SC    : 直後機器が同一行種グループなら'S'、異なる/末尾なら'M'。
    ///   ・ZCT   : 'K'。行種 TM/M では直後 LGR/ELR、それ以外では直後 ELR を'K'として取り込む。
    ///   ・計器  : (VS/LA/CT/VT/WH/F/AM)。基本区分を設定後、計器パターン表と前方の並びを
    ///             照合し、一致すれば構成機器すべてを'K'にして先頭を進める。
    ///   ・MCB/SB: 'M'。直後 LGR があれば両者'K'として取り込む。
    ///   ・XL/VM/AS: 'K'。
    /// </summary>
    private static void SetCircuitDivision(CircuitParseResult parse)
    {
        List<EquipmentTableEntry> kiki = parse.MainEquipment;
        int count = kiki.Count;
        int i = 0;

        while (i < count)
        {
            EquipmentTableEntry s = kiki[i];
            EquipmentType findtype = FindEquipmentType(s.ReservedWord);
            // 【C原典】S_Gyosyu = Find_Gyosyu((S_Kiki+i)->G_No, ...); S_Gyosyu->gyosyu。
            string gyosyu = FindLineType(parse, s.GroupNumber)?.LineType ?? string.Empty;

            // 【C原典】基本的回路区分。
            s.CircuitDivision = gyosyu == "PM" ? 'K' : 'M';

            // 【C原典】ＳＣ。
            if (findtype == EquipmentType.Sc)
            {
                if (i + 1 < count)
                {
                    s.CircuitDivision = s.GroupNumber == kiki[i + 1].GroupNumber ? 'S' : 'M';
                }
                else
                {
                    s.CircuitDivision = 'M';
                }
            }
            // 【C原典】ＺＣＴ。
            else if (findtype == EquipmentType.Zct)
            {
                s.CircuitDivision = 'K';
                if (gyosyu is "TM" or "M") // 【C原典】950907
                {
                    // 【C原典】ＬＧＲ・ＥＬＲ。
                    if (i + 1 < count && kiki[i + 1].ReservedWord is "LGR" or "ELR")
                    {
                        kiki[i + 1].CircuitDivision = 'K';
                        i++;
                    }
                }
                else
                {
                    if (i + 1 < count && kiki[i + 1].ReservedWord == "ELR")
                    {
                        kiki[i + 1].CircuitDivision = 'K';
                        i++;
                    }
                }
            }
            // 【C原典】その他の計器(VS/LA/CT/VT/WH/F/AM)。
            else if (findtype is EquipmentType.Vs or EquipmentType.La or EquipmentType.Ct
                     or EquipmentType.Vt or EquipmentType.Wh or EquipmentType.F or EquipmentType.Am)
            {
                if (findtype is EquipmentType.Vs or EquipmentType.F or EquipmentType.La or EquipmentType.Vt)
                {
                    s.CircuitDivision = 'K';
                }
                if (findtype is EquipmentType.Ct or EquipmentType.Wh or EquipmentType.Am)
                {
                    s.CircuitDivision = 'M';
                }

                int maxj = 0;
                // 【C原典】Search_Pattern_Name で findtype に一致する全パターンを走査する。
                foreach (EquipmentPattern p in Patterns)
                {
                    if (p.Type != findtype) continue;
                    int kazu = p.Count;

                    // 【C原典】先頭から kazu 個の予約語をカンマ連結(表示灯系は "XL" 置換)。
                    //   途中で機器テーブル末尾に達したら連結を打ち切る(→ 完全一致せず不一致扱い)。
                    string candidate = MeterName(kiki[i].ReservedWord);
                    for (int j = 1; j < kazu; j++)
                    {
                        if (i + j >= count) break;
                        candidate += "," + MeterName(kiki[i + j].ReservedWord);
                    }

                    if (p.PatternString == candidate)
                    {
                        for (int j = 0; j < kazu; j++)
                        {
                            kiki[i + j].CircuitDivision = 'K';
                        }
                        if (maxj < kazu - 1) maxj = kazu - 1;
                    }
                }
                i = maxj + i;
            }
            // 【C原典】ＭＣＢ・ＳＢ(直後 LGR の取り込み)。
            else if (findtype is EquipmentType.Mcb or EquipmentType.Sb)
            {
                s.CircuitDivision = 'M';
                if (i + 1 < count && kiki[i + 1].ReservedWord == "LGR")
                {
                    s.CircuitDivision = 'K';
                    kiki[i + 1].CircuitDivision = 'K';
                    i++;
                }
            }
            // 【C原典】ＶＭ・ＡＳ・ＸＬ。
            else if (findtype is EquipmentType.Xl or EquipmentType.Vm or EquipmentType.As)
            {
                s.CircuitDivision = 'K';
            }

            i++;
        }
    }

    /// <summary>
    /// 電気パラメータ同一チェック。
    /// 【C原典】Ele_Equal_Check(short i_Kikic, struct KIKITABLE* P_Kiki, ...)(Fyss12.c:4567)。    /// 同一機器認識番号(ysno)を持つ機器の電気パラメータ(key_tbl)が一致するかを検証する。
    ///
    /// 本フェーズ(E.3)では E.2 で移植済みの型 <b>MCDT/CSDT/MC</b> と、
    /// key_tbl 比較を伴わない <b>TSW</b> を移植する。
    /// R系(LGR/ELR/RRY)は ma[3][3](inum 添字配列)を用いるため E.2 と同様に後続フェーズへ保留する。
    /// </summary>
    /// <param name="parse">解析結果(機器テーブル P_Kiki とエラー領域)。</param>
    /// <returns>0=正常, 2=エラー打切り。【C原典】return(0)/return(2)。</returns>
    private static short CheckElectricalParameterEquality(CircuitParseResult parse)
    {
        List<EquipmentTableEntry> kiki = parse.MainEquipment;
        int count = kiki.Count;

        // ── MCDT ────────────────────────────────────────────────
        // 【C原典】key_tbl.mcdt の p/a/v/vc を比較する。
        for (int i = 0; i < count; i++)
        {
            int ysnoi = AtoiYsno(kiki[i].ReservedWordNumber);
            if (ysnoi == 0) continue;
            if (kiki[i].ReservedWord != "MCDT") continue; // 【C原典】strcmp(yoyaku,"MCDT")

            // 【C原典】次のMCDTを探す(同一 ysno の個数 n と最初の出現位置 k)。
            int n = 0, k = 0;
            for (int j = 0; j < count; j++)
            {
                if (j == i) continue;
                int ysnoj = AtoiYsno(kiki[j].ReservedWordNumber);
                if (kiki[j].ReservedWord == "MCDT" && ysnoj == ysnoi)
                {
                    n++;
                    if (n == 1) k = j;
                }
            }

            if (k > i) continue;               // 【C原典】以前にチェック済み
            if (n != 1)                        // 【C原典】ペア(2つ)でない
            {
                AddEquipmentError(parse, "FY-630E", kiki[i]);
                return 2;
            }
            if (!RatingFieldEquals(kiki[i], kiki[k], "p") ||
                !RatingFieldEquals(kiki[i], kiki[k], "a") ||
                !RatingFieldEquals(kiki[i], kiki[k], "v") ||
                !RatingFieldEquals(kiki[i], kiki[k], "vc"))
            {
                AddEquipmentError(parse, "FY-630E", kiki[i]);
                return 2;
            }
        }

        // ── CSDT ────────────────────────────────────────────────
        // 【C原典】key_tbl.csdt の p/a/v/fv を比較する。
        for (int i = 0; i < count; i++)
        {
            int ysnoi = AtoiYsno(kiki[i].ReservedWordNumber);
            if (ysnoi == 0) continue;
            if (kiki[i].ReservedWord != "CSDT") continue;

            int n = 0, k = 0;
            for (int j = 0; j < count; j++)
            {
                if (j == i) continue;
                int ysnoj = AtoiYsno(kiki[j].ReservedWordNumber);
                if (kiki[j].ReservedWord == "CSDT" && ysnoi == ysnoj)
                {
                    n++;
                    if (n == 1) k = j;
                }
            }

            if (k > i) continue;
            if (n != 1)
            {
                AddEquipmentError(parse, "FY-630E", kiki[i]);
                return 2;
            }
            if (!RatingFieldEquals(kiki[i], kiki[k], "p") ||
                !RatingFieldEquals(kiki[i], kiki[k], "a") ||
                !RatingFieldEquals(kiki[i], kiki[k], "v") ||
                !RatingFieldEquals(kiki[i], kiki[k], "fv"))
            {
                AddEquipmentError(parse, "FY-630E", kiki[i]);
                return 2;
            }
        }

        // ── MC(950227)─────────────────────────────────────────
        // 【C原典】先頭の MC を基準に、2つ目以降の同一 ysno は入力空を要求(空でなければ FY-631E)、
        //          その後、基準 MC の値を複写する。
        for (int i = 0; i < count; i++)
        {
            if (kiki[i].ReservedWord != "MC") continue;
            int ysnoi = AtoiYsno(kiki[i].ReservedWordNumber);
            if (ysnoi == 0) continue;

            // 【C原典】チェック済みかを判定(i より前に同一 ysno の MC があれば済み)。
            bool alreadyChecked = false;
            for (int j = i - 1; j >= 0; j--)
            {
                if (kiki[j].ReservedWord != "MC") continue;
                int ysnoj = AtoiYsno(kiki[j].ReservedWordNumber);
                if (ysnoj == 0) continue;
                if (ysnoi == ysnoj) { alreadyChecked = true; break; }
            }
            if (alreadyChecked) continue;

            // 【C原典】2つ目以降の入力はエラー(空でなければ FY-631E)、空なら基準値を複写。
            for (int j = i + 1; j < count; j++)
            {
                if (kiki[j].ReservedWord != "MC") continue;
                int ysnoj = AtoiYsno(kiki[j].ReservedWordNumber);
                if (ysnoi != ysnoj) continue;

                if (RatingHasAny(kiki[j], "p", "kw", "a", "v", "ac", "bc", "vc"))
                {
                    AddEquipmentError(parse, "FY-631E", kiki[j]);
                    return 2;
                }

                // 【C原典】p/kw/a/fv/v/fvc/vc/ac/bc を複写(DTYPE も複写するが未モデル化のため対象外)。
                CopyMcRatingFields(kiki[i], kiki[j]);
            }
        }

        // ── LGR/ELR/RRY(R系)──────────────────────────────────
        // 【C原典】key_tbl.lgr/elr/rry(ma[3][3] inum 添字配列)を複写・比較する。
        // E.2 で R系は未移植のため、本チェックも後続フェーズ(E.3b)へ保留する。
        // 参照: Fyss12.c Ele_Equal_Check LGR/ELR@4776, RRY@4869。

        // ── TSW ─────────────────────────────────────────────────
        // 【C原典】同一 ysno の TSW が複数存在したらエラー(key_tbl 比較なし)。
        for (int i = 0; i < count; i++)
        {
            int ysnoi = AtoiYsno(kiki[i].ReservedWordNumber);
            if (ysnoi == 0) continue;
            if (kiki[i].ReservedWord != "TSW") continue;

            for (int j = i + 1; j < count; j++)
            {
                int ysnoj = AtoiYsno(kiki[j].ReservedWordNumber);
                if (kiki[j].ReservedWord == "TSW" && ysnoj == ysnoi)
                {
                    AddEquipmentError(parse, "FY-630E", kiki[i]);
                    return 2;
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// 機器認識番号(ysno)を整数化する。【C原典】atoi(P_Kiki[].ysno)。
    /// 先頭の連続する数字のみを解釈し、非数字で打ち切る。
    /// </summary>
    private static int AtoiYsno(string s)
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

    /// <summary>2機器の定格キー同一フィールドが等しいか。【C原典】memcmp/比較。未格納(null)同士も一致扱い。</summary>
    private static bool RatingFieldEquals(EquipmentTableEntry a, EquipmentTableEntry b, string field)
        => string.Equals(a.RatingValues?.Get(field), b.RatingValues?.Get(field), StringComparison.Ordinal);

    /// <summary>指定フィールドのいずれかが格納済みか。【C原典】field[0] != '\0' の OR。</summary>
    private static bool RatingHasAny(EquipmentTableEntry e, params string[] fields)
    {
        if (e.RatingValues is null) return false;
        foreach (string f in fields)
        {
            if (e.RatingValues.Has(f)) return true;
        }
        return false;
    }

    /// <summary>基準 MC の定格キー値を複写先へコピーする。【C原典】key_tbl.mc の memcpy 群。</summary>
    private static void CopyMcRatingFields(EquipmentTableEntry src, EquipmentTableEntry dst)
    {
        dst.RatingValues ??= new RatingValues("MC");
        foreach (string f in new[] { "p", "kw", "a", "fv", "v", "fvc", "vc", "ac", "bc" })
        {
            string? value = src.RatingValues?.Get(f);
            if (value is not null)
            {
                dst.RatingValues.Set(f, value);
            }
        }
    }

    /// <summary>
    /// 機器テーブルエントリに対するエラー登録。
    /// 【C原典】Error_Proc(errcode, atoi(K_Gyo), atoi(K_Ket), "FYMEE80", Perrc, erra)。
    /// </summary>
    private static void AddEquipmentError(CircuitParseResult parse, string errorCode, EquipmentTableEntry kiki)
    {
        parse.Errors.Add(new CircuitParseError(errorCode, kiki.LineNumber, kiki.Column, "FYMEE80"));
    }

    /// <summary>
    /// 行種文字列をシンボルへ分類する。【C原典】Find_Gyosyu_Sym(P_CHAR gyosyu)。
    /// </summary>
    private static LineTypeSymbol FindLineTypeSymbol(string gyosyu) => gyosyu switch
    {
        "P" => LineTypeSymbol.P,
        "BN" => LineTypeSymbol.Bn,
        "TM" => LineTypeSymbol.Tm,
        "M" => LineTypeSymbol.M,
        "S" => LineTypeSymbol.S,
        "SM" => LineTypeSymbol.Sm,
        "BO" => LineTypeSymbol.Bo,
        "B" => LineTypeSymbol.B,
        "PM" => LineTypeSymbol.Pm,
        "O" => LineTypeSymbol.O,
        "C" => LineTypeSymbol.C,
        "PS" => LineTypeSymbol.Ps,
        "SEP" => LineTypeSymbol.Sep,
        "SP" => LineTypeSymbol.Sp,
        "MP" => LineTypeSymbol.Mp,
        "NP" => LineTypeSymbol.Np,
        "UP" => LineTypeSymbol.Up,
        _ => LineTypeSymbol.Other,
    };
}
