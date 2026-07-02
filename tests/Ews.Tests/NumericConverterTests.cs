using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 数値変換ユーティリティの検証。
/// 【C原典】libfysek.a の Fysk09(数値変換・丸め)。
/// </summary>
public sealed class NumericConverterTests
{
    [Theory]
    [InlineData(2.5, 0, 3)]   // 0.5 切り上げ(銀行丸めではない)
    [InlineData(3.5, 0, 4)]
    [InlineData(1.234, 2, 1.23)]
    public void RoundHalfUp_四捨五入する(double value, int digits, double expected)
    {
        Assert.Equal(expected, NumericConverter.RoundHalfUp(value, digits));
    }

    [Theory]
    [InlineData("123", 3, 0.123)]   // ".999" 属性(暗黙小数3桁)
    [InlineData("1500", 0, 1500)]
    [InlineData("   ", 3, 0)]        // 空白は既定値
    public void ParseImplicitDecimal_暗黙小数を解釈する(string text, int decimals, double expected)
    {
        Assert.Equal(expected, NumericConverter.ParseImplicitDecimal(text, decimals));
    }
}
