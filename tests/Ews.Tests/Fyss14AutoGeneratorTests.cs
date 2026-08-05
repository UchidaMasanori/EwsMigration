using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="Fyss14AutoGenerator"/>(yCŒ´“TzFyss14.c Fyss14_Make_UpperParm ‚Ì¶¬’i)‚Ì’P‘ÌƒeƒXƒgB
/// NT/VT/PLTR ”»’è¨‘}“ü‚ÌŒ‹ü‚Æ¶¬—L–³(=C ‚Ì f)‚Ì“`”d‚ğŒŸØ‚·‚éB
/// </summary>
public sealed class Fyss14AutoGeneratorTests
{
    // NT ”»’è‚ğ–‚½‚·‘Ši MCB ‚ğŠÜ‚Şå‰ñ˜H(P/MCB/SB)‚ğ‘g‚ŞB
    private static MainCircuitResult NtRec(
        int datano, string oyatno, string yoyaku,
        string goyano = "000", string kaisono = "000", string epap = "000")
    {
        var r = new MainCircuitResult { SequenceNumber = datano.ToString("D3") };
        MainCircuitData d = r.Data;
        d.SystemKind = '1';
        d.ReservedWord = yoyaku;
        d.ParentSequenceNumber = oyatno;
        d.GroupParentSequenceNumber = goyano;
        d.HierarchyNumber = kaisono;
        d.ElectricalParameterSlots[2].P = epap;
        d.CircuitPhaseCount = '3';
        d.CircuitWireType = '3';
        d.CircuitVoltage[0] = "210";
        d.CircuitVoltage[1] = "105";
        d.CircuitVoltage[2] = "000";
        return r;
    }

    // DI •\¦“”(PLTR ¶¬‘ÎÛ)‚ğ‘g‚ŞB
    private static MainCircuitResult Lamp(int datano)
    {
        var r = new MainCircuitResult { SequenceNumber = datano.ToString("D3") };
        MainCircuitData d = r.Data;
        d.ReservedWord = "GL";
        d.CircuitElement = '3';
        d.CircuitVoltageKind = 'A';
        d.CircuitVoltage[0] = "100";
        d.ElectricalParameterSlots[0].Bn = '1';
        d.ElectricalParameterSlots[2].Bn = '1';   // ep[2]”Õí—Ş=1 ¨ DI
        return r;
    }

    // ‚Ç‚Ì”»’è‚É‚àŠ|‚©‚ç‚È‚¢—v‘f(SB)‚ğ‘g‚ŞB
    private static MainCircuitResult Plain(int datano)
    {
        var r = new MainCircuitResult { SequenceNumber = datano.ToString("D3") };
        MainCircuitData d = r.Data;
        d.ReservedWord = "SB";
        d.CircuitElement = '3';
        return r;
    }

    [Fact]
    public void GenerateAutoCircuits_¶¬‚ª–³‚¯‚ê‚ÎGenerated‚Ífalse‚ÅŒ”‚Í•s•Ï()
    {
        var records = new List<MainCircuitResult> { Plain(1) };

        AutoGenerationSweep sweep = Fyss14AutoGenerator.GenerateAutoCircuits(records);

        Assert.False(sweep.Generated);
        Assert.Single(sweep.Records);
    }

    [Fact]
    public void GenerateAutoCircuits_PLTR¶¬‚ÅGenerated‚ªtrue‚É‚È‚èPLTR‚ª‘}“ü‚³‚ê‚é()
    {
        var records = new List<MainCircuitResult> { Lamp(1) };

        AutoGenerationSweep sweep = Fyss14AutoGenerator.GenerateAutoCircuits(records);

        Assert.True(sweep.Generated);
        Assert.Equal(2, sweep.Records.Count);
        Assert.Contains(sweep.Records, x => x.Data.ReservedWord == "PLTR");
    }

    [Fact]
    public void GenerateAutoCircuits_NT¶¬‚ÅGenerated‚ªtrue‚É‚È‚èNT‚ª‘}“ü‚³‚ê‚é()
    {
        var records = new List<MainCircuitResult>
        {
            NtRec(1, "000", "P"),
            NtRec(2, "001", "MCB", goyano: "001", kaisono: "001", epap: "001"),
            NtRec(3, "002", "SB", goyano: "001", kaisono: "001"),
        };

        AutoGenerationSweep sweep = Fyss14AutoGenerator.GenerateAutoCircuits(records);

        Assert.True(sweep.Generated);
        Assert.Equal(4, sweep.Records.Count);
        Assert.Contains(sweep.Records, x => x.Data.ReservedWord == "NT");
    }
}
