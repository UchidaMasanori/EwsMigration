using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ReservedNumberComparer"/>(【C原典】Fyss1k.c の sortcmp3)の単体テスト。
/// </summary>
public sealed class ReservedNumberComparerTests
{
    private static ReservedNumberEntry E(string key, short dno = 0)
        => new() { ReservedKey = key, DataNumber = dno };

    [Fact]
    public void 予約語昇順で比較する()
    {
        Assert.Equal(-1, ReservedNumberComparer.Instance.Compare(E("MC1"), E("MC2")));
        Assert.Equal(1, ReservedNumberComparer.Instance.Compare(E("MC2"), E("MC1")));
    }

    [Fact]
    public void 接頭辞なら短い方が先()
    {
        // memcmp 16バイト: "MC\0..." < "MC1..."。'\0' < '1'。
        Assert.Equal(-1, ReservedNumberComparer.Instance.Compare(E("MC"), E("MC1")));
        Assert.Equal(1, ReservedNumberComparer.Instance.Compare(E("MC1"), E("MC")));
    }

    [Fact]
    public void 予約語が同じなら0_dnoは無関係()
    {
        Assert.Equal(0, ReservedNumberComparer.Instance.Compare(E("RRY1", dno: 5), E("RRY1", dno: 9)));
    }

    [Fact]
    public void 空キー同士は0()
    {
        Assert.Equal(0, ReservedNumberComparer.Instance.Compare(E(""), E("")));
    }

    [Fact]
    public void リストソートで予約語昇順に整列しdnoは保持する()
    {
        var list = new List<ReservedNumberEntry>
        {
            E("RRY2", dno: 10),
            E("MC1", dno: 20),
            E("RRY1", dno: 30),
            E("MC1", dno: 40),
        };

        list.Sort(ReservedNumberComparer.Instance);

        Assert.Equal(new[] { "MC1", "MC1", "RRY1", "RRY2" }, list.ConvertAll(e => e.ReservedKey).ToArray());
        // 予約語が同じ MC1 群の順序は安定性に依存しないが、dno がキーとして保持されていること。
        Assert.Contains(list, e => e.ReservedKey == "RRY1" && e.DataNumber == 30);
        Assert.Contains(list, e => e.ReservedKey == "RRY2" && e.DataNumber == 10);
    }
}
