using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// LGR(漏電継電器)と ZCT(零相変流器)の両方に指定されたメーカーコード(共通メーカー)を抽出する。
/// 【C原典】<c>Get_Kyotu_Maker</c>(toku/sekkei/src/Fysk00.c:3915)。
///
/// メーカー指定域(FYDF802)から LGR / ZCT のメーカー順位表をそれぞれ取り出し、
/// 両者に共通して現れるメーカーコードを収集する。
/// </summary>
public static class CommonMakerResolver
{
    /// <summary>予約語比較幅。【C原典】memcmp(yoyaku,"LGR ",4)/"ZCT "。</summary>
    private const int ReservedWordKeyWidth = 4;

    private const string LeakageGroundRelayKey = "LGR ";
    private const string ZeroCurrentTransformerKey = "ZCT ";

    /// <summary>
    /// LGR と ZCT の共通メーカーコードを抽出する。【C原典】<c>Get_Kyotu_Maker(mn, mk, tmn, tmak)</c>。
    /// </summary>
    /// <param name="makers">メーカー指定域。【C原典】mk (FYDF802 [])、mn = 要素数。</param>
    /// <returns>共通メーカーコード(3 桁)の一覧。【C原典】tmak[][3]、件数 *tmn。</returns>
    public static IReadOnlyList<string> ResolveCommonMakers(IReadOnlyList<MakerDesignation> makers)
    {
        ArgumentNullException.ThrowIfNull(makers);

        // 【C原典】tmpl/tmpz を空白(' ')で初期化。LGR/ZCT のメーカー表をそれぞれ取り込む(後勝ち)。
        string[] leakageRelayMakers = CreateEmptyTable();
        string[] zeroCurrentMakers = CreateEmptyTable();

        foreach (MakerDesignation maker in makers)
        {
            string key = (maker.ReservedWord ?? string.Empty).PadRight(ReservedWordKeyWidth)[..ReservedWordKeyWidth];
            if (string.CompareOrdinal(key, LeakageGroundRelayKey) == 0)
            {
                CopyMakerCodes(leakageRelayMakers, maker.MakerCodes);
            }
            else if (string.CompareOrdinal(key, ZeroCurrentTransformerKey) == 0)
            {
                CopyMakerCodes(zeroCurrentMakers, maker.MakerCodes);
            }
        }

        var common = new List<string>();
        for (int i = 0; i < MakerDesignation.MakerCodeCount; i++)
        {
            // 【C原典】tmpl[i][0] == ' ' で打切り(空スロット以降は無し)。
            if (leakageRelayMakers[i][0] == ' ')
            {
                break;
            }

            for (int j = 0; j < MakerDesignation.MakerCodeCount; j++)
            {
                // 【C原典】内側 break 条件は tmpz[i][0](外側 i)を使う(C 原典どおりの添字。tmpz[j] ではない)。
                if (zeroCurrentMakers[i][0] == ' ')
                {
                    break;
                }

                // 【C原典】memcmp(tmpl[i], tmpz[j], 3) == 0 なら共通メーカーとして採用。
                if (string.CompareOrdinal(leakageRelayMakers[i], zeroCurrentMakers[j]) == 0)
                {
                    common.Add(leakageRelayMakers[i]);
                }
            }
        }

        return common;
    }

    /// <summary>空白 3 桁のメーカー表(4 件)を生成する。【C原典】memset(tmpX[0], ' ', 12)。</summary>
    private static string[] CreateEmptyTable()
        => ["   ", "   ", "   ", "   "];

    /// <summary>メーカー表を上書きコピーする(4 件 × 3 桁)。【C原典】memcpy(tmpX[0], mk[i].mkcd[0], 12)。</summary>
    private static void CopyMakerCodes(string[] destination, string[] source)
    {
        for (int i = 0; i < MakerDesignation.MakerCodeCount; i++)
        {
            string code = i < source.Length ? source[i] ?? string.Empty : string.Empty;
            destination[i] = code.PadRight(MakerDesignation.MakerCodeWidth)[..MakerDesignation.MakerCodeWidth];
        }
    }
}
