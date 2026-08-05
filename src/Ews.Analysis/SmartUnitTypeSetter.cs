using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// スマートユニットシステム対応(AM/VM のパラメータタイプ設定)。
/// 【C原典】toku/sekkei/src/FyssU0.c <c>PropSetAMprm</c> / <c>PropSetVMprm</c>
/// (2005/03/04 石川 和男, 改訂&lt;1&gt; 2005/05/09 CT 付き AM 対応)。
///
/// 制御対象機器/制御電源のデータ追番をもとに、同一電源系統・同一階層の
/// AM(電流計)/VM(電圧計)へタイプパラメータ(3 倍公称・カバー下半分透明色)を設定する。
/// 【C原典】統括 <c>FyssU0_Smart_Type</c>(Fysk10_Main からコール)は制御回路設計エリア
/// (FYRT802)を走査してスマートユニットの制御対象/制御電源データ追番を取得するが、
/// FYRT802 は未移植のため、本クラスでは追番文字列を引数に取る下請け 2 関数のみを段階移植する。
/// </summary>
public static class SmartUnitTypeSetter
{
    /// <summary>河村製メーカー区分。【C原典】strncmp(fp.fpamk,"K ",2)。</summary>
    private const string KawamuraMaker = "K ";

    /// <summary>
    /// 制御対象機器データ追番をもとに AM のパラメータタイプを設定する(in-place)。
    /// 【C原典】PropSetAMprm(FyssU0.c:113)。
    /// </summary>
    /// <param name="mains">主回路レコード列。対象 AM の DataType を in-place 更新する。【C原典】f800(件数 f800_cnt)。</param>
    /// <param name="controlTargetDataNumber">制御対象機器データ追番。【C原典】seikdno。</param>
    /// <returns>河村製でない AM を検出した場合の設計エラー(FY-574E)。無ければ null。【C原典】ret 0/-1・Perrc/Perra。</returns>
    public static CircuitParseError? SetAmType(IReadOnlyList<MainCircuitResult> mains, string? controlTargetDataNumber)
    {
        ArgumentNullException.ThrowIfNull(mains);

        // 【C原典】制御対象機器を取得(データ追番一致)。無ければ何もしない。
        int idx = FindByDataNumber(mains, controlTargetDataNumber);
        if (idx < 0)
        {
            return null;
        }

        MainCircuitData target = mains[idx].Data;

        // 【C原典】制御対象機器と同じ上流並列追番・階層番号・並列追番の AM にタイプを設定。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;

            if (!Matches(d.ReservedWord, "AM ", 3))
            {
                continue;
            }

            // 【C原典】河村製の AM でなければ FY-574E で中断。
            if (!Matches(d.AttachedParameter.MakerCode, KawamuraMaker, 2))
            {
                return MakeError(d);
            }

            // 【C原典】改訂<1>: 電源系統,上流並列追番,階層番号,行種グループ番号 が同じ。
            if (Matches(d.SystemNumber, target.SystemNumber, 3) &&
                Matches(d.UpperParallelNumber, target.UpperParallelNumber, 3) &&
                Matches(d.HierarchyNumber, target.HierarchyNumber, 3) &&
                Matches(d.LineTypeGroupNumber, target.LineTypeGroupNumber, 3))
            {
                // 【C原典】並列追番,又は直列追番が同じ。
                if (Matches(d.ParallelNumber, target.ParallelNumber, 3) ||
                    Matches(d.SeriesNumber, target.SeriesNumber, 3))
                {
                    d.DataType[0] = "3BK    ";  // 3 倍公称
                    d.DataType[5] = "G      ";  // カバー下半分の色(透明色)
                    break;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 制御電源データ追番の機器の電源系統をもとに VM のパラメータタイプを設定する(in-place)。
    /// 【C原典】PropSetVMprm(FyssU0.c:200)。
    /// </summary>
    /// <param name="mains">主回路レコード列。対象 VM の DataType を in-place 更新する。【C原典】f800(件数 f800_cnt)。</param>
    /// <param name="controlPowerDataNumber">制御電源データ追番。【C原典】seivdno。</param>
    /// <returns>河村製でない VM を検出した場合の設計エラー(FY-574E)。無ければ null。</returns>
    public static CircuitParseError? SetVmType(IReadOnlyList<MainCircuitResult> mains, string? controlPowerDataNumber)
    {
        ArgumentNullException.ThrowIfNull(mains);

        // 【C原典】制御電源データ追番の機器を取得。無ければ何もしない。
        int idx = FindByDataNumber(mains, controlPowerDataNumber);
        if (idx < 0)
        {
            return null;
        }

        MainCircuitData target = mains[idx].Data;

        // 【C原典】制御電源データ追番の機器の電源系統と同じ VM にタイプを設定。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;

            if (!Matches(d.ReservedWord, "VM ", 3))
            {
                continue;
            }

            // 【C原典】河村製の VM でなければ FY-574E で中断。
            if (!Matches(d.AttachedParameter.MakerCode, KawamuraMaker, 2))
            {
                return MakeError(d);
            }

            // 【C原典】電源系統が異なる VM はパス。
            if (!Matches(d.SystemNumber, target.SystemNumber, 3))
            {
                continue;
            }

            d.DataType[4] = "G      ";  // カバー下半分の色(透明色)
            break;
        }

        return null;
    }

    /// <summary>【C原典】データ追番一致の機器 index を返す(strncmp(datano, key, sizeof(datano)=3))。</summary>
    private static int FindByDataNumber(IReadOnlyList<MainCircuitResult> mains, string? dataNumber)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            if (Matches(mains[i].SequenceNumber, dataNumber, 3))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>【C原典】Fyss13ErrSet("FY-574E", gyo, keta, ...)。mapid=" FYMEE80"。</summary>
    private static CircuitParseError MakeError(MainCircuitData d) => new(
        "FY-574E",
        EquipmentParameterFormatter.Stoi(d.DescriptionRow, 3),
        EquipmentParameterFormatter.Stoi(d.DescriptionColumn, 3),
        "FYMEE80");

    /// <summary>【C原典】strncmp(value, expected, width)==0 相当(空白右詰め比較)。</summary>
    private static bool Matches(string? value, string? expected, int width) =>
        string.CompareOrdinal(Pad(value, width), Pad(expected, width)) == 0;

    private static string Pad(string? s, int width) => (s ?? string.Empty).PadRight(width)[..width];
}
