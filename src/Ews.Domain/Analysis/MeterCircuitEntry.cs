namespace Ews.Domain.Analysis;

/// <summary>
/// 計器回路機器の該当レコード情報。
/// 【C原典】<c>WK_Keiki</c>(toku/include/sekkei/fyrt814.h:67)。
/// </summary>
public sealed class MeterCircuitEntry
{
    /// <summary>該当機器の主回路レコード添字(0 始まり)。【C原典】rec (SHORT)。</summary>
    public int Rec { get; set; }

    /// <summary>
    /// 処理状態。0:未処理 / 1:VA・W 値設定済(機器サーチ待ち) / 2:機器サーチ済。
    /// 【C原典】katei (CHAR、数値 0/1/2 を格納)。
    /// </summary>
    public int Katei { get; set; }
}
