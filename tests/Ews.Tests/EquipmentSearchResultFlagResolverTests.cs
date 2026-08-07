namespace Ews.Tests;

using Ews.Analysis;
using Xunit;

/// <summary>
/// <see cref="EquipmentSearchResultFlagResolver"/>(=Fysk01_Get_Errflg)の移植テスト。
/// </summary>
public sealed class EquipmentSearchResultFlagResolverTests
{
    [Theory]
    [InlineData((short)1, "  ", (short)1, false)]
    [InlineData((short)2, "E1", (short)1, true)]
    [InlineData((short)3, "  ", (short)2, false)]
    [InlineData((short)4, "E2", (short)2, true)]
    [InlineData((short)5, "  ", (short)2, false)]
    [InlineData((short)6, "E3", (short)2, true)]
    [InlineData((short)7, "  ", (short)1, false)]
    [InlineData((short)8, "E3", (short)1, true)]
    public void エラー番号ごとにフラグと電気パラメータ番号とサーチ要否を返す(
        short errorNumber, string expectedFlag, short expectedParameterNumber, bool expectedSkip)
    {
        EquipmentSearchResultFlag? result = EquipmentSearchResultFlagResolver.Resolve(errorNumber);

        Assert.NotNull(result);
        Assert.Equal(expectedFlag, result!.Flag);
        Assert.Equal(expectedParameterNumber, result.ParameterNumber);
        Assert.Equal(expectedSkip, result.SkipSearch);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)9)]
    [InlineData((short)-1)]
    public void 範囲外のエラー番号はnullを返す(short errorNumber)
    {
        Assert.Null(EquipmentSearchResultFlagResolver.Resolve(errorNumber));
    }
}
