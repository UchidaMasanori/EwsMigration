using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 品名一致チェック(<see cref="ProductNameChecker"/>)の検証。
/// 【C原典】toku/sekkei/src/Fysk01.c Fysk01_Check_Hinmei(:4079)。
/// 品名未指定(先頭 10 桁が空白)なら絞り込みなしで GOOD、指定ありなら 25 桁一致判定。
/// 定数(fyrt808.h): GOOD=0 / NOGOOD=1。
/// </summary>
public sealed class ProductNameCheckerTests
{
    [Fact]
    public void Check_品名未指定なら常にGOODを返す()
    {
        // 指定品名が空 → 候補が何であっても絞り込みなし=GOOD。
        Assert.Equal(ProductNameChecker.Good, ProductNameChecker.Check(string.Empty, "ABC123"));
        Assert.Equal(ProductNameChecker.Good, ProductNameChecker.Check(null, "ABC123"));
    }

    [Fact]
    public void Check_先頭10桁が空白なら品名未指定扱いでGOODを返す()
    {
        // 先頭 10 桁が空白なら memcmp(hinmi,"          ",10)==0 で未指定扱い。
        Assert.Equal(ProductNameChecker.Good, ProductNameChecker.Check("          ABC", "XYZ"));
    }

    [Fact]
    public void Check_品名一致ならGOODを返す()
    {
        // 25 桁右詰め比較で一致(末尾空白差は無視)。
        Assert.Equal(ProductNameChecker.Good, ProductNameChecker.Check("BW50AAG", "BW50AAG"));
    }

    [Fact]
    public void Check_品名不一致ならNOGOODを返す()
    {
        Assert.Equal(ProductNameChecker.NoGood, ProductNameChecker.Check("BW50AAG", "BW32AAG"));
    }

    [Fact]
    public void Check_25桁を超える部分は切り捨てて比較する()
    {
        // 先頭 25 桁が同一なら 26 桁目以降の差は無視される(固定長 memcmp 相当)。
        string a = "ABCDEFGHIJKLMNOPQRSTUVWXY" + "1";
        string b = "ABCDEFGHIJKLMNOPQRSTUVWXY" + "2";
        Assert.Equal(ProductNameChecker.Good, ProductNameChecker.Check(a, b));
    }
}
