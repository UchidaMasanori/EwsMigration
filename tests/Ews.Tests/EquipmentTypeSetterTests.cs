using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="EquipmentTypeSetter"/>(C 原典 Type_Set / PropGetSenSou / PropSearch2PBrk)の単体テスト。
/// </summary>
public sealed class EquipmentTypeSetterTests
{
    private static MainCircuitResult Rec(
        string datano,
        string yoyaku,
        string kno = "001",
        char ksyubetu = '1',
        string gyocd = "",
        char kpaph = '1',
        char kpawr = '3',
        string oyatno = "000",
        string epap = "003",
        string epaat = "00000.000",
        string? dtype0 = null,
        string? dtype1 = null)
    {
        var r = new MainCircuitResult { SequenceNumber = datano };
        MainCircuitData d = r.Data;
        d.ReservedWord = yoyaku;
        d.SystemNumber = kno;
        d.SystemKind = ksyubetu;
        d.LineTypeCode = gyocd;
        d.CircuitPhaseCount = kpaph;
        d.CircuitWireType = kpawr;
        d.ParentSequenceNumber = oyatno;
        d.ElectricalParameterSlots[0].P = epap;
        d.ElectricalParameterSlots[0].At = epaat;
        if (dtype0 != null) d.DataType[0] = dtype0;
        if (dtype1 != null) d.DataType[1] = dtype1;
        return r;
    }

    // 系統に相数/線数を供給する P 行(1P3W)。
    private static MainCircuitResult PRow(string datano, string kno = "001", char kpaph = '1', char kpawr = '3') =>
        Rec(datano, "P", kno: kno, kpaph: kpaph, kpawr: kpawr);

    [Fact]
    public void F機器はタイプ0未設定で仕様区分01ならGTを設定する()
    {
        var f = Rec("001", "F");
        var mains = new[] { f };

        EquipmentTypeSetter.Set(mains, "01");

        Assert.Equal("GT     ", f.Data.DataType[0]);
    }

    [Fact]
    public void F機器は仕様区分01_02以外ならSTを設定する()
    {
        var f = Rec("001", "F");
        var mains = new[] { f };

        EquipmentTypeSetter.Set(mains, "99");

        Assert.Equal("ST     ", f.Data.DataType[0]);
    }

    [Fact]
    public void F機器はタイプ0設定済みなら変更しない()
    {
        var f = Rec("001", "F", dtype0: "XX     ");
        var mains = new[] { f };

        EquipmentTypeSetter.Set(mains, "01");

        Assert.Equal("XX     ", f.Data.DataType[0]);
    }

    [Fact]
    public void 主幹ELBは条件を満たすと既定NTを設定する()
    {
        // 1P3W・P系統・行種M・極数3P だが 2P ブレーカの子が無い → NT のまま
        var p = PRow("001");
        var elb = Rec("002", "ELB", gyocd: "M", epap: "003");
        var mains = new[] { p, elb };

        EquipmentTypeSetter.Set(mains, "01");

        Assert.Equal("NT     ", elb.Data.DataType[1]);
    }

    [Fact]
    public void 主幹ELBは1P_2Pブレーカの子がありATが600以下ならTLAを設定する()
    {
        var p = PRow("001");
        var elb = Rec("002", "ELB", gyocd: "M", epap: "003", epaat: "00400.000", oyatno: "000");
        // ELB(datano=002)の子=oyatno 002、2P ブレーカ(MCB, 極数002)
        var child = Rec("003", "MCB", gyocd: "", epap: "002", oyatno: "002");
        var mains = new[] { p, elb, child };

        EquipmentTypeSetter.Set(mains, "01");

        Assert.Equal("TLA    ", elb.Data.DataType[1]);
    }

    [Fact]
    public void 主幹ELBは子ブレーカがあってもATが600超ならTLAにしない()
    {
        var p = PRow("001");
        var elb = Rec("002", "ELB", gyocd: "M", epap: "003", epaat: "00601.000", oyatno: "000");
        var child = Rec("003", "MCB", gyocd: "", epap: "002", oyatno: "002");
        var mains = new[] { p, elb, child };

        EquipmentTypeSetter.Set(mains, "01");

        Assert.Equal("NT     ", elb.Data.DataType[1]);   // 600A 超 → NT のまま
    }

    [Fact]
    public void 相数線数が1P3Wでも3P4Wでもなければ対象外でNTのまま()
    {
        // P 行が 2P2W 相当 → 条件外
        var p = PRow("001", kpaph: '2', kpawr: '2');
        var elb = Rec("002", "ELB", gyocd: "M", epap: "003", oyatno: "000");
        var child = Rec("003", "MCB", gyocd: "", epap: "002", oyatno: "002");
        var mains = new[] { p, elb, child };

        EquipmentTypeSetter.Set(mains, "01");

        Assert.Equal("NT     ", elb.Data.DataType[1]);
    }

    [Fact]
    public void P系統でなければ対象外でNTのまま()
    {
        var p = PRow("001");
        var elb = Rec("002", "ELB", ksyubetu: '0', gyocd: "M", epap: "003", oyatno: "000");
        var child = Rec("003", "MCB", gyocd: "", epap: "002", oyatno: "002");
        var mains = new[] { p, elb, child };

        EquipmentTypeSetter.Set(mains, "01");

        Assert.Equal("NT     ", elb.Data.DataType[1]);
    }

    [Fact]
    public void 対象外の行種はNTのまま()
    {
        var p = PRow("001");
        var elb = Rec("002", "ELB", gyocd: "B", epap: "003", oyatno: "000");
        var child = Rec("003", "MCB", gyocd: "", epap: "002", oyatno: "002");
        var mains = new[] { p, elb, child };

        EquipmentTypeSetter.Set(mains, "01");

        Assert.Equal("NT     ", elb.Data.DataType[1]);
    }

    [Fact]
    public void 機器タイプ設定済みなら既定NTで上書きしない()
    {
        var p = PRow("001");
        var elb = Rec("002", "ELB", gyocd: "M", epap: "003", oyatno: "000", dtype1: "ZZ     ");
        var mains = new[] { p, elb };

        EquipmentTypeSetter.Set(mains, "01");

        Assert.Equal("ZZ     ", elb.Data.DataType[1]);
    }

    [Fact]
    public void 下流の子ブレーカも探査してTLAを設定する()
    {
        var p = PRow("001");
        var elb = Rec("002", "ELB", gyocd: "M", epap: "003", epaat: "00300.000", oyatno: "000");
        // ELB の直下は TB(非ブレーカ)、その下に 2P ブレーカ
        var tb = Rec("003", "TB", gyocd: "", epap: "000", oyatno: "002");
        var child = Rec("004", "MCB", gyocd: "", epap: "002", oyatno: "003");
        var mains = new[] { p, elb, tb, child };

        EquipmentTypeSetter.Set(mains, "01");

        Assert.Equal("TLA    ", elb.Data.DataType[1]);
    }
}
