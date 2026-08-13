namespace Ews.Tests;

using Ews.Analysis;
using Xunit;

/// <summary>
/// <see cref="EquipmentSearchFlagResolver"/>(=Fysk01_Get_Errflg)の移植テスト。
/// </summary>
public sealed class EquipmentSearchFlagResolverTests
{
    [Theory]
    [InlineData(1, true, "  ", 1)]
    [InlineData(2, false, "E1", 1)]
    [InlineData(3, true, "  ", 2)]
    [InlineData(4, false, "E2", 2)]
    [InlineData(5, true, "  ", 2)]
    [InlineData(6, false, "E3", 2)]
    [InlineData(7, true, "  ", 1)]
    [InlineData(8, false, "E3", 1)]
    public void エラー番号ごとにフラグと電気パラメータ番号とサーチ要否を返す(
        int errorNumber, bool shouldSearch, string flag, int parameterNumber)
    {
        EquipmentSearchFlag result = EquipmentSearchFlagResolver.Resolve(errorNumber);

        Assert.Equal(shouldSearch, result.ShouldSearch);
        Assert.Equal(flag, result.Flag);
        Assert.Equal(parameterNumber, result.ParameterNumber);
    }

    [Fact]
    public void 定義外のエラー番号はサーチするを返す()
    {
        EquipmentSearchFlag result = EquipmentSearchFlagResolver.Resolve(99);

        Assert.True(result.ShouldSearch);
        Assert.Equal("  ", result.Flag);
        Assert.Equal(0, result.ParameterNumber);
    }
}
