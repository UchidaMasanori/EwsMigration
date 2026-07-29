using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="MeterCircuitBuilder"/>(【C原典】Fysk00_Make_Keiki)の単体テスト。
/// </summary>
public class MeterCircuitBuilderTests
{
    /// <summary>datano=通し番号(index+1)、系統種別 '1' の主回路レコードを生成する。</summary>
    private static MainCircuitResult Rec(int sequence, string parent, string reserved, double ratedCapacity = 0.0,
                                         char identity = ' ', char element = ' ')
    {
        var r = new MainCircuitResult { SequenceNumber = sequence.ToString("000") };
        r.Data.SystemKind = '1';
        r.Data.ParentSequenceNumber = parent;
        r.Data.ReservedWord = reserved;
        r.Data.IdentityNumber = identity == ' ' ? "  " : identity.ToString().PadRight(2);
        r.Data.CircuitElement = element;
        r.Work.RatedCapacity = ratedCapacity;
        return r;
    }

    [Fact]
    public void AssignCapacities_PLTRは下流teiwvaを積み上げVAと二次電圧をセットする()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "PLTR"),          // 計器(index0)
            Rec(2, "001", "MCB", 1.0),      // 下流
            Rec(3, "001", "MCB", 0.0),      // 下流
            Rec(4, "000", "MCB", 99.0),     // 兄弟(打ち切り、積まれない)
        };
        var meters = new List<MeterCircuitEntry> { new() { Rec = 0, Katei = 0 } };

        short chk = MeterCircuitBuilder.AssignCapacities(meters, records);

        ElectricalParameters epOut = records[0].Data.ElectricalParameterSlots[2];   // eno=2
        Assert.Equal("0000001.00", epOut.Va);
        Assert.Equal("000005.5", epOut.V2[0]);              // all==1 → 5.5V
        Assert.Equal(0.0, records[1].Work.RatedCapacity);   // 積上元はクリア
        Assert.Equal(99.0, records[3].Work.RatedCapacity);  // 兄弟は不変
        Assert.Equal(1, meters[0].Katei);
        Assert.Equal((short)1, chk);                        // katei!=2 が残る
    }

    [Fact]
    public void AssignCapacities_PLTRで合計が1以外なら二次電圧は15V()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "PLTR"),
            Rec(2, "001", "MCB", 3.0),
            Rec(3, "000", "MCB"),
        };
        var meters = new List<MeterCircuitEntry> { new() { Rec = 0, Katei = 0 } };

        MeterCircuitBuilder.AssignCapacities(meters, records);

        ElectricalParameters epOut = records[0].Data.ElectricalParameterSlots[2];
        Assert.Equal("0000003.00", epOut.Va);
        Assert.Equal("000015.0", epOut.V2[0]);
    }

    [Fact]
    public void AssignCapacities_VTはVAのみ設定し二次電圧は触らない()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "VT"),
            Rec(2, "001", "MCB", 10.0),
            Rec(3, "000", "MCB"),
        };
        var meters = new List<MeterCircuitEntry> { new() { Rec = 0, Katei = 0 } };

        MeterCircuitBuilder.AssignCapacities(meters, records);

        ElectricalParameters epOut = records[0].Data.ElectricalParameterSlots[2];
        Assert.Equal("0000010.00", epOut.Va);
        Assert.Equal("00000000", epOut.V2[0]);   // 未設定のまま
    }

    [Fact]
    public void AssignCapacities_FはA2に合計割る二次電圧をセットしteiwvaへ合計を残す()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "F"),
            Rec(2, "001", "MCB", 200.0),
            Rec(3, "000", "MCB"),
        };
        records[0].Data.ElectricalParameterSlots[2].V2[0] = "000100.0";   // eno=2 側の二次電圧=100V
        var meters = new List<MeterCircuitEntry> { new() { Rec = 0, Katei = 0 } };

        MeterCircuitBuilder.AssignCapacities(meters, records);

        ElectricalParameters epOut = records[0].Data.ElectricalParameterSlots[2];
        Assert.Equal("00002.000", epOut.A2);              // 200 / 100 = 2.000
        Assert.Equal(200.0, records[0].Work.RatedCapacity);
        Assert.Equal(0.0, records[1].Work.RatedCapacity);
    }

    [Fact]
    public void AssignCapacities_ep1に定格値が有ればeno1側に書き込む()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "VT"),
            Rec(2, "001", "MCB", 7.0),
            Rec(3, "000", "MCB"),
        };
        records[0].Data.ElectricalParameterSlots[1].W1 = "0000000100";   // ep[1] に負荷容量 → eno=1
        var meters = new List<MeterCircuitEntry> { new() { Rec = 0, Katei = 0 } };

        MeterCircuitBuilder.AssignCapacities(meters, records);

        Assert.Equal("0000007.00", records[0].Data.ElectricalParameterSlots[1].Va);   // eno=1 に書く
        Assert.Equal("0000000000", records[0].Data.ElectricalParameterSlots[2].Va);   // eno=2 は不変
    }

    [Fact]
    public void AssignCapacities_ep1のVAが既設なら積上げず下流をクリアするのみ()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "VT"),
            Rec(2, "001", "MCB", 8.0),
            Rec(3, "000", "MCB"),
        };
        records[0].Data.ElectricalParameterSlots[1].Va = "0000005.00";   // ep[1] VA 既設
        var meters = new List<MeterCircuitEntry> { new() { Rec = 0, Katei = 0 } };

        MeterCircuitBuilder.AssignCapacities(meters, records);

        Assert.Equal("0000000000", records[0].Data.ElectricalParameterSlots[2].Va);   // VA は書かれない
        Assert.Equal(0.0, records[1].Work.RatedCapacity);                              // 下流はクリア
    }

    [Fact]
    public void AssignCapacities_CTは回路要素1以外の同一機器の下流を参照する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "CT", identity: '1', element: '1'),   // 計器(index0, kiryoso='1')
            Rec(2, "000", "CT", identity: '1', element: '2'),   // 同一機器CT(kiryoso!='1') → 起点
            Rec(3, "002", "MCB", 50.0),                          // その下流
            Rec(4, "000", "MCB"),                                // 兄弟(打ち切り)
        };
        var meters = new List<MeterCircuitEntry> { new() { Rec = 0, Katei = 0 } };

        MeterCircuitBuilder.AssignCapacities(meters, records);

        Assert.Equal("0000050.00", records[0].Data.ElectricalParameterSlots[2].Va);
        Assert.Equal(0.0, records[2].Work.RatedCapacity);
    }

    [Fact]
    public void AssignCapacities_予約語順で最初に該当したタイプのみ処理する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "VT"),   // index0
            Rec(2, "000", "F"),    // index1
        };
        var meters = new List<MeterCircuitEntry>
        {
            new() { Rec = 0, Katei = 0 },   // VT
            new() { Rec = 1, Katei = 0 },   // F
        };

        short chk = MeterCircuitBuilder.AssignCapacities(meters, records);

        Assert.Equal(1, meters[0].Katei);   // VT は処理
        Assert.Equal(0, meters[1].Katei);   // F は未処理(次回)
        Assert.Equal((short)1, chk);
    }

    [Fact]
    public void AssignCapacities_全機器がkatei2なら0を返す()
    {
        var records = new List<MainCircuitResult> { Rec(1, "000", "VT") };
        var meters = new List<MeterCircuitEntry> { new() { Rec = 0, Katei = 2 } };

        short chk = MeterCircuitBuilder.AssignCapacities(meters, records);

        Assert.Equal((short)0, chk);
    }
}
