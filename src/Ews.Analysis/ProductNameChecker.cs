namespace Ews.Analysis;

/// <summary>
/// 品名一致チェック。【C原典】<c>Fysk01_Check_Hinmei(CHAR *hinmi, CHAR *hinmk)</c>
/// (toku/sekkei/src/Fysk01.c:4079)。
///
/// 機器選定の候補(直近上下位参照ファイル FYDF812)ごとに、利用者が品名を指定していれば
/// 候補側の品名(<c>tmp.hinmei</c>[25])と一致するかを判定する。品名が未指定(先頭 10 バイトが
/// すべて空白)なら絞り込みを行わず OK とする。<c>Fysk01_Chokkin_Read_Check_ALL/_TMS</c> から
/// 呼ばれる純粋関数で、マスタ・記録列・物件に依存しない。
///
/// C原典は固定長 25 バイトの <c>memcmp</c> で比較する。本移植では品名文字列を空白で 25 桁に
/// 右詰めして(25 桁を超える分は切り捨て)バイト等価な比較を再現する。
///
/// 定数(【C原典】fyrt808.h): GOOD == 0 / NOGOOD == 1。
/// </summary>
public static class ProductNameChecker
{
    /// <summary>一致(または品名未指定)。【C原典】GOOD == 0(fyrt808.h:31)。</summary>
    public const int Good = 0;

    /// <summary>不一致。【C原典】NOGOOD == 1(fyrt808.h:32)。</summary>
    public const int NoGood = 1;

    /// <summary>品名フィールド長。【C原典】struct FYDF812 の hinmei[25]。</summary>
    private const int NameLength = 25;

    /// <summary>品名指定有無の判定に用いる先頭バイト数。【C原典】memcmp(hinmi, "          ", 10)。</summary>
    private const int PresenceCheckLength = 10;

    /// <summary>
    /// 品名一致チェック。【C原典】Fysk01_Check_Hinmei(Fysk01.c:4079)。
    /// 指定品名 <paramref name="specifiedName"/> の先頭 10 桁がすべて空白なら品名未指定とみなし
    /// <see cref="Good"/> を返す。指定ありの場合は 25 桁で候補品名 <paramref name="candidateName"/> と
    /// 突き合わせ、一致すれば <see cref="Good"/>、不一致なら <see cref="NoGood"/> を返す。
    /// </summary>
    /// <param name="specifiedName">利用者が指定した品名(【C原典】hinmi)。null は空文字と同等に扱う。</param>
    /// <param name="candidateName">候補側の品名(【C原典】hinmk = tmp.hinmei)。null は空文字と同等に扱う。</param>
    /// <returns><see cref="Good"/> または <see cref="NoGood"/>。</returns>
    public static int Check(string? specifiedName, string? candidateName)
    {
        string specified = PadToField(specifiedName);
        string candidate = PadToField(candidateName);

        // 先頭 10 桁がすべて空白 = 品名未指定 → 絞り込みなし。
        if (specified.AsSpan(0, PresenceCheckLength).IsWhiteSpace())
        {
            return Good;
        }

        return specified.Equals(candidate, StringComparison.Ordinal) ? Good : NoGood;
    }

    /// <summary>品名を空白で 25 桁に右詰めし、超過分は切り捨てる(固定長 memcmp 相当)。</summary>
    private static string PadToField(string? value)
    {
        string source = value ?? string.Empty;
        if (source.Length >= NameLength)
        {
            return source[..NameLength];
        }

        return source.PadRight(NameLength);
    }
}
