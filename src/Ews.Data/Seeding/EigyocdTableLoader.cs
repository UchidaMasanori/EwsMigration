using System.Text;
using Ews.Domain.Analysis;

namespace Ews.Data.Seeding;

/// <summary>
/// 物件/非物件管理データ識別テーブル(eigyocd.cns)を読み込み、
/// <see cref="NonPropertyOfficeEntry"/> 一覧を生成する。
///
/// 【入力】kawamura5/toku/const/sin/eigyocd.cns(Shift-JIS/CP932 テキスト)
///   先頭 2 バイトが "/*" の行はコメントとして無視(C 原典 strncmp(buf,"/*",2))。
///   データ行はカンマ区切りで、field0=非物件コード、field1～4=従来欄(空白)、field5 以降=営業所コード。
///
/// 【C原典】PropChkHibknNum(Fysk00.c:6130) の eigyocd.cns 読込部。fgets ループで 1 行ずつ読み、
///   strtok(",") で分割する。営業所コードの照合は 6 番目のフィールド(index 5)以降が対象。
/// </summary>
public static class EigyocdTableLoader
{
    /// <summary>営業所コード照合の対象になる最初のフィールド位置。【C原典】i&gt;5 判定。</summary>
    private const int OfficeCodeStartIndex = 5;

    /// <summary>eigyocd.cns ファイルを CP932 として読み込み、テーブルを返す。</summary>
    public static IReadOnlyList<NonPropertyOfficeEntry> LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"営業所コード識別コンスタントが見つかりません: {path}", path);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string content = File.ReadAllText(path, Encoding.GetEncoding(932));
        return Parse(content);
    }

    /// <summary>eigyocd.cns のテキスト内容を解析してテーブルを返す。</summary>
    public static IReadOnlyList<NonPropertyOfficeEntry> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var entries = new List<NonPropertyOfficeEntry>();

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            // 【C原典】先頭 2 バイトが "/*" の行はコメント(トリム前の生行で判定)。
            if (line.StartsWith("/*", StringComparison.Ordinal))
            {
                continue;
            }

            string[] tokens = line.Split(',');
            string nonPropertyCode = tokens[0].Trim(' ');
            if (nonPropertyCode.Length == 0)
            {
                continue;   // 空行/非データ行
            }

            var officeCodes = new List<string>();
            for (int i = OfficeCodeStartIndex; i < tokens.Length; i++)
            {
                string office = tokens[i].Trim(' ');
                if (office.Length > 0)
                {
                    officeCodes.Add(office);
                }
            }

            entries.Add(new NonPropertyOfficeEntry(nonPropertyCode, officeCodes));
        }

        return entries;
    }
}
