using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// スターデルタ(MGSD/MCSD)回路の MC/THR 選定容量を電気パラメータへ設定する。
/// 【C原典】PropSelChkMgsd(Fysk00.c:8141, 改訂&lt;126&gt;)。負荷容量・回路電圧をキーに
///   スターデルタ用 MC/THR 選定容量テーブル(sel_mgsd.cns)を引き、MC のヒータ呼び容量を
///   定格電流2(A2)へ、THR のヒータ呼び容量をトリップ電流(AT)へ設定する。
///
/// 呼出元 Fysk00_Kikisearch_FU(複合回路機器選定, 未移植)は、複合回路名が MGSD/MCSD かつ
///   予約語が MC/THR のとき <c>mcthrflg = LibCharToShort(datano,3) - 3</c>(0?2:MC, 3:THR)で呼ぶ。
/// </summary>
public sealed class StarDeltaCapacitySelector
{
    /// <summary>MC 品名52 のヒータ呼び容量を選択。【C原典】flg==0。</summary>
    public const int SlotMc52 = 0;

    /// <summary>MC 品名42 のヒータ呼び容量を選択。【C原典】flg==1。</summary>
    public const int SlotMc42 = 1;

    /// <summary>MC 品名6 のヒータ呼び容量を選択。【C原典】flg==2。</summary>
    public const int SlotMc6 = 2;

    /// <summary>THR のヒータ呼び容量を選択。【C原典】flg==3。</summary>
    public const int SlotThermal = 3;

    private const int OutputCapacityWidth = 7;   // 【C原典】strncmp(huky, youryo, 7)。
    private const int VoltageWidth = 3;          // 【C原典】strncmp(hukv, denatu, 3)。

    private readonly IReadOnlyList<StarDeltaCapacityEntry> _table;

    public StarDeltaCapacitySelector(IReadOnlyList<StarDeltaCapacityEntry> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _table = table;
    }

    /// <summary>
    /// 負荷容量・回路電圧が一致する行の MC/THR ヒータ呼び容量を電気パラメータへ設定する。
    /// 一致行が無ければ何もしない。【C原典】PropSelChkMgsd(常に 0 を返す)。
    /// </summary>
    /// <param name="loadCapacity">負荷容量。【C原典】huky(=fp.fpalw2)。</param>
    /// <param name="circuitVoltage">回路電圧。【C原典】hukv(=kpav[0])。</param>
    /// <param name="target">設定先の電気パラメータ(ep[2])。</param>
    /// <param name="slot">ヒータ呼び容量の選択(0:MC52 / 1:MC42 / 2:MC6 / 3:THR)。【C原典】flg。</param>
    public void ApplyHeaterCapacity(string loadCapacity, string circuitVoltage,
                                    ElectricalParameters target, int slot)
    {
        ArgumentNullException.ThrowIfNull(loadCapacity);
        ArgumentNullException.ThrowIfNull(circuitVoltage);
        ArgumentNullException.ThrowIfNull(target);

        foreach (StarDeltaCapacityEntry entry in _table)
        {
            if (!Matches(loadCapacity, entry.OutputCapacity, OutputCapacityWidth) ||
                !Matches(circuitVoltage, entry.Voltage, VoltageWidth))
            {
                continue;
            }

            switch (slot)
            {
                case SlotMc52:
                    target.A2 = entry.HeaterCapacity52;
                    break;
                case SlotMc42:
                    target.A2 = entry.HeaterCapacity42;
                    break;
                case SlotMc6:
                    target.A2 = entry.HeaterCapacity6;
                    break;
                case SlotThermal:
                    target.At = entry.ThermalHeaterCapacity;
                    break;
            }

            break;   // 【C原典】1 件一致で break。
        }
    }

    // 【C原典】strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
