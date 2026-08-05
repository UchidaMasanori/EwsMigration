using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="MeterCircuitOrderChecker"/>(【C原典】Fyss14.c Keiki_Kairo_Check)の単体テスト。
/// 計器回路(行種コード PM)機器の並び検証と、CT/WH の行種相互複写を確認する。
/// </summary>
public sealed class MeterCircuitOrderCheckerTests
{
    private static MainCircuitResult Rec(
        string datano,
        string yoyaku,
        string gyocd = "",
        char kiryoso = ' ',
        char tokkbn = ' ',
        string doukkno = "  ",
        string gyoglno = "000",
        string oyatno = "000")
    {
        var r = new MainCircuitResult { SequenceNumber = datano };
        MainCircuitData d = r.Data;
        d.ReservedWord = yoyaku;
        d.LineTypeCode = gyocd;
        d.CircuitElement = kiryoso;
        d.SpecialReservedWordKind = tokkbn;
        d.IdentityNumber = doukkno;
        d.LineTypeGroupNumber = gyoglno;
        d.ParentSequenceNumber = oyatno;
        d.DescriptionRow = "005";
        d.DescriptionColumn = "003";
        return r;
    }

    [Fact]
    public void Check_計器回路の許容予約語は正常を返す()
    {
        var mains = new List<MainCircuitResult> { Rec("001", "CT", gyocd: "PM", kiryoso: '2') };

        Assert.Null(MeterCircuitOrderChecker.Check(mains));
    }

    [Fact]
    public void Check_PM行種の空区分CRはFY648Eを返す()
    {
        var mains = new List<MainCircuitResult> { Rec("001", "CR", gyocd: "PM", tokkbn: ' ') };

        CircuitParseError? err = MeterCircuitOrderChecker.Check(mains);

        Assert.NotNull(err);
        Assert.Equal("FY-648E", err!.ErrorCode);
        Assert.Equal(5, err.LineNumber);
        Assert.Equal(3, err.Column);
        Assert.Equal("FYMEE80", err.MessageId);
    }

    [Fact]
    public void Check_PM行種の27区分CRは正常を返す()
    {
        // 改訂<35>: tokkbn が空以外(27A 等)の CR は許容。
        var mains = new List<MainCircuitResult> { Rec("001", "CR", gyocd: "PM", tokkbn: '3') };

        Assert.Null(MeterCircuitOrderChecker.Check(mains));
    }

    [Fact]
    public void Check_CTは後続同一機器認識番号CTへ行種を複写する()
    {
        var head = Rec("001", "CT", gyocd: "PM", kiryoso: '2', doukkno: "01", gyoglno: "007");
        var tail = Rec("002", "CT", gyocd: "", kiryoso: '1', doukkno: "01", gyoglno: "000");
        var mains = new List<MainCircuitResult> { head, tail };

        Assert.Null(MeterCircuitOrderChecker.Check(mains));

        Assert.Equal("007", tail.Data.LineTypeGroupNumber);
        Assert.Equal("PM", tail.Data.LineTypeCode);
    }

    [Fact]
    public void Check_WHは後続同一機器認識番号WHの行種を自身へ複写する()
    {
        var head = Rec("001", "WH", gyocd: "", kiryoso: '1', doukkno: "02", gyoglno: "000");
        var tail = Rec("002", "WH", gyocd: "PM", kiryoso: '2', doukkno: "02", gyoglno: "009");
        var mains = new List<MainCircuitResult> { head, tail };

        Assert.Null(MeterCircuitOrderChecker.Check(mains));

        Assert.Equal("009", head.Data.LineTypeGroupNumber);
        Assert.Equal("PM", head.Data.LineTypeCode);
    }

    [Fact]
    public void Check_ASの回路要素区分が2以外はFY645Eを返す()
    {
        var mains = new List<MainCircuitResult> { Rec("001", "AS", kiryoso: '1') };

        CircuitParseError? err = MeterCircuitOrderChecker.Check(mains);

        Assert.NotNull(err);
        Assert.Equal("FY-645E", err!.ErrorCode);
    }

    [Fact]
    public void Check_VSの回路要素区分が3か4なら正常を返す()
    {
        var mains = new List<MainCircuitResult> { Rec("001", "VS", kiryoso: '4') };

        Assert.Null(MeterCircuitOrderChecker.Check(mains));
    }

    [Fact]
    public void Check_VSの回路要素区分が3と4以外はFY645Eを返す()
    {
        var mains = new List<MainCircuitResult> { Rec("001", "VS", kiryoso: '2') };

        CircuitParseError? err = MeterCircuitOrderChecker.Check(mains);

        Assert.NotNull(err);
        Assert.Equal("FY-645E", err!.ErrorCode);
    }

    [Fact]
    public void Check_SCを親とする後続機器があればFY656Eを返す()
    {
        var sc = Rec("010", "SC", gyocd: "PM");
        var child = Rec("011", "F", oyatno: "010");
        var mains = new List<MainCircuitResult> { sc, child };

        CircuitParseError? err = MeterCircuitOrderChecker.Check(mains);

        Assert.NotNull(err);
        Assert.Equal("FY-656E", err!.ErrorCode);
    }

    [Fact]
    public void Check_SCを親とする後続機器がなければ正常を返す()
    {
        var sc = Rec("010", "SC", gyocd: "PM");
        var other = Rec("011", "F", oyatno: "099");
        var mains = new List<MainCircuitResult> { sc, other };

        Assert.Null(MeterCircuitOrderChecker.Check(mains));
    }
}
