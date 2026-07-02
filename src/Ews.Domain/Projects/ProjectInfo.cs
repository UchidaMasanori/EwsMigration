using Ews.Domain.Common;

namespace Ews.Domain.Projects;

/// <summary>
/// 物件情報(盤の依頼明細単位の基本情報)。
///
/// 【C原典】
///   - 構造体: struct FYDF801            (toku/include/common/fydf801.h)
///   - ファイルID: FYDF801 / 編成: EWS-ISAM / レコード長: 1200
///
/// キー情報(依頼明細番号)、図番、データ属性(物件/非物件・図面センター区分)、
/// 新規登録依頼明細番号などを保持する、仕様入力・回路解析の起点となるレコード。
///
/// 本パイロットでは先頭のキー/識別領域(先頭 50 バイト)のみ型付けし、
/// 物件共通情報 union(struct kyoutu / bmeisai 等)は <see cref="RawRecord"/> として保持する。
/// </summary>
public sealed class ProjectInfo : IIsamRecord
{
    /// <summary>【C原典】fydf801.h コメント「ﾌｧｲﾙﾚｲｱｳﾄ 1200」。</summary>
    public static int RecordLength => 1200;

    // ---- キー情報(struct key) ----

    /// <summary>営業所コード(依頼番号の先頭)。【C原典】key.im.eigyocd[2]。</summary>
    public string SalesOfficeCode { get; set; } = string.Empty;

    /// <summary>明細番号 '01'～'99'(第2キー)。【C原典】key.meisaino[2]。</summary>
    public string ItemNumber { get; set; } = string.Empty;

    /// <summary>図番(上10桁)。【C原典】zubanu10[10]。</summary>
    public string DrawingNumberUpper { get; set; } = string.Empty;

    /// <summary>図番(下5桁)。【C原典】zubanl5[5]。</summary>
    public string DrawingNumberLower { get; set; } = string.Empty;

    /// <summary>データ状態 ' ':通常 'P':保留。【C原典】datastat。</summary>
    public char DataStatus { get; set; } = ' ';

    /// <summary>物件・非物件管理データ区分 'B':物件管理 ' ':非物件管理。【C原典】datbukbn。</summary>
    public char ManagementKind { get; set; } = ' ';

    /// <summary>図面センター・他工場登録データ区分 'Z':図面C ' ':他工場。【C原典】datzukbn。</summary>
    public char RegistrationKind { get; set; } = ' ';

    /// <summary>新規登録依頼番号。【C原典】rk.airaino[7]。</summary>
    public string NewRequestNumber { get; set; } = string.Empty;

    /// <summary>新規登録明細番号。【C原典】rk.ameisano[2]。</summary>
    public string NewItemNumber { get; set; } = string.Empty;

    /// <summary>自動作図区分 ' ':通常 '1':自動。【C原典】autokbn。</summary>
    public char AutoDrawingKind { get; set; } = ' ';

    /// <summary>未展開領域を含むレコード全体のバイト列(Shift-JIS, 固定長)。</summary>
    public byte[] RawRecord { get; set; } = [];

    // バイトオフセット(struct FYDF801 先頭から算出)
    private const int OffsetSalesOfficeCode = 0;     // key.im.eigyocd[2]
    // key.im.filler1[5] (off 2)
    private const int OffsetItemNumber = 7;          // key.meisaino[2]
    private const int OffsetDrawingNumberUpper = 9;  // zubanu10[10]
    private const int OffsetDrawingNumberLower = 19; // zubanl5[5]
    // iraikai(24), zubankai(25)
    private const int OffsetDataStatus = 26;         // datastat
    private const int OffsetManagementKind = 27;     // datbukbn
    // datdokbn(28)
    private const int OffsetRegistrationKind = 29;   // datzukbn
    private const int OffsetNewRequestNumber = 30;   // rk.airaino[7]
    private const int OffsetNewItemNumber = 37;      // rk.ameisano[2]
    private const int OffsetAutoDrawingKind = 39;    // autokbn

    /// <summary>
    /// 固定長 Shift-JIS レコードからドメインモデルを生成する。
    /// 【C原典】物件情報読込(FyIsamRead で取得した struct FYDF801)。
    /// </summary>
    public static ProjectInfo FromFixedRecord(ReadOnlySpan<byte> record)
    {
        return new ProjectInfo
        {
            SalesOfficeCode = FixedFieldCodec.ReadText(record, OffsetSalesOfficeCode, 2),
            ItemNumber = FixedFieldCodec.ReadText(record, OffsetItemNumber, 2),
            DrawingNumberUpper = FixedFieldCodec.ReadText(record, OffsetDrawingNumberUpper, 10),
            DrawingNumberLower = FixedFieldCodec.ReadText(record, OffsetDrawingNumberLower, 5),
            DataStatus = ReadChar(record, OffsetDataStatus),
            ManagementKind = ReadChar(record, OffsetManagementKind),
            RegistrationKind = ReadChar(record, OffsetRegistrationKind),
            NewRequestNumber = FixedFieldCodec.ReadText(record, OffsetNewRequestNumber, 7),
            NewItemNumber = FixedFieldCodec.ReadText(record, OffsetNewItemNumber, 2),
            AutoDrawingKind = ReadChar(record, OffsetAutoDrawingKind),
            RawRecord = record.ToArray(),
        };
    }

    private static char ReadChar(ReadOnlySpan<byte> record, int offset)
        => (char)record[offset];
}
