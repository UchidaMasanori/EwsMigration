namespace Ews.Analysis;

/// <summary>
/// 回路解析で用いる数値変換・丸め処理。
///
/// 【C原典】
///   - libfysek.a の Fysk09 系関数(数値変換・丸め・調整・バイナリ出力)。
///     (toku/sekkei/src 配下。呼出元 FyskEwsMain.c / FySin80.c 等)
///
/// 定格容量や電流値の丸め(四捨五入)・単位調整など、回路解析の各所で共通利用される
/// 数値ユーティリティを集約する。
/// </summary>
public static class NumericConverter
{
    /// <summary>
    /// 指定桁での四捨五入(0.5 切り上げ)。
    /// 【C原典】Fysk09 の丸め処理。C では銀行丸めではなく一般的な四捨五入を行うため
    /// <see cref="MidpointRounding.AwayFromZero"/> を明示する。
    /// </summary>
    /// <param name="value">対象値。</param>
    /// <param name="digits">小数点以下桁数。</param>
    public static double RoundHalfUp(double value, int digits = 0)
        => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    /// <summary>
    /// C の固定小数文字列(例 ".999" 属性 = 暗黙小数)を double へ変換する。
    /// 【C原典】Fysk09 の文字列→数値変換。空白/非数値は <paramref name="defaultValue"/>。
    /// </summary>
    /// <param name="text">数値文字列。</param>
    /// <param name="implicitDecimals">暗黙の小数桁数(例 ".999" なら 3)。</param>
    /// <param name="defaultValue">変換不能時の既定値。</param>
    public static double ParseImplicitDecimal(string? text, int implicitDecimals, double defaultValue = 0d)
    {
        if (string.IsNullOrWhiteSpace(text) || !long.TryParse(text.Trim(), out long raw))
        {
            return defaultValue;
        }

        return raw / Math.Pow(10, implicitDecimals);
    }
}
