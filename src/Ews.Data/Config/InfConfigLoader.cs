using Ews.Domain.Common;

namespace Ews.Data.Config;

/// <summary>
/// .inf 構成ファイル(カンマ区切りテキスト)のパーサ。
///
/// 【C原典】
///   - toku/lib/libfycom/getfpath.c (filepath.inf 解析)
///   - toku/tool/src/getfname.c     (datafile.inf 解析)
///   - 共通処理: fopen("rt") + fgets + strchr + CpyNullStop による前後空白除去。
///
/// '#' 始まりの行はコメント、空行はスキップする。文字コードは Shift-JIS。
/// </summary>
public static class InfConfigLoader
{
    /// <summary>
    /// datafile.inf を読み込み、データファイル構成レジストリを返す。
    /// 【C原典】datafile.inf(ﾌｧｲﾙID, ﾌｧｲﾙﾊﾟｽID, ﾌｧｲﾙ名, ﾌｧｲﾙ名称)。
    /// </summary>
    public static IReadOnlyList<DataFileRegistryEntry> LoadDataFileRegistry(string path)
    {
        var entries = new List<DataFileRegistryEntry>();
        foreach (string[] fields in ReadCommaRecords(path))
        {
            if (fields.Length < 4)
            {
                continue;
            }

            entries.Add(new DataFileRegistryEntry(
                FileId: fields[0],
                PathId: fields[1],
                FileName: fields[2],
                DisplayName: fields[3]));
        }

        return entries;
    }

    /// <summary>
    /// filepath.inf を読み込み、パスID→物理パスの対応を返す。
    /// 【C原典】filepath.inf(PathID, Path)。FyGetFilePath() 相当。
    /// </summary>
    public static IReadOnlyDictionary<string, string> LoadFilePathMap(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string[] fields in ReadCommaRecords(path))
        {
            if (fields.Length < 2)
            {
                continue;
            }

            map[fields[0]] = fields[1];
        }

        return map;
    }

    /// <summary>
    /// .inf をカンマ区切りで読み、各フィールドを前後空白除去して列挙する。
    /// 【C原典】fgets + strchr によるカンマ分割 + CpyNullStop。
    /// </summary>
    public static IEnumerable<string[]> ReadCommaRecords(string path)
    {
        foreach (string rawLine in File.ReadLines(path, FixedFieldCodec.ShiftJis))
        {
            string line = rawLine.TrimEnd('\r', '\n');
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue; // 【C原典】rbuf[0]=='#' / 空行スキップ
            }

            string[] fields = line.Split(',');
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = fields[i].Trim(' ', '\t', '\u3000'); // CpyNullStop 相当
            }

            yield return fields;
        }
    }
}
