using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="SinglePhaseMagnetSelectionChecker"/>(=PropSelChkMcMg)の移植テスト。
/// </summary>
public sealed class SinglePhaseMagnetSelectionCheckerTests
{
    private static MainCircuitData MakeContext(
        char phase = '1',
        string a2 = "00000.000",
        string loadCapacity = "0002200")
    {
        MainCircuitData d = new()
        {
            CircuitPhaseCount = phase,
        };
        d.ElectricalParameterSlots[0].A2 = a2;
        d.AttachedParameter.LoadCapacity = loadCapacity;
        return d;
    }

    [Fact]
    public void コンテキストがnullなら選定可()
    {
        Assert.True(SinglePhaseMagnetSelectionChecker.CanSelect(null, "MSO-T10"));
    }

    [Fact]
    public void 単相以外は選定可()
    {
        MainCircuitData d = MakeContext(phase: '3');
        Assert.True(SinglePhaseMagnetSelectionChecker.CanSelect(d, "MSO-T10"));
    }

    [Fact]
    public void 定格入力ありなら選定可()
    {
        MainCircuitData d = MakeContext(a2: "00012.500");
        Assert.True(SinglePhaseMagnetSelectionChecker.CanSelect(d, "MSO-T10"));
    }

    [Fact]
    public void 負荷容量なしなら選定可()
    {
        MainCircuitData d = MakeContext(loadCapacity: "0000000");
        Assert.True(SinglePhaseMagnetSelectionChecker.CanSelect(d, "MSO-T10"));
    }

    [Fact]
    public void 定格なし負荷ありでMSOT10はNG()
    {
        MainCircuitData d = MakeContext();
        Assert.False(SinglePhaseMagnetSelectionChecker.CanSelect(d, "MSO-T10"));
    }

    [Fact]
    public void 定格なし負荷ありでST10はNG()
    {
        MainCircuitData d = MakeContext();
        Assert.False(SinglePhaseMagnetSelectionChecker.CanSelect(d, "S-T10"));
    }

    [Fact]
    public void 品名接頭辞一致で後続文字ありもNG()
    {
        MainCircuitData d = MakeContext();
        Assert.False(SinglePhaseMagnetSelectionChecker.CanSelect(d, "MSO-T10N"));
    }

    [Fact]
    public void 対象外品名は選定可()
    {
        MainCircuitData d = MakeContext();
        Assert.True(SinglePhaseMagnetSelectionChecker.CanSelect(d, "S-T20"));
    }

    [Fact]
    public void 品名がST10より短ければ選定可()
    {
        MainCircuitData d = MakeContext();
        Assert.True(SinglePhaseMagnetSelectionChecker.CanSelect(d, "S-T1"));
    }
}
