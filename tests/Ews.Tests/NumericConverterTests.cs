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

    [Theory]
    [InlineData((short)0, 1.0)]
    [InlineData((short)1, 10.0)]
    [InlineData((short)3, 1000.0)]
    [InlineData((short)-1, 0.1)]
    [InlineData((short)-2, 0.01)]
    public void PowerOfTen_10の指定桁乗を返す(short keta, double expected)
    {
        Assert.Equal(expected, NumericConverter.PowerOfTen(keta), 10);
    }

    [Theory]
    [InlineData(2.0, 2.0)]     // 整数はそのまま
    [InlineData(2.1, 3.0)]     // 小数部が正 → +1
    [InlineData(2.9, 3.0)]
    [InlineData(-2.5, -2.0)]   // ゼロ方向切り捨て後、小数部は正でない → そのまま
    public void Ceiling_切り上げする(double f, double expected)
    {
        Assert.Equal(expected, NumericConverter.Ceiling(f));
    }

    [Theory]
    [InlineData(2.9, 2.0)]
    [InlineData(2.1, 2.0)]
    [InlineData(-2.9, -2.0)]   // ゼロ方向切り捨て
    public void Truncate_切り捨てする(double f, double expected)
    {
        Assert.Equal(expected, NumericConverter.Truncate(f));
    }

    [Theory]
    [InlineData("12.300", "12.3")]
    [InlineData("12.000", "12")]
    [InlineData("12.0", "12")]
    [InlineData("12.34", "12.34")]
    [InlineData("120", "120")]      // '.' 無しは原文のまま
    [InlineData("1.0", "1")]
    public void TrimTrailingZeros_末尾ゼロと不要な小数点を除去する(string input, string expected)
    {
        Assert.Equal(expected, NumericConverter.TrimTrailingZeros(input));
    }
}
