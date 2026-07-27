using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="UpperParameterBuilder"/>(【C原典】Fyss14.c Kairo_Init_Take / Kairo_End_Set)の単体テスト。
/// 入線 ep[0] → 主回路パラメータ、および 主回路パラメータ → 回路電気値(kpa*)を検証する。
/// </summary>
public sealed class UpperParameterBuilderTests
{
    /// <summary>ep[0] を電圧 3 スロット(8 桁 "000NNN.0" 形式)で組み立てる。</summary>
    private static ElectricalParameters MakeEp(string ph2, string wr2, string p, char v2kbn, int v0, int v1, int v2)
    {
        var ep = new ElectricalParameters();
        ep.Ph2[0] = ph2;                       // epaph2[0]
        ep.Wr2[0] = wr2;                       // epawr2[0]
        ep.P = p;                              // epap
        ep.V2Kbn = v2kbn;                      // epav2kbn
        ep.V2[0] = v0.ToString("D6") + ".0";   // epav2[0] "000210.0"
        ep.V2[1] = v1.ToString("D6") + ".0";
        ep.V2[2] = v2.ToString("D6") + ".0";
        return ep;
    }

    // ---- Kairo_Init_Take -----------------------------------------------------

    [Fact]
    public void TakeIncomingParameter_1P3W210_105Vを取り出す()
    {
        var data = new MainCircuitData();
        data.ElectricalParameterSlots[0] = MakeEp("1", "3", "003", 'A', 210, 105, 0);

        MainCircuitParameter prm = UpperParameterBuilder.TakeIncomingParameter(data);

        Assert.Equal(1, prm.Phase);
        Assert.Equal(3, prm.WireType);
        Assert.Equal(3, prm.Pole);
        Assert.Equal(0, prm.AcDcKind);
        Assert.Equal(new short[] { 0, 210, 105 }, prm.Voltage); // 右詰め
    }

    [Fact]
    public void TakeIncomingParameter_105V単相2線は極数1の特例()
    {
        var data = new MainCircuitData();
        data.ElectricalParameterSlots[0] = MakeEp("1", "2", "002", 'A', 105, 0, 0);

        MainCircuitParameter prm = UpperParameterBuilder.TakeIncomingParameter(data);

        Assert.Equal(1, prm.Phase);
        Assert.Equal(2, prm.WireType);
        Assert.Equal(1, prm.Pole); // 105V・単相2線 → 極数1
        Assert.Equal(new short[] { 0, 0, 105 }, prm.Voltage);
    }

    [Fact]
    public void TakeIncomingParameter_DC区分はvkbn1になる()
    {
        var data = new MainCircuitData();
        data.ElectricalParameterSlots[0] = MakeEp("0", "0", "002", 'D', 100, 0, 0);

        MainCircuitParameter prm = UpperParameterBuilder.TakeIncomingParameter(data);

        Assert.Equal(1, prm.AcDcKind); // epav2kbn!='A' → 1(DC)
    }

    // ---- Kairo_End_Set -------------------------------------------------------

    [Fact]
    public void SetCircuitInfo_ACは相線式周波数極数電圧を設定する()
    {
        var data = new MainCircuitData();
        var prm = new MainCircuitParameter { Phase = 1, WireType = 3, Pole = 3, AcDcKind = 0 };
        prm.Voltage[0] = 0;
        prm.Voltage[1] = 210;
        prm.Voltage[2] = 105;

        UpperParameterBuilder.SetCircuitInfo(data, prm, UpperParameterBuilder.Hz1);

        Assert.Equal('1', data.CircuitPhaseCount);
        Assert.Equal('3', data.CircuitWireType);
        Assert.Equal("50", data.CircuitFrequency);
        Assert.Equal('3', data.CircuitPoleCount);
        Assert.Equal('A', data.CircuitVoltageKind);
        Assert.Equal("210", data.CircuitVoltage[0]); // 左詰めして 3 桁
        Assert.Equal("105", data.CircuitVoltage[1]);
        Assert.Equal("000", data.CircuitVoltage[2]);
    }

    [Fact]
    public void SetCircuitInfo_DCは相線式周波数0極数2にする()
    {
        var data = new MainCircuitData();
        var prm = new MainCircuitParameter { Phase = 1, WireType = 2, Pole = 2, AcDcKind = 1 };
        prm.Voltage[0] = 100;
        prm.Voltage[1] = 0;
        prm.Voltage[2] = 0;

        UpperParameterBuilder.SetCircuitInfo(data, prm, UpperParameterBuilder.Hz2);

        Assert.Equal('0', data.CircuitPhaseCount);
        Assert.Equal('0', data.CircuitWireType);
        Assert.Equal("00", data.CircuitFrequency);
        Assert.Equal('2', data.CircuitPoleCount);
        Assert.Equal('D', data.CircuitVoltageKind);
        Assert.Equal("100", data.CircuitVoltage[0]); // 電圧は保持
    }

    // ---- Kairo_Init_Take → Kairo_End_Set 連結 --------------------------------

    [Fact]
    public void 入線取り出しから回路電気値設定まで一貫する()
    {
        var data = new MainCircuitData();
        data.ElectricalParameterSlots[0] = MakeEp("1", "3", "003", 'A', 210, 105, 0);

        MainCircuitParameter prm = UpperParameterBuilder.TakeIncomingParameter(data);
        UpperParameterBuilder.SetCircuitInfo(data, prm, UpperParameterBuilder.Hz1);

        // Voltage=[0,210,105] → 左詰め [210,105,0]
        Assert.Equal('1', data.CircuitPhaseCount);
        Assert.Equal('3', data.CircuitWireType);
        Assert.Equal('3', data.CircuitPoleCount);
        Assert.Equal('A', data.CircuitVoltageKind);
        Assert.Equal("210", data.CircuitVoltage[0]);
        Assert.Equal("105", data.CircuitVoltage[1]);
        Assert.Equal("000", data.CircuitVoltage[2]);
    }
}
