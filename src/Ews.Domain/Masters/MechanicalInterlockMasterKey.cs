namespace Ews.Domain.Masters;

/// <summary>
/// 機械連動子(MI)の機器マスタ(FYDM805)検索キー。
/// 【C原典】Fysk01_Kiki_Read_MI(toku/sekkei/src/Fysk01.c:6444, 改訂&lt;35&gt;)のキー生成部。
///
/// 予約語 "PT"・メーカーコード "M" 固定で、容量(AF)が 250 以下なら "MI-05SV3"、
/// それを超えれば "MI-4SW3" を定格キーとして機器マスタを引く。
/// </summary>
public static class MechanicalInterlockMasterKey
{
    /// <summary>予約語。【C原典】pkey.yoyaku = "PT      "。</summary>
    public const string ReservedWord = "PT";

    /// <summary>メーカーコード。【C原典】pkey.mkcd = "M  "。</summary>
    public const string MakerCode = "M";

    /// <summary>容量(AF)しきい値。【C原典】epaaf &lt;= 250.0。</summary>
    public const double CapacityThresholdAf = 250.0;

    /// <summary>容量 250AF 以下の定格キー。【C原典】"MI-05SV3"。</summary>
    public const string RatingKeyUpTo250 = "MI-05SV3";

    /// <summary>容量 250AF 超の定格キー。【C原典】"MI-4SW3"。</summary>
    public const string RatingKeyOver250 = "MI-4SW3";

    /// <summary>
    /// 容量(AF)に応じた定格キーを返す。
    /// 【C原典】epaaf &lt;= 250.0 ? "MI-05SV3" : "MI-4SW3"。
    /// </summary>
    public static string RatingKeyFor(double capacityAf) =>
        capacityAf <= CapacityThresholdAf ? RatingKeyUpTo250 : RatingKeyOver250;
}
