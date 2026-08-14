using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="MitsubishiFrameCurrentSetter"/>(=PropSetAfForMitsubishi)の移植テスト。
/// </summary>
public sealed class MitsubishiFrameCurrentSetterTests
{
    private static NumericElectricalParameters[] Parameters(double at, double af = 0.0) =>
    [
        new NumericElectricalParameters { At = at, Af = af },
        new NumericElectricalParameters(),
    ];

    [Fact]
    public void 三菱MCBでAF未入力かつAT範囲内ならAFを50に補完する()
    {
        NumericElectricalParameters[] sep = Parameters(at: 30.0);
        MitsubishiFrameCurrentSetter.Apply("MCB ", "M  ", 1, sep);
        Assert.Equal(50.0, sep[1].Af);
    }

    [Fact]
    public void 三菱以外のメーカーは何もしない()
    {
        NumericElectricalParameters[] sep = Parameters(at: 30.0);
        MitsubishiFrameCurrentSetter.Apply("MCB ", "F  ", 1, sep);
        Assert.Equal(0.0, sep[1].Af);
    }

    [Fact]
    public void MCB_ELB以外の予約語は何もしない()
    {
        NumericElectricalParameters[] sep = Parameters(at: 30.0);
        MitsubishiFrameCurrentSetter.Apply("THR ", "M  ", 1, sep);
        Assert.Equal(0.0, sep[1].Af);
    }

    [Fact]
    public void フレーム容量が既に入力済みなら補完しない()
    {
        NumericElectricalParameters[] sep = Parameters(at: 30.0, af: 100.0);
        MitsubishiFrameCurrentSetter.Apply("MCB ", "MN ", 1, sep);
        Assert.Equal(0.0, sep[1].Af);
    }

    [Fact]
    public void AT下限5以下は補完しない()
    {
        NumericElectricalParameters[] sep = Parameters(at: 5.0);
        MitsubishiFrameCurrentSetter.Apply("ELB ", "MKY", 1, sep);
        Assert.Equal(0.0, sep[1].Af);
    }

    [Fact]
    public void AT上限50を超えると補完しない()
    {
        NumericElectricalParameters[] sep = Parameters(at: 60.0);
        MitsubishiFrameCurrentSetter.Apply("ELB ", "M  ", 1, sep);
        Assert.Equal(0.0, sep[1].Af);
    }

    [Fact]
    public void AT上限ちょうど50は補完する()
    {
        NumericElectricalParameters[] sep = Parameters(at: 50.0);
        MitsubishiFrameCurrentSetter.Apply("ELB ", "M  ", 1, sep);
        Assert.Equal(50.0, sep[1].Af);
    }
}
