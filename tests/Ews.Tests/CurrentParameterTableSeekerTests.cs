using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CurrentParameterTableSeeker"/>(【C原典】Fyss3G_CnsPrmtpSeek /
/// CnsSQsetSeek / CnsA2setSeek / CnsA1setSeek / Check_fyrt812)の単体テスト。
/// </summary>
public class CurrentParameterTableSeekerTests
{
    /// <summary>主回路データを 1 件生成する。</summary>
    private static MainCircuitResult Rec(
        string reservedWord = "MCB",
        char equipmentSelectionKind = ' ',
        string loadKind = "  ",
        char phaseCount = '3',
        string voltage = "000")
    {
        var r = new MainCircuitResult { SequenceNumber = "001" };
        r.Data.ReservedWord = reservedWord;
        r.Data.CircuitPhaseCount = phaseCount;
        r.Data.CircuitVoltage[0] = voltage;
        r.Data.AttachedParameter.LoadKind = loadKind;
        r.Work.EquipmentSelectionKind = equipmentSelectionKind;
        return r;
    }

    // ---- CnsPrmtpSeek(SeekParameterSettingType) ----

    [Fact]
    public void SeekParameterSettingType_予約語一致で最初のノードを返す()
    {
        var table = new List<ParameterSettingType>
        {
            new("ELB", 1, 20, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
            new("MCB", 2, 10, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
            new("MCB", 3, 99, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
        };

        ParameterSettingType? hit = CurrentParameterTableSeeker.SeekParameterSettingType(table, Rec("MCB"));

        Assert.NotNull(hit);
        Assert.Equal(10, hit!.SettingType);
    }

    [Fact]
    public void SeekParameterSettingType_予約語は8バイト右詰めで比較する()
    {
        var table = new List<ParameterSettingType>
        {
            new("SB", 1, 30, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
        };

        // 論理値 "SB" は 8 バイト右詰め("SB      ")で一致する。
        ParameterSettingType? hit = CurrentParameterTableSeeker.SeekParameterSettingType(table, Rec("SB"));

        Assert.NotNull(hit);
        Assert.Equal(30, hit!.SettingType);
    }

    [Fact]
    public void SeekParameterSettingType_一致無しはnull()
    {
        var table = new List<ParameterSettingType>
        {
            new("ELB", 1, 20, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
        };

        Assert.Null(CurrentParameterTableSeeker.SeekParameterSettingType(table, Rec("MCB")));
    }

    // ---- CnsSQsetSeek(SeekWireSize) ----

    [Fact]
    public void SeekWireSize_許容電流がkey以上かつ選定フラグ0の最初を返す()
    {
        var table = new List<WireSizeSetting>
        {
            new(2.0, 27.0, 0),
            new(3.5, 37.0, 0),
            new(5.5, 49.0, 0),
        };

        // key = 30 * 1.12 = 33.6 -> 最初に 33.6 以上の許容電流は 37.0(電線サイズ 3.5)。
        double sq = CurrentParameterTableSeeker.SeekWireSize(30.0, table);

        Assert.Equal(3.5, sq);
    }

    [Fact]
    public void SeekWireSize_選定フラグが1のノードは飛ばす()
    {
        var table = new List<WireSizeSetting>
        {
            new(2.0, 100.0, 1),
            new(3.5, 100.0, 0),
        };

        double sq = CurrentParameterTableSeeker.SeekWireSize(10.0, table);

        Assert.Equal(3.5, sq);
    }

    [Fact]
    public void SeekWireSize_該当無しは0()
    {
        var table = new List<WireSizeSetting>
        {
            new(2.0, 27.0, 0),
        };

        // key = 100 * 1.12 = 112 -> 27.0 は満たさない。
        Assert.Equal(0.0, CurrentParameterTableSeeker.SeekWireSize(100.0, table));
    }

    // ---- CnsA2setSeek(SeekRatedCurrent2Coefficient) ----

    [Fact]
    public void SeekRatedCurrent2Coefficient_機器選定区分が1でないなら常に1()
    {
        var records = new List<MainCircuitResult> { Rec(equipmentSelectionKind: ' ', loadKind: "M ") };
        var table = new List<RatedCurrent2Setting>
        {
            new("M", '3', 999, 4.0),
        };

        Assert.Equal(1.0, CurrentParameterTableSeeker.SeekRatedCurrent2Coefficient(records, 0, table));
    }

    [Fact]
    public void SeekRatedCurrent2Coefficient_負荷種類と電圧と相数一致で係数を返す()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(equipmentSelectionKind: '1', loadKind: "M ", phaseCount: '3', voltage: "220"),
        };
        var table = new List<RatedCurrent2Setting>
        {
            new("M", '3', 999, 4.0),
        };

        // 999 > 220 かつ相数 '3' 一致。
        Assert.Equal(4.0, CurrentParameterTableSeeker.SeekRatedCurrent2Coefficient(records, 0, table));
    }

    [Fact]
    public void SeekRatedCurrent2Coefficient_相数無指定は全相該当()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(equipmentSelectionKind: '1', loadKind: "M ", phaseCount: '1', voltage: "100"),
        };
        var table = new List<RatedCurrent2Setting>
        {
            new("M", '\0', 999, 2.5),
        };

        Assert.Equal(2.5, CurrentParameterTableSeeker.SeekRatedCurrent2Coefficient(records, 0, table));
    }

    [Fact]
    public void SeekRatedCurrent2Coefficient_電圧が対象以下なら一致しない()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(equipmentSelectionKind: '1', loadKind: "M ", phaseCount: '3', voltage: "220"),
        };
        var table = new List<RatedCurrent2Setting>
        {
            new("M", '3', 220, 4.0),
        };

        // 220 > 220 は偽 -> 係数は既定 1。
        Assert.Equal(1.0, CurrentParameterTableSeeker.SeekRatedCurrent2Coefficient(records, 0, table));
    }

    [Fact]
    public void SeekRatedCurrent2Coefficient_機器選定区分は先頭データを参照する()
    {
        // records[0] の区分は '1'、対象 records[1] の区分は ' '。C 原典は rt800[0].wk を見るため該当する。
        var records = new List<MainCircuitResult>
        {
            Rec(equipmentSelectionKind: '1', loadKind: "  ", phaseCount: '3', voltage: "000"),
            Rec(equipmentSelectionKind: ' ', loadKind: "M ", phaseCount: '3', voltage: "220"),
        };
        var table = new List<RatedCurrent2Setting>
        {
            new("M", '3', 999, 4.0),
        };

        Assert.Equal(4.0, CurrentParameterTableSeeker.SeekRatedCurrent2Coefficient(records, 1, table));
    }

    // ---- CnsA1setSeek(SeekRatedCurrent1) ----

    [Fact]
    public void SeekRatedCurrent1_keyより大きい最初の定格を返す()
    {
        var table = new List<RatedCurrent1Setting>
        {
            new(15.0),
            new(20.0),
            new(30.0),
        };

        // key=18 -> 18 より大きい最初は 20。
        Assert.Equal(20.0, CurrentParameterTableSeeker.SeekRatedCurrent1(18.0, table));
    }

    [Fact]
    public void SeekRatedCurrent1_該当無しは末尾の定格を返す()
    {
        var table = new List<RatedCurrent1Setting>
        {
            new(15.0),
            new(20.0),
            new(30.0),
        };

        // key=100 -> どれも超えない -> 末尾 30。
        Assert.Equal(30.0, CurrentParameterTableSeeker.SeekRatedCurrent1(100.0, table));
    }

    [Fact]
    public void SeekRatedCurrent1_先頭が既にkey超なら先頭を返す()
    {
        var table = new List<RatedCurrent1Setting>
        {
            new(15.0),
            new(20.0),
        };

        Assert.Equal(15.0, CurrentParameterTableSeeker.SeekRatedCurrent1(0.0, table));
    }

    // ---- Check_fyrt812(CheckLoadCapacityTable) ----

    [Fact]
    public void CheckLoadCapacityTable_ブレーカ系で区分1かつ負荷種類ありなら1()
    {
        var row = Rec(reservedWord: "MCB", equipmentSelectionKind: '1', loadKind: "1 ");

        Assert.Equal(1, CurrentParameterTableSeeker.CheckLoadCapacityTable(row));
    }

    [Fact]
    public void CheckLoadCapacityTable_負荷種類が空白なら0()
    {
        var row = Rec(reservedWord: "MCB", equipmentSelectionKind: '1', loadKind: "  ");

        Assert.Equal(0, CurrentParameterTableSeeker.CheckLoadCapacityTable(row));
    }

    [Fact]
    public void CheckLoadCapacityTable_機器選定区分が1でないなら0()
    {
        var row = Rec(reservedWord: "MCB", equipmentSelectionKind: ' ', loadKind: "1 ");

        Assert.Equal(0, CurrentParameterTableSeeker.CheckLoadCapacityTable(row));
    }

    [Fact]
    public void CheckLoadCapacityTable_ブレーカ系14語以外は0()
    {
        // MC は負荷容量決定テーブルには存在するが 94.09.27 追加のブレーカ系 14 語には含まれない。
        var row = Rec(reservedWord: "MC", equipmentSelectionKind: '1', loadKind: "1 ");

        Assert.Equal(0, CurrentParameterTableSeeker.CheckLoadCapacityTable(row));
    }

    [Fact]
    public void CheckLoadCapacityTable_CKSも対象()
    {
        var row = Rec(reservedWord: "CKS", equipmentSelectionKind: '1', loadKind: "1 ");

        Assert.Equal(1, CurrentParameterTableSeeker.CheckLoadCapacityTable(row));
    }
}
