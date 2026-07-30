namespace Ews.Domain.Analysis;

/// <summary>
/// 機器情報(制御仕様情報)の最小サブセット。
/// 【C原典】struct kikijg(toku/include/common/fydf808.h:88)。
///   制御回路の機器選定(Fysk05_Kikisearch_SE 系)で扱う 1 機器分の情報。
///   本移植では制御ランプの既定機器タイプ設定(PropChgLampType 系)が参照する
///   予約語/記述行桁/タイプ/電気パラメータ(eg)のみを保持し、他フィールドは
///   利用時に追加する(<see cref="MainCircuitData"/> と同方針)。
/// </summary>
public sealed class ControlEquipmentInfo
{
    /// <summary>予約語。【C原典】yoyaku[8]。</summary>
    public string ReservedWord { get; set; } = string.Empty;

    /// <summary>記述行。【C原典】gyo[3]。</summary>
    public string DescriptionRow { get; set; } = "000";

    /// <summary>記述桁。【C原典】keta[3]。</summary>
    public string DescriptionColumn { get; set; } = "000";

    /// <summary>タイプ(7 種)。【C原典】datatype[7][7]。未設定は空。</summary>
    public string[] DataType { get; set; } = ["", "", "", "", "", "", ""];

    /// <summary>付属パラメータのメーカーコード。【C原典】fp.fpamk[3]。既定 ' '。</summary>
    public string MakerCode { get; set; } = string.Empty;

    /// <summary>電気パラメータ(3 スロット)。【C原典】struct eparmg eg[3]。</summary>
    public ElectricalParameters[] ElectricalParameterSlots { get; set; } = [new(), new(), new()];
}
