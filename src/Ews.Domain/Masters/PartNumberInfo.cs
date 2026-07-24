using Ews.Domain.Common;

namespace Ews.Domain.Masters;

/// <summary>
/// 品番情報(hbninf / 通称 hbntb 品番テーブル)。
///
/// 【C原典】
///   - 構造体: struct hbninf            (toku/include/cmpchg/cmplogtr.h)
///   - ファイル: 案件ごとの生バイナリ 1 レコード <c>&lt;WORK&gt;/&lt;依頼明細番号&gt;.clh</c>
///     (compo の品番ロジックが <c>FyCpHbFilePut(iraino, "clh", sizeof(struct hbninf), …)</c> で出力)。
///   - レコード長: <b>908 バイト</b>(= sizeof(struct hbninf), HBN_SIZE=100)。
///
/// FYDF801 等のテキスト固定長と異なり、<b>生の C 構造体ダンプ</b>である
/// (<c>SHORT</c> は 2 バイト・AIX big-endian、CHAR 配列の後に padding が入り得る)。
/// 本移行では回路解析(Fyss12)の SEP 追加判定が参照する CHAR フィールドのみを保持する
/// (これらは endian 非依存)。SHORT のボックス寸法等は SEP では未使用のため未モデル化
/// (必要になれば big-endian を明示して追加する)。キー(依頼明細番号)はファイル名側にあり
/// レコード内には含まれない。
///
/// 参照箇所:
///   - <c>PropChkHbnHB300</c>(Fyss12.c) … <see cref="InputPartNumber"/>(inputhb)
///   - <c>PropChkSEPBox</c>(Fyss12.c)   … <see cref="GeneratedBoxPartNumber"/>(crboxtmp) /
///                                         <see cref="BoxType"/>(boxtyp)
/// </summary>
public sealed class PartNumberInfo : IIsamRecord
{
    /// <summary>【C原典】sizeof(struct hbninf)。cmplogtr.h(HBN_SIZE=100)。</summary>
    public static int RecordLength => 908;

    /// <summary>入力品番(200 文字 + \0)。【C原典】inputhb[HBN_SIZE*2+1]。PropChkHbnHB300 が参照。</summary>
    public string InputPartNumber { get; set; } = string.Empty;

    /// <summary>電力管内区分。【C原典】dekankbn[3](fydf801k.h と同区分)。</summary>
    public string PowerRegionKind { get; set; } = string.Empty;

    /// <summary>製作仕様区分。【C原典】sshiykbn[3]。'01':河村標準 等(FYDF801 と整合)。</summary>
    public string ManufacturingSpecKind { get; set; } = string.Empty;

    /// <summary>適用 BOX タイプ(全体, 2 文字 + \0)。【C原典】boxtyp[16]。PropChkSEPBox が参照。</summary>
    public string BoxType { get; set; } = string.Empty;

    /// <summary>生成 BOX 品番(コンポ以外, 12 文字 + \0)。【C原典】crboxtmp[32]。PropChkSEPBox が優先参照。</summary>
    public string GeneratedBoxPartNumber { get; set; } = string.Empty;

    // ---- バイトオフセット(struct hbninf, 実データ検証済) ----
    // inputhb[201]@0 → pnlsiti[9]@201 → SHORT paint/install/setspec … syshbbef[151]@256
    //  → dekankbn[3]@407 → sshiykbn[3]@410 → … boxtyp[16]@842 → … crboxtmp[32]@864。
    private const int OffsetInputPartNumber = 0;    // inputhb[201]
    private const int OffsetPowerRegionKind = 407;  // dekankbn[3]
    private const int OffsetManufacturingSpecKind = 410; // sshiykbn[3]
    private const int OffsetBoxType = 842;          // boxtyp[16]
    private const int OffsetGeneratedBoxPartNumber = 864; // crboxtmp[32]

    /// <summary>
    /// 生バイナリ hbninf レコード(908 バイト)からドメインモデルを生成する。
    /// 各 CHAR フィールドは C 文字列(先頭 \0 まで)として読む。
    /// 【C原典】FyCpHbHbnInfFileR(clfilerw.c) で読み込んだ struct hbninf。
    /// </summary>
    public static PartNumberInfo FromFixedRecord(ReadOnlySpan<byte> record)
    {
        return new PartNumberInfo
        {
            InputPartNumber = ReadCString(record, OffsetInputPartNumber, 201),
            PowerRegionKind = ReadCString(record, OffsetPowerRegionKind, 3),
            ManufacturingSpecKind = ReadCString(record, OffsetManufacturingSpecKind, 3),
            BoxType = ReadCString(record, OffsetBoxType, 16),
            GeneratedBoxPartNumber = ReadCString(record, OffsetGeneratedBoxPartNumber, 32),
        };
    }

    /// <summary>
    /// C 文字列(NUL 終端)フィールドを読む。先頭 \0 までを CP932 でデコードし、
    /// 末尾の空白を除去する。【C原典】CHAR 配列 + strlen/strncpy の C 文字列扱い。
    /// </summary>
    private static string ReadCString(ReadOnlySpan<byte> record, int offset, int maxLength)
    {
        ReadOnlySpan<byte> slice = record.Slice(offset, maxLength);
        int nul = slice.IndexOf((byte)0);
        if (nul >= 0)
        {
            slice = slice[..nul];
        }

        return FixedFieldCodec.ShiftJis.GetString(slice).TrimEnd(' ', '\u3000');
    }
}
