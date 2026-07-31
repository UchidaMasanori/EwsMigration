using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ＳＣ／ＮＴの上流積み上げ区分(jagekbn)をセットする。下流パラメータ生成
/// (<c>Fyss15_Make_LowerParm</c>)の第2ステップ。
///
/// 【C原典】<c>Fyss32_SC_NT_Tumiage_Set</c>(toku/sekkei/src/Fyss32.c)。
///
/// 処理:
///   1. 全レコードの積み上げ区分をクリア('K'→'1' 再セット、他→' ')。
///   2. P系統で予約語 'SC'/'NT' のとき、その直列最上位へ積み上げ区分='1' をセットする
///      (直列追番=1 なら自身、!=1 なら直列先頭 i-(cno-1))。SC は特別処理(951005/951031)。
///   3. 950925: P系統で予約語 'SC' かつ末端でない(mattan=' ')場合、fpaln[1] が "0KW"
///      でなければ積み上げ区分='1' をセットする。
/// </summary>
public static class UpstreamStackingKindSetter
{
    /// <summary>
    /// 上流積み上げ区分をセットする(<paramref name="mains"/> を破壊的に更新)。
    /// 【C原典】Fyss32_SC_NT_Tumiage_Set(Pmainc, maina)。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】maina(件数 Pmainc)。</param>
    public static void SetUpstreamStackingKind(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        // 1. 積み上げ区分クリア('K'は交互運転 → '1' 再セット、他は空白クリア)。
        foreach (MainCircuitResult m in mains)
        {
            m.Data.StackKind = m.Data.StackKind == 'K' ? '1' : ' ';
        }

        // 2. SC/NT の直列最上位へ積み上げ区分セット。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (di.SystemKind != '1')
            {
                continue;
            }

            if (!Matches(di.ReservedWord, "SC", 8) && !Matches(di.ReservedWord, "NT", 8))
            {
                continue;
            }

            int cno = EquipmentParameterFormatter.Stoi(di.SeriesNumber, 3);
            if (cno == 1)
            {
                SetSeriesTopForFirst(mains, i);
            }
            else
            {
                // 直列先頭(i-(cno-1))へ設定。
                int j = i - (cno - 1);
                if (j >= 0 && j < mains.Count)
                {
                    mains[j].Data.StackKind = '1';
                }
            }
        }

        // 3. 950925: 末端でない SC の追加処理。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (di.SystemKind != '1')
            {
                continue;
            }

            if (Matches(di.ReservedWord, "SC", 8) &&
                di.TerminalKind == ' ' &&
                !Matches(di.AttachedParameter.LoadName[1], "0KW", 3))
            {
                di.StackKind = '1';
            }
        }
    }

    /// <summary>
    /// 直列追番=1 の SC/NT に対する積み上げ区分設定。SC は直前 MC との関係で
    /// 設定先(自身 or 直前)を切り替える(951005/951031)。NT・非SC は自身へ設定。
    /// 【C原典】Fyss32.c の cno==1 ブロック。
    /// </summary>
    private static void SetSeriesTopForFirst(IReadOnlyList<MainCircuitResult> mains, int i)
    {
        MainCircuitData di = mains[i].Data;

        if (!Matches(di.ReservedWord, "SC", 8))
        {
            di.StackKind = '1';
            return;
        }

        // SC で fpaln[1] が "0KW" でなく、直前が親(oyatno==prev.datano)かつ MC の場合(951005)。
        if (i >= 1 &&
            !Matches(di.AttachedParameter.LoadName[1], "0KW", 3) &&
            Matches(di.ParentSequenceNumber, mains[i - 1].SequenceNumber, 3) &&
            Matches(mains[i - 1].Data.ReservedWord, "MC", 8))
        {
            // 同一行種・行種グループに MGSD/MCSD があるか。
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
                di.StackKind = '1';
            }
            else
            {
                mains[i - 1].Data.StackKind = '1';
            }
        }
        else
        {
            // 951031: 後方の同一行種・行種グループに有効負荷(fpalw2 != "0000000")があれば自身へ設定。
            bool hasDownstreamLoad = false;
            for (int j = i + 1; j < mains.Count; j++)
            {
                MainCircuitData dj = mains[j].Data;
                if (Matches(di.LineTypeCode, dj.LineTypeCode, 3) &&
                    Matches(di.LineTypeGroupNumber, dj.LineTypeGroupNumber, 3))
                {
                    if (!Matches(dj.AttachedParameter.LoadCapacity, "0000000", 7))
                    {
                        hasDownstreamLoad = true;
                    }

                    break;
                }
            }

            if (hasDownstreamLoad)
            {
                di.StackKind = '1';
            }
        }
    }

    // 【C原典】memcmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
