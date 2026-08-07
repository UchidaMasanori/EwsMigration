namespace Ews.Tests;

using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Xunit;

/// <summary>
/// <see cref="ControlSpecDataBuilder"/>(=GetSgData)の移植テスト。
/// </summary>
public sealed class ControlSpecDataBuilderTests
{
    private static ControlSpecEntry Spec(string rawText, short cnameno = 1) =>
        new() { RawText = rawText, SpecNameSequence = cnameno };

    private static EquipmentTableEntry Kiki(
        string reservedWord,
        short rank = 1,
        short kakko1 = 0,
        short quantity = 0,
        string itemName = "",
        string descRow = "",
        string descColumn = "",
        string[]? dtype = null)
    {
        var e = new EquipmentTableEntry
        {
            ReservedWord = reservedWord,
            Rank = rank,
            Kakko1 = kakko1,
            Quantity = quantity,
            ItemName = itemName,
            DescriptionRow = descRow,
            DescriptionColumn = descColumn,
        };
        if (dtype is not null)
        {
            for (int i = 0; i < dtype.Length && i < e.DType.Length; i++)
            {
                e.DType[i] = dtype[i];
            }
        }
        return e;
    }

    private static CircuitDescriptionLine Line(int lineNumber, string circuitText, char command = ' ') =>
        new() { LineNumber = lineNumber, CircuitText = circuitText, Command = command };

    [Fact]
    public void 制御対象機器を予約語_番号_サフィックスに分解する()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec("MG12,MC3:RL2"),
            new List<EquipmentTableEntry>(),
            descriptionRow: 5,
            descriptionColumn: 10,
            lineTypeGroup: 7,
            new List<CircuitDescriptionLine>());

        Assert.Equal(2, data.ControlTargets.Count);

        ControlTargetEntry t0 = data.ControlTargets[0];
        Assert.Equal("MG", t0.ReservedWord);
        Assert.Equal("12", t0.ReservedWordNumber);
        Assert.Equal(" ", t0.Suffix);
        Assert.Equal((short)5, t0.DescriptionRow);
        Assert.Equal((short)10, t0.DescriptionColumn);
        Assert.Equal((short)7, t0.GroupNumber);

        ControlTargetEntry t1 = data.ControlTargets[1];
        Assert.Equal("MC", t1.ReservedWord);
        Assert.Equal("03", t1.ReservedWordNumber);
        Assert.Equal(" ", t1.Suffix);
        // 【C原典】keta += strlen("MG12")+1 = 5 → 10+5 = 15
        Assert.Equal((short)15, t1.DescriptionColumn);
    }

    [Fact]
    public void コロンが無ければ制御対象機器は生成されない()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec("MG12"),
            new List<EquipmentTableEntry>(),
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        Assert.Empty(data.ControlTargets);
    }

    [Fact]
    public void サフィックス付き予約語を分解する()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec("RY1A:X"),
            new List<EquipmentTableEntry>(),
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        ControlTargetEntry t0 = data.ControlTargets[0];
        Assert.Equal("RY", t0.ReservedWord);
        Assert.Equal("01", t0.ReservedWordNumber);
        Assert.Equal("A", t0.Suffix);
    }

    [Fact]
    public void MGSHはMGとして扱う()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec("MGSH2:X"),
            new List<EquipmentTableEntry>(),
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        ControlTargetEntry t0 = data.ControlTargets[0];
        Assert.Equal("MG", t0.ReservedWord);
        Assert.Equal("02", t0.ReservedWordNumber);
    }

    [Fact]
    public void インターロック指定でフラグが立ちINVパターンで3になる()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec("OL<INV"),
            new List<EquipmentTableEntry>(),
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        Assert.True(data.InterlockFlag);
        Assert.Equal((short)3, data.PatternNumber);
    }

    [Fact]
    public void インターロック無しならフラグは立たない()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec("MG:X"),
            new List<EquipmentTableEntry>(),
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        Assert.False(data.InterlockFlag);
    }

    [Fact]
    public void PTN指定で品名の数値がパターン番号になる()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec(""),
            new List<EquipmentTableEntry> { Kiki("PTN", rank: 1, itemName: "5") },
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        Assert.Equal((short)5, data.PatternNumber);
    }

    [Fact]
    public void 外部制御機器はKakko1が12で個数0なら1になる()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec(""),
            new List<EquipmentTableEntry> { Kiki("XX", rank: 1, kakko1: 12, quantity: 0) },
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        ControlEquipmentEntry e = Assert.Single(data.ControlEquipment);
        Assert.Equal("XX", e.ReservedWord);
        Assert.Equal((short)1, e.ExternalCount);
        Assert.Equal((short)0, e.InternalCount);
    }

    [Fact]
    public void 内部制御機器は個数指定を保持する()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec(""),
            new List<EquipmentTableEntry> { Kiki("MC", rank: 1, kakko1: 0, quantity: 3) },
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        ControlEquipmentEntry e = Assert.Single(data.ControlEquipment);
        Assert.Equal("MC", e.ReservedWord);
        Assert.Equal((short)3, e.InternalCount);
    }

    [Fact]
    public void 名称追番が一致しない制御機器は対象外()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec("", cnameno: 1),
            new List<EquipmentTableEntry> { Kiki("MC", rank: 2, quantity: 1) },
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        Assert.Empty(data.ControlEquipment);
    }

    [Fact]
    public void THRは記述桁直前が山括弧なら予約語が山括弧付きになる()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec(""),
            new List<EquipmentTableEntry>
            {
                Kiki("THR", rank: 1, quantity: 1, descRow: "001", descColumn: "005"),
            },
            0, 0, 0,
            new List<CircuitDescriptionLine> { Line(1, "012<THR") });

        ControlEquipmentEntry e = Assert.Single(data.ControlEquipment);
        Assert.Equal("<THR", e.ReservedWord);
        Assert.Equal((short)1, e.InternalCount);
    }

    [Fact]
    public void THRは記述桁直前が山括弧でなければ予約語のまま()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec(""),
            new List<EquipmentTableEntry>
            {
                Kiki("THR", rank: 1, quantity: 1, descRow: "001", descColumn: "005"),
            },
            0, 0, 0,
            new List<CircuitDescriptionLine> { Line(1, "0123THR") });

        ControlEquipmentEntry e = Assert.Single(data.ControlEquipment);
        Assert.Equal("THR", e.ReservedWord);
    }

    [Fact]
    public void 液面リレー用途YOUでパターン3を設定する()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec(""),
            new List<EquipmentTableEntry>
            {
                Kiki("G", rank: 1, dtype: new[] { "YOU" }),
            },
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        Assert.Equal((short)3, data.PatternNumber);
    }

    [Fact]
    public void 液面リレー用途KUUでパターン4を設定する()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec(""),
            new List<EquipmentTableEntry>
            {
                Kiki("G1", rank: 1, dtype: new[] { "KUU" }),
            },
            0, 0, 0,
            new List<CircuitDescriptionLine>());

        Assert.Equal((short)4, data.PatternNumber);
    }

    [Fact]
    public void 制御対象機器と内部制御機器を同時に構築する()
    {
        ControlSpecData data = ControlSpecDataBuilder.BuildSgData(
            Spec("MG:X"),
            new List<EquipmentTableEntry> { Kiki("MC", rank: 1, quantity: 2) },
            descriptionRow: 3,
            descriptionColumn: 8,
            lineTypeGroup: 4,
            new List<CircuitDescriptionLine>());

        ControlTargetEntry t = Assert.Single(data.ControlTargets);
        Assert.Equal("MG", t.ReservedWord);
        Assert.Equal((short)3, t.DescriptionRow);
        Assert.Equal((short)4, t.GroupNumber);

        ControlEquipmentEntry e = Assert.Single(data.ControlEquipment);
        Assert.Equal("MC", e.ReservedWord);
        Assert.Equal((short)2, e.InternalCount);
    }
}
