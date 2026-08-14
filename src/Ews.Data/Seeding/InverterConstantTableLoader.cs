using System.Globalization;
using System.Text;
using Ews.Domain.Analysis;

namespace Ews.Data.Seeding;

/// <summary>
/// INV 直近上位検索用コンスタントファイル(inv001.cns)を読み込み、
/// <see cref="InverterConstant"/> 一覧を生成する。
///
/// 【入力】toku/const/sekkei/inv001.cns(Shift-JIS/CP932)
///   コメント行は "/*" 始まり。データ行はカンマ区切りで タイプ(49桁), kw, (末尾カンマ)。
///
/// 【C原典】Fysk01_ReadCnstINV001(toku/sekkei/src/Fysk01.c:5498, 改訂&lt;27&gt;)。
///   fgets ループで 1 行ずつ読み strtok(",") で タイプ(memcpy 49 バイト)・kw(atof) を取り出し
///   inv_prm 配列へ格納、件数を返す。タイプ 49 バイトは type[7][7] へ展開される。
/// </summary>
public static class InverterConstantTableLoader
{
    /// <summary>タイプ欄の全長。【C原典】memcpy(type[0], str, 49)。</summary>
    private const int TypeFieldWidth = 49;

    /// <summary>タイプスロット数。【C原典】type[7][7]。</summary>
    private const int SlotCount = 7;

    /// <summary>タイプスロット幅。【C原典】type[7][7]。</summary>
    private const int SlotWidth = 7;

    /// <summary>INV 直近上位検索用コンスタント(v1)のファイル名。</summary>
    public const string FileName = "inv001.cns";

    /// <summary>inv001.cns ファイルを CP932 として読み込み、コンスタントを返す。</summary>
    public static IReadOnlyList<InverterConstant> LoadInv001FromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"INV直近上位検索コンスタントが見つかりません: {path}", path);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string content = File.ReadAllText(path, Encoding.GetEncoding(932));
        return ParseInv001(content);
    }

    /// <summary>inv001.cns のテキスト内容を解析してコンスタントを返す。</summary>
    public static IReadOnlyList<InverterConstant> ParseInv001(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var entries = new List<InverterConstant>();

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            // 【C原典】strncmp(buff,"/*",2)==0 でコメント行を飛ばす。
            if (line.StartsWith("/*", StringComparison.Ordinal))
            {
                continue;
            }

            // 【C原典】strtok(buff,","): タイプが取れなければ読込終了(EOF/空行)。
            string[] tokens = line.Split(',');
            if (tokens[0].Length == 0)
            {
                break;
            }

            // 【C原典】memcpy(type[0], str, 49): タイプ 49 バイトを type[7][7] へ展開。
            string typeField = tokens[0].Length >= TypeFieldWidth
                ? tokens[0][..TypeFieldWidth]
                : tokens[0].PadRight(TypeFieldWidth);
            var slots = new string[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                slots[i] = typeField.Substring(i * SlotWidth, SlotWidth);
            }

            // 【C原典】kw=atof(str)。第2フィールドが無ければ 0.0。
            double kw = tokens.Length > 1 ? Atof(tokens[1]) : 0.0;

            entries.Add(new InverterConstant(slots, kw));
        }

        return entries;
    }

    // 【C原典】atof: 先頭の数値部のみ解釈し、解釈できなければ 0.0。
    private static double Atof(string value) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : 0.0;
}
