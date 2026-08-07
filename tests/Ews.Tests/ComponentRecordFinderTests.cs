namespace Ews.Tests;

using Ews.Analysis;
using Xunit;

/// <summary>
/// <see cref="ComponentRecordFinder"/>(=Fysk01_Copy_Rec_Get)の移植テスト。
/// </summary>
public sealed class ComponentRecordFinderTests
{
    private static string Key(string s) => s;

    [Fact]
    public void キー先頭10桁が一致する最初の位置を返す()
    {
        string[] recs = ["AAAAAAAAAA", "BBBBBBBBBB", "CCCCCCCCCC"];

        int r = ComponentRecordFinder.FindByKey(recs, recs.Length, "BBBBBBBBBB", Key);

        Assert.Equal(1, r);
    }

    [Fact]
    public void 該当なしは_マイナス1を返す()
    {
        string[] recs = ["AAAAAAAAAA", "BBBBBBBBBB"];

        int r = ComponentRecordFinder.FindByKey(recs, recs.Length, "ZZZZZZZZZZ", Key);

        Assert.Equal(-1, r);
    }

    [Fact]
    public void 複数一致でも最初の位置を返す()
    {
        string[] recs = ["AAAAAAAAAA", "AAAAAAAAAA", "AAAAAAAAAA"];

        int r = ComponentRecordFinder.FindByKey(recs, recs.Length, "AAAAAAAAAA", Key);

        Assert.Equal(0, r);
    }

    [Fact]
    public void 先頭10桁のみ比較し11文字目以降の違いは無視する()
    {
        string[] recs = ["AAAAAAAAAA-1", "AAAAAAAAAA-2"];

        int r = ComponentRecordFinder.FindByKey(recs, recs.Length, "AAAAAAAAAA-9", Key);

        Assert.Equal(0, r);
    }

    [Fact]
    public void count件のみ走査し範囲外の一致は無視する()
    {
        string[] recs = ["AAAAAAAAAA", "BBBBBBBBBB", "CCCCCCCCCC"];

        int r = ComponentRecordFinder.FindByKey(recs, 2, "CCCCCCCCCC", Key);

        Assert.Equal(-1, r);
    }

    [Fact]
    public void 件数0は_マイナス1を返す()
    {
        string[] recs = ["AAAAAAAAAA"];

        int r = ComponentRecordFinder.FindByKey(recs, 0, "AAAAAAAAAA", Key);

        Assert.Equal(-1, r);
    }
}
