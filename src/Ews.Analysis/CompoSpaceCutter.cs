namespace Ews.Analysis;

/// <summary>
/// 文字列の前後のスペースを切り捨てる(全角スペースは非対応)。
/// 【C原典】toku/compo/lib/compo_dir/cpspcutr.c の <c>FyCpSpcutr</c>(libcompo.a)。
///
/// 先頭は半角スペース(0x20)のみを読み飛ばし、末尾は半角スペースと改行(<c>\n</c>)を
/// 除去する。<see cref="PropertyStringTrimmer.TrimSpaces"/>(PropTrimSpace)とは
/// 先頭改行の扱い・末尾改行除去・全空白時の空文字化で挙動が異なる。
/// </summary>
public static class CompoSpaceCutter
{
    /// <summary>
    /// 先頭の半角スペースと末尾の空白/改行を除去する。【C原典】<c>FyCpSpcutr(CHAR *str)</c>。
    /// </summary>
    /// <param name="str">対象文字列。null/空は空文字を返す。</param>
    /// <returns>前後の空白を除去した文字列。</returns>
    public static string CutSpaces(string? str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return string.Empty;
        }

        // 【C原典】先頭の半角スペースのみ読み飛ばす(改行'\n'は読み飛ばさない)。
        int start = 0;
        while (start < str.Length && str[start] == ' ')
        {
            start++;
        }

        // 【C原典】非スペースが無く終端に達した(全て半角スペース)なら空文字。
        if (start >= str.Length)
        {
            return string.Empty;
        }

        // 【C原典】末尾の空白/改行を除いた最後の有効文字位置 e(初期 NULL)。
        int end = -1;
        for (int i = start; i < str.Length; i++)
        {
            if (str[i] != ' ' && str[i] != '\n')
            {
                end = i;
            }
        }

        // 【C原典】e が NULL のまま(start 以降が空白/改行のみ)なら空文字。
        if (end < 0)
        {
            return string.Empty;
        }

        return str[start..(end + 1)];
    }
}
