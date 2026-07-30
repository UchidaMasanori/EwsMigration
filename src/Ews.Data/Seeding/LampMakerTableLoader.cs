using System.Text;
using Ews.Domain.Analysis;

namespace Ews.Data.Seeding;

/// <summary>
/// ランプ機器 優先メーカー切替テーブル(sel_LAMP.cns)を読み込み、
/// <see cref="LampMakerEntry"/> 一覧を生成する。
///
/// 【入力】toku/const/sekkei/sel_LAMP.cns(Shift-JIS/CP932 テキスト)
///   コメント行は "/*" 始まり。データ行はカンマ区切りで
///   工場コード, 予約語, メーカー1, メーカー2, メーカー3, メーカー4。
///
/// 【C原典】PropCnsLampRead(Fysk00.c:11737)のファイル読込部。fgets ループで 1 行ずつ読み、
///   strtok(",") で分割して struct lamp_seltbl に格納する。
/// </summary>
public static class LampMakerTableLoader
{
    /// <summary>sel_LAMP.cns ファイルを CP932 として読み込み、テーブルを返す。</summary>
    public static IReadOnlyList<LampMakerEntry> LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"ランプメーカー切替コンスタントが見つかりません: {path}", path);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string content = File.ReadAllText(path, Encoding.GetEncoding(932));
        return Parse(content);
    }

    /// <summary>sel_LAMP.cns のテキスト内容を解析してテーブルを返す。</summary>
    public static IReadOnlyList<LampMakerEntry> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var entries = new List<LampMakerEntry>();

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            if (line.StartsWith("/*", StringComparison.Ordinal))
            {
                continue;   // コメント行
            }

            // 【C原典】strtok(","): 空トークンは飛ばす(空白 3 桁の "   " は残る)。
            string[] tokens = line.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                continue;
            }

            entries.Add(new LampMakerEntry(
                FacilityGroup: AtoiC(tokens[0]),
                ReservedWord: Field(tokens, 1),
                MakerCodes: [Field(tokens, 2), Field(tokens, 3), Field(tokens, 4), Field(tokens, 5)]));
        }

        return entries;
    }

    private static string Field(string[] tokens, int index) =>
        index < tokens.Length ? tokens[index] : string.Empty;

    // 【C原典】atoi: 先頭空白スキップ+符号+数字列(非数字/終端で停止)。
    private static int AtoiC(string value)
    {
        int i = 0;
        while (i < value.Length && (value[i] == ' ' || value[i] == '\t'))
        {
            i++;
        }

        int sign = 1;
        if (i < value.Length && (value[i] == '+' || value[i] == '-'))
        {
            sign = value[i] == '-' ? -1 : 1;
            i++;
        }

        int result = 0;
        while (i < value.Length && value[i] is >= '0' and <= '9')
        {
            result = (result * 10) + (value[i] - '0');
            i++;
        }

        return sign * result;
    }
}
