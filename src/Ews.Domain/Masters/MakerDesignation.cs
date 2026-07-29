namespace Ews.Domain.Masters;

/// <summary>
/// 機器メーカー指定(1 予約語分のメーカー順位テーブル)。
/// 【C原典】<c>struct FYDF802</c>「機器メーカー指定ファイル」(toku/include/common/fydf802.h)。
///
/// 本移行では現時点で参照される項目(予約語 + メーカーコード順位表)のみを保持する。
/// キー明細(aim)・filler・datajg 等は利用箇所の移植時に追加する。
/// </summary>
public sealed class MakerDesignation
{
    /// <summary>メーカーコードの順位数(mkcd の要素数)。【C原典】mkcd[4][3]。</summary>
    public const int MakerCodeCount = 4;

    /// <summary>メーカーコード 1 件の桁数。【C原典】mkcd[4][3]。</summary>
    public const int MakerCodeWidth = 3;

    /// <summary>予約語。【C原典】key.yoyaku[8]。</summary>
    public string ReservedWord { get; set; } = string.Empty;

    /// <summary>
    /// メーカーコード順位テーブル(4 件 × 3 桁)。【C原典】mkcd[4][3]。
    /// 11:特注盤の河村標準 / 12:特注盤の建設省(準拠)。既定は空(空白 3 桁)。
    /// </summary>
    public string[] MakerCodes { get; } = ["   ", "   ", "   ", "   "];
}
