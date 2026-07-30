using System.Collections.Generic;
using System.IO;
using System.Text;
using Ews.Data.Configuration;
using Ews.Domain.Configuration;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 実行時パラメータプロバイダ(<see cref="InMemoryRuntimeParameterProvider"/> /
/// <see cref="FileRuntimeParameterProvider"/>)の単体テスト。
/// </summary>
public class RuntimeParameterProviderTests
{
    [Fact]
    public void InMemory_定義済みの値を取得できる()
    {
        var provider = new InMemoryRuntimeParameterProvider(new Dictionary<string, string?>
        {
            [RuntimeParameterNames.ZoneCode] = "78007",
            [RuntimeParameterNames.LoginHost] = "host1",
        });

        Assert.Equal("78007", provider.GetValue(RuntimeParameterNames.ZoneCode));
        Assert.Equal("host1", provider.GetValue("LHOST"));
        Assert.Equal("78007", provider.ZoneCode);
    }

    [Fact]
    public void InMemory_未定義はnull_ZoneCodeは空文字()
    {
        var provider = new InMemoryRuntimeParameterProvider(new Dictionary<string, string?>());

        Assert.Null(provider.GetValue("TERMID"));
        Assert.Equal(string.Empty, provider.ZoneCode);
    }

    [Fact]
    public void InMemory_名前は大文字小文字を区別する()
    {
        var provider = new InMemoryRuntimeParameterProvider(new Dictionary<string, string?>
        {
            ["ZONECD"] = "78007",
        });

        Assert.Equal("78007", provider.GetValue("ZONECD"));
        Assert.Null(provider.GetValue("zonecd"));
    }

    [Fact]
    public void File_RuntimeParametersセクションから取得できる()
    {
        const string json = """
        {
          "RuntimeParameters": {
            "ZONECD": "78007",
            "GNAME": "水俣"
          }
        }
        """;

        FileRuntimeParameterProvider provider = FileRuntimeParameterProvider.FromJson(json);

        Assert.Equal("78007", provider.ZoneCode);
        Assert.Equal("水俣", provider.GetValue(RuntimeParameterNames.GroupName));
    }

    [Fact]
    public void File_直下オブジェクトからも取得できる()
    {
        const string json = """
        { "ZONECD": "10001" }
        """;

        FileRuntimeParameterProvider provider = FileRuntimeParameterProvider.FromJson(json);

        Assert.Equal("10001", provider.ZoneCode);
    }

    [Fact]
    public void File_ネストしたオブジェクトは無視する()
    {
        const string json = """
        {
          "ConnectionStrings": { "EwsDatabase": "x" },
          "ZONECD": "20002"
        }
        """;

        FileRuntimeParameterProvider provider = FileRuntimeParameterProvider.FromJson(json);

        Assert.Equal("20002", provider.ZoneCode);
        Assert.Null(provider.GetValue("ConnectionStrings"));
    }

    [Fact]
    public void File_設定ファイルを読み込める()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ews_rt_{System.Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{ "RuntimeParameters": { "ZONECD": "78007" } }""", new UTF8Encoding(false));

            FileRuntimeParameterProvider provider = FileRuntimeParameterProvider.LoadFromFile(path);

            Assert.Equal("78007", provider.ZoneCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void File_存在しないファイルは例外()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ews_missing_{System.Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(() => FileRuntimeParameterProvider.LoadFromFile(path));
    }
}
