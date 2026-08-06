using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlPowerSystemLocator"/>(yCŒ´“TzFyss1k.c ‚Ì getCtlDenKno)‚Ì’P‘ÌƒeƒXƒgB
/// </summary>
public sealed class ControlPowerSystemLocatorTests
{
    private static MainCircuitResult Main(string fpac, string kno)
    {
        var r = new MainCircuitResult();
        r.Data.AttachedParameter.ControlPowerNumber = fpac;
        r.Data.SystemNumber = kno;
        return r;
    }

    [Fact]
    public void §Œä“dŒ¹”Ô†ˆê’v‚ÅŒn“”Ô†‚ğ•Ô‚·()
    {
        var mains = new List<MainCircuitResult>
        {
            Main("01", "005"),
            Main("02", "010"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("02", mains, out string kno);

        Assert.Equal(0, ret);
        Assert.Equal("010", kno);
    }

    [Fact]
    public void æ“ªˆê’v‚ğ—Dæ‚·‚é()
    {
        var mains = new List<MainCircuitResult>
        {
            Main("01", "005"),
            Main("01", "006"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("01", mains, out string kno);

        Assert.Equal(0, ret);
        Assert.Equal("005", kno);
    }

    [Fact]
    public void ŠY“–‚È‚µ‚Í•‰1‚ÅŒn“”Ô†‚Í‹ó()
    {
        var mains = new List<MainCircuitResult>
        {
            Main("01", "005"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("09", mains, out string kno);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, kno);
    }

    [Fact]
    public void §Œä“dŒ¹”Ô†‚Í2ƒoƒCƒg‚Å”äŠr‚·‚é()
    {
        // fpac ‚Í 2 ƒoƒCƒgŒÅ’èB3 •¶š–ÚˆÈ~‚Í”äŠr‘ÎÛŠOB
        var mains = new List<MainCircuitResult>
        {
            Main("01", "005"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("012", mains, out string kno);

        Assert.Equal(0, ret);
        Assert.Equal("005", kno);
    }

    [Fact]
    public void ‹ó‚Ì§Œä“dŒ¹”Ô†‚Í‹ó”’2•¶š‚Æˆê’v‚·‚é()
    {
        var mains = new List<MainCircuitResult>
        {
            Main(string.Empty, "005"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("  ", mains, out string kno);

        Assert.Equal(0, ret);
        Assert.Equal("005", kno);
    }

    [Fact]
    public void ‹óƒe[ƒuƒ‹‚Í•‰1()
    {
        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("01", new List<MainCircuitResult>(), out string kno);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, kno);
    }
}
