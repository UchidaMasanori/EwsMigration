using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlSpecPatternChecker"/>(【C原典】Fyss1k.c の PropChkINVPtn / PropChkKiKiPtn)の単体テスト。
/// </summary>
public sealed class ControlSpecPatternCheckerTests
{
    [Theory]
    [InlineData("OL<INV", 3)]       // OL<INV パターン → PTN=03
    [InlineData("SOL<INV", 3)]      // OL は部分一致(strstr 相当)でも該当
    [InlineData("MC3", 0)]          // インターロック指定なし
    [InlineData("OL<THR", 0)]       // インターロックが INV でない
    [InlineData("OL:MC<INV", 0)]    // 制御対象機器(':' 前)あり
    [InlineData("OL,RL<INV", 0)]    // インターロック前の機器が複数種類
    [InlineData("OL<INV,MC", 0)]    // インターロック後続で除去後に ',' が残る
    [InlineData("RL<INV", 0)]       // インターロック前が OL でない
    [InlineData("", 0)]             // 空
    [InlineData(null, 0)]           // null
    public void CheckInvPattern_OLとINVの単独パターンのみ3(string? controlSpecText, int expected)
    {
        Assert.Equal(expected, ControlSpecPatternChecker.CheckInvPattern(controlSpecText));
    }

    private static Ews.Domain.Analysis.EquipmentTableEntry Kiki(short group, string yoyaku, short kosu)
    {
        return new Ews.Domain.Analysis.EquipmentTableEntry
        {
            GroupNumber = group,
            ReservedWord = yoyaku,
            Quantity = kosu,
        };
    }

    [Fact]
    public void CheckEquipmentPattern_全機器が特定パターンなら変更しない負1()
    {
        var kiki = new System.Collections.Generic.List<Ews.Domain.Analysis.EquipmentTableEntry>
        {
            Kiki(1, "RL", 2),
            Kiki(1, "GL", 2),
            Kiki(1, "CR", 0),
        };

        Assert.Equal(-1, ControlSpecPatternChecker.CheckEquipmentPattern("G3", kiki, "YOU", 1));
    }

    [Fact]
    public void CheckEquipmentPattern_対象機器がG3でなければ対象外0()
    {
        var kiki = new System.Collections.Generic.List<Ews.Domain.Analysis.EquipmentTableEntry> { Kiki(1, "RL", 2) };

        Assert.Equal(0, ControlSpecPatternChecker.CheckEquipmentPattern("MC", kiki, "YOU", 1));
    }

    [Fact]
    public void CheckEquipmentPattern_用途がYOUでなければ対象外0()
    {
        var kiki = new System.Collections.Generic.List<Ews.Domain.Analysis.EquipmentTableEntry> { Kiki(1, "RL", 2) };

        Assert.Equal(0, ControlSpecPatternChecker.CheckEquipmentPattern("G3", kiki, "MG", 1));
    }

    [Fact]
    public void CheckEquipmentPattern_個数が異なる機器があれば変更可0()
    {
        var kiki = new System.Collections.Generic.List<Ews.Domain.Analysis.EquipmentTableEntry>
        {
            Kiki(1, "RL", 2),
            Kiki(1, "GL", 3),   // GL は個数2でないと非該当
        };

        Assert.Equal(0, ControlSpecPatternChecker.CheckEquipmentPattern("G3", kiki, "YOU", 1));
    }

    [Fact]
    public void CheckEquipmentPattern_パターン外の予約語があれば変更可0()
    {
        var kiki = new System.Collections.Generic.List<Ews.Domain.Analysis.EquipmentTableEntry>
        {
            Kiki(1, "RL", 2),
            Kiki(1, "XX", 0),   // パターン表に無い予約語
        };

        Assert.Equal(0, ControlSpecPatternChecker.CheckEquipmentPattern("G3", kiki, "YOU", 1));
    }

    [Fact]
    public void CheckEquipmentPattern_別グループの機器は判定対象外()
    {
        var kiki = new System.Collections.Generic.List<Ews.Domain.Analysis.EquipmentTableEntry>
        {
            Kiki(1, "RL", 2),
            Kiki(2, "XX", 9),   // 行種グループ2は lineTypeGroup=1 の判定に含めない
        };

        Assert.Equal(-1, ControlSpecPatternChecker.CheckEquipmentPattern("G3", kiki, "YOU", 1));
    }

    [Fact]
    public void CheckEquipmentPattern_該当グループが空なら変更しない負1()
    {
        var kiki = new System.Collections.Generic.List<Ews.Domain.Analysis.EquipmentTableEntry> { Kiki(2, "RL", 2) };

        Assert.Equal(-1, ControlSpecPatternChecker.CheckEquipmentPattern("G3", kiki, "YOU", 1));
    }
}
