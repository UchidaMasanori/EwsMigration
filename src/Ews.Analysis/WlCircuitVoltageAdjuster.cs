using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ヒューズ(F)の子となる WL の回路電圧を変更する。
/// 【C原典】PropChangeWlKpav(Fysk00.c:7714, 改訂&lt;110&gt;)。
///   ヒューズのデフォルト機器タイプ設定(PropChgFuseType_SY)から呼ばれ、F の追番を親に持つ WL を探し、
///   河村製(WL ユニット="K  ")なら回路電圧を "005"、それ以外は F の回路電圧を WL へ複写する。
/// </summary>
public static class WlCircuitVoltageAdjuster
{
    private const string WlReservedWord = "WL ";
    private const string KawamuraMakerCode = "K  ";

    /// <summary>
    /// F(ヒューズ)の子 WL の回路電圧を変更する。最初に見つかった WL のみ対象(C 原典 break)。
    /// </summary>
    /// <param name="makerCode">F のメーカーコード。【C原典】mcod[0]。</param>
    /// <param name="fuse">ヒューズの主回路レコード。【C原典】sk。</param>
    /// <param name="records">主回路レコード列。【C原典】f800。</param>
    public static void Adjust(string makerCode, MainCircuitResult fuse,
                              IReadOnlyList<MainCircuitResult> records)
    {
        ArgumentNullException.ThrowIfNull(makerCode);
        ArgumentNullException.ThrowIfNull(fuse);
        ArgumentNullException.ThrowIfNull(records);

        foreach (MainCircuitResult record in records)
        {
            if (!Matches(record.Data.ReservedWord, WlReservedWord, 3))
            {
                continue;   // WL 以外パス
            }

            // 【C原典】F の追番(datano)を親(oyatno)に持つ WL を検索。
            if (!Matches(record.Data.ParentSequenceNumber, fuse.SequenceNumber, 3))
            {
                continue;
            }

            if (Matches(makerCode, KawamuraMakerCode, 3))
            {
                // 河村製(WL ユニット)は回路電圧を 5 にする。
                record.Data.CircuitVoltage[0] = "005";
            }
            else
            {
                // 河村製以外は F の回路電圧をセットする。
                record.Data.CircuitVoltage[0] = fuse.Data.CircuitVoltage[0];
            }

            break;
        }
    }

    // 【C原典】memcmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
