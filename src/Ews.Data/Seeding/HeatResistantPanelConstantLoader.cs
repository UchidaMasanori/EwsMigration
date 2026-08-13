using System.Text;
using Ews.Domain.Analysis;

namespace Ews.Data.Seeding;

/// <summary>
/// 耐熱盤部材判定用 自由文字コンスタントファイル(tainetuPT.cns)を読み込み、
/// <see cref="HeatResistantPanelClassificationConstant"/> 一覧を生成する。
///
/// 【入力】toku/const/sekkei/tainetuPT.cns(Shift-JIS/CP932 テキスト)。
///   1 データ行は固定バイト列: 先頭2桁=行番号 / ',' / 3バイト目から80バイト=自由文字 / ',' / 84バイト目=分類。
///   ※自由文字はカンマを含む固定80桁フィールドで、カンマ区切りでは分割しない。
///
/// 【C原典】Fysk01_ReadCnst_TainetuBOX(toku/sekkei/src/Fysk01.c:6521, 改訂&lt;13&gt;)。
///   FyGetFilePath("SEKKEI")+"tainetuPT.cns" を fopen("rb") し fgets ループで1行ずつ読む。
///   "/*" 始まりの行と strlen &lt;= 85 の行を読み飛ばし、strtok(",") で先頭トークンを得て
///   gyono=atoi(先頭2バイト) / jiyuumoji=memcpy(str+3, 80)後 先頭空白まで / bunrui=*(str+84)。
/// </summary>
public static class HeatResistantPanelConstantLoader
{
    /// <summary>コンスタントファイル名。【C原典】"tainetuPT.cns"。</summary>
    public const string FileName = "tainetuPT.cns";

    /// <summary>自由文字フィールドの開始バイト位置。【C原典】str+3。</summary>
    private const int FreeTextOffset = 3;

    /// <summary>自由文字フィールドのバイト長。【C原典】memcpy(..., 80)。</summary>
    private const int FreeTextWidth = 80;

    /// <summary>分類文字のバイト位置。【C原典】*(str+84)。</summary>
    private const int CategoryOffset = 84;

    private static readonly Encoding Cp932 = GetCp932();

    /// <summary>tainetuPT.cns を CP932 として読み込み、コンスタント一覧を返す。</summary>
    public static IReadOnlyList<HeatResistantPanelClassificationConstant> LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"耐熱盤部材判定コンスタントが見つかりません: {path}", path);
        }

        return Parse(File.ReadAllText(path, Cp932));
    }

    /// <summary>tainetuPT.cns のテキスト内容を解析してコンスタント一覧を返す。</summary>
    public static IReadOnlyList<HeatResistantPanelClassificationConstant> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var entries = new List<HeatResistantPanelClassificationConstant>();

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            // 【C原典】strncmp(buff,"/*",2)==0 でコメント行を飛ばす。
            if (line.StartsWith("/*", StringComparison.Ordinal))
            {
                continue;
            }

            byte[] bytes = Cp932.GetBytes(line);

            // 【C原典】strlen(buff) <= 85 は読み飛ばす。CはCRLF(rb)込みのバイト長で判定するが、
            // 本移植は改行除去後のバイト長で分類位置(index 84)の存在を保証する(実データは全行90バイト超)。
            if (bytes.Length <= CategoryOffset)
            {
                continue;
            }

            // 【C原典】gyono = atoi(先頭2バイト)。
            int lineNumber = Atoi2(bytes);

            // 【C原典】jiyuumoji = memcpy(str+3, 80) 後 先頭空白(0x20)まで。
            string freeText = ReadFreeText(bytes);

            // 【C原典】bunrui = *(str+84)。
            char category = (char)bytes[CategoryOffset];

            entries.Add(new HeatResistantPanelClassificationConstant(lineNumber, freeText, category));
        }

        return entries;
    }

    /// <summary>先頭2バイトを atoi する(先頭空白skip・以降の数字を数値化)。【C原典】atoi(work)。</summary>
    private static int Atoi2(byte[] bytes)
    {
        int i = 0;
        int limit = Math.Min(2, bytes.Length);
        while (i < limit && bytes[i] == (byte)' ')
        {
            i++;
        }
        int value = 0;
        for (; i < limit; i++)
        {
            byte c = bytes[i];
            if (c is < (byte)'0' or > (byte)'9')
            {
                break;
            }
            value = (value * 10) + (c - '0');
        }
        return value;
    }

    /// <summary>3バイト目から80バイト幅の自由文字を、先頭空白までで切り詰めて返す。</summary>
    private static string ReadFreeText(byte[] bytes)
    {
        int start = FreeTextOffset;
        int max = Math.Min(FreeTextWidth, bytes.Length - start);
        int length = 0;
        for (; length < max; length++)
        {
            if (bytes[start + length] == (byte)' ')
            {
                break;
            }
        }
        return Cp932.GetString(bytes, start, length);
    }

    private static Encoding GetCp932()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932);
    }
}
