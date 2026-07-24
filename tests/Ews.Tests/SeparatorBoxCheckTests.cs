using Ews.Analysis;
using Ews.Data.Seeding;
using Ews.Domain.Masters;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// セパレータ(SEP)作図要否判定の検証。【C原典】Fyss12.c PropChkSEPBox / PropChkHbnHB300(改訂&lt;12&gt;)。
/// </summary>
public sealed class SeparatorBoxCheckTests
{
    private readonly ITestOutputHelper _output;

    public SeparatorBoxCheckTests(ITestOutputHelper output) => _output = output;

    private static PartNumberInfo Hbn(string inputPartNumber = "", string boxType = "", string generatedBox = "")
        => new()
        {
            InputPartNumber = inputPartNumber,
            BoxType = boxType,
            GeneratedBoxPartNumber = generatedBox,
        };

    [Theory]
    [InlineData("JBR", "00350", 0)]   // JBR + 350 → SEP作図あり
    [InlineData("JOC", "00350", 0)]   // JOC + 350 → SEP作図あり
    [InlineData("JBR", "00250", -1)]  // 深さ不一致
    [InlineData("JOC", "00160", -1)]  // 深さ不一致
    [InlineData("BX", "00350", -1)]   // タイプ不一致
    [InlineData("", "00350", -1)]     // タイプ空
    public void CheckSepBox_適用BOXタイプと深さ350で判定する(string boxType, string boxDepth, int expected)
    {
        int rc = SeparatorBoxCheck.CheckSepBox(Hbn(boxType: boxType), boxDepth);
        Assert.Equal(expected, rc);
    }

    [Fact]
    public void CheckSepBox_生成BOX品番があれば優先し適用BOXタイプは使わない()
    {
        // 【C原典】crboxtmp(生成BOX品番)>0 のときは boxtyp を無視して crboxtmp を採用。
        var hbn = Hbn(boxType: "BX", generatedBox: "JOC-350A");
        Assert.Equal(0, SeparatorBoxCheck.CheckSepBox(hbn, "00350"));
    }

    [Fact]
    public void CheckSepBox_生成BOX品番が空なら適用BOXタイプへフォールバックする()
    {
        var hbn = Hbn(boxType: "JBR", generatedBox: "");
        Assert.Equal(0, SeparatorBoxCheck.CheckSepBox(hbn, "00350"));
    }

    [Theory]
    [InlineData("GSP05-GVT-100", 0)]     // GVT を含む → 該当
    [InlineData("GSP05-GM1-GVSP", 0)]    // GVSP を含む → 該当
    [InlineData("GSP05-GM1-GQ20-GTM", -1)] // いずれも含まない → 非該当
    public void CheckHb300_入力品番が幅300品番を含むかで判定する(string inputPartNumber, int expected)
    {
        var parts = new List<string> { "GVT", "GVW", "GVM", "GVSP" };
        int rc = SeparatorBoxCheck.CheckHb300(Hbn(inputPartNumber: inputPartNumber), parts);
        Assert.Equal(expected, rc);
    }

    [Fact]
    public void CheckHb300_品番一覧が空なら非該当()
    {
        Assert.Equal(-1, SeparatorBoxCheck.CheckHb300(Hbn(inputPartNumber: "GVT"), new List<string>()));
    }

    [Fact]
    public void 実unithb300cns_ローダが幅300品番を解析する()
    {
        string? cns = FindConstFile(Path.Combine("sekkei", "unithb300.cns"));
        if (cns is null)
        {
            _output.WriteLine("unithb300.cns 未配置のためスキップします。");
            return;
        }

        IReadOnlyList<string> parts = Hb300UnitPartLoader.Parse(cns);

        Assert.True(parts.Count > 0, "幅300品番が 0 件(解析失敗の疑い)。");
        // コメント行(/*)が混入していないこと。
        Assert.DoesNotContain(parts, p => p.StartsWith("/*", StringComparison.Ordinal));
        _output.WriteLine($"hb300Parts={parts.Count}: {string.Join(",", parts)}");
    }

    /// <summary>
    /// テスト実行ディレクトリから上位へ辿り、toku/const/&lt;relative&gt; を探す。
    /// </summary>
    private static string? FindConstFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "toku", "const", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
