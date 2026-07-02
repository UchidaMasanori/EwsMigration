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

    /// <summary>行種。【C原典】gyosyu。</summary>
    public string LineType { get; set; } = string.Empty;

    /// <summary>回路設計エリア(元記述)。【C原典】kairoar。</summary>
    public string CircuitText { get; set; } = string.Empty;

    /// <summary>盤名称。【C原典】ban。</summary>
    public BanKind Ban { get; set; } = BanKind.End;

    /// <summary>
    /// kikitable_add() がタグ付きで設定する属性群。
    /// 【C原典】kikitable_add(tag, value, ...) の tag("0","1","11","CM","LN" 等)→value。
    /// </summary>
    public Dictionary<string, string> Attributes { get; } = new();
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

    /// <summary>エラー。【C原典】erra(FYRT805)/Perrc。</summary>
    public List<CircuitParseError> Errors { get; } = new();

    /// <summary>現在の盤区分(解析中に BN 行で更新)。【C原典】static BAN ban。</summary>
    public BanKind CurrentBan { get; set; } = BanKind.End;

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
