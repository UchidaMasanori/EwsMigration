namespace Ews.Analysis;

/// <summary>
/// 負荷種類別の基準電流(Ibs)を算出する。Get_Ibs 系(Fysk01.c)のボトムアップ移植。
/// グループ番号は <see cref="ShapeTypeGroupResolver"/>(=Get_Group)で解決する。
/// </summary>
public static class LoadCurrentCalculator
{
    /// <summary>グループ未該当時の戻り値。【C原典】return(-10.0)。</summary>
    private const double GroupNotFoundCurrent = -10.0;

    /// <summary>単相を表す相数。【C原典】sou == 1。</summary>
    private const int SinglePhase = 1;

    /// <summary>負荷容量を kW 換算する除数。【C原典】fuka/1000.0。</summary>
    private const double WattPerKw = 1000.0;

    /// <summary>グループ L(=1)。【C原典】gpno == 1。</summary>
    private const int GroupL = 1;

    /// <summary>アーク溶接機の指数。【C原典】pow(den, 0.92)。</summary>
    private const double ArcWelderExponent = 0.92;

    /// <summary>三相を表す相数。【C原典】sou == 3。</summary>
    private const int ThreePhase = 3;

    /// <summary>始動区分の判定値。【C原典】st == '1'。</summary>
    private const char StartKind = '1';

    /// <summary>電動機でグループ未該当時の戻り値。【C原典】return(-1.0)。</summary>
    private const double MotorGroupNotFoundCurrent = -1.0;

    /// <summary>電動機の基準電流下限。【C原典】ibs &lt; 15.0 なら 15.0。</summary>
    private const double MotorFloorCurrent = 15.0;

    // 電動機テーブル(fyrt819.h grp_l/grp_m/grp_h)。[group-1][tno][yno] = {x(指数), y(係数)}。
    // grp_l は tno 0/1 のみ到達(gno==1 は p<11?0:1)、grp_m/grp_h は tno 0-5。
    private static readonly double[][][] MotorGroupL =
    [
        [[1.06, 2.20], [1.06, 2.20], [1.06, 2.20], [1.06, 2.20], [0.76, 3.60], [0.76, 3.60]],
        [[1.04, 2.20], [1.04, 2.20], [1.24, 0.92], [1.24, 0.92], [1.76, 3.60], [0.76, 3.60]],
    ];

    private static readonly double[][][] MotorGroupM =
    [
        [[0.98, 2.00], [0.98, 2.20], [0.95, 2.20], [0.81, 3.90], [0.76, 3.60], [0.76, 3.60]],
        [[0.96, 2.00], [0.90, 2.70], [0.95, 2.20], [0.81, 3.90], [1.76, 3.60], [0.76, 3.60]],
        [[0.96, 2.00], [0.90, 2.70], [0.94, 1.90], [0.81, 3.90], [1.76, 3.60], [0.76, 3.60]],
        [[0.94, 2.70], [0.94, 2.70], [0.94, 1.90], [0.81, 3.90], [1.76, 3.60], [0.76, 3.60]],
        [[0.59, 19.0], [0.59, 19.0], [1.02, 2.00], [0.81, 3.90], [1.76, 3.60], [0.76, 3.60]],
        [[0.59, 19.0], [0.59, 19.0], [1.02, 2.00], [1.02, 2.00], [1.76, 3.60], [0.76, 3.60]],
    ];

    private static readonly double[][][] MotorGroupH =
    [
        [[0.98, 2.00], [0.98, 2.00], [0.95, 2.20], [0.99, 2.10], [0.76, 3.60], [0.76, 3.60]],
        [[0.95, 1.70], [0.95, 1.70], [0.95, 2.20], [0.99, 2.10], [1.76, 3.60], [0.76, 3.60]],
        [[0.95, 1.70], [0.95, 1.70], [0.96, 1.70], [0.96, 1.70], [1.76, 3.60], [0.76, 3.60]],
        [[0.95, 2.10], [0.95, 2.10], [0.96, 1.70], [0.96, 1.70], [1.76, 3.60], [0.76, 3.60]],
        [[0.95, 2.10], [0.95, 2.10], [0.96, 1.70], [0.96, 1.80], [1.76, 3.60], [0.76, 3.60]],
        [[0.95, 2.10], [0.95, 2.10], [0.98, 1.80], [0.98, 1.80], [1.76, 3.60], [0.76, 3.60]],
    ];

    // 【C原典】g_tbl = { grp_l, grp_m, grp_h }。
    private static readonly double[][][][] MotorTables = [MotorGroupL, MotorGroupM, MotorGroupH];

    /// <summary>
    /// 変圧器(TR)の基準電流(Ibs)を算出する。
    ///
    /// 【C原典】Get_Ibs_TR(toku/sekkei/src/Fysk01.c:4975, static DOUBLE)。
    ///   gpno=Get_Group(yo,type)。gpno==0 は -10.0。単相(sou==1)は係数 L=6.34/他=3.70・指数 -0.10、
    ///   多相は係数 L=4.63/他=2.70・指数 -0.14。ibs = pow(fuka/1000, 指数) * den * 係数。
    /// </summary>
    /// <param name="reservedWord">予約語(yo)。</param>
    /// <param name="energizingCurrent">通電電流値(den)。</param>
    /// <param name="dataType">タイプパラメータ(type)。</param>
    /// <param name="loadCapacity">負荷容量 W(fuka)。</param>
    /// <param name="phaseCount">相数(sou)。</param>
    public static double CalculateTransformer(
        string reservedWord,
        double energizingCurrent,
        string dataType,
        double loadCapacity,
        int phaseCount)
    {
        ArgumentNullException.ThrowIfNull(reservedWord);
        ArgumentNullException.ThrowIfNull(dataType);

        int group = ShapeTypeGroupResolver.Resolve(reservedWord, dataType);
        if (group == 0)
        {
            return GroupNotFoundCurrent;
        }

        double coefficient;
        double exponent;
        if (phaseCount == SinglePhase)
        {
            coefficient = group == GroupL ? 6.34 : 3.70;
            exponent = -0.10;
        }
        else
        {
            coefficient = group == GroupL ? 4.63 : 2.70;
            exponent = -0.14;
        }

        return Math.Pow(loadCapacity / WattPerKw, exponent) * energizingCurrent * coefficient;
    }

    /// <summary>
    /// アーク溶接機(YA)の基準電流(Ibs)を算出する。
    ///
    /// 【C原典】Get_Ibs_YA(toku/sekkei/src/Fysk01.c:5007, static DOUBLE)。
    ///   gpno=Get_Group(yo,type)。gpno==0 は -10.0。係数はグループ L=2.00/M=1.33/H=1.19。
    ///   ibs = pow(den, 0.92) * 係数。
    /// </summary>
    /// <param name="reservedWord">予約語(yo)。</param>
    /// <param name="energizingCurrent">通電電流値(den)。</param>
    /// <param name="dataType">タイプパラメータ(type)。</param>
    public static double CalculateArcWelder(
        string reservedWord,
        double energizingCurrent,
        string dataType)
    {
        ArgumentNullException.ThrowIfNull(reservedWord);
        ArgumentNullException.ThrowIfNull(dataType);

        int group = ShapeTypeGroupResolver.Resolve(reservedWord, dataType);
        if (group == 0)
        {
            return GroupNotFoundCurrent;
        }

        double coefficient = group switch
        {
            GroupL => 2.00,
            2 => 1.33,
            _ => 1.19, // group == 3(Resolve は 0-3 のみ、0 は上で除外済み)
        };

        return Math.Pow(energizingCurrent, ArcWelderExponent) * coefficient;
    }

    /// <summary>
    /// 電動機(M)の基準電流(Ibs)を算出する。
    ///
    /// 【C原典】Get_Ibs_M(toku/sekkei/src/Fysk01.c:5045, static DOUBLE)。
    ///   相数・電圧・始動区分から yno(0-5)、容量 p=fuka/1000 とグループ gno から tno を決め、
    ///   g_tbl[gno-1].atai_t[tno].atai[yno] の {x, y} で ibs = pow(den, x) * y を算出する。
    ///   gno&lt;1 は -1.0、ibs&lt;15.0 は 15.0 に切り上げる。
    /// </summary>
    /// <param name="reservedWord">予約語(yo)。</param>
    /// <param name="energizingCurrent">通電電流値(den)。</param>
    /// <param name="dataType">タイプパラメータ(type)。</param>
    /// <param name="loadCapacity">負荷容量 W(fuka)。</param>
    /// <param name="phaseCount">相数(sou)。</param>
    /// <param name="voltage">電圧(vol)。</param>
    /// <param name="startKind">始動開始区分(st)。</param>
    public static double CalculateMotor(
        string reservedWord,
        double energizingCurrent,
        string dataType,
        double loadCapacity,
        int phaseCount,
        double voltage,
        char startKind)
    {
        ArgumentNullException.ThrowIfNull(reservedWord);
        ArgumentNullException.ThrowIfNull(dataType);

        double p = loadCapacity / WattPerKw;

        int yno;
        if (phaseCount == ThreePhase)
        {
            if (voltage <= 220.0)
            {
                yno = startKind == StartKind ? 0 : 1;
            }
            else
            {
                yno = startKind == StartKind ? 2 : 3;
            }
        }
        else
        {
            yno = voltage <= 105.0 ? 5 : 4;
        }

        int group = ShapeTypeGroupResolver.Resolve(reservedWord, dataType);
        if (group < 1)
        {
            return MotorGroupNotFoundCurrent;
        }

        int tno = group switch
        {
            1 => p < 11.0 ? 0 : 1,
            2 => MotorRowMedium(p),
            _ => MotorRowHigh(p), // group == 3(Resolve は 0-3 のみ、0 は上で除外済み)
        };

        double[] coefficients = MotorTables[group - 1][tno][yno];
        double ibs = Math.Pow(energizingCurrent, coefficients[0]) * coefficients[1];

        return ibs < MotorFloorCurrent ? MotorFloorCurrent : ibs;
    }

    // 【C原典】gno==2(M)の容量 p による tno(0-5)決定。
    private static int MotorRowMedium(double p)
    {
        if (p < 11.0) return 0;
        if (p < 18.5) return 1;
        if (p < 45.0) return 2;
        if (p < 75.0) return 3;
        if (p < 90.0) return 4;
        return 5;
    }

    // 【C原典】gno==3(H)の容量 p による tno(0-5)決定。
    private static int MotorRowHigh(double p)
    {
        if (p < 11.0) return 0;
        if (p < 18.5) return 1;
        if (p < 22.0) return 2;
        if (p < 45.0) return 3;
        if (p < 60.0) return 4;
        return 5;
    }
}
