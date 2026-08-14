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

    [Fact]
    public void アーク溶接機グループ未該当は負の10を返す()
    {
        double ibs = LoadCurrentCalculator.CalculateArcWelder("XXXX  ", 200.0, "KY  ");
        Assert.Equal(-10.0, ibs, Tolerance);
    }

    [Fact]
    public void アーク溶接機グループLは係数2_00で計算する()
    {
        // MCB/KY → group L(1): pow(den,0.92)*2.00
        double expected = System.Math.Pow(200.0, 0.92) * 2.00;
        double ibs = LoadCurrentCalculator.CalculateArcWelder("MCB   ", 200.0, "KY  ");
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void アーク溶接機グループMは係数1_33で計算する()
    {
        // MCB/KM → group M(2)
        double expected = System.Math.Pow(200.0, 0.92) * 1.33;
        double ibs = LoadCurrentCalculator.CalculateArcWelder("MCB   ", 200.0, "KM  ");
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void アーク溶接機グループHは係数1_19で計算する()
    {
        // MCB/ST → group H(3)
        double expected = System.Math.Pow(200.0, 0.92) * 1.19;
        double ibs = LoadCurrentCalculator.CalculateArcWelder("MCB   ", 200.0, "ST  ");
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 電動機グループ未該当は負の1を返す()
    {
        double ibs = LoadCurrentCalculator.CalculateMotor("XXXX  ", 50.0, "KY  ", 5000.0, 3, 200.0, '1');
        Assert.Equal(-1.0, ibs, Tolerance);
    }

    [Fact]
    public void 電動機三相220V以下始動1はyno0で計算する()
    {
        // MCB/KY→group L, p=5<11→tno0, 三相/220V以下/始動1→yno0: [1.06,2.20]
        double expected = System.Math.Pow(50.0, 1.06) * 2.20;
        double ibs = LoadCurrentCalculator.CalculateMotor("MCB   ", 50.0, "KY  ", 5000.0, 3, 200.0, '1');
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 電動機三相220V超始動1はyno2で計算する()
    {
        // MCB/KY→group L, p=15>=11→tno1, 三相/220V超/始動1→yno2: [1.24,0.92]
        double expected = System.Math.Pow(50.0, 1.24) * 0.92;
        double ibs = LoadCurrentCalculator.CalculateMotor("MCB   ", 50.0, "KY  ", 15000.0, 3, 400.0, '1');
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 電動機単相105V以下はyno5で計算する()
    {
        // MCB/KM→group M, p=5<11→tno0, 単相/105V以下→yno5: [0.76,3.60]
        double expected = System.Math.Pow(50.0, 0.76) * 3.60;
        double ibs = LoadCurrentCalculator.CalculateMotor("MCB   ", 50.0, "KM  ", 5000.0, 1, 100.0, '1');
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 電動機単相105V超はyno4で計算する()
    {
        // MCB/KM→group M, p=15→tno1, 単相/105V超→yno4: [1.76,3.60]
        double expected = System.Math.Pow(50.0, 1.76) * 3.60;
        double ibs = LoadCurrentCalculator.CalculateMotor("MCB   ", 50.0, "KM  ", 15000.0, 1, 200.0, '1');
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 電動機グループHの容量22から45はtno3で計算する()
    {
        // MCB/ST→group H, p=25(22<=p<45)→tno3, 三相/220V以下/始動1→yno0: [0.95,2.10]
        double expected = System.Math.Pow(50.0, 0.95) * 2.10;
        double ibs = LoadCurrentCalculator.CalculateMotor("MCB   ", 50.0, "ST  ", 25000.0, 3, 200.0, '1');
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 電動機グループMの容量90以上はtno5で計算する()
    {
        // MCB/KM→group M, p=100(>=90)→tno5, 三相/220V以下/始動1→yno0: [0.59,19.0]
        double expected = System.Math.Pow(50.0, 0.59) * 19.0;
        double ibs = LoadCurrentCalculator.CalculateMotor("MCB   ", 50.0, "KM  ", 100000.0, 3, 200.0, '1');
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void 電動機の基準電流が15未満なら15に切り上げる()
    {
        // MCB/KM→group M tno0 yno0=[0.98,2.00], den=1 → pow(1,0.98)*2.0=2.0 → 15.0
        double ibs = LoadCurrentCalculator.CalculateMotor("MCB   ", 1.0, "KM  ", 5000.0, 3, 200.0, '1');
        Assert.Equal(15.0, ibs, Tolerance);
    }

    [Fact]
    public void ディスパッチ_予約語MMCBは通電電流を直返しする()
    {
        double ibs = LoadCurrentCalculator.Calculate("H ", "MMCB  ", 80.0, "KY  ", 5000.0, 1, 200.0, '1');
        Assert.Equal(80.0, ibs, Tolerance);
    }

    [Fact]
    public void ディスパッチ_ヒータHはden掛ける1_25()
    {
        double ibs = LoadCurrentCalculator.Calculate("H ", "MCB   ", 80.0, "KY  ", 5000.0, 1, 200.0, '1');
        Assert.Equal(80.0 * 1.25, ibs, Tolerance);
    }

    [Fact]
    public void ディスパッチ_水銀灯Sはden掛ける1_00()
    {
        double ibs = LoadCurrentCalculator.Calculate("S ", "MCB   ", 50.0, "KY  ", 5000.0, 1, 200.0, '1');
        Assert.Equal(50.0 * 1.00, ibs, Tolerance);
    }

    [Fact]
    public void ディスパッチ_蛍光灯FLはden掛ける1_40()
    {
        double ibs = LoadCurrentCalculator.Calculate("FL", "MCB   ", 30.0, "KY  ", 5000.0, 1, 200.0, '1');
        Assert.Equal(30.0 * 1.40, ibs, Tolerance);
    }

    [Fact]
    public void ディスパッチ_スポット溶接機YSはden掛ける1_25()
    {
        double ibs = LoadCurrentCalculator.Calculate("YS", "MCB   ", 40.0, "KY  ", 5000.0, 1, 200.0, '1');
        Assert.Equal(40.0 * 1.25, ibs, Tolerance);
    }

    [Fact]
    public void ディスパッチ_変圧器TRはCalculateTransformerに委譲する()
    {
        double expected = LoadCurrentCalculator.CalculateTransformer("MCB   ", 100.0, "KY  ", 5000.0, 1);
        double ibs = LoadCurrentCalculator.Calculate("TR", "MCB   ", 100.0, "KY  ", 5000.0, 1, 200.0, '1');
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void ディスパッチ_アーク溶接機YAはCalculateArcWelderに委譲する()
    {
        double expected = LoadCurrentCalculator.CalculateArcWelder("MCB   ", 100.0, "KY  ");
        double ibs = LoadCurrentCalculator.Calculate("YA", "MCB   ", 100.0, "KY  ", 5000.0, 1, 200.0, '1');
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void ディスパッチ_電動機MはCalculateMotorに委譲する()
    {
        double expected = LoadCurrentCalculator.CalculateMotor("MCB   ", 50.0, "KY  ", 5000.0, 3, 200.0, '1');
        double ibs = LoadCurrentCalculator.Calculate("M ", "MCB   ", 50.0, "KY  ", 5000.0, 3, 200.0, '1');
        Assert.Equal(expected, ibs, Tolerance);
    }

    [Fact]
    public void ディスパッチ_負荷種類未該当は負の1を返す()
    {
        double ibs = LoadCurrentCalculator.Calculate("ZZ", "MCB   ", 100.0, "KY  ", 5000.0, 1, 200.0, '1');
        Assert.Equal(-1.0, ibs, Tolerance);
    }
}
