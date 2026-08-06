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

    // ---- GetUpstreamControlPowerData(yCŒ´“TzGetSeivdnoUp, Fyss1k.c:3392) ----

    private static MainCircuitResult MainRow(string gyo, string gyocd, string datano, char bn)
    {
        var r = new MainCircuitResult();
        r.Data.DescriptionRow = gyo;
        r.Data.LineTypeCode = gyocd;
        r.SequenceNumber = datano;
        r.Data.ElectricalParameterSlots[0].Bn = bn;
        return r;
    }

    private static ControlSpecEntry Spec(short kgyo) => new() { DescriptionRow = kgyo };

    [Fact]
    public void ’¼ãUPs‚©‚çƒf[ƒ^’Ç”Ô‚Æ”Õí—Ş‚ğæ“¾‚·‚é()
    {
        var mains = new List<MainCircuitResult>
        {
            MainRow("001", "UP ", "007", '2'),
            MainRow("002", "MC ", "008", '3'),
            MainRow("005", "MC ", "009", '4'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(3), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("007", seivdno);
        Assert.Equal('2', bn);
    }

    [Fact]
    public void ’¼ãå‰ñ˜Hs‚ª–³‚¯‚ê‚Î•‰1()
    {
        var mains = new List<MainCircuitResult>
        {
            MainRow("005", "UP ", "001", '1'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(1), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, seivdno);
        Assert.Equal('\0', bn);
    }

    [Fact]
    public void ’¼ã‚Í‚ ‚é‚ªUPs‚ª–³‚¯‚ê‚Î•‰1()
    {
        var mains = new List<MainCircuitResult>
        {
            MainRow("001", "MC ", "007", '2'),
            MainRow("002", "MG ", "008", '3'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(5), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, seivdno);
    }

    [Fact]
    public void ‹Lqs‚Æ“¯ˆê‚Ìgyo‚Í’¼ã‚Æ‚İ‚È‚³‚È‚¢()
    {
        // memcmp(kgyou, gyo, 3) > 0 ‚Ì‚İ‚ª’¼ãB“™‚µ‚¢s‚Í‘ÎÛŠOB
        var mains = new List<MainCircuitResult>
        {
            MainRow("003", "UP ", "007", '2'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(3), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
    }

    [Fact]
    public void ’¼‹ßãˆÊ‚ÌUPs‚ğ—Dæ‚·‚é()
    {
        var mains = new List<MainCircuitResult>
        {
            MainRow("001", "UP ", "100", '1'),
            MainRow("002", "UP ", "200", '5'),
            MainRow("004", "MC ", "300", '9'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(3), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("200", seivdno);
        Assert.Equal('5', bn);
    }

    [Fact]
    public void ‹Lqs‚Í3Œ…ƒ[ƒ–„‚ß‚Å”äŠr‚·‚é()
    {
        // kgyo=12 ‚Í "012" ‚É®Œ`‚³‚ê "009" ‚æ‚è‘å(”’l‡‚Æˆê’v)B
        var mains = new List<MainCircuitResult>
        {
            MainRow("009", "UP ", "050", '7'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(12), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("050", seivdno);
        Assert.Equal('7', bn);
    }

    [Fact]
    public void ãˆÊŒŸõ‚Í‹óƒe[ƒuƒ‹‚Å•‰1()
    {
        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(3), new List<MainCircuitResult>(), out string seivdno, out char bn);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, seivdno);
        Assert.Equal('\0', bn);
    }

    // ---- GetControlPowerDataFromOtherSystem(yCŒ´“TzGetSeivdnoOtherKeitou, Fyss1k.c:3051) ----

    private static readonly string[] Volt200 = ["200", "000", "000"];

    private static MainCircuitResult PRow(string kno, char kpaph, char kpawr, string[] kpav, string fpac, string datano, char bn)
    {
        var r = new MainCircuitResult();
        r.Data.LineTypeCode = "P";
        r.Data.SystemNumber = kno;
        r.Data.CircuitPhaseCount = kpaph;
        r.Data.CircuitWireType = kpawr;
        r.Data.CircuitVoltage = kpav;
        r.Data.AttachedParameter.ControlPowerNumber = fpac;
        r.SequenceNumber = datano;
        r.Data.ElectricalParameterSlots[0].Bn = bn;
        return r;
    }

    private static MainCircuitResult TargetRow(string kno, string fpac, string datano, char bn)
    {
        var r = new MainCircuitResult();
        r.Data.LineTypeCode = "MC";
        r.Data.SystemNumber = kno;
        r.Data.AttachedParameter.ControlPowerNumber = fpac;
        r.SequenceNumber = datano;
        r.Data.ElectricalParameterSlots[0].Bn = bn;
        return r;
    }

    private static ControlSpecEntry SpecKno(short kno, string gyono)
        => new() { SystemNumber = kno, LineTypeNumber = gyono };

    [Fact]
    public void •ÊŒn“‚Ìˆê’vPs‚©‚çƒf[ƒ^’Ç”Ô‚Æ”Õí—Ş‚ğæ“¾‚·‚é()
    {
        var mains = new List<MainCircuitResult>
        {
            PRow("005", '3', '3', Volt200, string.Empty, "001", '0'),
            PRow("010", '3', '3', Volt200, "XX", "050", '9'),
            TargetRow("010", "01", "077", '4'),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerDataFromOtherSystem(
            SpecKno(5, "01"), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("077", seivdno);
        Assert.Equal('4', bn);
    }

    [Fact]
    public void ˆê’v‚·‚é•ÊŒn“‚ª–³‚¯‚ê‚Î•‰1()
    {
        var mains = new List<MainCircuitResult>
        {
            PRow("005", '3', '3', Volt200, string.Empty, "001", '0'),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerDataFromOtherSystem(
            SpecKno(5, "01"), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, seivdno);
        Assert.Equal('\0', bn);
    }

    [Fact]
    public void ‰ñ˜H‘Š”ü®“dˆ³‚ª•sˆê’v‚Ì•ÊŒn“‚Í‘ÎÛŠO()
    {
        var mains = new List<MainCircuitResult>
        {
            PRow("005", '3', '3', Volt200, string.Empty, "001", '0'),
            PRow("010", '1', '2', ["100", "000", "000"], "XX", "050", '9'),
            TargetRow("010", "01", "077", '4'),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerDataFromOtherSystem(
            SpecKno(5, "01"), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
    }

    [Fact]
    public void sí”Ô†‚Æ•sˆê’v‚Ì§Œä“dŒ¹”Ô†‚Í‘ÎÛŠO()
    {
        var mains = new List<MainCircuitResult>
        {
            PRow("005", '3', '3', Volt200, string.Empty, "001", '0'),
            PRow("010", '3', '3', Volt200, "XX", "050", '9'),
            TargetRow("010", "09", "077", '4'),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerDataFromOtherSystem(
            SpecKno(5, "01"), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
    }

    [Fact]
    public void ©Œn“‚æ‚è¬‚³‚¢Œn“‚ğ—Dæ‚·‚é()
    {
        // ©Œn“=010B•ÊŒn“ 020(ã)‚Æ 005(‰º)‚ªŒó•âB‰º‘¤ 005 ‚ğ—DæB
        var mains = new List<MainCircuitResult>
        {
            PRow("010", '3', '3', Volt200, string.Empty, "001", '0'),
            PRow("020", '3', '3', Volt200, string.Empty, "050", '9'),
            TargetRow("020", "01", "820", '2'),
            PRow("005", '3', '3', Volt200, string.Empty, "060", '8'),
            TargetRow("005", "01", "805", '7'),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerDataFromOtherSystem(
            SpecKno(10, "01"), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("805", seivdno);
        Assert.Equal('7', bn);
    }

    [Fact]
    public void ¬‚³‚¢Œn“‚ª•¡”‚È‚ç©Œn“‚É‹ß‚¢•û‚ğ—Dæ‚·‚é()
    {
        // ©Œn“=010B•ÊŒn“ 003 ‚Æ 007 ‚ªŒó•â(‚¢‚¸‚ê‚à‰º)B‹ß‚¢ 007 ‚ğ—DæB
        var mains = new List<MainCircuitResult>
        {
            PRow("010", '3', '3', Volt200, string.Empty, "001", '0'),
            PRow("003", '3', '3', Volt200, string.Empty, "050", '3'),
            TargetRow("003", "01", "803", '3'),
            PRow("007", '3', '3', Volt200, string.Empty, "060", '7'),
            TargetRow("007", "01", "807", '7'),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerDataFromOtherSystem(
            SpecKno(10, "01"), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("807", seivdno);
        Assert.Equal('7', bn);
    }

    [Fact]
    public void ‘å‚«‚¢Œn“‚Ì‚İ‚È‚ç©Œn“‚É‹ß‚¢•û‚ğ—Dæ‚·‚é()
    {
        // ©Œn“=010B•ÊŒn“ 020 ‚Æ 015 ‚ªŒó•â(‚¢‚¸‚ê‚àã)B‹ß‚¢ 015 ‚ğ—DæB
        var mains = new List<MainCircuitResult>
        {
            PRow("010", '3', '3', Volt200, string.Empty, "001", '0'),
            PRow("020", '3', '3', Volt200, string.Empty, "050", '2'),
            TargetRow("020", "01", "820", '2'),
            PRow("015", '3', '3', Volt200, string.Empty, "060", '5'),
            TargetRow("015", "01", "815", '5'),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerDataFromOtherSystem(
            SpecKno(10, "01"), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("815", seivdno);
        Assert.Equal('5', bn);
    }

    // ---- GetControlPowerData(yCŒ´“TzGetSeivdno, Fyss1k.c:2933) ----

    private static MainCircuitResult Row(
        string fpac = "", string kno = "000", string datano = "000", char bn = '0',
        string gyocd = "", string yoyaku = "", string dtype1 = "")
    {
        var r = new MainCircuitResult();
        r.Data.AttachedParameter.ControlPowerNumber = fpac;
        r.Data.SystemNumber = kno;
        r.SequenceNumber = datano;
        r.Data.ElectricalParameterSlots[0].Bn = bn;
        r.Data.LineTypeCode = gyocd;
        r.Data.ReservedWord = yoyaku;
        r.Data.DataType[1] = dtype1;
        return r;
    }

    [Fact]
    public void §Œä“dŒ¹”Ô†‚ªgyono‚Æˆê’v‚·‚é1Œ‚ğæ“¾‚·‚é()
    {
        var mains = new List<MainCircuitResult>
        {
            Row(fpac: "01", kno: "005", datano: "077", bn: '4', gyocd: "MC"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerData(
            SpecKno(5, "01"), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("077", seivdno);
        Assert.Equal('4', bn);
    }

    [Fact]
    public void ˆê’v‚ª–³‚­‹~Ï‚à–³‚¯‚ê‚Î•‰1()
    {
        var mains = new List<MainCircuitResult>
        {
            Row(fpac: "01", kno: "005", datano: "077", bn: '4', gyocd: "MC"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerData(
            SpecKno(5, "09"), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
    }

    [Fact]
    public void ˆê’v‚ª•¡”‚È‚ç•‰1()
    {
        var mains = new List<MainCircuitResult>
        {
            Row(fpac: "01", kno: "005", datano: "A01", bn: '1', gyocd: "MC"),
            Row(fpac: "01", kno: "006", datano: "B02", bn: '2', gyocd: "MC"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerData(
            SpecKno(5, "01"), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
    }

    [Fact]
    public void MPs‚Ì“¯ˆêŒn“‚Í§Œä“dŒ¹•s—v‚ÅOK()
    {
        var mains = new List<MainCircuitResult>
        {
            Row(fpac: "01", kno: "005", datano: "077", bn: '4', gyocd: "MC"),
            Row(kno: "005", datano: "300", bn: '2', gyocd: "MP", yoyaku: "MPX"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerData(
            SpecKno(5, "09"), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("300", seivdno);
        Assert.Equal('2', bn);
    }

    [Fact]
    public void —\–ñŒê‚ªMP’P“Æ‚ÌMPs‚Í“Á—á‘ÎÛŠO‚Å•‰1()
    {
        var mains = new List<MainCircuitResult>
        {
            Row(kno: "005", datano: "300", bn: '2', gyocd: "MP", yoyaku: "MP"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerData(
            SpecKno(5, "09"), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
    }

    [Fact]
    public void RRY‚ª‘S‚Ä6AƒŠƒŒ[‚È‚ç999‚Å§Œä“dŒ¹•s—v()
    {
        var mains = new List<MainCircuitResult>
        {
            Row(kno: "005", datano: "300", gyocd: "MC", yoyaku: "RRY", dtype1: "6A4K"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerData(
            SpecKno(5, "09"), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("999", seivdno);
    }

    [Fact]
    public void RRY‚É”ñ6A‚ª¬İ‚·‚ê‚Î“Á—á‘ÎÛŠO‚Å•‰1()
    {
        var mains = new List<MainCircuitResult>
        {
            Row(kno: "005", datano: "300", gyocd: "MC", yoyaku: "RRY", dtype1: "6A4K"),
            Row(kno: "005", datano: "301", gyocd: "MC", yoyaku: "RRY", dtype1: "STD"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerData(
            SpecKno(5, "09"), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
    }

    [Fact]
    public void fpac‚ª00•¡”‚©‚Â©gyono‚ª00‚Í“¯ˆêŒn“‚ÉŒÀ’è‚·‚é()
    {
        // •ÊŒn“(kno=010)‚Ì fpac="00" s‚ÍƒT[ƒ`‘ÎÛŠOB“¯ˆêŒn“(kno=005)‚Ì‚İˆê’vB
        var mains = new List<MainCircuitResult>
        {
            Row(fpac: "00", kno: "005", datano: "055", bn: '5', gyocd: "MC"),
            Row(fpac: "00", kno: "010", datano: "099", bn: '9', gyocd: "MC"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerData(
            SpecKno(5, "00"), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("055", seivdno);
        Assert.Equal('5', bn);
    }
}
