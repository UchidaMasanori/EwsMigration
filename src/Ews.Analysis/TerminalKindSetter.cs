using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路エリアの末端区分(mattan)をセットする。下流パラメータ生成
/// (<c>Fyss15_Make_LowerParm</c>)の最初のステップ。
///
/// 【C原典】<c>Fyss30_MattanKubun_Set</c>(toku/sekkei/src/Fyss30.c)。
///
/// 処理:
///   1. 全レコードの末端区分を空白でクリアする。
///   2. 系統種別='1'(P系統)のレコードのうち、自データ追番(datano)がどの
///      親データ追番(oyatno)にも存在しないものを末端(mattan='1')とする。
///   3. SC の特別処理(950926): 単一 SC が末端で、直前機器が直列に並ぶとき、
///      末端区分を直前機器へ付け直す(条件により付属パラメータも移送する)。
/// </summary>
public static class TerminalKindSetter
{
    /// <summary>
    /// 主回路エリアの末端区分をセットする(<paramref name="mains"/> を破壊的に更新)。
    /// 【C原典】Fyss30_MattanKubun_Set(Pmainc, maina)。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】maina(件数 Pmainc)。</param>
    public static void SetTerminalKind(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        // 1. 末端区分クリア。
        foreach (MainCircuitResult m in mains)
        {
            m.Data.TerminalKind = ' ';
        }

        // 2. 末端区分セット(P系統かつ自 datano が親 oyatno に不在なら末端)。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (di.SystemKind != '1')
            {
                continue;
            }

            bool notParent = true;
            for (int j = 0; j < mains.Count; j++)
            {
                // datano が親データ追番として存在する時。
                if (Matches(mains[i].SequenceNumber, mains[j].Data.ParentSequenceNumber, 3))
                {
                    notParent = false;
                    break;
                }
            }

            if (notParent)
            {
                di.TerminalKind = '1';
            }
        }

        // 3. SC の特別処理(950926)。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;

            // 末端の単一 SC で、fpaln[1] が "0KW" でなく、直前機器の datano が自 oyatno と一致。
            if (di.TerminalKind != '1' ||
                !Matches(di.ReservedWord, "SC", 8) ||
                Matches(di.AttachedParameter.LoadName[1], "0KW", 3) ||
                i < 1 ||
                !Matches(di.ParentSequenceNumber, mains[i - 1].SequenceNumber, 3))
            {
                continue;
            }

            MainCircuitData prev = mains[i - 1].Data;
            if (Matches(prev.ReservedWord, "MC", 8))
            {
                // 直前が MC の場合、同一行種・行種グループに MGSD/MCSD があれば付け直さない
                // (SC より積算処理を行うため)。
                bool hasStarDelta = false;
                for (int j = 0; j < mains.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    MainCircuitData dj = mains[j].Data;
                    if (Matches(di.LineTypeCode, dj.LineTypeCode, 3) &&
                        Matches(di.LineTypeGroupNumber, dj.LineTypeGroupNumber, 3) &&
                        (Matches(dj.ReservedWord, "MGSD", 8) || Matches(dj.ReservedWord, "MCSD", 8)))
                    {
                        hasStarDelta = true;
                        break;
                    }
                }

                if (!hasStarDelta)
                {
                    ReattachTerminalToPrevious(mains, i);
                }
            }
            else
            {
                ReattachTerminalToPrevious(mains, i);
            }
        }
    }

    /// <summary>
    /// 末端区分を直前機器へ付け直す。直前機器と階層/並列が一致すれば付け直し(必要なら
    /// 付属パラメータを移送)、不一致なら後方に有効負荷が無い場合のみ付け直す。
    /// 【C原典】Fyss30.c の SC 付け直し内側ブロック(MC分岐/非MC分岐で共通)。
    /// </summary>
    private static void ReattachTerminalToPrevious(IReadOnlyList<MainCircuitResult> mains, int i)
    {
        MainCircuitData cur = mains[i].Data;
        MainCircuitData prev = mains[i - 1].Data;

        if (Matches(cur.HierarchyNumber, prev.HierarchyNumber, 3) &&
            Matches(cur.ParallelNumber, prev.ParallelNumber, 3))
        {
            cur.TerminalKind = ' ';
            prev.TerminalKind = '1';

            // 直前の負荷容量が未設定(空白)なら、自機器の負荷パラメータを移送。
            if (Matches(prev.AttachedParameter.LoadKind, "  ", 2))
            {
                AttachedParameters src = cur.AttachedParameter;
                AttachedParameters dst = prev.AttachedParameter;
                dst.LoadKind = src.LoadKind;
                dst.LoadCapacity = src.LoadCapacity;
                dst.LoadUnitKind = src.LoadUnitKind;
                dst.LoadName[0] = src.LoadName[0];
                dst.LoadName[1] = src.LoadName[1];
            }
        }
        else
        {
            // 後方の同一行種・行種グループに有効負荷(fpalw2 != "0000000")があれば付け直さない。
            bool hasDownstreamLoad = false;
            for (int j = i + 1; j < mains.Count; j++)
            {
                MainCircuitData dj = mains[j].Data;
                if (Matches(cur.LineTypeCode, dj.LineTypeCode, 3) &&
                    Matches(cur.LineTypeGroupNumber, dj.LineTypeGroupNumber, 3))
                {
                    if (!Matches(dj.AttachedParameter.LoadCapacity, "0000000", 7))
                    {
                        hasDownstreamLoad = true;
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            if (!hasDownstreamLoad)
            {
                cur.TerminalKind = ' ';
                prev.TerminalKind = '1';
            }
        }
    }

    // 【C原典】memcmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
