namespace Ews.Analysis;

/// <summary>
/// 入力品名と直近上下位データ品名を照合する。
/// 【C原典】Fysk01_Check_Hinmei(toku/sekkei/src/Fysk01.c:4079)。
/// 入力品名の先頭10文字が空白のときは照合せず一致扱い。それ以外は先頭25文字を比較する。
/// </summary>
public static class PartNameChecker
{
    private const int BlankCheckWidth = 10; // 【C原典】memcmp(hinmi,"          ",10)
    private const int CompareWidth = 25;    // 【C原典】memcmp(hinmi,hinmk,25)

    /// <summary>
    /// 品名が一致(GOOD)なら true、不一致(NOGOOD)なら false。
    /// 【C原典】戻り値 GOOD(0)/NOGOOD(1)。
    /// </summary>
    public static bool Matches(string inputPartName, string referencePartName)
    {
        inputPartName ??= string.Empty;
        referencePartName ??= string.Empty;

        // 入力品名先頭10文字が空白ならチェックしない(GOOD)
        if (Take(inputPartName, BlankCheckWidth) == new string(' ', BlankCheckWidth))
        {
            return true;
        }

        return Take(inputPartName, CompareWidth) == Take(referencePartName, CompareWidth);
    }

    private static string Take(string value, int width) =>
        value.PadRight(width)[..width];
}
