using System.Text;
using Ews.Data.Seeding;
using Ews.Domain.Common;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 機器マスター(FYDM805)固定長エクスポートのパース検証。
/// 【C原典】fydm805.h(ﾚｺｰﾄﾞ長 579)。hostdt/FYDM805.data は 1 レコードを
/// Shift-JIS 固定長で出力し LF 区切りにしたもの。
/// </summary>
public sealed class EquipmentMasterLoaderTests
{
    private const int ExportRecordBytes = 600; // エクスポートのレコード幅(579 を 600 へパディング)。

    /// <summary>指定フィールドを埋めた 600 バイト固定長レコードを生成する。</summary>
    private static byte[] BuildRecord(
        string yoyaku, string mkcd, string ptype, string teikkey,
        string hinban, string hinmei, string pstring)
    {
        var record = new byte[ExportRecordBytes];
        record.AsSpan().Fill((byte)' ');
        FixedFieldCodec.WriteText(record, 0, 8, yoyaku);
        FixedFieldCodec.WriteText(record, 8, 3, mkcd);
        FixedFieldCodec.WriteText(record, 11, 49, ptype);
        FixedFieldCodec.WriteText(record, 60, 80, teikkey);
        FixedFieldCodec.WriteText(record, 140, 15, hinban);
        FixedFieldCodec.WriteText(record, 155, 25, hinmei);
        FixedFieldCodec.WriteText(record, 180, 64, pstring);
        return record;
    }

    private static string WriteTempData(params byte[][] records)
    {
        string path = Path.Combine(Path.GetTempPath(), $"fydm805_{Guid.NewGuid():N}.data");
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        for (int i = 0; i < records.Length; i++)
        {
            stream.Write(records[i], 0, records[i].Length);
            stream.WriteByte((byte)'\n');
        }

        return path;
    }

    [Fact]
    public void ParseEquipmentMaster_固定長レコードから主要フィールドを抽出する()
    {
        byte[] r0 = BuildRecord("2COSU", "M", "KM", "TESTKEY", "24062-040", "BE-C06", "1.5A250VAC100V");
        byte[] r1 = BuildRecord("AM", "M", "YS", "KEY2", "23007-705-3-B", "YS-12NAA-B", "10/5A");
        string path = WriteTempData(r0, r1);

        try
        {
            IReadOnlyList<EquipmentMaster> rows = EquipmentMasterLoader.ParseEquipmentMaster(path);

            Assert.Equal(2, rows.Count);
            Assert.Equal("2COSU", rows[0].ReservedWord);
            Assert.Equal("M", rows[0].MakerCode);
            Assert.Equal("24062-040", rows[0].PartNumber);
            Assert.Equal("BE-C06", rows[0].PartName);
            Assert.Equal("1.5A250VAC100V", rows[0].ElectricalParameters);
            Assert.Equal("23007-705-3-B", rows[1].PartNumber);
            Assert.Equal("YS-12NAA-B", rows[1].PartName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseEquipmentMaster_244バイト未満の末尾断片を読み飛ばす()
    {
        byte[] full = BuildRecord("2COSU", "M", "KM", "KEY", "24062-040", "BE-C06", "1.5A");
        string path = WriteTempData(full);

        // エクスポート末尾に現れる不完全な短行(< 244 バイト)を追記する。
        byte[] fragment = Encoding.ASCII.GetBytes(new string('X', 138));
        File.AppendAllText(path, string.Empty);
        using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write))
        {
            stream.Write(fragment, 0, fragment.Length);
        }

        try
        {
            IReadOnlyList<EquipmentMaster> rows = EquipmentMasterLoader.ParseEquipmentMaster(path);

            Assert.Single(rows);
            Assert.Equal("24062-040", rows[0].PartNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
