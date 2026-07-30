using Ews.Domain.Analysis;
using Ews.Domain.Configuration;

namespace Ews.Analysis;

/// <summary>
/// 暁工場3F(ゾーンコード 78007)の製作図で、AL付きハーフサイズブレーカ(CT・AL・メーカー無指定)の
/// メーカーを三菱(M)に強制する。AL付きハーフサイズは在庫が少ないため三菱に固定する運用対応。
/// 【C原典】<c>PropChgCTALMaker</c>(toku/sekkei/src/Fysk00.c:6294 改訂&lt;72&gt;)。
///
/// ゾーンコードは OS 環境変数(ZONECD)ではなく <see cref="IRuntimeParameterProvider"/> から取得する。
/// 【C原典】FyGetZoneCD(getenv("ZONECD"))。
/// </summary>
public static class CtAlBreakerMakerAdjuster
{
    /// <summary>暁工場3F のゾーンコード。【C原典】strcmp(zonecd,"78007")。</summary>
    private const string AkatsukiFactory3FZoneCode = "78007";

    /// <summary>データタイプ/メーカーコードの比較幅。【C原典】strncmp(...,3)。</summary>
    private const int FieldWidth = 3;

    /// <summary>予約語の比較幅。【C原典】strncmp(yoyaku,"MCB ",4)。</summary>
    private const int ReservedWordKeyWidth = 4;

    /// <summary>三菱製メーカーコード(3 桁)。【C原典】strncpy(mcod[0],"M  ",3)。</summary>
    private const string MitsubishiMakerCode = "M  ";

    /// <summary>
    /// AL付きハーフサイズブレーカのメーカーコードを三菱に強制する。
    /// 【C原典】<c>PropChgCTALMaker(sk, mcod, msu)</c>。
    /// </summary>
    /// <param name="parameters">実行時パラメータ(ゾーンコード取得)。【C原典】FyGetZoneCD。</param>
    /// <param name="circuit">主回路データ。【C原典】struct FYRT800 *sk の dt。</param>
    /// <param name="makerCodes">現行メーカーコード選定順位(各 3 桁)。【C原典】mcod[][3]・件数 *msu。</param>
    /// <returns>調整後のメーカーコード選定順位。対象外は入力をそのまま返す。</returns>
    public static IReadOnlyList<string> AdjustMakerCodes(
        IRuntimeParameterProvider parameters,
        MainCircuitData circuit,
        IReadOnlyList<string> makerCodes)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(makerCodes);

        // 【C原典】暁工場3Fの製作図でなければ変更しない。
        if (!string.Equals(parameters.ZoneCode, AkatsukiFactory3FZoneCode, StringComparison.Ordinal))
        {
            return makerCodes;
        }

        // 【C原典】datatype[2]!="AL " || datatype[0]!="CT " || fpamk!="   "
        //         → メーカー指定あり、または AL付きハーフサイズでない。
        if (!MatchField(circuit.DataType[2], "AL ", FieldWidth)
            || !MatchField(circuit.DataType[0], "CT ", FieldWidth)
            || !MatchField(circuit.AttachedParameter.MakerCode, "   ", FieldWidth))
        {
            return makerCodes;
        }

        // 【C原典】yoyaku が "MCB "/"ELB " のいずれでもなければ対象外。
        if (!MatchField(circuit.ReservedWord, "MCB ", ReservedWordKeyWidth)
            && !MatchField(circuit.ReservedWord, "ELB ", ReservedWordKeyWidth))
        {
            return makerCodes;
        }

        // 【C原典】mcod[0]="M  "(三菱)のみ残し、以降を空白で潰して件数を 1 にする。
        return [MitsubishiMakerCode];
    }

    /// <summary>先頭 <paramref name="width"/> 桁を右空白詰めで比較する。【C原典】strncmp(値, expected, width)==0。</summary>
    private static bool MatchField(string? value, string expected, int width)
    {
        string padded = (value ?? string.Empty).PadRight(width);
        return string.CompareOrdinal(padded[..width], expected) == 0;
    }
}
