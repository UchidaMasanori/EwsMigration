using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 通電電流積算の電流計算リーフ(<see cref="TerminalCurrentIntegrator"/>)の移植検証。
/// 【C原典】Fyss37_Get_Fuka/Get_DenIa/Get_DenIb/Kei_TR/Set_Tden/Set_Sden(toku/sekkei/src/Fyss37.c)。
/// </summary>
public sealed class TerminalCurrentIntegratorTests
{
    private const int Precision = 9;

    private static MainCircuitResult Rec(
        string yoyaku = "",
        char kikiskbn = ' ',
        string denryu = "00000.00",
        char kpaph = '0',
        string kpav = "000")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = "001",
            Data = new MainCircuitData
            {
                ReservedWord = yoyaku,
                CircuitPhaseCount = kpaph,
                EnergizingCurrent = denryu,
            },
        };
        r.Data.CircuitVoltage[0] = kpav;
        r.Work.EquipmentSelectionKind = kikiskbn;
        return r;
    }

    // ── Get_Fuka(TryGetLoadFactor) ──────────────────────────────────

    [Fact]
    public void 負荷容量表でMCBはATを優先しpry1係数08になる()
    {
        MainCircuitResult r = Rec("MCB");
        r.Data.ElectricalParameterSlots[0].At = "00050.000";

        bool ok = TerminalCurrentIntegrator.TryGetLoadFactor(r, out double factor, out int pry);

        Assert.True(ok);
        Assert.Equal(1, pry);
        Assert.Equal(0.8, factor, Precision);
    }

    [Fact]
    public void 負荷容量表でMGはATゼロならW優先pry2係数10になる()
    {
        MainCircuitResult r = Rec("MG");
        r.Data.ElectricalParameterSlots[0].At = "00000.000"; // ゼロ
        r.Data.ElectricalParameterSlots[0].W1 = "0000050.00";

        bool ok = TerminalCurrentIntegrator.TryGetLoadFactor(r, out double factor, out int pry);

        Assert.True(ok);
        Assert.Equal(2, pry);
        Assert.Equal(1.0, factor, Precision);
    }

    [Fact]
    public void 負荷容量表でMCはA2優先pry5になる()
    {
        MainCircuitResult r = Rec("MC");
        r.Data.ElectricalParameterSlots[0].A2 = "00020.000";

        bool ok = TerminalCurrentIntegrator.TryGetLoadFactor(r, out _, out int pry);

        Assert.True(ok);
        Assert.Equal(5, pry);
    }

    [Fact]
    public void 負荷容量表に無い予約語はfalse()
    {
        MainCircuitResult r = Rec("XYZ");

        bool ok = TerminalCurrentIntegrator.TryGetLoadFactor(r, out _, out _);

        Assert.False(ok);
    }

    // ── Get_DenIa ────────────────────────────────────────────────────

    [Fact]
    public void 電流値Iaは優先パラメータに係数08を乗じる()
    {
        MainCircuitResult r = Rec("MCB");
        r.Data.ElectricalParameterSlots[0].At = "00050.000";

        bool ok = TerminalCurrentIntegrator.TryGetCurrentIa(r, out double current);

        Assert.True(ok);
        Assert.Equal(40.0, current, Precision); // 50 × 0.8
    }

    // ── Get_DenIb ────────────────────────────────────────────────────

    [Fact]
    public void 電流値Ibは積算エリアのa足すb足すcde08倍()
    {
        MainCircuitResult r = Rec("MCB");
        r.Data.ElectricalParameterSlots[0].At = "00050.000";
        r.Work.AccumulationSlots[0].A = 10;
        r.Work.AccumulationSlots[0].B = 5;
        r.Work.AccumulationSlots[0].C = 2;

        bool ok = TerminalCurrentIntegrator.TryGetCurrentIb(r, 0, out double current);

        Assert.True(ok);
        Assert.Equal(16.6, current, Precision); // 10 + 5 + 2×0.8
    }

    [Fact]
    public void 電流値Ibは負荷容量表に無ければfalse()
    {
        MainCircuitResult r = Rec("XYZ");

        bool ok = TerminalCurrentIntegrator.TryGetCurrentIb(r, 0, out _);

        Assert.False(ok);
    }

    // ── Kei_TR ───────────────────────────────────────────────────────

    [Fact]
    public void TR係数は定格電圧2割る定格電圧1()
    {
        MainCircuitResult r = Rec("TR");
        r.Data.ElectricalParameterSlots[0].V1Idx = "1";
        r.Data.ElectricalParameterSlots[0].V2Idx = "1";
        r.Data.ElectricalParameterSlots[0].V1[0] = "00000210";
        r.Data.ElectricalParameterSlots[0].V2[0] = "00000105";

        double kei = TerminalCurrentIntegrator.GetTrCoefficient(r);

        Assert.Equal(0.5, kei, Precision); // 105 / 210
    }

    // ── Set_Tden ─────────────────────────────────────────────────────

    [Fact]
    public void 通電電流値は機器選定1で各相最大値をセットする()
    {
        MainCircuitResult r = Rec(kikiskbn: '1', denryu: "00000.00");
        r.Work.AccumulationSlots[2].A = 40;

        TerminalCurrentIntegrator.SetEnergizingCurrent(r);

        Assert.Equal("00040.00", r.Data.EnergizingCurrent);
    }

    [Fact]
    public void 通電電流値は機器選定1以外でab足すcde08倍の最大値をセットする()
    {
        MainCircuitResult r = Rec(kikiskbn: '2', denryu: "00000.00");
        r.Work.AccumulationSlots[0].A = 10;
        r.Work.AccumulationSlots[0].B = 5;
        r.Work.AccumulationSlots[0].C = 2;

        TerminalCurrentIntegrator.SetEnergizingCurrent(r);

        Assert.Equal("00016.60", r.Data.EnergizingCurrent); // 10 + 5 + 2×0.8
    }

    [Fact]
    public void 通電電流値が既にあれば変更しない()
    {
        MainCircuitResult r = Rec(kikiskbn: '1', denryu: "00005.00");
        r.Work.AccumulationSlots[0].A = 40;

        TerminalCurrentIntegrator.SetEnergizingCurrent(r);

        Assert.Equal("00005.00", r.Data.EnergizingCurrent);
    }

    // ── Set_Sden ─────────────────────────────────────────────────────

    [Fact]
    public void 設定電流値は機器選定2で積算エリアから算出する()
    {
        MainCircuitResult r = Rec("MCB", kikiskbn: '2', kpav: "200", kpaph: '1');
        r.Data.ElectricalParameterSlots[0].At = "00050.000";
        r.Work.AccumulationSlots[0].S = 1000; // s=1, m=0 → wk1=1,wk2=1,is=5.4

        TerminalCurrentIntegrator.SetSetCurrent(r);

        Assert.Equal(5.4, r.Work.SetCurrent, Precision);
    }

    [Fact]
    public void 設定電流値は機器選定1では設定しない()
    {
        MainCircuitResult r = Rec("MCB", kikiskbn: '1');

        TerminalCurrentIntegrator.SetSetCurrent(r);

        Assert.Equal(0.0, r.Work.SetCurrent, Precision);
    }

    [Fact]
    public void 設定電流値は負荷容量表に無ければ設定しない()
    {
        MainCircuitResult r = Rec("XYZ", kikiskbn: '2');

        TerminalCurrentIntegrator.SetSetCurrent(r);

        Assert.Equal(0.0, r.Work.SetCurrent, Precision);
    }
}
