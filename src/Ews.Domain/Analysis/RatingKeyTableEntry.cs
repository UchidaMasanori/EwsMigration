namespace Ews.Domain.Analysis;

/// <summary>
/// 定格値編集用情報テーブルの1行。【C原典】<c>TCHI_T</c>(toku/include/sekkei/fyrt814.h:28、テーブル実体は fyrt817.h)。
///
/// 予約語(機器種別)ごとに、定格値キー(kteichi 50バイト)の構成項目と、
/// マスタ照合時の比較方法を定義する静的データ。C 原典のフィールド並びは
/// <c>{len, d_len, kouno, check, fromto, kakunou, c_toku, s_toku}</c>。
///
/// 定格値キー生成(Fysk04_Make_Teikakuchi)が用いるのは <see cref="Width"/>・<see cref="DecimalScale"/>・
/// <see cref="ItemNo"/>・<see cref="SelectFlag"/> のみ。残りはマスタ範囲照合(Chokisearch)用に保持する。
/// </summary>
/// <param name="Width">項目の文字幅。【C原典】len。-1 はテーブル終端。</param>
/// <param name="DecimalScale">数値の10のべき乗スケール。【C原典】d_len。値に 10^DecimalScale を乗じてから整数化する。</param>
/// <param name="ItemNo">データ項番。【C原典】kouno。Fysk00_Get_Datachi の取得対象を選択する。</param>
/// <param name="Comparison">比較種別。【C原典】check。1=一致 2=以上 3=以下(fyrt808.h の E/GE/LE)。</param>
/// <param name="RangeSide">範囲区分(下限/上限)。【C原典】fromto。</param>
/// <param name="StorageKind">格納区分。【C原典】kakunou。</param>
/// <param name="ColumnFlag">列特殊区分。【C原典】c_toku。</param>
/// <param name="SelectFlag">選択特殊区分。【C原典】s_toku。-3=以降を打切り -2=当該行スキップ -1=区分読取り(AC/DC) 0=常時採用 正数=区分一致時採用。</param>
public readonly record struct RatingKeyTableEntry(
    short Width,
    short DecimalScale,
    short ItemNo,
    short Comparison,
    short RangeSide,
    short StorageKind,
    short ColumnFlag,
    short SelectFlag)
{
    /// <summary>テーブル終端(<see cref="Width"/> == -1)か。【C原典】len == -1。</summary>
    public bool IsEnd => Width == -1;
}
