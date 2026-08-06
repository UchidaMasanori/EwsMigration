namespace Ews.Analysis;

using Ews.Domain.Analysis;

/// <summary>
/// 論理記述マスタ Key データ作成(FySgCheckSgkkSet / SgCheckRonriKM)における
/// 制御機器テーブル(SGKK)の重複可能判定リーフ。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>PropIsGNdiff</c>。
///
/// SGKK を予約語でソートしながら同一予約語のグループを畳み込む際、直前グループ
/// (<c>wk</c>)と現在エントリ(<c>other</c>)を「重複データ(同一グループに畳み込む)」か
/// 「別データ(グループを区切る)」かを判定する。
/// </summary>
public static class ControlEquipmentDuplicateChecker
{
    /// <summary>
    /// SGKK の 2 エントリが別データか(=グループを区切るべきか)を判定する。
    /// 【C原典】PropIsGNdiff(Fyss1k.c:1138)。
    /// </summary>
    /// <param name="wk">畳み込み中の代表エントリ。【C原典】SGKK wk。</param>
    /// <param name="other">比較対象エントリ。【C原典】Sgkk[idx]。</param>
    /// <param name="reservedWordCompare">
    /// 予約語比較結果。【C原典】ret = strcmp(wk.yoyaku, Sgkk[idx].yoyaku)。0 で予約語一致。
    /// </param>
    /// <returns>0:重複データ、1:別データ。【C原典】return 0/1。</returns>
    public static int IsDifferentData(ControlEquipmentEntry wk, ControlEquipmentEntry other, int reservedWordCompare)
    {
        ArgumentNullException.ThrowIfNull(wk);
        ArgumentNullException.ThrowIfNull(other);

        // 【C原典】予約語が違う → 別データ。
        if (reservedWordCompare != 0)
        {
            return 1;
        }

        // 【C原典】予約語が同じで、両方とも内部機器 → 別データ。
        if (wk.InternalCount > 0 && other.InternalCount > 0)
        {
            return 1;
        }

        // 【C原典】予約語が同じで、両方とも外部機器 → 別データ。
        if (wk.ExternalCount > 0 && other.ExternalCount > 0)
        {
            return 1;
        }

        return 0;
    }
}
