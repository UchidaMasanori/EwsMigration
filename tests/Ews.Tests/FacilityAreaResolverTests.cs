using Ews.Data.Configuration;
using Ews.Domain.Configuration;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 地区グループ取得(FyGetFacGrp)の移植検証。
///
/// 【C原典】FyGetFacGrp / FyGetInterTbl(toku/lib/libfycom/getinterfdt.c)。
/// 地区情報定義ファイル interfdt.inf(カンマ区切り 6 項目/行)を解析し、地区コード →
/// 地区グループのテーブルを引く。未定義/情報無しは本社地区(5)を返す。
///
/// 実 interfdt.inf(TOKUD/interfdt.inf)と同一書式の合成テキストで、パーサと検索を検証する。
/// </summary>
public sealed class FacilityAreaResolverTests
{
    // 実 interfdt.inf(TOKUD/interfdt.inf)から抜粋した代表行(コメント/空行/全角/前後空白/英数字コード含む)。
    private const string SampleInterfdt =
        "# <<<<<  地区情報  >>>>>\n" +
        "#\n" +
        "# 地区ｸﾞﾙｰﾌﾟ : 1:札幌 2:筑波 3:相模 4:水俣 5:本社地区\n" +
        "#\n" +
        "  01212   ,図面センター, hg_svr1    , 0       , 5          , hg_svr1       ,\n" +
        "  74000   ,札幌工場　　, sa_svr1    , 1       , 1          , sa_svr1       ,\n" +
        "  75007   ,相模原製作所, sag_svr1   , 1       , 2          , sag_svr1      ,\n" +
        "  76004   ,水俣工場　　, mi_svr1    , 1       , 4          , mi_svr1       ,\n" +
        "  78007   ,暁第一工場　, ak_svr1    , 1       , 5          , ak_svr1       ,\n" +
        "  7600A   ,溝口氏  　　, mig1_1     , 1       , 4          , mi_svr1       ,\n" +
        "\n" +
        "  99999   ,情報システム, ews159     , 0       , 5          , ews159       ,\n";

    [Fact]
    public void Parse_地区コードを地区グループへ対応付ける()
    {
        IFacilityAreaResolver resolver = new InMemoryFacilityAreaResolver(
            InterfdtFacilityAreaLoader.Parse(SampleInterfdt));

        Assert.Equal(5, resolver.GetFacilityGroup("01212"));   // 図面センター
        Assert.Equal(1, resolver.GetFacilityGroup("74000"));   // 札幌工場
        Assert.Equal(2, resolver.GetFacilityGroup("75007"));   // 相模原製作所
        Assert.Equal(4, resolver.GetFacilityGroup("76004"));   // 水俣工場
        Assert.Equal(5, resolver.GetFacilityGroup("78007"));   // 暁第一工場
        Assert.Equal(4, resolver.GetFacilityGroup("7600A"));   // 英数字コード(Ordinal 一致)
        Assert.Equal(5, resolver.GetFacilityGroup("99999"));   // 空行の直後の行も取り込む
    }

    [Fact]
    public void Parse_コメント行と空行を読み飛ばす()
    {
        IReadOnlyList<FacilityAreaEntry> entries = InterfdtFacilityAreaLoader.Parse(SampleInterfdt);

        // コメント 4 行・空行 1 行を除いたデータ 7 行のみ。
        Assert.Equal(7, entries.Count);
    }

    [Fact]
    public void Parse_各項目の前後の空白とタブを除去する()
    {
        IReadOnlyList<FacilityAreaEntry> entries =
            InterfdtFacilityAreaLoader.Parse("\t 12345 \t,名称,svr,0,\t 3 \t,areasvr,\n");

        FacilityAreaEntry entry = Assert.Single(entries);
        Assert.Equal("12345", entry.ZoneCode);
        Assert.Equal(3, entry.FacilityGroup);
    }

    [Fact]
    public void Parse_カンマが6個未満の行は採用しない()
    {
        // カンマ 5 個(=6 項目、地区サーバーホスト名の後のカンマが無い)は C の 6 個目の
        // strchr が NULL になり不採用。
        IReadOnlyList<FacilityAreaEntry> entries =
            InterfdtFacilityAreaLoader.Parse("01212,図面センター,hg_svr1,0,5\n");

        Assert.Empty(entries);
    }

    [Fact]
    public void Parse_最大100件で打ち切る()
    {
        string many = string.Concat(Enumerable.Range(0, 150)
            .Select(i => $"{i:D5},name,svr,0,5,areasvr,\n"));

        IReadOnlyList<FacilityAreaEntry> entries = InterfdtFacilityAreaLoader.Parse(many);

        // 【C原典】TBL_MAX=100。
        Assert.Equal(100, entries.Count);
    }

    [Fact]
    public void Resolver_未定義の地区コードは本社地区5を返す()
    {
        IFacilityAreaResolver resolver = new InMemoryFacilityAreaResolver(
            InterfdtFacilityAreaLoader.Parse(SampleInterfdt));

        Assert.Equal(InMemoryFacilityAreaResolver.HomeAreaGroup, resolver.GetFacilityGroup("ZZZZZ"));
    }

    [Fact]
    public void Resolver_情報が無いとき本社地区5を返す()
    {
        IFacilityAreaResolver resolver = new InMemoryFacilityAreaResolver([]);

        Assert.Equal(5, resolver.GetFacilityGroup("01212"));
    }

    [Fact]
    public void Resolver_同一地区コードは先勝ちで採用する()
    {
        IFacilityAreaResolver resolver = new InMemoryFacilityAreaResolver(
        [
            new FacilityAreaEntry("01212", 1),
            new FacilityAreaEntry("01212", 4),
        ]);

        Assert.Equal(1, resolver.GetFacilityGroup("01212"));
    }
}
