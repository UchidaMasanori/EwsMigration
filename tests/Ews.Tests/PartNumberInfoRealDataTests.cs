using Ews.Data.Seeding;
using Ews.Domain.Masters;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// 品番情報ファイル(hbninf / .clh)実データ検証。案件ごとの生バイナリ 1 レコード
/// <c>&lt;WORK&gt;/&lt;案件&gt;/&lt;案件&gt;.clh</c> を読み、<see cref="PartNumberInfo.FromFixedRecord"/> の
/// バイトオフセットが実機データと整合することを検証する。
///
/// 【C原典】struct hbninf (toku/include/cmpchg/cmplogtr.h, sizeof=908)。
/// .clh は本リポジトリ外(EWS/WORK 配下)にあるため、未配置環境ではスキップする。
/// </summary>
public sealed class PartNumberInfoRealDataTests
{
    private readonly ITestOutputHelper _output;

    public PartNumberInfoRealDataTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void 実clh_品番情報のCHARフィールドを検証済オフセットで読む()
    {
        string? work = FindWorkDir();
        if (work is null)
        {
            _output.WriteLine("WORK ディレクトリ未配置のためスキップします。");
            return;
        }

        int files = 0;
        int populated = 0;
        foreach (string projDir in Directory.EnumerateDirectories(work).OrderBy(d => d, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(projDir);
            string clh = Path.Combine(projDir, name + ".clh");

            PartNumberInfo? info = PartNumberInfoLoader.ReadFromFile(clh);
            if (info is null)
            {
                continue; // .clh が無い/サイズ不一致の案件は対象外。
            }

            files++;

            // 製作仕様区分は 空 または 2 桁(FYDF801 sshiykbn と同区分)。
            Assert.True(
                info.ManufacturingSpecKind.Length is 0 or 2,
                $"製作仕様区分が不正: [{info.ManufacturingSpecKind}] ({name})");

            // 適用 BOX タイプ(全体)は 2 文字 + \0 の短い識別子。
            Assert.True(info.BoxType.Length <= 15, $"BoxType が異常: [{info.BoxType}] ({name})");

            if (info.InputPartNumber.Length > 0)
            {
                populated++;
            }

            _output.WriteLine(
                $"{name}: input=[{info.InputPartNumber}] boxtyp=[{info.BoxType}] " +
                $"crboxtmp=[{info.GeneratedBoxPartNumber}] sshiy=[{info.ManufacturingSpecKind}]");
        }

        if (files == 0)
        {
            _output.WriteLine(".clh ファイルが見つからないためスキップします。");
            return;
        }

        // 入力品番(inputhb @0)が埋まった .clh が存在する(オフセット健全性)。
        Assert.True(populated > 0, "入力品番が埋まった .clh が見つからない(オフセット疑い)。");
        _output.WriteLine($"clhFiles={files} populatedInput={populated}");
    }

    private static string? FindWorkDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "WORK");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
