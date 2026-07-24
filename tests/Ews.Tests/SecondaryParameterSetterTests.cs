using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="SecondaryParameterSetter"/>(yCŒ´“TzFyss14.c SetParam_ep2_* ŒQ)‚Ì’P‘ÌƒeƒXƒgB
/// ‰ñ˜H“d‹C’l(kpa*)‚©‚ç ep[2] ‚ğŒˆ’è‚·‚éŒˆ’è“Iˆ—‚ğŒŸØ‚·‚éB
/// </summary>
public sealed class SecondaryParameterSetterTests
{
    private static MainCircuitData NewData() => new();

    // ---- SetParam_ep2_MCB_P -------------------------------------------------

    [Theory]
    [InlineData('1', "002")] // ‰ñ˜H‹É” '1' ¨ 3Œ…–Ú '2'
    [InlineData('2', "002")] // ‚»‚êˆÈŠO‚Í‰ñ˜H‹É”‚»‚Ì‚Ü‚Ü(2¨3Œ…–Ú'2')
    [InlineData('3', "003")]
    public void SetMcbPole‚Í‰ñ˜H‹É”‚©‚ç‹É”3Œ…–Ú‚ğŒˆ’è‚·‚é(char pole, string expectedP)
    {
        MainCircuitData data = NewData();
        data.CircuitPoleCount = pole;

        SecondaryParameterSetter.SetMcbPole(data);

        Assert.Equal(expectedP, data.ElectricalParameterSlots[2].P);
    }

    // ---- SetParam_ep2_MCB_E -------------------------------------------------

    [Theory]
    [InlineData('1', '2', '1', "1")]
    [InlineData('1', '2', '2', "2")]
    [InlineData('1', '3', '0', "2")]
    [InlineData('3', '3', '0', "3")]
    [InlineData('3', '4', '0', "3")]
    [InlineData('0', '0', '0', "2")]
    public void SetMcbElement‚Í‘Šü®‹É‚©‚çƒGƒŒƒƒ“ƒg”‚ğŒˆ’è‚·‚é(char ph, char wr, char p, string expectedE)
    {
        MainCircuitData data = NewData();
        data.CircuitPhaseCount = ph;
        data.CircuitWireType = wr;
        data.CircuitPoleCount = p;

        SecondaryParameterSetter.SetMcbElement(data);

        Assert.Equal(expectedE, data.ElectricalParameterSlots[2].E);
    }

    [Fact]
    public void SetMcbElement‚Íep0‚ÌAT‚ª99999_999‚È‚ç0‚É‚·‚é()
    {
        MainCircuitData data = NewData();
        data.CircuitPhaseCount = '1';
        data.CircuitWireType = '2';
        data.CircuitPoleCount = '1'; // ’Êí‚È‚ç "1" ‚É‚È‚éğŒ
        data.ElectricalParameterSlots[0].At = "99999.999";

        SecondaryParameterSetter.SetMcbElement(data);

        Assert.Equal("0", data.ElectricalParameterSlots[2].E);
    }

    // ---- SetParam_ep2_MCB_V2 ------------------------------------------------

    [Fact]
    public void SetMcbVoltage2‚ÍÅ‘å‰ñ˜H“dˆ³‚ğ“dˆ³2‚ÖŠi”[‚·‚é()
    {
        MainCircuitData data = NewData();
        data.CircuitVoltage = ["100", "200", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetMcbVoltage2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        // epav2[0] ‚Ì 4 Œ…–ÚˆÈ~ 3 Œ…‚ÖÅ‘å“dˆ³ "200"
        Assert.Equal("00020000", ep2.V2[0]);
        Assert.Equal("000000.0", ep2.V2[1]);
        Assert.Equal("000000.0", ep2.V2[2]);
        Assert.Equal('A', ep2.V2Kbn);
    }

    // ---- SetParam_ep2_MC_P --------------------------------------------------

    [Theory]
    [InlineData("200", "002")] // 105’´ ¨ '2'
    [InlineData("105", "001")] // 105ˆÈ‰º ¨ '1'
    [InlineData("100", "001")]
    public void SetMcPole‚Í‰ñ˜H“dˆ³0‚Ì105‹«ŠE‚Å‹É”‚ğŒˆ’è‚·‚é(string v0, string expectedP)
    {
        MainCircuitData data = NewData();
        data.CircuitVoltage = [v0, "000", "000"];

        SecondaryParameterSetter.SetMcPole(data);

        Assert.Equal(expectedP, data.ElectricalParameterSlots[2].P);
    }

    // ---- SetParam_ep2_MG_* --------------------------------------------------

    [Fact]
    public void SetMgElement‚Íí‚É2()
    {
        MainCircuitData data = NewData();
        SecondaryParameterSetter.SetMgElement(data);
        Assert.Equal("2", data.ElectricalParameterSlots[2].E);
    }

    [Fact]
    public void SetMgContactA‚ÆB‚Í00‚É‚·‚é()
    {
        MainCircuitData data = NewData();
        data.ElectricalParameterSlots[2].Ac = "99";
        data.ElectricalParameterSlots[2].Bc = "99";

        SecondaryParameterSetter.SetMgContactA(data);
        SecondaryParameterSetter.SetMgContactB(data);

        Assert.Equal("00", data.ElectricalParameterSlots[2].Ac);
        Assert.Equal("00", data.ElectricalParameterSlots[2].Bc);
    }

    // ---- SetParam_ep2_TS_* --------------------------------------------------

    [Fact]
    public void SetTsControlVoltage‚ÍÅ‘å‰ñ˜H“dˆ³‚Æ‹æ•ª‚ğ§Œä“dˆ³‚Öİ’è‚·‚é()
    {
        MainCircuitData data = NewData();
        data.CircuitVoltage = ["100", "210", "000"];
        data.CircuitVoltageKind = 'D';

        SecondaryParameterSetter.SetTsControlVoltage(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("210", ep2.Vc);
        Assert.Equal('D', ep2.VcKbn);
    }

    [Fact]
    public void SetTsContactA‚ÆB‚Í00‚É‚·‚é()
    {
        MainCircuitData data = NewData();
        data.ElectricalParameterSlots[2].Ac = "99";
        data.ElectricalParameterSlots[2].Bc = "99";

        SecondaryParameterSetter.SetTsContactA(data);
        SecondaryParameterSetter.SetTsContactB(data);

        Assert.Equal("00", data.ElectricalParameterSlots[2].Ac);
        Assert.Equal("00", data.ElectricalParameterSlots[2].Bc);
    }

    // ---- “]‘—ƒƒ\ƒbƒh(MC_V2/MG_V2/TS_V2 = MCB_V2) ---------------------------

    [Fact]
    public void MC_MG_TS‚ÌVoltage2‚ÍMCB_V2‚Æ“¯ˆêŒ‹‰Ê‚É‚È‚é()
    {
        MainCircuitData baseData = NewData();
        baseData.CircuitVoltage = ["220", "100", "000"];
        baseData.CircuitVoltageKind = 'A';

        MainCircuitData mcb = Clone(baseData);
        MainCircuitData mc = Clone(baseData);
        MainCircuitData mg = Clone(baseData);
        MainCircuitData ts = Clone(baseData);

        SecondaryParameterSetter.SetMcbVoltage2(mcb);
        SecondaryParameterSetter.SetMcVoltage2(mc);
        SecondaryParameterSetter.SetMgVoltage2(mg);
        SecondaryParameterSetter.SetTsVoltage2(ts);

        string expected = mcb.ElectricalParameterSlots[2].V2[0];
        Assert.Equal("00022000", expected); // Å‘å "220" ‚ª [3..6) ‚Ö
        Assert.Equal(expected, mc.ElectricalParameterSlots[2].V2[0]);
        Assert.Equal(expected, mg.ElectricalParameterSlots[2].V2[0]);
        Assert.Equal(expected, ts.ElectricalParameterSlots[2].V2[0]);
    }

    private static MainCircuitData Clone(MainCircuitData src)
    {
        return new MainCircuitData
        {
            CircuitPhaseCount = src.CircuitPhaseCount,
            CircuitWireType = src.CircuitWireType,
            CircuitPoleCount = src.CircuitPoleCount,
            CircuitVoltage = [.. src.CircuitVoltage],
            CircuitVoltageKind = src.CircuitVoltageKind,
        };
    }
}
