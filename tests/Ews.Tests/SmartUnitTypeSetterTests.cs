using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="SmartUnitTypeSetter"/>(【C原典】FyssU0.c PropSetAMprm / PropSetVMprm)の単体テスト。
/// スマートユニットの制御対象機器/制御電源データ追番をもとにした AM/VM タイプ設定と
/// 河村製以外(FY-574E)を検証する。
/// </summary>
public sealed class SmartUnitTypeSetterTests
{
    private static MainCircuitResult Rec(
        int datano,
        string yoyaku,
        string maker = "K ",
        string kno = "001",
        string joheino = "000",
        string kaisono = "000",
        string gyoglno = "000",
        string heino = "000",
        string chokuno = "000",
        string gyo = "005",
        string keta = "003")
    {
        var r = new MainCircuitResult { SequenceNumber = datano.ToString("D3") };
        MainCircuitData d = r.Data;
        d.ReservedWord = yoyaku;
        d.AttachedParameter.MakerCode = maker;
        d.SystemNumber = kno;
        d.UpperParallelNumber = joheino;
        d.HierarchyNumber = kaisono;
        d.LineTypeGroupNumber = gyoglno;
        d.ParallelNumber = heino;
        d.SeriesNumber = chokuno;
        d.DescriptionRow = gyo;
        d.DescriptionColumn = keta;
        return r;
    }

    [Fact]
    public void SetAmType_同一系統階層で並列一致のAMに3倍公称と透明色を設定する()
    {
        var target = Rec(1, "MC", kno: "001", joheino: "001", kaisono: "001", gyoglno: "001", heino: "001");
        var am = Rec(2, "AM", kno: "001", joheino: "001", kaisono: "001", gyoglno: "001", heino: "001");
        var mains = new[] { target, am };

        CircuitParseError? err = SmartUnitTypeSetter.SetAmType(mains, "001");

        Assert.Null(err);
        Assert.Equal("3BK    ", am.Data.DataType[0]);
        Assert.Equal("G      ", am.Data.DataType[5]);
    }

    [Fact]
    public void SetAmType_並列が違っても直列一致ならAMに設定する()
    {
        var target = Rec(1, "MC", kno: "001", joheino: "001", kaisono: "001", gyoglno: "001", heino: "001", chokuno: "005");
        var am = Rec(2, "AM", kno: "001", joheino: "001", kaisono: "001", gyoglno: "001", heino: "009", chokuno: "005");
        var mains = new[] { target, am };

        CircuitParseError? err = SmartUnitTypeSetter.SetAmType(mains, "001");

        Assert.Null(err);
        Assert.Equal("3BK    ", am.Data.DataType[0]);
    }

    [Fact]
    public void SetAmType_河村製でないAMはFY574Eを返す()
    {
        var target = Rec(1, "MC", kno: "001");
        var am = Rec(2, "AM", maker: "M ", kno: "001", gyo: "007", keta: "004");
        var mains = new[] { target, am };

        CircuitParseError? err = SmartUnitTypeSetter.SetAmType(mains, "001");

        Assert.NotNull(err);
        Assert.Equal("FY-574E", err!.ErrorCode);
        Assert.Equal(7, err.LineNumber);
        Assert.Equal(4, err.Column);
        Assert.Equal("", am.Data.DataType[0]);
    }

    [Fact]
    public void SetAmType_制御対象機器が見つからなければ何もしない()
    {
        var target = Rec(1, "MC", kno: "001");
        var am = Rec(2, "AM", kno: "001", joheino: "001");
        var mains = new[] { target, am };

        CircuitParseError? err = SmartUnitTypeSetter.SetAmType(mains, "999");

        Assert.Null(err);
        Assert.Equal("", am.Data.DataType[0]);
    }

    [Fact]
    public void SetAmType_系統や階層が異なるAMは変更しない()
    {
        var target = Rec(1, "MC", kno: "001", joheino: "001", kaisono: "001", gyoglno: "001", heino: "001");
        var am = Rec(2, "AM", kno: "002", joheino: "001", kaisono: "001", gyoglno: "001", heino: "001");
        var mains = new[] { target, am };

        CircuitParseError? err = SmartUnitTypeSetter.SetAmType(mains, "001");

        Assert.Null(err);
        Assert.Equal("", am.Data.DataType[0]);
    }

    [Fact]
    public void SetVmType_同一系統のVMに透明色を設定する()
    {
        var target = Rec(1, "MC", kno: "001");
        var vm = Rec(2, "VM", kno: "001");
        var mains = new[] { target, vm };

        CircuitParseError? err = SmartUnitTypeSetter.SetVmType(mains, "001");

        Assert.Null(err);
        Assert.Equal("G      ", vm.Data.DataType[4]);
    }

    [Fact]
    public void SetVmType_河村製でないVMはFY574Eを返す()
    {
        var target = Rec(1, "MC", kno: "001");
        var vm = Rec(2, "VM", maker: "M ", kno: "001", gyo: "008", keta: "002");
        var mains = new[] { target, vm };

        CircuitParseError? err = SmartUnitTypeSetter.SetVmType(mains, "001");

        Assert.NotNull(err);
        Assert.Equal("FY-574E", err!.ErrorCode);
        Assert.Equal(8, err.LineNumber);
        Assert.Equal(2, err.Column);
        Assert.Equal("", vm.Data.DataType[4]);
    }

    [Fact]
    public void SetVmType_電源系統が異なるVMは変更しない()
    {
        var target = Rec(1, "MC", kno: "001");
        var vm = Rec(2, "VM", kno: "002");
        var mains = new[] { target, vm };

        CircuitParseError? err = SmartUnitTypeSetter.SetVmType(mains, "001");

        Assert.Null(err);
        Assert.Equal("", vm.Data.DataType[4]);
    }
}
