using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="SelectionWorkParametersBuilder"/>(【C原典】Set_WK1)および
/// <see cref="ParentEquipmentLocator"/>(【C原典】Fysk0f_GetOyaP)の単体テスト。
/// </summary>
public class SelectionWorkParametersBuilderTests
{
    /// <summary>
    /// 主回路データを 1 件生成する。datano(SequenceNumber)は 1 始まりの通し番号。
    /// </summary>
    private static MainCircuitResult Rec(
        int datano,
        string lineTypeCode = "",
        string parent = "000",
        string loadCapacity = "0000000",
        string energizingCurrent = "00000000",
        string loadKind = "",
        char loadSource = ' ',
        char phaseCount = '3',
        string circuitVoltage = "000",
        char startKind = ' ')
    {
        var r = new MainCircuitResult { SequenceNumber = datano.ToString("000") };
        r.Data.LineTypeCode = lineTypeCode;
        r.Data.ParentSequenceNumber = parent;
        r.Data.AttachedParameter.LoadCapacity = loadCapacity;
        r.Data.AttachedParameter.LoadKind = loadKind;
        r.Data.EnergizingCurrent = energizingCurrent;
        r.Data.LoadSourceKind = loadSource;
        r.Data.CircuitPhaseCount = phaseCount;
        r.Data.CircuitVoltage[0] = circuitVoltage;
        r.Work.StartCircuitKind = startKind;
        return r;
    }

    [Fact]
    public void Build_各フィールドを主回路データから数値化して格納する()
    {
        // 001: 親 P 行(行種コード先頭 'P'), 002: 当該機器(親追番=001)。
        var records = new List<MainCircuitResult>
        {
            Rec(1, lineTypeCode: "P", parent: "000", phaseCount: '1'),
            Rec(2,
                lineTypeCode: "M",
                parent: "001",
                loadCapacity: "0002200",
                energizingCurrent: "00000150",
                loadKind: "PS",
                loadSource: '2',
                phaseCount: '3',
                circuitVoltage: "200",
                startKind: '1'),
        };

        SelectionWorkParameters work = SelectionWorkParametersBuilder.Build(records, records[1]);

        Assert.Equal(2200.0, work.LoadCapacity);
        Assert.Equal(150.0, work.EnergizingCurrent);
        Assert.Equal('1', work.StartKind);
        Assert.Equal("PS", work.LoadKind);
        Assert.Equal('2', work.OccurrenceKind);
        Assert.Equal((short)3, work.PhaseCount);
        Assert.Equal(200.0, work.CircuitVoltage);
        Assert.Equal('1', work.ParentPhaseCount);   // 親 P 行の回路相数
    }

    [Fact]
    public void Build_負荷種類は先頭2文字に切り詰める()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, lineTypeCode: "P", parent: "000"),
            Rec(2, lineTypeCode: "M", parent: "001", loadKind: "PSX"),
        };

        SelectionWorkParameters work = SelectionWorkParametersBuilder.Build(records, records[1]);

        Assert.Equal("PS", work.LoadKind);
    }

    [Fact]
    public void Build_負荷種類が空なら空白2文字になる()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, lineTypeCode: "P", parent: "000"),
            Rec(2, lineTypeCode: "M", parent: "001", loadKind: ""),
        };

        SelectionWorkParameters work = SelectionWorkParametersBuilder.Build(records, records[1]);

        Assert.Equal("  ", work.LoadKind);
    }

    [Fact]
    public void Build_回路相数はkpaphから0を引いた数値になる()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, lineTypeCode: "P", parent: "000"),
            Rec(2, lineTypeCode: "M", parent: "001", phaseCount: '1'),
        };

        SelectionWorkParameters work = SelectionWorkParametersBuilder.Build(records, records[1]);

        Assert.Equal((short)1, work.PhaseCount);
    }

    [Fact]
    public void Build_親P行が中間機器を経由しても辿って取得する()
    {
        // 001:P行 → 002:中間(親001) → 003:当該(親002)。
        var records = new List<MainCircuitResult>
        {
            Rec(1, lineTypeCode: "P", parent: "000", phaseCount: '1'),
            Rec(2, lineTypeCode: "M", parent: "001", phaseCount: '3'),
            Rec(3, lineTypeCode: "B", parent: "002", phaseCount: '3'),
        };

        SelectionWorkParameters work = SelectionWorkParametersBuilder.Build(records, records[2]);

        Assert.Equal('1', work.ParentPhaseCount);   // 親を辿って P 行(001)の相数
    }

    [Fact]
    public void FindParentPRow_P行が見つからなければnull()
    {
        // P 行が存在しない(全て非 P)。
        var records = new List<MainCircuitResult>
        {
            Rec(1, lineTypeCode: "M", parent: "000"),
            Rec(2, lineTypeCode: "B", parent: "001"),
        };

        MainCircuitResult? parent = ParentEquipmentLocator.FindParentPRow(records, "001");

        Assert.Null(parent);
    }

    [Fact]
    public void FindParentPRow_親追番0ならnull()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, lineTypeCode: "P", parent: "000"),
        };

        MainCircuitResult? parent = ParentEquipmentLocator.FindParentPRow(records, "000");

        Assert.Null(parent);
    }

    [Fact]
    public void Build_親P行が無い場合は相数を空白にする()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, lineTypeCode: "M", parent: "000"),
            Rec(2, lineTypeCode: "B", parent: "001"),
        };

        SelectionWorkParameters work = SelectionWorkParametersBuilder.Build(records, records[1]);

        Assert.Equal(' ', work.ParentPhaseCount);
    }
}
