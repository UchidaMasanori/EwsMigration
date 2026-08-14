using Ews.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ContactGoodDataSelector"/>(【C原典】Fysk01_Get_Seten_GoodData)のテスト。
/// 選定済みキー(KEY 部 62 + 定格値 size バイト)で前方一致した該当群から、
/// 接点数がよりよい 1 件へ絞り込むことを検証する。
/// </summary>
public sealed class ContactGoodDataSelectorTests
{
    private const int Good = 0;
    private const int NoGood = 1;

    /// <summary>TS 用の定格値キー(ac@13, bc@15, cc@17 の 2 桁, 先頭に識別文字)を生成する。</summary>
    private static string TsKey(int a, int b, int c, char head = ' ')
    {
        char[] k = new string(' ', 50).ToCharArray();
        k[0] = head;
        WriteNum(k, 13, a);
        WriteNum(k, 15, b);
        WriteNum(k, 17, c);
        return new string(k);
    }

    private static void WriteNum(char[] buffer, int offset, int value)
    {
        string s = value.ToString("D2");
        buffer[offset] = s[0];
        buffer[offset + 1] = s[1];
    }

    private static NearestRankReference Ref(string reservedWord, string ratingKey) =>
        new() { ReservedWord = reservedWord, RatingKey = ratingKey };

    [Fact]
    public void 該当データがなければNOGOODで元の選定を保持する()
    {
        var query = Ref("TS", TsKey(1, 0, 0));
        var candidates = new List<NearestRankReference>
        {
            Ref("MC", TsKey(1, 0, 0)),
            Ref("MG", TsKey(2, 0, 0)),
        };

        NearestRankSearchResult result = ContactGoodDataSelector.Select(query, candidates, 6, 1, 0, 0);

        Assert.Equal(NoGood, result.Status);
        Assert.Same(query, result.Selected);
    }

    [Fact]
    public void 該当が1件ならその該当データを返す()
    {
        var query = Ref("TS", TsKey(0, 0, 0));
        var only = Ref("TS", TsKey(2, 2, 0));
        var candidates = new List<NearestRankReference>
        {
            Ref("MC", TsKey(1, 0, 0)),
            only,
        };

        NearestRankSearchResult result = ContactGoodDataSelector.Select(query, candidates, 6, 1, 1, 0);

        Assert.Equal(Good, result.Status);
        Assert.Same(only, result.Selected);
    }

    [Fact]
    public void 該当が複数なら接点数最適な該当データを返す()
    {
        var query = Ref("TS", TsKey(0, 0, 0));
        var low = Ref("TS", TsKey(1, 1, 0));
        var best = Ref("TS", TsKey(3, 3, 0));
        var candidates = new List<NearestRankReference>
        {
            low,
            best,
            Ref("TS", TsKey(9, 9, 0, 'X')), // 定格値プレフィックス違いで前方一致から除外
        };

        NearestRankSearchResult result = ContactGoodDataSelector.Select(query, candidates, 6, 2, 2, 0);

        Assert.Equal(Good, result.Status);
        Assert.Same(best, result.Selected);
    }

    [Fact]
    public void 定格値プレフィックスが一致しない候補は除外する()
    {
        var query = Ref("TS", TsKey(0, 0, 0));
        var match = Ref("TS", TsKey(2, 2, 0));
        var candidates = new List<NearestRankReference>
        {
            Ref("TS", TsKey(3, 3, 0, 'Y')), // 先頭桁違いで除外
            match,
        };

        NearestRankSearchResult result = ContactGoodDataSelector.Select(query, candidates, 6, 1, 1, 0);

        Assert.Equal(Good, result.Status);
        Assert.Same(match, result.Selected);
    }

    [Fact]
    public void 予約語が異なる候補は前方一致しない()
    {
        var query = Ref("TS", TsKey(0, 0, 0));
        var candidates = new List<NearestRankReference>
        {
            Ref("KPRY", TsKey(1, 1, 0)),
        };

        NearestRankSearchResult result = ContactGoodDataSelector.Select(query, candidates, 6, 1, 1, 0);

        Assert.Equal(NoGood, result.Status);
        Assert.Same(query, result.Selected);
    }
}
