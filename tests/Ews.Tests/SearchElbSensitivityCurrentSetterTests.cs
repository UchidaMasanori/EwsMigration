using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="SearchElbSensitivityCurrentSetter"/>(=Fysk0e_SetELBkando2)の移植テスト。
/// </summary>
public sealed class SearchElbSensitivityCurrentSetterTests
{
    private static string[] MakeType(string secondElement)
        => new[] { "       ", secondElement };

    [Fact]
    public void 動力回路60AF以下EV形は15を設定する()
    {
        var ep = new NumericElectricalParameters();
        SearchElbSensitivityCurrentSetter.Apply(60.0, '3', MakeType("EV "), ep);
        Assert.Equal(15.0, ep.Ma[0]);
    }

    [Fact]
    public void 動力回路60AF以下非EVは30を設定する()
    {
        var ep = new NumericElectricalParameters();
        SearchElbSensitivityCurrentSetter.Apply(60.0, '3', MakeType("   "), ep);
        Assert.Equal(30.0, ep.Ma[0]);
    }

    [Fact]
    public void 動力回路100AF以下は100を設定する()
    {
        var ep = new NumericElectricalParameters();
        SearchElbSensitivityCurrentSetter.Apply(100.0, '3', MakeType("EV "), ep);
        Assert.Equal(100.0, ep.Ma[0]);
    }

    [Fact]
    public void 動力回路100AF超過は200を設定する()
    {
        var ep = new NumericElectricalParameters();
        SearchElbSensitivityCurrentSetter.Apply(150.0, '3', MakeType("   "), ep);
        Assert.Equal(200.0, ep.Ma[0]);
    }

    [Fact]
    public void 電灯回路100AF以下EV形は15を設定する()
    {
        var ep = new NumericElectricalParameters();
        SearchElbSensitivityCurrentSetter.Apply(100.0, '1', MakeType("EV "), ep);
        Assert.Equal(15.0, ep.Ma[0]);
    }

    [Fact]
    public void 電灯回路100AF以下非EVは30を設定する()
    {
        var ep = new NumericElectricalParameters();
        SearchElbSensitivityCurrentSetter.Apply(100.0, '1', MakeType("   "), ep);
        Assert.Equal(30.0, ep.Ma[0]);
    }

    [Fact]
    public void 電灯回路100AF超過は200を設定する()
    {
        var ep = new NumericElectricalParameters();
        SearchElbSensitivityCurrentSetter.Apply(200.0, '1', MakeType("EV "), ep);
        Assert.Equal(200.0, ep.Ma[0]);
    }

    [Fact]
    public void 親相数がその他なら感度電流を変更しない()
    {
        var ep = new NumericElectricalParameters();
        ep.Ma[0] = 99.0;
        SearchElbSensitivityCurrentSetter.Apply(50.0, '2', MakeType("EV "), ep);
        Assert.Equal(99.0, ep.Ma[0]);
    }
}
