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
}
