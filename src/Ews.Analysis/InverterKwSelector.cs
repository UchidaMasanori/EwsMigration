using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// <see cref="InverterKwSelector.SelectKwByParameter"/> の結果(直近上位 kw と選択タイプ)。
/// 【C原典】ChkInvKwPara の outputKw と outputtype[7][7]。
/// </summary>
/// <param name="Kw">選定 kw。該当なしは 0.0。【C原典】outputKw。</param>
/// <param name="SelectedType">選択タイプ(7 スロット)。該当なしは null。【C原典】outputtype[7][7]。</param>
public sealed record InverterKwSelection(double Kw, IReadOnlyList<string>? SelectedType);

/// <summary>
/// INV タイプに対応する直近上位の kw 値を選定する。
/// 【C原典】Fysk01_ChkInvKw(toku/sekkei/src/Fysk01.c:5567, 改訂&lt;28&gt;)。
///   コンスタント(inv_prm)を上から走査し、タイプ 2(dtype[1])が一致する行のうち
///   入力 kw 以上となる最初の kw を返す。一致タイプ帯を通り過ぎたら打切り。該当なしは 0.0。
/// </summary>
public static class InverterKwSelector
{
    private const int TypeWidth = 7;
    private const int TypeSlotCount = 7;

    /// <summary>
    /// 直近上位の kw 値を選定する。【C原典】Fysk01_ChkInvKw(prm, pnum, inputKw, dtype)。
    /// </summary>
    /// <param name="constants">INV コンスタント(inv_prm [])。【C原典】prm・pnum=要素数。</param>
    /// <param name="inputKw">入力 kw 値。【C原典】inputKw。</param>
    /// <param name="dataType">データタイプ(7 スロット × 7 桁)。【C原典】dtype[][7]。タイプ 2=dtype[1] で照合。</param>
    public static double SelectKw(
        IReadOnlyList<InverterConstant> constants,
        double inputKw,
        IReadOnlyList<string> dataType)
    {
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(dataType);

        // 【C原典】memcmp((prm+i)->type[0], dtype[1], 7): タイプ 2 だけで照合。
        string key = Take(dataType[1], TypeWidth);
        bool typeMatched = false;
        double outputKw = 0.0;

        foreach (InverterConstant prm in constants)
        {
            if (Take(prm.Types[0], TypeWidth) == key)
            {
                typeMatched = true;
                if (inputKw <= prm.RatedKw)
                {
                    outputKw = prm.RatedKw;
                    break;
                }
            }
            else if (typeMatched)
            {
                // 【C原典】該当タイプ帯を通り過ぎたら終了。
                break;
            }
        }

        return outputKw;
    }

    /// <summary>
    /// 別パラメータでの直近上位 kw を選定し、選択タイプも返す。
    /// 【C原典】Fysk01_ChkInvKwPara(toku/sekkei/src/Fysk01.c:5618, 改訂&lt;27&gt;)。
    ///   タイプ 1 番目からの一致桁数(i*7)を i=7..1 と緩めながら走査し、各段で入力 kw 以上となる
    ///   最初の行の kw/タイプで上書きする。外側ループは break しないため、最終段(最も緩い一致)が優先される。
    /// </summary>
    /// <param name="constants">INV コンスタント(inv_prm [])。【C原典】prm・pnum=要素数。</param>
    /// <param name="inputKw">入力 kw 値。【C原典】inputKw。</param>
    /// <param name="dataType">データタイプ(7 スロット × 7 桁)。【C原典】dtype[][7]。タイプ 1(dtype[0]) から前方一致で照合。</param>
    public static InverterKwSelection SelectKwByParameter(
        IReadOnlyList<InverterConstant> constants,
        double inputKw,
        IReadOnlyList<string> dataType)
    {
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(dataType);

        double outputKw = 0.0;
        IReadOnlyList<string>? outputType = null;

        // 【C原典】for(i=7;i>0;i--): 一致桁数(i*7)を緩めながら走査。外側は break せず上書き。
        for (int i = TypeSlotCount; i > 0; i--)
        {
            foreach (InverterConstant prm in constants)
            {
                if (!MatchesPrefix(prm.Types, dataType, i))
                {
                    continue;
                }
                if (inputKw <= prm.RatedKw)
                {
                    outputKw = prm.RatedKw;
                    outputType = CopyTypes(prm.Types);
                    break;
                }
            }
        }

        return new InverterKwSelection(outputKw, outputType);
    }

    /// <summary>type[0..slots-1] と dtype[0..slots-1] をスロット単位で照合。【C原典】memcmp(type[0], dtype[0], slots*7)。</summary>
    private static bool MatchesPrefix(IReadOnlyList<string> types, IReadOnlyList<string> dataType, int slots)
    {
        for (int k = 0; k < slots; k++)
        {
            if (Take(types[k], TypeWidth) != Take(dataType[k], TypeWidth))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>タイプ 7 スロットを複写する。【C原典】memcpy(outputtype[0], type[0], 49)。</summary>
    private static string[] CopyTypes(IReadOnlyList<string> types)
    {
        string[] copy = new string[TypeSlotCount];
        for (int k = 0; k < TypeSlotCount; k++)
        {
            copy[k] = Take(types[k], TypeWidth);
        }
        return copy;
    }

    private static string Take(string value, int width)
        => (value ?? string.Empty).PadRight(width)[..width];
}
