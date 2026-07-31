using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 末端回路行種先頭機器フラグ(sentflg)をセットする。下流パラメータ生成
/// (<c>Fyss15_Make_LowerParm</c>)の第3ステップ。
///
/// 【C原典】<c>Fyss34_MattanGyouSento_Set</c>(toku/sekkei/src/Fyss34.c)。
///
/// 処理:
///   1. 全レコードの先頭機器フラグをクリア(' ')。
///   2. 末端機器(mattan='1')ごとに、同一行種グループ番号(gyoglno)で回路要素が
///      主回路('1')/主回路振り分け('5')のレコードを抽出し、階層番号×1000+直列追番が
///      最小のレコード群に先頭機器フラグ='1' をセットする。
///      ただし計器の WH(kiryoso='3')・CT(kiryoso='2')は対象外(950405)。
/// </summary>
public static class LeadingEquipmentFlagSetter
{
    /// <summary>
    /// 末端回路行種先頭機器フラグをセットする(<paramref name="mains"/> を破壊的に更新)。
    /// 【C原典】Fyss34_MattanGyouSento_Set(Pmainc, maina)。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】maina(件数 Pmainc)。</param>
    public static void SetLeadingEquipmentFlag(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        // 1. 先頭機器フラグクリア。
        foreach (MainCircuitResult m in mains)
        {
            m.Work.LeadingEquipmentFlag = ' ';
        }

        // 2. 末端機器と同一行種グループ番号を抽出し、最小ランクへフラグセット。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (di.TerminalKind != '1')
            {
                continue;
            }

            // 計器の WH(VT無)/CT(VT無)は対象外(950405)。
            if ((Matches(di.ReservedWord, "WH", 8) && di.CircuitElement == '3') ||
                (Matches(di.ReservedWord, "CT", 8) && di.CircuitElement == '2'))
            {
                continue;
            }

            // 同一行種グループ番号かつ回路要素 '1'/'5' を抽出。【C原典】qs[]。fno/kaisono/chokuno は
            // C では CHAR だが、実データ範囲では int と等価(ランク計算は kaisono×1000+chokuno)。
            var group = new List<(int RecordIndex, int Rank)>();
            for (int j = 0; j < mains.Count; j++)
            {
                MainCircuitData dj = mains[j].Data;
                if (!Matches(di.LineTypeGroupNumber, dj.LineTypeGroupNumber, 3))
                {
                    continue;
                }

                if (dj.CircuitElement == '1' || dj.CircuitElement == '5')
                {
                    int kaisono = EquipmentParameterFormatter.Stoi(dj.HierarchyNumber, 3);
                    int chokuno = EquipmentParameterFormatter.Stoi(dj.SeriesNumber, 3);
                    group.Add((j, (kaisono * 1000) + chokuno));
                }
            }

            if (group.Count == 0)
            {
                continue;
            }

            // ランク昇順ソート後、最小ランク群にフラグセット。
            group.Sort((a, b) => a.Rank - b.Rank);
            int min = group[0].Rank;
            foreach ((int recordIndex, int rank) in group)
            {
                if (rank != min)
                {
                    break;
                }

                mains[recordIndex].Work.LeadingEquipmentFlag = '1';
            }
        }
    }

    // 【C原典】memcmp/strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
