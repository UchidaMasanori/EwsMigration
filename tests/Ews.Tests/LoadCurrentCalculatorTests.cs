using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="LoadCurrentCalculator"/>(=Get_Ibs 系)の移植テスト。
/// </summary>
public sealed class LoadCurrentCalculatorTests
{
    // TR は MCB のグループ表を借りて検証する(MCB: KY→L=1 / KM→M=2)。
    private const double Tolerance = 1e-9;

    [Fact]
    public void グループ未該当は負の10を返す()
    {
        double ibs = LoadCurrentCalculator.CalculateTransformer("XXXX  ", 100.0, "KY  ", 5000.0, 1);
        Assert.Equal(-10.0, ibs, Tolerance);
    }

    [Fact]
    public void 単相グループLは係数6_34指数マイナス0_10で計算する()
    {
        // MCB/KY → group L(1), sou=1: pow(fuka/1000,-0.10)*den*6.34
        double expected = System.Math.Pow(5000.0 / 1000.0, -0.10) * 100.0 * 6.34;
        double ibs = LoadCurrentCalculator.CalculateTransformer("MCB   ", 100.0, "KY  ", 5000.0, 1);
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 単相グループL以外は係数3_70指数マイナス0_10で計算する()
    {
        // MCB/KM → group M(2), sou=1
        double expected = System.Math.Pow(5000.0 / 1000.0, -0.10) * 100.0 * 3.70;
        double ibs = LoadCurrentCalculator.CalculateTransformer("MCB   ", 100.0, "KM  ", 5000.0, 1);
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 多相グループLは係数4_63指数マイナス0_14で計算する()
    {
        // MCB/KY → group L(1), sou=3
        double expected = System.Math.Pow(5000.0 / 1000.0, -0.14) * 100.0 * 4.63;
        double ibs = LoadCurrentCalculator.CalculateTransformer("MCB   ", 100.0, "KY  ", 5000.0, 3);
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 多相グループL以外は係数2_70指数マイナス0_14で計算する()
    {
        // MCB/KM → group M(2), sou=3
        double expected = System.Math.Pow(5000.0 / 1000.0, -0.14) * 100.0 * 2.70;
        double ibs = LoadCurrentCalculator.CalculateTransformer("MCB   ", 100.0, "KM  ", 5000.0, 3);
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 相数が1以外は全て多相扱い()
    {
        // sou=2 も多相分岐(指数-0.14)。
        double expected = System.Math.Pow(5000.0 / 1000.0, -0.14) * 100.0 * 4.63;
        double ibs = LoadCurrentCalculator.CalculateTransformer("MCB   ", 100.0, "KY  ", 5000.0, 2);
        Assert.Equal(expected, ibs, Tolerance);
    }
}
