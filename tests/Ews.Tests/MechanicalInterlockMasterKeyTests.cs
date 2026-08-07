namespace Ews.Tests;

using Ews.Domain.Masters;
using Xunit;

/// <summary>
/// <see cref="MechanicalInterlockMasterKey"/>(=Fysk01_Kiki_Read_MI のキー生成部)の移植テスト。
/// </summary>
public sealed class MechanicalInterlockMasterKeyTests
{
    [Fact]
    public void 予約語とメーカーコードは固定値()
    {
        Assert.Equal("PT", MechanicalInterlockMasterKey.ReservedWord);
        Assert.Equal("M", MechanicalInterlockMasterKey.MakerCode);
    }

    [Fact]
    public void 容量250AF以下はMI05SV3()
    {
        Assert.Equal("MI-05SV3", MechanicalInterlockMasterKey.RatingKeyFor(100.0));
    }

    [Fact]
    public void 境界値250AFちょうどはMI05SV3()
    {
        Assert.Equal("MI-05SV3", MechanicalInterlockMasterKey.RatingKeyFor(250.0));
    }

    [Fact]
    public void 容量250AF超はMI4SW3()
    {
        Assert.Equal("MI-4SW3", MechanicalInterlockMasterKey.RatingKeyFor(250.1));
    }

    [Fact]
    public void 容量が大きい場合もMI4SW3()
    {
        Assert.Equal("MI-4SW3", MechanicalInterlockMasterKey.RatingKeyFor(400.0));
    }

    [Fact]
    public void しきい値定数は250()
    {
        Assert.Equal(250.0, MechanicalInterlockMasterKey.CapacityThresholdAf);
    }
}
