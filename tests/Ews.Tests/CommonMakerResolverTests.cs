using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CommonMakerResolver"/>(【C原典】Get_Kyotu_Maker)の単体テスト。
/// </summary>
public class CommonMakerResolverTests
{
    private static MakerDesignation Maker(string reserved, params string[] codes)
    {
        var m = new MakerDesignation { ReservedWord = reserved };
        for (int i = 0; i < codes.Length && i < MakerDesignation.MakerCodeCount; i++)
        {
            m.MakerCodes[i] = codes[i];
        }
        return m;
    }

    [Fact]
    public void ResolveCommonMakers_LGRとZCTの共通メーカーを抽出する()
    {
        var makers = new List<MakerDesignation>
        {
            Maker("LGR ", "TS ", "M  "),
            Maker("ZCT ", "M  ", "TS "),
        };

        IReadOnlyList<string> common = CommonMakerResolver.ResolveCommonMakers(makers);

        Assert.Equal(new[] { "TS ", "M  " }, common);
    }

    [Fact]
    public void ResolveCommonMakers_共通が無ければ空()
    {
        var makers = new List<MakerDesignation>
        {
            Maker("LGR ", "TS ", "M  "),
            Maker("ZCT ", "H  ", "F  "),
        };

        IReadOnlyList<string> common = CommonMakerResolver.ResolveCommonMakers(makers);

        Assert.Empty(common);
    }

    [Fact]
    public void ResolveCommonMakers_LGRが無ければ空()
    {
        var makers = new List<MakerDesignation>
        {
            Maker("ZCT ", "M  ", "TS "),
        };

        IReadOnlyList<string> common = CommonMakerResolver.ResolveCommonMakers(makers);

        Assert.Empty(common);
    }

    [Fact]
    public void ResolveCommonMakers_LGRが複数なら後勝ち()
    {
        var makers = new List<MakerDesignation>
        {
            Maker("LGR ", "H  "),          // 先に無関係のメーカー
            Maker("LGR ", "M  "),          // 後勝ちでこちらが有効
            Maker("ZCT ", "M  "),
        };

        IReadOnlyList<string> common = CommonMakerResolver.ResolveCommonMakers(makers);

        Assert.Equal(new[] { "M  " }, common);
    }

    [Fact]
    public void ResolveCommonMakers_ZCTのi番目が空だと当該LGRは比較されない_C原典の添字癖()
    {
        // LGR は index0,1 に A,B。ZCT は index0 のみ B。
        // C 原典は内側 break 条件が tmpz[i][0]（外側 i）のため、i=1(LGR="B  ")で
        // tmpz[1] が空 → 即 break となり B は tmpz[0] の B と比較されず、共通判定されない。
        var makers = new List<MakerDesignation>
        {
            Maker("LGR ", "A  ", "B  "),
            Maker("ZCT ", "B  "),
        };

        IReadOnlyList<string> common = CommonMakerResolver.ResolveCommonMakers(makers);

        Assert.Empty(common);
    }

    [Fact]
    public void ResolveCommonMakers_ZCTのi番目が非空なら全jと比較する()
    {
        // LGR index0="B  "。ZCT index0="X  " index1="B  "。
        // i=0 では tmpz[0] 非空のため j=0..3 を全比較し、j=1 の "B  " と一致して採用。
        var makers = new List<MakerDesignation>
        {
            Maker("LGR ", "B  "),
            Maker("ZCT ", "X  ", "B  "),
        };

        IReadOnlyList<string> common = CommonMakerResolver.ResolveCommonMakers(makers);

        Assert.Equal(new[] { "B  " }, common);
    }
}
