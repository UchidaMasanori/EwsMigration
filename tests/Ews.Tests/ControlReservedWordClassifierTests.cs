using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlReservedWordClassifier"/>(【C原典】Fyss1k.c の予約語分類・インターロック判定関数群)の単体テスト。
/// </summary>
public sealed class ControlReservedWordClassifierTests
{
    [Theory]
    [InlineData("THR", 0)]
    [InlineData("2ERY", 0)]
    [InlineData("3ERY", 0)]
    [InlineData("4ERY", 0)]
    [InlineData("MC", 1)]
    [InlineData("", 1)]
    [InlineData(null, 1)]
    public void CheckMgReservedWord_MG機器のみ該当0(string? data, int expected)
    {
        Assert.Equal(expected, ControlReservedWordClassifier.CheckMgReservedWord(data));
    }

    [Theory]
    [InlineData("PT", 0)]
    [InlineData("RRY", 0)]
    [InlineData("RELB", 0)]
    [InlineData("RMMCB", 0)]
    [InlineData("RMCB", 0)]
    [InlineData("RELMB", 0)]
    [InlineData("MC", 1)]
    [InlineData("PTX", 1)]
    public void CheckRemoteReservedWord_リモコン機器のみ該当0(string? data, int expected)
    {
        Assert.Equal(expected, ControlReservedWordClassifier.CheckRemoteReservedWord(data));
    }

    [Theory]
    [InlineData("MC", 0)]
    [InlineData("MG", 0)]
    [InlineData("MCDT", 0)]
    [InlineData("MGLD", 0)]
    [InlineData("INV", 0)]
    [InlineData("MGFRSD", 0)]
    [InlineData("PT", 1)]
    [InlineData("", 1)]
    public void CheckControlTargetEquipment_制御対象機器のみ該当0(string? data, int expected)
    {
        Assert.Equal(expected, ControlReservedWordClassifier.CheckControlTargetEquipment(data));
    }

    [Theory]
    [InlineData("MC", 0, "MC")]
    [InlineData("MC", 1, "MG")]
    [InlineData("MG", 0, "MG")]
    [InlineData("MCFR", 0, "MCFR")]
    [InlineData("MCFR", 1, "MGFR")]
    [InlineData("MGFR", 1, "MGFR")]
    [InlineData("MCSD", 0, "MCSD")]
    [InlineData("MCSD", 1, "MGSD")]
    [InlineData("MCFRSD", 1, "MGFRSD")]
    [InlineData("MGFRSD", 0, "MGFRSD")]
    [InlineData("MCDT", 1, "MCDT")]
    [InlineData("INV", 0, "INV")]
    [InlineData("XXX", 1, "")]
    [InlineData(null, 0, "")]
    public void GetStartCircuitUsage_MG有無で用途を振り替える(string? equipment, int mgPresent, string expected)
    {
        Assert.Equal(expected, ControlReservedWordClassifier.GetStartCircuitUsage(equipment, mgPresent));
    }

    [Theory]
    [InlineData("MC3", 0)]                 // '<' 無し → 0
    [InlineData("", 0)]                    // 空 → 0
    [InlineData(null, 0)]                  // null → 0
    [InlineData("<THR", 0)]                // <THR のみ → 0
    [InlineData("<AL", 0)]                 // <AL のみ → 0
    [InlineData("A<THRB<ALC", 0)]         // 全て <THR/<AL → 0
    [InlineData("<CR", 1)]                 // 他の '<' → 1
    [InlineData("<THR<CR", 1)]            // 途中に他の '<' → 1
    [InlineData("<", 1)]                   // '<' 単独(範囲外) → 1
    [InlineData("<TH", 1)]                 // <TH で THR に満たない → 1
    public void CheckInterlock_THRとAL以外の記述はNG1(string? text, int expected)
    {
        Assert.Equal(expected, ControlReservedWordClassifier.CheckInterlock(text));
    }
}
