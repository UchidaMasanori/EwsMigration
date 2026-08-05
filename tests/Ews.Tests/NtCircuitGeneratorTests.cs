using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="NtCircuitGenerator"/>(【C原典】Fyss14.c Pre_NT_Make)の単体テスト。
/// ＮＴ自動生成の判定(対象 MCB 抽出・除外条件・同一グループ親の差し替え・既存 NT 検出)を検証する。
/// </summary>
public sealed class NtCircuitGeneratorTests
{
    private static MainCircuitResult Rec(
        int datano, string oyatno, string yoyaku,
        string goyano = "000", string kaisono = "000", string epap = "000",
        char ph = '3', char wr = '3', string v0 = "210", string v1 = "105", string v2 = "000")
    {
        var r = new MainCircuitResult { SequenceNumber = datano.ToString("D3") };
        MainCircuitData d = r.Data;
        d.SystemKind = '1';
        d.ReservedWord = yoyaku;
        d.ParentSequenceNumber = oyatno;
        d.GroupParentSequenceNumber = goyano;
        d.HierarchyNumber = kaisono;
        d.ElectricalParameterSlots[2].P = epap;
        d.CircuitPhaseCount = ph;
        d.CircuitWireType = wr;
        d.CircuitVoltage[0] = v0;
        d.CircuitVoltage[1] = v1;
        d.CircuitVoltage[2] = v2;
        return r;
    }

    [Fact]
    public void PrepareNtInsertions_資格MCBに対しNT挿入情報を生成する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "P"),                                        // グループ親(sijino=1)
            Rec(2, "001", "MCB", goyano: "001", kaisono: "001", epap: "001"), // 資格 MCB
            Rec(3, "002", "SB", goyano: "001", kaisono: "001"),        // 同一階層の下流
        };

        IReadOnlyList<NtInsertion> plan = NtCircuitGenerator.PrepareNtInsertions(records);

        NtInsertion e = Assert.Single(plan);
        Assert.Equal(2, e.McbSequenceNumber);
        Assert.Equal(3, e.EndSequenceNumber);
        Assert.Equal(3, e.InsertAfterSequenceNumber);
        Assert.Equal(1, e.Hierarchy);
    }

    [Fact]
    public void PrepareNtInsertions_1相2線210VのMCBは除外する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "P"),
            Rec(2, "001", "MCB", goyano: "001", kaisono: "001", epap: "001",
                ph: '1', wr: '2', v0: "210", v1: "000", v2: "000"),
            Rec(3, "002", "SB", goyano: "001", kaisono: "001"),
        };

        Assert.Empty(NtCircuitGenerator.PrepareNtInsertions(records));
    }

    [Fact]
    public void PrepareNtInsertions_ep2極数3桁目が1でなければ除外する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "P"),
            Rec(2, "001", "MCB", goyano: "001", kaisono: "001", epap: "000"),
            Rec(3, "002", "SB", goyano: "001", kaisono: "001"),
        };

        Assert.Empty(NtCircuitGenerator.PrepareNtInsertions(records));
    }

    [Fact]
    public void PrepareNtInsertions_グループ親000は除外する()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "P"),
            Rec(2, "001", "MCB", goyano: "000", kaisono: "001", epap: "001"),
            Rec(3, "002", "SB", goyano: "001", kaisono: "001"),
        };

        Assert.Empty(NtCircuitGenerator.PrepareNtInsertions(records));
    }

    [Fact]
    public void PrepareNtInsertions_同一グループ親の後続MCBは対象MCBを差し替える()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "P"),
            Rec(2, "001", "MCB", goyano: "001", kaisono: "001", epap: "001"),
            Rec(3, "001", "MCB", goyano: "001", kaisono: "002", epap: "001"),
        };

        NtInsertion e = Assert.Single(NtCircuitGenerator.PrepareNtInsertions(records));
        Assert.Equal(3, e.McbSequenceNumber); // 後続 MCB(datano=3)へ差し替え
    }

    [Fact]
    public void PrepareNtInsertions_下流に同一階層のNTが既にあれば生成しない()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(1, "000", "P"),
            Rec(2, "001", "MCB", goyano: "001", kaisono: "001", epap: "001"),
            Rec(3, "002", "NT", goyano: "001", kaisono: "001"),
        };

        Assert.Empty(NtCircuitGenerator.PrepareNtInsertions(records));
    }
}
