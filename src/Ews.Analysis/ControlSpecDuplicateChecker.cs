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

        // 【C原典】制御対象機器重複チェックデータを収集。seikdno[200] を 0 終端まで。
        var entries = new List<(short OiBan, short Row, short Column)>();
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

                entries.Add((oiban, spec.DescriptionRow, spec.DescriptionColumn));
            }
        }

        // 【C原典】qsort(sckcmp): 追番→記述行→記述桁の昇順。
        entries.Sort(static (a, b) =>
        {
            int r = a.OiBan - b.OiBan;
            if (r != 0)
            {
                return r;
            }

            r = a.Row - b.Row;
            if (r != 0)
            {
                return r;
            }

            return a.Column - b.Column;
        });

        // 【C原典】隣接する追番が一致すれば重複エラー。
        for (int i = 0; i < entries.Count - 1; i++)
        {
            if (entries[i].OiBan == entries[i + 1].OiBan)
            {
                return new CircuitParseError("FY-904E", entries[i].Row, entries[i].Column, "FYMEE80");
            }
        }

        return null;
    }
}
