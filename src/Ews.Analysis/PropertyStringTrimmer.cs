namespace Ews.Analysis;

/// <summary>
/// 文字列の前後にある半角スペース('  ' 0x20)を除去する。
/// 【C原典】<c>PropTrimSpace</c>(toku/sekkei/src/Fysk00.c:6253, 改訂&lt;70&gt;)。
/// </summary>
public static class PropertyStringTrimmer
{
    /// <summary>
    /// 文字列の末尾→先頭の順に半角スペースのみを除去する。【C原典】<c>PropTrimSpace(CHAR *s)</c>。
    /// </summary>
    /// <param name="value">対象文字列。【C原典】CHAR *s(入出力。NULL は -1 復帰=未変更)。</param>
    /// <returns>前後の半角スペースを除去した文字列。null は空文字扱い。</returns>
    public static string TrimSpaces(string? value)
    {
        // 【C原典】s==NULL は -1 復帰(未変更)。全角スペースやタブは対象外(半角 0x20 のみ)。
        return value is null ? string.Empty : value.Trim(' ');
    }
}
