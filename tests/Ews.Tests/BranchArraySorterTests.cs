using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 分岐配列並べ替え(Fyss3C_Bunki_Sort)基盤ヘルパーのテスト。
/// </summary>
public class BranchArraySorterTests
{
    [Theory]
    [InlineData("MCB     ", BranchArraySorter.ReservedWordKind.MCB)]
    [InlineData("ELB     ", BranchArraySorter.ReservedWordKind.ELB)]
    [InlineData("2ERY    ", BranchArraySorter.ReservedWordKind.ERY2)]
    [InlineData("VVVF    ", BranchArraySorter.ReservedWordKind.VVVF)]
    [InlineData("P       ", BranchArraySorter.ReservedWordKind.P)]
    public void 予約語は識別子へ変換される(string yoyaku, BranchArraySorter.ReservedWordKind expected)
    {
        Assert.Equal(expected, BranchArraySorter.GetReservedWordKind(yoyaku));
    }

    [Fact]
    public void 短い予約語は8バイト右詰めで一致する()
    {
        Assert.Equal(BranchArraySorter.ReservedWordKind.MCB, BranchArraySorter.GetReservedWordKind("MCB"));
    }

    [Fact]
    public void 未知の予約語はNoneを返す()
    {
        Assert.Equal(BranchArraySorter.ReservedWordKind.None, BranchArraySorter.GetReservedWordKind("XXXX"));
    }

    [Fact]
    public void SetDecimalPointは末尾n桁の前に小数点を挿入する()
    {
        Assert.Equal("000.00", BranchArraySorter.SetDecimalPoint("00000", 2));
    }

    [Fact]
    public void SetDecimalPointはn以上の長さで先頭に0点を付す()
    {
        Assert.Equal("0.00000", BranchArraySorter.SetDecimalPoint("00000", 5));
    }

    [Fact]
    public void SetDecimalPointはn0以下で無変更()
    {
        Assert.Equal("00000", BranchArraySorter.SetDecimalPoint("00000", 0));
        Assert.Equal("00000", BranchArraySorter.SetDecimalPoint("00000", -1));
    }

    [Fact]
    public void FormatFixedWidthは幅3の0詰めにする()
    {
        Assert.Equal("007", BranchArraySorter.FormatFixedWidth(7, 3));
        Assert.Equal("123", BranchArraySorter.FormatFixedWidth(123, 3));
    }

    [Fact]
    public void FormatFixedWidthは超過時に先頭幅分を切り出す()
    {
        Assert.Equal("123", BranchArraySorter.FormatFixedWidth(12345, 3));
    }
}
