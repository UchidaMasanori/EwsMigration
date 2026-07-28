using Ews.Domain.Analysis;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// 直近上下位参照ファイル(FYDF812)検索による機器選定。
///
/// 【C原典】toku/sekkei/src/Fysk01.c
///   - <c>Fysk01_Chokkin_Read_Check</c>(:2342)   … 検索方式の振り分け(TM/THSW は専用検索)。
///   - <c>Fysk01_Chokkin_Read_Check_ALL</c>(:2389) … 主/複/制御回路用。前方一致した候補を先頭から
///       走査し、定格値チェック・品名・ハンドルロックを満たす最初の候補を採用する。
///   - <c>Fysk01_Chokkin_Read_Check_TMS</c>(:2389) … TM/THSW 専用。範囲を持つ候補(CMP_2>TOL)は
///       中点距離(<see cref="EquipmentSelector.CompareByMidpointDistance"/>)で最良 1 件を選ぶ。
///   - <c>Fysk0a_CmpMojisu_Get</c>(Fysk0a.c:172)   … 入力有無に応じた定格値比較文字数を求める。
///
/// レガシーは ISAM(<c>FyIsamGStartR</c>/<c>FyIsamGNextR</c>)でキー前方一致範囲を走査するが、
/// 本移植では固定長テキストから読み込んだ候補一覧(<see cref="NearestRankReference"/>)を
/// KEY 先頭 <c>siz</c> バイトで絞り込んで走査する。FYDF812 はキー順に出力されているため、
/// 前方一致した候補群はキー順の連続ブロックと等価になる。
///
/// 接点計算(<c>Fysk01_Get_Seten_GoodData</c>、制御回路のみ)は今後の増分で追加する。
/// 定数(fyrt808.h): GOOD=0 / NOGOOD=1 / REPEAT=2 / SYS_ERR=-1 / TOL=0.001 / PC_5=15(THSW) / PC_6=16(TM)。
/// </summary>
public static class NearestRankSearch
{
    /// <summary>該当あり。【C原典】GOOD == 0。</summary>
    public const int Good = 0;

    /// <summary>該当なし。【C原典】NOGOOD == 1。</summary>
    public const int NoGood = 1;

    /// <summary>システムエラー。【C原典】SYS_ERR == -1。</summary>
    public const int SystemError = -1;

    /// <summary>再試行(ma 入力なし・1 回目)。【C原典】REPEAT == 2。</summary>
    private const int Repeat = 2;

    /// <summary>実数一致許容誤差。【C原典】TOL == 0.001。</summary>
    private const double Tolerance = 0.001;

    /// <summary>THSW 専用検索のプロセス番号。【C原典】PC_5 == 15。</summary>
    public const short Pc5Thsw = 15;

    /// <summary>TM 専用検索のプロセス番号。【C原典】PC_6 == 16。</summary>
    public const short Pc6Tm = 16;

    /// <summary>
    /// 直近上下位参照ファイル検索の入口(検索方式で下請けへ分岐)。
    /// 【C原典】<c>Fysk01_Chokkin_Read_Check</c>(Fysk01.c:2342)。
    /// proc_no が PC_5(THSW)/PC_6(TM)なら中点距離選択、その他は先頭一致採用。
    /// </summary>
    /// <param name="table">予約語別チェック情報。【C原典】TCHI_TBL tbl。</param>
    /// <param name="query">KEY 部入力済みの検索キー。【C原典】struct FYDF812 *data。</param>
    /// <param name="candidates">直近上下位参照ファイル全候補(キー順)。【C原典】FYDF812 ISAM。</param>
    /// <param name="productName">品名(絞り込み)。【C原典】CHAR *hinm。</param>
    /// <param name="handleLockFlag">ハンドルロックチェックフラグ。【C原典】SHORT hfg。</param>
    /// <param name="parameters">数値化済み電気パラメータ。【C原典】eparmg_s *sep。</param>
    /// <param name="inputFlags">入力有無チェック。【C原典】CHAR sfg[]。</param>
    /// <param name="contactCalculationFlag">接点計算要否(制御回路のみ、-1=不要)。【C原典】stn[0]。</param>
    public static NearestRankSearchResult Search(
        RatingCheckTable table,
        NearestRankReference query,
        IReadOnlyList<NearestRankReference> candidates,
        string productName,
        short handleLockFlag,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        int contactCalculationFlag)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (table.ProcessNumber == Pc5Thsw || table.ProcessNumber == Pc6Tm)
        {
            return SearchClosestByMidpoint(table, query, candidates, productName, handleLockFlag, parameters, inputFlags, contactCalculationFlag);
        }

        return SearchFirstMatch(table, query, candidates, productName, handleLockFlag, parameters, inputFlags, contactCalculationFlag);
    }

    /// <summary>
    /// 主/複/制御回路用の候補抽出。【C原典】<c>Fysk01_Chokkin_Read_Check_ALL</c>(Fysk01.c:2389)。
    /// 前方一致した候補を先頭から走査し、定格値チェック(GOOD)・品名一致・ハンドルロック条件を
    /// すべて満たす最初の候補を採用する。ma 入力なしの 1 回目で REPEAT が出た場合は回数を進めて再走査する。
    /// </summary>
    public static NearestRankSearchResult SearchFirstMatch(
        RatingCheckTable table,
        NearestRankReference query,
        IReadOnlyList<NearestRankReference> candidates,
        string productName,
        short handleLockFlag,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        int contactCalculationFlag)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(inputFlags);

        int compareSize = ComputeMatchSize(table, inputFlags);
        List<NearestRankReference> matches = FrontMatch(query, candidates, compareSize);
        var comparison = new RatingComparisonState();

        int times = 1;
        while (true)
        {
            bool needRepeat = false;

            foreach (NearestRankReference candidate in matches)
            {
                NumericSharedInfo scd = SharedInfoConverter.Convert(candidate);
                int ret = RatingValueChecker.Check(
                    table.Flag, table.Entries, parameters, inputFlags, candidate.RatingKey, scd, times, comparison);

                if (ret == Repeat)
                {
                    // 【C原典】if(ret==REPEAT){ marepeat=1; break; }
                    needRepeat = true;
                    continue;
                }

                // 【C原典】if(ret != GOOD) break;(次候補へ)
                if (ret != Good)
                {
                    continue;
                }

                if (ProductNameChecker.Check(productName, candidate.ProductName) != ProductNameChecker.Good)
                {
                    continue;
                }

                // 【C原典】if(hfg > -1 && tmp.hlkbn != 'H') break;
                if (handleLockFlag > -1 && candidate.HandleLockKind != 'H')
                {
                    continue;
                }

                RequireNoContactCalculation(contactCalculationFlag);
                return new NearestRankSearchResult(Good, candidate);
            }

            // 【C原典】ERR_ISAM_NOTHING:(前方一致範囲を走査し切った)
            if (needRepeat)
            {
                // 【C原典】marepeat==1 なら times++ で再走査。
                times++;
                continue;
            }

            return new NearestRankSearchResult(NoGood, null);
        }
    }

    /// <summary>
    /// TM/THSW 専用の候補抽出。【C原典】<c>Fysk01_Chokkin_Read_Check_TMS</c>(Fysk01.c:2389)。
    /// 条件を満たす候補のうち、範囲を持つもの(CMP_2 &gt; TOL)は基準値と幅中点の距離で最良 1 件を選ぶ。
    /// 範囲を持たない候補(CMP_2 &lt;= TOL)が現れた時点で即採用する。
    /// </summary>
    public static NearestRankSearchResult SearchClosestByMidpoint(
        RatingCheckTable table,
        NearestRankReference query,
        IReadOnlyList<NearestRankReference> candidates,
        string productName,
        short handleLockFlag,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        int contactCalculationFlag)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(inputFlags);

        int compareSize = ComputeMatchSize(table, inputFlags);
        List<NearestRankReference> matches = FrontMatch(query, candidates, compareSize);
        var comparison = new RatingComparisonState();

        int times = 1;
        int count = 0;                          // 【C原典】cnt
        NearestRankReference? best = null;      // 【C原典】ckwk
        double[] bestPair = new double[2];      // 【C原典】wk[2]

        while (true)
        {
            bool needRepeat = false;

            foreach (NearestRankReference candidate in matches)
            {
                NumericSharedInfo scd = SharedInfoConverter.Convert(candidate);
                int ret = RatingValueChecker.Check(
                    table.Flag, table.Entries, parameters, inputFlags, candidate.RatingKey, scd, times, comparison);

                if (ret == Repeat)
                {
                    needRepeat = true;
                    continue;
                }

                if (ret != Good)
                {
                    continue;
                }

                if (ProductNameChecker.Check(productName, candidate.ProductName) != ProductNameChecker.Good)
                {
                    continue;
                }

                if (handleLockFlag > -1 && candidate.HandleLockKind != 'H')
                {
                    continue;
                }

                double cmp2 = comparison.AmpereTripSecond;
                if (cmp2 > Tolerance)
                {
                    // 【C原典】幅を持つ候補は中点距離で最良を保持する。
                    double[] pair = { comparison.AmpereTripPair[0], comparison.AmpereTripPair[1] };
                    if (count == 0)
                    {
                        best = candidate;
                        bestPair = pair;
                        count++;
                    }
                    else
                    {
                        short chk = EquipmentSelector.CompareByMidpointDistance(cmp2, pair, bestPair);
                        if (chk == 1)
                        {
                            best = candidate;
                            bestPair = pair;
                        }
                    }

                    continue;
                }

                // 【C原典】CMP_2 <= TOL は即採用。
                RequireNoContactCalculation(contactCalculationFlag);
                return new NearestRankSearchResult(Good, candidate);
            }

            // 前方一致範囲を走査し切った。
            if (needRepeat)
            {
                times++;
                continue;
            }

            if (count == 0)
            {
                return new NearestRankSearchResult(NoGood, null);
            }

            RequireNoContactCalculation(contactCalculationFlag);
            return new NearestRankSearchResult(Good, best);
        }
    }

    /// <summary>
    /// 前方一致サイズを求める(KEY 部 62 + 定格値比較文字数)。
    /// 【C原典】siz = (sfg[0]==0 ? tbl.cpsize : Fysk0a_CmpMojisu_Get(tbl,sfg)) + 62。
    /// </summary>
    private static int ComputeMatchSize(RatingCheckTable table, IReadOnlyList<int> inputFlags)
    {
        int size = Flag(inputFlags, 0) == 0 ? table.ReadSize : ComputeCompareSize(table, inputFlags);
        return size + NearestRankReference.KeyPrefixLength;
    }

    /// <summary>
    /// 定格値比較文字数の作成。【C原典】<c>Fysk0a_CmpMojisu_Get</c>(Fysk0a.c:172)。
    /// 展開情報を先頭から辿り、入力ありかつ選択区分が -3/-1 でない項目の幅を積み上げる。
    /// 終端・入力なし・選択区分 -3/-1 のいずれかで打ち切る。
    /// </summary>
    public static short ComputeCompareSize(RatingCheckTable table, IReadOnlyList<int> inputFlags)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(inputFlags);

        short size = 0;
        foreach (RatingKeyTableEntry entry in table.Entries)
        {
            if (entry.IsEnd)
            {
                break;
            }

            if (Flag(inputFlags, entry.ItemNo) == 0)
            {
                break;
            }

            if (entry.SelectFlag == -3 || entry.SelectFlag == -1)
            {
                break;
            }

            size += entry.Width;
        }

        return size;
    }

    /// <summary>KEY 先頭 <paramref name="compareSize"/> バイトが検索キーと一致する候補を抽出する。</summary>
    private static List<NearestRankReference> FrontMatch(
        NearestRankReference query, IReadOnlyList<NearestRankReference> candidates, int compareSize)
    {
        string queryKey = query.BuildComparisonKey();
        int size = Math.Clamp(compareSize, 0, queryKey.Length);
        string prefix = queryKey[..size];

        var matches = new List<NearestRankReference>();
        foreach (NearestRankReference candidate in candidates)
        {
            string candidateKey = candidate.BuildComparisonKey();
            if (candidateKey.Length >= size && candidateKey.AsSpan(0, size).SequenceEqual(prefix))
            {
                matches.Add(candidate);
            }
        }

        return matches;
    }

    /// <summary>接点計算が必要な場合は未移植として通知する。【C原典】Fysk01_Get_Seten_GoodData。</summary>
    private static void RequireNoContactCalculation(int contactCalculationFlag)
    {
        if (contactCalculationFlag > -1)
        {
            throw new NotSupportedException(
                "接点計算(Fysk01_Get_Seten_GoodData、制御回路のみ)は未移植です。今後の増分で追加します。");
        }
    }

    private static int Flag(IReadOnlyList<int> inputFlags, int index)
        => index >= 0 && index < inputFlags.Count ? inputFlags[index] : 0;
}

/// <summary>
/// 直近上下位参照ファイル検索の結果。【C原典】<c>Fysk01_Chokkin_Read_Check*</c> の戻り値(SHORT)と
/// 更新後の <c>data</c>(struct FYDF812)。
/// </summary>
/// <param name="Status">GOOD(0)/NOGOOD(1)/SYS_ERR(-1)。</param>
/// <param name="Selected">採用候補(該当なしは null)。【C原典】memcpy(data, &amp;tmp/&amp;ckwk, ...)。</param>
public readonly record struct NearestRankSearchResult(int Status, NearestRankReference? Selected);
