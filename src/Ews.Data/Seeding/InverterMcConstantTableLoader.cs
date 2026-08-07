using System.Globalization;
using System.Text;
using Ews.Domain.Analysis;

namespace Ews.Data.Seeding;

/// <summary>
/// INV 対応 MC 機器選定コンスタントファイル(inv003a.cns / inv003b.cns)を読み込み、
/// <see cref="InverterMcConstant"/> 一覧を生成する。
///
/// 【入力】toku/const/sekkei/inv003a.cns(リアクトル無) / inv003b.cns(リアクトル有)(Shift-JIS/CP932)
///   コメント行は "/*" 始まり。データ行はカンマ区切りで タイプ(7桁), kw, MC品名, (末尾カンマ)。
///
/// 【C原典】Fysk01_ReadCnstINV_MC(toku/sekkei/src/Fysk01.c:6314, 改訂&lt;29&gt;)。acdc==1 で
///   inv003b.cns、それ以外で inv003a.cns を開き、fgets ループで 1 行ずつ読み strtok(",") で
///   タイプ・kw(atof)・品名(strncpy)を取り出して invmc_prm 配列へ格納、件数を返す。
/// </summary>
public static class InverterMcConstantTableLoader
{
    /// <summary>タイプ長。【C原典】memcpy(type, str, 7)。</summary>
    private const int TypeWidth = 7;

    /// <summary>リアクトル有の選定コンスタント。【C原典】acdc==1。</summary>
    public const string FileNameWithReactor = "inv003b.cns";

    /// <summary>リアクトル無の選定コンスタント。【C原典】acdc!=1。</summary>
    public const string FileNameWithoutReactor = "inv003a.cns";

    /// <summary>
    /// INV AC/DC(リアクトル)有無に応じた選定コンスタントのファイル名を返す。
    /// 【C原典】acdc==1 ? "inv003b.cns" : "inv003a.cns"。
    /// </summary>
    public static string ResolveFileName(bool hasReactor) =>
        hasReactor ? FileNameWithReactor : FileNameWithoutReactor;

    /// <summary>inv003a/b.cns ファイルを CP932 として読み込み、選定コンスタントを返す。</summary>
    public static IReadOnlyList<InverterMcConstant> LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"INV対応MC選定コンスタントが見つかりません: {path}", path);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string content = File.ReadAllText(path, Encoding.GetEncoding(932));
        return Parse(content);
    }

    /// <summary>inv003a/b.cns のテキスト内容を解析して選定コンスタントを返す。</summary>
    public static IReadOnlyList<InverterMcConstant> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var entries = new List<InverterMcConstant>();

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

            // 【C原典】memcpy(type, str, 7)。
            string type = tokens[0].Length > TypeWidth ? tokens[0][..TypeWidth] : tokens[0];
            // 【C原典】kw=atof(str)。第2フィールドが無ければ 0.0。
            double kw = tokens.Length > 1 ? Atof(tokens[1]) : 0.0;
            // 【C原典】strncpy(hinmei, str, strlen(str))。第3フィールドが無ければ空。
            string productName = tokens.Length > 2 ? tokens[2] : string.Empty;

            entries.Add(new InverterMcConstant(type, kw, productName));
        }

        return entries;
    }

    // 【C原典】atof: 先頭の数値部のみ解釈し、解釈できなければ 0.0。
    private static double Atof(string value) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : 0.0;
}
