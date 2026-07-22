using System.Text;
using Ews.Data.Seeding;
using Ews.Domain.Common;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// .cns テキストマスタ取込の検証。
/// 【C原典】Zs20SiyoInfoRead (toku/interf/zs50/src/Fymzs40Cns.c) の階層パース。
/// </summary>
public sealed class CnsMasterLoaderTests
{
    /// <summary>Shift-JIS の一時 .cns ファイルを作成し、テスト後に破棄する。</summary>
    private static string WriteTempCns(string content)
    {
        Encoding sjis = FixedFieldCodec.ShiftJis;
        string path = Path.Combine(Path.GetTempPath(), $"ews_test_{Guid.NewGuid():N}.cns");
        File.WriteAllBytes(path, sjis.GetBytes(content));
        return path;
    }

    [Fact]
    public void ParseSpecificationMaster_部署と仕様書の階層を解釈する()
    {
        string cns = string.Join('\n',
            "#コメント行は無視",
            "部署:01212",
            "  仕様書:標準",
            "    仕様書説明:河村仕様 国土交通省",
            "    仕様書パス:/KCAD/DATA/SIYOU/",
            "    仕様書ファイル:KAWAMURA-HYOUJUN",
            "    仕様書ファイル:KAWAMURA-TANSISENTEI",
            "  END仕様書",
            "  仕様書:太陽光仕様書",
            "    仕様書説明:太陽光1000V",
            "    仕様書パス:/KCAD/DATA/SIYOU/",
            "    仕様書ファイル:KAWA-HYO-PV1-1K",
            "  END仕様書",
            "END部署");
        string path = WriteTempCns(cns);

        try
        {
            IReadOnlyList<SpecificationInfo> result = CnsMasterLoader.ParseSpecificationMaster(path);

            SpecificationInfo dept = Assert.Single(result);
            Assert.Equal("01212", dept.DepartmentCode);
            Assert.Equal(2, dept.Kinds.Count);

            SpecificationKind standard = dept.Kinds[0];
            Assert.Equal("標準", standard.Name);
            Assert.Equal("河村仕様 国土交通省", standard.Description);
            Assert.Equal("/KCAD/DATA/SIYOU/", standard.Path);
            Assert.Equal(["KAWAMURA-HYOUJUN", "KAWAMURA-TANSISENTEI"], standard.Files);

            SpecificationKind pv = dept.Kinds[1];
            Assert.Equal("太陽光仕様書", pv.Name);
            Assert.Equal("太陽光1000V", pv.Description);
            Assert.Equal(["KAWA-HYO-PV1-1K"], pv.Files);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseSpecificationMaster_複数部署を全て取り込む()
    {
        // 【C原典】C は ZONECD 一致部署のみ保持するが、取込は全部署。
        string cns = string.Join('\n',
            "部署:01212",
            "  仕様書:標準",
            "    仕様書ファイル:A",
            "  END仕様書",
            "END部署",
            "部署:02020",
            "  仕様書:特注",
            "    仕様書ファイル:B",
            "  END仕様書",
            "END部署");
        string path = WriteTempCns(cns);

        try
        {
            IReadOnlyList<SpecificationInfo> result = CnsMasterLoader.ParseSpecificationMaster(path);

            Assert.Equal(2, result.Count);
            Assert.Equal("01212", result[0].DepartmentCode);
            Assert.Equal("02020", result[1].DepartmentCode);
            Assert.Equal("特注", result[1].Kinds[0].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseSpecificationMaster_END無しの末尾部署も確定する()
    {
        // 【C原典】ファイル終端。END部署 が無くても末尾ブロックを確定。
        string cns = string.Join('\n',
            "部署:03030",
            "  仕様書:標準",
            "    仕様書パス:/X/",
            "    仕様書ファイル:C");
        string path = WriteTempCns(cns);

        try
        {
            IReadOnlyList<SpecificationInfo> result = CnsMasterLoader.ParseSpecificationMaster(path);

            SpecificationInfo dept = Assert.Single(result);
            Assert.Equal("03030", dept.DepartmentCode);
            SpecificationKind kind = Assert.Single(dept.Kinds);
            Assert.Equal("/X/", kind.Path);
            Assert.Equal(["C"], kind.Files);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
