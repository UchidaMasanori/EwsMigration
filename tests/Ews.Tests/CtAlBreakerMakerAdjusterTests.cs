using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Configuration;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CtAlBreakerMakerAdjuster"/>(【C原典】PropChgCTALMaker)の単体テスト。
/// </summary>
public class CtAlBreakerMakerAdjusterTests
{
    private static IRuntimeParameterProvider Zone(string zoneCode)
        => new InMemoryRuntimeParameterProvider(new Dictionary<string, string?>
        {
            [RuntimeParameterNames.ZoneCode] = zoneCode,
        });

    private static MainCircuitData Circuit(string reservedWord, string dataType0, string dataType2, string makerCode)
    {
        var dt = new MainCircuitData { ReservedWord = reservedWord };
        dt.DataType[0] = dataType0;
        dt.DataType[2] = dataType2;
        dt.AttachedParameter.MakerCode = makerCode;
        return dt;
    }

    [Fact]
    public void 暁工場3F_CT_AL_MCB_メーカー無指定は三菱に強制()
    {
        IReadOnlyList<string> result = CtAlBreakerMakerAdjuster.AdjustMakerCodes(
            Zone("78007"),
            Circuit("MCB", "CT", "AL", ""),
            ["F  ", "T  ", "N  "]);

        Assert.Equal(["M  "], result);
    }

    [Fact]
    public void 予約語ELBも対象()
    {
        IReadOnlyList<string> result = CtAlBreakerMakerAdjuster.AdjustMakerCodes(
            Zone("78007"),
            Circuit("ELB", "CT", "AL", ""),
            ["F  "]);

        Assert.Equal(["M  "], result);
    }

    [Fact]
    public void 暁工場3F以外は変更しない()
    {
        IReadOnlyList<string> input = ["F  ", "T  "];

        IReadOnlyList<string> result = CtAlBreakerMakerAdjuster.AdjustMakerCodes(
            Zone("10001"),
            Circuit("MCB", "CT", "AL", ""),
            input);

        Assert.Same(input, result);
    }

    [Fact]
    public void ゾーンコード未設定は変更しない()
    {
        IReadOnlyList<string> input = ["F  "];

        IReadOnlyList<string> result = CtAlBreakerMakerAdjuster.AdjustMakerCodes(
            new InMemoryRuntimeParameterProvider(new Dictionary<string, string?>()),
            Circuit("MCB", "CT", "AL", ""),
            input);

        Assert.Same(input, result);
    }

    [Fact]
    public void AL以外は変更しない()
    {
        IReadOnlyList<string> input = ["F  "];

        IReadOnlyList<string> result = CtAlBreakerMakerAdjuster.AdjustMakerCodes(
            Zone("78007"),
            Circuit("MCB", "CT", "AX", ""),
            input);

        Assert.Same(input, result);
    }

    [Fact]
    public void CT以外は変更しない()
    {
        IReadOnlyList<string> input = ["F  "];

        IReadOnlyList<string> result = CtAlBreakerMakerAdjuster.AdjustMakerCodes(
            Zone("78007"),
            Circuit("MCB", "VT", "AL", ""),
            input);

        Assert.Same(input, result);
    }

    [Fact]
    public void メーカー指定ありは変更しない()
    {
        IReadOnlyList<string> input = ["F  "];

        IReadOnlyList<string> result = CtAlBreakerMakerAdjuster.AdjustMakerCodes(
            Zone("78007"),
            Circuit("MCB", "CT", "AL", "F"),
            input);

        Assert.Same(input, result);
    }

    [Fact]
    public void 予約語がMCBELB以外は変更しない()
    {
        IReadOnlyList<string> input = ["F  "];

        IReadOnlyList<string> result = CtAlBreakerMakerAdjuster.AdjustMakerCodes(
            Zone("78007"),
            Circuit("CT", "CT", "AL", ""),
            input);

        Assert.Same(input, result);
    }
}
