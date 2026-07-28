namespace Ews.Domain.Analysis;

/// <summary>
/// 選択番号付き設定タイプ(1 変換候補)。
/// 【C原典】Type_T (fyrt819.h:172): { SHORT seleno; SHORT su; CHAR typ[4][8]; }。
///
/// <see cref="Types"/> は先頭 su 個の設定タイプ("KY"/"ET"/"NOTHING" 等)。
/// C は各要素を strlen 分だけ 7 文字枠へ左詰めコピーする(空白で右詰め)。
/// </summary>
public sealed record SelectionShapeVariant(int SelectionNumber, IReadOnlyList<string> Types);

/// <summary>
/// 予約語ごとの変換形状タイプ表エントリ。
/// 【C原典】Type_TBL (fyrt819.h:178):
///   { CHAR yoyaku[8]; SHORT ichi; SHORT hdlchk; Type_T *type_t; }。
/// </summary>
/// <param name="ReservedWord">予約語(末尾空白を含む。前方一致長は strlen 相当)。【C原典】yoyaku[8]。</param>
/// <param name="Position">設定位置(データタイプ配列の参照位置)。【C原典】ichi。</param>
/// <param name="HandleLockPosition">ハンドルロック位置フラグ(-1=なし)。【C原典】hdlchk。</param>
/// <param name="Variants">設定タイプ情報。【C原典】type_t(Type_T 配列)。</param>
public sealed record SelectionShapeTableEntry(
    string ReservedWord,
    int Position,
    int HandleLockPosition,
    IReadOnlyList<SelectionShapeVariant> Variants);

/// <summary>
/// タイプチェック(<c>Fysk01_Type_Check</c>)の結果。
/// </summary>
/// <param name="DataTypes">
/// 使用有無チェック(<c>Fysk08_Usetype_Check</c>)適用後のデータタイプ(7 文字×7 枠)。
/// 【C原典】in/out 引数 dtype[][7]。
/// </param>
/// <param name="ConvertedTypes">変換形状タイプ一覧(7 文字×7 枠の生バッファ)。【C原典】wtype[][7]。</param>
/// <param name="TypeCount">変換形状タイプ数。【C原典】*tsu。</param>
/// <param name="TypePosition">タイプ位置。【C原典】*ti。</param>
/// <param name="HandleLockPosition">ハンドルロック位置(-1=なし)。【C原典】*fg。</param>
/// <param name="Found">予約語が予約語マスタに存在したか(false=NOGOOD)。</param>
public sealed record ShapeTypeCheckResult(
    IReadOnlyList<string> DataTypes,
    IReadOnlyList<string> ConvertedTypes,
    int TypeCount,
    int TypePosition,
    int HandleLockPosition,
    bool Found);
