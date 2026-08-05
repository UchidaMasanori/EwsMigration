using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="LacslRemoteTypeSetter"/>(【C原典】Fyss1p.c Fyss1p_LACSL_RryType /
/// PropSetRRYprm / PropCheckOyaTrip)の単体テスト。
/// RTR="LA" 系統の RRY への "LA" タイプ設定と親器トリップ電流超過(FY-800E)を検証する。
/// </summary>
public sealed class LacslRemoteTypeSetterTests
{
    private static MainCircuitResult Rec(
        int datano,
        string yoyaku,
        string kno = "001",
        string oyatno = "000",
        string dtype0 = "",
        string dtype1 = "",
        string epaat = "00000.000",
        string gyo = "005",
        string keta = "003")
    {
        var r = new MainCircuitResult { SequenceNumber = datano.ToString("D3") };
        MainCircuitData d = r.Data;
        d.ReservedWord = yoyaku;
        d.SystemNumber = kno;
        d.ParentSequenceNumber = oyatno;
        d.DataType[0] = dtype0;
        d.DataType[1] = dtype1;
        d.ElectricalParameterSlots[0].At = epaat;
        d.DescriptionRow = gyo;
        d.DescriptionColumn = keta;
        return r;
    }

    [Fact]
    public void Apply_LAタイプRTRと同一系統のRRYにLAタイプを設定する()
    {
        // datano=001 が親器(oyatno 参照先), 002=LA タイプ RTR, 003=同一系統 RRY
        var parent = Rec(1, "MCB", kno: "001", epaat: "00010.000");
        var rtr = Rec(2, "RTR", kno: "001", dtype0: "LA     ");
        var rry = Rec(3, "RRY", kno: "001", oyatno: "001");
        var mains = new[] { parent, rtr, rry };

        IReadOnlyList<CircuitParseError> errors = LacslRemoteTypeSetter.Apply(mains);

        Assert.Empty(errors);
        Assert.Equal("LA     ", rry.Data.DataType[1]);
    }

    [Fact]
    public void Apply_RTRがLAタイプでなければRRYを変更しない()
    {
        var parent = Rec(1, "MCB", kno: "001", epaat: "00010.000");
        var rtr = Rec(2, "RTR", kno: "001", dtype0: "NT     ");
        var rry = Rec(3, "RRY", kno: "001", oyatno: "001");
        var mains = new[] { parent, rtr, rry };

        LacslRemoteTypeSetter.Apply(mains);

        Assert.Equal("", rry.Data.DataType[1]);
    }

    [Fact]
    public void Apply_電源系統が異なるRRYは変更しない()
    {
        var parent = Rec(1, "MCB", kno: "001", epaat: "00010.000");
        var rtr = Rec(2, "RTR", kno: "001", dtype0: "LA     ");
        var rry = Rec(3, "RRY", kno: "002", oyatno: "001");   // 別系統
        var mains = new[] { parent, rtr, rry };

        LacslRemoteTypeSetter.Apply(mains);

        Assert.Equal("", rry.Data.DataType[1]);
    }

    [Fact]
    public void Apply_親器トリップ電流が30以上ならFY800Eを返す()
    {
        var parent = Rec(1, "MCB", kno: "001", epaat: "00030.000", gyo: "007", keta: "004");
        var rtr = Rec(2, "RTR", kno: "001", dtype0: "LA     ");
        var rry = Rec(3, "RRY", kno: "001", oyatno: "001");
        var mains = new[] { parent, rtr, rry };

        IReadOnlyList<CircuitParseError> errors = LacslRemoteTypeSetter.Apply(mains);

        Assert.Single(errors);
        Assert.Equal("FY-800E", errors[0].ErrorCode);
        Assert.Equal(7, errors[0].LineNumber);
        Assert.Equal(4, errors[0].Column);
        Assert.Equal("", rry.Data.DataType[1]);   // 超過時はタイプ未設定
    }

    [Fact]
    public void Apply_親器トリップ電流が30未満ならLAタイプを設定する()
    {
        var parent = Rec(1, "MCB", kno: "001", epaat: "00029.999");
        var rtr = Rec(2, "RTR", kno: "001", dtype0: "LA     ");
        var rry = Rec(3, "RRY", kno: "001", oyatno: "001");
        var mains = new[] { parent, rtr, rry };

        IReadOnlyList<CircuitParseError> errors = LacslRemoteTypeSetter.Apply(mains);

        Assert.Empty(errors);
        Assert.Equal("LA     ", rry.Data.DataType[1]);
    }

    [Fact]
    public void Apply_予約語RRY以外は同一系統でも変更しない()
    {
        var parent = Rec(1, "MCB", kno: "001", epaat: "00010.000");
        var rtr = Rec(2, "RTR", kno: "001", dtype0: "LA     ");
        var other = Rec(3, "MCB", kno: "001", oyatno: "001");
        var mains = new[] { parent, rtr, other };

        LacslRemoteTypeSetter.Apply(mains);

        Assert.Equal("", other.Data.DataType[1]);
    }

    [Fact]
    public void Apply_同一系統の複数RRYすべてにLAタイプを設定する()
    {
        var parent = Rec(1, "MCB", kno: "001", epaat: "00010.000");
        var rtr = Rec(2, "RTR", kno: "001", dtype0: "LA     ");
        var rry1 = Rec(3, "RRY", kno: "001", oyatno: "001");
        var rry2 = Rec(4, "RRY", kno: "001", oyatno: "001");
        var mains = new[] { parent, rtr, rry1, rry2 };

        LacslRemoteTypeSetter.Apply(mains);

        Assert.Equal("LA     ", rry1.Data.DataType[1]);
        Assert.Equal("LA     ", rry2.Data.DataType[1]);
    }
}
