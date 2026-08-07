namespace Ews.Analysis;

using System.Collections.Generic;
using Ews.Domain.Circuits;

/// <summary>
/// 回路設計エリア(FYDF805)から指定記述行の回路内容記述を取得する。
/// 【C原典】toku/sekkei/src/Fysk11.c の <c>Fysk11_FYDF805_GyoGet</c>(改訂&lt;5&gt;)。
///
/// 記述桁が回路内容記述エリア長(KAIROARLEN=200)以上の場合、回路記述が複数行に
/// 渡るとみなして記述行を繰り上げてから、削除行(cmd=='D')を除く行番号一致の
/// 最初のレコードの回路内容記述(kairoar)を返す。
/// </summary>
public static class CircuitAreaLineReader
{
    /// <summary>
    /// 指定記述行/桁に対応する回路内容記述を取得する。【C原典】<c>Fysk11_FYDF805_GyoGet(kk_gyo, kk_keta, kkstr)</c>。
    /// </summary>
    /// <param name="descriptionRow">回路記述行。【C原典】kk_gyo(先頭3文字を使用)。</param>
    /// <param name="descriptionColumn">回路記述桁。【C原典】kk_keta(先頭3文字を使用)。</param>
    /// <param name="circuitLines">回路内容記述レコード群。【C原典】グローバル f805[0..f805_num)。</param>
    /// <returns>一致行の回路内容記述。未一致は空文字。【C原典】kkstr。</returns>
    public static string GetCircuitAreaText(
        string? descriptionRow,
        string? descriptionColumn,
        IReadOnlyList<CircuitDescriptionLine> circuitLines)
    {
        ArgumentNullException.ThrowIfNull(circuitLines);

        // 【C原典】記述行/桁の先頭3文字を数値化(strncpy ...,3 → LibCharToShort)。
        int row = EquipmentParameterFormatter.Stoi(descriptionRow, 3);
        int column = EquipmentParameterFormatter.Stoi(descriptionColumn, 3);

        // 【C原典】回路記述が複数行に渡る場合(桁が KAIROARLEN 以上)は行を繰り上げる。
        if (column >= CircuitDescriptionLine.CircuitTextLength)
        {
            int over = column / CircuitDescriptionLine.CircuitTextLength;
            row += over;
        }

        // 【C原典】f805 を走査し削除行(cmd=='D')を除き行番号一致の最初のレコードを返す。
        foreach (CircuitDescriptionLine line in circuitLines)
        {
            if (line.Command == 'D')
            {
                continue;
            }
            if (line.LineNumber != row)
            {
                continue;
            }

            return line.CircuitText;
        }

        return string.Empty;
    }
}
