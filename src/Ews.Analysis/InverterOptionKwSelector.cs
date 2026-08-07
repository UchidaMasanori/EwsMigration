using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// INV オプション機器の直近上位 kw に対応する品名(定格値)を選定する。
/// 【C原典】Fysk01_ChkInvKw_OP(toku/sekkei/src/Fysk01.c:5967)。
///   コンスタント(invop_prm)を上から走査し、入力 kw 以上となる最初の行の品名を返す。
///   タイプは照合しない。該当なしは null(C原典では出力 teikaku を変更しない)。
/// </summary>
public static class InverterOptionKwSelector
{
    /// <summary>
    /// 入力 kw 以上となる最初の行の品名を返す。【C原典】Fysk01_ChkInvKw_OP(prm, pnum, inputKw, teikaku)。
    /// </summary>
    /// <param name="constants">INV オプションコンスタント(invop_prm [])。【C原典】prm・pnum=要素数。</param>
    /// <param name="inputKw">入力 kw 値。【C原典】inputKw。</param>
    /// <returns>該当行の品名。該当なしは null。【C原典】teikaku(未該当時は不変)。</returns>
    public static string? SelectProductName(IReadOnlyList<InverterOptionConstant> constants, double inputKw)
    {
        ArgumentNullException.ThrowIfNull(constants);

        foreach (InverterOptionConstant prm in constants)
        {
            if (inputKw <= prm.RatedKw)
            {
                return prm.ProductName;
            }
        }

        return null;
    }
}
