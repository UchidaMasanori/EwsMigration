using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 負荷発生元の負荷容量決定(<see cref="LoadSourceSelector"/>)の移植検証。
/// 【C原典】set_fky/get_ep(toku/sekkei/src/Fyss31.c)。
/// </summary>
public sealed class LoadSourceSelectorTests
{
    private const int Precision = 9;

    private static MainCircuitResult Rec(string yoyaku, char kpaph = '0', string kpav = "000")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = "001",
            Data = new MainCircuitData
            {
                ReservedWord = yoyaku,
                CircuitPhaseCount = kpaph,
            },
        };
        r.Data.CircuitVoltage[0] = kpav;
        return r;
    }

    [Fact]
    public void ブレーカはATに係数を乗じて電流を求め予約語優先順位2を返す()
    {
        MainCircuitResult mcb = Rec("MCB");
        mcb.Data.ElectricalParameterSlots[0].At = "00050.000";

        int rc = LoadSourceSelector.SelectLoadCurrent([mcb], 0, 4, out int priority, out double current);

        Assert.Equal(LoadSourceSelector.Selected, rc);
        Assert.Equal(2, priority);
        Assert.Equal(40.0, current, Precision); // 0.8 × 50
    }

    [Fact]
    public void 候補の予約語優先順位がbestより低ければ2を返す()
    {
        MainCircuitResult mcb = Rec("MCB"); // 予約語優先順位 2
        mcb.Data.ElectricalParameterSlots[0].At = "00050.000";

        int rc = LoadSourceSelector.SelectLoadCurrent([mcb], 0, 1, out _, out _);

        Assert.Equal(LoadSourceSelector.LowerPriority, rc);
    }

    [Fact]
    public void MGはATゼロならWで電動機として電流化し優先順位1を返す()
    {
        MainCircuitResult mg = Rec("MG", kpaph: '3', kpav: "200");
        mg.Data.ElectricalParameterSlots[0].At = "00000.000";
        mg.Data.ElectricalParameterSlots[0].W1 = "0000050.00"; // 50W

        int rc = LoadSourceSelector.SelectLoadCurrent([mg], 0, 4, out int priority, out double current);

        EnergizingCurrentCalculator.TryCalculate(mg.Data, 50.0, "M ", out double expected);

        Assert.Equal(LoadSourceSelector.Selected, rc);
        Assert.Equal(1, priority);
        Assert.Equal(expected, current, Precision);
    }

    [Fact]
    public void VAは非電動機予約語で相数により電動機かヒーターを選ぶ()
    {
        MainCircuitResult tr = Rec("TR", kpaph: '1', kpav: "100"); // TR ep_pry {0,0,1,0,0}=VA
        tr.Data.ElectricalParameterSlots[0].Va = "0000500.00"; // 500VA

        int rc = LoadSourceSelector.SelectLoadCurrent([tr], 0, 4, out _, out double current);

        // TR は非電動機予約語 → 単相なのでヒーター("H ")として電流化。
        EnergizingCurrentCalculator.TryCalculate(tr.Data, 500.0, "H ", out double expected);

        Assert.Equal(LoadSourceSelector.Selected, rc);
        Assert.Equal(expected, current, Precision);
    }

    [Fact]
    public void MCはA2に係数を乗じて電流を求める()
    {
        MainCircuitResult mc = Rec("MC"); // MC ep_pry {0,0,0,0,1}
        mc.Data.ElectricalParameterSlots[0].A2 = "00020.000";

        int rc = LoadSourceSelector.SelectLoadCurrent([mc], 0, 4, out int priority, out double current);

        Assert.Equal(LoadSourceSelector.Selected, rc);
        Assert.Equal(2, priority);
        Assert.Equal(16.0, current, Precision); // 0.8 × 20
    }

    [Fact]
    public void ATがサーチ上限値ならフレーム電流AFを用いる()
    {
        MainCircuitResult mcb = Rec("MCB");
        mcb.Data.ElectricalParameterSlots[0].At = "99999.999";
        mcb.Data.ElectricalParameterSlots[0].Af = "00030.000";

        int rc = LoadSourceSelector.SelectLoadCurrent([mcb], 0, 4, out _, out double current);

        Assert.Equal(LoadSourceSelector.Selected, rc);
        Assert.Equal(24.0, current, Precision); // 0.8 × 30
    }

    [Fact]
    public void 電気パラメータが全てゼロなら値無しの1を返す()
    {
        MainCircuitResult mcb = Rec("MCB"); // ep_pry {1,0,0,0,0}=AT のみ
        mcb.Data.ElectricalParameterSlots[0].At = "00000.000";

        int rc = LoadSourceSelector.SelectLoadCurrent([mcb], 0, 4, out _, out _);

        Assert.Equal(LoadSourceSelector.NoValue, rc);
    }

    [Fact]
    public void 負荷容量表に無い予約語は3を返す()
    {
        MainCircuitResult x = Rec("XYZ");

        int rc = LoadSourceSelector.SelectLoadCurrent([x], 0, 4, out _, out _);

        Assert.Equal(LoadSourceSelector.NotInTable, rc);
    }
}
