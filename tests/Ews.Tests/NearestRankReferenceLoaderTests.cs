using Ews.Data.Seeding;
using Ews.Domain.Common;
using Ews.Domain.Masters;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// 直近上下位参照ファイル(FYDF812)ローダー検証。
///
/// 【C原典】struct FYDF812 (toku/include/common/fydf812.h, レコード長 300)、
/// Fysk01_Chokkin_Read_Check(_ALL/_TMS) の直近上下位ファイル走査。
///
/// 合成レコードによる決定的なオフセット検証と、実データ hostdt/FYDF812.data による
/// 整合検証(未配置環境ではスキップ)の 2 本立て。
/// </summary>
public sealed class NearestRankReferenceLoaderTests
{
    private readonly ITestOutputHelper _output;

    public NearestRankReferenceLoaderTests(ITestOutputHelper output) => _output = output;

    private const int RecordLength = 300;
    private const int OffsetReservedWord = 0;
    private const int OffsetMakerCode = 8;
    private const int OffsetParameterTypes = 11;
    private const int ParameterTypeSize = 7;
    private const int OffsetMainPowerAcDc = 60;
    private const int OffsetControlPowerAcDc = 61;
    private const int OffsetRatingKey = 62;
    private const int OffsetDataSequence = 112;
    private const int OffsetHandleLockKind = 175;
    private const int OffsetEquipmentMasterRatingKey = 176;
    private const int OffsetProductName = 256;
    private const int OffsetControlVoltageRangeFrom = 281;
    private const int OffsetControlVoltageRangeTo = 284;
    private const int OffsetSharedMainPowerAcDc = 116;
    private const int OffsetSharedControlPowerAcDc = 117;
    private const int OffsetSensitivityCurrents = 118;
    private const int OffsetPrimaryVoltage = 134;
    private const int OffsetSecondaryVoltage = 145;
    private const int OffsetControlVoltage = 160;

    /// <summary>
    /// 合成した 300 バイトレコードから KEY 部と外側フィールドを正しく抽出できる。
    /// </summary>
    [Fact]
    public void 合成レコード_全フィールドを検証済オフセットで読む()
    {
        byte[] record = BuildRecord(
            reservedWord: "2COSU",
            makerCode: "M  ",
            parameterTypes: ["KM", "ET", "ST", "", "", "", ""],
            mainPowerAcDc: 'A',
            controlPowerAcDc: 'A',
            ratingKey: "100100012500150",
            dataSequence: "0001",
            handleLock: ' ',
            equipmentMasterRatingKey: "0150000250000000 000100 000000 00001",
            productName: "BE-C06",
            vcFrom: "085",
            vcTo: "110");

        NearestRankReference r = NearestRankReference.FromFixedRecord(record);

        Assert.Equal("2COSU", r.ReservedWord);
        Assert.Equal("M  ", r.MakerCode);
        Assert.Equal(7, r.ParameterTypes.Count);
        Assert.Equal(new[] { "KM", "ET", "ST", "", "", "", "" }, r.ParameterTypes);
        Assert.Equal('A', r.MainPowerAcDc);
        Assert.Equal('A', r.ControlPowerAcDc);
        Assert.Equal("100100012500150", r.RatingKey);
        Assert.Equal("0001", r.DataSequence);
        Assert.Equal(' ', r.HandleLockKind);
        Assert.Equal("0150000250000000 000100 000000 00001", r.EquipmentMasterRatingKey);
        Assert.Equal("BE-C06", r.ProductName);
        Assert.Equal("085", r.ControlVoltageRangeFrom);
        Assert.Equal("110", r.ControlVoltageRangeTo);
    }

    /// <summary>
    /// 共用情報部(jg)を検証済オフセットで読み取れる。
    /// 【C原典】struct kyoyojg (ksadkbn/kcadkbn/km/kv1/kv2/kvc)。
    /// </summary>
    [Fact]
    public void 合成レコード_共用情報部を検証済オフセットで読む()
    {
        var record = new byte[RecordLength];
        record.AsSpan().Fill((byte)' ');
        PutText(record, OffsetReservedWord, 8, "ELB");
        PutText(record, OffsetDataSequence, 4, "0001");

        record[OffsetSharedMainPowerAcDc] = (byte)'A';
        record[OffsetSharedControlPowerAcDc] = (byte)'D';
        // km.kyomad[4][4]
        PutText(record, OffsetSensitivityCurrents + 0, 4, "0200");
        PutText(record, OffsetSensitivityCurrents + 4, 4, "0030");
        PutText(record, OffsetSensitivityCurrents + 8, 4, "0005");
        PutText(record, OffsetSensitivityCurrents + 12, 4, "0001");
        // kv1: d1[3]/k1/d2[3]/k2/d3[3]
        PutText(record, OffsetPrimaryVoltage + 0, 3, "100");
        record[OffsetPrimaryVoltage + 3] = (byte)'X';
        PutText(record, OffsetPrimaryVoltage + 4, 3, "200");
        record[OffsetPrimaryVoltage + 7] = (byte)'Y';
        PutText(record, OffsetPrimaryVoltage + 8, 3, "300");
        // kv2: d1[3]/k1/d2[3]/k2/d3[3]/k3/d4[3]
        PutText(record, OffsetSecondaryVoltage + 0, 3, "080");
        record[OffsetSecondaryVoltage + 3] = (byte)':';
        PutText(record, OffsetSecondaryVoltage + 4, 3, "484");
        record[OffsetSecondaryVoltage + 7] = (byte)'P';
        PutText(record, OffsetSecondaryVoltage + 8, 3, "010");
        record[OffsetSecondaryVoltage + 11] = (byte)'Q';
        PutText(record, OffsetSecondaryVoltage + 12, 3, "020");
        // kvc: d1[3]/k1/d2[3]/k2/d3[3]/k3/d4[3]
        PutText(record, OffsetControlVoltage + 0, 3, "105");
        record[OffsetControlVoltage + 3] = (byte)'a';
        PutText(record, OffsetControlVoltage + 4, 3, "110");

        NearestRankReference r = NearestRankReference.FromFixedRecord(record);
        NearestRankSharedInfo jg = r.SharedInfo;

        Assert.Equal('A', jg.MainPowerSharedAcDc);
        Assert.Equal('D', jg.ControlPowerSharedAcDc);
        Assert.Equal(new[] { "0200", "0030", "0005", "0001" }, jg.SensitivityCurrents);
        Assert.Equal(new[] { "100", "200", "300" }, jg.PrimaryVoltageValues);
        Assert.Equal(new[] { 'X', 'Y' }, jg.PrimaryVoltageKinds);
        Assert.Equal(new[] { "080", "484", "010", "020" }, jg.SecondaryVoltageValues);
        Assert.Equal(new[] { ':', 'P', 'Q' }, jg.SecondaryVoltageKinds);
        Assert.Equal("105", jg.ControlVoltageValues[0]);
        Assert.Equal("110", jg.ControlVoltageValues[1]);
        Assert.Equal('a', jg.ControlVoltageKinds[0]);
    }

    /// <summary>
    /// ローダーは LF 区切りの複数レコードを解析し、末尾の断片行を読み飛ばす。
    /// </summary>
    [Fact]
    public void ローダー_LF区切りの複数レコードを解析し断片を無視する()
    {
        byte[] r1 = BuildRecord("2COSU", "M  ", ["KM"], 'A', 'A', "100100012500150", "0001", ' ', "TK1", "BE-C06", "085", "110");
        byte[] r2 = BuildRecord("2ERY", "M  ", ["SL"], ' ', 'A', "000250010006000100240", "0001", ' ', "TK2", "ET-N60", "000", "000");

        using var buffer = new MemoryStream();
        buffer.Write(r1);
        buffer.WriteByte((byte)'\n');
        buffer.Write(r2);
        buffer.WriteByte((byte)'\n');
        buffer.Write("garbage"u8.ToArray());

        string path = Path.Combine(Path.GetTempPath(), $"fydf812_{Guid.NewGuid():N}.data");
        File.WriteAllBytes(path, buffer.ToArray());
        try
        {
            IReadOnlyList<NearestRankReference> rows = NearestRankReferenceLoader.ParseNearestRankReference(path);

            Assert.Equal(2, rows.Count);
            Assert.Equal("2COSU", rows[0].ReservedWord);
            Assert.Equal("BE-C06", rows[0].ProductName);
            Assert.Equal("2ERY", rows[1].ReservedWord);
            Assert.Equal(' ', rows[1].MainPowerAcDc);
            Assert.Equal("ET-N60", rows[1].ProductName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 実データ hostdt/FYDF812.data を解析し、レコード数と各フィールドの不変条件を満たす。
    /// </summary>
    [Fact]
    public void 実FYDF812_全レコードでオフセット不変条件を満たす()
    {
        string? path = FindHostFile("FYDF812.data");
        if (path is null)
        {
            _output.WriteLine("hostdt/FYDF812.data 未配置のため検証をスキップします。");
            return;
        }

        IReadOnlyList<NearestRankReference> rows = NearestRankReferenceLoader.ParseNearestRankReference(path);

        Assert.NotEmpty(rows);

        foreach (NearestRankReference r in rows)
        {
            // 予約語は 1..8 文字。
            Assert.True(r.ReservedWord.Length is > 0 and <= 8, $"予約語が不正: [{r.ReservedWord}]");
            // メーカーコードは 3 文字固定。
            Assert.Equal(3, r.MakerCode.Length);
            // パラメータタイプは必ず 7 枠。
            Assert.Equal(ParameterTypeSlotCount(), r.ParameterTypes.Count);
            // データ追番は 4 桁の数字。
            Assert.Matches("^[0-9]{4}$", r.DataSequence);
            // ハンドルロック区分は 'H' か空白。
            Assert.True(r.HandleLockKind is 'H' or ' ', $"hlkbn が不正: [{r.HandleLockKind}] (予約語=[{r.ReservedWord}])");
        }

        // KEY(予約語+メーカー+タイプ+電源区分+定格値+追番)は ISAM 主キーとして一意。
        int distinct = rows
            .Select(r => string.Join('|',
                r.ReservedWord, r.MakerCode, string.Join(',', r.ParameterTypes),
                r.MainPowerAcDc, r.ControlPowerAcDc, r.RatingKey, r.DataSequence))
            .Distinct()
            .Count();
        Assert.Equal(rows.Count, distinct);

        // 実データのスポット検証: 先頭は 2COSU / BE-C06。
        Assert.Equal("2COSU", rows[0].ReservedWord);
        Assert.Equal("BE-C06", rows[0].ProductName);

        _output.WriteLine($"total={rows.Count} distinct={distinct}");
    }

    private static int ParameterTypeSlotCount() => NearestRankReference.ParameterTypeSlotCount;

    /// <summary>
    /// KEY 部と外側フィールドを配置した 300 バイトレコードを生成する。
    /// </summary>
    private static byte[] BuildRecord(
        string reservedWord,
        string makerCode,
        string[] parameterTypes,
        char mainPowerAcDc,
        char controlPowerAcDc,
        string ratingKey,
        string dataSequence,
        char handleLock,
        string equipmentMasterRatingKey,
        string productName,
        string vcFrom,
        string vcTo)
    {
        var record = new byte[RecordLength];
        record.AsSpan().Fill((byte)' ');

        PutText(record, OffsetReservedWord, 8, reservedWord);
        PutText(record, OffsetMakerCode, 3, makerCode);
        for (int i = 0; i < parameterTypes.Length && i < 7; i++)
        {
            PutText(record, OffsetParameterTypes + (i * ParameterTypeSize), ParameterTypeSize, parameterTypes[i]);
        }
        record[OffsetMainPowerAcDc] = (byte)mainPowerAcDc;
        record[OffsetControlPowerAcDc] = (byte)controlPowerAcDc;
        PutText(record, OffsetRatingKey, 50, ratingKey);
        PutText(record, OffsetDataSequence, 4, dataSequence);
        record[OffsetHandleLockKind] = (byte)handleLock;
        PutText(record, OffsetEquipmentMasterRatingKey, 80, equipmentMasterRatingKey);
        PutText(record, OffsetProductName, 25, productName);
        PutText(record, OffsetControlVoltageRangeFrom, 3, vcFrom);
        PutText(record, OffsetControlVoltageRangeTo, 3, vcTo);

        return record;
    }

    private static void PutText(byte[] record, int offset, int width, string value)
    {
        byte[] bytes = FixedFieldCodec.ShiftJis.GetBytes(value);
        int count = Math.Min(bytes.Length, width);
        bytes.AsSpan(0, count).CopyTo(record.AsSpan(offset, width));
    }

    /// <summary>
    /// テスト実行ディレクトリから上位へ辿り、hostdt 配下の指定データファイルを探す。
    /// 見つからなければ null(検証スキップ)。
    /// </summary>
    private static string? FindHostFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "hostdt", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
