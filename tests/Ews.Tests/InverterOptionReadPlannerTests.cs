using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="InverterOptionReadPlanner"/>(=Fysk01_Kiki_Read_INV_OP 前提判定部)の移植テスト。
/// </summary>
public sealed class InverterOptionReadPlannerTests
{
    [Fact]
    public void AC指定でKW有りなら続行しinvACへ振り分ける()
    {
        InverterOptionReadPlan plan = InverterOptionReadPlanner.Plan(0, "AC", 3700);
        Assert.Equal(InverterOptionReadPlanner.ProceedStatus, plan.Status);
        Assert.Equal("invAC.cns", plan.ConstantFileName);
        Assert.False(plan.UseRadioNoise);
    }

    [Fact]
    public void KW入力はfpalw2を10分の1し100で割った値になる()
    {
        InverterOptionReadPlan plan = InverterOptionReadPlanner.Plan(0, "AC", 3700);
        Assert.Equal(3.7, plan.InputKw);
    }

    [Fact]
    public void 種別が一致しなければパラメータ指定無しを返す()
    {
        InverterOptionReadPlan plan = InverterOptionReadPlanner.Plan(0, "DC", 3700);
        Assert.Equal(InverterOptionReadPlanner.ParameterNotSpecifiedStatus, plan.Status);
    }

    [Fact]
    public void RN以外はKW入力無しでエラーを返す()
    {
        InverterOptionReadPlan plan = InverterOptionReadPlanner.Plan(0, "AC", 0);
        Assert.Equal(InverterOptionReadPlanner.KwMissingStatus, plan.Status);
    }

    [Fact]
    public void RNはKW入力無しでも続行し専用処理を使う()
    {
        InverterOptionReadPlan plan = InverterOptionReadPlanner.Plan(2, "RN", 0);
        Assert.Equal(InverterOptionReadPlanner.ProceedStatus, plan.Status);
        Assert.True(plan.UseRadioNoise);
        Assert.Null(plan.ConstantFileName);
    }

    [Fact]
    public void MCはパラメータチェックせずinvMCへ振り分ける()
    {
        InverterOptionReadPlan plan = InverterOptionReadPlanner.Plan(4, "ZZ", 5000);
        Assert.Equal(InverterOptionReadPlanner.ProceedStatus, plan.Status);
        Assert.Equal("invMC.cns", plan.ConstantFileName);
        Assert.False(plan.UseRadioNoise);
    }

    [Fact]
    public void MCはKW入力無しならエラーを返す()
    {
        InverterOptionReadPlan plan = InverterOptionReadPlanner.Plan(4, "ZZ", 0);
        Assert.Equal(InverterOptionReadPlanner.KwMissingStatus, plan.Status);
    }
}
