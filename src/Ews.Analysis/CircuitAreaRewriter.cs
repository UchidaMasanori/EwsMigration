using System.Globalization;
using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 機器選定で確定した上位／下位機器の数値電気パラメータ(<see cref="NumericElectricalParameters"/>)を、
/// 回路側の整形済み電気パラメータ(<see cref="ElectricalParameters"/>)へ書き戻す。
/// 【C原典】<c>Fysk00_Area_Rewrite</c> / <c>Fysk00_Set_Kairo</c> / <c>Fysk00_Set_Datachi</c>
/// (toku/sekkei/src/Fysk00.c:3685 / 3738 / 3775)。
///
/// <see cref="AreaRewriteFlags"/>(【C原典】WK_STRUCT3)で指示された項目について、
/// 数値パラメータ(sep)から <see cref="RatingKeyBuilder.GetDataValue(short, NumericElectricalParameters)"/>
/// で値を取り出し、回路側項目番号の書式(桁数・小数桁)で整形して回路側(epa)の該当フィールドへ格納する。
/// </summary>
public static class CircuitAreaRewriter
{
    /// <summary>正常終了。【C原典】GOOD(fyrt808.h)。</summary>
    public const short Good = 0;

    /// <summary>システムエラー。【C原典】SYS_ERR(fyrt808.h)。</summary>
    public const short SystemError = -1;

    /// <summary>
    /// 回路側書き戻し対象項目の定義。【C原典】<c>struct KAIRO_T</c> / <c>kairo_t[]</c>
    /// (toku/include/sekkei/fyrt817.h:919/926)。
    /// </summary>
    /// <param name="Symbol">シンボル。【C原典】symb[4]。</param>
    /// <param name="Width">桁数。【C原典】ln。</param>
    /// <param name="DecimalScale">小数部桁数。【C原典】dln。</param>
    /// <param name="ItemNo">回路側項目番号。【C原典】kno。</param>
    private readonly record struct CircuitFieldMap(string Symbol, short Width, short DecimalScale, short ItemNo);

    /// <summary>【C原典】kairo_t[](fyrt817.h:926)。AT/A/AF/MA0/MA1/MA2/AM の7項目。</summary>
    private static readonly CircuitFieldMap[] KairoTable =
    {
        new("AT", 9, 3, 9),    // at
        new("A", 9, 3, 11),    // a2
        new("AF", 9, 3, 8),    // af
        new("MA0", 4, 0, 16),  // ma
        new("MA1", 4, 0, 17),  // ma
        new("MA2", 4, 0, 18),  // ma
        new("AM", 3, 0, 28),   // am
    };

    /// <summary>感度電流のシンボル。【C原典】static CHAR M[3][4]={"MA0","MA1","MA2"}。</summary>
    private static readonly string[] MaSymbols = { "MA0", "MA1", "MA2" };

    /// <summary>
    /// 上位(epa[1]/sep[1])・下位(epa[2]/sep[2])機器について、指示フラグの項目を回路側へ書き戻す。
    /// 【C原典】<c>Fysk00_Area_Rewrite</c>(Fysk00.c:3685)。
    /// </summary>
    /// <param name="epa">回路側電気パラメータ配列(要素数3以上)。【C原典】struct eparmg epa[]。</param>
    /// <param name="sep">数値化済み電気パラメータ配列(要素数3以上)。【C原典】struct eparmg_s sep[]。</param>
    /// <param name="flags">書き戻し指示フラグ。【C原典】WK_STRUCT3 wk3。</param>
    /// <returns><see cref="Good"/> または <see cref="SystemError"/>。</returns>
    public static short Rewrite(ElectricalParameters[] epa, NumericElectricalParameters[] sep, AreaRewriteFlags flags)
    {
        ArgumentNullException.ThrowIfNull(epa);
        ArgumentNullException.ThrowIfNull(sep);
        ArgumentNullException.ThrowIfNull(flags);
        if (epa.Length < 3 || sep.Length < 3)
        {
            // 【C原典】Area_Rewrite は epa[i+1]/sep[i+1](i=0,1)を参照する=要素1,2が必須。
            throw new ArgumentException("epa/sep は要素数3以上が必要です。");
        }

        for (int i = 0; i < 2; i++)
        {
            NumericElectricalParameters s = sep[i + 1];
            ElectricalParameters e = epa[i + 1];

            if (flags.At[i] && SetKairo("AT", s, e) != Good)
            {
                return SystemError;
            }

            if (flags.A2[i] && SetKairo("A", s, e) != Good)
            {
                return SystemError;
            }

            if (flags.Af[i] && SetKairo("AF", s, e) != Good)
            {
                return SystemError;
            }

            if (flags.Ma[i])
            {
                foreach (string sym in MaSymbols)
                {
                    if (SetKairo(sym, s, e) != Good)
                    {
                        return SystemError;
                    }
                }
            }

            if (flags.Am[i] && SetKairo("AM", s, e) != Good)
            {
                return SystemError;
            }
        }

        return Good;
    }

    /// <summary>
    /// シンボルに対応する回路側項目へ、数値パラメータの値を整形して書き戻す。
    /// 【C原典】<c>Fysk00_Set_Kairo</c>(Fysk00.c:3738)。
    /// </summary>
    private static short SetKairo(string symbol, NumericElectricalParameters sep, ElectricalParameters epa)
    {
        foreach (CircuitFieldMap map in KairoTable)
        {
            if (string.Equals(symbol, map.Symbol, StringComparison.Ordinal))
            {
                // 【C原典】ifc = Fysk00_Get_Datachi(kno, sep, dmy); ifc.su.fsu(数値)。
                double value = RatingKeyBuilder.GetDataValue(map.ItemNo, sep).Numeric;

                // 【C原典】sprintf(frm,"%%%02d.%df", ln, dln) → "%09.3f" 等の動的書式。
                string format = "%" + map.Width.ToString("00", CultureInfo.InvariantCulture)
                                    + "." + map.DecimalScale.ToString(CultureInfo.InvariantCulture) + "f";
                string str = EquipmentParameterFormatter.SprintfF(format, value);

                SetDatachi(map.ItemNo, str, epa);
                return Good;
            }
        }

        return SystemError;
    }

    /// <summary>
    /// 指定した回路側項目番号のフィールドへ整形済み文字列を格納する。
    /// 【C原典】<c>Fysk00_Set_Datachi</c>(Fysk00.c:3775)。<c>memcpy(field, str, sizeof(field))</c> を
    /// フィールド幅ぶんの転記(超過分は切り捨て)として再現する。
    /// </summary>
    private static void SetDatachi(short itemNo, string str, ElectricalParameters ep)
    {
        switch (itemNo)
        {
            case 8:  // af
                ep.Af = Fit(str, 9);
                break;
            case 9:  // at
                ep.At = Fit(str, 9);
                break;
            case 11: // a2
                ep.A2 = Fit(str, 9);
                break;
            case 16: // ma0
                ep.Ma[0] = Fit(str, 4);
                break;
            case 17: // ma1
                ep.Ma[1] = Fit(str, 4);
                break;
            case 18: // ma2
                ep.Ma[2] = Fit(str, 4);
                break;
            case 28: // am
                ep.Am = Fit(str, 3);
                break;
        }
    }

    /// <summary>
    /// 【C原典】<c>memcpy(field, str, sizeof(field))</c> 相当。フィールド幅ぶんを転記する。
    /// str が長ければ先頭 width 文字へ切り詰め、短ければ末尾を '0' で埋める(書式幅=フィールド幅のため通常は等長)。
    /// </summary>
    private static string Fit(string str, int width)
        => str.Length >= width ? str[..width] : str.PadRight(width, '0');
}
