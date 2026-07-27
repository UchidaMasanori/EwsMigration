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

    /// <summary>
    /// 10 の <paramref name="exponent"/> 乗を返す(桁合わせ係数)。
    /// 【C原典】Ketaawase(Fysk09.c:41)。<c>a=1; for(k=0;k&lt;abs(keta);k++) a*=10; if(keta&lt;0) a=1/a;</c>。
    /// 負値は 10 の負乗(1/10^|keta|)。
    /// </summary>
    /// <param name="exponent">桁数(負値可)。【C原典】keta。</param>
    public static double PowerOfTen(short exponent)
    {
        double a = 1.0;
        int n = Math.Abs((int)exponent);
        for (int k = 0; k < n; k++)
        {
            a *= 10.0;
        }

        if (exponent < 0)
        {
            a = 1.0 / a;
        }

        return a;
    }

    /// <summary>
    /// 切り上げ(正の無限大方向)。
    /// 【C原典】Kiriage(Fysk09.c:27)。<c>i=(INT)f; if(f-(DOUBLE)i&gt;0.0) i=i+1;</c>。
    /// 整数部(ゼロ方向切り捨て)に対し、小数部が正なら +1 する(数学的な天井関数と一致)。
    /// </summary>
    /// <param name="value">対象値。【C原典】f。</param>
    public static double Ceiling(double value)
    {
        double i = Math.Truncate(value);
        if (value - i > 0.0)
        {
            i += 1;
        }

        return i;
    }

    /// <summary>
    /// 切り捨て(ゼロ方向)。【C原典】Kirisute(Fysk09.c:34)。<c>return (INT)f;</c>。
    /// </summary>
    /// <param name="value">対象値。【C原典】f。</param>
    public static double Truncate(double value) => Math.Truncate(value);

    /// <summary>
    /// 数値文字列の小数点以下の余分な末尾ゼロ(と、それにより不要となる小数点)を除去する。
    /// 【C原典】Chousei(Fysk09.c:52)。例 "12.300"→"12.3" / "12.000"→"12" / "12.0"→"12"。
    /// C は '.' を含む前提(strchr の戻りを無条件参照)のため、'.' が無い場合は原文をそのまま返す。
    /// </summary>
    /// <param name="text">数値文字列。【C原典】str。</param>
    public static string TrimTrailingZeros(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!text.Contains('.', StringComparison.Ordinal))
        {
            return text;
        }

        int size = text.Length;
        int cut = size;
        for (int i = 0; i < size; i++)
        {
            char c = text[size - 1 - i];
            if (c == '0')
            {
                cut = size - 1 - i;
            }
            else if (c == '.' && cut == size - i)
            {
                cut = size - 1 - i;
                break;
            }
            else
            {
                break;
            }
        }

        return text[..cut];
    }
}
