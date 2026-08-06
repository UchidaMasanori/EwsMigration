using System.Collections.Generic;
using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlEquipmentScanner"/>(【C原典】Fyss1k.c の CheckRmKiki / CheckTenmetu)の単体テスト。
/// </summary>
public sealed class ControlEquipmentScannerTests
{
    [Fact]
    public void CountRemoteControlEquipment_キーワードを含むエントリを数える()
    {
        // RSW/TU/CU/PT のいずれかを部分一致で含むものを 1 件ずつ数える。
        var words = new List<string?> { "RSW", "24TU", "MCCU5", "PT", "MC" };
        Assert.Equal(4, ControlEquipmentScanner.CountRemoteControlEquipment(words));
    }

    [Fact]
    public void CountRemoteControlEquipment_複数キーワード一致でも1件()
    {
        // 1 エントリに複数キーワードが含まれても最初の一致で break するため 1 件。
        var words = new List<string?> { "RSWTUCUPT" };
        Assert.Equal(1, ControlEquipmentScanner.CountRemoteControlEquipment(words));
    }

    [Fact]
    public void CountRemoteControlEquipment_該当無しは0()
    {
        var words = new List<string?> { "MC", "MG", "THR", "" };
        Assert.Equal(0, ControlEquipmentScanner.CountRemoteControlEquipment(words));
    }

    [Fact]
    public void CountRemoteControlEquipment_null要素は空文字扱いで非該当()
    {
        var words = new List<string?> { null, "PT" };
        Assert.Equal(1, ControlEquipmentScanner.CountRemoteControlEquipment(words));
    }

    [Fact]
    public void CountRemoteControlEquipment_空列は0()
    {
        Assert.Equal(0, ControlEquipmentScanner.CountRemoteControlEquipment(new List<string?>()));
    }

    [Fact]
    public void CountAutoFlashEquipment_キーワードを含むエントリを数える()
    {
        // TSU/SSWU のいずれかを部分一致で含むものを数える。
        var words = new List<string?> { "TSU", "1SSWU2", "TSUSSWU", "MC" };
        Assert.Equal(3, ControlEquipmentScanner.CountAutoFlashEquipment(words));
    }

    [Fact]
    public void CountAutoFlashEquipment_該当無しは0()
    {
        var words = new List<string?> { "MC", "PT", "RSW", null };
        Assert.Equal(0, ControlEquipmentScanner.CountAutoFlashEquipment(words));
    }

    [Fact]
    public void CountAutoFlashEquipment_複数キーワード一致でも1件()
    {
        var words = new List<string?> { "TSUSSWU" };
        Assert.Equal(1, ControlEquipmentScanner.CountAutoFlashEquipment(words));
    }
}
