using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="SpecialReservedKindSetter"/>(【C原典】Fyss14.c Parm_Set_MGSH / Parm_Set_27)の単体テスト。
/// 自由文字から MGSH(シャッター)/27A・27B・27C の特殊予約語区分を設定する挙動を検証する。
/// </summary>
public sealed class SpecialReservedKindSetterTests
{
    private static MainCircuitResult Rec(string yoyaku)
    {
        var r = new MainCircuitResult();
        MainCircuitData d = r.Data;
        d.ReservedWord = yoyaku;
        d.DescriptionRow = "005";
        d.DescriptionColumn = "001";
        return r;
    }

    private static CircuitDescriptionArea Area(string circuitText) =>
        new([new CircuitDescriptionLine { LineNumber = 5, CircuitText = circuitText }]);

    [Fact]
    public void SetMgshKind_MGSH3Pに区分1を設定する()
    {
        var mains = new List<MainCircuitResult> { Rec("MG") };

        SpecialReservedKindSetter.SetMgshKind(mains, Area("MGSH+(3P)"));

        Assert.Equal('1', mains[0].Data.SpecialReservedWordKind);
    }

    [Fact]
    public void SetMgshKind_MGSH2Pに区分2を設定する()
    {
        var mains = new List<MainCircuitResult> { Rec("MG") };

        SpecialReservedKindSetter.SetMgshKind(mains, Area("MGSH+(2P)"));

        Assert.Equal('2', mains[0].Data.SpecialReservedWordKind);
    }

    [Fact]
    public void SetMgshKind_極数記述なしMGSHは区分1を設定する()
    {
        var mains = new List<MainCircuitResult> { Rec("MG") };

        SpecialReservedKindSetter.SetMgshKind(mains, Area("MGSH"));

        Assert.Equal('1', mains[0].Data.SpecialReservedWordKind);
    }

    [Fact]
    public void SetMgshKind_MGSH以外の自由文字は区分を変えない()
    {
        var mains = new List<MainCircuitResult> { Rec("MG") };

        SpecialReservedKindSetter.SetMgshKind(mains, Area("MG,FOO"));

        Assert.Equal(' ', mains[0].Data.SpecialReservedWordKind);
    }

    [Fact]
    public void SetMgshKind_予約語MG以外は対象外()
    {
        var mains = new List<MainCircuitResult> { Rec("CR") };

        SpecialReservedKindSetter.SetMgshKind(mains, Area("MGSH+(3P)"));

        Assert.Equal(' ', mains[0].Data.SpecialReservedWordKind);
    }

    [Fact]
    public void Set27Kind_27Aに区分3を設定する()
    {
        var mains = new List<MainCircuitResult> { Rec("CR") };

        SpecialReservedKindSetter.Set27Kind(mains, Area("27A"));

        Assert.Equal('3', mains[0].Data.SpecialReservedWordKind);
    }

    [Fact]
    public void Set27Kind_27Bに区分4を設定する()
    {
        var mains = new List<MainCircuitResult> { Rec("CR") };

        SpecialReservedKindSetter.Set27Kind(mains, Area("27B"));

        Assert.Equal('4', mains[0].Data.SpecialReservedWordKind);
    }

    [Fact]
    public void Set27Kind_27Cに区分5を設定する()
    {
        var mains = new List<MainCircuitResult> { Rec("CR") };

        SpecialReservedKindSetter.Set27Kind(mains, Area("27C"));

        Assert.Equal('5', mains[0].Data.SpecialReservedWordKind);
    }

    [Fact]
    public void Set27Kind_27記述なしCRは区分を変えない()
    {
        var mains = new List<MainCircuitResult> { Rec("CR") };

        SpecialReservedKindSetter.Set27Kind(mains, Area("CR,FOO"));

        Assert.Equal(' ', mains[0].Data.SpecialReservedWordKind);
    }
}
