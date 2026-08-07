using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// <see cref="MakerCodeSelector.Select"/> の結果(メーカーコード選定順位)。
/// 【C原典】mcod[4][3] と件数 *msu。
/// </summary>
public sealed record MakerCodeSelection(IReadOnlyList<string> MakerCodes, int Count);

/// <summary>
/// 指定予約語に対するメーカーコード選定順位を作成する。
/// 【C原典】Fysk01_MakerCode_Check(toku/sekkei/src/Fysk01.c:2903)。
///   指定メーカーコード dmc が空白なら、L/LGT は空白 1 件、それ以外はメーカー指定域
///   FYDF802 から予約語一致行の mkcd(空白以外)を順位表へ展開する。dmc 指定時は dmc 1 件。
/// </summary>
public static class MakerCodeSelector
{
    /// <summary>メーカーコード順位数。【C原典】mcod[4][3]。</summary>
    public const int MakerCodeCount = 4;

    /// <summary>メーカーコード桁数。【C原典】mcod[4][3]。</summary>
    public const int MakerCodeWidth = 3;

    private const string BlankCode = "   ";
    private const int ReservedWordWidth = 8;

    /// <summary>
    /// メーカーコード選定順位を作成する。【C原典】Fysk01_MakerCode_Check(yo, dmc, mn, mtbl, msu, mcod)。
    /// </summary>
    /// <param name="reservedWord">指定予約語。【C原典】yo。</param>
    /// <param name="designatedMakerCode">指定メーカーコード(3 桁)。空白なら自動選定。【C原典】dmc。</param>
    /// <param name="makerTable">メーカー指定域(FYDF802 [])。【C原典】mtbl・mn=要素数。</param>
    public static MakerCodeSelection Select(
        string reservedWord,
        string designatedMakerCode,
        IReadOnlyList<MakerDesignation> makerTable)
    {
        ArgumentNullException.ThrowIfNull(makerTable);

        // 【C原典】memset(mcod,' ',12); *msu=0;
        string[] mcod = [BlankCode, BlankCode, BlankCode, BlankCode];
        int msu = 0;

        // 【C原典】dmc 指定あり: mcod[0]=dmc, *msu=1。
        if (Take(designatedMakerCode, MakerCodeWidth) != BlankCode)
        {
            mcod[0] = Take(designatedMakerCode, MakerCodeWidth);
            return new MakerCodeSelection(mcod, 1);
        }

        // 【C原典】L/LGT は自動選定せずメーカー空白 1 件。
        if (Take(reservedWord, 2) == "L " || Take(reservedWord, 4) == "LGT ")
        {
            return new MakerCodeSelection(mcod, 1);
        }

        // 【C原典】予約語一致行の mkcd(空白以外)を順位表へ展開。
        bool found = false;
        foreach (MakerDesignation maker in makerTable)
        {
            if (Take(reservedWord, ReservedWordWidth) != Take(maker.ReservedWord, ReservedWordWidth))
            {
                continue;
            }

            for (int j = 0; j < MakerCodeCount; j++)
            {
                string code = Take(maker.MakerCodes[j], MakerCodeWidth);
                if (code != BlankCode)
                {
                    mcod[msu] = code;
                    msu++;
                }
            }
            found = true;
            break;
        }

        // 【C原典】一致行なし: mcod[0]=空白, *msu=1。
        if (!found)
        {
            mcod[0] = BlankCode;
            msu = 1;
        }

        return new MakerCodeSelection(mcod, msu);
    }

    /// <summary>先頭 width 桁を取り出す(不足は空白詰め)。【C原典】固定長 char 配列の memcmp/memcpy 相当。</summary>
    private static string Take(string value, int width)
        => (value ?? string.Empty).PadRight(width)[..width];
}
