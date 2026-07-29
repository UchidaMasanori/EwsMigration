using Ews.Domain.Analysis;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// VA 積み上げの BASE 機器を判定し、機器の定格容量(VA または W)または負荷容量(VA)を
/// 主回路のワーク領域(teiwva)へセットする。
/// 【C原典】<c>Fysk00_Set_VA_W</c>(toku/sekkei/src/Fysk00.c:4108)。
///
/// この teiwva は計器回路(<see cref="MeterCircuitBuilder"/>)が下流機器を積み上げる際の基礎値となる。
/// 回路要素(kiryoso)が '1' の機器は積み上げ対象外(teiwva=0)。
/// </summary>
public static class StackingCapacityResolver
{
    /// <summary>
    /// 積み上げ対象予約語(前方一致キー)。末尾 2 件は空文字で、memcmp 0 バイト一致により
    /// 上位いずれにも該当しない予約語を捕捉する既定(フラグ 0)。
    /// 【C原典】static CHAR VA_YO[16][5](14 件のみ初期化、残り 2 件は空)。
    /// </summary>
    private static readonly string[] VaReservedWords =
    [
        "WH ", "VM ", "AM ", "CR ", "HM ",
        "WL ", "GL ", "RL ", "OL ", "BL ", "FL ",
        "VT ", "CT ", "PLTR",
        "", "",
    ];

    /// <summary>
    /// 予約語別の値種別。0:定格容量(AC=teiva[0]/DC=teiw) / 1:定格容量(teiw) / 2:負荷容量(ep[epno].VA)。
    /// 【C原典】static SHORT VA_YOFG[16] = { 0,0,0,0,0, 1,1,1,1,1,1, 2,2,2 }(残り 2 件は 0)。
    /// </summary>
    private static readonly int[] VaFlags =
    [
        0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1,
        2, 2, 2,
        0, 0,
    ];

    /// <summary>定格容量フィールド(hojg.teiva[0]/teiw)の幅。【C原典】sizeof(CHAR[7])。</summary>
    private const int RatedCapacityWidth = 7;

    /// <summary>負荷容量(ep[epno].epava)の幅。【C原典】epava[10]。</summary>
    private const int LoadCapacityWidth = 10;

    /// <summary>
    /// 機器の定格/負荷容量を回路ワーク(teiwva)へセットする。【C原典】<c>Fysk00_Set_VA_W(kk, sk, epno)</c>。
    /// </summary>
    /// <param name="equipment">機器マスター該当エリア(1 件分)。【C原典】kk (FYDM805)。</param>
    /// <param name="record">主回路エリア(1 件分)。【C原典】sk (FYRT800 *)。</param>
    /// <param name="electricalParameterIndex">電気パラメータ番号。【C原典】epno。</param>
    public static void Resolve(EquipmentMaster equipment, MainCircuitResult record, int electricalParameterIndex)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(record);

        MainCircuitData data = record.Data;

        // 【C原典】回路要素 '1' は積み上げ対象外。
        if (data.CircuitElement != '1')
        {
            string padded = (data.ReservedWord ?? string.Empty).PadRight(8);
            for (int i = 0; i < VaReservedWords.Length; i++)
            {
                // 【C原典】memcmp(yoyaku, VA_YO[i], strlen(VA_YO[i]))==0(空文字は 0 バイト比較=常に一致)。
                if (!padded.StartsWith(VaReservedWords[i], StringComparison.Ordinal))
                {
                    continue;
                }

                switch (VaFlags[i])
                {
                    case 0:   // 【C原典】定格容量 AC/DC
                        record.Work.RatedCapacity = data.CircuitVoltageKind == 'A'
                            ? Stof(equipment.RatedCapacityAcVa, RatedCapacityWidth)   // AC: teiva[0]
                            : Stof(equipment.RatedCapacityDcW, RatedCapacityWidth);   // DC: teiw
                        break;

                    case 1:   // 【C原典】定格容量(teiw)
                        record.Work.RatedCapacity = Stof(equipment.RatedCapacityDcW, RatedCapacityWidth);
                        break;

                    case 2:   // 【C原典】負荷容量(VA) = ep[epno].epava
                        record.Work.RatedCapacity = Stof(data.ElectricalParameterSlots[electricalParameterIndex].Va, LoadCapacityWidth);
                        break;
                }

                return;
            }
        }

        record.Work.RatedCapacity = 0.0;
    }

    private static double Stof(string? s, int size) => EquipmentParameterFormatter.Stof(s, size);
}
