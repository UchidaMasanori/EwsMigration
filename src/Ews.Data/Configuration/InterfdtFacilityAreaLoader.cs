using System.Text;
using Ews.Domain.Configuration;

namespace Ews.Data.Configuration;

/// <summary>
/// 地区情報定義ファイル interfdt.inf を読み込み、<see cref="FacilityAreaEntry"/> 一覧
/// (=地区コード → 地区グループのテーブル)を生成する。
///
/// 【入力】interfdt.inf(Shift-JIS/CP932 テキスト。カンマ区切り 6 項目/行)
///   地区コード, 地区名, サーバーホスト名, 地区特性, 地区グループ, 地区サーバーホスト名,
///   先頭 '#' はコメント行。空行はスキップ。
///
/// 【C原典】FyGetInterTbl()(getinterfdt.c:423)。fgets ループで 1 行ずつ読み、
///   strchr(',') で 6 項目に分割し CpyNullStop(前後空白/TAB 除去)で static テーブル
///   interf[][] に格納する。本移植では FyGetFacGrp が使う地区コードと地区グループのみ保持。
/// </summary>
public static class InterfdtFacilityAreaLoader
{
    /// <summary>内部テーブルの最大件数。【C原典】TBL_MAX(getinterfdt.c:64)。</summary>
    private const int MaxEntries = 100;

    /// <summary>地区グループ項目のインデックス。【C原典】IDX_AREAGR。</summary>
    private const int AreaGroupIndex = 4;

    /// <summary>
    /// interfdt.inf ファイルを読み込み、地区情報一覧を返す。
    /// ファイルは CP932(Shift-JIS)として解釈する。
    /// </summary>
    public static IReadOnlyList<FacilityAreaEntry> LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"地区情報定義ファイルが見つかりません: {path}", path);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string content = File.ReadAllText(path, Encoding.GetEncoding(932));
        return Parse(content);
    }

    /// <summary>interfdt.inf のテキスト内容を解析して地区情報一覧を返す。</summary>
    public static IReadOnlyList<FacilityAreaEntry> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var entries = new List<FacilityAreaEntry>();

        foreach (string rawLine in content.Split('\n'))
        {
            // CRLF 由来の末尾 CR を除去(C の rbuf は改行込みだが挙動は同じ)。
            string line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            // 【C原典】strlen(rbuf) <= 1(改行のみ)→ スキップ / 先頭 '#' はコメント行。
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            // 【C原典】6 個の strchr(',') が全て成功する行のみ採用(= カンマ 6 個以上)。
            string[] fields = line.Split(',');
            if (fields.Length < 7)
            {
                continue;
            }

            // 【C原典】CpyNullStop: 前後の半角スペース/TAB を除去する。
            string zoneCode = fields[0].Trim(' ', '\t');
            string groupText = fields[AreaGroupIndex].Trim(' ', '\t');

            entries.Add(new FacilityAreaEntry(zoneCode, AtoiC(groupText)));

            // 【C原典】datacnt >= TBL_MAX で読込打切り。
            if (entries.Count >= MaxEntries)
            {
                break;
            }
        }

        return entries;
    }

    /// <summary>
    /// C の <c>atoi</c> 相当。先頭空白をスキップし符号+数字列のみ解釈する(非数字/終端で停止)。
    /// 【C原典】atoi( interf[i][IDX_AREAGR] )。
    /// </summary>
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
            if (value[i] == '-')
            {
                sign = -1;
            }

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
