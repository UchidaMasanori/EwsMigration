using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路レコード列から入線(予約語 "P")の相数を取得する。
/// 【C原典】<c>Fysk00_ph</c>(toku/sekkei/src/Fysk00.c:4413)。
///
/// 指定レコードより上流(添字が小さい側)へ遡り、最初に見つかった入線("P")レコードの相数を返す。
/// ただし入線が三相四線(<c>ep[0].epaph2[0]=='3'</c> かつ <c>ep[0].epawr2[0]=='4'</c>)の場合は、
/// 自機器(指定レコード)の回路相数(<c>dt.kpaph</c>)を返す(No.1196 対応: 1996.07.29)。
/// 入線が見つからない場合は '1'(単相)を返す。
/// </summary>
public static class IncomingPhaseResolver
{
    /// <summary>
    /// 入線相数を取得する。【C原典】<c>Fysk00_ph(struct FYRT800 *sk, SHORT icnt)</c>。
    /// </summary>
    /// <param name="records">主回路レコード列(FYRT800 配列相当)。【C原典】sk[]。</param>
    /// <param name="index">自機器のレコード添字。【C原典】icnt。</param>
    /// <returns>相数を表す 1 文字。入線が無ければ '1'。</returns>
    public static char Resolve(IReadOnlyList<MainCircuitResult> records, int index)
    {
        ArgumentNullException.ThrowIfNull(records);

        // 【C原典】for(i=icnt-1;i>=0;i--) … 上流へ遡って入線を探索。
        for (int i = index - 1; i >= 0; i--)
        {
            MainCircuitData p = records[i].Data;
            if (!IsIncomingLine(p.ReservedWord))
            {
                continue;
            }

            ElectricalParameters ep0 = p.ElectricalParameterSlots[0];

            // 【C原典】入線が三相四線の場合は自分自身(sk[icnt])の回路相数を返す(No1196)。
            if (FirstChar(ep0.Ph2[0]) == '3' && FirstChar(ep0.Wr2[0]) == '4')
            {
                return records[index].Data.CircuitPhaseCount;
            }

            // 【C原典】return(sk[i].dt.ep[0].epaph2[0])。
            return FirstChar(ep0.Ph2[0]);
        }

        return '1';
    }

    /// <summary>
    /// 予約語が入線("P")かを判定する。【C原典】<c>memcmp(yoyaku,"P       ",8)==0</c>(8 バイト完全一致)。
    /// </summary>
    private static bool IsIncomingLine(string? reservedWord)
    {
        string padded = (reservedWord ?? string.Empty).PadRight(8);
        return string.CompareOrdinal(padded, 0, "P       ", 0, 8) == 0;
    }

    /// <summary>固定長 1 文字フィールドの先頭文字。空文字は '\0'(C の未初期化に相当しない安全既定)。</summary>
    private static char FirstChar(string field) => field.Length > 0 ? field[0] : '\0';
}
