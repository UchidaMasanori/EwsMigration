using System.Text;
using Ews.Domain.Common;

namespace Ews.Domain.Analysis;

/// <summary>
/// <see cref="AttachedParameters"/>(=C <c>struct fparmg</c>)を FYDF806 の固定長バイト列へ
/// 相互変換するコーデック。【C原典】toku/include/common/fycommon.h:77 の fparmg(157 バイト)。
///
/// FYDF806(RL=1219)の中で ep[2](eparmg 253 バイト)の後、offset +873(=ep[0]@114 + 253×3)へ配置される。
/// フィールド並び・幅・既定は fparmg 構造体定義に一致する(合計 <see cref="RecordLength"/> バイト)。
/// エンコードは Shift-JIS(CP932)。負荷名称/コメント/品名は全角(2 バイト)を含み得るため、
/// 各テキストフィールドはバイト幅で管理する(【C原典】CHAR 配列)。
///
/// 各フィールドは「論理値(mainfile_set の memcpy/strncpy が組み立てる生値)」を CP932 バイト化し、
/// 先頭から値をコピー、残りを既定バイト(<c>Main_Area_Clear</c> の '0' もしくは ' ')で埋めることで、
/// C の memcpy(dst, buff, strlen(buff))(残りは Main_Area_Clear の既定)を再現する。
/// </summary>
public static class FparmgCodec
{
    /// <summary>fparmg 1 レコード長(バイト)。【C原典】sizeof(struct fparmg)。</summary>
    public const int RecordLength = 157;

    private static readonly Encoding Cp932 = FixedFieldCodec.ShiftJis;

    /// <summary>
    /// <paramref name="fp"/> を fparmg 構造の 157 バイト固定長レコードへ直列化する。
    /// </summary>
    public static byte[] Serialize(AttachedParameters fp)
    {
        ArgumentNullException.ThrowIfNull(fp);

        byte[] buf = new byte[RecordLength];
        int pos = 0;

        // 論理値を CP932 バイト化 → 先頭から width バイトへ値をコピー → 残りを fill で埋める。
        void PutField(string value, int width, byte fill)
        {
            byte[] b = Cp932.GetBytes(value ?? string.Empty);
            for (int i = 0; i < width; i++)
            {
                buf[pos + i] = i < b.Length ? b[i] : fill;
            }
            pos += width;
        }

        void PutChar(char value)
        {
            buf[pos] = (byte)value;
            pos += 1;
        }

        PutField(fp.LoadKind, 2, (byte)' ');            // fpalw1[2]
        PutField(fp.LoadCapacity, 7, (byte)'0');        // fpalw2[7]
        PutChar(fp.LoadUnitKind);                       // fpalwkbn
        PutField(fp.LoadVoltage[0], 3, (byte)'0');      // fpalv[0][3]
        PutField(fp.LoadVoltage[1], 3, (byte)'0');      // fpalv[1][3]
        PutField(fp.LoadName[0], 20, (byte)' ');        // fpaln[0][20]
        PutField(fp.LoadName[1], 20, (byte)' ');        // fpaln[1][20]
        PutField(fp.Comment, 20, (byte)' ');            // fpacm1[20]
        PutField(fp.LineTypeComment, 20, (byte)' ');    // fpacm2[20]
        PutField(fp.CommentGroupNumber, 3, (byte)'0');  // fpacglno[3]
        PutField(fp.ItemName, 25, (byte)' ');           // fpaitpt[25]
        PutChar(fp.SpFutureMountKind);                  // spkvn
        PutField(fp.DimensionHeight, 4, (byte)'0');     // fpah[4]
        PutField(fp.DimensionWidth, 4, (byte)'0');      // fpaw[4]
        PutField(fp.DimensionDepth, 4, (byte)'0');      // fpad[4]
        PutField(fp.DimensionGroupNumber, 3, (byte)'0');// fpasglno[3]
        PutChar(fp.ExternalMountKind);                  // fpag
        PutChar(fp.SealKind);                           // fpahu
        PutChar(fp.SuppliedKind);                       // fpas
        PutChar(fp.PartitionKind);                      // fpak
        PutChar(fp.MeterSealKind);                      // fpamh
        PutField(fp.ControlPowerNumber, 2, (byte)' ');  // fpac[2]
        PutField(fp.MakerCode, 3, (byte)' ');           // fpamk[3]
        PutField(fp.PowerVoltage, 6, (byte)' ');        // fpaup[6]
        PutChar(fp.DoorMountKind);                      // tikbn

        if (pos != RecordLength)
        {
            throw new InvalidOperationException($"fparmg レイアウト不整合: {pos} != {RecordLength}");
        }
        return buf;
    }

    /// <summary>
    /// fparmg 構造の 157 バイト固定長レコードから <see cref="AttachedParameters"/> を復元する。
    /// 各フィールドは幅ぴったりのバイト内容をそのまま保持する(<see cref="Serialize"/> と対称)。
    /// </summary>
    public static AttachedParameters Deserialize(ReadOnlySpan<byte> record)
    {
        if (record.Length < RecordLength)
        {
            throw new ArgumentException($"fparmg レコードは {RecordLength} バイト必要(実際: {record.Length})。", nameof(record));
        }

        AttachedParameters fp = new();
        int pos = 0;

        fp.LoadKind = Cp932.GetString(record.Slice(pos, 2)); pos += 2;
        fp.LoadCapacity = Cp932.GetString(record.Slice(pos, 7)); pos += 7;
        fp.LoadUnitKind = (char)record[pos]; pos += 1;
        fp.LoadVoltage[0] = Cp932.GetString(record.Slice(pos, 3)); pos += 3;
        fp.LoadVoltage[1] = Cp932.GetString(record.Slice(pos, 3)); pos += 3;
        fp.LoadName[0] = Cp932.GetString(record.Slice(pos, 20)); pos += 20;
        fp.LoadName[1] = Cp932.GetString(record.Slice(pos, 20)); pos += 20;
        fp.Comment = Cp932.GetString(record.Slice(pos, 20)); pos += 20;
        fp.LineTypeComment = Cp932.GetString(record.Slice(pos, 20)); pos += 20;
        fp.CommentGroupNumber = Cp932.GetString(record.Slice(pos, 3)); pos += 3;
        fp.ItemName = Cp932.GetString(record.Slice(pos, 25)); pos += 25;
        fp.SpFutureMountKind = (char)record[pos]; pos += 1;
        fp.DimensionHeight = Cp932.GetString(record.Slice(pos, 4)); pos += 4;
        fp.DimensionWidth = Cp932.GetString(record.Slice(pos, 4)); pos += 4;
        fp.DimensionDepth = Cp932.GetString(record.Slice(pos, 4)); pos += 4;
        fp.DimensionGroupNumber = Cp932.GetString(record.Slice(pos, 3)); pos += 3;
        fp.ExternalMountKind = (char)record[pos]; pos += 1;
        fp.SealKind = (char)record[pos]; pos += 1;
        fp.SuppliedKind = (char)record[pos]; pos += 1;
        fp.PartitionKind = (char)record[pos]; pos += 1;
        fp.MeterSealKind = (char)record[pos]; pos += 1;
        fp.ControlPowerNumber = Cp932.GetString(record.Slice(pos, 2)); pos += 2;
        fp.MakerCode = Cp932.GetString(record.Slice(pos, 3)); pos += 3;
        fp.PowerVoltage = Cp932.GetString(record.Slice(pos, 6)); pos += 6;
        fp.DoorMountKind = (char)record[pos];

        return fp;
    }
}
