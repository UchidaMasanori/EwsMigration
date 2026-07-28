using Ews.Domain.Common;

namespace Ews.Domain.Masters;

/// <summary>
/// 直近上下位参照ファイル(機器選定の候補検索マスタ)。
///
/// 【C原典】
///   - 構造体: struct FYDF812            (toku/include/common/fydf812.h)
///   - ファイルID: FYDF812 / 編成: EWS-ISAM / レコード長: 300
///   - 検索: Fysk01_Chokkin_Read_Check(_ALL/_TMS)(toku/sekkei/src/Fysk01.c)が
///           FyIsamGStartR/FyIsamGNextR で走査し、キー先頭 <c>siz</c> バイト
///           (=予約語+メーカー+パラメータタイプ+電源区分+定格値の先頭)を
///           <c>memcmp</c> で前方一致検索して候補を絞り込む。
///
/// レガシーでは機器マスタ選定の直前に、要求仕様(予約語・変換形状タイプ・定格値キー)で
/// この ISAM を検索し、該当する実機器の定格キー(teikkey)・品名(hinmei)・
/// ハンドルロック区分(hlkbn)を得る。ep[2] の最終確定もこの検索結果に基づく。
///
/// 本移行では、まず検索の同定に使う KEY 部と、候補判定で直接参照する外側フィールド
/// (ハンドルロック区分・機器マスタ定格キー・品名・制御電圧適応範囲)を型付けする。
/// 共用情報部(kyoyojg: 主/制御電源共用区分・感度電流・各電圧)は、数値変換
/// <c>Fysk01_Change_Chokin</c> を移植する後続増分で追加する。
/// </summary>
public sealed class NearestRankReference : IIsamRecord
{
    /// <summary>【C原典】fydf812.h コメント「ﾚｺｰﾄﾞ長 300」。</summary>
    public static int RecordLength => 300;

    /// <summary>パラメータタイプ枠数。【C原典】key.tjg.ptype[7][7]。</summary>
    public const int ParameterTypeSlotCount = 7;

    // ---- KEY 部(struct reckeyc) ----

    /// <summary>予約語。【C原典】key.yoyaku[8]。</summary>
    public string ReservedWord { get; set; } = string.Empty;

    /// <summary>メーカーコード。【C原典】key.mkcd[3]。</summary>
    public string MakerCode { get; set; } = string.Empty;

    /// <summary>パラメータタイプ(7 枠)。【C原典】key.tjg.ptype[7][7]。</summary>
    public IReadOnlyList<string> ParameterTypes { get; set; } = [];

    /// <summary>主電源 AC/DC 区分。【C原典】key.sadkbn。</summary>
    public char MainPowerAcDc { get; set; } = ' ';

    /// <summary>制御電源 AC/DC 区分。【C原典】key.cadkbn。</summary>
    public char ControlPowerAcDc { get; set; } = ' ';

    /// <summary>
    /// 定格値キー。【C原典】key.kteichi[50]。
    /// Fysk04_Make_Teikakuchi(=<c>RatingKeyBuilder.MakeRatingKey</c>)が生成する
    /// 定格値キーと同形式で、検索の前方一致と定格値チェックに用いる。
    /// </summary>
    public string RatingKey { get; set; } = string.Empty;

    /// <summary>データ追番。【C原典】key.datano[4]。</summary>
    public string DataSequence { get; set; } = string.Empty;

    // ---- 共用情報部(struct kyoyojg jg) ----

    /// <summary>
    /// 共用情報部(主/制御電源共用区分・感度電流・一次/二次定格電圧・制御電圧)。
    /// 【C原典】jg。数値化は <c>Fysk01_Change_Chokin</c> で行う。
    /// </summary>
    public NearestRankSharedInfo SharedInfo { get; set; } = new();

    // ---- 外側フィールド(struct FYDF812) ----

    /// <summary>ハンドルロック区分('H'=ハンドルロック有)。【C原典】hlkbn。</summary>
    public char HandleLockKind { get; set; } = ' ';

    /// <summary>機器マスタ定格キー(選定結果として機器マスタ検索に渡すキー)。【C原典】teikkey[80]。</summary>
    public string EquipmentMasterRatingKey { get; set; } = string.Empty;

    /// <summary>品名。【C原典】hinmei[25]。</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>制御電圧適応範囲(from)。【C原典】vcfrom[3]。</summary>
    public string ControlVoltageRangeFrom { get; set; } = string.Empty;

    /// <summary>制御電圧適応範囲(to)。【C原典】vcto[3]。</summary>
    public string ControlVoltageRangeTo { get; set; } = string.Empty;

    // ---- バイトオフセット(struct FYDF812 先頭から算出) ----
    private const int OffsetReservedWord = 0;            // key.yoyaku[8]
    private const int OffsetMakerCode = 8;               // key.mkcd[3]
    private const int OffsetParameterTypes = 11;         // key.tjg.ptype[7][7]
    private const int ParameterTypeSize = 7;             // ptype[i][7]
    private const int OffsetMainPowerAcDc = 60;          // key.sadkbn
    private const int OffsetControlPowerAcDc = 61;       // key.cadkbn
    private const int OffsetRatingKey = 62;              // key.kteichi[50]
    private const int OffsetDataSequence = 112;          // key.datano[4]
    // 共用情報部 jg[59] = 116..174(ksadkbn/kcadkbn/kyoma[16]/kv1[11]/kv2[15]/kvc[15])
    private const int OffsetSharedMainPowerAcDc = 116;   // jg.ksadkbn
    private const int OffsetSharedControlPowerAcDc = 117; // jg.kcadkbn
    private const int OffsetSensitivityCurrents = 118;   // jg.km.kyomad[4][4]
    private const int SensitivityCurrentSize = 4;        // kyomad[i][4]
    private const int OffsetPrimaryVoltage = 134;        // jg.kv1(d1[3]/k1/d2[3]/k2/d3[3] = 11)
    private const int OffsetSecondaryVoltage = 145;      // jg.kv2(d1[3]/k1/d2[3]/k2/d3[3]/k3/d4[3] = 15)
    private const int OffsetControlVoltage = 160;        // jg.kvc(d1[3]/k1/d2[3]/k2/d3[3]/k3/d4[3] = 15)
    private const int OffsetHandleLockKind = 175;        // hlkbn
    private const int OffsetEquipmentMasterRatingKey = 176; // teikkey[80]
    private const int OffsetProductName = 256;           // hinmei[25]
    private const int OffsetControlVoltageRangeFrom = 281; // vcfrom[3]
    private const int OffsetControlVoltageRangeTo = 284;   // vcto[3]
    // filler[13] = 287..299

    /// <summary>
    /// 固定長 Shift-JIS レコードからドメインモデルを生成する。
    /// 【C原典】FyIsamGStartR/FyIsamGNextR で取得した struct FYDF812。
    /// </summary>
    public static NearestRankReference FromFixedRecord(ReadOnlySpan<byte> record)
    {
        var types = new string[ParameterTypeSlotCount];
        for (int i = 0; i < ParameterTypeSlotCount; i++)
        {
            types[i] = FixedFieldCodec.ReadText(record, OffsetParameterTypes + (i * ParameterTypeSize), ParameterTypeSize);
        }

        return new NearestRankReference
        {
            ReservedWord = FixedFieldCodec.ReadText(record, OffsetReservedWord, 8),
            // メーカーコードは "M  " など後続空白に意味があるため生 3 文字を保持する。
            MakerCode = FixedFieldCodec.ShiftJis.GetString(record.Slice(OffsetMakerCode, 3)),
            ParameterTypes = types,
            MainPowerAcDc = (char)record[OffsetMainPowerAcDc],
            ControlPowerAcDc = (char)record[OffsetControlPowerAcDc],
            RatingKey = FixedFieldCodec.ReadText(record, OffsetRatingKey, 50),
            DataSequence = FixedFieldCodec.ReadText(record, OffsetDataSequence, 4),
            SharedInfo = ReadSharedInfo(record),
            HandleLockKind = (char)record[OffsetHandleLockKind],
            EquipmentMasterRatingKey = FixedFieldCodec.ReadText(record, OffsetEquipmentMasterRatingKey, 80),
            ProductName = FixedFieldCodec.ReadText(record, OffsetProductName, 25),
            ControlVoltageRangeFrom = FixedFieldCodec.ReadText(record, OffsetControlVoltageRangeFrom, 3),
            ControlVoltageRangeTo = FixedFieldCodec.ReadText(record, OffsetControlVoltageRangeTo, 3),
        };
    }

    /// <summary>共用情報部(jg)を固定長レコードから読み取る。【C原典】struct kyoyojg。</summary>
    private static NearestRankSharedInfo ReadSharedInfo(ReadOnlySpan<byte> record)
    {
        var sensitivity = new string[NearestRankSharedInfo.SensitivityCurrentCount];
        for (int i = 0; i < sensitivity.Length; i++)
        {
            sensitivity[i] = FixedFieldCodec.ReadText(record, OffsetSensitivityCurrents + (i * SensitivityCurrentSize), SensitivityCurrentSize);
        }

        // kv1: d1[3]/k1/d2[3]/k2/d3[3]
        int kv1 = OffsetPrimaryVoltage;
        // kv2: d1[3]/k1/d2[3]/k2/d3[3]/k3/d4[3]
        int kv2 = OffsetSecondaryVoltage;
        // kvc: d1[3]/k1/d2[3]/k2/d3[3]/k3/d4[3]
        int kvc = OffsetControlVoltage;

        return new NearestRankSharedInfo
        {
            MainPowerSharedAcDc = (char)record[OffsetSharedMainPowerAcDc],
            ControlPowerSharedAcDc = (char)record[OffsetSharedControlPowerAcDc],
            SensitivityCurrents = sensitivity,
            PrimaryVoltageValues =
            [
                FixedFieldCodec.ReadText(record, kv1, 3),
                FixedFieldCodec.ReadText(record, kv1 + 4, 3),
                FixedFieldCodec.ReadText(record, kv1 + 8, 3),
            ],
            PrimaryVoltageKinds = [(char)record[kv1 + 3], (char)record[kv1 + 7]],
            SecondaryVoltageValues =
            [
                FixedFieldCodec.ReadText(record, kv2, 3),
                FixedFieldCodec.ReadText(record, kv2 + 4, 3),
                FixedFieldCodec.ReadText(record, kv2 + 8, 3),
                FixedFieldCodec.ReadText(record, kv2 + 12, 3),
            ],
            SecondaryVoltageKinds = [(char)record[kv2 + 3], (char)record[kv2 + 7], (char)record[kv2 + 11]],
            ControlVoltageValues =
            [
                FixedFieldCodec.ReadText(record, kvc, 3),
                FixedFieldCodec.ReadText(record, kvc + 4, 3),
                FixedFieldCodec.ReadText(record, kvc + 8, 3),
                FixedFieldCodec.ReadText(record, kvc + 12, 3),
            ],
            ControlVoltageKinds = [(char)record[kvc + 3], (char)record[kvc + 7], (char)record[kvc + 11]],
        };
    }

    /// <summary>KEY 部の合計バイト長(前方一致の基準位置)。【C原典】kteichi 開始オフセット = 62。</summary>
    public const int KeyPrefixLength = 62;

    /// <summary>
    /// 前方一致検索用の比較キー(KEY 部 62 バイト + 定格値キー 50 バイト = 112 文字)を組み立てる。
    /// 【C原典】<c>memcmp(data, &amp;tmp, siz)</c>(Fysk01.c)。C 原典は固定長バイト列を直接比較するため、
    /// 各フィールドを元の固定幅に空白右詰め(超過分は切り捨て)して等価なバイト列を再現する。
    /// レイアウト: yoyaku[8] + mkcd[3] + ptype[7][7]=49 + sadkbn[1] + cadkbn[1] + kteichi[50]。
    /// </summary>
    public string BuildComparisonKey()
    {
        var buffer = new System.Text.StringBuilder(KeyPrefixLength + 50);
        buffer.Append(Fit(ReservedWord, 8));
        buffer.Append(Fit(MakerCode, 3));
        for (int i = 0; i < ParameterTypeSlotCount; i++)
        {
            string type = i < ParameterTypes.Count ? ParameterTypes[i] : string.Empty;
            buffer.Append(Fit(type, ParameterTypeSize));
        }
        buffer.Append(MainPowerAcDc);
        buffer.Append(ControlPowerAcDc);
        buffer.Append(Fit(RatingKey, 50));
        return buffer.ToString();
    }

    /// <summary>文字列を空白で固定幅に右詰めし、超過分は切り捨てる(固定長フィールド相当)。</summary>
    private static string Fit(string value, int width)
    {
        string source = value ?? string.Empty;
        return source.Length >= width ? source[..width] : source.PadRight(width);
    }
}
