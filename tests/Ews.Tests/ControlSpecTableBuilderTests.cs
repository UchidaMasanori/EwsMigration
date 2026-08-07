namespace Ews.Tests;

using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

/// <summary>
/// <see cref="ControlSpecTableBuilder"/>(=MakeSgsTable)の移植テスト。
/// </summary>
public sealed class ControlSpecTableBuilderTests
{
    private static SystemTableEntry System(short kno, char kind) =>
        new() { SystemNumber = kno, SystemKind = kind };

    private static LineTypeTableEntry LineType(
        string formatted, string raw, string descRow, short gno) =>
        new()
        {
            LineType = formatted,
            LineTypeRaw = raw,
            DescriptionRow = descRow,
            GroupNumber = gno,
        };

    [Fact]
    public void 基本フィールドをsetして追加する()
    {
        var table = new List<ControlSpecEntry>();

        ControlSpecEntry e = ControlSpecTableBuilder.MakeSgsTable(
            table,
            System(12, '1'),
            LineType("MC", "MC1", "003", 5),
            "MG:RL",
            controlSpecGroup: 7,
            column: 20);

        Assert.Single(table);
        Assert.Same(e, table[0]);
        Assert.Equal((short)12, e.SystemNumber);
        Assert.Equal("MC ", e.LineTypeCode);      // 3桁左詰め・空白埋め
        Assert.Equal("01", e.LineTypeNumber);     // Gyosyu 以降"1"を2桁右詰め・0埋め
        Assert.Equal((short)7, e.ControlSpecGroupNumber);
        Assert.Equal((short)5, e.GroupNumber);
        Assert.Equal("MG:RL", e.RawText);
        Assert.Equal((short)3, e.DescriptionRow);
        Assert.Equal((short)20, e.DescriptionColumn);
        Assert.Equal((short)1, e.SpecNameSequence);  // cnameno = 1(1始まり)
    }

    [Theory]
    [InlineData('1', '1')]
    [InlineData('3', '1')]
    [InlineData('4', '2')]
    [InlineData('2', ' ')]
    [InlineData(' ', ' ')]
    public void 系統種別を変換する(char kind, char expected)
    {
        var table = new List<ControlSpecEntry>();

        ControlSpecEntry e = ControlSpecTableBuilder.MakeSgsTable(
            table, System(1, kind), LineType("MC", "MC", "001", 1), "X", 0, 0);

        Assert.Equal(expected, e.SystemKind);
    }

    [Fact]
    public void 行種番号が無ければ00になる()
    {
        var table = new List<ControlSpecEntry>();

        ControlSpecEntry e = ControlSpecTableBuilder.MakeSgsTable(
            table, System(1, '1'), LineType("MC", "MC", "001", 1), "X", 0, 0);

        Assert.Equal("00", e.LineTypeNumber);
    }

    [Fact]
    public void 行種番号が2桁ならそのまま()
    {
        var table = new List<ControlSpecEntry>();

        ControlSpecEntry e = ControlSpecTableBuilder.MakeSgsTable(
            table, System(1, '1'), LineType("MC", "MC12", "001", 1), "X", 0, 0);

        Assert.Equal("12", e.LineTypeNumber);
    }

    [Fact]
    public void 行種コードが3桁未満なら空白で埋める()
    {
        var table = new List<ControlSpecEntry>();

        ControlSpecEntry e = ControlSpecTableBuilder.MakeSgsTable(
            table, System(1, '1'), LineType("M", "M1", "001", 1), "X", 0, 0);

        Assert.Equal("M  ", e.LineTypeCode);
    }

    [Fact]
    public void 記述行は数値化する()
    {
        var table = new List<ControlSpecEntry>();

        ControlSpecEntry e = ControlSpecTableBuilder.MakeSgsTable(
            table, System(1, '1'), LineType("MC", "MC", "015", 1), "X", 0, 0);

        Assert.Equal((short)15, e.DescriptionRow);
    }

    [Fact]
    public void 追加ごとに名称追番が増える()
    {
        var table = new List<ControlSpecEntry>();

        ControlSpecEntry e1 = ControlSpecTableBuilder.MakeSgsTable(
            table, System(1, '1'), LineType("MC", "MC", "001", 1), "A", 0, 0);
        ControlSpecEntry e2 = ControlSpecTableBuilder.MakeSgsTable(
            table, System(1, '1'), LineType("MC", "MC", "002", 1), "B", 0, 0);
        ControlSpecEntry e3 = ControlSpecTableBuilder.MakeSgsTable(
            table, System(1, '1'), LineType("MC", "MC", "003", 1), "C", 0, 0);

        Assert.Equal((short)1, e1.SpecNameSequence);
        Assert.Equal((short)2, e2.SpecNameSequence);
        Assert.Equal((short)3, e3.SpecNameSequence);
        Assert.Equal(3, table.Count);
    }

    [Fact]
    public void 制御対象機器データ追番は初期化されている()
    {
        var table = new List<ControlSpecEntry>();

        ControlSpecEntry e = ControlSpecTableBuilder.MakeSgsTable(
            table, System(1, '1'), LineType("MC", "MC", "001", 1), "X", 0, 0);

        Assert.Empty(e.ControlTargetSequenceNumbers);
    }

    [Fact]
    public void 制御仕様文字列がnullなら空文字になる()
    {
        var table = new List<ControlSpecEntry>();

        ControlSpecEntry e = ControlSpecTableBuilder.MakeSgsTable(
            table, System(1, '1'), LineType("MC", "MC", "001", 1), null, 0, 0);

        Assert.Equal(string.Empty, e.RawText);
    }
}
