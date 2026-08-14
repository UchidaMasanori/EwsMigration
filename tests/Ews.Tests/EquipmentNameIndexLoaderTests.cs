using Ews.Analysis;
using Ews.Data.Seeding;
using Ews.Domain.Common;
using Ews.Domain.Masters;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// 機器マスター品名索引(FYDF817)ローダー検証。
///
/// 【C原典】struct FYDF817 (toku/include/common/fydf817.h, レコード長 184)、
/// Fysk01_Kikisearch_PT/PT2 の品名索引読み。
///
/// 合成レコードによる決定的なオフセット検証と、実データ hostdt/FYDF817.data による
/// 整合検証(未配置環境ではスキップ)の 2 本立て。
/// </summary>
public sealed class EquipmentNameIndexLoaderTests
{
    private readonly ITestOutputHelper _output;

    public EquipmentNameIndexLoaderTests(ITestOutputHelper output) => _output = output;

    private const int RecordLength = 184;
    private const int OffsetProductName = 0;    // key.hinmei[25]
    private const int OffsetDataNo = 25;        // key.datano[4]
    private const int OffsetReservedWord = 29;  // pkey.yoyaku[8]
    private const int OffsetMakerCode = 37;     // pkey.mkcd[3]
    private const int OffsetRatingKey = 89;     // pkey.teikkey[80]
    private const int OffsetPartNumber = 169;   // hinban[15]

    [Fact]
    public void 合成レコード_主要フィールドを検証済オフセットで読む()
    {
        byte[] r1 = BuildRecord("BBR910", "0001", "PT", "M  ", "0150000250000000", "22130-530");

        string path = WriteRecords(r1);
        try
        {
            IReadOnlyList<EquipmentNameIndex> rows = EquipmentNameIndexLoader.ParseEquipmentNameIndex(path);

            EquipmentNameIndex only = Assert.Single(rows);
            Assert.Equal("BBR910", only.ProductName);
            Assert.Equal("0001", only.DataNo);
            Assert.Equal("PT", only.ReservedWord);
            Assert.Equal("M", only.MakerCode);
            Assert.Equal("0150000250000000", only.RatingKey);
            Assert.Equal("22130-530", only.PartNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ローダー_LF区切りの複数レコードを解析し断片を無視する()
    {
        byte[] r1 = BuildRecord("203Y3-3FD-A1", "0001", "MCDT", "AI ", "", "");
        byte[] r2 = BuildRecord("ZT80B", "0001", "ZCT", "M  ", "", "22130-530");

        using var buffer = new MemoryStream();
        buffer.Write(r1);
        buffer.WriteByte((byte)'\n');
        buffer.Write(r2);
        buffer.WriteByte((byte)'\n');
        buffer.Write("garbage"u8.ToArray());

        string path = Path.Combine(Path.GetTempPath(), $"fydf817_{Guid.NewGuid():N}.data");
        File.WriteAllBytes(path, buffer.ToArray());
        try
        {
            IReadOnlyList<EquipmentNameIndex> rows = EquipmentNameIndexLoader.ParseEquipmentNameIndex(path);

            Assert.Equal(2, rows.Count);
            Assert.Equal("203Y3-3FD-A1", rows[0].ProductName);
            Assert.Equal("MCDT", rows[0].ReservedWord);
            Assert.Equal("ZT80B", rows[1].ProductName);
            Assert.Equal("ZCT", rows[1].ReservedWord);
            Assert.Equal("22130-530", rows[1].PartNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 実FYDF817_全レコードでオフセット不変条件を満たす()
    {
        string? path = FindHostFile("FYDF817.data");
        if (path is null)
        {
            _output.WriteLine("hostdt/FYDF817.data 未配置のため検証をスキップします。");
            return;
        }

        IReadOnlyList<EquipmentNameIndex> rows = EquipmentNameIndexLoader.ParseEquipmentNameIndex(path);

        Assert.True(rows.Count > 10000, $"レコード数が想定より少ない: {rows.Count}");

        foreach (EquipmentNameIndex r in rows)
        {
            // 品名は 1..25 文字。
            Assert.True(r.ProductName.Length is > 0 and <= 25, $"品名が不正: [{r.ProductName}]");
            // データ追番は 4 桁の数字。
            Assert.Matches("^[0-9]{4}$", r.DataNo);
            // 予約語は 1..8 文字。
            Assert.True(r.ReservedWord.Length is > 0 and <= 8, $"予約語が不正: [{r.ReservedWord}] (品名=[{r.ProductName}])");
        }

        // キー(品名+追番)は ISAM 主キーとして一意。
        int distinct = rows.Select(r => $"{r.ProductName}|{r.DataNo}").Distinct().Count();
        Assert.Equal(rows.Count, distinct);

        // 予約語 PT の索引が存在し、PT2 相当の検索でその追番を取得できる。
        EquipmentNameIndexSearchResult pt = EquipmentNameIndexSearch.SearchPt("BBR910", rows);
        Assert.Equal(EquipmentNameIndexSearch.DataFound, pt.Status);
        Assert.Equal("PT", pt.Record!.ReservedWord);

        _output.WriteLine($"total={rows.Count} distinct={distinct}");
    }

    private static string WriteRecords(params byte[][] records)
    {
        using var buffer = new MemoryStream();
        foreach (byte[] record in records)
        {
            buffer.Write(record);
            buffer.WriteByte((byte)'\n');
        }

        string path = Path.Combine(Path.GetTempPath(), $"fydf817_{Guid.NewGuid():N}.data");
        File.WriteAllBytes(path, buffer.ToArray());
        return path;
    }

    private static byte[] BuildRecord(
        string productName, string dataNo, string reservedWord, string makerCode, string ratingKey, string partNumber)
    {
        var record = new byte[RecordLength];
        record.AsSpan().Fill((byte)' ');

        PutText(record, OffsetProductName, 25, productName);
        PutText(record, OffsetDataNo, 4, dataNo);
        PutText(record, OffsetReservedWord, 8, reservedWord);
        PutText(record, OffsetMakerCode, 3, makerCode);
        PutText(record, OffsetRatingKey, 80, ratingKey);
        PutText(record, OffsetPartNumber, 15, partNumber);

        return record;
    }

    private static void PutText(byte[] record, int offset, int width, string value)
    {
        byte[] bytes = FixedFieldCodec.ShiftJis.GetBytes(value);
        int count = Math.Min(bytes.Length, width);
        bytes.AsSpan(0, count).CopyTo(record.AsSpan(offset, width));
    }

    /// <summary>
    /// テスト実行ディレクトリから上位へ辿り、hostdt 配下(兄弟 EWS/hostdt も含む)の
    /// 指定データファイルを探す。見つからなければ null(検証スキップ)。
    /// </summary>
    private static string? FindHostFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string direct = Path.Combine(dir.FullName, "hostdt", fileName);
            if (File.Exists(direct))
            {
                return direct;
            }

            string sibling = Path.Combine(dir.FullName, "EWS", "hostdt", fileName);
            if (File.Exists(sibling))
            {
                return sibling;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
