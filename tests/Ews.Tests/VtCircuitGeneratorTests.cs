using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="VtCircuitGenerator"/>(【C原典】Fyss14.c Pre_VT_Make)の単体テスト。
/// ＶＴ自動生成の判定(対象 WH/VM 抽出・除外条件・既存 VT による抑止と回路要素格下げ・
/// 挿入位置の後方探索・重複抑止・ステータス)を検証する。
/// </summary>
public sealed class VtCircuitGeneratorTests
{
    private static MainCircuitResult Rec(
        int datano, string yoyaku,
        char kiryoso = '3', string kpav0 = "440",
        string gyocd = "", string gyoglno = "000")
    {
        var r = new MainCircuitResult { SequenceNumber = datano.ToString("D3") };
        MainCircuitData d = r.Data;
        d.ReservedWord = yoyaku;
        d.CircuitElement = kiryoso;
        d.CircuitVoltage[0] = kpav0;
        d.LineTypeCode = gyocd;
        d.LineTypeGroupNumber = gyoglno;
        return r;
    }

    [Fact]
    public void PrepareVtInsertions_資格WHに対しVT挿入情報を生成する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "MCB", kiryoso: '1'),                 // 回路要素 '3' の連続の直前(区切り)。
            Rec(2, "F", kiryoso: '3', gyocd: "AAA"),     // 連続先頭(ただし F は含めない)。
            Rec(3, "WH", kiryoso: '3', gyocd: "AAA"),    // 発生原因 WH。
        };

        VtPreparation result = VtCircuitGenerator.PrepareVtInsertions(records);

        Assert.Equal(1, result.Status);
        VtInsertion e = Assert.Single(result.Insertions);
        Assert.Equal(3, e.WhVmSequenceNumber);
        Assert.Equal(3, e.InsertBeforeSequenceNumber); // F(datano=2)で区切り、その直後 datano=3 が先頭。
    }

    [Fact]
    public void PrepareVtInsertions_回路要素3の連続先頭を後方に探す()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "MCB", kiryoso: '1'),                 // 区切り(kiryoso!='3')。
            Rec(2, "AM", kiryoso: '3', gyocd: "AAA"),    // 連続先頭。
            Rec(3, "WH", kiryoso: '3', gyocd: "AAA"),    // 発生原因 WH。
        };

        VtInsertion e = Assert.Single(VtCircuitGenerator.PrepareVtInsertions(records).Insertions);
        Assert.Equal(2, e.InsertBeforeSequenceNumber); // datano=2 が先頭。
    }

    [Fact]
    public void PrepareVtInsertions_回路電圧220以下は除外する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "WH", kiryoso: '3', kpav0: "220"),
            Rec(2, "WH", kiryoso: '3', kpav0: "210"),
        };

        VtPreparation result = VtCircuitGenerator.PrepareVtInsertions(records);
        Assert.Empty(result.Insertions);
        Assert.Equal(0, result.Status);
    }

    [Fact]
    public void PrepareVtInsertions_回路要素が3でなければ除外する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "WH", kiryoso: '4'),
        };

        Assert.Empty(VtCircuitGenerator.PrepareVtInsertions(records).Insertions);
    }

    [Fact]
    public void PrepareVtInsertions_既存VTがあれば抑止しステータス2を返す()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "VT", kiryoso: '1', gyocd: "AAA", gyoglno: "001"),
            Rec(2, "WH", kiryoso: '3', gyocd: "AAA", gyoglno: "001"),
        };

        VtPreparation result = VtCircuitGenerator.PrepareVtInsertions(records);
        Assert.Empty(result.Insertions);
        Assert.Equal(2, result.Status);
    }

    [Fact]
    public void PrepareVtInsertions_既存VTの直後の回路要素3を4へ格下げする()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "VT", kiryoso: '1', gyocd: "AAA", gyoglno: "001"),
            Rec(2, "AM", kiryoso: '3', gyocd: "BBB"),    // VT 直後 → '4' へ格下げ。
            Rec(3, "AM", kiryoso: '4', gyocd: "CCC"),    // '4' は読み飛ばし継続。
            Rec(4, "WH", kiryoso: '3', gyocd: "AAA", gyoglno: "001"),  // 発生原因 WH(既存VTにより抑止)。
            Rec(5, "SB", kiryoso: '1'),                  // '3'/'4' 以外 → ここで終了(格下げ対象外)。
        };

        VtCircuitGenerator.PrepareVtInsertions(records);

        Assert.Equal('4', records[1].Data.CircuitElement); // datano=2 が格下げ。
        Assert.Equal('4', records[2].Data.CircuitElement); // datano=3 は元々 '4'(読み飛ばし)。
        Assert.Equal('4', records[3].Data.CircuitElement); // datano=4(WH)も VT 直後の走査で '4' 化。
        Assert.Equal('1', records[4].Data.CircuitElement); // datano=5 は '3'/'4' 以外なので終了・非対象。
    }

    [Fact]
    public void PrepareVtInsertions_同一挿入位置は重複挿入しない()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "MCB", kiryoso: '1'),                 // 区切り。
            Rec(2, "WH", kiryoso: '3', gyocd: "AAA"),    // 先頭かつ発生原因。
            Rec(3, "VM", kiryoso: '3', gyocd: "BBB"),    // 同一連続 → 同じ挿入位置 datano=2。
        };

        VtInsertion e = Assert.Single(VtCircuitGenerator.PrepareVtInsertions(records).Insertions);
        Assert.Equal(2, e.InsertBeforeSequenceNumber);
        Assert.Equal(2, e.WhVmSequenceNumber); // 最初の WH のみ登録。
    }

    [Fact]
    public void InsertVtRecords_VT要素を挿入位置の直前へ挿入し発生元を複写する()
    {
        var wh = Rec(2, "WH", kiryoso: '3', gyocd: "AAA", gyoglno: "005");
        MainCircuitData wd = wh.Data;
        wd.SystemNumber = "007";
        wd.SystemKind = '1';
        wd.HierarchyNumber = "002";
        wd.ParallelNumber = "003";
        wd.SortKind = '3';
        wd.LineTypeNumber = "01";
        wd.IncomingNumber = "009";
        wd.CircuitClass = 'M';
        wd.CircuitNumberSuffix = "SFX";
        wd.ElectricalParameterSlots[0].Bn = '2';

        var records = new List<MainCircuitResult>
        {
            Rec(1, "MCB", kiryoso: '1'),
            wh,
            Rec(3, "SB", kiryoso: '1'),   // 非 '3' → 格下げ終了。
        };

        var plan = new[] { new VtInsertion(WhVmSequenceNumber: 2, InsertBeforeSequenceNumber: 2) };

        IReadOnlyList<MainCircuitResult> result = VtCircuitGenerator.InsertVtRecords(records, plan);

        Assert.Equal(4, result.Count);

        MainCircuitData vt = result[1].Data;
        Assert.Equal("002", result[1].SequenceNumber);
        Assert.Equal("VT", vt.ReservedWord);
        Assert.Equal('1', vt.AutoGenerationKind);
        Assert.Equal('4', vt.CircuitElement);
        Assert.Equal('3', vt.SortKind);                       // 発生元の元 narakbn。
        Assert.Equal('1', vt.ElectricalParameterSlots[0].Qty);
        Assert.Equal("007", vt.SystemNumber);
        Assert.Equal('1', vt.SystemKind);
        Assert.Equal("002", vt.HierarchyNumber);
        Assert.Equal("003", vt.ParallelNumber);
        Assert.Equal("AAA", vt.LineTypeCode);
        Assert.Equal("01", vt.LineTypeNumber);
        Assert.Equal("005", vt.LineTypeGroupNumber);
        Assert.Equal("009", vt.IncomingNumber);
        Assert.Equal('M', vt.CircuitClass);
        Assert.Equal("SFX", vt.CircuitNumberSuffix);
        Assert.Equal('2', vt.ElectricalParameterSlots[0].Bn);
        Assert.Equal("FU     ", vt.DataType[0]);              // 同一行種に F 無し → FU。

        // 発生元 WH は VT の直後へ移り回路要素が '4' へ格下げ、narakbn は 3→2(発生時)→1(直後調整)。
        Assert.Equal("003", result[2].SequenceNumber);
        Assert.Equal('4', result[2].Data.CircuitElement);
        Assert.Equal('1', result[2].Data.SortKind);
    }

    [Fact]
    public void InsertVtRecords_挿入で後続要素の親追番が新採番へ付け替わる()
    {
        var sb = Rec(3, "SB", kiryoso: '1');
        sb.Data.ParentSequenceNumber = "002";   // 親 = 発生元 WH(datano=2)。

        var records = new List<MainCircuitResult>
        {
            Rec(1, "MCB", kiryoso: '1'),
            Rec(2, "WH", kiryoso: '3', gyocd: "AAA"),
            sb,
        };

        var plan = new[] { new VtInsertion(WhVmSequenceNumber: 2, InsertBeforeSequenceNumber: 2) };

        IReadOnlyList<MainCircuitResult> result = VtCircuitGenerator.InsertVtRecords(records, plan);

        Assert.Equal(4, result.Count);
        Assert.Equal("004", result[3].SequenceNumber);
        Assert.Equal("003", result[3].Data.ParentSequenceNumber);  // WH の新採番 003 へ付け替え。
    }

    [Fact]
    public void InsertVtRecords_同一行種にFがあればタイプFNを付与する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "F", kiryoso: '1', gyocd: "AAA", gyoglno: "005"),   // 同一行種の F。
            Rec(2, "WH", kiryoso: '3', gyocd: "AAA", gyoglno: "005"),  // 発生元。
        };

        var plan = new[] { new VtInsertion(WhVmSequenceNumber: 2, InsertBeforeSequenceNumber: 2) };

        IReadOnlyList<MainCircuitResult> result = VtCircuitGenerator.InsertVtRecords(records, plan);

        Assert.Equal("FN     ", result[1].Data.DataType[0]);
    }
}
