using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="InverterErrorParameterSetter"/>(=PropSetInvErrEpstr)‚ÌˆÚAƒeƒXƒgB
/// </summary>
public sealed class InverterErrorParameterSetterTests
{
    private static NumericElectricalParameters[] Parameters() =>
    [
        new NumericElectricalParameters(),
        new NumericElectricalParameters(),
        new NumericElectricalParameters(),
    ];

    [Fact]
    public void İ’èkw‚ğWŠ·Z‚µæ“ª3—v‘f‚Ö‘‚«‚Ş()
    {
        NumericElectricalParameters[] sep = Parameters();
        InverterErrorParameterSetter.SetWattFromKw(sep, 3.7);
        Assert.Equal(3700.0, sep[0].W1);
        Assert.Equal(3700.0, sep[1].W1);
        Assert.Equal(3700.0, sep[2].W1);
    }

    [Fact]
    public void kwƒ[ƒ‚ÍW’l‚àƒ[ƒ‚É‚È‚é()
    {
        NumericElectricalParameters[] sep = Parameters();
        InverterErrorParameterSetter.SetWattFromKw(sep, 0.0);
        Assert.Equal(0.0, sep[0].W1);
        Assert.Equal(0.0, sep[1].W1);
        Assert.Equal(0.0, sep[2].W1);
    }

    [Fact]
    public void æ“ª3—v‘f‚Ì‚İİ’è‚µ4—v‘f–ÚˆÈ~‚Í•ÏX‚µ‚È‚¢()
    {
        NumericElectricalParameters[] sep =
        [
            new NumericElectricalParameters(),
            new NumericElectricalParameters(),
            new NumericElectricalParameters(),
            new NumericElectricalParameters { W1 = 99.0 },
        ];
        InverterErrorParameterSetter.SetWattFromKw(sep, 5.5);
        Assert.Equal(99.0, sep[3].W1);
    }
}
