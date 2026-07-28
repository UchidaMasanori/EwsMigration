using Ews.Data.Seeding;
using Ews.Domain.Common;
using Ews.Domain.Masters;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// 予約語マスタ(FYDF810)ローダー検証。
///
/// 【C原典】struct FYDF810 (toku/include/common/fydf810.h, レコード長 14980)、
/// Fysk08_Get_YoyakugoFile / Fysk08_CreYoyakuTbl。
///
/// 合成レコードによる決定的なオフセット検証と、実データ hostdt/FYDF810.data による
/// 整合検証(未配置環境ではスキップ)の 2 本立て。
/// </summary>
public sealed class ReservedWordMasterLoaderTests
{
    private readonly ITestOutputHelper _output;

    public ReservedWordMasterLoaderTests(ITestOutputHelper output) => _output = output;

    private const int RecordLength = 14980;
    private const int OffsetTypeTable = 1445;
    private const int TypeTableEntrySize = 1915;
    private const int OffsetKsenkbnInEntry = 20;

    /// <summary>
    /// 合成した 14980 バイトレコードから予約語と 7 枠の ksenkbn を正しく抽出できる。
    /// </summary>
    [Fact]
    public void 合成レコード_予約語と7枠のksenkbnを検証済オフセットで読む()
    {
        byte[] record = BuildRecord("AM", ['1', '1', '1', ' ', '1', ' ', '1']);

        ReservedWordMaster m = ReservedWordMaster.FromFixedRecord(record);

        Assert.Equal("AM", m.ReservedWord);
        Assert.Equal(7, m.SelectionElementKinds.Count);
        Assert.Equal(new[] { '1', '1', '1', ' ', '1', ' ', '1' }, m.SelectionElementKinds);
    }

    /// <summary>
    /// ローダーは LF 区切りの複数レコードを解析し、末尾の断片行を読み飛ばす。
    /// </summary>
    [Fact]
    public void ローダー_LF区切りの複数レコードを解析し断片を無視する()
    {
        byte[] r1 = BuildRecord("2COSU", ['1', '1', '1', ' ', ' ', ' ', ' ']);
        byte[] r2 = BuildRecord("3ERY", ['1', '1', '1', '1', '1', ' ', ' ']);

        using var buffer = new MemoryStream();
        buffer.Write(r1);
        buffer.WriteByte((byte)'\n');
        buffer.Write(r2);
        buffer.WriteByte((byte)'\n');
        // 断片(MinRecordBytes 未満)は無視される。
        buffer.Write("garbage"u8.ToArray());

        string path = Path.Combine(Path.GetTempPath(), $"fydf810_{Guid.NewGuid():N}.data");
        File.WriteAllBytes(path, buffer.ToArray());
        try
        {
            IReadOnlyList<ReservedWordMaster> rows = ReservedWordMasterLoader.ParseReservedWordMaster(path);

            Assert.Equal(2, rows.Count);
            Assert.Equal("2COSU", rows[0].ReservedWord);
            Assert.Equal(new[] { '1', '1', '1', ' ', ' ', ' ', ' ' }, rows[0].SelectionElementKinds);
            Assert.Equal("3ERY", rows[1].ReservedWord);
            Assert.Equal(new[] { '1', '1', '1', '1', '1', ' ', ' ' }, rows[1].SelectionElementKinds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 実データ hostdt/FYDF810.data を解析し、予約語と ksenkbn の不変条件を満たす。
    /// </summary>
    [Fact]
    public void 実FYDF810_全レコードでオフセット不変条件を満たす()
    {
        string? path = FindHostFile("FYDF810.data");
        if (path is null)
        {
            _output.WriteLine("hostdt/FYDF810.data 未配置のため検証をスキップします。");
            return;
        }

        IReadOnlyList<ReservedWordMaster> rows = ReservedWordMasterLoader.ParseReservedWordMaster(path);

        Assert.NotEmpty(rows);

        foreach (ReservedWordMaster r in rows)
        {
            // 予約語は最大 8 文字。
            Assert.True(r.ReservedWord.Length is > 0 and <= 8, $"予約語が不正: [{r.ReservedWord}]");
            // タイプ枠は必ず 7 枠。
            Assert.Equal(7, r.SelectionElementKinds.Count);
            // ksenkbn は '1'(機器選定要素)または ' '(以外)のみ。
            foreach (char k in r.SelectionElementKinds)
            {
                Assert.True(k is '1' or ' ', $"ksenkbn が不正: [{k}] (予約語=[{r.ReservedWord}])");
            }
        }

        // 予約語は一意(ISAM 主キー)。
        int distinct = rows.Select(r => r.ReservedWord).Distinct().Count();
        Assert.Equal(rows.Count, distinct);

        // 実データのスポット検証: AM は全 7 枠が機器選定要素。
        ReservedWordMaster? am = rows.FirstOrDefault(r => r.ReservedWord == "AM");
        if (am is not null)
        {
            Assert.All(am.SelectionElementKinds, k => Assert.Equal('1', k));
        }

        _output.WriteLine($"total={rows.Count} distinct={distinct}");
    }

    /// <summary>
    /// 予約語(8) + タイプ枠(7 枠)ごとの ksenkbn を配置した 14980 バイトレコードを生成する。
    /// </summary>
    private static byte[] BuildRecord(string reservedWord, char[] ksenkbn)
    {
        var record = new byte[RecordLength];
        record.AsSpan().Fill((byte)' ');

        byte[] yo = FixedFieldCodec.ShiftJis.GetBytes(reservedWord);
        yo.AsSpan(0, Math.Min(yo.Length, 8)).CopyTo(record.AsSpan(0, 8));

        for (int i = 0; i < ksenkbn.Length; i++)
        {
            int offset = OffsetTypeTable + (i * TypeTableEntrySize) + OffsetKsenkbnInEntry;
            record[offset] = (byte)ksenkbn[i];
        }

        return record;
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
