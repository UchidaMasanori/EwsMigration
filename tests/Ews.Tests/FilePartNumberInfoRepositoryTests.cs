using System.Text;
using Ews.Data.Seeding;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 品番情報リポジトリ(FyCpHbHbnInfFileR)の移植検証。
///
/// 【C原典】FyCpHbHbnInfFileR(clfilerw.c)。依頼明細番号をキーに &lt;WORK&gt;/&lt;iraimei&gt;/&lt;iraimei&gt;.clh
/// を読み込む。存在しない・サイズ不一致は null。
/// </summary>
public sealed class FilePartNumberInfoRepositoryTests
{
    private const int RecordLength = 908;   // 【C原典】sizeof(struct hbninf)。

    private static string CreateClh(string workDir, string iraimei, string inputPartNumber, int size = RecordLength)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] record = new byte[size];
        byte[] hb = Encoding.GetEncoding(932).GetBytes(inputPartNumber);
        Array.Copy(hb, record, Math.Min(hb.Length, size - 1));   // inputhb@0、末尾は NUL 終端。

        string caseDir = Path.Combine(workDir, iraimei);
        Directory.CreateDirectory(caseDir);
        string path = Path.Combine(caseDir, iraimei + ".clh");
        File.WriteAllBytes(path, record);
        return path;
    }

    [Fact]
    public void Find_依頼明細番号のclhを読み込む()
    {
        string workDir = Path.Combine(Path.GetTempPath(), "ews_clh_" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateClh(workDir, "2607AL01", "GWL-GM1-GQ20");
            var repository = new FilePartNumberInfoRepository(workDir);

            PartNumberInfo? info = repository.Find("2607AL01");

            Assert.NotNull(info);
            Assert.StartsWith("GWL-GM1-GQ20", info!.InputPartNumber);
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    [Fact]
    public void Find_空白を除去してファイル名に用いる()
    {
        string workDir = Path.Combine(Path.GetTempPath(), "ews_clh_" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateClh(workDir, "2607AL01", "GJWL");
            var repository = new FilePartNumberInfoRepository(workDir);

            PartNumberInfo? info = repository.Find("2607AL01 ");

            Assert.NotNull(info);
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    [Fact]
    public void Find_ファイルが無ければnull()
    {
        string workDir = Path.Combine(Path.GetTempPath(), "ews_clh_" + Guid.NewGuid().ToString("N"));
        var repository = new FilePartNumberInfoRepository(workDir);

        Assert.Null(repository.Find("9999XX99"));
    }

    [Fact]
    public void Find_サイズ不一致はnull()
    {
        string workDir = Path.Combine(Path.GetTempPath(), "ews_clh_" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateClh(workDir, "2607AL02", "GWL", size: 100);   // 908 バイトでない
            var repository = new FilePartNumberInfoRepository(workDir);

            Assert.Null(repository.Find("2607AL02"));
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }
}
