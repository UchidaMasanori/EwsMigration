namespace Ews.Tests;

using Ews.Domain.Analysis;
using Xunit;

/// <summary>
/// <see cref="ElectricalParameters.ValueEquals"/>(=eparmg の memcmp 相当)の移植テスト。
/// </summary>
public sealed class ElectricalParametersValueEqualsTests
{
    [Fact]
    public void 初期値同士は一致する()
    {
        var a = new ElectricalParameters();
        var b = new ElectricalParameters();

        Assert.True(a.ValueEquals(b));
    }

    [Fact]
    public void CopyFrom後は一致する()
    {
        var a = new ElectricalParameters { Af = "000001000", Qty = '1' };
        a.Ma[2] = "0012";
        var b = new ElectricalParameters();
        b.CopyFrom(a);

        Assert.True(a.ValueEquals(b));
    }

    [Fact]
    public void スカラフィールドが異なれば不一致()
    {
        var a = new ElectricalParameters();
        var b = new ElectricalParameters { At = "000009999" };

        Assert.False(a.ValueEquals(b));
    }

    [Fact]
    public void 配列フィールドが異なれば不一致()
    {
        var a = new ElectricalParameters();
        var b = new ElectricalParameters();
        b.V1[1] = "00000100";

        Assert.False(a.ValueEquals(b));
    }

    [Fact]
    public void char区分フィールドが異なれば不一致()
    {
        var a = new ElectricalParameters();
        var b = new ElectricalParameters { V2Kbn = 'A' };

        Assert.False(a.ValueEquals(b));
    }
}
