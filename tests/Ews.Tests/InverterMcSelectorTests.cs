namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

/// <summary>
/// <see cref="InverterMcSelector"/>(=Fysk01_ChkInv_MC)の移植テスト。
/// </summary>
public sealed class InverterMcSelectorTests
{
    private static readonly IReadOnlyList<InverterMcConstant> Table =
    [
        new("MI-05SV3", 3.7, "MC-1"),
        new("MI-05SV3", 7.5, "MC-2"),
        new("MI-05SV3", 15.0, "MC-3"),
        new("MI-4SW3", 22.0, "MC-4"),
        new("MI-4SW3", 37.0, "MC-5"),
    ];

    [Fact]
    public void 入力kw以上となる最初の同タイプ行の品名を返す()
    {
        Assert.Equal("MC-2", InverterMcSelector.SelectProductName(Table, 5.5, "MI-05SV3"));
    }

    [Fact]
    public void 境界値は該当する()
    {
        Assert.Equal("MC-1", InverterMcSelector.SelectProductName(Table, 3.7, "MI-05SV3"));
    }

    [Fact]
    public void 別タイプは自タイプの帯だけを対象にする()
    {
        Assert.Equal("MC-4", InverterMcSelector.SelectProductName(Table, 20.0, "MI-4SW3"));
    }

    [Fact]
    public void 入力kwが該当タイプの最大を超えると見つからない()
    {
        Assert.Null(InverterMcSelector.SelectProductName(Table, 40.0, "MI-05SV3"));
    }

    [Fact]
    public void 該当タイプが存在しないと見つからない()
    {
        Assert.Null(InverterMcSelector.SelectProductName(Table, 5.0, "MI-99XX9"));
    }

    [Fact]
    public void タイプは先頭一致で判定する()
    {
        Assert.Equal("MC-1", InverterMcSelector.SelectProductName(Table, 3.7, "MI-05"));
    }

    [Fact]
    public void 空リストは見つからない()
    {
        Assert.Null(InverterMcSelector.SelectProductName([], 5.0, "MI-05SV3"));
    }
}
