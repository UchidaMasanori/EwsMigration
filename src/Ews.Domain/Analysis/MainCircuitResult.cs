namespace Ews.Domain.Analysis;

/// <summary>
/// 主回路エリア(回路解析の出力 = 主回路1データ分の解析結果)。
///
/// 【C原典】
///   - 構造体: struct FYRT800            (toku/include/common/fyrt800.h)
///   - ファイルID: FYRT800「主回路エリア」/ メイン定義は struct fydf806 を内包。
///
/// FYRT 系はファイル固定長レコードというより、回路解析中のメモリ上の作業/結果領域
/// (DOUBLE を含む積算エリア sk_work)である。本クラスは解析結果を保持する POCO とし、
/// 永続化が必要な項目のみ後段で複合回路ファイル(FYDF807)等へ書き出す。
/// </summary>
public sealed class MainCircuitResult
{
    /// <summary>データ追番 001～(予約語毎の追番)。【C原典】FYRT800.datano[3]。</summary>
    public string SequenceNumber { get; set; } = string.Empty;

    /// <summary>主回路データ定義部。【C原典】struct syukairo dt。</summary>
    public MainCircuitData Data { get; set; } = new();

    /// <summary>積算用ワークエリア。【C原典】struct sk_work wk。</summary>
    public CircuitWork Work { get; set; } = new();
}

/// <summary>
/// 主回路データ定義部(FYRT800 のレコード定義部)。
/// 【C原典】struct syukairo(toku/include/common/fydf806.h)。
///
/// C原典は固定長 CHAR フィールドの集合で、<c>Main_Area_Clear</c>(Fyss1f.c:3293)が
/// 数値("9")フィールドを '0'、文字("C")フィールドを ' ' 等で初期化してから
/// <c>mainfile_set</c>(Fyss1f.c:1464)が各フィールドを設定する。
/// 本移行では各フィールドを論理値(数値文字列 or 文字)として保持し、固定長への整形は
/// 永続化(FYDF806/複合回路 FYDF807)時に行う。<see cref="Create"/> が Main_Area_Clear 相当の
/// 初期値を与える。フィールドは mainfile_set の各ブロックを移植するに従い順次追加する
/// (電気パラメータ ep[3] / 付属パラメータ fp は eparm_set 移植時に追加)。
/// </summary>
public sealed class MainCircuitData
{
    /// <summary>記述行。【C原典】gyo[3]("9")。</summary>
    public string DescriptionRow { get; set; } = "000";

    /// <summary>記述桁。【C原典】keta[3]("9",改訂&lt;5&gt;で 2→3 桁)。</summary>
    public string DescriptionColumn { get; set; } = "000";

    /// <summary>系統番号(系統毎の追番)。【C原典】kno[3]("9")。</summary>
    public string SystemNumber { get; set; } = "000";

    /// <summary>系統種別 '1':P系統 '2':SP系統 '3':MP系統。【C原典】ksyubetu("9")。</summary>
    public char SystemKind { get; set; } = ' ';

    /// <summary>入線番号(P系統の連番)。【C原典】nyuseno[3]("9")。</summary>
    public string IncomingNumber { get; set; } = "000";

    /// <summary>上流並列追番。【C原典】joheino[3]("9")。</summary>
    public string UpperParallelNumber { get; set; } = "000";

    /// <summary>階層番号(ランク)。【C原典】kaisono[3]("9")。</summary>
    public string HierarchyNumber { get; set; } = "000";

    /// <summary>並列追番。【C原典】heino[3]("9")。</summary>
    public string ParallelNumber { get; set; } = "000";

    /// <summary>直列追番。【C原典】chokuno[3]("9")。</summary>
    public string SeriesNumber { get; set; } = "000";

    /// <summary>予約語自動生成区分 ' ':入力 '1':自動発生。【C原典】yoyakkbn("C")。</summary>
    public char AutoGenerationKind { get; set; } = ' ';

    /// <summary>予約語。【C原典】yoyaku[8]("C")。</summary>
    public string ReservedWord { get; set; } = string.Empty;

    /// <summary>行種コード。【C原典】gyocd[3]("C")。</summary>
    public string LineTypeCode { get; set; } = string.Empty;

    /// <summary>行種番号。【C原典】gyono[2]("C")。</summary>
    public string LineTypeNumber { get; set; } = string.Empty;

    /// <summary>行種グループ番号。【C原典】gyoglno[3]("9")。</summary>
    public string LineTypeGroupNumber { get; set; } = "000";

    /// <summary>予約語指定番号。【C原典】ysno[2]("9")。</summary>
    public string DesignationNumber { get; set; } = "00";

    /// <summary>予約語生成サフィックス。【C原典】yssfx("C")。</summary>
    public char DesignationSuffix { get; set; } = ' ';

    /// <summary>指定回路番号。【C原典】skno[5]("C")。</summary>
    public string CircuitDesignationNumber { get; set; } = string.Empty;

    /// <summary>指定回路生成サフィックス A,B,C,…。【C原典】sksfx("C")。</summary>
    public char CircuitDesignationSuffix { get; set; } = ' ';

    /// <summary>同一機器認識番号。【C原典】doukkno[2]("9")。</summary>
    public string IdentityNumber { get; set; } = "  ";

    /// <summary>タイプ(7 種)。【C原典】datatype[7][7]("C")。未移植は空(空白)。</summary>
    public string[] DataType { get; set; } = ["", "", "", "", "", "", ""];

    /// <summary>末端区分。【C原典】mattan("C")。</summary>
    public char TerminalKind { get; set; } = ' ';

    /// <summary>回路要素。【C原典】kiryoso("C")。</summary>
    public char CircuitElement { get; set; } = ' ';

    /// <summary>負荷発生元区分。【C原典】ahassei("C")。</summary>
    public char LoadSourceKind { get; set; } = ' ';

    /// <summary>上流積み上げ区分('K':交互運転 ' ':通常運転)。【C原典】jagekbn("C")。</summary>
    public char StackKind { get; set; } = ' ';

    /// <summary>並び替え機器区分。【C原典】narakbn("C")。Main_Area_Clear 既定 '1'。</summary>
    public char SortKind { get; set; } = '1';

    /// <summary>切り換えタイプ。【C原典】kaetyp("C")。</summary>
    public char SwitchType { get; set; } = ' ';

    /// <summary>生成回路分類 'M':TM,M,SM ' ':B 'O':O 'B':BO 'P':PM。【C原典】kairobun("C")。</summary>
    public char CircuitClass { get; set; } = ' ';

    /// <summary>生成回路番号。【C原典】kairono[3]("9")。</summary>
    public string CircuitNumber { get; set; } = "000";

    /// <summary>生成回路番号サフィックス。【C原典】kairsfx[5]("C")。</summary>
    public string CircuitNumberSuffix { get; set; } = string.Empty;

    /// <summary>
    /// 電気パラメータ(3 スロット)。【C原典】struct eparmg ep[3](fydf806.h)。
    /// ep[0]=入力値(mainfile_set が eparm_set で設定)、ep[1]=入力値からの生成値、
    /// ep[2]=システム側の生成値。本移行では ep[0] を <c>eparm_set</c> 相当で設定する。
    /// </summary>
    public ElectricalParameters[] ElectricalParameterSlots { get; set; } = [new(), new(), new()];

    /// <summary>
    /// 付属パラメータ。【C原典】struct fparmg fp(fydf806.h:64)。
    /// mainfile_set の付属パラメータ設定ブロック(Fyss1f.c:1957-2205)が機器テーブルの
    /// 代入文タグ値(DLW/DLV/DLN/DCM/DIT/DMK/DSP/括弧区分/制御電源番号 等)を整形して格納する。
    /// </summary>
    public AttachedParameters AttachedParameter { get; set; } = new();

    // ---------------------------------------------------------------------
    // 以降のフィールド(使用相 siyouso / 線番 senban1,2 / 回路電気値 kpa* /
    // 通電電流 denryu 等)は、それぞれ対応する mainfile_set の
    // ブロック(負荷容量/電圧セット等)を移植する段階で追加する。これらは機器選定
    // (Fyss13-15)の未移植データ(DLW/DLV/DLN/DTYPE 等)に依存する。
    // ---------------------------------------------------------------------

    /// <summary>
    /// Main_Area_Clear 相当の初期値を持つ主回路データを生成する。
    /// 【C原典】Main_Area_Clear(Fyss1f.c:3293)。
    /// </summary>
    public static MainCircuitData Create() => new();
}

/// <summary>
/// 回路解析の積算用ワークエリア。
/// 【C原典】struct sk_work (fyrt800.h)。
/// </summary>
public sealed class CircuitWork
{
    /// <summary>機器選定区分。【C原典】sk_work.kikiskbn。</summary>
    public char EquipmentSelectionKind { get; set; } = ' ';

    /// <summary>始動回路区分。【C原典】sk_work.startkbn。</summary>
    public char StartCircuitKind { get; set; } = ' ';

    /// <summary>設定電流値。【C原典】sk_work.setteii (DOUBLE)。</summary>
    public double SetCurrent { get; set; }

    /// <summary>機器マスター補助情報の定格容量(W, VA)。【C原典】sk_work.teiwva (DOUBLE)。</summary>
    public double RatedCapacity { get; set; }
}
