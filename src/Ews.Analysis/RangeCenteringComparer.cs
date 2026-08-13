namespace Ews.Analysis;

/// <summary>
/// 基準値が2つの幅データ範囲のどちらでより中央寄りかを比較し、よりよいデータを選ぶ。
/// 【C原典】Fysk01_Choki_Cmp1(toku/sekkei/src/Fysk01.c:2734, 特殊予約語 THR/MG/XERY用)。
/// </summary>
public static class RangeCenteringComparer
{
    /// <summary>範囲不正時の戻り値。【C原典】SYS_ERR(fyrt808.h:35 = -1)。</summary>
    public const int SystemError = -1;

    /// <summary>
    /// 今回データ(dt1)が前回データ(dt2)より基準値に対し中央寄りなら 1(入れ替える)、
    /// そうでなければ 0、範囲不正なら -1(SYS_ERR)。
    /// </summary>
    public static int Compare(double referenceValue,
                              double currentLower, double currentUpper,
                              double previousLower, double previousUpper)
    {
        if (currentLower >= currentUpper) return SystemError;
        if (previousLower >= previousUpper) return SystemError;
        if (referenceValue < currentLower || referenceValue > currentUpper) return SystemError;
        if (referenceValue < previousLower || referenceValue > previousUpper) return SystemError;

        double currentAsymmetry = Asymmetry(referenceValue, currentLower, currentUpper);
        double previousAsymmetry = Asymmetry(referenceValue, previousLower, previousUpper);

        return currentAsymmetry < previousAsymmetry ? 1 : 0;
    }

    // |(基準-下限)/幅 - (上限-基準)/幅| = 基準値の範囲内での偏り
    private static double Asymmetry(double value, double lower, double upper)
    {
        double width = upper - lower;
        double lowerRatio = (value - lower) / width;
        double upperRatio = (upper - value) / width;
        return Math.Abs(lowerRatio - upperRatio);
    }
}
