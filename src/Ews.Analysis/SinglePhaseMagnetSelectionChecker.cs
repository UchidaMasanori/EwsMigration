using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 1相3線のマグネット(MC/MG)選定可否を判定する。
///
/// 【C原典】PropSelChkMcMg(toku/sekkei/src/Fysk01.c:2042, static SHORT)。
///   定格入力なし・かつ負荷容量の入力ありの 1相3線 では "MSO-T10"(MG)/"S-T10"(MC)を選定しない
///   (根拠 ZS00-033-A-6)。C は 0:選定OK / -1:NG を返す。本移植は true:選定OK / false:NG。
///   グローバル sk_mc は呼び出し元が保持する MC 選定コンテキスト(主回路データ)を引数で受ける。
/// </summary>
public static class SinglePhaseMagnetSelectionChecker
{
    /// <summary>1相を表す回路相数。【C原典】sk_mc-&gt;dt.kpaph == '1'。</summary>
    private const char SinglePhase = '1';

    /// <summary>定格入力なしを表す定格電流２。【C原典】strncmp(epaa2, "00000.000", 9)==0。</summary>
    private const string RatingAbsent = "00000.000";

    /// <summary>負荷容量なしを表す値。【C原典】strncmp(fpalw2, "0000000", 7)==0。</summary>
    private const string LoadCapacityAbsent = "0000000";

    /// <summary>選定対象外の MG 品名。【C原典】strncmp(hinmei, "MSO-T10", 7)。</summary>
    private const string ExcludedMg = "MSO-T10";

    /// <summary>選定対象外の MC 品名。【C原典】strncmp(hinmei, "S-T10", 5)。</summary>
    private const string ExcludedMc = "S-T10";

    /// <summary>
    /// マグネット選定の可否を判定する。
    /// </summary>
    /// <param name="mcContext">MC 選定コンテキスト(sk_mc-&gt;dt)。null(=sk_mc が NULL)なら選定可。</param>
    /// <param name="productName">候補品名(f812-&gt;hinmei)。</param>
    /// <returns>選定可なら true、NG なら false。【C原典】0:OK / -1:NG。</returns>
    public static bool CanSelect(MainCircuitData? mcContext, string productName)
    {
        ArgumentNullException.ThrowIfNull(productName);

        if (mcContext is null || mcContext.CircuitPhaseCount != SinglePhase)
        {
            return true;
        }

        bool ratingAbsent = HasPrefix(mcContext.ElectricalParameterSlots[0].A2, RatingAbsent);
        bool loadPresent = !HasPrefix(mcContext.AttachedParameter.LoadCapacity, LoadCapacityAbsent);
        if (!(ratingAbsent && loadPresent))
        {
            return true;
        }

        if (HasPrefix(productName, ExcludedMg) || HasPrefix(productName, ExcludedMc))
        {
            return false;
        }

        return true;
    }

    // 【C原典】strncmp(value, prefix, strlen(prefix)) == 0。value が prefix で始まるか。
    private static bool HasPrefix(string value, string prefix)
        => value.AsSpan().StartsWith(prefix);
}
