using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="LampDefaultTypeResolver"/>(【C原典】PropSetDefLampType)の単体テスト。
/// </summary>
public class LampDefaultTypeResolverTests
{
    [Fact]
    public void ResolveDefaultType_タイプ指定が無ければLED()
    {
        Assert.Equal("LED    ", LampDefaultTypeResolver.ResolveDefaultType("WL", "AN     "));
    }

    [Fact]
    public void ResolveDefaultType_括弧内にNPが無ければLED()
    {
        Assert.Equal("LED    ", LampDefaultTypeResolver.ResolveDefaultType("WL+(AX)", "AN     "));
    }

    [Fact]
    public void ResolveDefaultType_括弧内にNPが有れば現行タイプ据置()
    {
        Assert.Equal("AN     ", LampDefaultTypeResolver.ResolveDefaultType("WL+(NP)", "AN     "));
    }

    [Fact]
    public void ResolveDefaultType_とじ括弧無しでNPが無ければLED()
    {
        Assert.Equal("LED    ", LampDefaultTypeResolver.ResolveDefaultType("WL+(AX", "AN     "));
    }

    [Fact]
    public void ResolveDefaultType_とじ括弧無しでNPが有れば現行タイプ据置()
    {
        Assert.Equal("AN     ", LampDefaultTypeResolver.ResolveDefaultType("WL+(NP", "AN     "));
    }

    [Fact]
    public void ResolveDefaultType_とじ括弧より後のNPは切詰めで除外されLED()
    {
        // "+(AX)" で ')' 切詰め → 判定対象 "WL+(AX" に NP は含まれず LED。
        Assert.Equal("LED    ", LampDefaultTypeResolver.ResolveDefaultType("WL+(AX)NP", "AN     "));
    }

    [Fact]
    public void ResolveDefaultType_nullは空扱いでLED()
    {
        Assert.Equal("LED    ", LampDefaultTypeResolver.ResolveDefaultType(null, null));
    }
}
