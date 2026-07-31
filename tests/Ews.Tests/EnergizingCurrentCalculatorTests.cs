using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 負荷容量→通電電流値算出(<see cref="EnergizingCurrentCalculator"/>)の移植検証。
/// 【C原典】set_denryu(toku/sekkei/src/Fyss31.c:889)。
/// </summary>
public sealed class EnergizingCurrentCalculatorTests
{
    private const int Precision = 9;
    private static readonly double Sqrt3 = Math.Pow(3.0, 0.5);

    private static MainCircuitData Circuit(string kpav0 = "000", char kpaph = '3', string yoyaku = "", char tokkbn = ' ')
    {
        var d = new MainCircuitData
        {
            CircuitPhaseCount = kpaph,
            ReservedWord = yoyaku,
            SpecialReservedWordKind = tokkbn,
        };
        d.CircuitVoltage[0] = kpav0;
        return d;
    }

    // ── 電動機 "M " ──────────────────────────────────────────────────

    [Fact]
    public void 電動機三相220V以下で1000W以上は係数44の近似式()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("200", '3'), 3700, "M ", out double a);

        Assert.True(ok);
        Assert.Equal(Math.Pow(3.7, 0.948) * 4.4, a, Precision);
    }

    [Fact]
    public void 電動機三相220V以下で1000W未満は係数可変の近似式()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("200", '3'), 400, "M ", out double a);

        Assert.True(ok);
        Assert.Equal(Math.Pow(0.4, 0.945) * (6.0 - (1.6 * 0.4)), a, Precision);
    }

    [Fact]
    public void 電動機MGは750W超1000W以下で44A固定()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("200", '3', yoyaku: "MG"), 800, "M ", out double a);

        Assert.True(ok);
        Assert.Equal(4.4, a, Precision);
    }

    [Fact]
    public void 電動機は製作仕様2で1500W超2200W以下は1110A固定()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(
            Circuit("200", '3'), 2000, "M ", out double a, productionSpec: 2);

        Assert.True(ok);
        Assert.Equal(11.10, a, Precision);
    }

    [Fact]
    public void 電動機は製作仕様標準では強制値を適用しない()
    {
        // productionSpec 既定(標準)なら 2000W は基本式(pow*4.4)のまま。
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("200", '3'), 2000, "M ", out double a);

        Assert.True(ok);
        Assert.Equal(Math.Pow(2.0, 0.948) * 4.4, a, Precision);
    }

    [Fact]
    public void 電動機三相220V超で1500W以上は係数226の近似式()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("440", '3'), 2000, "M ", out double a);

        Assert.True(ok);
        Assert.Equal(Math.Pow(2.0, 0.948) * 2.26, a, Precision);
    }

    [Fact]
    public void 電動機三相220V超で1500W未満は係数可変の近似式()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("440", '3'), 1000, "M ", out double a);

        Assert.True(ok);
        Assert.Equal(2.05, a, Precision); // pow(1,0.948)*(3.3-1.25)=1*2.05
    }

    [Theory]
    [InlineData(100, 0.7)]
    [InlineData(750, 3.6)]
    [InlineData(30000, 105.0)]
    public void 電動機INVBPのTHRは容量帯別に電流値を強制する(double fuka, double expected)
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(
            Circuit("200", '3', yoyaku: "THR", tokkbn: '7'), fuka, "M ", out double a);

        Assert.True(ok);
        Assert.Equal(expected, a, Precision);
    }

    [Fact]
    public void 電動機単相105V以下は係数183の近似式()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("100", '1'), 200, "M ", out double a);

        Assert.True(ok);
        Assert.Equal(Math.Pow(0.2, 0.71) * 18.3, a, Precision);
    }

    [Fact]
    public void 電動機単相105V超は係数91の近似式()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("200", '1'), 200, "M ", out double a);

        Assert.True(ok);
        Assert.Equal(Math.Pow(0.2, 0.71) * 9.1, a, Precision);
    }

    // ── ヒーター "H " ────────────────────────────────────────────────

    [Fact]
    public void ヒーター単相はfuka割るv()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("100", '1'), 500, "H ", out double a);

        Assert.True(ok);
        Assert.Equal(5.0, a, Precision);
    }

    [Fact]
    public void ヒーター三相はfuka割るv割るルート3()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("200", '3'), 1000, "H ", out double a);

        Assert.True(ok);
        Assert.Equal(1000.0 / 200.0 / Sqrt3, a, Precision);
    }

    [Fact]
    public void ヒーター三相は7270から10000を7270へ丸める()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("200", '3'), 8000, "H ", out double a);

        Assert.True(ok);
        Assert.Equal(7270.0 / 200.0 / Sqrt3, a, Precision);
    }

    [Fact]
    public void ヒーターは下限001Aを保証する()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("200", '1'), 1, "H ", out double a);

        Assert.True(ok);
        Assert.Equal(0.01, a, Precision);
    }

    // ── 水銀灯 "S " ──────────────────────────────────────────────────

    [Fact]
    public void 水銀灯は36A超で係数140()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("100", '1'), 4000, "S ", out double a);

        Assert.True(ok);
        Assert.Equal(40.0 * 1.40, a, Precision);
    }

    [Fact]
    public void 水銀灯は36A以下で係数167()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("100", '1'), 3000, "S ", out double a);

        Assert.True(ok);
        Assert.Equal(30.0 * 1.67, a, Precision);
    }

    // ── その他 ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("HA")]
    [InlineData("FL")]
    [InlineData("NA")]
    [InlineData("YA")]
    [InlineData("YS")]
    public void 放電灯類はfuka割るv(string loadKind)
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("100", '1'), 500, loadKind, out double a);

        Assert.True(ok);
        Assert.Equal(5.0, a, Precision);
    }

    [Fact]
    public void トランスTRは単相でfuka割るv()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("100", '1'), 500, "TR", out double a);

        Assert.True(ok);
        Assert.Equal(5.0, a, Precision);
    }

    [Fact]
    public void トランスTRは三相でfuka割るv割るルート3()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("200", '3'), 1000, "TR", out double a);

        Assert.True(ok);
        Assert.Equal(1000.0 / 200.0 / Sqrt3, a, Precision);
    }

    [Fact]
    public void 対象外の負荷種類はfalseを返す()
    {
        bool ok = EnergizingCurrentCalculator.TryCalculate(Circuit("100", '1'), 500, "XX", out double a);

        Assert.False(ok);
        Assert.Equal(0.0, a, Precision);
    }
}
