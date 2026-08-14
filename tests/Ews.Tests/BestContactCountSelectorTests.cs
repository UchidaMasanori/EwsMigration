using Ews.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="BestContactCountSelector"/>(【C原典】Fysc29_Best_Cont_Count)のテスト。
/// 接点数は定格値キー(kteichi[50])の予約語別レイアウト位置に配置して検証する。
/// </summary>
public sealed class BestContactCountSelectorTests
{
    /// <summary>MC 用の定格値キー(ac@13, bc@14 の 1 桁)を生成する。</summary>
    private static string McKey(int a, int b)
    {
        char[] k = new string(' ', 50).ToCharArray();
        k[13] = (char)('0' + a);
        k[14] = (char)('0' + b);
        return new string(k);
    }

    /// <summary>TS 用の定格値キー(ac@13, bc@15, cc@17 の 2 桁)を生成する。</summary>
    private static string TsKey(int a, int b, int c)
    {
        char[] k = new string(' ', 50).ToCharArray();
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
    public void 対象外予約語はマイナス1を返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("XYZ", McKey(1, 0)),
            Ref("XYZ", McKey(2, 0)),
        };

        Assert.Equal(-1, BestContactCountSelector.Select(list, 1, 0, 0));
    }

    [Fact]
    public void 件数1件のときは常に0を返す()
    {
        var list = new List<NearestRankReference> { Ref("MC", McKey(0, 0)) };

        Assert.Equal(0, BestContactCountSelector.Select(list, 3, 3, 3));
    }

    [Fact]
    public void TS使用1接点で1Cレコードがあれば優先して返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("TS", TsKey(1, 0, 0)),
            Ref("TS", TsKey(0, 0, 1)),
        };

        Assert.Equal(1, BestContactCountSelector.Select(list, 1, 0, 0));
    }

    [Fact]
    public void ABCすべて使用時はCグループの合計以上を返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("TS", TsKey(0, 0, 3)),
            Ref("TS", TsKey(0, 0, 0)),
        };

        Assert.Equal(0, BestContactCountSelector.Select(list, 1, 1, 1));
    }

    [Fact]
    public void A接点のみ使用時は同数のA接点を優先して返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("TS", TsKey(3, 0, 0)),
            Ref("TS", TsKey(2, 0, 0)),
        };

        Assert.Equal(1, BestContactCountSelector.Select(list, 2, 0, 0));
    }

    [Fact]
    public void A接点のみ使用時に同数がなければ以上のものを返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("TS", TsKey(4, 0, 0)),
            Ref("TS", TsKey(3, 0, 0)),
        };

        Assert.Equal(0, BestContactCountSelector.Select(list, 2, 0, 0));
    }

    [Fact]
    public void B接点のみ使用時はB接点以上のものを返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("TS", TsKey(0, 2, 0)),
            Ref("TS", TsKey(0, 1, 0)),
        };

        Assert.Equal(0, BestContactCountSelector.Select(list, 0, 1, 0));
    }

    [Fact]
    public void AB接点使用時はAB両方を満たすものを返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("TS", TsKey(2, 2, 0)),
            Ref("TS", TsKey(1, 1, 0)),
        };

        Assert.Equal(0, BestContactCountSelector.Select(list, 1, 1, 0));
    }

    [Fact]
    public void 使用接点なしでMCMG以外は1件目を返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("CR", TsKey(0, 0, 0)),
            Ref("CR", TsKey(1, 0, 0)),
        };

        // CR は no=4(>1) のため使用接点なしなら常に 0。
        Assert.Equal(0, BestContactCountSelector.Select(list, 0, 0, 0));
    }

    [Fact]
    public void 使用接点なしのMCで接点なしレコードがあれば1件目を返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("MC", McKey(1, 0)),
            Ref("MC", McKey(0, 0)),
        };

        Assert.Equal(0, BestContactCountSelector.Select(list, 0, 0, 0));
    }

    [Fact]
    public void 使用接点なしのMCで接点なしがなければA接点保有の最初を返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("MC", McKey(0, 1)),
            Ref("MC", McKey(2, 0)),
        };

        Assert.Equal(1, BestContactCountSelector.Select(list, 0, 0, 0));
    }

    [Fact]
    public void 該当グループが空ならマイナス1を返す()
    {
        var list = new List<NearestRankReference>
        {
            Ref("TS", TsKey(0, 2, 0)),
            Ref("TS", TsKey(0, 1, 0)),
        };

        // A 接点のみ使用だが候補は B 接点のみ(flgA/flgT/flgC いずれも空)→ -1。
        Assert.Equal(-1, BestContactCountSelector.Select(list, 1, 0, 0));
    }
}
