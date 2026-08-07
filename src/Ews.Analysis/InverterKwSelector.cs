using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// INV タイプに対応する直近上位の kw 値を選定する。
/// 【C原典】Fysk01_ChkInvKw(toku/sekkei/src/Fysk01.c:5567, 改訂&lt;28&gt;)。
///   コンスタント(inv_prm)を上から走査し、タイプ 2(dtype[1])が一致する行のうち
///   入力 kw 以上となる最初の kw を返す。一致タイプ帯を通り過ぎたら打切り。該当なしは 0.0。
/// </summary>
public static class InverterKwSelector
{
    private const int TypeWidth = 7;

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

    private static string Take(string value, int width)
        => (value ?? string.Empty).PadRight(width)[..width];
}
