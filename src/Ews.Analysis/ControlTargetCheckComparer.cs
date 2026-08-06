namespace Ews.Analysis;

using System;
using System.Collections.Generic;
using Ews.Domain.Analysis;

/// <summary>
/// 制御対象機器重複チェックデータ(SGTCHK)のソート比較子。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>sckcmp</c>。
///
/// 制御対象機器重複チェック(SgtkkDoubleCheck)で sck[] を qsort する際の比較関数。
/// 追番(oiban)昇順 → 記述行(K_Gyo)昇順 → 記述桁(K_Ket)昇順。
/// </summary>
public sealed class ControlTargetCheckComparer : IComparer<ControlTargetCheckEntry>
{
    /// <summary>共有インスタンス。【C原典】qsort(..., sckcmp)。</summary>
    public static ControlTargetCheckComparer Instance { get; } = new();

    /// <summary>
    /// SGTCHK 2 エントリを追番→記述行→記述桁で昇順比較する。【C原典】sckcmp(Fyss1k.c:2396)。
    /// </summary>
    /// <returns>最初に相違した項目の差(【C原典】cmp1-cmp2)。符号のみ有意。</returns>
    public int Compare(ControlTargetCheckEntry? x, ControlTargetCheckEntry? y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        // 【C原典】ret = cmp1->oiban - cmp2->oiban; if(ret) return ret;
        int ret = x.DataSequence - y.DataSequence;
        if (ret != 0)
        {
            return ret;
        }

        // 【C原典】ret = cmp1->K_Gyo - cmp2->K_Gyo; if(ret) return ret;
        ret = x.DescriptionRow - y.DescriptionRow;
        if (ret != 0)
        {
            return ret;
        }

        // 【C原典】ret = cmp1->K_Ket - cmp2->K_Ket; return ret;
        return x.DescriptionColumn - y.DescriptionColumn;
    }
}
