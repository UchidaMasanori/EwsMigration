using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CurrentParameterSetter"/> のリーフセッタ群
/// (【C原典】Fyss3G_Set_CON / Set_MCDT / Set_F / Set_ELR / Set_LGR / Set_TS /
///  Set_SU / Set_SSW / Set_CKS / Set_L, toku/sekkei/src/Fyss3G.c)の単体テスト。
/// </summary>
public class CurrentParameterSetterLeafTests
{
    /// <summary>AT/A1/A2 の未設定を表す整形前ゼロ("000000000")。</summary>
    private static readonly string RawZero9 = new('0', 9);

    /// <summary>主回路データを 1 件生成する。denryu は "%08.2f"(8桁)形式。</summary>
    private static MainCircuitResult Rec(
        char leadingFlag = '1',
        string energizingCurrent = "00000.00",
        double setCurrent = 0.0)
    {
        var r = new MainCircuitResult { SequenceNumber = "001" };
        r.Data.EnergizingCurrent = energizingCurrent;
        r.Work.LeadingEquipmentFlag = leadingFlag;
        r.Work.SetCurrent = setCurrent;
        return r;
    }

    // ==== Set_CON ====

    [Fact]
    public void SetCon_通電電流からep2のA2を設定する()
    {
        var row = Rec(energizingCurrent: "00025.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetCon(records, 0, 1);

        Assert.Equal("00025.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetCon_先頭機器フラグ不一致なら何もしない()
    {
        var row = Rec(leadingFlag: ' ', energizingCurrent: "00025.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetCon(records, 0, 1);

        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].A2);
    }

    // ==== Set_MCDT ====

    [Fact]
    public void SetMcdt_通電電流の1_25倍をep2のA2に設定する()
    {
        var row = Rec(energizingCurrent: "00100.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetMcdt(records, 0, 1);

        // 100 * 1.25 = 125.0。
        Assert.Equal("00125.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    // ==== Set_F ====

    [Fact]
    public void SetF_通電電流3A未満は3Aを設定する()
    {
        var row = Rec(energizingCurrent: "00002.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetF(records, 0, 1);

        Assert.Equal("00003.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetF_通電電流3A以上はそのまま設定する()
    {
        var row = Rec(energizingCurrent: "00010.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetF(records, 0, 1);

        Assert.Equal("00010.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    // ==== Set_ELR ====

    [Fact]
    public void SetElr_100A以下は30mAを設定する()
    {
        var row = Rec(energizingCurrent: "00050.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetElr(records, 0, 1);

        Assert.Equal("0030", row.Data.ElectricalParameterSlots[2].Ma[0]);
    }

    [Fact]
    public void SetElr_100A超は200mAを設定する()
    {
        var row = Rec(energizingCurrent: "00150.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetElr(records, 0, 1);

        Assert.Equal("0200", row.Data.ElectricalParameterSlots[2].Ma[0]);
    }

    [Fact]
    public void SetElr_ep0のMAが設定済みなら何もしない()
    {
        var row = Rec(energizingCurrent: "00050.00");
        row.Data.ElectricalParameterSlots[0].Ma[0] = "0100";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetElr(records, 0, 1);

        Assert.Equal("0000", row.Data.ElectricalParameterSlots[2].Ma[0]);
    }

    // ==== Set_LGR ====

    [Fact]
    public void SetLgr_ep0のMA未設定なら200mAを設定する()
    {
        var row = Rec(energizingCurrent: "00050.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetLgr(records, 0, 1);

        Assert.Equal("0200", row.Data.ElectricalParameterSlots[2].Ma[0]);
    }

    [Fact]
    public void SetLgr_ep0のMAが設定済みなら何もしない()
    {
        var row = Rec(energizingCurrent: "00050.00");
        row.Data.ElectricalParameterSlots[0].Ma[0] = "0100";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetLgr(records, 0, 1);

        Assert.Equal("0000", row.Data.ElectricalParameterSlots[2].Ma[0]);
    }

    // ==== Set_TS ====

    [Fact]
    public void SetTs_ep2のA2に15Aを設定する()
    {
        var row = Rec();
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetTs(1, records, 0, 1);

        Assert.Equal("00015.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetTs_prm1がonのときep1のA2にep0のA2をコピーする()
    {
        var row = Rec();
        row.Data.ElectricalParameterSlots[0].A2 = "00007.000";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetTs(0, records, 0, 1);

        Assert.Equal("00007.000", row.Data.ElectricalParameterSlots[1].A2);
    }

    // ==== Set_SU ====

    [Fact]
    public void SetSu_ep2のA2に1_5Aを設定する()
    {
        var row = Rec();
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetSu(1, records, 0, 1);

        Assert.Equal("00001.500", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetSu_prm1がonのときep1のA2にep0のA2をコピーする()
    {
        var row = Rec();
        row.Data.ElectricalParameterSlots[0].A2 = "00001.500";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetSu(0, records, 0, 1);

        Assert.Equal("00001.500", row.Data.ElectricalParameterSlots[1].A2);
    }

    // ==== Set_SSW ====

    [Fact]
    public void SetSsw_通電電流からep2のA2を設定する()
    {
        var row = Rec(energizingCurrent: "00063.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetSsw(records, 0, 1);

        Assert.Equal("00063.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    // ==== Set_CKS ====

    [Fact]
    public void SetCks_設定電流があればその値をep2のA2に設定する()
    {
        var row = Rec(energizingCurrent: "00033.00", setCurrent: 20.0);
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetCks(0, records, 0, 1);

        Assert.Equal("00020.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetCks_設定電流が0なら通電電流をep2のA2に設定する()
    {
        var row = Rec(energizingCurrent: "00033.00", setCurrent: 0.0);
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetCks(0, records, 0, 1);

        Assert.Equal("00033.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetCks_prm2がonなら何もしない()
    {
        var row = Rec(energizingCurrent: "00033.00", setCurrent: 20.0);
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetCks(1, records, 0, 1);

        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].A2);
    }

    // ==== Set_L ====

    [Fact]
    public void SetL_通電電流40A未満は30Aを設定する()
    {
        var row = Rec(energizingCurrent: "00030.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetL(records, 0, 1);

        Assert.Equal("00030.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetL_通電電流40A以上は60Aを設定する()
    {
        var row = Rec(energizingCurrent: "00050.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetL(records, 0, 1);

        Assert.Equal("00060.000", row.Data.ElectricalParameterSlots[2].A2);
    }
}
