using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="PltrCircuitGenerator"/>(【C原典】Fyss14.c Pre_PLTR_Make)の単体テスト。
/// ＰＬＴＲ自動生成の判定(表示灯タイプ TR/DI 割付・タイプ TR 除外・直前 F の 005V 上書き・
/// 直前 PLTR 除外・盤種類 5/6 除外・挿入位置)を検証する。
/// PLTR が付くのはタイプ DI(直入)の表示灯で、TR(トランス内蔵)は対象外。
/// </summary>
public sealed class PltrCircuitGeneratorTests
{
    private static MainCircuitResult Rec(
        int datano, string yoyaku,
        char kiryoso = '3', char kpavkbn = 'A', string kpav0 = "100",
        char epabn = '1', char ep2bn = '0', string gyocd = "", string gyoglno = "000")
    {
        var r = new MainCircuitResult { SequenceNumber = datano.ToString("D3") };
        MainCircuitData d = r.Data;
        d.ReservedWord = yoyaku;
        d.CircuitElement = kiryoso;
        d.CircuitVoltageKind = kpavkbn;
        d.CircuitVoltage[0] = kpav0;
        d.ElectricalParameterSlots[0].Bn = epabn;
        d.ElectricalParameterSlots[2].Bn = ep2bn;
        d.LineTypeCode = gyocd;
        d.LineTypeGroupNumber = gyoglno;
        return r;
    }

    [Fact]
    public void PreparePltrInsertions_DI表示灯にPLTRを生成する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "GL", kpav0: "100", epabn: '1', ep2bn: '1'),   // ep[2]盤種類'1' → DI。
        };

        IReadOnlyList<PltrInsertion> result = PltrCircuitGenerator.PreparePltrInsertions(records);

        PltrInsertion e = Assert.Single(result);
        Assert.Equal(1, e.CauseSequenceNumber);
        Assert.Equal(1, e.InsertBeforeSequenceNumber);
        Assert.Equal("DI     ", records[0].Data.DataType[0]);
    }

    [Fact]
    public void PreparePltrInsertions_タイプTRの表示灯はPLTR対象外()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "RL", kpav0: "100", epabn: '1', ep2bn: '0'),   // ep[2]盤種類'0' → TR。
        };

        IReadOnlyList<PltrInsertion> result = PltrCircuitGenerator.PreparePltrInsertions(records);

        Assert.Empty(result);                                 // タイプ TR → 対象外。
        Assert.Equal("TR     ", records[0].Data.DataType[0]);
    }

    [Fact]
    public void PreparePltrInsertions_回路電圧100未満はタイプDIで対象外()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "OL", kpav0: "005", epabn: '1'),
        };

        Assert.Empty(PltrCircuitGenerator.PreparePltrInsertions(records));
        Assert.Equal("DI     ", records[0].Data.DataType[0]);  // 回路電圧<100 → DI。
    }

    [Fact]
    public void PreparePltrInsertions_直前FがTRなら回路電圧を005Vへ落としPLTRを付けない()
    {
        var f = Rec(1, "F", kiryoso: '3', kpav0: "100");
        f.Data.DataType[0] = "TR     ";

        var records = new List<MainCircuitResult>
        {
            f,
            Rec(2, "GL", kpav0: "100", epabn: '1', ep2bn: '0'),   // 直前FがTR → このGLはDIに。
        };

        IReadOnlyList<PltrInsertion> result = PltrCircuitGenerator.PreparePltrInsertions(records);

        Assert.Empty(result);
        Assert.Equal("DI     ", records[1].Data.DataType[0]);
        Assert.Equal("005", records[1].Data.CircuitVoltage[0]);
        Assert.Equal("000005.5", records[1].Data.ElectricalParameterSlots[2].V2[0]);
    }

    [Fact]
    public void PreparePltrInsertions_連続するDI表示灯はそれぞれ自身を挿入位置とする()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "GL", kpav0: "100", epabn: '1', ep2bn: '1'),
            Rec(2, "RL", kpav0: "100", epabn: '1', ep2bn: '1'),
        };

        IReadOnlyList<PltrInsertion> result = PltrCircuitGenerator.PreparePltrInsertions(records);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].InsertBeforeSequenceNumber); // GL は自身の直前。
        Assert.Equal(2, result[1].InsertBeforeSequenceNumber); // RL は自身の直前。
    }

    [Fact]
    public void PreparePltrInsertions_直前がPLTRなら対象外()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "PLTR", kiryoso: '1'),
            Rec(2, "GL", kpav0: "100", epabn: '1', ep2bn: '1'),
        };

        Assert.Empty(PltrCircuitGenerator.PreparePltrInsertions(records));
    }

    [Fact]
    public void PreparePltrInsertions_盤種類5や6は制御盤警報盤として対象外()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "GL", kpav0: "100", epabn: '5', ep2bn: '1'),   // DI かつ ep[0]盤種類'5'。
        };

        Assert.Empty(PltrCircuitGenerator.PreparePltrInsertions(records));
    }

    [Fact]
    public void PreparePltrInsertions_直前FがグレードでTR化され005Vへ落ちる()
    {
        var f = Rec(1, "F", kiryoso: '3', kpav0: "100");   // タイプ未設定・数量'1'・電圧100。
        f.Data.ElectricalParameterSlots[0].Qty = '1';

        var records = new List<MainCircuitResult>
        {
            f,
            Rec(2, "GL", kpav0: "100", epabn: '1', ep2bn: '1', gyocd: "AAA"),  // DI。
        };

        IReadOnlyList<PltrInsertion> result = PltrCircuitGenerator.PreparePltrInsertions(
            records, manufacturingSpecKind: "01");

        Assert.Empty(result);
        Assert.Equal("TR     ", records[0].Data.DataType[0]);   // 直前 F が TR 化。
        Assert.Equal("005", records[1].Data.CircuitVoltage[0]);
    }

    [Fact]
    public void InsertPltrRecords_PLTR要素を挿入位置の直前へ挿入し発生元を複写する()
    {
        var lamp = Rec(2, "GL", kpav0: "100", epabn: '1', gyocd: "AAA", gyoglno: "005");
        MainCircuitData ld = lamp.Data;
        ld.SystemNumber = "007";
        ld.SystemKind = '1';
        ld.HierarchyNumber = "002";
        ld.ParallelNumber = "003";
        ld.SortKind = '3';
        ld.LineTypeNumber = "01";
        ld.IncomingNumber = "009";
        ld.CircuitClass = 'M';
        ld.CircuitNumberSuffix = "SFX";
        ld.AttachedParameter.MakerCode = "MK1";

        var records = new List<MainCircuitResult>
        {
            Rec(1, "MCB", kiryoso: '1'),
            lamp,
            Rec(3, "SB", kiryoso: '1'),
        };

        var plan = new[] { new PltrInsertion(CauseSequenceNumber: 2, InsertBeforeSequenceNumber: 2) };

        IReadOnlyList<MainCircuitResult> result = PltrCircuitGenerator.InsertPltrRecords(records, plan);

        Assert.Equal(4, result.Count);

        MainCircuitData pltr = result[1].Data;
        Assert.Equal("002", result[1].SequenceNumber);
        Assert.Equal("PLTR", pltr.ReservedWord);
        Assert.Equal('1', pltr.AutoGenerationKind);
        Assert.Equal('3', pltr.CircuitElement);
        Assert.Equal('3', pltr.SortKind);                     // 発生元の narakbn(減算なし)。
        Assert.Equal('1', pltr.ElectricalParameterSlots[0].Qty);
        Assert.Equal("007", pltr.SystemNumber);
        Assert.Equal("002", pltr.HierarchyNumber);
        Assert.Equal("003", pltr.ParallelNumber);
        Assert.Equal("AAA", pltr.LineTypeCode);
        Assert.Equal("01", pltr.LineTypeNumber);
        Assert.Equal("005", pltr.LineTypeGroupNumber);
        Assert.Equal("009", pltr.IncomingNumber);
        Assert.Equal('M', pltr.CircuitClass);
        Assert.Equal("SFX", pltr.CircuitNumberSuffix);
        Assert.Equal("MK1", pltr.AttachedParameter.MakerCode);
        Assert.Equal('1', pltr.ElectricalParameterSlots[0].Bn);

        // 発生元 GL は PLTR の直後へ移り、narakbn は減算されない(直後同階層同並列調整の対象外)。
        Assert.Equal("003", result[2].SequenceNumber);
        Assert.Equal('3', result[2].Data.SortKind);
    }

    [Fact]
    public void InsertPltrRecords_挿入で後続要素の親追番が新採番へ付け替わる()
    {
        var sb = Rec(3, "SB", kiryoso: '1');
        sb.Data.ParentSequenceNumber = "002";   // 親 = 発生元 GL(datano=2)。

        var records = new List<MainCircuitResult>
        {
            Rec(1, "MCB", kiryoso: '1'),
            Rec(2, "GL", kpav0: "100", epabn: '1'),
            sb,
        };

        var plan = new[] { new PltrInsertion(CauseSequenceNumber: 2, InsertBeforeSequenceNumber: 2) };

        IReadOnlyList<MainCircuitResult> result = PltrCircuitGenerator.InsertPltrRecords(records, plan);

        Assert.Equal(4, result.Count);
        Assert.Equal("004", result[3].SequenceNumber);
        Assert.Equal("003", result[3].Data.ParentSequenceNumber);  // GL の新採番 003 へ付け替え。
    }

    [Fact]
    public void InsertPltrRecords_PLTR直後の同階層同並列の並び替え機器区分を1戻す()
    {
        var lamp = Rec(2, "GL", kpav0: "100", epabn: '1');
        lamp.Data.HierarchyNumber = "002";
        lamp.Data.ParallelNumber = "003";

        var follow = Rec(3, "GL", kiryoso: '3', kpav0: "100", epabn: '1');
        follow.Data.HierarchyNumber = "002";   // PLTR と同一階層。
        follow.Data.ParallelNumber = "003";    // PLTR と同一並列。
        follow.Data.SortKind = '4';

        var records = new List<MainCircuitResult> { lamp, follow };

        var plan = new[] { new PltrInsertion(CauseSequenceNumber: 1, InsertBeforeSequenceNumber: 1) };

        IReadOnlyList<MainCircuitResult> result = PltrCircuitGenerator.InsertPltrRecords(records, plan);

        // result[0]=PLTR '3', result[1]=元GL(発生元), result[2]=後続GL(narakbn 4→3)。
        Assert.Equal("PLTR", result[0].Data.ReservedWord);
        Assert.Equal('3', result[2].Data.SortKind);
    }
}
