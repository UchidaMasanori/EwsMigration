namespace Ews.Domain.Analysis;

/// <summary>
/// INV オプション機器コンスタントファイルの 1 行。
/// 【C原典】struct invop_prm(toku/include/sekkei/struct.h:245, 改訂&lt;11&gt;)。
///   定格 kw の昇順に並び、入力 kw 以上となる最初の行の品名(定格値)を採用する。
/// </summary>
/// <param name="Type">タイプパラメータ。【C原典】type[7]。</param>
/// <param name="RatedKw">INV 機器 kw。【C原典】kw。</param>
/// <param name="ProductName">品名(定格値)。【C原典】hinmei[25]。</param>
public sealed record InverterOptionConstant(string Type, double RatedKw, string ProductName);
