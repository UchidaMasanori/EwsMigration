namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Masters;
using Xunit;

/// <summary>
/// <see cref="MakerCodeSelector"/>(=Fysk01_MakerCode_Check)の移植テスト。
/// </summary>
public sealed class MakerCodeSelectorTests
{
    private static MakerDesignation MakeMaker(string reservedWord, params string[] codes)
    {
        MakerDesignation m = new() { ReservedWord = reservedWord };
        for (int i = 0; i < codes.Length && i < MakerDesignation.MakerCodeCount; i++)
        {
            m.MakerCodes[i] = codes[i];
        }
        return m;
    }

    [Fact]
    public void 指定メーカーコードがあればそれを1件返す()
    {
        MakerCodeSelection r = MakerCodeSelector.Select("MCB     ", "M  ", []);

        Assert.Equal(1, r.Count);
        Assert.Equal("M  ", r.MakerCodes[0]);
    }

    [Fact]
    public void 予約語がL始まりなら空白1件を返す()
    {
        MakerCodeSelection r = MakerCodeSelector.Select("L       ", "   ",
            [MakeMaker("L       ", "M  ")]);

        Assert.Equal(1, r.Count);
        Assert.Equal("   ", r.MakerCodes[0]);
    }

    [Fact]
    public void 予約語がLGTなら空白1件を返す()
    {
        MakerCodeSelection r = MakerCodeSelector.Select("LGT     ", "   ",
            [MakeMaker("LGT     ", "M  ")]);

        Assert.Equal(1, r.Count);
        Assert.Equal("   ", r.MakerCodes[0]);
    }

    [Fact]
    public void 予約語一致行の空白以外mkcdを順位表へ展開する()
    {
        MakerCodeSelection r = MakerCodeSelector.Select("MCB     ", "   ",
            [MakeMaker("MCB     ", "M  ", "   ", "T  ", "   ")]);

        Assert.Equal(2, r.Count);
        Assert.Equal("M  ", r.MakerCodes[0]);
        Assert.Equal("T  ", r.MakerCodes[1]);
    }

    [Fact]
    public void mkcd4件全てを展開できる()
    {
        MakerCodeSelection r = MakerCodeSelector.Select("MCB     ", "   ",
            [MakeMaker("MCB     ", "M  ", "T  ", "F  ", "K  ")]);

        Assert.Equal(4, r.Count);
        Assert.Equal(["M  ", "T  ", "F  ", "K  "], r.MakerCodes);
    }

    [Fact]
    public void 予約語一致行が無ければ空白1件を返す()
    {
        MakerCodeSelection r = MakerCodeSelector.Select("MCB     ", "   ",
            [MakeMaker("ELB     ", "M  ")]);

        Assert.Equal(1, r.Count);
        Assert.Equal("   ", r.MakerCodes[0]);
    }

    [Fact]
    public void 一致行だが全mkcdが空白なら件数0を返す()
    {
        MakerCodeSelection r = MakerCodeSelector.Select("MCB     ", "   ",
            [MakeMaker("MCB     ")]);

        Assert.Equal(0, r.Count);
    }
}
