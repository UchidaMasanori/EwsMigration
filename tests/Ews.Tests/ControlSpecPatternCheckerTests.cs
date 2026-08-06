using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlSpecPatternChecker"/>(【C原典】Fyss1k.c の PropChkINVPtn)の単体テスト。
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
}
