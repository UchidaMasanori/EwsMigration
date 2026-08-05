using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CoordinateOptimizer"/>(C Œ´“T OptimZahyo / HeiretuCheck / EditZahyo)‚Ì’P‘ÌƒeƒXƒgB
/// </summary>
public sealed class CoordinateOptimizerTests
{
    private static MainCircuitResult Rec(
        string datano = "000",
        string nyuseno = "001",
        string kaisono = "000",
        string chokuno = "001",
        string heino = "001",
        string joheino = "000",
        string oyatno = "000",
        string goyano = "000",
        string yoyaku = "")
    {
        return new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                IncomingNumber = nyuseno,
                HierarchyNumber = kaisono,
                SeriesNumber = chokuno,
                ParallelNumber = heino,
                UpperParallelNumber = joheino,
                ParentSequenceNumber = oyatno,
                GroupParentSequenceNumber = goyano,
                ReservedWord = yoyaku,
            },
        };
    }

    [Fact]
    public void •À—ñŠÖŒW‚ª‚È‚¯‚ê‚ÎŠK‘w‚ğ1‚Âã‚°À•W‚ğÅ“K‰»‚·‚é()
    {
        // P(000) - 002ŠK‘w‚Ì’¼—ñ001—v‘f ‚ª•À—ñ‚ğ‚½‚È‚¢ ¨ 002‚ğ001‚Öã‚°’¼—ñ‰»
        var p = Rec(datano: "001", kaisono: "000");
        var mid = Rec(datano: "002", kaisono: "001", chokuno: "001", heino: "001");
        var target = Rec(datano: "003", kaisono: "002", chokuno: "001", heino: "001",
                         joheino: "000", oyatno: "002", goyano: "005");
        var mains = new[] { p, mid, target };

        CoordinateOptimizer.Optimize(mains);

        Assert.Equal("001", target.Data.HierarchyNumber);   // 002 ¨ 001
        Assert.Equal("001", target.Data.ParallelNumber);    // joheino=000 ¨ heino=001
    }

    [Fact]
    public void ’¼—ñ’Ç”Ô‚ª001ˆÈŠO‚È‚ç‘ÎÛŠO()
    {
        var p = Rec(datano: "001", kaisono: "000");
        var target = Rec(datano: "002", kaisono: "002", chokuno: "002", heino: "001");
        var mains = new[] { p, target };

        CoordinateOptimizer.Optimize(mains);

        Assert.Equal("002", target.Data.HierarchyNumber);   // •Ï‰»‚È‚µ
    }

    [Fact]
    public void “¯ŠK‘w‚Å•À—ñ’Ç”Ô‚ªˆÙ‚È‚ê‚Î•À—ñŠÖŒW‚ ‚è‚Æ‚µ‚Ä‘ÎÛŠO()
    {
        var p = Rec(datano: "001", kaisono: "000");
        var a = Rec(datano: "002", kaisono: "002", chokuno: "001", heino: "001");
        var b = Rec(datano: "003", kaisono: "002", chokuno: "001", heino: "002");
        var mains = new[] { p, a, b };

        CoordinateOptimizer.Optimize(mains);

        Assert.Equal("002", a.Data.HierarchyNumber);   // •À—ñŠÖŒW‚ ‚è=•Ï‰»‚È‚µ
        Assert.Equal("002", b.Data.HierarchyNumber);
    }

    [Fact]
    public void LA‹@Ší‚Í•ÊŠK‘w‚Ì‚Ü‚ÜÅ“K‰»‚µ‚È‚¢()
    {
        var p = Rec(datano: "001", kaisono: "000");
        var la = Rec(datano: "002", kaisono: "002", chokuno: "001", heino: "001", yoyaku: "LA");
        var mains = new[] { p, la };

        CoordinateOptimizer.Optimize(mains);

        Assert.Equal("002", la.Data.HierarchyNumber);   // LA=•Ï‰»‚È‚µ
    }

    [Fact]
    public void Šî€ŠK‘w‚æ‚è[‚¢ŠK‘w‚à1‚Âã‚°‚ç‚êˆÈ~‚Ì•ÒW‚Ís‚í‚È‚¢()
    {
        var p = Rec(datano: "001", kaisono: "000");
        var target = Rec(datano: "002", kaisono: "002", chokuno: "001", heino: "001",
                         joheino: "000", oyatno: "001", goyano: "000");
        // ’¼—ñ002=‚»‚ê©g‚ÍÅ“K‰»‘ÎÛ(iNo)‚É‚È‚ç‚È‚¢BEditZahyo “à‚ÅŠK‘w‚Ì‚İ 1 ‚Âã‚°‚ç‚ê‚éB
        var deeper = Rec(datano: "003", nyuseno: "001", kaisono: "003", chokuno: "002", heino: "007");
        var mains = new[] { p, target, deeper };

        CoordinateOptimizer.Optimize(mains);

        Assert.Equal("001", target.Data.HierarchyNumber);   // 002 ¨ 001(Å“K‰»‘ÎÛ)
        Assert.Equal("002", deeper.Data.HierarchyNumber);   // 003 ¨ 002(ŠK‘w‚Ì‚İXV)
        Assert.Equal("007", deeper.Data.ParallelNumber);    // •À—ñ’Ç”Ô‚Í˜‚¦’u‚«
    }

    [Fact]
    public void ã—¬•À—ñ‚ª000ˆÈŠO‚È‚ç•À—ñ’Ç”Ô‚Ö•¡Ê‚·‚é()
    {
        var p = Rec(datano: "001", kaisono: "000");
        var target = Rec(datano: "002", kaisono: "002", chokuno: "001", heino: "005",
                         joheino: "003", oyatno: "001");
        var mains = new[] { p, target };

        CoordinateOptimizer.Optimize(mains);

        Assert.Equal("003", target.Data.ParallelNumber);   // joheino=003 ¨ heino=003
    }

    [Fact]
    public void “üü”Ô†‚ªˆÙ‚È‚é—v‘f‚Í•ÒW‚ÅŠª‚«‚Ü‚ê‚È‚¢()
    {
        var p = Rec(datano: "001", nyuseno: "001", kaisono: "000");
        var target = Rec(datano: "002", nyuseno: "001", kaisono: "002", chokuno: "001", heino: "001", oyatno: "001");
        // •Ê“üü‚©‚Â’¼—ñ002=Å“K‰»‘ÎÛ(iNo)‚É‚È‚ç‚È‚¢—v‘fB•ÒW‚ÅŠK‘w‚ğG‚ç‚ê‚È‚¢‚±‚ÆB
        var other = Rec(datano: "003", nyuseno: "002", kaisono: "002", chokuno: "002", heino: "001");
        var mains = new[] { p, target, other };

        CoordinateOptimizer.Optimize(mains);

        Assert.Equal("001", target.Data.HierarchyNumber);   // “¯“üü=Å“K‰»‚³‚ê‚é
        Assert.Equal("002", other.Data.HierarchyNumber);    // •Ê“üü=˜‚¦’u‚«
    }
}
