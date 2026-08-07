namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

/// <summary>
/// <see cref="MainCircuitCopyChecker"/>(=Fysk01_Copy_Check)の移植テスト。
/// </summary>
public sealed class MainCircuitCopyCheckerTests
{
    private static MainCircuitResult MakeRecord() =>
        new() { Data = MainCircuitData.Create(), Work = new CircuitWork() };

    [Fact]
    public void 全フィールド一致で同一と判定する()
    {
        MainCircuitResult a = MakeRecord();
        MainCircuitResult b = MakeRecord();

        Assert.True(MainCircuitCopyChecker.AreCopyEquivalent(a, b));
    }

    [Fact]
    public void 予約語が異なれば非同一()
    {
        MainCircuitResult a = MakeRecord();
        MainCircuitResult b = MakeRecord();
        b.Data.ReservedWord = "PT";

        Assert.False(MainCircuitCopyChecker.AreCopyEquivalent(a, b));
    }

    [Fact]
    public void タイプが異なれば非同一()
    {
        MainCircuitResult a = MakeRecord();
        MainCircuitResult b = MakeRecord();
        b.Data.DataType[2] = "AL";

        Assert.False(MainCircuitCopyChecker.AreCopyEquivalent(a, b));
    }

    [Fact]
    public void 電気パラメータが異なれば非同一()
    {
        MainCircuitResult a = MakeRecord();
        MainCircuitResult b = MakeRecord();
        b.Data.ElectricalParameterSlots[1].Af = "000001000";

        Assert.False(MainCircuitCopyChecker.AreCopyEquivalent(a, b));
    }

    [Fact]
    public void 封印区分が異なれば非同一()
    {
        MainCircuitResult a = MakeRecord();
        MainCircuitResult b = MakeRecord();
        b.Data.AttachedParameter.SealKind = 'H';

        Assert.False(MainCircuitCopyChecker.AreCopyEquivalent(a, b));
    }

    [Fact]
    public void 始動回路区分が異なれば非同一()
    {
        MainCircuitResult a = MakeRecord();
        MainCircuitResult b = MakeRecord();
        b.Work.StartCircuitKind = '1';

        Assert.False(MainCircuitCopyChecker.AreCopyEquivalent(a, b));
    }

    [Fact]
    public void 予約語の末尾空白差は同一とみなす()
    {
        MainCircuitResult a = MakeRecord();
        MainCircuitResult b = MakeRecord();
        a.Data.ReservedWord = "PT";
        b.Data.ReservedWord = "PT      ";

        Assert.True(MainCircuitCopyChecker.AreCopyEquivalent(a, b));
    }
}
