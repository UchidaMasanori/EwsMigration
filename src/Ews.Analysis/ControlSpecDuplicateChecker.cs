namespace Ews.Analysis;

using System;
using System.Collections.Generic;
using Ews.Domain.Analysis;

/// <summary>
/// 制御仕様テーブル(FYRT820)の制御対象機器の重複を検証するリーフ。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>SgtkkDoubleCheck</c>。
/// </summary>
public static class ControlSpecDuplicateChecker
{
    /// <summary>
    /// 制御対象機器データ追番(seikdno)の重複を検証する。
    /// 【C原典】SgtkkDoubleCheck(Fyss1k.c:2354)。
    /// 全エントリの seikdno[200](0 終端)を収集し、追番→記述行→記述桁の昇順に整列した後、
    /// 隣接する追番が一致すればエラー(FY-904E)を返す。
    /// </summary>
    /// <param name="controlSpecs">制御仕様テーブル。【C原典】SgsTable(件数 iSgsTabl)。</param>
    /// <returns>重複時は <see cref="CircuitParseError"/>(=C の return(2))、重複なしは null(=return(0))。</returns>
    public static CircuitParseError? CheckDuplicateControlTargets(IReadOnlyList<ControlSpecEntry> controlSpecs)
    {
        ArgumentNullException.ThrowIfNull(controlSpecs);

        // 【C原典】制御対象機器重複チェックデータ(SGTCHK)を収集。seikdno[200] を 0 終端まで。
        var entries = new List<ControlTargetCheckEntry>();
        foreach (ControlSpecEntry spec in controlSpecs)
        {
            int limit = Math.Min(spec.ControlTargetSequenceNumbers.Count, 200);
            for (int j = 0; j < limit; j++)
            {
                short oiban = spec.ControlTargetSequenceNumbers[j];
                if (oiban == 0)
                {
                    break;
                }

                entries.Add(new ControlTargetCheckEntry
                {
                    DataSequence = oiban,
                    DescriptionRow = spec.DescriptionRow,
                    DescriptionColumn = spec.DescriptionColumn,
                });
            }
        }

        // 【C原典】qsort(sck, setcnt, sizeof(SGTCHK), sckcmp): 追番→記述行→記述桁の昇順。
        entries.Sort(ControlTargetCheckComparer.Instance);

        // 【C原典】隣接する追番が一致すれば重複エラー。
        for (int i = 0; i < entries.Count - 1; i++)
        {
            if (entries[i].DataSequence == entries[i + 1].DataSequence)
            {
                return new CircuitParseError("FY-904E", entries[i].DescriptionRow, entries[i].DescriptionColumn, "FYMEE80");
            }
        }

        return null;
    }
}
