namespace Ews.Domain.Analysis;

/// <summary>
/// 定格値チェック中に設定される比較用グローバル値。
///
/// 【C原典】<c>CMP_1[2]</c> / <c>CMP_2</c> / <c>CMP_3</c>(Fysk01.c:119-121 のグローバル変数)。
///
/// <c>Fysk02_Check_Teichi_Part</c>(=<c>RatingValueChecker.CheckPart</c>)が定格値展開情報の
/// 格納区分(kakunou)に応じて直近上下位データ値を書き込み、機器選定の候補比較
/// (<c>Fysk01_Data_Cmp</c>)が読み出す。原典ではグローバル変数だが、副作用を明示するため
/// 呼出し側が生成して受け渡す可変状態としてモデル化する。
/// </summary>
public sealed class RatingComparisonState
{
    /// <summary>
    /// AT(定格電流)系の比較値 2 枠。【C原典】<c>CMP_1[2]</c>。
    /// 格納区分(kakunou)が 1 または 2 のとき <c>CMP_1[kakunou-1]</c> に格納される。
    /// </summary>
    public double[] AmpereTripPair { get; } = new double[2];

    /// <summary>AT 系の比較値。【C原典】<c>CMP_2</c>。格納区分(kakunou)が 3 のとき格納される。</summary>
    public double AmpereTripSecond { get; set; }

    /// <summary>V(電圧)系の比較値。【C原典】<c>CMP_3</c>。格納区分(kakunou)が 4 のとき格納される。</summary>
    public double Voltage { get; set; }

    /// <summary>全比較値を 0 に初期化する。【C原典】<c>CMP_1[0]=CMP_1[1]=CMP_2=CMP_3=0.0;</c>(Fysk01.c:1291)。</summary>
    public void Reset()
    {
        AmpereTripPair[0] = 0.0;
        AmpereTripPair[1] = 0.0;
        AmpereTripSecond = 0.0;
        Voltage = 0.0;
    }
}
