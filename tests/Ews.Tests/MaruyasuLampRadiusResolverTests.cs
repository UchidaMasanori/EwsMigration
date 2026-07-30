using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Ews.Domain.Configuration;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// ƒ}ƒ‹ƒ„ƒX»ƒ‰ƒ“ƒv‚ÌŒaƒTƒCƒYİ’è(PropChgMALampType / PropChgMALampTypeC)‚ÌˆÚAŒŸØB
///
/// yCŒ´“TzFysk00.c(toku/sekkei/src, ‰ü’ù&lt;117&gt;/&lt;146&gt;)BMA/MAN w’èƒ‰ƒ“ƒv‚ÅŒa“ü—Í(››P)‚ª
/// –³‚¢ê‡AŒaƒTƒCƒY‚ğD–yHê=22mmE‚»‚êˆÈŠO=25mm ‚Éİ’è‚·‚éB
/// </summary>
public sealed class MaruyasuLampRadiusResolverTests
{
    private static MaruyasuLampRadiusResolver Build(int facilityGroup, string circuitText)
    {
        const string zone = "Z";
        var facility = new InMemoryFacilityAreaResolver([new FacilityAreaEntry(zone, facilityGroup)]);
        var parameters = new InMemoryRuntimeParameterProvider(
            new Dictionary<string, string?> { [RuntimeParameterNames.ZoneCode] = zone });
        var area = new CircuitDescriptionArea(
            [new CircuitDescriptionLine { LineNumber = 5, CircuitText = circuitText }]);
        return new MaruyasuLampRadiusResolver(facility, parameters, area);
    }

    private static MainCircuitResult Lamp(string reservedWord = "WL") =>
        new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = reservedWord,
                DescriptionRow = "005",
                DescriptionColumn = "001",
            },
        };

    private static ControlEquipmentInfo ControlLamp(string reservedWord = "RL") =>
        new()
        {
            ReservedWord = reservedWord,
            DescriptionRow = "005",
            DescriptionColumn = "001",
        };

    private static NumericElectricalParameters[] Sep() => [new(), new(), new()];

    // ---- å‰ñ˜H PropChgMALampType ----

    [Fact]
    public void å‰ñ˜H_ƒ}ƒ‹ƒ„ƒXw’è‚ÅŒa“ü—Í‚È‚µ‚ÍŒa25‚É‚·‚é()
    {
        MainCircuitResult lamp = Lamp("WL");
        NumericElectricalParameters[] sep = Sep();

        Build(5, "WL,").Resolve(lamp, ["MA ", "   ", "   ", "   "], sep);

        Assert.Equal(25.0, sep[0].Ksize);
        Assert.Equal(25.0, sep[2].Ksize);
        Assert.Equal("025.0", lamp.Data.ElectricalParameterSlots[0].Ksize);
        Assert.Equal("025.0", lamp.Data.ElectricalParameterSlots[2].Ksize);
    }

    [Fact]
    public void å‰ñ˜H_D–yHê‚ÍŒa22‚É‚·‚é()
    {
        MainCircuitResult lamp = Lamp("WL");
        NumericElectricalParameters[] sep = Sep();

        Build(1, "WL,").Resolve(lamp, ["MAN", "   ", "   ", "   "], sep);

        Assert.Equal(22.0, sep[0].Ksize);
        Assert.Equal("022.0", lamp.Data.ElectricalParameterSlots[0].Ksize);
    }

    [Fact]
    public void å‰ñ˜H_Œa“ü—Í‚ ‚è‚Í•ÏX‚µ‚È‚¢()
    {
        MainCircuitResult lamp = Lamp("WL");
        NumericElectricalParameters[] sep = Sep();

        Build(5, "WL22P,").Resolve(lamp, ["MA ", "   ", "   ", "   "], sep);

        Assert.Equal(0.0, sep[0].Ksize);
    }

    [Fact]
    public void å‰ñ˜H_ƒ}ƒ‹ƒ„ƒXˆÈŠO‚Í•ÏX‚µ‚È‚¢()
    {
        MainCircuitResult lamp = Lamp("WL");
        NumericElectricalParameters[] sep = Sep();

        Build(5, "WL,").Resolve(lamp, ["IZ ", "   ", "   ", "   "], sep);

        Assert.Equal(0.0, sep[0].Ksize);
    }

    [Fact]
    public void å‰ñ˜H_ƒ‰ƒ“ƒv—\–ñŒê‚Å‚È‚¯‚ê‚Î•ÏX‚µ‚È‚¢()
    {
        MainCircuitResult lamp = Lamp("MC");
        NumericElectricalParameters[] sep = Sep();

        Build(5, "WL,").Resolve(lamp, ["MA ", "   ", "   ", "   "], sep);

        Assert.Equal(0.0, sep[0].Ksize);
    }

    // ---- §Œä‰ñ˜H PropChgMALampTypeC ----

    [Fact]
    public void §Œä_ƒ}ƒ‹ƒ„ƒXw’è‚ÅŒa“ü—Í‚È‚µ‚Í3˜g‚ğŒa25‚É‚·‚é()
    {
        ControlEquipmentInfo lamp = ControlLamp("RL");

        Build(5, "RL,").ResolveControl(lamp, ["MA ", "   ", "   ", "   "]);

        Assert.Equal("025.0", lamp.ElectricalParameterSlots[0].Ksize);
        Assert.Equal("025.0", lamp.ElectricalParameterSlots[1].Ksize);
        Assert.Equal("025.0", lamp.ElectricalParameterSlots[2].Ksize);
    }

    [Fact]
    public void §Œä_D–yHê‚ÍŒa22‚É‚·‚é()
    {
        ControlEquipmentInfo lamp = ControlLamp("GL");

        Build(1, "GL,").ResolveControl(lamp, ["MAN", "   ", "   ", "   "]);

        Assert.Equal("022.0", lamp.ElectricalParameterSlots[0].Ksize);
    }

    [Fact]
    public void §Œä_WL‚Í‘ÎÛŠO‚Å•ÏX‚µ‚È‚¢()
    {
        ControlEquipmentInfo lamp = ControlLamp("WL");
        string before = lamp.ElectricalParameterSlots[0].Ksize;

        Build(5, "WL,").ResolveControl(lamp, ["MA ", "   ", "   ", "   "]);

        Assert.Equal(before, lamp.ElectricalParameterSlots[0].Ksize);
    }

    [Fact]
    public void §Œä_Œa“ü—Í‚ ‚è‚Í•ÏX‚µ‚È‚¢()
    {
        ControlEquipmentInfo lamp = ControlLamp("RL");
        string before = lamp.ElectricalParameterSlots[0].Ksize;

        Build(5, "RL22P,").ResolveControl(lamp, ["MA ", "   ", "   ", "   "]);

        Assert.Equal(before, lamp.ElectricalParameterSlots[0].Ksize);
    }
}
