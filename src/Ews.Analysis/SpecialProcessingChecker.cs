using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// WH/CT/TS の特殊処理チェック(<see cref="SpecialProcessingChecker.Check"/>)の結果。
/// </summary>
/// <param name="Flag">特殊処理フラグ。0:特殊処理しない 1:特殊処理する。【C原典】ret(tokuflg)。</param>
/// <param name="ShapeTypes">表示用出力データタイプ(7枠×7桁)。【C原典】type[][7]。</param>
public sealed record SpecialProcessingResult(int Flag, IReadOnlyList<string> ShapeTypes);

/// <summary>
/// 予約語 WH/CT/TS に対して特殊処理(表示タイプの上書き)を行うか判定する。
/// 【C原典】Fysk0c_Check_Tokusyu(toku/sekkei/src/Fysk0c.c:146)。
///   WH: SP枠(spkvn=='1')かつ封印(fpahu=='H')またはメータ封印(fpamh=='M')なら dtype[2] を出力枠[2]へ。
///       ただしスマートメータ(dtype[4]=="SM ")は特殊処理しない(フラグ0)。
///   CT: dtype[1]=="BOX " なら dtype[1] を出力枠[1]へ。
///   TS: dtype[2]=="SIN " なら dtype[2] を出力枠[2]へ。
///   それ以外はフラグ0・出力は全枠空白。
/// </summary>
public static class SpecialProcessingChecker
{
    /// <summary>出力タイプ枠数。【C原典】type[7][7] の 7 枠。</summary>
    private const int SlotCount = 7;

    /// <summary>1 枠の桁数。【C原典】TSIZE(=7)。</summary>
    private const int SlotWidth = 7;

    /// <summary>
    /// 特殊処理の要否を判定する。【C原典】Fysk0c_Check_Tokusyu(yo, dtype, fp, type) → ret。
    /// </summary>
    /// <param name="reservedWord">主回路エリア予約語。【C原典】yo。</param>
    /// <param name="dataTypes">回路データタイプ(7枠)。【C原典】dtype。</param>
    /// <param name="attachedParameter">回路付属パラメータ。【C原典】fp(fparmg)。</param>
    public static SpecialProcessingResult Check(
        string reservedWord, IReadOnlyList<string> dataTypes, AttachedParameters attachedParameter)
    {
        ArgumentNullException.ThrowIfNull(reservedWord);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(attachedParameter);

        // 【C原典】memset(type[0], ' ', 49)。全7枠を空白(7桁)で初期化。
        string[] type = new string[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            type[i] = new string(' ', SlotWidth);
        }

        int flag = 0;

        if (HasPrefix(reservedWord, "WH ", 3))
        {
            // 【C原典】SP枠(spkvn=='1')かつ封印(fpahu=='H')またはメータ封印(fpamh=='M')。
            if (attachedParameter.SpFutureMountKind == '1' &&
                (attachedParameter.SealKind == 'H' || attachedParameter.MeterSealKind == 'M'))
            {
                type[2] = Slot(dataTypes, 2);
                flag = 1;

                // 【C原典】スマートメータ(dtype[4]=="SM ")は特殊処理しない。
                if (HasPrefix(Slot(dataTypes, 4), "SM ", 3))
                {
                    flag = 0;
                }
            }
        }
        else if (HasPrefix(reservedWord, "CT ", 3))
        {
            if (HasPrefix(Slot(dataTypes, 1), "BOX ", 4))
            {
                type[1] = Slot(dataTypes, 1);
                flag = 1;
            }
        }
        else if (HasPrefix(reservedWord, "TS ", 3))
        {
            if (HasPrefix(Slot(dataTypes, 2), "SIN ", 4))
            {
                type[2] = Slot(dataTypes, 2);
                flag = 1;
            }
        }

        return new SpecialProcessingResult(flag, type);
    }

    // 指定枠を 7 桁左詰めで取得(範囲外は空白)。【C原典】dtype[index]。
    private static string Slot(IReadOnlyList<string> types, int index)
    {
        string value = index >= 0 && index < types.Count ? types[index] ?? string.Empty : string.Empty;
        return value.PadRight(SlotWidth)[..SlotWidth];
    }

    // 【C原典】memcmp(value, prefix, width) == 0 相当(先頭 width 桁一致)。
    private static bool HasPrefix(string value, string prefix, int width)
    {
        string v = (value ?? string.Empty).PadRight(width);
        return v.AsSpan(0, width).SequenceEqual(prefix.AsSpan(0, width));
    }
}
