using System.Text;
using Ews.Domain.Analysis;

namespace Ews.Data.Seeding;

/// <summary>
/// スターデルタ用 MC/THR 選定容量テーブル(sel_mgsd.cns)を読み込み、
/// <see cref="StarDeltaCapacityEntry"/> 一覧を生成する。
///
/// 【入力】toku/const/sekkei/sel_mgsd.cns(Shift-JIS/CP932 テキスト)
///   コメント行は "/*" 始まり。データ行はカンマ/空白区切りで
///   電圧, 出力容量, MC品名52, MC品名42, MC品名6, THRヒータ呼び容量, MC品番×3, THR品番。
///   末尾の全空白行で読込終了。
///
/// 【C原典】PropGetMcThrTblCnst(Fysk00.c:8205)。fgets ループで 1 行ずつ読み、
///   strtok(" ,") で分割して mcthr_tbl に格納する。品番(参考)は読み飛ばす。
/// </summary>
public static class StarDeltaCapacityTableLoader
{
    private static readonly char[] Separators = [' ', ','];

    /// <summary>sel_mgsd.cns ファイルを CP932 として読み込み、容量テーブルを返す。</summary>
    public static IReadOnlyList<StarDeltaCapacityEntry> LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"スターデルタ容量選定コンスタントが見つかりません: {path}", path);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string content = File.ReadAllText(path, Encoding.GetEncoding(932));
        return Parse(content);
    }

    /// <summary>sel_mgsd.cns のテキスト内容を解析して容量テーブルを返す。</summary>
    public static IReadOnlyList<StarDeltaCapacityEntry> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var entries = new List<StarDeltaCapacityEntry>();

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            if (line.StartsWith("/*", StringComparison.Ordinal))
            {
                continue;   // コメント行
            }

            // 【C原典】strtok(" ,"): 空白/カンマの連続を 1 区切りとして分割する。
            string[] tokens = line.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                break;      // 【C原典】末尾の全空白行(strtok=NULL)で読込終了。
            }

            entries.Add(new StarDeltaCapacityEntry(
                Voltage: tokens[0],
                OutputCapacity: Field(tokens, 1),
                HeaterCapacity52: Field(tokens, 2),
                HeaterCapacity42: Field(tokens, 3),
                HeaterCapacity6: Field(tokens, 4),
                ThermalHeaterCapacity: Field(tokens, 5)));
        }

        return entries;
    }

    private static string Field(string[] tokens, int index) =>
        index < tokens.Length ? tokens[index] : string.Empty;
}
