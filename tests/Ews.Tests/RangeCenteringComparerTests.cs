namespace Ews.Tests;

using Ews.Analysis;
using Xunit;

/// <summary>
/// <see cref="RangeCenteringComparer"/>(=Fysk01_Choki_Cmp1)の移植テスト。
/// </summary>
public sealed class RangeCenteringComparerTests
{
    [Fact]
    public void 今回データが中央寄りなら入れ替える1を返す()
    {
        // 今回[0,10]は基準5で完全中央(偏り0)、前回[0,20]は偏り0.5
        Assert.Equal(1, RangeCenteringComparer.Compare(5.0, 0.0, 10.0, 0.0, 20.0));
    }

    [Fact]
    public void 前回データが中央寄りなら入れ替えない0を返す()
    {
        Assert.Equal(0, RangeCenteringComparer.Compare(5.0, 0.0, 20.0, 0.0, 10.0));
    }

    [Fact]
    public void 偏りが同じなら入れ替えない0を返す()
    {
        Assert.Equal(0, RangeCenteringComparer.Compare(5.0, 0.0, 10.0, 0.0, 10.0));
    }

    [Fact]
    public void 今回範囲の下限が上限以上ならシステムエラーを返す()
    {
        Assert.Equal(-1, RangeCenteringComparer.Compare(5.0, 10.0, 10.0, 0.0, 20.0));
    }

    [Fact]
    public void 前回範囲の下限が上限以上ならシステムエラーを返す()
    {
        Assert.Equal(-1, RangeCenteringComparer.Compare(5.0, 0.0, 10.0, 20.0, 20.0));
    }

    [Fact]
    public void 基準値が今回範囲外ならシステムエラーを返す()
    {
        Assert.Equal(-1, RangeCenteringComparer.Compare(15.0, 0.0, 10.0, 0.0, 20.0));
    }

    [Fact]
    public void 基準値が前回範囲外ならシステムエラーを返す()
    {
        Assert.Equal(-1, RangeCenteringComparer.Compare(5.0, 0.0, 10.0, 6.0, 20.0));
    }

    [Fact]
    public void 基準値が範囲境界上でもエラーにならない()
    {
        // 基準0は今回[0,10]・前回[0,20]の下限＝範囲内。両者とも偏り1で入れ替えない0
        Assert.Equal(0, RangeCenteringComparer.Compare(0.0, 0.0, 10.0, 0.0, 20.0));
    }
}
