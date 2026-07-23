namespace Ews.Domain.Analysis;

/// <summary>
/// 1 機器の付属パラメータ(付属情報)。【C原典】<c>struct fparmg</c>(toku/include/common/fycommon.h:77)。
///
/// <c>mainfile_set</c>(Fyss1f.c:1464)の付属パラメータ設定ブロック(Fyss1f.c:1957?2205)で、
/// 機器テーブル(<c>KIKITABLE</c> / EquipmentTableEntry)の各種型式値(DLW/DLV/DLN/DCM/DIT/DMK など)から
/// 構成される付属フィールド群を格納する保持体。
/// C 原典では <c>syukairo</c>(FYDF806)配下に置かれ、<c>Main_Area_Clear</c>(Fyss1f.c:3293)が
/// syukairo 全体を '0' で埋めた後、一部の fp フィールドのみ ' ' で上書きするため、
/// フィールド既定値は '0' と ' ' が混在する(下記プロパティのコメント参照)。
///
/// 各値は「<c>mainfile_set</c> の memcpy/strncpy が組み立てる論理値(桁詰め前の生値)」を保持する。
/// 固定長バイト境界の整形(幅ぴったりへのパディング、全角=2バイトの CP932 バイト幅)は
/// <see cref="FparmgCodec"/> が行う(【C原典】memcpy(dst, buff, strlen(buff)) と同値の左詰め上書き)。
///
/// 整形は <c>Ews.Analysis.EquipmentParameterFormatter.FparmSet</c>(【C原典】mainfile_set の fp ブロック)が実施。
/// </summary>
public sealed class AttachedParameters
{
    /// <summary>負荷種類(LW=xx99KW の接頭部)。【C原典】fpalw1[2]。既定 ' '。</summary>
    public string LoadKind { get; set; } = string.Empty;

    /// <summary>負荷容量(W/VA)。【C原典】fpalw2[7]。既定 '0'。"%07.0f" 整形。</summary>
    public string LoadCapacity { get; set; } = string.Empty;

    /// <summary>負荷単位区分。【C原典】fpalwkbn。既定 ' '。'V':VA 'W':W。</summary>
    public char LoadUnitKind { get; set; } = ' ';

    /// <summary>負荷電圧(LV=)。【C原典】fpalv[2][3]。既定 '0'。</summary>
    public string[] LoadVoltage { get; } = new string[2] { string.Empty, string.Empty };

    /// <summary>負荷名称(LN=)/負荷名称2。【C原典】fpaln[2][20]。既定 ' '。[1]は予約語 "P"(盤)や負荷容量代用に使用。</summary>
    public string[] LoadName { get; } = new string[2] { string.Empty, string.Empty };

    /// <summary>コメント(予約語対象)(CM=)。【C原典】fpacm1[20]。既定 ' '。</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>コメント(行種対象)(CM=)。【C原典】fpacm2[20]。既定 ' '。(GCM/グループNo 依存のため当面未整形)</summary>
    public string LineTypeComment { get; set; } = string.Empty;

    /// <summary>コメントグループNO.。【C原典】fpacglno[3]。既定 '0'。(GCM_Group 依存のため当面未整形)</summary>
    public string CommentGroupNumber { get; set; } = string.Empty;

    /// <summary>品名(IT=)/PT。【C原典】fpaitpt[25]。既定 ' '。</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>SP区分(スペース枠付)。【C原典】spkvn。既定 ' '。' ':通常機器 '1':SP枠。</summary>
    public char SpFutureMountKind { get; set; } = ' ';

    /// <summary>寸法 縦mm(SP=縦*横*深)。【C原典】fpah[4]。既定 '0'。"%04d"。</summary>
    public string DimensionHeight { get; set; } = string.Empty;

    /// <summary>寸法 横mm。【C原典】fpaw[4]。既定 '0'。"%04d"。</summary>
    public string DimensionWidth { get; set; } = string.Empty;

    /// <summary>寸法 深mm。【C原典】fpad[4]。既定 '0'。"%04d"。</summary>
    public string DimensionDepth { get; set; } = string.Empty;

    /// <summary>寸法グループNO.。【C原典】fpasglno[3]。既定 '0'。(SP_Group 依存のため当面未整形)</summary>
    public string DimensionGroupNumber { get; set; } = string.Empty;

    /// <summary>外部取付区分 G()。【C原典】fpag。既定 ' '。'G':外部。</summary>
    public char ExternalMountKind { get; set; } = ' ';

    /// <summary>封印区分 H()。【C原典】fpahu。既定 ' '。'H':封印。</summary>
    public char SealKind { get; set; } = ' ';

    /// <summary>支給品区分 S()。【C原典】fpas。既定 ' '。'S':支給品。</summary>
    public char SuppliedKind { get; set; } = ' ';

    /// <summary>隔壁区分 K()。【C原典】fpak。既定 ' '。'K':隔壁。</summary>
    public char PartitionKind { get; set; } = ' ';

    /// <summary>メータ封印区分 MH()。【C原典】fpamh。既定 ' '。'M':メータ封印。</summary>
    public char MeterSealKind { get; set; } = ' ';

    /// <summary>制御電源番号(C)。【C原典】fpac[2]。既定 ' '。"%02d"。</summary>
    public string ControlPowerNumber { get; set; } = string.Empty;

    /// <summary>メーカーコード(MK=)。【C原典】fpamk[3]。既定 ' '。</summary>
    public string MakerCode { get; set; } = string.Empty;

    /// <summary>有電圧電源(UP=)。【C原典】fpaup[6]。既定 ' '。(mainfile_set の fp ブロック外で設定)</summary>
    public string PowerVoltage { get; set; } = string.Empty;

    /// <summary>扉取付区分 T(=扉)/I(=内)。【C原典】tikbn(改訂&lt;1&gt;)。既定 '0'。(mainfile_set の fp ブロック外で設定)</summary>
    public char DoorMountKind { get; set; } = '0';
}
