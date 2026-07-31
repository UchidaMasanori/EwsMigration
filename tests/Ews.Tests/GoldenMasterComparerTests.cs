using Ews.Domain.Validation;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// ゴールデンマスタ 5 ファイル比較コア(<see cref="GoldenMasterComparer"/>)の検証。
///
/// 合成レコードで比較エンジン(datajg マスク・件数差・バイト差検出)を網羅し、
/// 実 WORK データ(AIX 実機出力の FYDF806/808/809/811)でレコード長定義と
/// マスク挙動が実バイト列に整合することを実証する(未配置環境ではスキップ)。
/// </summary>
public sealed class GoldenMasterComparerTests
{
    private readonly ITestOutputHelper _output;

    public GoldenMasterComparerTests(ITestOutputHelper output) => _output = output;

    // ── レイアウト定義 ───────────────────────────────────────────────

    [Theory]
    [InlineData(GoldenMasterFileKind.MainCircuit, 1219, "FYDF806")]
    [InlineData(GoldenMasterFileKind.Composite, 1219, "FYDF807")]
    [InlineData(GoldenMasterFileKind.Control, 1920, "FYDF808")]
    [InlineData(GoldenMasterFileKind.Logic, 304, "FYDF809")]
    [InlineData(GoldenMasterFileKind.ComponentEquipment, 350, "FYDF811")]
    public void レイアウト定義はC構造体のレコード長とファイルIDに一致する(
        GoldenMasterFileKind kind, int recordLength, string fileId)
    {
        Assert.Equal(recordLength, GoldenMasterLayout.RecordLength(kind));
        Assert.Equal(fileId, GoldenMasterLayout.FileId(kind));
        Assert.Equal(recordLength - GoldenMasterLayout.DatajgLength, GoldenMasterLayout.DatajgOffset(kind));
    }

    [Fact]
    public void datajg長は36バイト固定である()
    {
        Assert.Equal(36, GoldenMasterLayout.DatajgLength);
    }

    // ── 比較エンジン(合成データ) ────────────────────────────────────

    [Fact]
    public void 同一バイト列は一致と判定される()
    {
        int rl = GoldenMasterLayout.RecordLength(GoldenMasterFileKind.Logic);
        byte[] data = FillPattern(rl * 3);

        GoldenMasterComparisonResult result =
            GoldenMasterComparer.Compare(GoldenMasterFileKind.Logic, data, (byte[])data.Clone());

        Assert.True(result.IsMatch);
        Assert.Equal(3, result.ExpectedRecordCount);
        Assert.Equal(3, result.ActualRecordCount);
        Assert.Empty(result.RecordDiffs);
    }

    [Fact]
    public void データ領域のバイト差分は検出される()
    {
        var kind = GoldenMasterFileKind.Logic;
        int rl = GoldenMasterLayout.RecordLength(kind);
        byte[] expected = FillPattern(rl * 2);
        byte[] actual = (byte[])expected.Clone();

        // レコード 1 の先頭から 10 バイト目(データ領域)を書き換える。
        int mutatedOffset = rl + 10;
        actual[mutatedOffset] = (byte)(actual[mutatedOffset] ^ 0xFF);

        GoldenMasterComparisonResult result = GoldenMasterComparer.Compare(kind, expected, actual);

        Assert.False(result.IsMatch);
        GoldenMasterRecordDiff diff = Assert.Single(result.RecordDiffs);
        Assert.Equal(1, diff.RecordIndex);
        Assert.Equal(10, diff.FirstDiff.ByteOffset);
        Assert.Equal(expected[mutatedOffset], diff.FirstDiff.Expected);
        Assert.Equal(actual[mutatedOffset], diff.FirstDiff.Actual);
        Assert.Equal(1, diff.DifferingByteCount);
    }

    [Fact]
    public void datajg領域の差分はマスク時に無視され非マスク時に検出される()
    {
        var kind = GoldenMasterFileKind.MainCircuit;
        int rl = GoldenMasterLayout.RecordLength(kind);
        byte[] expected = FillPattern(rl);
        byte[] actual = (byte[])expected.Clone();

        // datajg 領域(末尾 36 バイト)の 1 バイトを書き換える。
        int datajgByte = GoldenMasterLayout.DatajgOffset(kind) + 5;
        actual[datajgByte] = (byte)(actual[datajgByte] ^ 0xFF);

        GoldenMasterComparisonResult masked = GoldenMasterComparer.Compare(kind, expected, actual, maskDatajg: true);
        Assert.True(masked.IsMatch);

        GoldenMasterComparisonResult unmasked = GoldenMasterComparer.Compare(kind, expected, actual, maskDatajg: false);
        Assert.False(unmasked.IsMatch);
        GoldenMasterRecordDiff diff = Assert.Single(unmasked.RecordDiffs);
        Assert.Equal(GoldenMasterLayout.DatajgOffset(kind) + 5, diff.FirstDiff.ByteOffset);
    }

    [Fact]
    public void レコード件数差は結果に反映される()
    {
        var kind = GoldenMasterFileKind.ComponentEquipment;
        int rl = GoldenMasterLayout.RecordLength(kind);
        byte[] expected = FillPattern(rl * 3);
        byte[] actual = FillPattern(rl * 2);

        GoldenMasterComparisonResult result = GoldenMasterComparer.Compare(kind, expected, actual);

        Assert.False(result.IsMatch);
        Assert.Equal(3, result.ExpectedRecordCount);
        Assert.Equal(2, result.ActualRecordCount);
        // 共通の 2 レコードはパターンが一致するため差分なし。
        Assert.Empty(result.RecordDiffs);
    }

    [Fact]
    public void 複数バイト差分は総数が集計される()
    {
        var kind = GoldenMasterFileKind.Logic;
        int rl = GoldenMasterLayout.RecordLength(kind);
        byte[] expected = FillPattern(rl);
        byte[] actual = (byte[])expected.Clone();

        for (int i = 0; i < 4; i++)
        {
            actual[i] = (byte)(actual[i] ^ 0xFF);
        }

        GoldenMasterComparisonResult result = GoldenMasterComparer.Compare(kind, expected, actual);

        GoldenMasterRecordDiff diff = Assert.Single(result.RecordDiffs);
        Assert.Equal(0, diff.FirstDiff.ByteOffset);
        Assert.Equal(4, diff.DifferingByteCount);
    }

    [Fact]
    public void レコード長の整数倍でない場合は例外となる()
    {
        var kind = GoldenMasterFileKind.Logic; // rl=304
        byte[] bad = new byte[305];
        byte[] ok = new byte[304];

        Assert.Throws<ArgumentException>(() => GoldenMasterComparer.Compare(kind, bad, ok));
        Assert.Throws<ArgumentException>(() => GoldenMasterComparer.Compare(kind, ok, bad));
    }

    // ── 実 WORK データ検証(未配置ならスキップ) ──────────────────────

    [Fact]
    public void 実WORKデータのファイル長は定義レコード長の整数倍である()
    {
        string? work = FindWorkDir();
        if (work is null)
        {
            _output.WriteLine("WORK ディレクトリ未配置のため検証をスキップします。");
            return;
        }

        int checkedFiles = 0;
        foreach (string proj in EnumerateProjects(work))
        {
            foreach (GoldenMasterFileKind kind in Enum.GetValues<GoldenMasterFileKind>())
            {
                byte[]? b = ReadGoldenFile(proj, kind);
                if (b is null || b.Length == 0)
                {
                    continue;
                }

                int rl = GoldenMasterLayout.RecordLength(kind);
                Assert.True(
                    b.Length % rl == 0,
                    $"{Path.GetFileName(proj)}/{GoldenMasterLayout.FileId(kind)} 長 {b.Length} が RL={rl} の整数倍ではありません。");
                checkedFiles++;
            }
        }

        _output.WriteLine($"レコード長整合を検証したファイル数: {checkedFiles}");
        Assert.True(checkedFiles > 0, "検証対象のゴールデンファイルが見つかりませんでした。");
    }

    [Fact]
    public void 実WORKデータの自己比較は一致しdatajg改変はマスクされる()
    {
        string? work = FindWorkDir();
        if (work is null)
        {
            _output.WriteLine("WORK ディレクトリ未配置のため検証をスキップします。");
            return;
        }

        int verified = 0;
        foreach (string proj in EnumerateProjects(work))
        {
            foreach (GoldenMasterFileKind kind in Enum.GetValues<GoldenMasterFileKind>())
            {
                byte[]? b = ReadGoldenFile(proj, kind);
                if (b is null || b.Length == 0)
                {
                    continue;
                }

                // 自己比較は完全一致。
                Assert.True(GoldenMasterComparer.Compare(kind, b, b).IsMatch);

                // datajg 領域(先頭レコードの末尾)を改変してもマスク時は一致。
                byte[] tampered = (byte[])b.Clone();
                int datajgByte = GoldenMasterLayout.DatajgOffset(kind);
                tampered[datajgByte] = (byte)(tampered[datajgByte] ^ 0xFF);
                Assert.True(GoldenMasterComparer.Compare(kind, b, tampered, maskDatajg: true).IsMatch);
                Assert.False(GoldenMasterComparer.Compare(kind, b, tampered, maskDatajg: false).IsMatch);

                verified++;
            }
        }

        _output.WriteLine($"自己比較/マスク検証を実施したファイル数: {verified}");
        Assert.True(verified > 0, "検証対象のゴールデンファイルが見つかりませんでした。");
    }

    // ── 補助 ─────────────────────────────────────────────────────────

    private static byte[] FillPattern(int length)
    {
        byte[] b = new byte[length];
        for (int i = 0; i < length; i++)
        {
            b[i] = (byte)((i * 31 + 7) & 0xFF);
        }

        return b;
    }

    private static byte[]? ReadGoldenFile(string projDir, GoldenMasterFileKind kind)
    {
        string name = Path.GetFileName(projDir);
        string path = Path.Combine(projDir, $"{GoldenMasterLayout.FileId(kind)}.{name}");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    private static IEnumerable<string> EnumerateProjects(string workDir)
    {
        return Directory.EnumerateDirectories(workDir)
            .OrderBy(d => d, StringComparer.Ordinal)
            .Take(50);
    }

    private static string? FindWorkDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "WORK");
            if (Directory.Exists(candidate) && HasProjectData(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static bool HasProjectData(string workDir)
    {
        foreach (string sub in Directory.EnumerateDirectories(workDir))
        {
            string name = Path.GetFileName(sub);
            if (File.Exists(Path.Combine(sub, $"FYDF806.{name}")))
            {
                return true;
            }
        }

        return false;
    }
}
