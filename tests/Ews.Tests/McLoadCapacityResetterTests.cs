using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="McLoadCapacityResetter"/>(C 原典 PropMcFukaReset)の単体テスト。
/// </summary>
public sealed class McLoadCapacityResetterTests
{
    private static MainCircuitResult Rec(
        string datano = "000",
        string yoyaku = "",
        string oyatno = "000",
        string gyoglno = "000",
        string loadKind = "",
        string loadCap = "0000000")
    {
        return new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ReservedWord = yoyaku,
                ParentSequenceNumber = oyatno,
                LineTypeGroupNumber = gyoglno,
                AttachedParameter = new AttachedParameters
                {
                    LoadKind = loadKind,
                    LoadCapacity = loadCap,
                },
            },
        };
    }

    [Fact]
    public void 親MCB配下のMCは負荷容量が初期化される()
    {
        var mcb = Rec(datano: "001", yoyaku: "MCB", gyoglno: "001");
        var mc = Rec(datano: "002", yoyaku: "MC", oyatno: "001", gyoglno: "001",
                     loadKind: "M", loadCap: "0001000");
        var mains = new[] { mcb, mc };

        McLoadCapacityResetter.Reset(mains);

        Assert.Equal("  ", mc.Data.AttachedParameter.LoadKind);
        Assert.Equal("0000000", mc.Data.AttachedParameter.LoadCapacity);
    }

    [Fact]
    public void 親ELB配下のMCも初期化される()
    {
        var elb = Rec(datano: "001", yoyaku: "ELB", gyoglno: "001");
        var mc = Rec(datano: "002", yoyaku: "MC", oyatno: "001", gyoglno: "001",
                     loadKind: "M", loadCap: "0002000");
        var mains = new[] { elb, mc };

        McLoadCapacityResetter.Reset(mains);

        Assert.Equal("  ", mc.Data.AttachedParameter.LoadKind);
        Assert.Equal("0000000", mc.Data.AttachedParameter.LoadCapacity);
    }

    [Fact]
    public void 親がMCBELB以外なら初期化しない()
    {
        var f = Rec(datano: "001", yoyaku: "F", gyoglno: "001");
        var mc = Rec(datano: "002", yoyaku: "MC", oyatno: "001", gyoglno: "001",
                     loadKind: "M", loadCap: "0001000");
        var mains = new[] { f, mc };

        McLoadCapacityResetter.Reset(mains);

        Assert.Equal("M", mc.Data.AttachedParameter.LoadKind);
        Assert.Equal("0001000", mc.Data.AttachedParameter.LoadCapacity);
    }

    [Fact]
    public void 行種グループ番号が異なれば対象外()
    {
        var mcb = Rec(datano: "001", yoyaku: "MCB", gyoglno: "001");
        var mc = Rec(datano: "002", yoyaku: "MC", oyatno: "001", gyoglno: "002",
                     loadKind: "M", loadCap: "0001000");
        var mains = new[] { mcb, mc };

        McLoadCapacityResetter.Reset(mains);

        Assert.Equal("M", mc.Data.AttachedParameter.LoadKind);
        Assert.Equal("0001000", mc.Data.AttachedParameter.LoadCapacity);
    }

    [Fact]
    public void 予約語MC以外は対象外()
    {
        var mcb = Rec(datano: "001", yoyaku: "MCB", gyoglno: "001");
        var mcfr = Rec(datano: "002", yoyaku: "MCFR", oyatno: "001", gyoglno: "001",
                       loadKind: "M", loadCap: "0001000");
        var mains = new[] { mcb, mcfr };

        McLoadCapacityResetter.Reset(mains);

        Assert.Equal("M", mcfr.Data.AttachedParameter.LoadKind);   // MC ではないので対象外
        Assert.Equal("0001000", mcfr.Data.AttachedParameter.LoadCapacity);
    }
}
