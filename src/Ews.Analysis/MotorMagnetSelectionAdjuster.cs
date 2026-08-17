using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 動力 MG 回路(3.7KW)の MG 選定。公共建築仕様(製作仕様区分 "02")の MG 機器で、同一系統に
/// 動力(3相)電源が存在する場合、主回路データの空きタイプ枠へ "2ET" を設定する。
/// 【C原典】PropMGSentei(toku/sekkei/src/Fysk00.c:8029, 改訂&lt;121&gt;)。
/// 呼び出し元: Fysk00_Kikisearch_SY_Sub(Fysk00.c:1985)。
/// </summary>
public static class MotorMagnetSelectionAdjuster
{
    /// <summary>系統番号の桁数。【C原典】sizeof(dt.kno)=3。</summary>
    private const int SystemNumberWidth = 3;

    /// <summary>タイプ枠数。【C原典】datatype[7][7] の 7 枠。</summary>
    private const int TypeSlotCount = 7;

    /// <summary>1 タイプ枠の桁数。【C原典】7。</summary>
    private const int TypeWidth = 7;

    /// <summary>空きタイプ枠を示す番兵。【C原典】"NOTHING"。</summary>
    private const string EmptyType = "NOTHING";

    /// <summary>設定するアース端子タイプ。【C原典】"2ET    "(7 桁)。</summary>
    private const string EarthTerminalType = "2ET    ";

    /// <summary>
    /// 動力 MG 回路の 2ET タイプ設定を行う。【C原典】PropMGSentei(f800cnt, f800, bknk, sk)。
    /// </summary>
    /// <param name="mains">主回路エリア(全件)。【C原典】f800(件数 f800cnt)。</param>
    /// <param name="manufacturingSpecKind">製作仕様区分。【C原典】bknk-&gt;com.kyo.sshiykbn。"02":公共建築仕様。</param>
    /// <param name="target">対象の主回路該当レコード(タイプ枠を書き換える)。【C原典】sk。</param>
    public static void Apply(
        IReadOnlyList<MainCircuitResult> mains, string manufacturingSpecKind, MainCircuitResult target)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(manufacturingSpecKind);
        ArgumentNullException.ThrowIfNull(target);

        // 【C原典】MG(マグネット)かつ公共建築仕様(02)以外は対象外。
        if (!Matches(target.Data.ReservedWord, "MG", 2) ||
            !Matches(manufacturingSpecKind, "02", 2))
        {
            return;
        }

        // 【C原典】同一系統に動力(3相)電源(予約語 "P")が存在するか探す。
        bool motorPowerFound = false;
        foreach (MainCircuitResult m in mains)
        {
            if (!Matches(m.Data.ReservedWord, "P  ", 3))
            {
                continue;
            }
            if (!Matches(m.Data.SystemNumber, target.Data.SystemNumber, SystemNumberWidth))
            {
                // 系統違う
                continue;
            }
            if (m.Data.CircuitPhaseCount == '3')
            {
                // 動力なので処理対象
                motorPowerFound = true;
                break;
            }
        }

        if (!motorPowerFound)
        {
            return;
        }

        // 【C原典】空きタイプ枠(NOTHING)へ "2ET" を設定。既に 2ET があれば何もしない。
        string[] dataType = target.Data.DataType;
        for (int i = 0; i < TypeSlotCount; i++)
        {
            if (Matches(dataType[i], EarthTerminalType, TypeWidth))
            {
                break;
            }
            if (Matches(dataType[i], EmptyType, TypeWidth))
            {
                dataType[i] = EarthTerminalType;
                break;
            }
        }
    }

    // 【C原典】strncmp(value, expected, width) == 0 相当(先頭 width 桁一致)。
    private static bool Matches(string value, string expected, int width)
    {
        string v = (value ?? string.Empty).PadRight(width);
        string e = expected.PadRight(width);
        return v.AsSpan(0, width).SequenceEqual(e.AsSpan(0, width));
    }
}
