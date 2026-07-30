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
            throw new NotImplementedException("Fysk01_Chokisearch_MTG(THR/MC/MG/MGSD) は後続バッチで移植予定です。");
        }
        if (Matches(yo, "CT ", 3))
        {
            throw new NotImplementedException("Fysk01_Chokisearch_CT は後続バッチで移植予定です。");
        }
        if (Matches(yo, "PBS ", 4))
        {
            throw new NotImplementedException("Fysk01_Chokisearch_PBS は後続バッチで移植予定です。");
        }
        if (Matches(yo, "SC  ", 4))
        {
            throw new NotImplementedException("Fysk01_Chokisearch_SC は後続バッチで移植予定です。");
        }
        if (IsBreaker(yo))
        {
            throw new NotImplementedException("Fysk01_Chokisearch_BRK は後続バッチで移植予定です。");
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
                        mainAcDc, controlAcDc, ratingKey);

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
        char mainAcDc, char controlAcDc, string ratingKey)
    {
        string[] types = (string[])baseTypes.Clone();
        if (shapeIndex >= 0 && shapeIndex < types.Length)
        {
            types[shapeIndex] = shapeType;
        }
        if (secondaryIndex >= 0 && secondaryIndex < types.Length)
        {
            types[secondaryIndex] = secondaryType;
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

    private static string Slot(IReadOnlyList<string> list, int index, int width)
    {
        string value = index >= 0 && index < list.Count ? list[index] ?? string.Empty : string.Empty;
        return value.PadRight(width)[..width];
    }

    private static string Pad(string value) => value.PadRight(TypeWidth)[..TypeWidth];

    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
