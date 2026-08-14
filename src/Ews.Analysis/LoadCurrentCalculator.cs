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
}
