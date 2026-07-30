using Ews.Data.Seeding;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 営業所コード識別テーブル(eigyocd.cns)/三菱製WH優先営業所テーブル(whm_sentei.cns)の
/// ローダ移植検証。【C原典】PropChkHibknNum(Fysk00.c:6130)のファイル読込部。
/// </summary>
public sealed class WhmPriorityTableLoaderTests
{
    [Fact]
    public void eigyocdはコメント行を無視し営業所コードを取り込む()
    {
        string content = string.Join("\r\n",
            "/* header comment */",
            "   AB,     ,     ,     ,     ,   TE,   TJ,   AK,",
            "   AC,     ,     ,     ,     ,   AR,   AP,");

        IReadOnlyList<NonPropertyOfficeEntry> entries = EigyocdTableLoader.Parse(content);

        Assert.Equal(2, entries.Count);
        Assert.Equal("AB", entries[0].NonPropertyCode);
        Assert.Contains("TE", entries[0].OfficeCodes);
        Assert.Contains("AK", entries[0].OfficeCodes);
        Assert.Equal("AC", entries[1].NonPropertyCode);
        Assert.Contains("AR", entries[1].OfficeCodes);
    }

    [Fact]
    public void eigyocdは従来欄の空白フィールドを営業所コードに含めない()
    {
        string content = "   AB,     ,     ,     ,     ,   TE,";

        IReadOnlyList<NonPropertyOfficeEntry> entries = EigyocdTableLoader.Parse(content);

        Assert.Single(entries);
        Assert.Single(entries[0].OfficeCodes);   // "TE" のみ(空白欄は除外)
        Assert.Equal("TE", entries[0].OfficeCodes[0]);
    }

    [Fact]
    public void 行頭が空白でコメントアウトされていない行はデータとして扱う()
    {
        // C 原典は strncmp(buf,"/*",2) のみで判定するため、行頭空白の行はコメントにならない。
        string content = string.Join("\r\n",
            "/*   AO,     ,     ,     ,     ,   AA,",
            "   AY,     ,     ,     ,     ,   QM,   VE,");

        IReadOnlyList<NonPropertyOfficeEntry> entries = EigyocdTableLoader.Parse(content);

        Assert.Single(entries);
        Assert.Equal("AY", entries[0].NonPropertyCode);
    }

    [Fact]
    public void whmSenteiは全行コメントなら空一覧を返す()
    {
        string content = string.Join("\r\n",
            "/* 非物件コード,営業所コード,営業署名 */",
            "/* CG,00316,東京第１,    */",
            "/* CE,00314,さいたま,    */");

        IReadOnlyList<string> codes = WhmSenteiTableLoader.Parse(content);

        Assert.Empty(codes);
    }

    [Fact]
    public void whmSenteiは有効行の先頭を非物件コードとして取り込む()
    {
        string content = string.Join("\r\n",
            "/* comment */",
            "CG,00316,東京第１,",
            "CE,00314,さいたま,");

        IReadOnlyList<string> codes = WhmSenteiTableLoader.Parse(content);

        Assert.Equal(["CG", "CE"], codes);
    }
}
