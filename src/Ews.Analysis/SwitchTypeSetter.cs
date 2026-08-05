using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// CSDT/MCDT の切り換えタイプ(1-2型 / 2-1型)を設定する。
/// 【C原典】CS_MCDT_12_21_SET(toku/sekkei/src/Fyss14.c:6236, 950426/960404)。
///
/// 予約語 CSDT/MCDT かつ予約語指定番号 ysno!="00" かつ切り換えタイプ未設定の要素について、
/// 同一予約語・同一 ysno・切り換えタイプ未設定の後続要素と対を作り、親データ追番 oyatno が
/// 一致すれば 1-2型('1')、一致しなければ 2-1型('2')を双方へ設定する。ただし親が異なるのに
/// 系統番号 kno が同一なら FY-922E を返す。Fyss14_Make_UpperParm のループ後処理群の 1 つ。
/// </summary>
public static class SwitchTypeSetter
{
    /// <summary>記述行/桁のフィールド幅。</summary>
    private const int FieldWidth = 3;

    /// <summary>
    /// 切り換えタイプを設定する(in-place)。
    /// 【C原典】CS_MCDT_12_21_SET(Fyss14.c:6236)。
    /// </summary>
    /// <returns>異常時は <see cref="CircuitParseError"/>(=C の return(-1))、正常時は null(=return(0))。</returns>
    public static CircuitParseError? Set(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        for (int i = 0; i < mains.Count - 1; i++)
        {
            MainCircuitData di = mains[i].Data;

            if ((di.ReservedWord == "CSDT" || di.ReservedWord == "MCDT") &&
                di.DesignationNumber != "00" &&
                di.SwitchType == ' ')
            {
                for (int j = i + 1; j < mains.Count; j++)
                {
                    MainCircuitData dj = mains[j].Data;

                    // 同一予約語・同一予約語指定番号・切り換えタイプ未設定の後続要素と対を作る
                    if (di.ReservedWord == dj.ReservedWord &&
                        di.DesignationNumber == dj.DesignationNumber &&
                        dj.SwitchType == ' ')
                    {
                        if (Eq3(di.ParentSequenceNumber, dj.ParentSequenceNumber))
                        {
                            di.SwitchType = '1';   // 1-2型
                            dj.SwitchType = '1';
                        }
                        else
                        {
                            // 親が異なるのに同一系統=異常
                            if (Eq3(di.SystemNumber, dj.SystemNumber))
                            {
                                return new CircuitParseError(
                                    "FY-922E",
                                    EquipmentParameterFormatter.Stoi(di.DescriptionRow, FieldWidth),
                                    EquipmentParameterFormatter.Stoi(di.DescriptionColumn, FieldWidth),   // 改訂<20>
                                    "FYMEE80");
                            }
                            di.SwitchType = '2';   // 2-1型
                            dj.SwitchType = '2';
                        }
                    }
                }
            }
        }

        return null;
    }

    // 【C原典】memcmp(a, b, 3): 先頭 3 バイトを序数比較。
    private static bool Eq3(string? a, string? b) =>
        string.CompareOrdinal((a ?? string.Empty).PadRight(3)[..3], (b ?? string.Empty).PadRight(3)[..3]) == 0;
}
