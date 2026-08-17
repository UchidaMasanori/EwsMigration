using Ews.Domain.Analysis;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>主回路/複合回路の機器選定結果。【C原典】Fysk01_Kikisearch_S1 の戻り値と ckidata。</summary>
/// <param name="Status">1:epno=1で成功 2:epno=1で失敗 3:epno=2で成功 4:epno=2で失敗。</param>
/// <param name="Result">直近上下位該当データ。【C原典】ckidata(FYDF812)。</param>
public sealed record MainSelectionResult(int Status, NearestRankReference? Result);

/// <summary>
/// 主回路/複合回路の機器選定(直近上下位ファイル検索)。移植済みの下請け
/// (<see cref="RatingKeyBuilder"/>/<see cref="ShapeTypeExpander"/>/<see cref="NearestRankSearch"/>)を
/// 束ねて、要求仕様から直近上下位参照ファイル(FYDF812)を検索する。
/// 【C原典】(toku/sekkei/src/Fysk01.c)
///   - <see cref="SelectMain"/>  : Fysk01_Kikisearch_S1(:295)
///   - <see cref="SelectMpSp"/>  : Fysk01_Kikisearch_P(:549, MP/SP 系統)
///   - <see cref="Dispatch"/>    : Fysk01_Chokisearch(:614, 予約語/proc_no で検索方式を分岐)
///   - <see cref="SearchGeneral"/>: Fysk01_Chokisearch_ALL(:1589, 汎用)
/// 専門検索(BRK/MTG/CT/SC/PBS)は後続バッチで追加する。
/// </summary>
public static class NearestRankSelector
{
    private const int Good = NearestRankSearch.Good;
    private const int NoGood = NearestRankSearch.NoGood;
    private const int TypeWidth = 7;

    // 【C原典】fyrt808.h: PC_1=11(THR/MGFR) PC_2=12(MC) PC_3=13(MG) PC_4=14(MGSD/nERY)。
    private const short Pc1 = 11;
    private const short Pc4 = 14;

    /// <summary>
    /// 主回路/複合回路の機器選定。【C原典】Fysk01_Kikisearch_S1(Fysk01.c:295)。
    /// 電気パラメータ入力有無で epno を決め、直近上下位検索を行い、結果を 1/2/3/4 で返す。
    /// </summary>
    /// <param name="specKind">仕様(特注:0 コンポ:1)。【C原典】cpf。</param>
    /// <param name="table">予約語別チェック情報。【C原典】tbl(TCHI_TBL)。</param>
    /// <param name="parameters">電気パラメータ(自機/上位/下位の 3 組)。【C原典】sep[]。</param>
    /// <param name="dataTypes">データタイプ(7枠)。【C原典】dtype。</param>
    /// <param name="shapeTypes">変換形状タイプ一覧。【C原典】wtype(tsu 件)。</param>
    /// <param name="shapeTypeIndex">変換タイプ位置。【C原典】ti。</param>
    /// <param name="makerCodes">変換メーカーコード一覧。【C原典】mcod(msu 件, 各3桁)。</param>
    /// <param name="productName">品名。【C原典】hinm。</param>
    /// <param name="handleLockFlag">ハンドルロック有無チェックフラグ。【C原典】hfg。</param>
    /// <param name="candidates">直近上下位参照ファイル全候補(キー順)。【C原典】FYDF812 ISAM。</param>
    public static MainSelectionResult SelectMain(
        int specKind,
        RatingCheckTable table,
        IReadOnlyList<NumericElectricalParameters> parameters,
        string[] dataTypes,
        IReadOnlyList<string> shapeTypes,
        int shapeTypeIndex,
        IReadOnlyList<string> makerCodes,
        string productName,
        short handleLockFlag,
        IReadOnlyList<NearestRankReference> candidates)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);

        // 【C原典】epno = Fysk0a_EparInput_Check(sep[0], sfg)。
        ElectricalParameterInput input = ElectricalParameterInputChecker.Check(parameters[0]);
        int epno = input.ParameterNumber;

        // 【C原典】stn[0] = -1(主回路は接点計算なし)。
        NearestRankSearchResult result = Dispatch(
            specKind, table, parameters[epno], input.InputFlags, shapeTypes, shapeTypeIndex,
            dataTypes, makerCodes, productName, handleLockFlag, -1, candidates);

        // 【C原典】epno==1 → 1/2、epno==2 → 3/4。
        int status = epno == 1
            ? (result.Status == Good ? 1 : 2)
            : (result.Status == Good ? 3 : 4);

        return new MainSelectionResult(status, result.Selected);
    }

    /// <summary>
    /// 主/複合回路(MP/SP 系統)の機器選定。【C原典】Fysk01_Kikisearch_P(Fysk01.c:549)。
    /// 電気パラメータ入力有無で入力項目フラグ(sfg)のみを求め(epno は使用しない)、
    /// 常に接点計算なし(stn[0]=-1)・電気パラメータ 2 番目(sep[1])で直近上下位検索を行い、
    /// 該当ありで 7、該当なしで 8 を返す。
    /// </summary>
    /// <param name="specKind">仕様(特注:0 コンポ:1)。【C原典】cpf。</param>
    /// <param name="table">予約語別チェック情報。【C原典】tbl(TCHI_TBL)。</param>
    /// <param name="parameters">電気パラメータ(自機/上位/下位の 3 組)。【C原典】sep[]。</param>
    /// <param name="dataTypes">データタイプ(7枠)。【C原典】dtype。</param>
    /// <param name="shapeTypes">変換形状タイプ一覧。【C原典】wtype(tsu 件)。</param>
    /// <param name="shapeTypeIndex">変換タイプ位置。【C原典】ti。</param>
    /// <param name="makerCodes">変換メーカーコード一覧。【C原典】mcod(msu 件, 各3桁)。</param>
    /// <param name="productName">品名。【C原典】hinm。</param>
    /// <param name="handleLockFlag">ハンドルロック有無チェックフラグ。【C原典】hfg。</param>
    /// <param name="candidates">直近上下位参照ファイル全候補(キー順)。【C原典】FYDF812 ISAM。</param>
    public static MainSelectionResult SelectMpSp(
        int specKind,
        RatingCheckTable table,
        IReadOnlyList<NumericElectricalParameters> parameters,
        string[] dataTypes,
        IReadOnlyList<string> shapeTypes,
        int shapeTypeIndex,
        IReadOnlyList<string> makerCodes,
        string productName,
        short handleLockFlag,
        IReadOnlyList<NearestRankReference> candidates)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);

        // 【C原典】ret = Fysk0a_EparInput_Check(sep[0], sfg)。戻り epno は破棄し sfg のみ使用。
        ElectricalParameterInput input = ElectricalParameterInputChecker.Check(parameters[0]);

        // 【C原典】stn[0]=-1、sep[1](電気パラメータ2番目)で検索。
        NearestRankSearchResult result = Dispatch(
            specKind, table, parameters[1], input.InputFlags, shapeTypes, shapeTypeIndex,
            dataTypes, makerCodes, productName, handleLockFlag, -1, candidates);

        // 【C原典】GOOD → 7、それ以外 → 8。
        int status = result.Status == Good ? 7 : 8;

        return new MainSelectionResult(status, result.Selected);
    }

    /// <summary>
    /// 特別予約語(MCB/ELB/MMCB/ELMB/SB/RMCB/RELB/RMMCB/RELMB/NHMB/HPSB/HSB/CP/CKS)の機器選定。
    /// 【C原典】Fysk01_Kikisearch_T(Fysk01.c:4467)。電気パラメータ入力有無で epno を求め、まず下位機器
    /// (epno=2)検索で下位パラメータを設定し、電流系入力があれば(epno==1)上位機器(epno=1)検索を行って
    /// その結果を採用する。戻り: epno==1 は 1(該当)/2(なし)、epno==2 は 3(該当)/4(なし)。
    /// 【C原典】cpf は下請け Chokisearch_T が未使用のため省略。
    /// </summary>
    /// <param name="table">予約語別チェック情報。【C原典】tbl(TCHI_TBL)。</param>
    /// <param name="parameters">電気パラメータ sep[0..2]。【C原典】sep[]。</param>
    /// <param name="dataTypes">データタイプ(7枠)。【C原典】dtype。</param>
    /// <param name="shapeTypes">変換形状タイプ一覧。【C原典】wtype(tsu 件)。</param>
    /// <param name="shapeTypeIndex">変換タイプ位置。【C原典】ti。</param>
    /// <param name="makerCodes">変換メーカーコード一覧。【C原典】mcod(msu 件, 各3桁)。</param>
    /// <param name="productName">品名。【C原典】hinm。</param>
    /// <param name="handleLockFlag">ハンドルロック有無チェックフラグ。【C原典】hfg。</param>
    /// <param name="work">選定ワーク(負荷容量/通電電流等)。【C原典】wk1。</param>
    /// <param name="flags">項目書替えフラグ(初期化してから設定)。【C原典】wk3。</param>
    /// <param name="candidates">直近上下位参照ファイル全候補(キー順)。【C原典】FYDF812 ISAM。</param>
    public static MainSelectionResult SelectSpecialReservedWord(
        RatingCheckTable table,
        NumericElectricalParameters[] parameters,
        string[] dataTypes,
        IReadOnlyList<string> shapeTypes,
        int shapeTypeIndex,
        IReadOnlyList<string> makerCodes,
        string productName,
        short handleLockFlag,
        SelectionWorkParameters work,
        AreaRewriteFlags flags,
        IReadOnlyList<NearestRankReference> candidates)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(flags);

        // 【C原典】memset(wk3, 0, ...)。項目書替えフラグを初期化。
        flags.Reset();

        // 【C原典】epno = Fysk0a_EparInput_Check(sep[0], sfg)。
        ElectricalParameterInput input = ElectricalParameterInputChecker.Check(parameters[0]);
        int epno = input.ParameterNumber;
        int[] sfg = [.. input.InputFlags];

        // 【C原典】epno==1 は第1入力フラグを退避し 0 にして先に下位検索。
        int savedFirst = sfg[0];
        if (epno == 1)
        {
            sfg[0] = 0;
        }

        // 【C原典】直近上下位検索(電気パラメータ3番目=sep[2], epno=2)。
        NearestRankSearchResult lower = SearchSpecialReservedWord(
            table, 2, parameters, sfg, shapeTypes, shapeTypeIndex, dataTypes,
            makerCodes, productName, handleLockFlag, work, flags, candidates);

        if (epno != 1)
        {
            // 【C原典】epno==2: GOOD→3 / それ以外→4。
            return new MainSelectionResult(lower.Status == Good ? 3 : 4, lower.Selected);
        }

        // 【C原典】epno==1: 第1フラグを戻し、上位(epno=1)検索の結果を採用。
        sfg[0] = savedFirst;
        NearestRankSearchResult upper = SearchSpecialReservedWord(
            table, 1, parameters, sfg, shapeTypes, shapeTypeIndex, dataTypes,
            makerCodes, productName, handleLockFlag, work, flags, candidates);

        return new MainSelectionResult(upper.Status == Good ? 1 : 2, upper.Selected);
    }

    /// <summary>
    /// 予約語/proc_no で検索方式を分岐する。【C原典】Fysk01_Chokisearch(Fysk01.c:614)。
    /// </summary>
    public static NearestRankSearchResult Dispatch(
        int specKind,
        RatingCheckTable table,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        IReadOnlyList<string> shapeTypes,
        int shapeTypeIndex,
        string[] dataTypes,
        IReadOnlyList<string> makerCodes,
        string productName,
        short handleLockFlag,
        int contactFlag,
        IReadOnlyList<NearestRankReference> candidates)
    {
        ArgumentNullException.ThrowIfNull(table);
        string yo = table.ReservedWord;

        if (table.ProcessNumber is >= Pc1 and <= Pc4)
        {
            return SearchMotorSwitch(specKind, table, parameters, inputFlags, shapeTypes, shapeTypeIndex,
                                     dataTypes, makerCodes, productName, handleLockFlag, candidates);
        }
        if (Matches(yo, "CT ", 3))
        {
            return SearchCurrentTransformer(specKind, table, parameters, inputFlags, shapeTypes, shapeTypeIndex,
                                            dataTypes, makerCodes, productName, candidates);
        }
        if (Matches(yo, "PBS ", 4))
        {
            return SearchPushButton(specKind, table, parameters, inputFlags, shapeTypes, shapeTypeIndex,
                                    dataTypes, makerCodes, productName, handleLockFlag, contactFlag, candidates);
        }
        if (Matches(yo, "SC  ", 4))
        {
            throw new NotImplementedException("Fysk01_Chokisearch_SC は Fysk02_Check_Teichi_SC2 / PropSelChkSc 未移植のため保留です。");
        }
        if (IsBreaker(yo))
        {
            return SearchBreaker(table, parameters, inputFlags, shapeTypes, shapeTypeIndex,
                                 dataTypes, makerCodes, productName, handleLockFlag, contactFlag, candidates);
        }

        return SearchGeneral(specKind, table, parameters, inputFlags, shapeTypes, shapeTypeIndex,
                             dataTypes, makerCodes, productName, handleLockFlag, contactFlag, candidates);
    }

    /// <summary>
    /// 汎用の直近上下位検索。【C原典】Fysk01_Chokisearch_ALL(Fysk01.c:1589)。
    /// メーカーコード・形状タイプ・二次形状タイプの総当たりで検索キーを組み立て、
    /// 最初に該当した候補を採用する。
    /// </summary>
    public static NearestRankSearchResult SearchGeneral(
        int specKind,
        RatingCheckTable table,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        IReadOnlyList<string> shapeTypes,
        int shapeTypeIndex,
        string[] dataTypes,
        IReadOnlyList<string> makerCodes,
        string productName,
        short handleLockFlag,
        int contactFlag,
        IReadOnlyList<NearestRankReference> candidates)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(shapeTypes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(candidates);

        string yo = table.ReservedWord;

        // 【C原典】第一キー: 電源 AC/DC 区分(DCPW は主電源 AC 固定)。
        char mainAcDc = parameters.V2Kbn;
        if (Matches(yo, "DCPW", 4))
        {
            mainAcDc = 'A';
        }
        char controlAcDc = parameters.VcKbn;

        // 【C原典】LGT/LA は選定用に一部パラメータを退避して 0/1 に置換(検索後に復元)。
        int restoreKind = 0;
        double savedP = 0.0, savedPh2 = 0.0, savedWr2 = 0.0;
        if (Matches(yo, "LGT ", 4))
        {
            savedP = parameters.P;
            parameters.P = 1.0;
            restoreKind = 1;
        }
        else if (Matches(yo, "LA ", 3) &&
                 (Matches(dataTypes[0], "ST ", 3) || Matches(dataTypes[0], "YT ", 3)))
        {
            savedPh2 = parameters.Ph2[0];
            savedWr2 = parameters.Wr2[0];
            parameters.Ph2[0] = 0.0;
            parameters.Wr2[0] = 0.0;
            restoreKind = 2;
        }
        // 【C原典】改訂<34> AM は PropSelChkAM で LW 選定パラメータを設定(後続バッチで移植)。

        try
        {
            // 【C原典】Fysk01_Type_Check2 → 二次形状タイプ wktype/tsu2/ti2。
            ShapeTypeExpansion expansion = ShapeTypeExpander.Expand(yo, dataTypes);
            IReadOnlyList<string> secondaryShapeTypes = expansion.ShapeTypes;
            int secondaryIndex = expansion.TypeIndex;

            // 【C原典】Fysk04_Make_Teikakuchi で定格値キー(kteichi 50)を作成。
            string ratingKey = RatingKeyBuilder.MakeRatingKey(table.Entries, parameters);

            // 【C原典】ptype[0..6] = dtype。WL/COS/WH の特殊タイプ修正を適用。
            string[] baseTypes = BuildBaseTypes(yo, dataTypes, makerCodes, parameters);

            NearestRankReference? firstQuery = null;

            // 【C原典】特注(cpf==0): メーカー→形状→二次形状 / コンポ(cpf==1): 形状→メーカー→二次形状。
            IEnumerable<(int MakerIdx, int ShapeIdx)> outerOrder = specKind == 0
                ? EnumeratePairs(makerCodes.Count, shapeTypes.Count, makerFirst: true)
                : EnumeratePairs(makerCodes.Count, shapeTypes.Count, makerFirst: false);

            foreach ((int j, int k) in outerOrder)
            {
                for (int i = 0; i < secondaryShapeTypes.Count; i++)
                {
                    NearestRankReference query = BuildQuery(
                        yo, Slot(makerCodes, j, 3), baseTypes,
                        shapeTypeIndex, Pad(shapeTypes[k]),
                        secondaryIndex, Pad(secondaryShapeTypes[i]),
                        mainAcDc, controlAcDc, ratingKey, shapeOverridesSecondary: false);

                    firstQuery ??= query;

                    NearestRankSearchResult r = NearestRankSearch.Search(
                        table, query, candidates, productName, handleLockFlag, parameters, inputFlags, contactFlag);

                    if (r.Status == Good)
                    {
                        return r;
                    }
                }
            }

            // 【C原典】該当なし。ck は最初に組み立てたキー(wk)。
            return new NearestRankSearchResult(NoGood, firstQuery);
        }
        finally
        {
            if (restoreKind == 1)
            {
                parameters.P = savedP;
            }
            else if (restoreKind == 2)
            {
                parameters.Ph2[0] = savedPh2;
                parameters.Wr2[0] = savedWr2;
            }
        }
    }

    /// <summary>
    /// 特別予約語(MCB/ELB/MMCB/ELMB/SB/RMCB/RELB/RMMCB/RELMB/NHMB/HPSB/HSB/CP/CKS)専用の
    /// 直近上下位検索。【C原典】Fysk01_Chokisearch_T(Fysk01.c:4588, static SHORT)。
    /// 二次形状(専用機能変換)→形状→メーカーの順に総当たりし、各組合せで電気値(AT/AF/MA/AM)を
    /// 設定・三菱フレーム補完してから定格値キーを作り、直近上下位ファイルを検索して最初に該当した
    /// 候補を採用する。電気値設定が負(SYS_ERR)なら即中断する。該当なしのときは先頭 dtype で電流値を
    /// 再設定し、キーは先頭候補(定格値キーは空白化)を返す。【C原典】cpf は本関数では未使用、stn[0]=-1 固定。
    /// </summary>
    /// <param name="table">予約語別チェック情報。【C原典】tbl(TCHI_TBL)。</param>
    /// <param name="electricalParameterNo">電気パラメータ番号(1 or 2)。【C原典】epno。</param>
    /// <param name="parameters">電気パラメータ sep[0..2](設定先=sep[epno])。【C原典】sep[]。</param>
    /// <param name="inputFlags">入力有無フラグ。【C原典】sfg[]。</param>
    /// <param name="shapeTypes">変換形状タイプ一覧。【C原典】wtype(tsu 件)。</param>
    /// <param name="shapeTypeIndex">変換タイプ位置。【C原典】ti。</param>
    /// <param name="dataTypes">データタイプ(7枠)。【C原典】dtype。</param>
    /// <param name="makerCodes">変換メーカーコード一覧。【C原典】mcod(msu 件, 各3桁)。</param>
    /// <param name="productName">品名。【C原典】hinm。</param>
    /// <param name="handleLockFlag">ハンドルロック有無チェックフラグ。【C原典】hfg。</param>
    /// <param name="work">選定ワーク(負荷容量/通電電流等)。【C原典】wk1。</param>
    /// <param name="flags">項目書替えフラグ。【C原典】wk3。</param>
    /// <param name="candidates">直近上下位参照ファイル全候補(キー順)。【C原典】FYDF812 ISAM。</param>
    public static NearestRankSearchResult SearchSpecialReservedWord(
        RatingCheckTable table,
        int electricalParameterNo,
        NumericElectricalParameters[] parameters,
        IReadOnlyList<int> inputFlags,
        IReadOnlyList<string> shapeTypes,
        int shapeTypeIndex,
        string[] dataTypes,
        IReadOnlyList<string> makerCodes,
        string productName,
        short handleLockFlag,
        SelectionWorkParameters work,
        AreaRewriteFlags flags,
        IReadOnlyList<NearestRankReference> candidates)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(inputFlags);
        ArgumentNullException.ThrowIfNull(shapeTypes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(flags);
        ArgumentNullException.ThrowIfNull(candidates);

        string yo = table.ReservedWord;
        NumericElectricalParameters working = parameters[electricalParameterNo];

        // 【C原典】ck の第一キー: 電源 AC/DC 区分は sep[epno] からそのまま(DCPW 上書きなし)。
        char mainAcDc = working.V2Kbn;
        char controlAcDc = working.VcKbn;

        // 【C原典】Fysk01_Type_Check2 → 二次形状タイプ(専用機能変換)。
        ShapeTypeExpansion expansion = ShapeTypeExpander.Expand(yo, dataTypes);
        IReadOnlyList<string> secondaryShapeTypes = expansion.ShapeTypes;
        int secondaryIndex = expansion.TypeIndex;

        // 【C原典】ptype 基底は dtype をそのまま(ALL と異なり WL/COS/WH 修正なし)。
        string[] baseTypes = BaseDataTypes(dataTypes);

        NearestRankReference? firstQuery = null;
        string[]? firstDataTypes = null;

        // 【C原典】二次形状(i)→形状(k)→メーカー(j)の順で総当たり(cpf 依存なし)。
        for (int i = 0; i < secondaryShapeTypes.Count; i++)
        {
            for (int k = 0; k < shapeTypes.Count; k++)
            {
                for (int j = 0; j < makerCodes.Count; j++)
                {
                    string makerCode = Slot(makerCodes, j, 3);

                    // 【C原典】dtype[ti2]=二次形状 / dtype[ti]=形状(ti==ti2 は形状優先)。
                    string[] currentTypes = (string[])baseTypes.Clone();
                    Assign(currentTypes, secondaryIndex, secondaryShapeTypes[i]);
                    Assign(currentTypes, shapeTypeIndex, shapeTypes[k]);

                    // 【C原典】ck の ptype/mkcd を設定。kteichi は Make_Teikakuchi 後に確定。
                    NearestRankReference query = BuildQuery(
                        yo, makerCode, baseTypes,
                        shapeTypeIndex, Pad(shapeTypes[k]),
                        secondaryIndex, Pad(secondaryShapeTypes[i]),
                        mainAcDc, controlAcDc, string.Empty, shapeOverridesSecondary: true);

                    firstQuery ??= query;
                    firstDataTypes ??= currentTypes;

                    // 【C原典】電流値等の設定。負なら SYS_ERR で即中断。
                    short set = AtAfMaAmSetter.Apply(
                        yo, electricalParameterNo, parameters, currentTypes, work, flags);
                    if (set != 0)
                    {
                        return new NearestRankSearchResult(NearestRankSearch.SystemError, query);
                    }

                    // 【C原典】三菱製ブレーカのフレーム設定。
                    MitsubishiFrameCurrentSetter.Apply(yo, makerCode, electricalParameterNo, parameters);

                    // 【C原典】Fysk04_Make_Teikakuchi で定格値キー(kteichi)を作成。
                    query.RatingKey = RatingKeyBuilder.MakeRatingKey(table.Entries, working);

                    // 【C原典】直近上下位ファイル検索&チェック(stn[0]=-1)。
                    NearestRankSearchResult r = NearestRankSearch.Search(
                        table, query, candidates, productName, handleLockFlag, working, inputFlags, -1);

                    if (r.Status == Good)
                    {
                        return r;
                    }
                }
            }
        }

        // 【C原典】該当なし。先頭 dtype で電流値を再設定し、キーは先頭候補(定格値キーは空白)。
        if (firstDataTypes is not null)
        {
            AtAfMaAmSetter.Apply(yo, electricalParameterNo, parameters, firstDataTypes, work, flags);
        }
        if (firstQuery is not null)
        {
            firstQuery.RatingKey = new string(' ', firstQuery.RatingKey.Length);
        }
        return new NearestRankSearchResult(NoGood, firstQuery);
    }

    /// <summary>
    /// 遮断器専用の直近上下位検索。【C原典】Fysk01_Chokisearch_BRK(Fysk01.c:1453)。
    /// 二次形状→形状→メーカーの順(特注/コンポ共通)で検索キーを組み立て、最初に該当した候補を採用する。
    /// ALL と異なり WL/COS/WH の特殊タイプ修正は行わず、ti==ti2 のときは形状タイプを優先する。
    /// </summary>
    public static NearestRankSearchResult SearchBreaker(
        RatingCheckTable table,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        IReadOnlyList<string> shapeTypes,
        int shapeTypeIndex,
        string[] dataTypes,
        IReadOnlyList<string> makerCodes,
        string productName,
        short handleLockFlag,
        int contactFlag,
        IReadOnlyList<NearestRankReference> candidates)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(shapeTypes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(candidates);

        string yo = table.ReservedWord;
        char mainAcDc = parameters.V2Kbn;
        char controlAcDc = parameters.VcKbn;

        // 【C原典】LGT/LA の退避・復元(遮断器予約語では発生しないが C に合わせて保持)。
        int restoreKind = 0;
        double savedP = 0.0, savedPh2 = 0.0, savedWr2 = 0.0;
        if (Matches(yo, "LGT ", 4))
        {
            savedP = parameters.P;
            parameters.P = 1.0;
            restoreKind = 1;
        }
        else if (Matches(yo, "LA ", 3) &&
                 (Matches(dataTypes[0], "ST ", 3) || Matches(dataTypes[0], "YT ", 3)))
        {
            savedPh2 = parameters.Ph2[0];
            savedWr2 = parameters.Wr2[0];
            parameters.Ph2[0] = 0.0;
            parameters.Wr2[0] = 0.0;
            restoreKind = 2;
        }

        try
        {
            ShapeTypeExpansion expansion = ShapeTypeExpander.Expand(yo, dataTypes);
            IReadOnlyList<string> secondaryShapeTypes = expansion.ShapeTypes;
            int secondaryIndex = expansion.TypeIndex;
            string ratingKey = RatingKeyBuilder.MakeRatingKey(table.Entries, parameters);

            // 【C原典】ptype[0..6] = dtype(遵断器は特殊タイプ修正なし)。
            string[] baseTypes = new string[NearestRankReference.ParameterTypeSlotCount];
            for (int n = 0; n < baseTypes.Length; n++)
            {
                baseTypes[n] = Pad(n < dataTypes.Length ? dataTypes[n] : string.Empty);
            }

            NearestRankReference? firstQuery = null;

            // 【C原典】二次形状(i) → 形状(k) → メーカー(j) の順。
            for (int i = 0; i < secondaryShapeTypes.Count; i++)
            {
                for (int k = 0; k < shapeTypes.Count; k++)
                {
                    for (int j = 0; j < makerCodes.Count; j++)
                    {
                        NearestRankReference query = BuildQuery(
                            yo, Slot(makerCodes, j, 3), baseTypes,
                            shapeTypeIndex, Pad(shapeTypes[k]),
                            secondaryIndex, Pad(secondaryShapeTypes[i]),
                            mainAcDc, controlAcDc, ratingKey, shapeOverridesSecondary: true);

                        firstQuery ??= query;

                        NearestRankSearchResult r = NearestRankSearch.Search(
                            table, query, candidates, productName, handleLockFlag, parameters, inputFlags, contactFlag);

                        if (r.Status == Good)
                        {
                            return r;
                        }
                    }
                }
            }

            return new NearestRankSearchResult(NoGood, firstQuery);
        }
        finally
        {
            if (restoreKind == 1)
            {
                parameters.P = savedP;
            }
            else if (restoreKind == 2)
            {
                parameters.Ph2[0] = savedPh2;
                parameters.Wr2[0] = savedWr2;
            }
        }
    }

    // 【C原典】ptype[0..6]=dtype + WL(LED)/COS/WH の特殊修正。
    private static string[] BuildBaseTypes(string yo, string[] dataTypes,
                                           IReadOnlyList<string> makerCodes, NumericElectricalParameters sep)
    {
        string[] types = new string[NearestRankReference.ParameterTypeSlotCount];
        for (int n = 0; n < types.Length; n++)
        {
            types[n] = Pad(n < dataTypes.Length ? dataTypes[n] : string.Empty);
        }

        // 【C原典】改訂<1> スマート操作パネル取付 WL は径20でLEDタイプ。
        if (Matches(yo, "WL ", 3) && sep.Ksize == 20.0)
        {
            types[3] = Pad("LED");
        }

        // 【C原典】改訂<7>/<12> COS の 1A1B/3N・イズミ・径25 は 2A で選定。
        if (Matches(yo, "COS", 3))
        {
            if (Matches(types[2], "1A1B ", 5) && Matches(types[3], "3N ", 3) &&
                Matches(Slot(makerCodes, 0, 2), "IZ", 2) && sep.Ksize == 25.0)
            {
                types[2] = Pad("2A ");
            }
        }
        // 【C原典】改訂<15> WH の計器箱木板 NA は機器マスタに無いため NOTHING に。
        else if (Matches(yo, "WH", 2))
        {
            if (Matches(types[5], "NA ", 3))
            {
                types[5] = Pad("NOTHING");
            }
        }

        return types;
    }

    // 【C原典】cpf による外側ループ順の切替(特注=メーカー先/コンポ=形状先)。
    private static IEnumerable<(int MakerIdx, int ShapeIdx)> EnumeratePairs(int makerCount, int shapeCount, bool makerFirst)
    {
        if (makerFirst)
        {
            for (int j = 0; j < makerCount; j++)
            {
                for (int k = 0; k < shapeCount; k++)
                {
                    yield return (j, k);
                }
            }
        }
        else
        {
            for (int k = 0; k < shapeCount; k++)
            {
                for (int j = 0; j < makerCount; j++)
                {
                    yield return (j, k);
                }
            }
        }
    }

    private static NearestRankReference BuildQuery(
        string reservedWord, string makerCode, string[] baseTypes,
        int shapeIndex, string shapeType, int secondaryIndex, string secondaryType,
        char mainAcDc, char controlAcDc, string ratingKey, bool shapeOverridesSecondary)
    {
        string[] types = (string[])baseTypes.Clone();

        // 【C原典】ptype[ti]/ptype[ti2] の上書き順。ti==ti2 のとき後に適用した方が残る。
        if (shapeOverridesSecondary)
        {
            Assign(types, secondaryIndex, secondaryType);
            Assign(types, shapeIndex, shapeType);
        }
        else
        {
            Assign(types, shapeIndex, shapeType);
            Assign(types, secondaryIndex, secondaryType);
        }

        return new NearestRankReference
        {
            ReservedWord = reservedWord,
            MakerCode = makerCode,
            ParameterTypes = types,
            MainPowerAcDc = mainAcDc,
            ControlPowerAcDc = controlAcDc,
            RatingKey = ratingKey,
        };
    }

    // 【C原典】遮断器系予約語(Fysk01_Chokisearch の BRK 分岐条件)。
    private static bool IsBreaker(string yo) =>
        Matches(yo, "ELB ", 4) || Matches(yo, "MCB ", 4) || Matches(yo, "MMCB ", 5) ||
        Matches(yo, "ELMB ", 5) || Matches(yo, "RMCB ", 5) || Matches(yo, "RELB ", 5) ||
        Matches(yo, "RMMCB ", 6) || Matches(yo, "RELMB ", 6);

    /// <summary>
    /// THR/MC/MG/MGSD/XERY 専用の直近上下位検索。【C原典】Fysk01_Chokisearch_MTG(Fysk01.c:1268)。
    /// メーカー・形状を変えて <see cref="NearestRankSearch.SearchMotorGroup"/> を呼び、候補が得られた
    /// 最初の(メーカー,形状)の二次形状候補から、PC_1/PC_3 は <see cref="EquipmentSelector.CompareCandidate"/> で
    /// 最良 1 件を選ぶ(他は最後の該当を採用)。
    /// </summary>
    public static NearestRankSearchResult SearchMotorSwitch(
        int specKind,
        RatingCheckTable table,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        IReadOnlyList<string> shapeTypes,
        int shapeTypeIndex,
        string[] dataTypes,
        IReadOnlyList<string> makerCodes,
        string productName,
        short handleLockFlag,
        IReadOnlyList<NearestRankReference> candidates)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(shapeTypes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(candidates);

        string yo = table.ReservedWord;
        char mainAcDc = parameters.V2Kbn;
        char controlAcDc = parameters.VcKbn;

        ShapeTypeExpansion expansion = ShapeTypeExpander.Expand(yo, dataTypes);
        IReadOnlyList<string> secondaryShapeTypes = expansion.ShapeTypes;
        int secondaryIndex = expansion.TypeIndex;
        string ratingKey = RatingKeyBuilder.MakeRatingKey(table.Entries, parameters);

        string[] baseTypes = new string[NearestRankReference.ParameterTypeSlotCount];
        for (int n = 0; n < baseTypes.Length; n++)
        {
            baseTypes[n] = Pad(n < dataTypes.Length ? dataTypes[n] : string.Empty);
        }

        short proc = table.ProcessNumber;
        bool selectBest = proc == NearestRankSearch.Pc1Thr || proc == NearestRankSearch.Pc3Mg;

        NearestRankReference? firstQuery = null;
        NearestRankReference? best = null;
        double[] bestPair = new double[2];
        double bestVoltage = 0.0;
        int found = 0;

        IEnumerable<(int MakerIdx, int ShapeIdx)> order = specKind == 0
            ? EnumeratePairs(makerCodes.Count, shapeTypes.Count, makerFirst: true)
            : EnumeratePairs(makerCodes.Count, shapeTypes.Count, makerFirst: false);

        foreach ((int j, int k) in order)
        {
            for (int i = 0; i < secondaryShapeTypes.Count; i++)
            {
                NearestRankReference query = BuildQuery(
                    yo, Slot(makerCodes, j, 3), baseTypes,
                    shapeTypeIndex, Pad(shapeTypes[k]),
                    secondaryIndex, Pad(secondaryShapeTypes[i]),
                    mainAcDc, controlAcDc, ratingKey, shapeOverridesSecondary: false);

                firstQuery ??= query;

                MotorGroupSearchResult r = NearestRankSearch.SearchMotorGroup(
                    table, query, candidates, productName, handleLockFlag, parameters, inputFlags);

                if (r.Status != Good || r.Selected is null)
                {
                    continue;
                }

                if (selectBest)
                {
                    if (found == 0 ||
                        EquipmentSelector.CompareCandidate(
                            proc, parameters.At, r.AmpereTripPair, r.Voltage, Key50(r.Selected),
                            bestPair, bestVoltage, Key50(best!)) == 1)
                    {
                        best = r.Selected;
                        bestPair = r.AmpereTripPair;
                        bestVoltage = r.Voltage;
                    }
                }
                else
                {
                    best = r.Selected;
                }
                found++;
            }

            // 【C原典】当該(メーカー,形状)で該当が得られたら確定。
            if (found > 0)
            {
                return new NearestRankSearchResult(Good, best);
            }
        }

        return new NearestRankSearchResult(NoGood, firstQuery);
    }

    private static string Key50(NearestRankReference reference) =>
        (reference.RatingKey ?? string.Empty).PadRight(50)[..50];

    /// <summary>
    /// PBS 専用の直近上下位検索。【C原典】Fysk01_Chokisearch_PBS(Fysk01.c:853)。
    /// Type_Check2 の二次形状(ti2)に加え、Type_Check3 の接点タイプ(ti3)をも展開して
    /// メーカー×形状×ti3×ti2 の総当たりで検索し、最初に該当した候補を採用する。
    /// </summary>
    public static NearestRankSearchResult SearchPushButton(
        int specKind,
        RatingCheckTable table,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        IReadOnlyList<string> shapeTypes,
        int shapeTypeIndex,
        string[] dataTypes,
        IReadOnlyList<string> makerCodes,
        string productName,
        short handleLockFlag,
        int contactFlag,
        IReadOnlyList<NearestRankReference> candidates)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(shapeTypes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(candidates);

        string yo = table.ReservedWord;
        char mainAcDc = parameters.V2Kbn;
        char controlAcDc = parameters.VcKbn;

        ShapeTypeExpansion secondaryExpansion = ShapeTypeExpander.Expand(yo, dataTypes);
        IReadOnlyList<string> secondaryShapeTypes = secondaryExpansion.ShapeTypes;
        int secondaryIndex = secondaryExpansion.TypeIndex;

        ShapeTypeExpansion thirdExpansion = ShapeTypeExpander.ExpandSecondary(yo, dataTypes);
        IReadOnlyList<string> thirdShapeTypes = thirdExpansion.ShapeTypes;
        int thirdIndex = thirdExpansion.TypeIndex;

        string ratingKey = RatingKeyBuilder.MakeRatingKey(table.Entries, parameters);
        string[] baseTypes = BaseDataTypes(dataTypes);

        NearestRankReference? firstQuery = null;

        IEnumerable<(int MakerIdx, int ShapeIdx)> order = specKind == 0
            ? EnumeratePairs(makerCodes.Count, shapeTypes.Count, makerFirst: true)
            : EnumeratePairs(makerCodes.Count, shapeTypes.Count, makerFirst: false);

        foreach ((int j, int k) in order)
        {
            for (int n = 0; n < thirdShapeTypes.Count; n++)
            {
                for (int i = 0; i < secondaryShapeTypes.Count; i++)
                {
                    string[] types = (string[])baseTypes.Clone();
                    Assign(types, shapeTypeIndex, Pad(shapeTypes[k]));
                    Assign(types, thirdIndex, Pad(thirdShapeTypes[n]));
                    Assign(types, secondaryIndex, Pad(secondaryShapeTypes[i]));

                    NearestRankReference query = BuildQueryFromTypes(
                        yo, Slot(makerCodes, j, 3), types, mainAcDc, controlAcDc, ratingKey);
                    firstQuery ??= query;

                    NearestRankSearchResult r = NearestRankSearch.Search(
                        table, query, candidates, productName, handleLockFlag, parameters, inputFlags, contactFlag);

                    if (r.Status == Good)
                    {
                        return r;
                    }
                }
            }
        }

        return new NearestRankSearchResult(NoGood, firstQuery);
    }

    /// <summary>
    /// CT 専用の直近上下位検索。【C原典】Fysk01_Chokisearch_CT(Fysk01.c:702)。
    /// 定格電流(A1)を n 倍(1,2,3…)しながら 1200 を超えるまで定格値キーを再生成して検索し、
    /// 最初に該当した候補を採用する(CT 比換比の直近上位探索)。
    /// ※改訂&lt;34&gt; の PropSelChkCT(cns LW CT 選定パラメータ)は後続移植のため未適用。
    /// QrespoPlus の最低電流 5A 補正は zoneCode 指定時のみ適用(未指定は A1&lt;=0 で探索省略)。
    /// </summary>
    public static NearestRankSearchResult SearchCurrentTransformer(
        int specKind,
        RatingCheckTable table,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        IReadOnlyList<string> shapeTypes,
        int shapeTypeIndex,
        string[] dataTypes,
        IReadOnlyList<string> makerCodes,
        string productName,
        IReadOnlyList<NearestRankReference> candidates,
        string zoneCode = "")
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(shapeTypes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(candidates);

        // 【C原典】改訂<36> QrespoPlus は末端機器が無い場合の無限ループ回避で最低電流 5A。
        if ((zoneCode == "33333" || zoneCode == "33334" || zoneCode == "33335") && parameters.A1 <= 0.0)
        {
            parameters.A1 = 5.0;
        }

        string yo = table.ReservedWord;
        char mainAcDc = parameters.V2Kbn;
        char controlAcDc = parameters.VcKbn;

        ShapeTypeExpansion expansion = ShapeTypeExpander.Expand(yo, dataTypes);
        IReadOnlyList<string> secondaryShapeTypes = expansion.ShapeTypes;
        int secondaryIndex = expansion.TypeIndex;
        string[] baseTypes = BaseDataTypes(dataTypes);

        double baseCurrent = parameters.A1;
        NearestRankReference? firstQuery = null;

        try
        {
            IEnumerable<(int MakerIdx, int ShapeIdx)> order = specKind == 0
                ? EnumeratePairs(makerCodes.Count, shapeTypes.Count, makerFirst: true)
                : EnumeratePairs(makerCodes.Count, shapeTypes.Count, makerFirst: false);

            foreach ((int j, int k) in order)
            {
                for (int i = 0; i < secondaryShapeTypes.Count; i++)
                {
                    string[] types = (string[])baseTypes.Clone();
                    Assign(types, shapeTypeIndex, Pad(shapeTypes[k]));
                    Assign(types, secondaryIndex, Pad(secondaryShapeTypes[i]));

                    // 【C原典】A1 を n 倍しながら kteichi を再生成して検索(A1<=0 は無限ループ回避で打切り)。
                    for (int scale = 1; ; scale++)
                    {
                        parameters.A1 = baseCurrent * scale;
                        if (parameters.A1 > 1200.0 || baseCurrent <= 0.0)
                        {
                            break;
                        }

                        string ratingKey = RatingKeyBuilder.MakeRatingKey(table.Entries, parameters);
                        NearestRankReference query = BuildQueryFromTypes(
                            yo, Slot(makerCodes, j, 3), types, mainAcDc, controlAcDc, ratingKey);
                        firstQuery ??= query;

                        NearestRankSearchResult r = NearestRankSearch.Search(
                            table, query, candidates, productName, -1, parameters, inputFlags, -1);

                        if (r.Status == Good)
                        {
                            return r;
                        }
                    }
                }
            }

            return new NearestRankSearchResult(NoGood, firstQuery);
        }
        finally
        {
            parameters.A1 = baseCurrent;
        }
    }

    private static string[] BaseDataTypes(string[] dataTypes)
    {
        string[] types = new string[NearestRankReference.ParameterTypeSlotCount];
        for (int n = 0; n < types.Length; n++)
        {
            types[n] = Pad(n < dataTypes.Length ? dataTypes[n] : string.Empty);
        }
        return types;
    }

    private static NearestRankReference BuildQueryFromTypes(
        string reservedWord, string makerCode, string[] types,
        char mainAcDc, char controlAcDc, string ratingKey)
        => new()
        {
            ReservedWord = reservedWord,
            MakerCode = makerCode,
            ParameterTypes = types,
            MainPowerAcDc = mainAcDc,
            ControlPowerAcDc = controlAcDc,
            RatingKey = ratingKey,
        };

    private static void Assign(string[] types, int index, string value)
    {
        if (index >= 0 && index < types.Length)
        {
            types[index] = value;
        }
    }

    private static string Slot(IReadOnlyList<string> list, int index, int width)
    {
        string value = index >= 0 && index < list.Count ? list[index] ?? string.Empty : string.Empty;
        return value.PadRight(width)[..width];
    }

    private static string Pad(string value) => value.PadRight(TypeWidth)[..TypeWidth];

    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
