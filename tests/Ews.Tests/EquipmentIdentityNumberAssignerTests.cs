using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="EquipmentIdentityNumberAssigner"/>(yCŒ´“TzFyss14.c Kiki_Equal_Bangou_Set)‚Ì’P‘ÌƒeƒXƒgB
/// CT/ZCT/WH/PS ‚Ì“Á—á‚ÆA—\–ñŒêƒ}ƒXƒ^ douskkbn ‚É‚æ‚é”Ä—p•t—^‚ğŒŸØ‚·‚éB
/// </summary>
public sealed class EquipmentIdentityNumberAssignerTests
{
    private static MainCircuitResult Rec(
        string yoyaku,
        char kiryoso = ' ',
        char yoyakkbn = ' ',
        string ysno = "00",
        char yssfx = ' ')
    {
        var r = new MainCircuitResult();
        MainCircuitData d = r.Data;
        d.ReservedWord = yoyaku;
        d.CircuitElement = kiryoso;
        d.AutoGenerationKind = yoyakkbn;
        d.DesignationNumber = ysno;
        d.DesignationSuffix = yssfx;
        return r;
    }

    private static IReadOnlyList<ReservedWordMaster> Master(params (string Word, char Douskkbn)[] entries)
    {
        var list = new List<ReservedWordMaster>();
        foreach ((string word, char douskkbn) in entries)
        {
            list.Add(new ReservedWordMaster { ReservedWord = word, SameEquipmentAssignableKind = douskkbn });
        }
        return list;
    }

    [Fact]
    public void Assign_CT‚Ì1—v‘f‚Æ‘O•û2—v‘f‚É“¯ˆê”Ô†‚ğ•t—^‚·‚é()
    {
        var two = Rec("CT", kiryoso: '2');
        var one = Rec("CT", kiryoso: '1');
        var mains = new List<MainCircuitResult> { two, one };

        EquipmentIdentityNumberAssigner.Assign(mains, Master());

        Assert.Equal("01", two.Data.IdentityNumber);
        Assert.Equal("01", one.Data.IdentityNumber);
    }

    [Fact]
    public void Assign_ZCT©“®¶¬‚Ì‘Î‚É“¯ˆê”Ô†‚ğ•t—^‚·‚é()
    {
        var first = Rec("ZCT", yoyakkbn: '1');
        var second = Rec("ZCT", yoyakkbn: '1');
        var mains = new List<MainCircuitResult> { first, second };

        EquipmentIdentityNumberAssigner.Assign(mains, Master());

        Assert.Equal("01", first.Data.IdentityNumber);
        Assert.Equal("01", second.Data.IdentityNumber);
    }

    [Fact]
    public void Assign_WH©“®¶¬4‚ÍŒã•û‚Ì”ñ©“®¶¬WH‚Æ‘Î‚É‚·‚é()
    {
        var auto = Rec("WH", kiryoso: '4', yoyakkbn: '1');
        var manual = Rec("WH", kiryoso: '1', yoyakkbn: ' ');
        var mains = new List<MainCircuitResult> { auto, manual };

        EquipmentIdentityNumberAssigner.Assign(mains, Master());

        Assert.Equal("01", auto.Data.IdentityNumber);
        Assert.Equal("01", manual.Data.IdentityNumber);
    }

    [Fact]
    public void Assign_PS‚Ì‘Î‚ğ“¯ˆê”Ô†‚É‚µTR‚Ö‘‚«Š·‚¦‚é()
    {
        var first = Rec("PS");
        var second = Rec("PS");
        var mains = new List<MainCircuitResult> { first, second };

        EquipmentIdentityNumberAssigner.Assign(mains, Master());

        Assert.Equal("01", first.Data.IdentityNumber);
        Assert.Equal("01", second.Data.IdentityNumber);
        Assert.Equal("TR", first.Data.ReservedWord);
        Assert.Equal("TR", second.Data.ReservedWord);
    }

    [Fact]
    public void Assign_”Ä—p‚Í—\–ñŒê”Ô†ˆê’v‚Ædouskkbn1‚Å“¯ˆê”Ô†‚ğ•t—^‚·‚é()
    {
        var first = Rec("MC", ysno: "05", yssfx: 'A');
        var second = Rec("MC", ysno: "05", yssfx: 'A');
        var mains = new List<MainCircuitResult> { first, second };

        EquipmentIdentityNumberAssigner.Assign(mains, Master(("MC", '1')));

        Assert.Equal("01", first.Data.IdentityNumber);
        Assert.Equal("01", second.Data.IdentityNumber);
    }

    [Fact]
    public void Assign_”Ä—p‚Ådouskkbn‚ª1ˆÈŠO‚È‚ç•t—^‚µ‚È‚¢()
    {
        var first = Rec("MC", ysno: "05", yssfx: 'A');
        var second = Rec("MC", ysno: "05", yssfx: 'A');
        var mains = new List<MainCircuitResult> { first, second };

        EquipmentIdentityNumberAssigner.Assign(mains, Master(("MC", ' ')));

        Assert.Equal("00", first.Data.IdentityNumber);
        Assert.Equal("00", second.Data.IdentityNumber);
    }

    [Fact]
    public void Assign_”Ä—p‚Å—\–ñŒê”Ô†00‚Í‘ÎÛŠO()
    {
        var first = Rec("MC", ysno: "00", yssfx: 'A');
        var second = Rec("MC", ysno: "00", yssfx: 'A');
        var mains = new List<MainCircuitResult> { first, second };

        EquipmentIdentityNumberAssigner.Assign(mains, Master(("MC", '1')));

        Assert.Equal("00", first.Data.IdentityNumber);
        Assert.Equal("00", second.Data.IdentityNumber);
    }

    [Fact]
    public void Assign_”Ä—p‚ÅƒTƒtƒBƒbƒNƒX•sˆê’v‚È‚ç•t—^‚µ‚È‚¢()
    {
        var first = Rec("MC", ysno: "05", yssfx: 'A');
        var second = Rec("MC", ysno: "05", yssfx: 'B');
        var mains = new List<MainCircuitResult> { first, second };

        EquipmentIdentityNumberAssigner.Assign(mains, Master(("MC", '1')));

        Assert.Equal("00", first.Data.IdentityNumber);
        Assert.Equal("00", second.Data.IdentityNumber);
    }

    [Fact]
    public void Assign_ELR‚Íƒ}ƒXƒ^–¢“o˜^‚Å‚à—\–ñŒê”Ô†ˆê’v‚Å•t—^‚·‚é()
    {
        var first = Rec("ELR", ysno: "07", yssfx: 'A');
        var second = Rec("ELR", ysno: "07", yssfx: 'A');
        var mains = new List<MainCircuitResult> { first, second };

        EquipmentIdentityNumberAssigner.Assign(mains, Master());

        Assert.Equal("01", first.Data.IdentityNumber);
        Assert.Equal("01", second.Data.IdentityNumber);
    }

    [Fact]
    public void Assign_”Ä—p‚Å3—v‘f–Ú‚ÍŠ„“–Ï”Ô†‚ğŒp³‚·‚é()
    {
        var first = Rec("MC", ysno: "05", yssfx: 'A');
        var second = Rec("MC", ysno: "05", yssfx: 'A');
        var third = Rec("MC", ysno: "05", yssfx: 'A');
        var mains = new List<MainCircuitResult> { first, second, third };

        EquipmentIdentityNumberAssigner.Assign(mains, Master(("MC", '1')));

        Assert.Equal("01", first.Data.IdentityNumber);
        Assert.Equal("01", second.Data.IdentityNumber);
        Assert.Equal("01", third.Data.IdentityNumber);
    }
}
