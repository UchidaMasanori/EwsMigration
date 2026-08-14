using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// 直近上下位該当データ群から、接点数がよりよい 1 件を選定する。
/// 【C原典】<c>Fysk01_Get_Seten_GoodData</c>(toku/sekkei/src/Fysk01.c:4314)。
/// 選定済みレコード(<paramref name="query"/>)のキー(KEY 部 62 バイト + 定格値有効桁 size バイト)で
/// 直近上下位参照ファイル(CK)を前方一致再検索し、該当群に対して
/// <see cref="BestContactCountSelector"/> で最適な接点数のレコードへ絞り込む。
/// </summary>
public static class ContactGoodDataSelector
{
    /// <summary>正常終了。【C原典】GOOD(0)。</summary>
    private const int Good = 0;

    /// <summary>該当なし。【C原典】NOGOOD。</summary>
    private const int NoGood = 1;

    /// <summary>
    /// 接点のよりよいデータを選定する。
    /// 【C原典】<c>Fysk01_Get_Seten_GoodData(data, stn, size)</c>。
    /// </summary>
    /// <param name="query">選定済みの該当データ(検索キー元)。【C原典】struct FYDF812 *data(I/O)。</param>
    /// <param name="candidates">直近上下位参照ファイル全候補(キー順)。【C原典】FNAME_CK の ISAM。</param>
    /// <param name="compareSize">定格値有効桁数(kteichi の比較バイト数)。【C原典】size(=tbl.seten)。</param>
    /// <param name="usedA">使用 A 接点数。【C原典】stn[0]。</param>
    /// <param name="usedB">使用 B 接点数。【C原典】stn[1]。</param>
    /// <param name="usedC">使用 C 接点数。【C原典】stn[2]。</param>
    /// <returns>
    /// Status=GOOD(0) と選定レコード、または Status=NOGOOD(1)(該当なし。元の選定を保持)。
    /// 【C原典】memcpy(data, ckwk+rec, ...) / return GOOD|NOGOOD。
    /// </returns>
    public static NearestRankSearchResult Select(
        NearestRankReference query,
        IReadOnlyList<NearestRankReference> candidates,
        int compareSize,
        int usedA,
        int usedB,
        int usedC)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);

        // 【C原典】csize = size + 62 バイトの前方一致で該当データ群 ckwk を収集する。
        int prefixLength = NearestRankReference.KeyPrefixLength + Math.Max(0, compareSize);
        List<NearestRankReference> matches = FrontMatch(query, candidates, prefixLength);

        // 【C原典】cnt==0 は NOGOOD を返し data を更新しない(元の選定を保持)。
        if (matches.Count == 0)
        {
            return new NearestRankSearchResult(NoGood, query);
        }

        // 【C原典】cnt>1 は Best_Cont_Count で選定、cnt==1 は先頭(rec=0)。
        int rec = matches.Count > 1
            ? BestContactCountSelector.Select(matches, usedA, usedB, usedC)
            : 0;

        // 【移植注記】C原典は rec<0(Best_Cont_Count が NG)時に ckwk[-1] を参照する未定義動作となる。
        // 移植では元の選定を保持して安全側に倒す。
        if (rec < 0 || rec >= matches.Count)
        {
            return new NearestRankSearchResult(Good, query);
        }

        return new NearestRankSearchResult(Good, matches[rec]);
    }

    /// <summary>
    /// KEY 先頭 <paramref name="prefixLength"/> 文字が検索キーと一致する候補を抽出する。
    /// 【C原典】<c>memcmp(&amp;wk, data, csize)</c> による総なめ照合。
    /// </summary>
    private static List<NearestRankReference> FrontMatch(
        NearestRankReference query, IReadOnlyList<NearestRankReference> candidates, int prefixLength)
    {
        string queryKey = query.BuildComparisonKey();
        int size = Math.Clamp(prefixLength, 0, queryKey.Length);
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
}
