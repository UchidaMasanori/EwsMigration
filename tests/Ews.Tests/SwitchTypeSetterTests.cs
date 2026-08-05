using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="SwitchTypeSetter"/>(C 原典 CS_MCDT_12_21_SET)の単体テスト。
/// </summary>
public sealed class SwitchTypeSetterTests
{
    private static MainCircuitResult Rec(
        string datano,
        string yoyaku,
        string ysno = "00",
        char kaetyp = ' ',
        string oyatno = "000",
        string kno = "001")
    {
        var r = new MainCircuitResult { SequenceNumber = datano };
        MainCircuitData d = r.Data;
        d.ReservedWord = yoyaku;
        d.DesignationNumber = ysno;
        d.SwitchType = kaetyp;
        d.ParentSequenceNumber = oyatno;
        d.SystemNumber = kno;
        d.DescriptionRow = "005";
        d.DescriptionColumn = "003";
        return r;
    }

    [Fact]
    public void 親追番が一致すれば1_2型を双方へ設定する()
    {
        var a = Rec("001", "MCDT", ysno: "01", oyatno: "010");
        var b = Rec("002", "MCDT", ysno: "01", oyatno: "010");
        var mains = new[] { a, b };

        Assert.Null(SwitchTypeSetter.Set(mains));

        Assert.Equal('1', a.Data.SwitchType);
        Assert.Equal('1', b.Data.SwitchType);
    }

    [Fact]
    public void 親追番が異なり系統が異なれば2_1型を双方へ設定する()
    {
        var a = Rec("001", "CSDT", ysno: "01", oyatno: "010", kno: "001");
        var b = Rec("002", "CSDT", ysno: "01", oyatno: "020", kno: "002");
        var mains = new[] { a, b };

        Assert.Null(SwitchTypeSetter.Set(mains));

        Assert.Equal('2', a.Data.SwitchType);
        Assert.Equal('2', b.Data.SwitchType);
    }

    [Fact]
    public void 親追番が異なり同一系統ならFY922Eを返す()
    {
        var a = Rec("001", "MCDT", ysno: "01", oyatno: "010", kno: "001");
        var b = Rec("002", "MCDT", ysno: "01", oyatno: "020", kno: "001");
        var mains = new[] { a, b };

        CircuitParseError? err = SwitchTypeSetter.Set(mains);

        Assert.NotNull(err);
        Assert.Equal("FY-922E", err!.ErrorCode);
        Assert.Equal(5, err.LineNumber);
        Assert.Equal(3, err.Column);
        Assert.Equal("FYMEE80", err.MessageId);
        Assert.Equal(' ', a.Data.SwitchType);   // エラー時は未設定のまま
    }

    [Fact]
    public void 予約語指定番号00は対象外()
    {
        var a = Rec("001", "MCDT", ysno: "00", oyatno: "010");
        var b = Rec("002", "MCDT", ysno: "00", oyatno: "010");
        var mains = new[] { a, b };

        Assert.Null(SwitchTypeSetter.Set(mains));

        Assert.Equal(' ', a.Data.SwitchType);
        Assert.Equal(' ', b.Data.SwitchType);
    }

    [Fact]
    public void 予約語がCSDTMCDT以外は対象外()
    {
        var a = Rec("001", "MC", ysno: "01", oyatno: "010");
        var b = Rec("002", "MC", ysno: "01", oyatno: "010");
        var mains = new[] { a, b };

        Assert.Null(SwitchTypeSetter.Set(mains));

        Assert.Equal(' ', a.Data.SwitchType);
        Assert.Equal(' ', b.Data.SwitchType);
    }

    [Fact]
    public void 予約語指定番号が異なる後続とは対を作らない()
    {
        var a = Rec("001", "MCDT", ysno: "01", oyatno: "010");
        var b = Rec("002", "MCDT", ysno: "02", oyatno: "010");
        var mains = new[] { a, b };

        Assert.Null(SwitchTypeSetter.Set(mains));

        Assert.Equal(' ', a.Data.SwitchType);
        Assert.Equal(' ', b.Data.SwitchType);
    }

    [Fact]
    public void 切り換えタイプ設定済みの後続とは対を作らない()
    {
        var a = Rec("001", "MCDT", ysno: "01", oyatno: "010");
        var b = Rec("002", "MCDT", ysno: "01", oyatno: "010", kaetyp: '1');
        var mains = new[] { a, b };

        Assert.Null(SwitchTypeSetter.Set(mains));

        Assert.Equal(' ', a.Data.SwitchType);   // 対象後続なし=未設定
        Assert.Equal('1', b.Data.SwitchType);
    }
}
