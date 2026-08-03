namespace Ews.Domain.Analysis;

/// <summary>
/// 電源・分岐固まりのプラグインブレーカグループ。
/// 【C原典】struct grp_plug(toku/sekkei/src/Fyss3R.c:29)。
///
/// プラグインブレーカ結線処理(<c>Fyss3R_TokuPlugIn_Kes_Set</c>)で、主回路エリアを
/// 電源(予約語 "P ")区切り＋プラグインタイプ(ハーフサイズ 'C'/アダプタ 'K')で
/// まとめた 1 グループを表す。<see cref="StartIndex"/>～<see cref="EndIndex"/> は
/// 主回路エリア(maina)上の連続範囲を指す。
/// </summary>
public sealed class PlugInGroup
{
    /// <summary>電源の相線。【C原典】sousen。13:単相3線 / 33:三相3線。(kpaph×10＋kpawr)。</summary>
    public int SourcePhaseWire { get; set; }

    /// <summary>プラグインタイプ。【C原典】type。'C':ハーフサイズ / 'K':アダプタ。</summary>
    public char Type { get; set; }

    /// <summary>グループ開始の主回路 index。【C原典】st_idx。</summary>
    public int StartIndex { get; set; }

    /// <summary>グループ終了の主回路 index。【C原典】ed_idx。</summary>
    public int EndIndex { get; set; }
}
