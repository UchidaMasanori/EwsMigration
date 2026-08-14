using System.Globalization;
using System.Text;
using Ews.Domain.Analysis;

namespace Ews.Data.Seeding;

/// <summary>
/// INV オプション機器直近上位検索用コンスタントファイル(invAC.cns / invDC.cns / invLN.cns / invMC.cns)を
/// 読み込み、<see cref="InverterOptionConstant"/> 一覧を生成する。
///
/// 【入力】toku/const/sekkei/ 配下の invXX.cns(Shift-JIS/CP932)
///   コメント行は "/*" 始まり。データ行はカンマ区切りで タイプ(7桁), kw, 品名(定格値), (末尾カンマ)。
///
/// 【C原典】Fysk01_ReadCnstINV_OP(toku/sekkei/src/Fysk01.c:5883, 改訂&lt;11&gt;)。呼び出し側から
///   ファイル名を受け取り fopen、fgets ループで 1 行ずつ読み strtok(",") で タイプ(memcpy 7)・
///   kw(atof)・品名(strncpy)を取り出して invop_prm 配列へ格納、件数を返す。
/// </summary>
public static class InverterOptionConstantTableLoader
{
    /// <summary>タイプ長。【C原典】memcpy(type, str, 7)。</summary>
    private const int TypeWidth = 7;

    /// <summary>invXX.cns ファイルを CP932 として読み込み、コンスタントを返す。</summary>
    public static IReadOnlyList<InverterOptionConstant> LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"INVオプション機器コンスタントが見つかりません: {path}", path);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string content = File.ReadAllText(path, Encoding.GetEncoding(932));
        return Parse(content);
    }

    /// <summary>invXX.cns のテキスト内容を解析してコンスタントを返す。</summary>
    public static IReadOnlyList<InverterOptionConstant> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var entries = new List<InverterOptionConstant>();

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

            entries.Add(new InverterOptionConstant(type, kw, productName));
        }

        return entries;
    }

    // 【C原典】atof: 先頭の数値部のみ解釈し、解釈できなければ 0.0。
    private static double Atof(string value) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : 0.0;
}
