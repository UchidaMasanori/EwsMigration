using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// INV 対応 MC 機器を、入力 kw に応じて選定コンスタント(inv003a/b.cns)から選ぶ。
/// 【C原典】Fysk01_ChkInv_MC(toku/sekkei/src/Fysk01.c:6401, 改訂&lt;12&gt;)。
///
/// コンスタントは上から同一 INV タイプが定格 kw 昇順で並び、機器タイプ(dtype)が一致する帯の中で
/// 入力 kw 以上となる最初の行の MC 品名を返す。該当タイプの帯を通り過ぎた時点で探索を打ち切る。
/// 一致行が無ければ何も返さない(C原典は出力 hinmei を書き換えず呼出側の初期値を残す)。
/// </summary>
public static class InverterMcSelector
{
    /// <summary>
    /// 入力 kw 以上となる最初の同タイプ行の MC 品名を返す。無ければ <c>null</c>。
    /// 【C原典】Fysk01_ChkInv_MC(prm, pnum, inputKw, dtype, hinmei)。
    /// </summary>
    /// <param name="constants">INV 対応 MC 選定コンスタント(タイプ・kw 昇順)。【C原典】prm[pnum]。</param>
    /// <param name="inputKw">入力 kw 値。【C原典】inputKw。</param>
    /// <param name="deviceType">INV 機器タイプ。【C原典】dtype。</param>
    public static string? SelectProductName(
        IReadOnlyList<InverterMcConstant> constants, double inputKw, string deviceType)
    {
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(deviceType);

        bool typeMatched = false;
        foreach (InverterMcConstant constant in constants)
        {
            // 【C原典】memcmp(type, dtype, strlen(dtype)): type の先頭 dtype 桁が一致。
            if (constant.Type.StartsWith(deviceType, StringComparison.Ordinal))
            {
                typeMatched = true;
                if (inputKw <= constant.RatedKw)
                {
                    return constant.ProductName;
                }
            }
            else if (typeMatched)
            {
                // 該当タイプの帯を通り過ぎたので終了。
                break;
            }
        }

        return null;
    }
}
