namespace Ews.Tests;

using Ews.Analysis;
using Xunit;

/// <summary>
/// <see cref="RangeMidpointComparer"/>(=Fysk01_Choki_Cmp2)の移植テスト。
/// </summary>
public sealed class RangeMidpointComparerTests
{
    [Fact]
    public void 今回データが中点に近いなら入れ替える1を返す()
    {
        // 今回[0,10]は中点5で基準5と距離0、前回[0,20]は中点10で距離5
        Assert.Equal(1, RangeMidpointComparer.Compare(5.0, 0.0, 10.0, 0.0, 20.0));
    }

    [Fact]
    public void 前回データが中点に近いなら入れ替えない0を返す()
    {
        Assert.Equal(0, RangeMidpointComparer.Compare(5.0, 0.0, 20.0, 0.0, 10.0));
    }

    [Fact]
    public void 中点距離が同じなら入れ替えない0を返す()
    {
        Assert.Equal(0, RangeMidpointComparer.Compare(5.0, 0.0, 10.0, 0.0, 10.0));
    }

    [Fact]
    public void 今回範囲の下限が上限以上ならシステムエラーを返す()
    {
        Assert.Equal(-1, RangeMidpointComparer.Compare(5.0, 10.0, 10.0, 0.0, 20.0));
    }

    [Fact]
    public void 前回範囲の下限が上限以上ならシステムエラーを返す()
    {
        Assert.Equal(-1, RangeMidpointComparer.Compare(5.0, 0.0, 10.0, 20.0, 20.0));
    }

    [Fact]
    public void 基準値が今回範囲外ならシステムエラーを返す()
    {
        Assert.Equal(-1, RangeMidpointComparer.Compare(15.0, 0.0, 10.0, 0.0, 20.0));
    }

    [Fact]
    public void 基準値が前回範囲外ならシステムエラーを返す()
    {
        Assert.Equal(-1, RangeMidpointComparer.Compare(5.0, 0.0, 10.0, 6.0, 20.0));
    }

    [Fact]
    public void 基準値が範囲境界上でもエラーにならない()
    {
        // 基準0は今回[0,10]・前回[0,100]の下限＝範囲内。今回中点距離5<前回中点距離50で1
        Assert.Equal(1, RangeMidpointComparer.Compare(0.0, 0.0, 10.0, 0.0, 100.0));
    }
}
