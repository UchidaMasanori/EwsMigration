using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// ＳＣ／ＮＴ の上流積算処理(<see cref="ScNtUpstreamAccumulator"/>)の移植検証。
/// 【C原典】Fyss3A_SC_NT_Sekisan ほか(toku/sekkei/src/Fyss3A.c)。
/// 対象データチェック・通電電流値取得(NT=A2 / SC=UF換算 / KVAR変換 / SC1次電流)・
/// 上流積算(積上区分='1' まで遡り)・オーケストレータ を検証する。
/// </summary>
public sealed class ScNtUpstreamAccumulatorTests
{
    private static MainCircuitResult Row(
        string datano,
        string oyatno = "000",
        char kiryoso = '1',
        string yoyaku = "",
        char jagekbn = ' ',
        char kpaph = '0',
        string kpav0 = "000",
        string kpahz = "00",
        string ep2A2 = "00000.000",
        string ep2Uf = "00000000",
        string ep2Kvar = "000000",
        string fpaln1 = "",
        string denryu = "00000.00")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ParentSequenceNumber = oyatno,
                CircuitElement = kiryoso,
                ReservedWord = yoyaku,
                StackKind = jagekbn,
                CircuitPhaseCount = kpaph,
                CircuitFrequency = kpahz,
                EnergizingCurrent = denryu,
            },
        };
        r.Data.CircuitVoltage[0] = kpav0;
        r.Data.ElectricalParameterSlots[2].A2 = ep2A2;
        r.Data.ElectricalParameterSlots[2].Uf = ep2Uf;
        r.Data.ElectricalParameterSlots[2].Kvar = ep2Kvar;
        r.Data.AttachedParameter.LoadName[1] = fpaln1;
        return r;
    }

    // ── 対象データチェック(Fyss3A_Chk_Yoyaku) ────────────────────────────────

    [Fact]
    public void 回路要素1でNT予約語なら対象かつフラグ1()
    {
        var r = Row("001", kiryoso: '1', yoyaku: "NT");
        Assert.Equal((0, 1), ScNtUpstreamAccumulator.CheckReservedWord([r], 0));
    }

    [Fact]
    public void 回路要素1でSC予約語なら対象かつフラグ2()
    {
        var r = Row("001", kiryoso: '1', yoyaku: "SC");
        Assert.Equal((0, 2), ScNtUpstreamAccumulator.CheckReservedWord([r], 0));
    }

    [Fact]
    public void 回路要素1でも他予約語なら対象外()
    {
        var r = Row("001", kiryoso: '1', yoyaku: "MC");
        Assert.Equal((1, 0), ScNtUpstreamAccumulator.CheckReservedWord([r], 0));
    }

    [Fact]
    public void 回路要素が1以外なら対象外()
    {
        var r = Row("001", kiryoso: '2', yoyaku: "SC");
        Assert.Equal((1, 0), ScNtUpstreamAccumulator.CheckReservedWord([r], 0));
    }

    // ── 通電電流値取得(Fyss3A_Get_Tsuden) ────────────────────────────────────

    [Fact]
    public void NTは定格電流2をそのまま返す()
    {
        var r = Row("001", yoyaku: "NT", ep2A2: "00010.000");
        Assert.Equal(10.0, ScNtUpstreamAccumulator.GetEnergizingCurrent([r], 0, 1), 6);
    }

    [Fact]
    public void SC単相は静電容量を回路電圧で割る()
    {
        var r = Row("001", yoyaku: "SC", kpaph: '1', kpav0: "100", ep2Uf: "000010.0");
        Assert.Equal(0.1, ScNtUpstreamAccumulator.GetEnergizingCurrent([r], 0, 2), 6);
    }

    [Fact]
    public void SC三相は静電容量を1732倍電圧で割る()
    {
        var r = Row("001", yoyaku: "SC", kpaph: '3', kpav0: "100", ep2Uf: "000010.0");
        Assert.Equal(10.0 / (1.732 * 100.0), ScNtUpstreamAccumulator.GetEnergizingCurrent([r], 0, 2), 6);
    }

    [Fact]
    public void SC相数がその他なら静電容量をそのまま返す()
    {
        var r = Row("001", yoyaku: "SC", kpaph: '0', kpav0: "100", ep2Uf: "000010.0");
        Assert.Equal(10.0, ScNtUpstreamAccumulator.GetEnergizingCurrent([r], 0, 2), 6);
    }

    [Fact]
    public void SC静電容量が0なら定格容量から変換する()
    {
        // UF = (KVAR*1000)/(2*3.14*HZ*V^2*0.000001)
        // = 10000 / (2*3.14*60*40000*0.000001) = 10000 / 15.072 = 663.481953
        var r = Row("001", yoyaku: "SC", kpaph: '0', kpav0: "200", kpahz: "60",
            ep2Uf: "00000000", ep2Kvar: "010.00");
        Assert.Equal(663.482, ScNtUpstreamAccumulator.GetEnergizingCurrent([r], 0, 2), 3);
    }

    [Fact]
    public void SCの1次側がMCならSC1次電流に換算する()
    {
        // UF = 2*3.14159*60*10*200^2*1E-6 = 150.79632(相数=0 のためそのまま返す)
        var mc = Row("001", yoyaku: "MC");
        var sc = Row("002", oyatno: "001", yoyaku: "SC", kpaph: '0', kpav0: "200", kpahz: "60",
            ep2Uf: "000010.0");
        Assert.Equal(150.796, ScNtUpstreamAccumulator.GetEnergizingCurrent([mc, sc], 1, 2), 3);
    }

    [Fact]
    public void 先頭要素はMCチェックをスキップする()
    {
        // index==0 は syu[-1] 参照を回避するため MC チェックせず SC1次電流換算しない。
        var sc = Row("001", yoyaku: "SC", kpaph: '0', kpav0: "200", kpahz: "60", ep2Uf: "000010.0");
        Assert.Equal(10.0, ScNtUpstreamAccumulator.GetEnergizingCurrent([sc], 0, 2), 6);
    }

    // ── 積算処理(Fyss3A_Prc_Seksan) ──────────────────────────────────────────

    [Fact]
    public void 積上区分1まで上流へ通電電流値をセットする()
    {
        var top = Row("001", oyatno: "000", jagekbn: '1');
        var mid = Row("002", oyatno: "001", jagekbn: ' ');
        var sc = Row("003", oyatno: "002", jagekbn: ' ');
        var mains = new[] { top, mid, sc };

        ScNtUpstreamAccumulator.ProcessAccumulation(mains, 2, 2, 5.0);

        Assert.Equal("00005.00", sc.Data.EnergizingCurrent);
        Assert.Equal("00005.00", mid.Data.EnergizingCurrent);
        Assert.Equal("00005.00", top.Data.EnergizingCurrent);
    }

    [Fact]
    public void NTは使用相にNをセットする()
    {
        var top = Row("001", oyatno: "000", jagekbn: '1');
        ScNtUpstreamAccumulator.ProcessAccumulation([top], 0, 1, 3.0);
        Assert.Equal("N   ", top.Data.UsedPhase);
    }

    [Fact]
    public void SCは使用相をセットしない()
    {
        var top = Row("001", oyatno: "000", jagekbn: '1');
        ScNtUpstreamAccumulator.ProcessAccumulation([top], 0, 2, 3.0);
        Assert.Equal(string.Empty, top.Data.UsedPhase);
    }

    [Fact]
    public void 親データ追番が0なら積算を打ち切る()
    {
        var only = Row("001", oyatno: "000", jagekbn: ' ');
        var other = Row("002", oyatno: "000", jagekbn: ' ', denryu: "99999.99");
        var mains = new[] { only, other };

        ScNtUpstreamAccumulator.ProcessAccumulation(mains, 0, 2, 3.0);

        Assert.Equal("00003.00", only.Data.EnergizingCurrent);
        Assert.Equal("99999.99", other.Data.EnergizingCurrent); // 他要素は不変
    }

    // ── オーケストレータ(Fyss3A_SC_NT_Sekisan) ───────────────────────────────

    [Fact]
    public void SC要素の通電電流値が上流まで積算される()
    {
        var top = Row("001", oyatno: "000", kiryoso: '1', yoyaku: "P", jagekbn: '1');
        var sc = Row("002", oyatno: "001", kiryoso: '1', yoyaku: "SC", jagekbn: ' ',
            kpaph: '1', kpav0: "100", ep2Uf: "000010.0");
        var mains = new[] { top, sc };

        ScNtUpstreamAccumulator.AccumulateScNt(mains);

        Assert.Equal("00000.10", sc.Data.EnergizingCurrent);
        Assert.Equal("00000.10", top.Data.EnergizingCurrent);
    }

    [Fact]
    public void 負荷名称が0KWのSCは積算対象外()
    {
        var top = Row("001", oyatno: "000", kiryoso: '1', yoyaku: "P", jagekbn: '1', denryu: "12345.67");
        var sc = Row("002", oyatno: "001", kiryoso: '1', yoyaku: "SC", jagekbn: ' ',
            kpaph: '1', kpav0: "100", ep2Uf: "000010.0", fpaln1: "0KW", denryu: "88888.88");
        var mains = new[] { top, sc };

        ScNtUpstreamAccumulator.AccumulateScNt(mains);

        Assert.Equal("88888.88", sc.Data.EnergizingCurrent); // 不変
        Assert.Equal("12345.67", top.Data.EnergizingCurrent); // 不変
    }

    [Fact]
    public void SCNT以外の要素は積算対象外()
    {
        var mc = Row("001", oyatno: "000", kiryoso: '1', yoyaku: "MC", jagekbn: '1', denryu: "55555.55");
        var mains = new[] { mc };

        ScNtUpstreamAccumulator.AccumulateScNt(mains);

        Assert.Equal("55555.55", mc.Data.EnergizingCurrent); // 不変
    }
}
