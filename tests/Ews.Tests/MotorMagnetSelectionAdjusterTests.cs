using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 動力 MG 回路(3.7KW)の MG 選定(<see cref="MotorMagnetSelectionAdjuster.Apply"/>)の移植テスト。
/// 【C原典】PropMGSentei(Fysk00.c:8029)。
/// </summary>
public sealed class MotorMagnetSelectionAdjusterTests
{
    private static MainCircuitResult Circuit(
        string reservedWord, string systemNumber = "001", char phase = ' ', string[]? dataType = null)
    {
        var r = new MainCircuitResult();
        r.Data.ReservedWord = reservedWord;
        r.Data.SystemNumber = systemNumber;
        r.Data.CircuitPhaseCount = phase;
        if (dataType != null)
        {
            r.Data.DataType = dataType;
        }
        return r;
    }

    private static string[] AllNothing()
        => ["NOTHING", "NOTHING", "NOTHING", "NOTHING", "NOTHING", "NOTHING", "NOTHING"];

    // 動力(3相)電源 P を同一系統に持つ mains。
    private static List<MainCircuitResult> MainsWithMotorPower(string systemNumber = "001")
        => [Circuit("P  ", systemNumber, '3')];

    [Fact]
    public void 公共建築仕様のMGで動力電源があれば空き枠に2ETを設定する()
    {
        MainCircuitResult target = Circuit("MG", "001", dataType: AllNothing());

        MotorMagnetSelectionAdjuster.Apply(MainsWithMotorPower(), "02", target);

        Assert.Equal("2ET    ", target.Data.DataType[0]);
        Assert.Equal("NOTHING", target.Data.DataType[1]);
    }

    [Fact]
    public void 先頭以外の空き枠に2ETを設定する()
    {
        string[] dt = ["1C     ", "NOTHING", "NOTHING", "NOTHING", "NOTHING", "NOTHING", "NOTHING"];
        MainCircuitResult target = Circuit("MG", "001", dataType: dt);

        MotorMagnetSelectionAdjuster.Apply(MainsWithMotorPower(), "02", target);

        Assert.Equal("1C     ", target.Data.DataType[0]);
        Assert.Equal("2ET    ", target.Data.DataType[1]);
    }

    [Fact]
    public void 既に2ETがあれば追加しない()
    {
        string[] dt = ["2ET    ", "NOTHING", "NOTHING", "NOTHING", "NOTHING", "NOTHING", "NOTHING"];
        MainCircuitResult target = Circuit("MG", "001", dataType: dt);

        MotorMagnetSelectionAdjuster.Apply(MainsWithMotorPower(), "02", target);

        Assert.Equal("2ET    ", target.Data.DataType[0]);
        Assert.Equal("NOTHING", target.Data.DataType[1]);
    }

    [Fact]
    public void MG以外の予約語は対象外()
    {
        MainCircuitResult target = Circuit("THR", "001", dataType: AllNothing());

        MotorMagnetSelectionAdjuster.Apply(MainsWithMotorPower(), "02", target);

        Assert.Equal("NOTHING", target.Data.DataType[0]);
    }

    [Fact]
    public void 公共建築仕様以外は対象外()
    {
        MainCircuitResult target = Circuit("MG", "001", dataType: AllNothing());

        MotorMagnetSelectionAdjuster.Apply(MainsWithMotorPower(), "01", target);

        Assert.Equal("NOTHING", target.Data.DataType[0]);
    }

    [Fact]
    public void 系統が違う電源では設定しない()
    {
        MainCircuitResult target = Circuit("MG", "001", dataType: AllNothing());

        MotorMagnetSelectionAdjuster.Apply(MainsWithMotorPower("999"), "02", target);

        Assert.Equal("NOTHING", target.Data.DataType[0]);
    }

    [Fact]
    public void 動力でない電源では設定しない()
    {
        var mains = new List<MainCircuitResult> { Circuit("P  ", "001", '1') };
        MainCircuitResult target = Circuit("MG", "001", dataType: AllNothing());

        MotorMagnetSelectionAdjuster.Apply(mains, "02", target);

        Assert.Equal("NOTHING", target.Data.DataType[0]);
    }

    [Fact]
    public void 電源が存在しなければ設定しない()
    {
        var mains = new List<MainCircuitResult> { Circuit("THR", "001", '3') };
        MainCircuitResult target = Circuit("MG", "001", dataType: AllNothing());

        MotorMagnetSelectionAdjuster.Apply(mains, "02", target);

        Assert.Equal("NOTHING", target.Data.DataType[0]);
    }
}
