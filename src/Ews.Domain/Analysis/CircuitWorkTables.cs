namespace Ews.Domain.Analysis;

/// <summary>
/// 回路設計の盤区分。
/// 【C原典】Fyss11.c の typedef enum _BAN(序数はC原典と一致させること)。
/// 盤名称文(BN)の解析で確定し、機器テーブルへ "%03d" で展開される。
/// </summary>
public enum BanKind : short
{
    /// <summary>終了/未確定。【C原典】ban_END。</summary>
    End = 0,

    /// <summary>分岐盤。【C原典】ban_BUN。</summary>
    Branch = 1,

    /// <summary>引込盤。【C原典】ban_HIK。</summary>
    Incoming = 2,

    /// <summary>開閉器盤。【C原典】ban_KAI。</summary>
    Switch = 3,

    /// <summary>主幹盤。【C原典】ban_SYU。</summary>
    Main = 4,

    /// <summary>制御盤。【C原典】ban_SEI。</summary>
    Control = 5,

    /// <summary>計器盤。【C原典】ban_KEI。</summary>
    Meter = 6,

    /// <summary>ボックス。【C原典】ban_BOX。</summary>
    Box = 7,

    /// <summary>内器盤。【C原典】ban_NAI。</summary>
    Internal = 8,

    /// <summary>エラー。【C原典】ban_ERR。</summary>
    Error = 9,
}

/// <summary>
/// 系統テーブル(1行=1系統)。
/// 【C原典】struct KEITOU(toku/include/sekkei/struct.h)。
/// </summary>
public sealed class SystemTableEntry
{
    /// <summary>系統番号。【C原典】K_No。</summary>
    public short SystemNumber { get; set; }

    /// <summary>系統種別。【C原典】Kind。</summary>
    public char SystemKind { get; set; } = ' ';

    /// <summary>系統行種。【C原典】gyosyu[GYOSYULEN+1]。</summary>
    public string LineType { get; set; } = string.Empty;
}

/// <summary>
/// 行種テーブル(1行=1行種グループ)。
/// 【C原典】struct GYOSYU(toku/include/sekkei/struct.h)。
/// </summary>
public sealed class LineTypeTableEntry
{
    /// <summary>系統番号。【C原典】K_No。</summary>
    public short SystemNumber { get; set; }

    /// <summary>行種グループNo。【C原典】G_No。</summary>
    public short GroupNumber { get; set; }

    /// <summary>記述行。【C原典】K_Gyo[3+1]。</summary>
    public string DescriptionRow { get; set; } = string.Empty;

    /// <summary>記述桁。【C原典】K_Ket[3+1]。</summary>
    public string DescriptionColumn { get; set; } = string.Empty;

    /// <summary>親行種グループNo。【C原典】O_No。</summary>
    public short ParentGroupNumber { get; set; }

    /// <summary>出現数。【C原典】Cnt。</summary>
    public short Count { get; set; }

    /// <summary>行種ランク。【C原典】Rank。</summary>
    public short Rank { get; set; }

    /// <summary>行種(原文)。【C原典】Gyosyu[GYOSYULEN+1]。</summary>
    public string LineTypeRaw { get; set; } = string.Empty;

    /// <summary>行種(整形)。【C原典】gyosyu[GYOSYULEN+1]。</summary>
    public string LineType { get; set; } = string.Empty;

    /// <summary>行種連番。【C原典】G_ren。</summary>
    public short Sequence { get; set; }

    /// <summary>回路分類。【C原典】G_kind。</summary>
    public char CircuitClass { get; set; } = ' ';

    /// <summary>記述区分。【C原典】K_kind。</summary>
    public char DescriptionKind { get; set; } = ' ';

    /// <summary>回路番号。【C原典】kairono[3+1]。</summary>
    public string CircuitNumber { get; set; } = string.Empty;

    /// <summary>相と電圧(3桁目)。【C原典】souden[3](改訂&lt;6&gt;)。</summary>
    public string PhaseVoltage { get; set; } = string.Empty;

    /// <summary>回路相線数。【C原典】sousen[4+1](改訂&lt;25&gt;)。</summary>
    public string PhaseWires { get; set; } = string.Empty;
}

/// <summary>
/// 仕様文字列テーブル(1行=1仕様文字列)。
/// 【C原典】struct SPEC(toku/include/sekkei/struct.h)。
/// </summary>
public sealed class SpecTableEntry
{
    /// <summary>系統番号。【C原典】K_No。</summary>
    public short SystemNumber { get; set; }

    /// <summary>行種グループNo。【C原典】G_No。</summary>
    public short GroupNumber { get; set; }

    /// <summary>仕様文字列No。【C原典】S_No。</summary>
    public short SpecNumber { get; set; }

    /// <summary>記述行。【C原典】K_Gyo[3+1]。</summary>
    public string DescriptionRow { get; set; } = string.Empty;

    /// <summary>記述桁。【C原典】K_Ket[3+1]。</summary>
    public string DescriptionColumn { get; set; } = string.Empty;

    /// <summary>仕様文字列長さ。【C原典】Len。</summary>
    public short Length { get; set; }

    /// <summary>仕様文字列開始文字。【C原典】Pref。</summary>
    public char Prefix { get; set; } = ' ';

    /// <summary>仕様文字列。【C原典】*Stg。</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// 機器テーブル(主回路用/制御用)。
/// 【C原典】struct KIKITABLE(toku/include/sekkei/struct.h)。
///
/// C原典は50超のフィールドを持つ大型構造体で、機器選定(Fyss12?Fyss15)で
/// 段階的に値が設定される。本移行ではまず識別キー系のフィールドと、
/// kikitable_add() がタグ付きで設定する属性を <see cref="Attributes"/> として保持し、
/// 個別フィールドは各 Fyss* リーフ移植時に追加していく。
/// </summary>
public sealed class EquipmentTableEntry
{
    /// <summary>系統番号。【C原典】K_No。</summary>
    public short SystemNumber { get; set; }

    /// <summary>行種グループNo。【C原典】G_No。</summary>
    public short GroupNumber { get; set; }

    /// <summary>仕様文字列No。【C原典】S_No。</summary>
    public short SpecNumber { get; set; }

    /// <summary>機器No。【C原典】D_No。</summary>
    public short EquipmentNumber { get; set; }

    /// <summary>文字列連番。【C原典】B_No。</summary>
    public short StringSequence { get; set; }

    /// <summary>文字列回路番号連番。【C原典】N_No。</summary>
    public short CircuitNumberSequence { get; set; }

    /// <summary>コメントグループ番号(複合予約語の括弧階層)。【C原典】GroupNo。</summary>
    public short ControlGroupNumber { get; set; }

    /// <summary>予約語(英字部)。【C原典】yoyaku。</summary>
    public string ReservedWord { get; set; } = string.Empty;

    /// <summary>予約語番号。【C原典】ysno。</summary>
    public string ReservedWordNumber { get; set; } = string.Empty;

    /// <summary>
    /// 品名(予約語マスタ照合済みの正規予約語)。【C原典】kikimei / s_yoyaku。
    /// 予約語文を予約語マスタ(fyak_tbl)へ照合して確定した正規の予約語を保持する。
    /// (電気パラメータ検証・型式展開 Fyss1d は段階移植のため未反映)
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>記述行(行番号)。【C原典】解析対象 gyono。</summary>
    public short LineNumber { get; set; }

    /// <summary>記述桁。【C原典】colm。</summary>
    public short Column { get; set; }

    /// <summary>
    /// 記述行。【C原典】K_Gyo[3+1]。
    /// 回路記述上の行位置。主回路ファイルエリア生成(Fyss1f mainfile_set)が
    /// 記述行(gyo)算出に使用する。数値文字列("003"等)。未設定時は空。
    /// </summary>
    public string DescriptionRow { get; set; } = string.Empty;

    /// <summary>
    /// 記述桁。【C原典】K_Ket[3+1]。
    /// 回路記述上の桁位置。主回路ファイルエリア生成(Fyss1f mainfile_set)が
    /// 記述桁(keta)・記述行(gyo)算出に使用する(keta%KAIROARLEN, (keta-1)/KAIROARLEN)。
    /// 数値文字列。未設定時は空。
    /// </summary>
    public string DescriptionColumn { get; set; } = string.Empty;

    /// <summary>行種。【C原典】gyosyu。</summary>
    public string LineType { get; set; } = string.Empty;

    /// <summary>回路設計エリア(元記述)。【C原典】kairoar。</summary>
    public string CircuitText { get; set; } = string.Empty;

    /// <summary>盤名称。【C原典】ban。</summary>
    public BanKind Ban { get; set; } = BanKind.End;

    /// <summary>
    /// 回路区分。【C原典】K_Kubun。
    /// Kairo_Kubun_Set(Fyss12 step5)が機器種別・行種・計器パターンに応じて設定する。
    /// 'K'=計器/付属機器グループ, 'M'=主機器, 'S'=SC分岐, ' '=未設定。
    /// </summary>
    public char CircuitDivision { get; set; } = ' ';

    /// <summary>
    /// 同一機器認識番号。【C原典】E_No。
    /// Yoyakugo_Add_Main(Fyss12 step6)の機器追加(SEP/CT/WH/ZCT)で 0 に設定される。
    /// </summary>
    public short EquipmentIdentityNumber { get; set; }

    /// <summary>
    /// 自動生成区分。【C原典】yoyakkbn。
    /// ' '=通常記述, '1'=自動生成(SEP/CT/WH/ZCT 等の追加機器)。
    /// </summary>
    public char AutoGenerationKind { get; set; } = ' ';

    /// <summary>数量。【C原典】Kosu。</summary>
    public short Quantity { get; set; }

    /// <summary>グループ数量。【C原典】GKosu。</summary>
    public short GroupQuantity { get; set; }

    /// <summary>
    /// 機器ランク(階層番号)。【C原典】Rank。
    /// Kiki_Rank_Set/Pattern_Rank_Update/TR_Rank_Set/WH_Rank_Set(Fyss12 step9-13.5)が設定する。
    /// </summary>
    public short Rank { get; set; }

    /// <summary>
    /// 機器先頭フラグ。【C原典】TOP_Flg。
    /// Kiki_Rank_Update/Pattern_Rank_Update(Fyss12 step10/12)が設定する。
    /// '1'=グループ先頭機器, ' '=非先頭。
    /// </summary>
    public char TopFlag { get; set; } = ' ';

    /// <summary>
    /// 電源フラグ。【C原典】C_Flg。
    /// 電源機器(2電源等)の識別に使用(941220 改訂で Kiki_Rank_Update が参照)。
    /// </summary>
    public char PowerSourceFlag { get; set; } = ' ';

    /// <summary>
    /// 指定回路番号(単一)。【C原典】DNO。
    /// 「(NO=2)」等の回路番号指定記述。主回路ファイルエリア生成
    /// (Fyss1f Find_Nobangou/mainfile_pre_set)が参照する。未設定時は空。
    /// </summary>
    public string CircuitNumberText { get; set; } = string.Empty;

    /// <summary>
    /// 指定回路番号(グループ集約)。【C原典】GNO。
    /// 「(NO=2,3)」等のカンマ連結した回路番号指定。
    /// 主回路ファイルエリア生成(Fyss1f Find_Nobangou)が参照する。未設定時は空。
    /// </summary>
    public string GroupCircuitNumberText { get; set; } = string.Empty;

    /// <summary>
    /// 括弧種別１。【C原典】Kakko1。複合予約語の括弧階層識別。
    /// </summary>
    public short Kakko1 { get; set; }

    /// <summary>
    /// 括弧種別２。【C原典】Kakko2。複合予約語の括弧階層識別。
    /// </summary>
    public short Kakko2 { get; set; }

    /// <summary>
    /// 電源番号。【C原典】C_No。kikitable_add("5") で電源機器に採番する。
    /// </summary>
    public short PowerSourceNumber { get; set; }

    /// <summary>
    /// ＳＰフラグ。【C原典】SP_Flg。kikitable_add("9") で設定する。
    /// </summary>
    public char SpecialFlag { get; set; } = ' ';

    /// <summary>
    /// メーカー。【C原典】DMK[3+1]。代入文「(MK=…)」の値。kikitable_add("MK")。
    /// </summary>
    public string Maker { get; set; } = string.Empty;

    /// <summary>
    /// 品名。【C原典】DIT[25+1]。代入文「(IT=…)」の値。kikitable_add("IT")。
    /// (機器マスター品名索引 FYDF817 による妥当性検証 Check_IT は ISAM 依存のため未実施)
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// コメント。【C原典】DCM[20+1]。代入文「(CM=…)」の値(1つ目)。kikitable_add("CM")。
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// コメント2。【C原典】DCM2[20+1]。代入文「(CM=…)」の値(2つ目)。kikitable_add("CM")。
    /// </summary>
    public string Comment2 { get; set; } = string.Empty;

    /// <summary>
    /// ＳＰ(寸法)。【C原典】DSP[14+1]。代入文「(SP=…)」の値。kikitable_add("SP")。
    /// </summary>
    public string SpecialDimension { get; set; } = string.Empty;

    /// <summary>
    /// 負荷容量。【C原典】DLW[10+1]。代入文「(LW=…)」の値。kikitable_add("LW")。
    /// </summary>
    public string LoadCapacity { get; set; } = string.Empty;

    /// <summary>
    /// 負荷名称。【C原典】DLN[20+1]。代入文「(LN=…)」の値。kikitable_add("LN")。
    /// </summary>
    public string LoadName { get; set; } = string.Empty;

    /// <summary>
    /// 負荷電圧。【C原典】DLV[2][3+1]。代入文「(LV=…)」の値。kikitable_add("LV0")。
    /// </summary>
    public string[] LoadVoltage { get; } = new string[2] { string.Empty, string.Empty };

    /// <summary>
    /// 有電圧電源。【C原典】DUP[6+1]。代入文「(UP=…)」の値。kikitable_add("UP")。
    /// </summary>
    public string PowerVoltage { get; set; } = string.Empty;

    /// <summary>
    /// 特注送り配置。【C原典】HAI。代入文「(HAI=L/C/T/O/D)」の値。kikitable_add("HAI")。
    /// </summary>
    public char SendPlacement { get; set; } = ' ';

    /// <summary>
    /// 特注盤分岐配列。【C原典】BUN_RETU。代入文「(B=W/L/R)」の値。kikitable_add("B")。
    /// </summary>
    public char BranchArrangement { get; set; } = ' ';

    /// <summary>
    /// ＷＨ配置指定。【C原典】WHAI。代入文「(WHAI=L/R)」の値。kikitable_add("WHAI")。
    /// </summary>
    public char WhPlacement { get; set; } = ' ';

    /// <summary>
    /// 備考。【C原典】BIKO[34]。代入文「(BK=…)」「(BKO=…)」の値。kikitable_add("BK")。
    /// </summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    /// 下部出線。【C原典】CNCT。代入文「(CNCT=POW)」の値。kikitable_add("CNCT")。
    /// </summary>
    public char BottomOutgoing { get; set; } = ' ';

    /// <summary>
    /// kikitable_add() がタグ付きで設定する属性群。
    /// 【C原典】kikitable_add(tag, value, ...) の tag("0","1","11","CM","LN" 等)→value。
    /// </summary>
    public Dictionary<string, string> Attributes { get; } = new();

    /// <summary>
    /// 定格キー(電気パラメータ)値。【C原典】KIKITABLE の <c>key_tbl</c>(union fyrt811)。
    /// 電気パラメータ検証(Fyss1d key_check_&lt;TYPE&gt;)で格納され、
    /// Ele_Equal_Check(Fyss12 step3)等の後段チェックが参照する。未検証時は null。
    /// </summary>
    public RatingValues? RatingValues { get; set; }
}

/// <summary>
/// 制御仕様テーブル(最小モデル)。
/// 【C原典】struct FYRT820(制御仕様テーブル, P_SgsTable)。
/// 詳細フィールドは制御回路移植(Fyss13/Fyss19)時に拡張する。
/// </summary>
public sealed class ControlSpecEntry
{
    /// <summary>系統番号。【C原典】K_No 相当。</summary>
    public short SystemNumber { get; set; }

    /// <summary>制御仕様の生記述。【C原典】FYRT820 の記述領域。</summary>
    public string RawText { get; set; } = string.Empty;
}

/// <summary>
/// 回路文字列解析で検出したエラー。
/// 【C原典】Error_Proc(errcode, gyono, colm, msgid, Perrc, erra) → struct FYRT805。
/// </summary>
/// <param name="ErrorCode">エラーコード。【C原典】errcode(例 "FY-004E")。</param>
/// <param name="LineNumber">行番号。【C原典】gyono(errline)。</param>
/// <param name="Column">桁。【C原典】colm(errcolm)。</param>
/// <param name="MessageId">メッセージID。【C原典】msgid(例 "FYMEE80")。</param>
public sealed record CircuitParseError(
    string ErrorCode,
    int LineNumber,
    int Column,
    string MessageId);

/// <summary>
/// 主回路ファイルエリア生成における機器グループの分解種別。
/// 【C原典】Fyss1f Main_File_Area_Make が三段階で判定する分解方式に対応する。
/// </summary>
public enum MainCircuitSegmentKind
{
    /// <summary>単純グループ(繰り返し/回路番号指定なし)。【C原典】Find_Group → Main_File_Make_s。</summary>
    Simple = 0,

    /// <summary>繰り返し(グループ数量による数量分解)。【C原典】Find_Iteration → Main_File_Make_d。</summary>
    Iteration = 1,

    /// <summary>回路番号文(NO 指定文の展開)。【C原典】Find_Nobangou → Main_File_Make_n。</summary>
    CircuitNumber = 2,
}

/// <summary>
/// 主回路ファイルエリア生成における 1 機器グループの数量分解結果。
/// 【C原典】Fyss1f Main_File_Area_Make のループ 1 反復(Find_* + Main_File_Make_*)に対応。
/// C原典は判定後ただちに FYRT800 レコードを生成するが、本移行では
/// レコード整形(mainfile_set/FYRT800)を段階移植とし、分解結果のみを保持する。
/// </summary>
public sealed class MainCircuitSegment
{
    /// <summary>分解種別。【C原典】Find_Iteration/Find_Nobangou/Find_Group の選択結果。</summary>
    public MainCircuitSegmentKind Kind { get; set; }

    /// <summary>機器テーブル上の開始インデックス。【C原典】ループ変数 i。</summary>
    public int StartIndex { get; set; }

    /// <summary>グループ機器件数。【C原典】kensu(Find_* の *kj)。</summary>
    public short Count { get; set; }

    /// <summary>行種グループNo。【C原典】KIKITABLE.G_No。</summary>
    public short GroupNumber { get; set; }

    /// <summary>文字列連番。【C原典】KIKITABLE.B_No。</summary>
    public short StringSequence { get; set; }

    /// <summary>文字列回路番号連番。【C原典】KIKITABLE.N_No。</summary>
    public short CircuitNumberSequence { get; set; }

    /// <summary>グループ内最小機器No。【C原典】Min_No。</summary>
    public short MinNumber { get; set; }

    /// <summary>グループ内最大機器No。【C原典】Max_No。</summary>
    public short MaxNumber { get; set; }

    /// <summary>分解基点の機器No。【C原典】D_No(Find_Iteration/Find_Nobangou)。</summary>
    public short StartNumber { get; set; }

    /// <summary>繰り返し数(グループ数量)。【C原典】Iteration(Find_Iteration)。</summary>
    public short Iteration { get; set; }

    /// <summary>最大回路番号連番。【C原典】Max_rank(Find_Nobangou)。</summary>
    public short MaxCircuitNumberRank { get; set; }

    /// <summary>指定回路番号(単一)。【C原典】DNO(Find_Nobangou)。</summary>
    public string CircuitNumberText { get; set; } = string.Empty;

    /// <summary>指定回路番号(グループ集約)。【C原典】GNO(Find_Nobangou)。</summary>
    public string GroupCircuitNumberText { get; set; } = string.Empty;
}

/// <summary>
/// 系統文字列チェックの出力一式。
/// 【C原典】Fyss11_Mojiretu_Check() の出力引数群
///   (P_Keitou/P_Gyosyu/P_Spec/P_Kiki/P_CKiki/P_SgsTable/erra)。
/// </summary>
public sealed class CircuitParseResult
{
    /// <summary>系統テーブル。【C原典】P_Keitou(KEITOU)。</summary>
    public List<SystemTableEntry> Systems { get; } = new();

    /// <summary>行種テーブル。【C原典】P_Gyosyu(GYOSYU)。</summary>
    public List<LineTypeTableEntry> LineTypes { get; } = new();

    /// <summary>仕様文字列テーブル。【C原典】P_Spec(SPEC)。</summary>
    public List<SpecTableEntry> Specs { get; } = new();

    /// <summary>主回路用機器テーブル。【C原典】P_Kiki(KIKITABLE)。</summary>
    public List<EquipmentTableEntry> MainEquipment { get; } = new();

    /// <summary>制御用機器テーブル。【C原典】P_CKiki(KIKITABLE)。</summary>
    public List<EquipmentTableEntry> ControlEquipment { get; } = new();

    /// <summary>制御仕様テーブル。【C原典】P_SgsTable(FYRT820)。</summary>
    public List<ControlSpecEntry> ControlSpecs { get; } = new();

    /// <summary>
    /// 主回路ファイルエリアの数量分解結果。
    /// 【C原典】Fyss1f Main_File_Area_Make が主回路設計エリア(FYRT800)へ
    /// レコード生成する際の機器グループ単位の分解結果を保持する。
    /// FYRT800 レコードのフィールド整形(mainfile_set)は段階移植のため未実装。
    /// </summary>
    public List<MainCircuitSegment> MainCircuitSegments { get; } = new();

    /// <summary>
    /// 生成された主回路設計エリア(FYRT800)レコード群。
    /// 【C原典】Fyss1f Main_File_Area_Make → Main_File_Make_s/d/n → mainfile_pre_set →
    /// mainfile_set が生成する struct FYRT800 の配列(*maina, 件数 *Pmainc)。
    /// 本移行では単純グループ(Find_Group → Main_File_Make_s)の決定的フィールドのみを設定する。
    /// 繰り返し/回路番号文(Make_d/Make_n)およびサフィックス生成・電気/付属パラメータは段階移植。
    /// </summary>
    public List<MainCircuitResult> MainCircuits { get; } = new();

    /// <summary>エラー。【C原典】erra(FYRT805)/Perrc。</summary>
    public List<CircuitParseError> Errors { get; } = new();

    /// <summary>現在の盤区分(解析中に BN 行で更新)。【C原典】static BAN ban。</summary>
    public BanKind CurrentBan { get; set; } = BanKind.End;

    /// <summary>
    /// 盤名称状態(現在)。【C原典】Fyss1f.c static CHAR epabn(初期 '1')。
    /// mainfile_set が予約語(P/SP/MP/UP)別に更新し ep[0].epabn へ反映する。
    /// Fyss12_Make_Main_Sub の冒頭で '1' にリセットされる。
    /// </summary>
    public char PanelNameKind { get; set; } = '1';

    /// <summary>
    /// 盤名称状態(直前)。【C原典】Fyss1f.c static CHAR bepabn(初期 '\0')。
    /// </summary>
    public char PanelNameKindPrevious { get; set; } = '\0';

    /// <summary>
    /// 現在の系統種別(直近の P/SP/MP/UP 行で確定)。
    /// 【C原典】static CHAR syu = '5'(既定値)。予約語行はこの系統に属する。
    /// </summary>
    public char CurrentSystemKind { get; set; } = '5';

    /// <summary>
    /// 現系統内の行種連番。【C原典】static i_Gyo_Ren(新系統で 0 クリア、行種毎に ++)。
    /// </summary>
    public short LineTypeSequence { get; set; }

    /// <summary>
    /// 現在の回路相数(直近の入線/有電源文で確定)。【C原典】static CHAR KAIROSOU = '0'。
    /// </summary>
    public char CircuitPhase { get; set; } = '0';

    /// <summary>
    /// 現在の回路電源電圧(3桁目)。【C原典】static CHAR KAIRODEN = '0'(改訂&lt;13&gt;)。
    /// </summary>
    public char CircuitVoltageDigit { get; set; } = '0';

    /// <summary>
    /// 現在の回路相線数。【C原典】static CHAR KAIROSOUSEN[4+1] = "0"(改訂&lt;25&gt;)。
    /// </summary>
    public string CircuitPhaseWires { get; set; } = "0";

    /// <summary>
    /// 回路番号の通し採番カウンタ。【C原典】static struct BANGOU Kbangoua。
    /// 解析全体で累積し、相数('1'/'3')と行種区分により該当カウンタを進める。
    /// </summary>
    public CircuitNumberCounters CircuitNumbers { get; } = new();

    /// <summary>エラーが無ければ true。【C原典】*Perrc == 0。</summary>
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// 回路番号カウンタ。【C原典】struct BANGOU(toku/sekkei/src/Fyss11.c)。
/// 単相(相数'1')系と三相(それ以外)系で別々のカウンタを持つ。
/// </summary>
public sealed class CircuitNumberCounters
{
    /// <summary>回路番号 M1。【C原典】Mno1。</summary>
    public short MainSingle { get; set; }

    /// <summary>回路番号 B1。【C原典】Bno1。</summary>
    public short BranchSingle { get; set; }

    /// <summary>回路番号 O1。【C原典】Ono1。</summary>
    public short OutgoingSingle { get; set; }

    /// <summary>回路番号 1。【C原典】Nno1。</summary>
    public short NumberSingle { get; set; }

    /// <summary>回路番号 M3。【C原典】Mno3。</summary>
    public short MainThree { get; set; }

    /// <summary>回路番号 B3。【C原典】Bno3。</summary>
    public short BranchThree { get; set; }

    /// <summary>回路番号 O3。【C原典】Ono3。</summary>
    public short OutgoingThree { get; set; }

    /// <summary>回路番号 3。【C原典】Nno3。</summary>
    public short NumberThree { get; set; }

    /// <summary>回路番号 S。【C原典】Sno。</summary>
    public short Switch { get; set; }
}
