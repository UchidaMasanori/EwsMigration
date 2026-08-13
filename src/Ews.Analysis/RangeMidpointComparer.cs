namespace Ews.Analysis;

/// <summary>
/// 基準値が今回/前回2つの幅データ範囲のどちらでより中点に近いかを比較し、よりよいデータを選ぶ。
/// 【C原典】Fysk01_Choki_Cmp2(toku/sekkei/src/Fysk01.c:2779, 特殊予約語 THSW/TM用)。
/// </summary>
public static class RangeMidpointComparer
{
    /// <summary>範囲不正時の戻り値。【C原典】SYS_ERR(fyrt808.h:35 = -1)。</summary>
    public const int SystemError = -1;

    /// <summary>
    /// 今回データ(dt1)が前回データ(dt2)より基準値が中点に近いなら 1(入れ替える)、
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

        double currentDistance = MidpointDistance(referenceValue, currentLower, currentUpper);
        double previousDistance = MidpointDistance(referenceValue, previousLower, previousUpper);

        return currentDistance < previousDistance ? 1 : 0;
    }

    // 中点=(上限-下限)/2+下限、その中点と基準値の距離
    private static double MidpointDistance(double value, double lower, double upper) =>
        Math.Abs((upper - lower) / 2.0 + lower - value);
}
