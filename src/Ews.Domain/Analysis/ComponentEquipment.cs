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

    // ---- 以下は構成機器生成(Fysk01_Make_Koukiki 系)が設定するフィールド。利用時に追加する方針 ----

    /// <summary>機器発生区分。【C原典】key.kkhkbn(FYRT804KEY)。'4'=追加機器。</summary>
    public char EquipmentOccurrenceKind { get; set; } = ' ';

    /// <summary>制御回路仕様名称追番。【C原典】key.cnameno[3]。</summary>
    public string ControlSpecNumber { get; set; } = "000";

    /// <summary>生成追番。【C原典】key.seino[3]。</summary>
    public string GenerationNumber { get; set; } = "000";

    /// <summary>行種。【C原典】dt.gyo[5]。</summary>
    public string LineType { get; set; } = string.Empty;

    /// <summary>電気パラメータ文字列。【C原典】dt.pstring[64]。</summary>
    public string ElectricalParameterString { get; set; } = string.Empty;

    /// <summary>品名。【C原典】dt.hinmei[25]。</summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>機器サーチ結果コード。【C原典】dt.ksrhkcd[4][2]。</summary>
    public string SearchResultCode { get; set; } = string.Empty;

    /// <summary>手配数量(QTY)。【C原典】dt.epaqty。</summary>
    public char OrderQuantity { get; set; } = ' ';

    /// <summary>生産管理データ転送対象有無。【C原典】dt.btnkubn。'Y'=転送。</summary>
    public char ProductionTransferKind { get; set; } = ' ';

    /// <summary>扉取付区分。【C原典】dt.tikbn。'T'=扉/'I'=中。</summary>
    public char DoorMountKind { get; set; } = ' ';

    /// <summary>定格容量(AC) VA。【C原典】hojg.teiva[0][7](機器マスタ補助情報)。</summary>
    public string RatedCapacityAcVa { get; set; } = string.Empty;

    /// <summary>定格容量(DC) W。【C原典】hojg.teiw[7]。</summary>
    public string RatedCapacityDcW { get; set; } = string.Empty;

    /// <summary>
    /// 構成機器キー(FYRT804KEY=機器発生区分+データ追番+制御回路仕様名称追番+生成追番=10 バイト)。
    /// 【C原典】memcmp(&amp;wk, *k_adr+i, sizeof(struct FYRT804KEY)) の比較対象。
    /// </summary>
    public string ComponentKey =>
        $"{EquipmentOccurrenceKind}{PadField(DataNumber, 3)}{PadField(ControlSpecNumber, 3)}{PadField(GenerationNumber, 3)}";

    private static string PadField(string value, int width) =>
        (value ?? string.Empty).PadRight(width)[..width];
}

