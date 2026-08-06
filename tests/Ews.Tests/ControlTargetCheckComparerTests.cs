using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlTargetCheckComparer"/>(【C原典】Fyss1k.c の sckcmp)の単体テスト。
/// </summary>
public sealed class ControlTargetCheckComparerTests
{
    private static ControlTargetCheckEntry E(short oiban, short kgyo = 0, short kket = 0)
        => new() { DataSequence = oiban, DescriptionRow = kgyo, DescriptionColumn = kket };

    [Fact]
    public void 追番昇順で比較する()
    {
        Assert.True(ControlTargetCheckComparer.Instance.Compare(E(1), E(2)) < 0);
        Assert.True(ControlTargetCheckComparer.Instance.Compare(E(2), E(1)) > 0);
    }

    [Fact]
    public void 追番が同じなら記述行昇順()
    {
        Assert.True(ControlTargetCheckComparer.Instance.Compare(E(5, kgyo: 1), E(5, kgyo: 3)) < 0);
        Assert.True(ControlTargetCheckComparer.Instance.Compare(E(5, kgyo: 3), E(5, kgyo: 1)) > 0);
    }

    [Fact]
    public void 追番と記述行が同じなら記述桁昇順()
    {
        Assert.True(ControlTargetCheckComparer.Instance.Compare(E(5, kgyo: 2, kket: 4), E(5, kgyo: 2, kket: 7)) < 0);
        Assert.True(ControlTargetCheckComparer.Instance.Compare(E(5, kgyo: 2, kket: 7), E(5, kgyo: 2, kket: 4)) > 0);
    }

    [Fact]
    public void 全項目一致なら0()
    {
        Assert.Equal(0, ControlTargetCheckComparer.Instance.Compare(E(5, kgyo: 2, kket: 4), E(5, kgyo: 2, kket: 4)));
    }

    [Fact]
    public void 差そのものを返す()
    {
        // 【C原典】ret = cmp1->oiban - cmp2->oiban。符号だけでなく差を返す。
        Assert.Equal(3, ControlTargetCheckComparer.Instance.Compare(E(8), E(5)));
        Assert.Equal(-3, ControlTargetCheckComparer.Instance.Compare(E(5), E(8)));
    }

    [Fact]
    public void ListSortで追番_記述行_記述桁の順に整列する()
    {
        var list = new List<ControlTargetCheckEntry>
        {
            E(2, kgyo: 1, kket: 1),
            E(1, kgyo: 9, kket: 9),
            E(2, kgyo: 1, kket: 0),
            E(1, kgyo: 1, kket: 5),
        };

        list.Sort(ControlTargetCheckComparer.Instance);

        Assert.Equal(1, list[0].DataSequence);
        Assert.Equal(1, list[0].DescriptionRow);
        Assert.Equal(1, list[1].DataSequence);
        Assert.Equal(9, list[1].DescriptionRow);
        Assert.Equal(2, list[2].DataSequence);
        Assert.Equal(0, list[2].DescriptionColumn);
        Assert.Equal(2, list[3].DataSequence);
        Assert.Equal(1, list[3].DescriptionColumn);
    }
}
