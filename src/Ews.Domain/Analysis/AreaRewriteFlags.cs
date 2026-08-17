namespace Ews.Domain.Analysis;

/// <summary>
/// 上位／下位機器(sep[1]/sep[2])のどの電気パラメータ項目を回路側へ書き戻すかを示すフラグ。
/// 【C原典】<c>struct WK_STRUCT3</c>(toku/include/sekkei/fyrt814.h:58)。
///
/// C 原典は各フラグを <c>CHAR</c> の 0／非0 で保持し、
/// <c>Fysk01_Kikisearch_S1</c>(funcsk01.h:20)が機器選定結果に応じてセットする。
/// 本移植では真偽値として表現し、<c>Fysk00_Area_Rewrite</c> が参照する項目のみを保持する。
/// 添字 [0]=上位機器(sep[1])／[1]=下位機器(sep[2])。
/// </summary>
public sealed class AreaRewriteFlags
{
    /// <summary>トリップ電流(ＡＴ)を書き戻すか。【C原典】<c>at[2]</c>。</summary>
    public bool[] At { get; } = new bool[2];

    /// <summary>定格電流２(Ａ)を書き戻すか。【C原典】<c>a2[2]</c>。</summary>
    public bool[] A2 { get; } = new bool[2];

    /// <summary>フレーム電流(ＡＦ)を書き戻すか。【C原典】<c>af[2]</c>。</summary>
    public bool[] Af { get; } = new bool[2];

    /// <summary>
    /// 感度電流(ＭＡ0/1/2)を書き戻すか。【C原典】<c>ma[2][3]</c>。
    /// Area_Rewrite は <c>ma[i][0]</c> の 0／非0 のみを判定し、非0 のとき MA0/MA1/MA2 の3項目をまとめて書き戻す。
    /// </summary>
    public bool[] Ma { get; } = new bool[2];

    /// <summary>メーター定格(ＡＭ)を書き戻すか。【C原典】<c>am[2]</c>。</summary>
    public bool[] Am { get; } = new bool[2];

    /// <summary>全フラグを false に戻す。【C原典】memset(wk3, 0, sizeof(WK_STRUCT3))。</summary>
    public void Reset()
    {
        Array.Clear(At);
        Array.Clear(A2);
        Array.Clear(Af);
        Array.Clear(Ma);
        Array.Clear(Am);
    }
}
