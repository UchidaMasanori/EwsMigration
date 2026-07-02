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

        // 3. 電気パラメータ同一チェック。【C原典】Ele_Equal_Check()(前段は無効化、後段で実行)。TODO。
        // 4. 機器情報関連チェック。【C原典】Yoyakugo_Check_Double()。TODO。
        // 5. 回路要素セット。【C原典】Kairo_Kubun_Set()。TODO。
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
        //        電気パラメータ同一チェック。【C原典】Ele_Equal_Check()。TODO。
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
