namespace Ews.Domain.Validation;

/// <summary>
/// 設計エンジンのゴールデンマスタ検証で比較する 5 出力ファイル種別。
/// 【C原典】<c>Fysk07_File_Write_ALL</c>(toku/sekkei/src/Fysk07.c)が書き出す固定長ファイル。
/// </summary>
public enum GoldenMasterFileKind
{
    /// <summary>主回路。【C原典】FNAME_SY = FYDF806(struct FYDF806)。</summary>
    MainCircuit,

    /// <summary>複合回路。【C原典】FNAME_FU = FYDF807(struct FYDF807)。</summary>
    Composite,

    /// <summary>制御回路。【C原典】FNAME_SE = FYDF808(struct FYDF808)。</summary>
    Control,

    /// <summary>論理図面回路。【C原典】FNAME_RO = FYDF809(struct FYDF809)。</summary>
    Logic,

    /// <summary>構成機器。【C原典】FNAME_KO = FYDF811(struct FYDF811)。</summary>
    ComponentEquipment,
}

/// <summary>
/// ゴールデンマスタ 5 ファイルの固定長レイアウト定義。
///
/// 各レコード長は C 構造体 <c>sizeof(struct FYDFxxx)</c>(全フィールドが CHAR = 1 バイトで
/// パディング無し)に一致する。末尾には登録情報 <c>struct datajg</c>(36 バイト)が配置され、
/// 実行毎に変化する(termid/date/time)ためバイト比較前にマスクする。
///
/// 【C原典】
///   - fydf806.h: RL=1219(key 12 + syukairo + datajg 36)
///   - fydf807.h: RL=1219(key 15 + fukugo + datajg 36)
///   - fydf808.h: RL=1920(key 18 + union{seijg…} + datajg 36)
///   - fydf809.h: RL=304 (key 20 + ronzu + datajg 36)
///   - fydf811.h: RL=350 (key 19 + kosekiki + datajg 36)
/// </summary>
public static class GoldenMasterLayout
{
    /// <summary>登録情報 <c>struct datajg</c> のバイト長。【C原典】fycommon.h(4+8+6+4+8+6)。</summary>
    public const int DatajgLength = 36;

    /// <summary>指定ファイル種別のレコード長(バイト)。【C原典】sizeof(struct FYDFxxx)。</summary>
    public static int RecordLength(GoldenMasterFileKind kind) => kind switch
    {
        GoldenMasterFileKind.MainCircuit => 1219,
        GoldenMasterFileKind.Composite => 1219,
        GoldenMasterFileKind.Control => 1920,
        GoldenMasterFileKind.Logic => 304,
        GoldenMasterFileKind.ComponentEquipment => 350,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知のゴールデンマスタファイル種別です。"),
    };

    /// <summary>ファイル ID(FYDF806～FYDF811)。【C原典】FNAME_SY/FU/SE/RO/KO(fyrt808.h)。</summary>
    public static string FileId(GoldenMasterFileKind kind) => kind switch
    {
        GoldenMasterFileKind.MainCircuit => "FYDF806",
        GoldenMasterFileKind.Composite => "FYDF807",
        GoldenMasterFileKind.Control => "FYDF808",
        GoldenMasterFileKind.Logic => "FYDF809",
        GoldenMasterFileKind.ComponentEquipment => "FYDF811",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知のゴールデンマスタファイル種別です。"),
    };

    /// <summary>
    /// レコード内での datajg(登録情報)の開始オフセット。全 5 ファイルとも datajg は
    /// レコード末尾に位置するため <c>RecordLength - DatajgLength</c>。
    /// </summary>
    public static int DatajgOffset(GoldenMasterFileKind kind) => RecordLength(kind) - DatajgLength;
}

/// <summary>
/// レコード内の 1 バイト差分。
/// </summary>
/// <param name="RecordIndex">差分が発生したレコード番号(0 始まり)。</param>
/// <param name="ByteOffset">レコード先頭からのバイトオフセット。</param>
/// <param name="Expected">基準(C 版)側のバイト値。</param>
/// <param name="Actual">検証(C# 版)側のバイト値。</param>
public sealed record GoldenMasterByteDiff(int RecordIndex, int ByteOffset, byte Expected, byte Actual);

/// <summary>
/// 1 レコードの差分サマリ。
/// </summary>
/// <param name="RecordIndex">レコード番号(0 始まり)。</param>
/// <param name="FirstDiff">最初に検出した差分バイト。</param>
/// <param name="DifferingByteCount">当該レコード内で相違したバイト総数(マスク領域除く)。</param>
public sealed record GoldenMasterRecordDiff(int RecordIndex, GoldenMasterByteDiff FirstDiff, int DifferingByteCount);

/// <summary>
/// ゴールデンマスタ 1 ファイルの比較結果。
/// </summary>
public sealed record GoldenMasterComparisonResult
{
    /// <summary>比較対象ファイル種別。</summary>
    public required GoldenMasterFileKind Kind { get; init; }

    /// <summary>基準(C 版)側のレコード件数。</summary>
    public required int ExpectedRecordCount { get; init; }

    /// <summary>検証(C# 版)側のレコード件数。</summary>
    public required int ActualRecordCount { get; init; }

    /// <summary>差分が検出されたレコードの一覧(マスク領域を除く)。</summary>
    public required IReadOnlyList<GoldenMasterRecordDiff> RecordDiffs { get; init; }

    /// <summary>
    /// 件数一致かつ全レコードでバイト差分が無い場合に true。
    /// </summary>
    public bool IsMatch => ExpectedRecordCount == ActualRecordCount && RecordDiffs.Count == 0;
}

/// <summary>
/// ゴールデンマスタ 5 ファイル比較のコア(固定長レコードのバイト比較エンジン)。
///
/// 【方式】MIGRATION_PLAN §7。C 版 <c>Fysk07_File_Write_ALL</c> が出力した固定長ファイルと、
/// C# パイプラインの出力ライタが生成した固定長ファイルを、レコード単位・バイト単位で比較する。
/// 登録情報 <c>datajg</c>(実行毎に変化)はマスクして比較対象から除外する。
/// </summary>
public static class GoldenMasterComparer
{
    /// <summary>
    /// 指定種別の 2 つの固定長ファイルバイト列を比較する。
    /// </summary>
    /// <param name="kind">ファイル種別(レコード長・datajg 位置を決定)。</param>
    /// <param name="expected">基準(C 版)側の全レコードバイト列。</param>
    /// <param name="actual">検証(C# 版)側の全レコードバイト列。</param>
    /// <param name="maskDatajg">true の場合、各レコード末尾の datajg(36 バイト)を比較対象から除外する。</param>
    /// <exception cref="ArgumentException">いずれかの長さがレコード長の整数倍でない場合。</exception>
    public static GoldenMasterComparisonResult Compare(
        GoldenMasterFileKind kind,
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual,
        bool maskDatajg = true)
    {
        int recordLength = GoldenMasterLayout.RecordLength(kind);

        if (expected.Length % recordLength != 0)
        {
            throw new ArgumentException(
                $"{GoldenMasterLayout.FileId(kind)} の基準データ長 {expected.Length} がレコード長 {recordLength} の整数倍ではありません。",
                nameof(expected));
        }

        if (actual.Length % recordLength != 0)
        {
            throw new ArgumentException(
                $"{GoldenMasterLayout.FileId(kind)} の検証データ長 {actual.Length} がレコード長 {recordLength} の整数倍ではありません。",
                nameof(actual));
        }

        int expectedCount = expected.Length / recordLength;
        int actualCount = actual.Length / recordLength;

        // datajg マスク範囲(全 5 ファイルとも末尾 36 バイト)。
        int compareEnd = maskDatajg ? recordLength - GoldenMasterLayout.DatajgLength : recordLength;

        var diffs = new List<GoldenMasterRecordDiff>();
        int common = Math.Min(expectedCount, actualCount);

        for (int r = 0; r < common; r++)
        {
            int baseOffset = r * recordLength;
            ReadOnlySpan<byte> e = expected.Slice(baseOffset, recordLength);
            ReadOnlySpan<byte> a = actual.Slice(baseOffset, recordLength);

            GoldenMasterByteDiff? first = null;
            int differing = 0;

            for (int i = 0; i < compareEnd; i++)
            {
                if (e[i] != a[i])
                {
                    first ??= new GoldenMasterByteDiff(r, i, e[i], a[i]);
                    differing++;
                }
            }

            if (first is not null)
            {
                diffs.Add(new GoldenMasterRecordDiff(r, first, differing));
            }
        }

        return new GoldenMasterComparisonResult
        {
            Kind = kind,
            ExpectedRecordCount = expectedCount,
            ActualRecordCount = actualCount,
            RecordDiffs = diffs,
        };
    }
}
