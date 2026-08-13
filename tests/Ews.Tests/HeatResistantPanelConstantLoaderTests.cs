using Ews.Data.Seeding;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="HeatResistantPanelConstantLoader"/>(=Fysk01_ReadCnst_TainetuBOX)の移植テスト。
/// </summary>
public sealed class HeatResistantPanelConstantLoaderTests
{
    // 固定バイト列: 行番号2桁 + ',' + 自由文字80桁 + ',' + 分類1桁 + 末尾。
    private static string Line(string lineNumber, string freeText, char category)
        => $"{lineNumber},{freeText.PadRight(80)},{category},";

    [Fact]
    public void データ行を解析し行番号と自由文字と分類を取得する()
    {
        string content = "/* header */\r\n" + Line("00", "TB2P60A+(F1+BOX)", 'A') + "\r\n";

        IReadOnlyList<HeatResistantPanelClassificationConstant> r = HeatResistantPanelConstantLoader.Parse(content);

        HeatResistantPanelClassificationConstant only = Assert.Single(r);
        Assert.Equal(0, only.LineNumber);
        Assert.Equal("TB2P60A+(F1+BOX)", only.FreeText);
        Assert.Equal('A', only.Category);
    }

    [Fact]
    public void コメント行を読み飛ばす()
    {
        string content =
            "/* comment1 */\r\n" +
            "/* comment2 */\r\n" +
            Line("00", "ABC", 'A') + "\r\n";

        IReadOnlyList<HeatResistantPanelClassificationConstant> r = HeatResistantPanelConstantLoader.Parse(content);

        Assert.Single(r);
    }

    [Fact]
    public void 行番号2桁を数値化する()
    {
        string content = Line("02", "X", 'B') + "\r\n" + Line("12", "Y", 'C') + "\r\n";

        IReadOnlyList<HeatResistantPanelClassificationConstant> r = HeatResistantPanelConstantLoader.Parse(content);

        Assert.Equal(2, r[0].LineNumber);
        Assert.Equal(12, r[1].LineNumber);
    }

    [Fact]
    public void 自由文字はカンマを含み先頭空白までで切り詰める()
    {
        // 自由文字はカンマ区切りではなく固定80桁フィールド(カンマは内容の一部)。
        string content = Line("00", "TB2P60A+(F1+BOX),MCB2P50AT+(F1+BOX)(MK=N)", 'A') + "\r\n";

        IReadOnlyList<HeatResistantPanelClassificationConstant> r = HeatResistantPanelConstantLoader.Parse(content);

        Assert.Equal("TB2P60A+(F1+BOX),MCB2P50AT+(F1+BOX)(MK=N)", Assert.Single(r).FreeText);
    }

    [Fact]
    public void 分類位置に満たない短い行は読み飛ばす()
    {
        string content = "00,SHORT\r\n";

        IReadOnlyList<HeatResistantPanelClassificationConstant> r = HeatResistantPanelConstantLoader.Parse(content);

        Assert.Empty(r);
    }

    [Fact]
    public void 複数データ行を順に取得する()
    {
        string content =
            Line("00", "AAA", 'A') + "\r\n" +
            Line("01", "BBB", 'B') + "\r\n" +
            Line("02", "CCC", 'C') + "\r\n";

        IReadOnlyList<HeatResistantPanelClassificationConstant> r = HeatResistantPanelConstantLoader.Parse(content);

        Assert.Equal(3, r.Count);
        Assert.Equal("AAA", r[0].FreeText);
        Assert.Equal('B', r[1].Category);
        Assert.Equal(2, r[2].LineNumber);
    }

    [Fact]
    public void 実ファイルを読み込み分類コンスタントを取得する()
    {
        string? path = FindRealCns();
        if (path is null)
        {
            return; // 実ファイル未配置環境ではスキップ相当。
        }

        IReadOnlyList<HeatResistantPanelClassificationConstant> r = HeatResistantPanelConstantLoader.LoadFromFile(path);

        Assert.NotEmpty(r);
        Assert.StartsWith("TB2P60A+(F1+BOX)", r[0].FreeText);
        Assert.Equal('A', r[0].Category);
    }

    private static string? FindRealCns()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "toku", "const", "sekkei", "tainetuPT.cns");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
