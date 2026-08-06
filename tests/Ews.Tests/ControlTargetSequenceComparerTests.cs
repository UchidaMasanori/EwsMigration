using System.Collections.Generic;
using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlTargetSequenceComparer"/>(【C原典】Fyss1k.c の sgkkcmp)の単体テスト。
/// </summary>
public sealed class ControlTargetSequenceComparerTests
{
    [Fact]
    public void 小さい方が前()
    {
        Assert.True(ControlTargetSequenceComparer.Instance.Compare(1, 3) < 0);
    }

    [Fact]
    public void 大きい方が後()
    {
        Assert.True(ControlTargetSequenceComparer.Instance.Compare(5, 2) > 0);
    }

    [Fact]
    public void 等しければ0()
    {
        Assert.Equal(0, ControlTargetSequenceComparer.Instance.Compare(7, 7));
    }

    [Fact]
    public void 差をそのまま返す()
    {
        // 【C原典】*dat1 - *dat2。符号のみ有意だが差そのものを返す。
        Assert.Equal(4, ControlTargetSequenceComparer.Instance.Compare(10, 6));
        Assert.Equal(-4, ControlTargetSequenceComparer.Instance.Compare(6, 10));
    }

    [Fact]
    public void リストソートで昇順に整列する()
    {
        var list = new List<short> { 30, 5, 12, 5, 1 };
        list.Sort(ControlTargetSequenceComparer.Instance);
        Assert.Equal(new short[] { 1, 5, 5, 12, 30 }, list.ToArray());
    }

    [Fact]
    public void 負値も昇順に整列する()
    {
        var list = new List<short> { 3, -2, 0, -10 };
        list.Sort(ControlTargetSequenceComparer.Instance);
        Assert.Equal(new short[] { -10, -2, 0, 3 }, list.ToArray());
    }
}
