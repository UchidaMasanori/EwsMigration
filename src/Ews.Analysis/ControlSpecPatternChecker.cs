namespace Ews.Analysis;

using Ews.Domain.Analysis;

/// <summary>
/// 制御仕様のパターン番号(PTN)強制変更判定リーフ。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>PropChkINVPtn</c> / <c>PropChkKiKiPtn</c>。
///
/// 制御仕様文字列(FYRT820.Pcstrg)や制御機器テーブル(KIKITABLE)を解析し、
/// 特定の入力パターンで PTN 番号を強制する。
/// 既移植の <see cref="ControlSpecTextParser"/>(GetIntrData/GetAtCharData)を合成する。
/// 制御仕様テーブル(FYRT820)本体は未移植のため、使用フィールド(Pcstrg=制御仕様文字列)を
/// 文字列引数として受け取る。
/// </summary>
public static class ControlSpecPatternChecker
{
    // 【C原典】static KIKIPTN kikiptn[9]。特定パターン(予約語, 個数)。
    private static readonly (string ReservedWord, short Quantity)[] EquipmentPatterns =
    {
        ("RL", 2), ("GL", 2), ("COS", 2), ("G3", 0), ("APN", 0),
        ("OL", 2), ("BZ", 0), ("PBS", 0), ("CR", 0),
    };

    /// <summary>
    /// INV パターン(OL&lt;INV)を判定する。【C原典】PropChkINVPtn(Fyss1k.c:3519, 改訂&lt;13&gt;)。
    /// インターロックが INV 1機器のみ・制御対象機器なし・インターロック前が OL 1種類の場合に
    /// PTN=03(OL&lt;CR のパターン)を強制する。
    /// </summary>
    /// <param name="controlSpecText">制御仕様文字列(FYRT820.Pcstrg)。</param>
    /// <returns>3:OL&lt;INV パターン(PTN=03 を選択)、0:PTN 指定なし。</returns>
    public static int CheckInvPattern(string? controlSpecText)
    {
        string strg = controlSpecText ?? string.Empty;

        // 【C原典】p = strchr(strg, '<')。インターロック指定が有る場合のみ処理。
        int ltPos = strg.IndexOf('<');
        if (ltPos < 0)
        {
            return 0;
        }

        // 【C原典】GetIntrData(strg, buf)。インターロック機器を取り出し strg から除去。
        string body = strg;
        string interlock = ControlSpecTextParser.GetIntrData(ref body);

        // インターロック機器が1機器(',' なし)かつ INV。
        if (interlock.IndexOf(',') >= 0 || !interlock.Contains("INV", StringComparison.Ordinal))
        {
            return 0;
        }

        // 【C原典】work=strg(除去後)、*p='\0' で '<' 位置(=前置部の長さ)で切詰め。
        // GetIntrData は前置部 strg[0..ltPos) を保持するため body.Length >= ltPos。
        string work = body;
        string head = body.Substring(0, ltPos);

        // 【C原典】制御対象機器の文字列を ':' 前で取得。ret==1 は制御対象機器なし。
        int ret = ControlSpecTextParser.GetAtCharData(ref head, out string target, ':');
        if (ret == 1
            && work.IndexOf(',') < 0                                 // インターロック前の機器が1種類
            && target.Contains("OL", StringComparison.Ordinal))     // インターロック前の機器が OL
        {
            return 3;   // OL<CR のパターン3を選択
        }

        return 0;
    }

    /// <summary>
    /// 対象機器 G3・用途 YOU の場合に、制御機器グループが特定パターンか判定する。
    /// 【C原典】PropChkKiKiPtn(Fyss1k.c:3444, 改訂&lt;11&gt;)。
    /// 該当行種グループ内の全機器が特定パターン(予約語+個数)に一致すれば
    /// PTN 番号を強制的に維持する(変更しない)。
    /// </summary>
    /// <param name="targetEquipment">対象機器(予約語)。【C原典】er。</param>
    /// <param name="controlEquipments">制御機器テーブル。【C原典】CKikiTabl。</param>
    /// <param name="usage">用途。【C原典】DTYPE。</param>
    /// <param name="lineTypeGroup">行種グループNo。【C原典】gyogp。</param>
    /// <returns>-1:特定パターン(PTN 番号を変更しない)、0:対象外又は非該当(変更可)。</returns>
    public static int CheckEquipmentPattern(
        string? targetEquipment,
        IReadOnlyList<EquipmentTableEntry> controlEquipments,
        string? usage,
        short lineTypeGroup)
    {
        ArgumentNullException.ThrowIfNull(controlEquipments);

        // 【C原典】対象機器 G3・用途 YOU 以外は対象外。
        if (!string.Equals(targetEquipment, "G3", StringComparison.Ordinal)
            || !string.Equals(usage, "YOU", StringComparison.Ordinal))
        {
            return 0;
        }

        // 【C原典】特定パターンの場合に PTN 番号を変更しないようにする。
        foreach (EquipmentTableEntry entry in controlEquipments)
        {
            if (entry.GroupNumber != lineTypeGroup)
            {
                continue;
            }

            bool matched = false;
            foreach ((string reservedWord, short quantity) in EquipmentPatterns)
            {
                if (string.Equals(entry.ReservedWord, reservedWord, StringComparison.Ordinal)
                    && entry.Quantity == quantity)
                {
                    matched = true;
                    break;
                }
            }

            // 一つでも対象外があれば変更可。
            if (!matched)
            {
                return 0;
            }
        }

        return -1;
    }
}