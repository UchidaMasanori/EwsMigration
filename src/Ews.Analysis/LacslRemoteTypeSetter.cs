using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// LACSL(ラクスル)リモコンシステムの機器タイプ設定。
/// 【C原典】toku/sekkei/src/Fyss1p.c <c>Fyss1p_LACSL_RryType</c> /
/// <c>PropSetRRYprm</c> / <c>PropCheckOyaTrip</c>(2005/08/16)。
///
/// 予約語 RTR が "LA" タイプなら、同一電源系統(kno)の全 RRY を LACSL リモコンとして
/// タイプパラメータ datatype[1] に "LA" を設定する。設定前に RRY の親器のトリップ電流
/// (ep[0].epaat)が 30.0 以上なら FY-800E(AT 値入力誤り)を報告する。
/// 【C原典】Fysk10_Main() からコールされる。
/// </summary>
public static class LacslRemoteTypeSetter
{
    /// <summary>親器トリップ電流のリミット値(これ以上は超過)。【C原典】const DOUBLE limit = 30.0。</summary>
    private const double TripLimit = 30.0;

    /// <summary>LACSL RRY に設定するタイプパラメータ。【C原典】strncpy(datatype[1],"LA     ",7)。</summary>
    private const string LacslType = "LA     ";

    /// <summary>
    /// LACSL リモコンの機器タイプを設定する(in-place)。【C原典】Fyss1p_LACSL_RryType(Fyss1p.c:44)。
    /// </summary>
    /// <param name="mains">主回路レコード列。対象 RRY の datatype[1] を in-place 更新する。【C原典】f800(件数 f800_cnt)。</param>
    /// <returns>親器トリップ電流超過で報告された設計エラー(FY-800E)の一覧。無ければ空。【C原典】Perrc/Perra。</returns>
    public static IReadOnlyList<CircuitParseError> Apply(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        var errors = new List<CircuitParseError>();

        // 【C原典】RTR が "LA" タイプなら、すべての RRY を LACSL リモコンとする。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;

            // 【C原典】LA タイプの RTR でなければ対象外。
            if (!Matches(d.ReservedWord, "RTR ", 4) || !Matches(d.DataType[0], "LA ", 3))
            {
                continue;
            }

            // 【C原典】ret = PropSetRRYprm(...); if(ret != 0) break;
            if (SetRryType(mains, d.SystemNumber, errors) != 0)
            {
                break;
            }
        }

        return errors;
    }

    /// <summary>
    /// 指定電源系統の LACSL RRY にタイプをセットする。【C原典】PropSetRRYprm(Fyss1p.c:91)。
    /// </summary>
    /// <returns>0: LACSL リモコンあり、-1: なし(または親器トリップ超過で中断)。</returns>
    private static int SetRryType(IReadOnlyList<MainCircuitResult> mains, string? kno, List<CircuitParseError> errors)
    {
        int ret = -1;

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;

            // 【C原典】電源系統が異なる。
            if (!Matches(d.SystemNumber, kno, 3))
            {
                continue;
            }

            // 【C原典】予約語 RRY 以外は対象外。
            if (!Matches(d.ReservedWord, "RRY ", 4))
            {
                continue;
            }

            // 【C原典】親器のトリップ電流超過をチェック。超過なら ret=-1 で中断。
            if (CheckParentTrip(mains, d.ParentSequenceNumber, errors) != 0)
            {
                ret = -1;
                break;
            }

            // 【C原典】タイプパラメータに "LA" セット。
            d.DataType[1] = LacslType;
            ret = 0;
        }

        return ret;
    }

    /// <summary>
    /// LACSL RRY の親器のトリップ電流超過をチェックする。【C原典】PropCheckOyaTrip(Fyss1p.c:158)。
    /// </summary>
    /// <returns>0: 正常、-1: リミット値オーバー(FY-800E を収集)。</returns>
    private static int CheckParentTrip(IReadOnlyList<MainCircuitResult> mains, string? oyatno, List<CircuitParseError> errors)
    {
        // 【C原典】idx = LibCharToShort(oyatno,3) - 1;(親器 index)。
        int idx = EquipmentParameterFormatter.Stoi(oyatno, 3) - 1;

        // 【C原典】f800[idx] を参照。範囲外(親器なし)は防御的にスキップ(整形データでは到達しない)。
        if (idx < 0 || idx >= mains.Count)
        {
            return 0;
        }

        MainCircuitData parent = mains[idx].Data;

        // 【C原典】atof(epaat) >= limit でリミット値オーバー。
        if (EquipmentParameterFormatter.Stof(parent.ElectricalParameterSlots[0].At, 9) >= TripLimit)
        {
            errors.Add(new CircuitParseError(
                "FY-800E",
                EquipmentParameterFormatter.Stoi(parent.DescriptionRow, 3),
                EquipmentParameterFormatter.Stoi(parent.DescriptionColumn, 3),   // 改訂<1>: keta 幅 3
                "FYMEE80"));
            return -1;
        }

        return 0;
    }

    /// <summary>【C原典】strncmp(value, expected, width)==0 相当(空白右詰め比較)。</summary>
    private static bool Matches(string? value, string? expected, int width) =>
        string.CompareOrdinal(Pad(value, width), Pad(expected, width)) == 0;

    private static string Pad(string? s, int width) => (s ?? string.Empty).PadRight(width)[..width];
}
