namespace Ews.Domain.Analysis;

/// <summary>
/// 機器マスターキー(ＰＲＩＭＡＲＹキー)。【C原典】struct p805_key(toku/include/common/fydm805.h:46)。
/// 構成機器・機器マスタが共通で持つ機器同定キー。分岐配列並べ替え
/// (<c>Fyss3C_Bunki_Sort</c>)の <c>SortIndex</c>/<c>KouseiGetElement</c> は
/// 予約語・パラメータタイプ・定格キー(電気定格の生データ)を参照する。
/// </summary>
public sealed class MachineMasterKey
{
    /// <summary>予約語。【C原典】yoyaku[8]。</summary>
    public string ReservedWord { get; set; } = string.Empty;

    /// <summary>メーカーコード。【C原典】mkcd[3]。</summary>
    public string MakerCode { get; set; } = string.Empty;

    /// <summary>パラメータタイプ(7 種、各 7 桁)。【C原典】ptype[7][7]。未設定は空。</summary>
    public string[] ParameterTypes { get; set; } = ["", "", "", "", "", "", ""];

    /// <summary>定格キー(電気定格の生データ 80 桁、予約語別に <c>union fyrt701</c> として解釈)。【C原典】teikkey[80]。</summary>
    public string RatingKey { get; set; } = string.Empty;
}

/// <summary>
/// 構成機器データの最小サブセット。【C原典】struct FYDF811(toku/include/common/fydf811.h:22)。
/// 主回路の各機器がどの構成機器レコードに対応するかをデータ追番(<see cref="DataNumber"/>)で
/// 突き合わせ、並べ替えのソートキー生成(<c>SortIndex</c>/<c>KouseiGetElement</c>)で
/// 機器マスターキー(<see cref="MachineKey"/>)を参照する。他フィールドは利用時に追加する
/// (<see cref="MainCircuitData"/> と同方針)。
/// </summary>
public sealed class ComponentEquipment
{
    /// <summary>データ追番。【C原典】key.datano[3]。主回路(FYRT800)の datano と突き合わせる。</summary>
    public string DataNumber { get; set; } = "000";

    /// <summary>機器マスターキー。【C原典】dt.km_key(struct p805_key)。</summary>
    public MachineMasterKey MachineKey { get; set; } = new();
}
