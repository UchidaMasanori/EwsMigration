using Ews.Analysis;
using Ews.Data.Seeding;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// スターデルタ MC/THR 容量選定(PropGetMcThrTblCnst / PropSelChkMgsd)の移植検証。
///
/// 【C原典】Fysk00.c(toku/sekkei/src)。sel_mgsd.cns から容量テーブルを取込み、
/// 負荷容量・回路電圧をキーに MC/THR のヒータ呼び容量を電気パラメータへ設定する。
/// </summary>
public sealed class StarDeltaCapacityTests
{
    // 実 sel_mgsd.cns(toku/const/sekkei)から抜粋した代表行(ヘッダ/表罫線コメント・末尾空白行含む)。
    private const string SampleCns =
        "/* <Title> sel_mgsd.cns */\n" +
        "/* 電圧 | 出力容量 | 品名52 | 品名42 | 品名6 | ヒータ | 品番 ... */\n" +
        "       210,      0005500,     18.0,    18.0,    9.0,           22.0,   S-T20,   S-T20,   S-T12,    TH-T65,\n" +
        "       210,      0007500,     20.0,    20.0,    9.0,           29.0,   S-T21,   S-T21,   S-T12,    TH-T65,\n" +
        "       400,      0005500,      9.0,     9.0,    9.0,           11.0,   S-T12,   S-T12,   S-T12,    TH-T25,\n" +
        "          ,             ,         ,        ,       ,               ,        ,        ,        ,          ,\n" +
        "/*------< End of sel_mcmg.cns >------*/\n";

    private static StarDeltaCapacitySelector Selector() =>
        new(StarDeltaCapacityTableLoader.Parse(SampleCns));

    [Fact]
    public void Loader_コメント行と末尾空白行を除いた3行を取込む()
    {
        IReadOnlyList<StarDeltaCapacityEntry> table = StarDeltaCapacityTableLoader.Parse(SampleCns);

        Assert.Equal(3, table.Count);
        Assert.Equal("210", table[0].Voltage);
        Assert.Equal("0005500", table[0].OutputCapacity);
        Assert.Equal("18.0", table[0].HeaterCapacity52);
        Assert.Equal("18.0", table[0].HeaterCapacity42);
        Assert.Equal("9.0", table[0].HeaterCapacity6);
        Assert.Equal("22.0", table[0].ThermalHeaterCapacity);
    }

    [Fact]
    public void SelChkMgsd_MC品名52を定格電流2へ設定する()
    {
        var epa = new ElectricalParameters();

        Selector().ApplyHeaterCapacity("0005500", "210", epa, StarDeltaCapacitySelector.SlotMc52);

        Assert.Equal("18.0", epa.A2);
    }

    [Fact]
    public void SelChkMgsd_MC品名6を定格電流2へ設定する()
    {
        var epa = new ElectricalParameters();

        Selector().ApplyHeaterCapacity("0005500", "210", epa, StarDeltaCapacitySelector.SlotMc6);

        Assert.Equal("9.0", epa.A2);
    }

    [Fact]
    public void SelChkMgsd_THRのヒータ呼び容量をトリップ電流へ設定する()
    {
        var epa = new ElectricalParameters();

        Selector().ApplyHeaterCapacity("0007500", "210", epa, StarDeltaCapacitySelector.SlotThermal);

        Assert.Equal("29.0", epa.At);
    }

    [Fact]
    public void SelChkMgsd_電圧違いは同一容量でも別行を選ぶ()
    {
        var epa = new ElectricalParameters();

        // 出力容量 0005500 は 210V と 400V で MC52 が異なる(18.0 / 9.0)。
        Selector().ApplyHeaterCapacity("0005500", "400", epa, StarDeltaCapacitySelector.SlotMc52);

        Assert.Equal("9.0", epa.A2);
    }

    [Fact]
    public void SelChkMgsd_一致行が無ければ変更しない()
    {
        var epa = new ElectricalParameters();
        string before = epa.A2;

        Selector().ApplyHeaterCapacity("9999999", "210", epa, StarDeltaCapacitySelector.SlotMc52);

        Assert.Equal(before, epa.A2);
    }
}
