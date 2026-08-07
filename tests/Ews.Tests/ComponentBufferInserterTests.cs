namespace Ews.Tests;

using System.Collections.Generic;
using Ews.Analysis;
using Xunit;

/// <summary>
/// <see cref="ComponentBufferInserter"/>(=Fysk01_Mem_Control)の移植テスト。
/// 構成機器エリアへのソート挿入(割り込み)の下請け。
/// </summary>
public sealed class ComponentBufferInserterTests
{
    [Fact]
    public void 先頭へ割り込むと以降が後方へずれる()
    {
        var buffer = new List<int> { 10, 20, 30 };

        ComponentBufferInserter.Insert(buffer, 5, 0, buffer.Count);

        Assert.Equal(new[] { 5, 10, 20, 30 }, buffer);
    }

    [Fact]
    public void 中間へ割り込むと当該位置以降だけがずれる()
    {
        var buffer = new List<int> { 10, 20, 30, 40 };

        ComponentBufferInserter.Insert(buffer, 25, 2, buffer.Count);

        Assert.Equal(new[] { 10, 20, 25, 30, 40 }, buffer);
    }

    [Fact]
    public void 末尾位置への割り込みは追記になる()
    {
        var buffer = new List<int> { 10, 20, 30 };

        ComponentBufferInserter.Insert(buffer, 40, buffer.Count, buffer.Count);

        Assert.Equal(new[] { 10, 20, 30, 40 }, buffer);
    }

    [Fact]
    public void 空バッファへの割り込みは1件目の格納になる()
    {
        var buffer = new List<int>();

        ComponentBufferInserter.Insert(buffer, 99, 0, buffer.Count);

        Assert.Equal(new[] { 99 }, buffer);
    }

    [Fact]
    public void 件数が1件増える()
    {
        var buffer = new List<string> { "A", "B" };

        ComponentBufferInserter.Insert(buffer, "X", 1, buffer.Count);

        Assert.Equal(3, buffer.Count);
        Assert.Equal(new[] { "A", "X", "B" }, buffer);
    }

    [Fact]
    public void 参照型でも既存要素の同一性が保たれる()
    {
        var a = new object();
        var b = new object();
        var x = new object();
        var buffer = new List<object> { a, b };

        ComponentBufferInserter.Insert(buffer, x, 1, buffer.Count);

        Assert.Same(a, buffer[0]);
        Assert.Same(x, buffer[1]);
        Assert.Same(b, buffer[2]);
    }
}
