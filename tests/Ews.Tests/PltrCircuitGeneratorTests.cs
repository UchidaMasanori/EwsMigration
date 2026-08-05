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
}
