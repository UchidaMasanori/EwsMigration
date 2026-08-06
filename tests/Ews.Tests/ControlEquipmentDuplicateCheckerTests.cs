using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlEquipmentDuplicateChecker"/>(【C原典】Fyss1k.c の PropIsGNdiff)の単体テスト。
/// </summary>
public sealed class ControlEquipmentDuplicateCheckerTests
{
    private static ControlEquipmentEntry Entry(short nkosu, short gkosu, string yoyaku = "MC")
        => new() { ReservedWord = yoyaku, InternalCount = nkosu, ExternalCount = gkosu };

    [Fact]
    public void 予約語が違えば別データ()
    {
        // ret != 0 → 1(別データ)。個数に関わらず。
        int r = ControlEquipmentDuplicateChecker.IsDifferentData(Entry(1, 0), Entry(0, 1), reservedWordCompare: -1);
        Assert.Equal(1, r);
    }

    [Fact]
    public void 予約語同じで両方内部機器なら別データ()
    {
        int r = ControlEquipmentDuplicateChecker.IsDifferentData(Entry(2, 0), Entry(3, 0), reservedWordCompare: 0);
        Assert.Equal(1, r);
    }

    [Fact]
    public void 予約語同じで両方外部機器なら別データ()
    {
        int r = ControlEquipmentDuplicateChecker.IsDifferentData(Entry(0, 2), Entry(0, 3), reservedWordCompare: 0);
        Assert.Equal(1, r);
    }

    [Fact]
    public void 予約語同じで内部と外部が混在なら重複データ()
    {
        // wk=内部のみ, other=外部のみ → 0(重複データ)。
        int r = ControlEquipmentDuplicateChecker.IsDifferentData(Entry(2, 0), Entry(0, 3), reservedWordCompare: 0);
        Assert.Equal(0, r);
    }

    [Fact]
    public void 予約語同じで両方個数ゼロなら重複データ()
    {
        int r = ControlEquipmentDuplicateChecker.IsDifferentData(Entry(0, 0), Entry(0, 0), reservedWordCompare: 0);
        Assert.Equal(0, r);
    }

    [Fact]
    public void 予約語同じで片方のみ内部機器あり他方ゼロなら重複データ()
    {
        // wk 内部あり・other 内部ゼロかつ外部ゼロ → 内部条件も外部条件も成立せず 0。
        int r = ControlEquipmentDuplicateChecker.IsDifferentData(Entry(2, 0), Entry(0, 0), reservedWordCompare: 0);
        Assert.Equal(0, r);
    }

    [Fact]
    public void 予約語一致でも内部同士は正_境界は個数1()
    {
        int r = ControlEquipmentDuplicateChecker.IsDifferentData(Entry(1, 0), Entry(1, 0), reservedWordCompare: 0);
        Assert.Equal(1, r);
    }
}
