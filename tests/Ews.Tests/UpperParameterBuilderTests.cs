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

    // ---- Find_Parent ---------------------------------------------------------

    /// <summary>datano/oyatno/kpa* を持つ主回路レコードを組み立てる。</summary>
    private static MainCircuitResult Rec(
        string datano, string oyatno, char ph, char wr, char p, char vkbn,
        string v0, string v1, string v2, string yoyaku, char kiryoso = '1')
        => new()
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ParentSequenceNumber = oyatno,
                CircuitPhaseCount = ph,
                CircuitWireType = wr,
                CircuitPoleCount = p,
                CircuitVoltageKind = vkbn,
                CircuitVoltage = [v0, v1, v2],
                ReservedWord = yoyaku,
                CircuitElement = kiryoso,
            },
        };

    [Fact]
    public void FindParent_親のkpaを取り出して右詰めする()
    {
        var records = new List<MainCircuitResult>
        {
            Rec("001", "000", '1', '3', '3', 'A', "210", "105", "000", "P"),
            Rec("002", "001", '0', '0', '0', 'A', "000", "000", "000", "MCB"),
        };
        var output = new MainCircuitParameter();

        bool found = UpperParameterBuilder.FindParent(records, 1, output);

        Assert.True(found);
        Assert.Equal(1, output.Phase);
        Assert.Equal(3, output.WireType);
        Assert.Equal(3, output.Pole);
        Assert.Equal(0, output.AcDcKind);
        Assert.Equal(new short[] { 0, 210, 105 }, output.Voltage);
    }

    [Fact]
    public void FindParent_親追番が一致しなければfalse()
    {
        var records = new List<MainCircuitResult>
        {
            Rec("001", "000", '1', '3', '3', 'A', "210", "105", "000", "P"),
            Rec("002", "999", '0', '0', '0', 'A', "000", "000", "000", "MCB"),
        };
        var output = new MainCircuitParameter();

        Assert.False(UpperParameterBuilder.FindParent(records, 1, output));
    }

    [Fact]
    public void FindParent_VTでもFでもなく回路要素4なら電圧110()
    {
        var records = new List<MainCircuitResult>
        {
            Rec("001", "000", '3', '3', '3', 'A', "210", "000", "000", "P"),
            Rec("002", "001", '0', '0', '0', 'A', "000", "000", "000", "AM", '4'),
        };
        var output = new MainCircuitParameter();

        Assert.True(UpperParameterBuilder.FindParent(records, 1, output));
        Assert.Equal(new short[] { 0, 0, 110 }, output.Voltage); // 110/0/0 → 右詰め
    }

    // ---- Make_UpperParm(統括ループ core) ------------------------------------

    private static MainCircuitResult MakeRec(string datano, string oyatno, string yoyaku, ElectricalParameters ep)
    {
        var data = new MainCircuitData
        {
            SystemKind = '1',
            ReservedWord = yoyaku,
            ParentSequenceNumber = oyatno,
        };
        data.ElectricalParameterSlots[0] = ep;
        return new MainCircuitResult { SequenceNumber = datano, Data = data };
    }

    [Fact]
    public void GenerateUpperParameters_入線と子機器のkpaを一貫生成する()
    {
        var records = new List<MainCircuitResult>
        {
            MakeRec("001", "000", "P", MakeEp("1", "3", "003", 'A', 210, 105, 0)),
            MakeRec("002", "001", "MCB", MakeEp("3", "3", "003", 'A', 0, 0, 0)),
        };

        UpperParameterBuilder.GenerateUpperParameters(records, UpperParameterBuilder.Hz1);

        // 入線 kpa*(決定的)
        MainCircuitData p = records[0].Data;
        Assert.Equal('1', p.CircuitPhaseCount);
        Assert.Equal('3', p.CircuitWireType);
        Assert.Equal("50", p.CircuitFrequency);
        Assert.Equal('3', p.CircuitPoleCount);
        Assert.Equal('A', p.CircuitVoltageKind);
        Assert.Equal("210", p.CircuitVoltage[0]);
        Assert.Equal("105", p.CircuitVoltage[1]);
        Assert.Equal("000", p.CircuitVoltage[2]);

        // 子機器: Find_Parent→Kairo_Parm_Set→Kairo_End_Set が走り周波数・AC区分が適用される
        MainCircuitData c = records[1].Data;
        Assert.Equal("50", c.CircuitFrequency);
        Assert.Equal('A', c.CircuitVoltageKind);
    }

    [Fact]
    public void GenerateUpperParameters_P系統以外はスキップする()
    {
        MainCircuitResult rec = MakeRec("001", "000", "P", MakeEp("1", "3", "003", 'A', 210, 105, 0));
        rec.Data.SystemKind = '2'; // SP系統(P系統でない)
        var records = new List<MainCircuitResult> { rec };

        UpperParameterBuilder.GenerateUpperParameters(records, UpperParameterBuilder.Hz1);

        Assert.Equal('0', rec.Data.CircuitPhaseCount); // 未処理(既定のまま)
        Assert.Equal("00", rec.Data.CircuitFrequency);
    }
}


