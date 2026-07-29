using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路データ(FYRT800)1 機器分から、機器選定検索用ワーク構造体(WK_STRUCT1)を組み立てる。
/// 【C原典】<c>static VOID Set_WK1(struct FYRT800 *f800, struct FYRT800 *sk, WK_STRUCT1 *wk1)</c>
/// (toku/sekkei/src/Fysk00.c:4174, 改訂&lt;22&gt;)。
///
/// 付属パラメータ負荷容量/通電電流/回路相数/回路電圧などを数値化し、
/// 親機器(P 行)の回路相数(改訂&lt;1&gt;)も取得して格納する。
/// </summary>
public static class SelectionWorkParametersBuilder
{
    /// <summary>付属パラメータ負荷容量フィールド幅。【C原典】sizeof(fp.fpalw2)=7。</summary>
    private const int LoadCapacityWidth = 7;

    /// <summary>通電電流値フィールド幅。【C原典】sizeof(denryu)=8。</summary>
    private const int EnergizingCurrentWidth = 8;

    /// <summary>回路電圧フィールド幅。【C原典】Stof(kpav[0], 3)。</summary>
    private const int CircuitVoltageWidth = 3;

    /// <summary>付属パラメータ負荷種類の桁数。【C原典】memcpy(fukasyu, fp.fpalw1, 2)。</summary>
    private const int LoadKindWidth = 2;

    /// <summary>
    /// 1 機器分の主回路データからワーク構造体(WK_STRUCT1)を組み立てる。【C原典】<c>Set_WK1(f800, sk, wk1)</c>。
    /// </summary>
    /// <param name="records">主回路データ配列。【C原典】f800 (FYRT800 *)。親 P 行検索に使用。</param>
    /// <param name="record">当該機器の主回路データ。【C原典】sk (FYRT800 *)。</param>
    /// <returns>組み立てたワーク構造体。</returns>
    public static SelectionWorkParameters Build(IReadOnlyList<MainCircuitResult> records, MainCircuitResult record)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(record);

        MainCircuitData data = record.Data;

        var work = new SelectionWorkParameters
        {
            // 【C原典】wk1->fuka = Stof(sk->dt.fp.fpalw2, sizeof(fpalw2))。
            LoadCapacity = EquipmentParameterFormatter.Stof(data.AttachedParameter.LoadCapacity, LoadCapacityWidth),

            // 【C原典】wk1->tsuden = Stof(sk->dt.denryu, sizeof(denryu))。
            EnergizingCurrent = EquipmentParameterFormatter.Stof(data.EnergizingCurrent, EnergizingCurrentWidth),

            // 【C原典】wk1->startkbn = sk->wk.startkbn。
            StartKind = record.Work.StartCircuitKind,

            // 【C原典】memcpy(wk1->fukasyu, sk->dt.fp.fpalw1, 2)。
            LoadKind = (data.AttachedParameter.LoadKind ?? string.Empty).PadRight(LoadKindWidth)[..LoadKindWidth],

            // 【C原典】wk1->hasei = sk->dt.ahassei。
            OccurrenceKind = data.LoadSourceKind,

            // 【C原典】wk1->sou = sk->dt.kpaph - '0'。
            PhaseCount = (short)(data.CircuitPhaseCount - '0'),

            // 【C原典】wk1->denatu = Stof(sk->dt.kpav[0], 3)。
            CircuitVoltage = EquipmentParameterFormatter.Stof(data.CircuitVoltage[0], CircuitVoltageWidth),
        };

        // 【C原典】Fysk0f_GetOyaP(f800, sk->dt.oyatno, &oya); wk1->kpaph = oya->dt.kpaph;(改訂<22>)。
        MainCircuitResult? parent = ParentEquipmentLocator.FindParentPRow(records, data.ParentSequenceNumber);
        work.ParentPhaseCount = parent is not null ? parent.Data.CircuitPhaseCount : ' ';

        return work;
    }
}
