namespace Ews.Tests;

using Ews.Analysis;
using Xunit;

/// <summary>
/// <see cref="WattToAmpereConverter"/>(=Fysk01_Change_W_AT)‚ÌˆÚAƒeƒXƒgB
/// </summary>
public sealed class WattToAmpereConverterTests
{
    private const int Precision = 9;

    [Fact]
    public void O‘Š220VˆÈ‰º1000WˆÈã‚Í0_948æ‚Ì4_4ŒW”()
    {
        double expected = Math.Pow(2000.0 / 1000.0, 0.948) * 4.4;
        Assert.Equal(expected, WattToAmpereConverter.Convert(2000.0, 3, 220.0), Precision);
    }

    [Fact]
    public void O‘Š220VˆÈ‰º1000W–¢–‚Í0_945æ‚Ì‰Â•ÏŒW”()
    {
        double expected = Math.Pow(750.0 / 1000.0, 0.945) * (6.0 - 1.6 * 750.0 / 1000.0);
        Assert.Equal(expected, WattToAmpereConverter.Convert(750.0, 3, 200.0), Precision);
    }

    [Fact]
    public void O‘Š220V’´1500WˆÈã‚Í0_948æ‚Ì2_26ŒW”()
    {
        double expected = Math.Pow(2000.0 / 1000.0, 0.948) * 2.26;
        Assert.Equal(expected, WattToAmpereConverter.Convert(2000.0, 3, 400.0), Precision);
    }

    [Fact]
    public void O‘Š220V’´1500W–¢–‚Í0_948æ‚Ì‰Â•ÏŒW”()
    {
        double expected = Math.Pow(1000.0 / 1000.0, 0.948) * (3.3 - 1.25 * 1000.0 / 1000.0);
        Assert.Equal(expected, WattToAmpereConverter.Convert(1000.0, 3, 400.0), Precision);
    }

    [Fact]
    public void ’P‘Š105VˆÈ‰º‚Í0_71æ‚Ì18_3ŒW”()
    {
        double expected = Math.Pow(1000.0 / 1000.0, 0.71) * 18.3;
        Assert.Equal(expected, WattToAmpereConverter.Convert(1000.0, 1, 100.0), Precision);
    }

    [Fact]
    public void ’P‘Š105V’´‚Í0_71æ‚Ì9_1ŒW”()
    {
        double expected = Math.Pow(1000.0 / 1000.0, 0.71) * 9.1;
        Assert.Equal(expected, WattToAmpereConverter.Convert(1000.0, 1, 200.0), Precision);
    }

    [Fact]
    public void ‘Š”‚ª1‚Å‚à3‚Å‚à‚È‚¢ê‡‚Í9_1ŒW”‚Ì®()
    {
        double expected = Math.Pow(1000.0 / 1000.0, 0.71) * 9.1;
        Assert.Equal(expected, WattToAmpereConverter.Convert(1000.0, 2, 100.0), Precision);
    }

    [Fact]
    public void O‘Š“dˆ³‹«ŠE220V‚¿‚å‚¤‚Ç‚Í220VˆÈ‰º‘¤()
    {
        double expected = Math.Pow(1000.0 / 1000.0, 0.948) * 4.4;
        Assert.Equal(expected, WattToAmpereConverter.Convert(1000.0, 3, 220.0), Precision);
    }

    [Fact]
    public void ’P‘Š“dˆ³‹«ŠE105V‚¿‚å‚¤‚Ç‚Í105VˆÈ‰º‘¤()
    {
        double expected = Math.Pow(1000.0 / 1000.0, 0.71) * 18.3;
        Assert.Equal(expected, WattToAmpereConverter.Convert(1000.0, 1, 105.0), Precision);
    }
}
