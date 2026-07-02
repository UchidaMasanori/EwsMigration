using Ews.Domain.Common;

namespace Ews.Domain.Circuits;

/// <summary>
/// 回路内容記述ファイルの1行(行番号単位の回路記述)。
///
/// 【C原典】
///   - 構造体: struct FYDF805            (toku/include/common/fydf805.h)
///   - ファイルID: FYDF805 / 編成: EWS-ISAM
///   - 「回路内容記述エリア」は改訂&lt;1&gt;で 100→200 文字へ拡張(KAIROARLEN=200)。
///
/// 1物件は (新規登録依頼明細番号 + 行番号) をキーとする複数行で構成され、
/// 回路解析(<c>Fysk10_Main</c>)の主入力となる。
/// </summary>
public sealed class CircuitDescriptionLine : IIsamRecord
{
    /// <summary>【C原典】KAIROARLEN=200 を含む struct FYDF805 のレコード長。</summary>
    public static int RecordLength => 200 + 30; // 概算(回路記述200 + キー/付帯領域)

    /// <summary>回路内容記述エリアの最大長。【C原典】#define KAIROARLEN 200。</summary>
    public const int CircuitTextLength = 200;

    // ---- キー情報(struct key) ----

    /// <summary>新規登録依頼番号。【C原典】key.im.airaino[7]。</summary>
    public string RequestNumber { get; set; } = string.Empty;

    /// <summary>新規登録明細番号。【C原典】key.im.ameisano[2]。</summary>
    public string ItemNumber { get; set; } = string.Empty;

    /// <summary>行番号。【C原典】key.gyono[3] (数値属性 "9")。</summary>
    public int LineNumber { get; set; }

    /// <summary>行種。【C原典】gyosyu[5]。</summary>
    public string LineType { get; set; } = string.Empty;

    /// <summary>回路内容記述エリア。【C原典】kairoar[KAIROARLEN]。</summary>
    public string CircuitText { get; set; } = string.Empty;

    /// <summary>オリジナル行番号(コンポで使用)。【C原典】orgno[3]。</summary>
    public int OriginalLineNumber { get; set; }

    /// <summary>コマンド(仕様入力で使用)。【C原典】cmd。</summary>
    public char Command { get; set; } = ' ';

    /// <summary>未展開領域を含むレコード全体のバイト列。</summary>
    public byte[] RawRecord { get; set; } = [];

    // バイトオフセット(struct FYDF805 先頭から算出)
    private const int OffsetRequestNumber = 0;   // key.im.airaino[7]
    private const int OffsetItemNumber = 7;      // key.im.ameisano[2]
    private const int OffsetLineNumber = 9;      // key.gyono[3]
    private const int OffsetLineType = 12;       // gyosyu[5]
    private const int OffsetCircuitText = 17;    // kairoar[200]
    private const int OffsetOriginalLineNumber = 217; // orgno[3]
    // kijno[3](220)
    private const int OffsetCommand = 223;       // cmd

    /// <summary>
    /// 固定長 Shift-JIS レコードからドメインモデルを生成する。
    /// 【C原典】回路内容記述読込(FyIsamRead/FyIsamNextR で取得した struct FYDF805)。
    /// </summary>
    public static CircuitDescriptionLine FromFixedRecord(ReadOnlySpan<byte> record)
    {
        return new CircuitDescriptionLine
        {
            RequestNumber = FixedFieldCodec.ReadText(record, OffsetRequestNumber, 7),
            ItemNumber = FixedFieldCodec.ReadText(record, OffsetItemNumber, 2),
            LineNumber = (int)FixedFieldCodec.ReadNumber(record, OffsetLineNumber, 3),
            LineType = FixedFieldCodec.ReadText(record, OffsetLineType, 5),
            CircuitText = FixedFieldCodec.ReadText(record, OffsetCircuitText, CircuitTextLength),
            OriginalLineNumber = (int)FixedFieldCodec.ReadNumber(record, OffsetOriginalLineNumber, 3),
            Command = (char)record[OffsetCommand],
            RawRecord = record.ToArray(),
        };
    }
}
