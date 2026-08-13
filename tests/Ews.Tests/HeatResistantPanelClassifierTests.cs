namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Xunit;

/// <summary>
/// <see cref="HeatResistantPanelClassifier"/>(=Fysk01_Chk_TainetuBunrui)の移植テスト。
/// </summary>
public sealed class HeatResistantPanelClassifierTests
{
    private static CircuitDescriptionLine L(string lineType, string circuit, char cmd = ' ')
        => new() { LineType = lineType, CircuitText = circuit, Command = cmd };

    private static HeatResistantPanelClassificationConstant K(int gyono, string free, char bunrui)
        => new(gyono, free, bunrui);

    [Fact]
    public void 行番号0のコンスタントは1行一致で分類が確定する()
    {
        HeatResistantPanelClassificationConstant[] c = [K(0, "F1+BOX/AAA", 'A')];
        CircuitDescriptionLine[] f =
        [
            L("P", "1P3W"),
            L(" ", "F1+BOX/AAA"),
            L("END", ""),
        ];

        var r = HeatResistantPanelClassifier.Classify(c, f);

        var only = Assert.Single(r);
        Assert.Equal(1, only.SystemNumber);
        Assert.Equal(1, only.PhaseCount);
        Assert.Equal(3, only.WireCount);
        Assert.Equal('A', only.Category);
    }

    [Fact]
    public void 行番号1のコンスタントは次行との2行一致で分類が確定する()
    {
        HeatResistantPanelClassificationConstant[] c =
        [
            K(1, "F1+BOX/LINE1", 'B'),
            K(2, "SECOND", 'B'),
        ];
        CircuitDescriptionLine[] f =
        [
            L("P", "3P4W"),
            L(" ", "F1+BOX/LINE1"),
            L(" ", "SECOND"),
            L("END", ""),
        ];

        var r = HeatResistantPanelClassifier.Classify(c, f);

        var only = Assert.Single(r);
        Assert.Equal(3, only.PhaseCount);
        Assert.Equal(4, only.WireCount);
        Assert.Equal('B', only.Category);
    }

    [Fact]
    public void 相数はP直前線数はW直前の文字から求める()
    {
        HeatResistantPanelClassificationConstant[] c = [K(0, "F1+BOX", 'C')];
        CircuitDescriptionLine[] f =
        [
            L("P", "2P3W200V"),
            L(" ", "F1+BOX"),
        ];

        var r = HeatResistantPanelClassifier.Classify(c, f);

        var only = Assert.Single(r);
        Assert.Equal(2, only.PhaseCount);
        Assert.Equal(3, only.WireCount);
    }

    [Fact]
    public void W記述が無い場合は線数0となる()
    {
        HeatResistantPanelClassificationConstant[] c = [K(0, "F1+BOX", 'C')];
        CircuitDescriptionLine[] f =
        [
            L("P", "1P"),
            L(" ", "F1+BOX"),
        ];

        var r = HeatResistantPanelClassifier.Classify(c, f);

        Assert.Equal(0, Assert.Single(r).WireCount);
    }

    [Fact]
    public void 同一系統に2件該当する場合は分類なしとなる()
    {
        HeatResistantPanelClassificationConstant[] c =
        [
            K(0, "F1+BOX/AAA", 'A'),
            K(0, "F1+BOX/BBB", 'B'),
        ];
        CircuitDescriptionLine[] f =
        [
            L("P", "1P3W"),
            L(" ", "F1+BOX/AAA"),
            L(" ", "F1+BOX/BBB"),
            L("END", ""),
        ];

        var r = HeatResistantPanelClassifier.Classify(c, f);

        Assert.Empty(r);
    }

    [Fact]
    public void cmdがDの行はスキップされる()
    {
        HeatResistantPanelClassificationConstant[] c = [K(0, "F1+BOX/AAA", 'A')];
        CircuitDescriptionLine[] f =
        [
            L("P", "1P3W"),
            L(" ", "F1+BOX/AAA", cmd: 'D'),
            L("END", ""),
        ];

        var r = HeatResistantPanelClassifier.Classify(c, f);

        Assert.Empty(r);
    }

    [Fact]
    public void END以降の行は処理されない()
    {
        HeatResistantPanelClassificationConstant[] c = [K(0, "F1+BOX/AAA", 'A')];
        CircuitDescriptionLine[] f =
        [
            L("P", "1P3W"),
            L(" ", "F1+BOX/AAA"),
            L("END", ""),
            L(" ", "F1+BOX/AAA"), // END 以降は無視され 2件目扱いにならない
        ];

        var r = HeatResistantPanelClassifier.Classify(c, f);

        Assert.Single(r);
    }

    [Fact]
    public void 複数系統でそれぞれ1件ずつ分類が確定する()
    {
        HeatResistantPanelClassificationConstant[] c =
        [
            K(0, "F1+BOX/AAA", 'A'),
            K(0, "F1+BOX/BBB", 'B'),
        ];
        CircuitDescriptionLine[] f =
        [
            L("P", "1P2W"),
            L(" ", "F1+BOX/AAA"),
            L("P", "3P4W"),
            L(" ", "F1+BOX/BBB"),
            L("END", ""),
        ];

        var r = HeatResistantPanelClassifier.Classify(c, f);

        Assert.Equal(2, r.Count);
        Assert.Equal(1, r[0].SystemNumber);
        Assert.Equal('A', r[0].Category);
        Assert.Equal(2, r[1].SystemNumber);
        Assert.Equal('B', r[1].Category);
    }
}
