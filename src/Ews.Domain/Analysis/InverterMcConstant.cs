namespace Ews.Domain.Analysis;

/// <summary>
/// INV 対応 MC 機器選定コンスタントファイル(inv003a.cns / inv003b.cns)の 1 行。
/// 【C原典】struct invmc_prm(toku/include/sekkei/struct.h:254, 改訂&lt;12&gt;)。
///   同一 INV タイプの行が定格 kw の昇順に並び、入力 kw 以上となる最初の行の MC 品名を採用する。
/// </summary>
/// <param name="Type">INV タイプパラメータ。【C原典】type[7]。</param>
/// <param name="RatedKw">INV 機器 kw。【C原典】kw。</param>
/// <param name="ProductName">MC 品名。【C原典】hinmei[25]。</param>
public sealed record InverterMcConstant(string Type, double RatedKw, string ProductName);
