using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路 2 レコード(FYRT800)が構成機器 copy を共有できる同一データかを判定する。
/// 【C原典】Fysk01_Copy_Check(toku/sekkei/src/Fysk01.c:3385)。
///   予約語・タイプ・行種コード・電気パラメータ ep[3]・付属パラメータ(負荷/メーカー/品名/封印)・
///   回路電圧[0]・通電電流・負荷発生元・回路相数・始動回路区分が全て一致すれば同一(C 原典 ret=0)。
/// </summary>
public static class MainCircuitCopyChecker
{
    private const int ReservedWordWidth = 8;
    private const int DataTypeWidth = 7;
    private const int DataTypeSlotCount = 7;
    private const int LineTypeCodeWidth = 3;
    private const int LoadKindWidth = 2;
    private const int LoadCapacityWidth = 7;
    private const int MakerCodeWidth = 3;
    private const int ItemNameWidth = 25;
    private const int CircuitVoltageWidth = 3;
    private const int EnergizingCurrentWidth = 8;

    /// <summary>
    /// 2 レコードが copy 共有できる同一データかを判定する。
    /// 【C原典】Fysk01_Copy_Check(dat1, dat2)。戻り値は C 原典と逆で、一致(ret=0)を <c>true</c> で返す。
    /// </summary>
    /// <param name="dat1">比較元 1。【C原典】dat1(FYRT800)。</param>
    /// <param name="dat2">比較元 2。【C原典】dat2(FYRT800)。</param>
    /// <returns>全比較フィールドが一致すれば true(C 原典 ret=0)。不一致は false(ret=1)。</returns>
    public static bool AreCopyEquivalent(MainCircuitResult dat1, MainCircuitResult dat2)
    {
        ArgumentNullException.ThrowIfNull(dat1);
        ArgumentNullException.ThrowIfNull(dat2);

        MainCircuitData d1 = dat1.Data;
        MainCircuitData d2 = dat2.Data;

        return Same(d1.ReservedWord, d2.ReservedWord, ReservedWordWidth)
            && DataTypesEqual(d1.DataType, d2.DataType)
            && Same(d1.LineTypeCode, d2.LineTypeCode, LineTypeCodeWidth)
            && ElectricalSlotsEqual(d1.ElectricalParameterSlots, d2.ElectricalParameterSlots)
            && Same(d1.AttachedParameter.LoadKind, d2.AttachedParameter.LoadKind, LoadKindWidth)
            && Same(d1.AttachedParameter.LoadCapacity, d2.AttachedParameter.LoadCapacity, LoadCapacityWidth)
            && d1.AttachedParameter.LoadUnitKind == d2.AttachedParameter.LoadUnitKind
            && Same(d1.AttachedParameter.MakerCode, d2.AttachedParameter.MakerCode, MakerCodeWidth)
            && Same(d1.CircuitVoltage[0], d2.CircuitVoltage[0], CircuitVoltageWidth)
            && Same(d1.EnergizingCurrent, d2.EnergizingCurrent, EnergizingCurrentWidth)
            && Same(d1.AttachedParameter.ItemName, d2.AttachedParameter.ItemName, ItemNameWidth)
            && d1.LoadSourceKind == d2.LoadSourceKind
            && d1.CircuitPhaseCount == d2.CircuitPhaseCount
            && d1.AttachedParameter.SealKind == d2.AttachedParameter.SealKind
            && dat1.Work.StartCircuitKind == dat2.Work.StartCircuitKind;
    }

    // 【C原典】memcmp(&dat1.dt.datatype, &dat2.dt.datatype, 49)=7 枠×7 バイトを一括比較。
    private static bool DataTypesEqual(string[] a, string[] b)
    {
        for (int i = 0; i < DataTypeSlotCount; i++)
        {
            if (!Same(a[i], b[i], DataTypeWidth))
            {
                return false;
            }
        }
        return true;
    }

    // 【C原典】memcmp(dat1.dt.ep, dat2.dt.ep, sizeof(struct eparmg)*3)=ep[0..2] を一括比較。
    private static bool ElectricalSlotsEqual(ElectricalParameters[] a, ElectricalParameters[] b)
    {
        for (int i = 0; i < 3; i++)
        {
            if (!a[i].ValueEquals(b[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool Same(string a, string b, int width) => Take(a, width) == Take(b, width);

    private static string Take(string value, int width) => (value ?? string.Empty).PadRight(width)[..width];
}
