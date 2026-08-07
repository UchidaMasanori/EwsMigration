namespace Ews.Domain.Analysis;

/// <summary>
/// INV 直近上位検索用コンスタントファイル(inv001.cns 等)の 1 行。
/// 【C原典】struct inv_prm(toku/include/sekkei/struct.h:237, 改訂&lt;10&gt;)。
///   同一 INV タイプの行が定格 kw の昇順に並び、入力 kw 以上となる最初の行の kw を採用する。
/// </summary>
/// <param name="Types">タイプパラメータ(7 スロット × 7 桁)。【C原典】type[7][7]。</param>
/// <param name="RatedKw">INV 機器 kw。【C原典】kw。</param>
public sealed record InverterConstant(IReadOnlyList<string> Types, double RatedKw);
