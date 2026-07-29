namespace Ews.Domain.Analysis;

/// <summary>
/// 機器選定(直近上下位検索 Fysk01_Kikisearch_S1)で使用するワーク構造体。
/// 主回路データから抽出・変換した負荷/電気属性を保持する。
/// 【C原典】<c>typedef struct { ... } WK_STRUCT1</c>(toku/include/sekkei/fyrt814.h:47)。
///
/// 値は <c>Set_WK1</c>(Fysk00.c:4174)が 1 機器分の主回路データ(FYRT800)から生成する。
/// </summary>
public sealed class SelectionWorkParameters
{
    /// <summary>付属パラメータ負荷容量。【C原典】fuka(DOUBLE) ← Stof(fp.fpalw2, 7)。</summary>
    public double LoadCapacity { get; set; }

    /// <summary>通電電流値。【C原典】tsuden(DOUBLE) ← Stof(denryu, 8)。</summary>
    public double EnergizingCurrent { get; set; }

    /// <summary>回路相数。【C原典】sou(SHORT) ← kpaph - '0'。</summary>
    public short PhaseCount { get; set; }

    /// <summary>回路電圧。【C原典】denatu(DOUBLE) ← Stof(kpav[0], 3)。</summary>
    public double CircuitVoltage { get; set; }

    /// <summary>始動開始区分。【C原典】startkbn(CHAR) ← wk.startkbn。</summary>
    public char StartKind { get; set; } = ' ';

    /// <summary>機器発生区分。【C原典】hasei(CHAR) ← dt.ahassei。</summary>
    public char OccurrenceKind { get; set; } = ' ';

    /// <summary>付属パラメータ負荷種類(2 文字)。【C原典】fukasyu[2] ← memcpy(fp.fpalw1, 2)。</summary>
    public string LoadKind { get; set; } = "  ";

    /// <summary>親機器(P 行)の回路相数。【C原典】kpaph(CHAR) ← 親 P 行の dt.kpaph(改訂&lt;1&gt;)。</summary>
    public char ParentPhaseCount { get; set; } = ' ';
}
