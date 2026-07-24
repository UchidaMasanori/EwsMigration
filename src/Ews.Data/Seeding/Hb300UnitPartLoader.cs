using System.Text;
using Ews.Domain.Common;

namespace Ews.Data.Seeding;

/// <summary>
/// 幅300用ユニット品番ファイル(unithb300.cns)を読み込むローダ。
///
/// 【C原典】PropChkHbnHB300(toku/sekkei/src/Fyss12.c)内のファイル読込ロジック。
///   ・FyGetFilePath("SEKKEI") + "/unithb300.cns" を fopen し、1 行ずつ読む。
///   ・"/*" で始まる行(コメント)は読み飛ばす。
///   ・最初の ',' で行を打ち切り、前後空白を除去(FyCpSpcutr)した品番を得る。
/// これらの品番のいずれかが入力品番(hbninf.inputhb)に部分一致すれば HB300 対象と判定する。
/// 【入力】toku/const/sekkei/unithb300.cns (幅300を判定するユニット品番のカンマ区切り)。
/// </summary>
public sealed class Hb300UnitPartLoader
{
    private static readonly Encoding Cp932 = FixedFieldCodec.ShiftJis;

    /// <summary>
    /// unithb300.cns を解析して幅300用ユニット品番の一覧を返す。
    /// コメント行(先頭 "/*")を除外し、各データ行を最初の ',' で打ち切って
    /// 前後空白を除去した品番のみを返す(空になった行は除外)。
    /// </summary>
    public static IReadOnlyList<string> Parse(string cnsPath)
    {
        var list = new List<string>();
        foreach (string raw in File.ReadLines(cnsPath, Cp932))
        {
            // 【C原典】if( 0 == strncmp(buf,"/*",2) ) continue;
            if (raw.StartsWith("/*", StringComparison.Ordinal))
            {
                continue;
            }

            // 【C原典】最初の ',' で '\0' 終端 → FyCpSpcutr で前後空白除去。
            string line = raw;
            int comma = line.IndexOf(',');
            if (comma >= 0)
            {
                line = line[..comma];
            }

            line = line.Trim();
            if (line.Length > 0)
            {
                list.Add(line);
            }
        }

        return list;
    }
}
