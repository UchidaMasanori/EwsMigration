using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// WL の回路電圧変更(PropChangeWlKpav)の移植検証。
///
/// 【C原典】PropChangeWlKpav(Fysk00.c:7714)。F の追番を親に持つ WL の回路電圧を、
/// 河村製(K)なら "005"、それ以外は F の回路電圧に変更する。
/// </summary>
public sealed class WlCircuitVoltageAdjusterTests
{
    private static MainCircuitResult Fuse(string datano, string voltage) =>
        new()
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ReservedWord = "F",
                CircuitVoltage = [voltage, "000", "000"],
            },
        };

    private static MainCircuitResult Wl(string oyatno, string voltage) =>
        new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "WL",
                ParentSequenceNumber = oyatno,
                CircuitVoltage = [voltage, "000", "000"],
            },
        };

    [Fact]
    public void 河村製WLユニットは回路電圧を005にする()
    {
        MainCircuitResult fuse = Fuse("005", "210");
        MainCircuitResult wl = Wl("005", "105");

        WlCircuitVoltageAdjuster.Adjust("K  ", fuse, [fuse, wl]);

        Assert.Equal("005", wl.Data.CircuitVoltage[0]);
    }

    [Fact]
    public void 河村製以外はFの回路電圧を複写する()
    {
        MainCircuitResult fuse = Fuse("005", "210");
        MainCircuitResult wl = Wl("005", "105");

        WlCircuitVoltageAdjuster.Adjust("M  ", fuse, [fuse, wl]);

        Assert.Equal("210", wl.Data.CircuitVoltage[0]);
    }

    [Fact]
    public void Fの子でないWLは変更しない()
    {
        MainCircuitResult fuse = Fuse("005", "210");
        MainCircuitResult wl = Wl("009", "105");   // 親が F でない

        WlCircuitVoltageAdjuster.Adjust("K  ", fuse, [fuse, wl]);

        Assert.Equal("105", wl.Data.CircuitVoltage[0]);
    }

    [Fact]
    public void 最初に一致したWLのみ変更する()
    {
        MainCircuitResult fuse = Fuse("005", "210");
        MainCircuitResult wl1 = Wl("005", "105");
        MainCircuitResult wl2 = Wl("005", "105");

        WlCircuitVoltageAdjuster.Adjust("K  ", fuse, [fuse, wl1, wl2]);

        Assert.Equal("005", wl1.Data.CircuitVoltage[0]);
        Assert.Equal("105", wl2.Data.CircuitVoltage[0]);   // 2 件目は break で未変更
    }
}
