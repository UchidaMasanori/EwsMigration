using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CurrentParameterSetter"/> の非ブレーカ系セッタ
/// (【C原典】Fyss3G_Set_THR / Set_MG / Set_WH, toku/sekkei/src/Fyss3G.c)の単体テスト。
/// </summary>
public class CurrentParameterSetterDeviceTests
{
    /// <summary>AT/A1/A2 の整形済みゼロ("00000.000")。ep の「未設定」を表す。</summary>
    private const string FormattedZero9 = "00000.000";

    /// <summary>W1 の整形済みゼロ("0000000.00")。</summary>
    private const string FormattedZero10 = "0000000.00";

    /// <summary>主回路データを 1 件生成する。denryu は "%08.2f"(8桁)形式。</summary>
    private static MainCircuitResult Rec(
        char circuitElement = '1',
        char leadingFlag = '1',
        string energizingCurrent = "00000.00",
        char loadSource = ' ',
        char phaseCount = '3',
        string voltage = "000",
        string loadKind = "")
    {
        var r = new MainCircuitResult { SequenceNumber = "001" };
        r.Data.CircuitElement = circuitElement;
        r.Data.EnergizingCurrent = energizingCurrent;
        r.Data.LoadSourceKind = loadSource;
        r.Data.CircuitPhaseCount = phaseCount;
        r.Data.CircuitVoltage[0] = voltage;
        r.Data.AttachedParameter.LoadKind = loadKind;
        r.Work.LeadingEquipmentFlag = leadingFlag;
        return r;
    }

    // ==== Set_THR ====

    [Fact]
    public void SetThr_主回路は通電電流からep2のATを設定する()
    {
        var row = Rec(circuitElement: '1', energizingCurrent: "00150.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetThr(1, records, 0, 1);

        // memcpy("00000.000")後に denryu(8桁)を上書き→末尾に '0' が残り "00150.000"。
        Assert.Equal("00150.000", row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void SetThr_計器回路はep2のATを設定しない()
    {
        var row = Rec(circuitElement: ' ', energizingCurrent: "00150.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetThr(1, records, 0, 1);

        Assert.Equal(new string('0', 9), row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void SetThr_先頭機器フラグ不一致なら何もしない()
    {
        // inpflg==1 かつ sentflg!='1' → ShouldSet=false。
        var row = Rec(circuitElement: '1', leadingFlag: ' ', energizingCurrent: "00150.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetThr(1, records, 0, 1);

        Assert.Equal(new string('0', 9), row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void SetThr_W1ありでSet_IMからep1のATを算出する()
    {
        // kpaph='1' → loadKind=2(ヒータ), kpav=100, W1=1500 → 1500/100 = 15.0。
        var row = Rec(circuitElement: ' ', phaseCount: '1', voltage: "100");
        row.Data.ElectricalParameterSlots[0].At = FormattedZero9; // ep[0].AT==0
        row.Data.ElectricalParameterSlots[0].W1 = "0001500.00";   // ep[0].W1!=0
        row.Data.ElectricalParameterSlots[1].W1 = "0001500.00";   // Set_IM 入力
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetThr(0, records, 0, 1);

        Assert.Equal("00015.000", row.Data.ElectricalParameterSlots[1].At);
    }

    [Fact]
    public void SetThr_負荷発生元でep1のATをep2へコピーする()
    {
        var row = Rec(circuitElement: ' ', loadSource: '1');
        row.Data.ElectricalParameterSlots[0].At = "00050.000"; // ep[0].AT!=0 → W1ブロックskip
        row.Data.ElectricalParameterSlots[1].At = "00099.000";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetThr(0, records, 0, 1);

        Assert.Equal("00099.000", row.Data.ElectricalParameterSlots[2].At);
    }

    // ==== Set_MG ====

    [Fact]
    public void SetMg_ep2にATとA2を設定する_係数1()
    {
        var row = Rec(energizingCurrent: "00150.00");
        var records = new List<MainCircuitResult> { row };
        var a2set = new List<RatedCurrent2Setting>(); // 空 → 係数 1.0

        CurrentParameterSetter.SetMg(1, records, 0, a2set, 1);

        Assert.Equal("00150.000", row.Data.ElectricalParameterSlots[2].At);
        Assert.Equal("00150.000", row.Data.ElectricalParameterSlots[2].A2); // 150 * 1.0
    }

    [Fact]
    public void SetMg_A2SET係数でA2を算出する()
    {
        // records[0].wk.kikiskbn=='1' + 負荷種類一致 + 電圧超過 + 全相 → 係数 2.0。
        var row = Rec(energizingCurrent: "00150.00", phaseCount: '3', voltage: "200", loadKind: "M ");
        row.Work.EquipmentSelectionKind = '1';
        var records = new List<MainCircuitResult> { row };
        var a2set = new List<RatedCurrent2Setting>
        {
            new("M ", '\0', 1000, 2.0),
        };

        CurrentParameterSetter.SetMg(1, records, 0, a2set, 1);

        Assert.Equal("00300.000", row.Data.ElectricalParameterSlots[2].A2); // 150 * 2.0
    }

    [Fact]
    public void SetMg_param1_W1ゼロならep2からコピーする()
    {
        var row = Rec(energizingCurrent: "00150.00");
        row.Data.ElectricalParameterSlots[1].At = FormattedZero9;
        row.Data.ElectricalParameterSlots[1].A2 = FormattedZero9;
        row.Data.ElectricalParameterSlots[1].W1 = FormattedZero10;
        var records = new List<MainCircuitResult> { row };
        var a2set = new List<RatedCurrent2Setting>();

        CurrentParameterSetter.SetMg(0, records, 0, a2set, 1);

        Assert.Equal("00150.000", row.Data.ElectricalParameterSlots[1].At);
        Assert.Equal("00150.000", row.Data.ElectricalParameterSlots[1].A2);
    }

    [Fact]
    public void SetMg_param1_W1ありでSet_IMからATとA2を算出する()
    {
        var row = Rec(energizingCurrent: "00150.00", phaseCount: '1', voltage: "100");
        row.Data.ElectricalParameterSlots[1].At = FormattedZero9;
        row.Data.ElectricalParameterSlots[1].A2 = FormattedZero9;
        row.Data.ElectricalParameterSlots[1].W1 = "0001500.00";
        var records = new List<MainCircuitResult> { row };
        var a2set = new List<RatedCurrent2Setting>();

        CurrentParameterSetter.SetMg(0, records, 0, a2set, 1);

        // kpaph='1' → loadKind=2, 1500/100 = 15.0。
        Assert.Equal("00015.000", row.Data.ElectricalParameterSlots[1].At);
        Assert.Equal("00015.000", row.Data.ElectricalParameterSlots[1].A2);
    }

    [Fact]
    public void SetMg_負荷発生元でep1をep2へコピーする()
    {
        var row = Rec(energizingCurrent: "00150.00", loadSource: '1');
        row.Data.ElectricalParameterSlots[1].At = "00088.000"; // 非ゼロ → ATブロックskip
        row.Data.ElectricalParameterSlots[1].A2 = "00077.000"; // 非ゼロ → A2ブロックskip
        var records = new List<MainCircuitResult> { row };
        var a2set = new List<RatedCurrent2Setting>();

        CurrentParameterSetter.SetMg(0, records, 0, a2set, 1);

        Assert.Equal("00088.000", row.Data.ElectricalParameterSlots[2].At);
        Assert.Equal("00077.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    // ==== Set_WH ====

    [Fact]
    public void SetWh_主回路_A1初期化しA2は電流40以下で30A()
    {
        var row = Rec(circuitElement: '1', energizingCurrent: "00030.00");
        var records = new List<MainCircuitResult> { row };
        var a1set = new List<RatedCurrent1Setting>();

        CurrentParameterSetter.SetWh(1, records, 0, a1set, 1);

        Assert.Equal("00000.000", row.Data.ElectricalParameterSlots[2].A1);
        Assert.Equal("00030.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetWh_主回路_電流40超で120A()
    {
        var row = Rec(circuitElement: '1', energizingCurrent: "00050.00");
        var records = new List<MainCircuitResult> { row };
        var a1set = new List<RatedCurrent1Setting>();

        CurrentParameterSetter.SetWh(1, records, 0, a1set, 1);

        Assert.Equal("00120.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetWh_計器回路_A1はA1SET検索A2は5固定()
    {
        var row = Rec(circuitElement: ' ', energizingCurrent: "00030.00");
        var records = new List<MainCircuitResult> { row };
        var a1set = new List<RatedCurrent1Setting>
        {
            new(15.0),
            new(30.0),
            new(50.0),
        };

        CurrentParameterSetter.SetWh(1, records, 0, a1set, 1);

        // key=30 → 30 超の最初 = 50。
        Assert.Equal("00050.000", row.Data.ElectricalParameterSlots[2].A1);
        Assert.Equal("00005.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetWh_param1_計器回路A1が非ゼロなら再検索する()
    {
        var row = Rec(circuitElement: ' ', energizingCurrent: "00030.00");
        row.Data.ElectricalParameterSlots[1].A1 = "00099.000"; // 非ゼロ → 再検索
        var records = new List<MainCircuitResult> { row };
        var a1set = new List<RatedCurrent1Setting> { new(15.0), new(30.0), new(50.0) };

        CurrentParameterSetter.SetWh(0, records, 0, a1set, 1);

        Assert.Equal("00050.000", row.Data.ElectricalParameterSlots[1].A1);
    }

    [Fact]
    public void SetWh_param1_主回路A2はep0のA2が40以下で30A()
    {
        var row = Rec(circuitElement: '1', energizingCurrent: "00030.00");
        row.Data.ElectricalParameterSlots[0].A2 = "00035.000"; // 35<=40 → 30A
        var records = new List<MainCircuitResult> { row };
        var a1set = new List<RatedCurrent1Setting>();

        CurrentParameterSetter.SetWh(0, records, 0, a1set, 1);

        Assert.Equal("00030.000", row.Data.ElectricalParameterSlots[1].A2);
    }

    [Fact]
    public void SetWh_param1_主回路A2はep0のA2が40超150以下で120A()
    {
        var row = Rec(circuitElement: '1', energizingCurrent: "00030.00");
        row.Data.ElectricalParameterSlots[0].A2 = "00100.000"; // 40<100<=150 → 120A
        var records = new List<MainCircuitResult> { row };
        var a1set = new List<RatedCurrent1Setting>();

        CurrentParameterSetter.SetWh(0, records, 0, a1set, 1);

        Assert.Equal("00120.000", row.Data.ElectricalParameterSlots[1].A2);
    }

    [Fact]
    public void SetWh_param1_主回路A2が150超なら据え置き()
    {
        var row = Rec(circuitElement: '1', energizingCurrent: "00030.00");
        row.Data.ElectricalParameterSlots[0].A2 = "00200.000"; // >150 → else省略で据え置き
        var records = new List<MainCircuitResult> { row };
        var a1set = new List<RatedCurrent1Setting>();

        CurrentParameterSetter.SetWh(0, records, 0, a1set, 1);

        Assert.Equal(new string('0', 9), row.Data.ElectricalParameterSlots[1].A2);
    }

    [Fact]
    public void SetWh_負荷発生元でep1をep2へコピーする()
    {
        var row = Rec(circuitElement: '1', energizingCurrent: "00030.00", loadSource: '1');
        row.Data.ElectricalParameterSlots[1].A1 = "00011.000";
        var records = new List<MainCircuitResult> { row };
        var a1set = new List<RatedCurrent1Setting>();

        CurrentParameterSetter.SetWh(0, records, 0, a1set, 1);

        // 主回路のため param1 A2 は ep[0].A2(既定0) <=40 で 30A。
        Assert.Equal("00011.000", row.Data.ElectricalParameterSlots[2].A1);
        Assert.Equal("00030.000", row.Data.ElectricalParameterSlots[2].A2);
    }
}
