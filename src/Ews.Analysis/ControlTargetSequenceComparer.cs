namespace Ews.Analysis;

using System.Collections.Generic;

/// <summary>
/// 制御対象機器データ追番(seikdno)の昇順ソート比較子。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>sgkkcmp</c>。
///
/// 制御回路エリア生成(CreatControla)で各制御仕様テーブルの
/// <c>P_SgsTable[i].seikdno[200]</c>(制御対象機器データ追番)を qsort で昇順整列する際の比較関数。
/// </summary>
public sealed class ControlTargetSequenceComparer : IComparer<short>
{
    /// <summary>共有インスタンス。【C原典】qsort(..., sgkkcmp)。</summary>
    public static ControlTargetSequenceComparer Instance { get; } = new();

    /// <summary>
    /// SHORT 2 値を昇順比較する。【C原典】sgkkcmp(Fyss1k.c:2784)= <c>*dat1 - *dat2</c>。
    /// </summary>
    /// <returns>差(x - y)。qsort と同じく符号のみが有意。</returns>
    public int Compare(short x, short y)
    {
        return x - y;
    }
}
