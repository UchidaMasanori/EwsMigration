using System.Text;

namespace Ews.Data.Seeding;

/// <summary>
/// 三菱製WH優先の営業所テーブル(whm_sentei.cns)を読み込み、非物件コード一覧を生成する。
///
/// 【入力】toku/const/sekkei/whm_sentei.cns(Shift-JIS/CP932 テキスト)
///   先頭 2 バイトが "/*" の行はコメントとして無視。データ行はカンマ区切りで、field0=非物件コード。
///   ※ 改訂&lt;1&gt;(2020/08/26)で登録は全削除され、現行ファイルは全行コメントアウト(=一覧は空)。
///
/// 【C原典】PropChkHibknNum(Fysk00.c:6202) の whm_sentei.cns 読込部。fgets ループで 1 行ずつ読み、
///   strtok(",") の先頭フィールドを非物件コードとして照合する。
/// </summary>
public static class WhmSenteiTableLoader
{
    /// <summary>whm_sentei.cns ファイルを CP932 として読み込み、非物件コード一覧を返す。</summary>
    public static IReadOnlyList<string> LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"三菱製WH優先営業所コンスタントが見つかりません: {path}", path);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string content = File.ReadAllText(path, Encoding.GetEncoding(932));
        return Parse(content);
    }

    /// <summary>whm_sentei.cns のテキスト内容を解析して非物件コード一覧を返す。</summary>
    public static IReadOnlyList<string> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var codes = new List<string>();

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            // 【C原典】先頭 2 バイトが "/*" の行はコメント(トリム前の生行で判定)。
            if (line.StartsWith("/*", StringComparison.Ordinal))
            {
                continue;
            }

            string nonPropertyCode = line.Split(',')[0].Trim(' ');
            if (nonPropertyCode.Length > 0)
            {
                codes.Add(nonPropertyCode);
            }
        }

        return codes;
    }
}
