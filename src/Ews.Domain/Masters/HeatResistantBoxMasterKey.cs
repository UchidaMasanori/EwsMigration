namespace Ews.Domain.Masters;

/// <summary>
/// 耐熱盤BOX の機器マスタ(FYDM805)検索キー。
/// 【C原典】Fysk01_Kiki_Read_TainetuBOX(toku/sekkei/src/Fysk01.c:6793)のキー生成部。
///
/// 予約語 "PT"・メーカーコード "K" 固定、パラメータタイプは空白、定格キーに機器品名
/// (hinmei)をそのまま設定して機器マスタを引く。
/// </summary>
public static class HeatResistantBoxMasterKey
{
    /// <summary>予約語。【C原典】pkey.yoyaku = "PT      "。</summary>
    public const string ReservedWord = "PT";

    /// <summary>メーカーコード。【C原典】pkey.mkcd = "K  "。</summary>
    public const string MakerCode = "K";

    /// <summary>定格キーの最大長。【C原典】pkey.teikkey[80]。</summary>
    public const int RatingKeyLength = 80;

    /// <summary>
    /// 機器品名から定格キーを作る。
    /// 【C原典】memcpy(kdata-&gt;pkey.teikkey, hinmei, strlen(hinmei))。
    /// teikkey は 80 バイト固定のため 80 文字で切り詰める。
    /// </summary>
    public static string RatingKeyFor(string partName)
    {
        partName ??= string.Empty;
        return partName.Length <= RatingKeyLength ? partName : partName[..RatingKeyLength];
    }
}
