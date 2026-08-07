using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路エリア(FYRT800)の 1 機器分を別の機器へ部分コピーする。
/// 【C原典】Fysk01_Area_Copy_SY(toku/sekkei/src/Fysk01.c:3515)。
///   コピー元(from)の電気パラメータ ep[1]・ep[2]・タイプ datatype・
///   定格容量 wk.teiwva をコピー先(to)へ複写する。ep[0] は複写しない。
/// </summary>
public static class MainCircuitAreaCopier
{
    /// <summary>
    /// 主回路エリアの from 番目から to 番目へ ep[1]/ep[2]・datatype・teiwva をコピーする。
    /// 【C原典】Fysk01_Area_Copy_SY(sk, from, to)。
    /// </summary>
    /// <param name="records">主回路エリア(FYRT800 配列相当)。【C原典】struct FYRT800 sk[]。</param>
    /// <param name="from">コピー元インデックス。【C原典】SHORT from。</param>
    /// <param name="to">コピー先インデックス。【C原典】SHORT to。</param>
    public static void CopyArea(IReadOnlyList<MainCircuitResult> records, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(records);

        MainCircuitData src = records[from].Data;
        MainCircuitData dst = records[to].Data;

        dst.ElectricalParameterSlots[1].CopyFrom(src.ElectricalParameterSlots[1]);
        dst.ElectricalParameterSlots[2].CopyFrom(src.ElectricalParameterSlots[2]);

        System.Array.Copy(src.DataType, dst.DataType, src.DataType.Length);

        records[to].Work.RatedCapacity = records[from].Work.RatedCapacity;
    }
}
