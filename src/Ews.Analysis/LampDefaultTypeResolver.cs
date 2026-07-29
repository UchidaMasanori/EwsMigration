namespace Ews.Analysis;

/// <summary>
/// ランプ機器のデフォルト機器タイプ(LED)を判定する。
/// 【C原典】<c>PropSetDefLampType</c>(toku/sekkei/src/Fysk00.c:5290, 改訂&lt;65&gt;)。
///
/// 回路内容記述にタイプ指定「+(...)」が無い、または括弧内に「NP」指定が無い場合は
/// デフォルトの "LED    " を採用する。「NP」指定がある場合は現行タイプを据え置く。
/// </summary>
public static class LampDefaultTypeResolver
{
    /// <summary>LED デフォルトタイプ(7 桁)。【C原典】strcpy(def_type,"LED    ")。</summary>
    private const string LedType = "LED    ";

    /// <summary>タイプ指定開始マーカー。【C原典】strstr(kairoar,"+(")。</summary>
    private const string TypeMarker = "+(";

    /// <summary>タイプ無し(NP)指定。【C原典】strstr(kairoar,"NP")。</summary>
    private const string NoTypeMarker = "NP";

    /// <summary>
    /// ランプの既定機器タイプを判定する。【C原典】<c>PropSetDefLampType(kairoar, def_type)</c>。
    /// </summary>
    /// <param name="circuitDescription">回路内容記述。【C原典】CHAR *kairoar(入出力。C原典は ')' で切詰めるが呼出側は再利用しない)。</param>
    /// <param name="currentType">現行の機器タイプ。【C原典】CHAR *def_type(入出力。NP 指定時は据置)。</param>
    /// <returns>判定後の機器タイプ。タイプ指定/NP 無しは "LED    "、NP 有りは現行タイプ据置。</returns>
    public static string ResolveDefaultType(string? circuitDescription, string? currentType)
    {
        string description = circuitDescription ?? string.Empty;

        // 【C原典】"+(" が無ければ LED をデフォルト設定。
        int typeMarker = description.IndexOf(TypeMarker, StringComparison.Ordinal);
        if (typeMarker < 0)
        {
            return LedType;
        }

        // 【C原典】"+(" 以降の ')' で NUL 切詰め。以降の NP 判定は括弧閉じ手前までを対象とする。
        int closeParen = description.IndexOf(')', typeMarker);
        string searchArea = closeParen >= 0 ? description[..closeParen] : description;

        // 【C原典】切詰め後に "NP" が無ければ LED、有れば現行タイプ据置。
        if (searchArea.IndexOf(NoTypeMarker, StringComparison.Ordinal) < 0)
        {
            return LedType;
        }

        return currentType ?? string.Empty;
    }
}
