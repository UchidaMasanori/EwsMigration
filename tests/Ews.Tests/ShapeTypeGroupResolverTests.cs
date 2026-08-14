using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ShapeTypeGroupResolver"/>(=Get_Group)の移植テスト。
/// </summary>
public sealed class ShapeTypeGroupResolverTests
{
    [Fact]
    public void MCBのKYはグループL1を返す()
    {
        Assert.Equal(1, ShapeTypeGroupResolver.Resolve("MCB   ", "KY  "));
    }

    [Fact]
    public void MCBのKMはグループM2を返す()
    {
        Assert.Equal(2, ShapeTypeGroupResolver.Resolve("MCB   ", "KM  "));
    }

    [Fact]
    public void MCBのSTはグループH3を返す()
    {
        Assert.Equal(3, ShapeTypeGroupResolver.Resolve("MCB   ", "ST  "));
    }

    [Fact]
    public void ELB固有のJIはグループL1を返す()
    {
        Assert.Equal(1, ShapeTypeGroupResolver.Resolve("ELB   ", "JI  "));
    }

    [Fact]
    public void ワイルドカードALLのSBはタイプに依らずグループL1を返す()
    {
        Assert.Equal(1, ShapeTypeGroupResolver.Resolve("SB    ", "XX  "));
    }

    [Fact]
    public void ワイルドカードALLのRMCBはグループM2を返す()
    {
        Assert.Equal(2, ShapeTypeGroupResolver.Resolve("RMCB  ", "ANY "));
    }

    [Fact]
    public void 予約語がテーブルに無ければ0を返す()
    {
        Assert.Equal(0, ShapeTypeGroupResolver.Resolve("XXXX  ", "KY  "));
    }

    [Fact]
    public void 予約語一致でも形状タイプ非該当は0を返す()
    {
        Assert.Equal(0, ShapeTypeGroupResolver.Resolve("MCB   ", "ZZ  "));
    }

    [Fact]
    public void 予約語は先頭6バイトで照合する()
    {
        // "MCB   " の6バイトに続く文字は無視される。
        Assert.Equal(1, ShapeTypeGroupResolver.Resolve("MCB   X", "KY  "));
    }

    [Fact]
    public void 形状タイプは先頭4バイトで照合する()
    {
        // "KY  " の4バイトに続く文字は無視される。
        Assert.Equal(1, ShapeTypeGroupResolver.Resolve("MCB   ", "KY  Z"));
    }

    [Fact]
    public void CPのETはグループM2を返す()
    {
        Assert.Equal(2, ShapeTypeGroupResolver.Resolve("CP    ", "ET  "));
    }
}
