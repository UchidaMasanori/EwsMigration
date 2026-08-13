namespace Ews.Tests;

using Ews.Analysis;
using Xunit;

/// <summary>
/// <see cref="PartNameChecker"/>(=Fysk01_Check_Hinmei)の移植テスト。
/// </summary>
public sealed class PartNameCheckerTests
{
    [Fact]
    public void 入力品名が空白なら参照が異なっても一致を返す()
    {
        Assert.True(PartNameChecker.Matches("          ", "MCB2P50AF20AT"));
    }

    [Fact]
    public void 入力品名と参照品名が完全一致なら一致を返す()
    {
        Assert.True(PartNameChecker.Matches("MCB2P50AF20AT", "MCB2P50AF20AT"));
    }

    [Fact]
    public void 先頭25文字が異なれば不一致を返す()
    {
        Assert.False(PartNameChecker.Matches("MCB2P50AF20AT", "MCB2P75AF30AT"));
    }

    [Fact]
    public void 二十六文字目以降の相違は無視して一致を返す()
    {
        // 先頭25文字は同一、26文字目のみ相違
        string common = new string('A', 25);
        Assert.True(PartNameChecker.Matches(common + "X", common + "Y"));
    }

    [Fact]
    public void 末尾空白差は同一とみなす()
    {
        Assert.True(PartNameChecker.Matches("ABC", "ABC       "));
    }

    [Fact]
    public void 入力先頭10文字が空白なら後続に文字があってもチェックせず一致を返す()
    {
        // 空白判定は先頭10文字のみ。先頭10空白なら11文字目以降を問わずGOOD
        Assert.True(PartNameChecker.Matches("          ABC", "          XYZ"));
    }
}
