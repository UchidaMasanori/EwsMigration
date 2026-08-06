namespace Ews.Analysis;

/// <summary>
/// 制御回路サブシステムの予約語分類・インターロック判定リーフ群。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>CheckYoyakugoMG</c> / <c>CheckYoyakugoRM</c> /
/// <c>CheckSgtkiki</c> / <c>GetSidouYouto</c> / <c>ChkInterp</c>。
///
/// 制御仕様テーブル作成(FySgCheckSgkkSet)の内部で、制御対象機器・リモコン機器・
/// 始動回路用途・インターロック記述を予約語文字列から分類する純粋関数。
/// 上位の制御仕様テーブル作成/構造体(FYRT820 等)は未移植のため、
/// 構造体非依存の純粋文字列関数のみを先行移植する。
///
/// 戻り値の 0/1 は C 原典の strcmp 慣習に忠実(0:該当、1:非該当)。
/// </summary>
public static class ControlReservedWordClassifier
{
    // 【C原典】static CHAR *MG_yoyakugo[] = { "THR","2ERY","3ERY","4ERY", NULL };
    private static readonly string[] MgReservedWords = { "THR", "2ERY", "3ERY", "4ERY" };

    // 【C原典】static CHAR *RM_yoyakugo[] = { "PT","RRY","RELB","RMMCB","RMCB","RELMB", NULL };
    private static readonly string[] RemoteReservedWords = { "PT", "RRY", "RELB", "RMMCB", "RMCB", "RELMB" };

    // 【C原典】static CHAR *sgtkk[] = { "MC","MG","MCDT","MCFR","MGFR","MCSD","MGSD","MGLD","INV","MCFRSD","MGFRSD", NULL };
    private static readonly string[] ControlTargetEquipments =
    {
        "MC", "MG", "MCDT", "MCFR", "MGFR", "MCSD", "MGSD", "MGLD", "INV", "MCFRSD", "MGFRSD",
    };

    /// <summary>
    /// 予約語が MG 機器(THR/2ERY/3ERY/4ERY)か判定する。【C原典】CheckYoyakugoMG(Fyss1k.c:1394)。
    /// </summary>
    /// <returns>0:該当、1:非該当。</returns>
    public static int CheckMgReservedWord(string? data)
    {
        return MatchExact(MgReservedWords, data);
    }

    /// <summary>
    /// 予約語がリモコン機器(PT/RRY/RELB/RMMCB/RMCB/RELMB)か判定する。【C原典】CheckYoyakugoRM(Fyss1k.c:1412)。
    /// </summary>
    /// <returns>0:該当、1:非該当。</returns>
    public static int CheckRemoteReservedWord(string? data)
    {
        return MatchExact(RemoteReservedWords, data);
    }

    /// <summary>
    /// 予約語が制御対象機器(MC/MG/MCDT/MCFR/MGFR/MCSD/MGSD/MGLD/INV/MCFRSD/MGFRSD)か判定する。
    /// 【C原典】CheckSgtkiki(Fyss1k.c:1845)。
    /// </summary>
    /// <returns>0:該当、1:非該当。</returns>
    public static int CheckControlTargetEquipment(string? data)
    {
        return MatchExact(ControlTargetEquipments, data);
    }

    /// <summary>
    /// 論理記述マスタの用途(始動回路用)を取得する。【C原典】GetSidouYouto(Fyss1k.c:1430)。
    /// MG 有無(<paramref name="mgPresent"/>=1 で MG 化)により MC 系を MG 系へ振り替える。
    /// </summary>
    /// <returns>用途文字列。該当なしは空文字。</returns>
    public static string GetStartCircuitUsage(string? equipment, int mgPresent)
    {
        string s = equipment ?? string.Empty;
        return s switch
        {
            "MC" => mgPresent == 1 ? "MG" : "MC",
            "MG" => "MG",
            "MCFR" => mgPresent == 1 ? "MGFR" : "MCFR",
            "MGFR" => "MGFR",
            "MCSD" => mgPresent == 1 ? "MGSD" : "MCSD",
            "MGSD" => "MGSD",
            "MCFRSD" => mgPresent == 1 ? "MGFRSD" : "MCFRSD",
            "MGFRSD" => "MGFRSD",
            "MCDT" => "MCDT",
            "INV" => "INV",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// インターロック記述の妥当性を判定する。【C原典】ChkInterp(Fyss1k.c:503)。
    /// '&lt;' で始まる記述のうち、&lt;THR と &lt;AL 以外の '&lt;' が現れたら NG。
    /// </summary>
    /// <returns>0:'&lt;' 無し又は全て &lt;THR/&lt;AL、1:それ以外の '&lt;' あり。</returns>
    public static int CheckInterlock(string? text)
    {
        string s = text ?? string.Empty;
        int ret = 0;

        int pos = 0;
        while (true)
        {
            int lt = s.IndexOf('<', pos);
            if (lt < 0)
            {
                break;
            }

            ret = 1;
            // 【C原典】*(pt+1)=='T' && *(pt+2)=='H' && *(pt+3)=='R'。範囲外は非該当扱い(C の終端 '\0' 比較と等価)。
            if (HasAt(s, lt + 1, 'T') && HasAt(s, lt + 2, 'H') && HasAt(s, lt + 3, 'R'))
            {
                ret = 0;
            }
            else if (HasAt(s, lt + 1, 'A') && HasAt(s, lt + 2, 'L'))
            {
                ret = 0;
            }

            if (ret == 1)
            {
                return ret;
            }

            pos = lt + 1;
        }

        return ret;
    }

    // 【C原典】NULL 終端リストを先頭から strcmp。完全一致で 0、末尾(NULL)到達で 1。
    private static int MatchExact(string[] table, string? data)
    {
        string s = data ?? string.Empty;
        foreach (string entry in table)
        {
            if (string.Equals(s, entry, StringComparison.Ordinal))
            {
                return 0;
            }
        }
        return 1;
    }

    private static bool HasAt(string s, int index, char expected)
    {
        return index < s.Length && s[index] == expected;
    }
}
