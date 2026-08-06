using System.Collections.Generic;
using System.Linq;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlInterlockKeyBuilder"/>(【C原典】Fyss1k.c の setInterlockToSkey)の単体テスト。
/// </summary>
public sealed class ControlInterlockKeyBuilderTests
{
    private static ControlEquipmentEntry Kiki(string yoyaku, short nkosu = 0)
        => new() { ReservedWord = yoyaku, InternalCount = nkosu };

    [Fact]
    public void 山括弧が無ければ何も追加しない()
    {
        var list = new List<ControlEquipmentEntry> { Kiki("MC", 1) };
        ControlInterlockKeyBuilder.AppendInterlockKeys("MC*2", list);
        Assert.Single(list);
    }

    [Fact]
    public void THRを追加する()
    {
        var list = new List<ControlEquipmentEntry>();
        ControlInterlockKeyBuilder.AppendInterlockKeys("<THR", list);
        Assert.Single(list);
        Assert.Equal("<THR", list[0].ReservedWord);
        Assert.Equal(1, list[0].InternalCount);
    }

    [Fact]
    public void 既にTHRがあれば追加しない()
    {
        var list = new List<ControlEquipmentEntry> { Kiki("<THR", 1) };
        ControlInterlockKeyBuilder.AppendInterlockKeys("<THR", list);
        Assert.Single(list);
    }

    [Fact]
    public void ALをカンマ無しで追加する()
    {
        var list = new List<ControlEquipmentEntry>();
        ControlInterlockKeyBuilder.AppendInterlockKeys("<AL", list);
        Assert.Single(list);
        Assert.Equal("<AL", list[0].ReservedWord);
        Assert.Equal(1, list[0].InternalCount);
    }

    [Fact]
    public void ALの後にカンマがあれば追加しない()
    {
        var list = new List<ControlEquipmentEntry>();
        ControlInterlockKeyBuilder.AppendInterlockKeys("<AL,B", list);
        Assert.Empty(list);
    }

    [Fact]
    public void 既にALがあれば追加しない()
    {
        var list = new List<ControlEquipmentEntry> { Kiki("<AL", 1) };
        ControlInterlockKeyBuilder.AppendInterlockKeys("<AL", list);
        Assert.Single(list);
    }

    [Fact]
    public void CR記述が2個以上でCRを個数マイナス1で追加する()
    {
        var list = new List<ControlEquipmentEntry>();
        ControlInterlockKeyBuilder.AppendInterlockKeys("<CR<CR", list);
        Assert.Single(list);
        Assert.Equal("<CR", list[0].ReservedWord);
        Assert.Equal(1, list[0].InternalCount);
    }

    [Fact]
    public void CR記述が3個ならCR個数は2()
    {
        var list = new List<ControlEquipmentEntry>();
        ControlInterlockKeyBuilder.AppendInterlockKeys("<X<Y<Z", list);
        Assert.Single(list);
        Assert.Equal("<CR", list[0].ReservedWord);
        Assert.Equal(2, list[0].InternalCount);
    }

    [Fact]
    public void CR記述が1個ならCRを追加しない()
    {
        var list = new List<ControlEquipmentEntry>();
        ControlInterlockKeyBuilder.AppendInterlockKeys("<X", list);
        Assert.Empty(list);
    }

    [Fact]
    public void THRとCR2個を混在で両方追加する()
    {
        // <THR は THR/AL 以外の '<' ではないので CR 対象外。<X と <Y が CR 2 個。
        var list = new List<ControlEquipmentEntry>();
        ControlInterlockKeyBuilder.AppendInterlockKeys("<THR<X<Y", list);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, e => e.ReservedWord == "<THR" && e.InternalCount == 1);
        Assert.Contains(list, e => e.ReservedWord == "<CR" && e.InternalCount == 1);
    }

    [Fact]
    public void THRが未設定時は複数THRを各回追加する()
    {
        // thr_flg はループ内で更新されない(C原典の忠実再現)ため 2 個追加される。
        var list = new List<ControlEquipmentEntry>();
        ControlInterlockKeyBuilder.AppendInterlockKeys("<THR<THR", list);
        Assert.Equal(2, list.Count(e => e.ReservedWord == "<THR"));
    }
}
