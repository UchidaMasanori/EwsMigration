namespace Ews.Domain.Analysis;

/// <summary>
/// 形状タイプ変換の1シンボル分の設定。指定データタイプ(dtype)の先頭が
/// <see cref="Symbol"/> と一致した場合に <see cref="Types"/>(先頭 <see cref="Count"/> 個)へ展開する。
/// 【C原典】<c>Type_T2</c>(usr/include/toku/sekkei/fyrt819.h:48)。
/// </summary>
/// <param name="Symbol">照合シンボル。【C原典】sym[8](strlen 桁で memcmp)。</param>
/// <param name="Count">展開する形状タイプ数。【C原典】su。</param>
/// <param name="Types">設定形状タイプ(各要素は 7 文字へ空白詰め)。【C原典】typ[3][8]。</param>
public sealed record ShapeTypeVariant(string Symbol, int Count, IReadOnlyList<string> Types);

/// <summary>
/// 予約語1件分の形状タイプ変換テーブル。予約語が一致したら参照するデータタイプ位置
/// <see cref="Position"/> と、そこでのシンボル別展開 <see cref="Variants"/> を保持する。
/// 【C原典】<c>Type_Tbl2</c>(fyrt819.h:54) と <c>type_tbl2</c>(fyrt819.h の配列)。
/// </summary>
/// <param name="ReservedWord">予約語。【C原典】yoyaku[8](strlen 桁で memcmp)。</param>
/// <param name="Position">参照するデータタイプ位置。【C原典】ichi。</param>
/// <param name="Variants">シンボル別の形状タイプ設定。【C原典】type_t2。</param>
public sealed record ShapeTypeTableEntry(string ReservedWord, int Position, IReadOnlyList<ShapeTypeVariant> Variants);

/// <summary>
/// 形状タイプ変換一覧作成(<c>Fysk01_Type_Check2</c>)の結果。
/// <see cref="Types"/> は変換形状タイプ一覧(各 7 文字)、<see cref="Position"/> は
/// 採用したデータタイプ位置。C 原典の出力引数 wtype / tsu(=Types.Count) / ti を集約する。
/// </summary>
/// <param name="Types">変換形状タイプ一覧。【C原典】wtype(各 TSIZE=7 文字)。要素数が tsu。</param>
/// <param name="Position">タイプ位置。【C原典】ti。</param>
public sealed record ShapeTypeResult(IReadOnlyList<string> Types, int Position);
