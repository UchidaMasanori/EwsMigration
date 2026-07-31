using System.Collections.Generic;
using System.Globalization;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CurrentParameterSetter"/>(【C原典】Fyss3G_Set_MCB / Set_ELB /
/// Set_MMCB / Set_ELMB / Check_fyrt800 / Set_IM / PropSetELBKando / Fysk0e_SetELBkando)の単体テスト。
/// </summary>
public class CurrentParameterSetterTests
{
    /// <summary>AT/AF/A1/A2 の整形済みゼロ("00000.000")。ep[0] の「未入力」を表す。</summary>
    private const string FormattedZero9 = "00000.000";

    /// <summary>主回路データを 1 件生成する。</summary>
    private static MainCircuitResult Rec(
        string reservedWord = "MCB",
        string parent = "000",
        string lineTypeCode = "",
        char leadingFlag = '1',
        double setCurrent = 0.0,
        string energizingCurrent = "00000000",
        char loadSource = ' ',
        char phaseCount = '3',
        string voltage = "000")
    {
        var r = new MainCircuitResult { SequenceNumber = "001" };
        r.Data.ReservedWord = reservedWord;
        r.Data.ParentSequenceNumber = parent;
        r.Data.LineTypeCode = lineTypeCode;
        r.Data.EnergizingCurrent = energizingCurrent;
        r.Data.LoadSourceKind = loadSource;
        r.Data.CircuitPhaseCount = phaseCount;
        r.Data.CircuitVoltage[0] = voltage;
        r.Work.LeadingEquipmentFlag = leadingFlag;
        r.Work.SetCurrent = setCurrent;
        return r;
    }

    /// <summary>Format9 相当(C の sprintf("%09.3lf") + 先頭 9 桁 memcpy)。</summary>
    private static string Fmt9(double v)
    {
        string s = v.ToString("F3", CultureInfo.InvariantCulture);
        if (s.Length < 9)
        {
            s = s.PadLeft(9, '0');
        }
        return s.Length >= 9 ? s[..9] : s;
    }

    // ---- Check_fyrt800(ComputeParameterFlags) ----

    [Fact]
    public void ComputeParameterFlags_入力が全て未設定なら設定不要()
    {
        var row = Rec();
        ElectricalParameters ep0 = row.Data.ElectricalParameterSlots[0];
        ep0.At = FormattedZero9;
        ep0.Af = FormattedZero9;
        ep0.A1 = FormattedZero9;
        ep0.A2 = FormattedZero9;
        ep0.W1 = "0000000.00";

        CurrentParameterSetter.ComputeParameterFlags(row, out int prm1, out int prm2);

        Assert.Equal(1, prm1);
        Assert.Equal(1, prm2);
    }

    [Fact]
    public void ComputeParameterFlags_ATに入力があれば設定要()
    {
        var row = Rec();
        ElectricalParameters ep0 = row.Data.ElectricalParameterSlots[0];
        ep0.At = "00050.000";
        ep0.Af = FormattedZero9;
        ep0.A1 = FormattedZero9;
        ep0.A2 = FormattedZero9;
        ep0.W1 = "0000000.00";

        CurrentParameterSetter.ComputeParameterFlags(row, out int prm1, out int prm2);

        Assert.Equal(0, prm1);
        Assert.Equal(1, prm2); // ahassei != '1'
    }

    [Fact]
    public void ComputeParameterFlags_設定要かつ負荷発生元ならprm2も設定要()
    {
        var row = Rec(loadSource: '1');
        ElectricalParameters ep0 = row.Data.ElectricalParameterSlots[0];
        ep0.At = "00050.000";
        ep0.Af = FormattedZero9;
        ep0.A1 = FormattedZero9;
        ep0.A2 = FormattedZero9;
        ep0.W1 = "0000000.00";

        CurrentParameterSetter.ComputeParameterFlags(row, out int prm1, out int prm2);

        Assert.Equal(0, prm1);
        Assert.Equal(0, prm2);
    }

    // ---- Set_MCB ----

    [Fact]
    public void SetMcb_通電電流からep2のATとAFを設定する()
    {
        var row = Rec(energizingCurrent: "00000150");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetMcb(1, records, 0, 1);

        Assert.Equal("00150.000", row.Data.ElectricalParameterSlots[2].At);
        Assert.Equal("00150.000", row.Data.ElectricalParameterSlots[2].Af);
    }

    [Fact]
    public void SetMcb_設定電流があればそれをATに使う()
    {
        var row = Rec(setCurrent: 30.0, energizingCurrent: "00000150");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetMcb(1, records, 0, 1);

        Assert.Equal("00030.000", row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void SetMcb_先頭機器フラグ不一致なら何もしない()
    {
        // inpflg==1 かつ sentflg!='1' → ShouldSet=false。
        var row = Rec(leadingFlag: ' ', energizingCurrent: "00000150");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetMcb(1, records, 0, 1);

        // 既定("000000000")のまま。
        Assert.Equal("000000000", row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void SetMcb_prm1が0のときep0のAFからep1のATを生成する()
    {
        var row = Rec(energizingCurrent: "00000100");
        ElectricalParameters ep0 = row.Data.ElectricalParameterSlots[0];
        ep0.At = FormattedZero9;   // 未入力
        ep0.Af = "00050.000";      // AF 入力あり
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetMcb(0, records, 0, 1);

        Assert.Equal("00050.000", row.Data.ElectricalParameterSlots[1].At);
        Assert.Equal("00100.000", row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void SetMcb_HPSBはAFからメーカー定格AMを設定する()
    {
        var row = Rec(reservedWord: "HPSB", energizingCurrent: "00000100");
        row.Data.DataType[0] = "AM     ";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetMcb(1, records, 0, 1);

        // 通電電流 100 → dwork=100 → AM="100"。
        Assert.Equal("100", row.Data.ElectricalParameterSlots[2].Am);
    }

    [Fact]
    public void SetMcb_HPSBで設定電流ありのときAMは0になる_dwork誤記バグ再現()
    {
        // setteii!=0 経路では dwork が代入されず(C原典 == 誤記で no-op)、AM は 0 由来の "000"。
        var row = Rec(reservedWord: "HPSB", setCurrent: 30.0, energizingCurrent: "00000100");
        row.Data.DataType[0] = "AM     ";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetMcb(1, records, 0, 1);

        Assert.Equal("00030.000", row.Data.ElectricalParameterSlots[2].At);
        Assert.Equal("000", row.Data.ElectricalParameterSlots[2].Am);
    }

    // ---- Set_ELB ----

    [Fact]
    public void SetElb_動力回路60AF以下は感度電流0030を設定する()
    {
        var parent = Rec(reservedWord: "P", lineTypeCode: "P", phaseCount: '3');
        var elb = Rec(reservedWord: "ELB", parent: "001", energizingCurrent: "00000030");
        var records = new List<MainCircuitResult> { parent, elb };

        CurrentParameterSetter.SetElb(1, records, 1, 1);

        Assert.Equal("00030.000", elb.Data.ElectricalParameterSlots[2].Af);
        Assert.Equal("0030", elb.Data.ElectricalParameterSlots[2].Ma[0]);
    }

    [Fact]
    public void SetElb_EVタイプは高感度0015を設定する()
    {
        var parent = Rec(reservedWord: "P", lineTypeCode: "P", phaseCount: '3');
        var elb = Rec(reservedWord: "ELB", parent: "001", energizingCurrent: "00000030");
        elb.Data.DataType[1] = "EV ";
        var records = new List<MainCircuitResult> { parent, elb };

        CurrentParameterSetter.SetElb(1, records, 1, 1);

        Assert.Equal("0015", elb.Data.ElectricalParameterSlots[2].Ma[0]);
    }

    [Fact]
    public void SetElb_動力回路100AF超過は0200を設定する()
    {
        var parent = Rec(reservedWord: "P", lineTypeCode: "P", phaseCount: '3');
        var elb = Rec(reservedWord: "ELB", parent: "001", energizingCurrent: "00000150");
        var records = new List<MainCircuitResult> { parent, elb };

        CurrentParameterSetter.SetElb(1, records, 1, 1);

        Assert.Equal("0200", elb.Data.ElectricalParameterSlots[2].Ma[0]);
    }

    [Fact]
    public void SetElb_親P行が無ければ感度電流は設定しない()
    {
        // 親追番 000 → 親 P 行なし。NULL 参照回避で MA は既定のまま。
        var elb = Rec(reservedWord: "ELB", parent: "000", energizingCurrent: "00000030");
        var records = new List<MainCircuitResult> { elb };

        CurrentParameterSetter.SetElb(1, records, 0, 1);

        Assert.Equal("0000", elb.Data.ElectricalParameterSlots[2].Ma[0]);
    }

    [Fact]
    public void SetElb_負荷発生元はep1をep2へ再設定する()
    {
        var parent = Rec(reservedWord: "P", lineTypeCode: "P", phaseCount: '1');
        var elb = Rec(reservedWord: "ELB", parent: "001", energizingCurrent: "00000080", loadSource: '1');
        ElectricalParameters ep0 = elb.Data.ElectricalParameterSlots[0];
        ep0.At = FormattedZero9;
        ep0.Af = "00080.000";
        var records = new List<MainCircuitResult> { parent, elb };

        CurrentParameterSetter.SetElb(0, records, 1, 1);

        ElectricalParameters ep1 = elb.Data.ElectricalParameterSlots[1];
        ElectricalParameters ep2 = elb.Data.ElectricalParameterSlots[2];
        Assert.Equal(ep1.At, ep2.At);
        Assert.Equal(ep1.Af, ep2.Af);
        Assert.Equal(ep1.Ma[0], ep2.Ma[0]);
    }

    // ---- Set_MMCB ----

    [Fact]
    public void SetMmcb_通電電流からep2のATとAFを設定する()
    {
        var row = Rec(reservedWord: "MMCB", energizingCurrent: "00000030");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetMmcb(1, records, 0, 1);

        Assert.Equal("00030.000", row.Data.ElectricalParameterSlots[2].At);
        Assert.Equal("00030.000", row.Data.ElectricalParameterSlots[2].Af);
    }

    [Fact]
    public void SetMmcb_負荷容量W1があればSet_IMで電動機電流をep1のATに設定する()
    {
        var row = Rec(reservedWord: "MMCB", energizingCurrent: "00000000", phaseCount: '3', voltage: "200");
        ElectricalParameters ep0 = row.Data.ElectricalParameterSlots[0];
        ep0.At = FormattedZero9;
        ep0.W1 = "0000100.00";  // W1 入力あり(!= "0000000.00")
        row.Data.ElectricalParameterSlots[1].W1 = "0000100.00"; // Set_IM は ep[1].W1 を参照
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetMmcb(0, records, 0, 1);

        // 三相・電圧200(<=220)・W1=100(<1000) → denryu = pow(0.1,0.945)*(6.0-0.16)。
        double w1 = 100.0;
        double expected = System.Math.Pow(w1 / 1000.0, 0.945) * (6.0 - 1.6 * w1 / 1000.0);
        Assert.Equal(Fmt9(expected), row.Data.ElectricalParameterSlots[1].At);
    }

    [Fact]
    public void SetMmcb_NHMBはAFを設定しない()
    {
        var row = Rec(reservedWord: "NHMB", energizingCurrent: "00000000");
        ElectricalParameters ep0 = row.Data.ElectricalParameterSlots[0];
        ep0.At = FormattedZero9;
        ep0.Af = "00050.000";
        ep0.W1 = "0000000.00"; // W1 未入力(整形済みゼロ)。
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetMmcb(0, records, 0, 1);

        // yoyaku=="NHMB" のため AF 設定ブロックはスキップ。ep[1].Af は既定のまま。
        Assert.Equal("000000000", row.Data.ElectricalParameterSlots[1].Af);
    }

    // ---- Set_ELMB ----

    [Fact]
    public void SetElmb_ep2にATとAFと感度電流を設定する()
    {
        var parent = Rec(reservedWord: "P", lineTypeCode: "P", phaseCount: '3');
        var elmb = Rec(reservedWord: "ELMB", parent: "001", energizingCurrent: "00000040");
        var records = new List<MainCircuitResult> { parent, elmb };

        CurrentParameterSetter.SetElmb(1, records, 1, 1);

        Assert.Equal("00040.000", elmb.Data.ElectricalParameterSlots[2].At);
        Assert.Equal("00040.000", elmb.Data.ElectricalParameterSlots[2].Af);
        Assert.Equal("0030", elmb.Data.ElectricalParameterSlots[2].Ma[0]);
    }

    [Fact]
    public void SetElmb_W1があればSet_IMでep1のATを生成する()
    {
        var parent = Rec(reservedWord: "P", lineTypeCode: "P", phaseCount: '3');
        var elmb = Rec(reservedWord: "ELMB", parent: "001", energizingCurrent: "00000040", voltage: "200");
        ElectricalParameters ep0 = elmb.Data.ElectricalParameterSlots[0];
        ep0.At = FormattedZero9;
        ep0.W1 = "0000100.00";
        elmb.Data.ElectricalParameterSlots[1].W1 = "0000100.00";
        var records = new List<MainCircuitResult> { parent, elmb };

        CurrentParameterSetter.SetElmb(0, records, 1, 1);

        double w1 = 100.0;
        double expected = System.Math.Pow(w1 / 1000.0, 0.945) * (6.0 - 1.6 * w1 / 1000.0);
        Assert.Equal(Fmt9(expected), elmb.Data.ElectricalParameterSlots[1].At);
    }
}
