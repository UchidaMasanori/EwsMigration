namespace Ews.Analysis;

/// <summary>
/// 特殊予約語(THR/MG)の直近上下位データを今回と前回で比較し、より条件に合うデータを選ぶ。
/// 【C原典】Fysk01_Data_Cmp(toku/sekkei/src/Fysk01.c:2668)。
/// </summary>
public static class NearestRankDataComparer
{
    /// <summary>予約語区分 THR。【C原典】PC_1(fyrt808.h:38 = 11)。</summary>
    public const int ThrKind = 11;

    /// <summary>予約語区分 MG。【C原典】PC_3(fyrt808.h:40 = 13)。</summary>
    public const int MgKind = 13;

    private const double Tolerance = 0.001;  // 【C原典】TOL
    private const int RatingValueLength = 50; // 【C原典】sizeof key.kteichi[50]

    /// <summary>
    /// 今回データを採用(入れ替え)するなら 1、しないなら 0。
    /// </summary>
    /// <param name="reservedWordKind">予約語区分 no(THR/MG)。</param>
    /// <param name="referenceValue">基準値 schi。</param>
    /// <param name="currentLower">今回幅下限 dat1a[0]。</param>
    /// <param name="currentUpper">今回幅上限 dat1a[1]。</param>
    /// <param name="currentValue">今回値 dat1v(MG判定用)。</param>
    /// <param name="currentRatingValue">今回定格値 dat1.key.kteichi。</param>
    /// <param name="previousLower">前回幅下限 dat2a[0]。</param>
    /// <param name="previousUpper">前回幅上限 dat2a[1]。</param>
    /// <param name="previousValue">前回値 dat2v(MG判定用)。</param>
    /// <param name="previousRatingValue">前回定格値 dat2.key.kteichi。</param>
    public static int Compare(int reservedWordKind,
                              double referenceValue,
                              double currentLower, double currentUpper, double currentValue,
                              string currentRatingValue,
                              double previousLower, double previousUpper, double previousValue,
                              string previousRatingValue)
    {
        if (reservedWordKind == MgKind)
        {
            if (currentValue < previousValue)
            {
                return 1;
            }
            if (Math.Abs(currentValue - previousValue) < Tolerance)
            {
                return ChooseByWidthAndRating(referenceValue,
                    currentLower, currentUpper, currentRatingValue,
                    previousLower, previousUpper, previousRatingValue);
            }
            return 0;
        }

        if (reservedWordKind == ThrKind)
        {
            return ChooseByWidthAndRating(referenceValue,
                currentLower, currentUpper, currentRatingValue,
                previousLower, previousUpper, previousRatingValue);
        }

        return 0;
    }

    private static int ChooseByWidthAndRating(double referenceValue,
                                              double currentLower, double currentUpper, string currentRatingValue,
                                              double previousLower, double previousUpper, string previousRatingValue)
    {
        // 幅(下限・上限)が一致するときのみ定格値/中央寄りで判定
        if (Math.Abs(currentLower - previousLower) >= Tolerance ||
            Math.Abs(currentUpper - previousUpper) >= Tolerance)
        {
            return 0;
        }

        if (CompareRating(currentRatingValue, previousRatingValue) < 0)
        {
            return 1;
        }

        return RangeCenteringComparer.Compare(referenceValue,
            currentLower, currentUpper, previousLower, previousUpper) == 1 ? 1 : 0;
    }

    private static int CompareRating(string a, string b) =>
        string.CompareOrdinal(Take(a, RatingValueLength), Take(b, RatingValueLength));

    private static string Take(string value, int width) =>
        (value ?? string.Empty).PadRight(width)[..width];
}
