using Ews.Data.Seeding;
using Ews.Domain.Masters;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// 物件情報(FYDF801)実データ検証。実エクスポート master/FYDF801.data を解析し、
/// <see cref="ProjectInformation.FromFixedRecord"/> のバイトオフセットが実機データと
/// 整合することを、全レコードの不変条件で検証する。
///
/// 【C原典】struct FYDF801 (toku/include/common/fydf801.h, レコード長 1200)。
/// 実データは本リポジトリ外(EWS/master 配下)にあるため、未配置環境ではスキップする
/// (テストは成功扱い)。
/// </summary>
public sealed class ProjectInformationRealDataTests
{
    private readonly ITestOutputHelper _output;

    public ProjectInformationRealDataTests(ITestOutputHelper output) => _output = output;

    /// <summary>周波数区分 hzkbn の許容値。ドキュメントは 1/2/3 だが実データに 5/6(地域別)も存在する。</summary>
    private static readonly HashSet<string> AllowedFrequencyKinds = ["", "1", "2", "3", "5", "6"];

    [Fact]
    public void 実FYDF801_全レコードでオフセット不変条件を満たす()
    {
        string? path = FindMasterFile("FYDF801.data");
        if (path is null)
        {
            _output.WriteLine("master/FYDF801.data 未配置のため検証をスキップします。");
            return;
        }

        IReadOnlyList<ProjectInformation> rows = ProjectInformationLoader.ParseProjectInformation(path);

        Assert.True(rows.Count > 1000, $"レコード数が想定より少ない: {rows.Count}");

        int common = 0;   // 物件共通情報(明細番号ブランク)
        int detail = 0;   // 盤明細情報(明細番号 01～99)
        int populatedJp = 0;
        var hzDist = new Dictionary<string, int>();

        foreach (ProjectInformation r in rows)
        {
            // 明細番号は 2 バイトフィールド。ブランク(物件共通)または最大 2 文字
            // (盤明細 '01'～'99'。実データには '0A'～'0D' 等の英字を含む明細番号も存在する)。
            Assert.True(
                r.DetailNumber.Length <= 2,
                $"明細番号が長さ超過: [{r.DetailNumber}] (req=[{r.RequestNumber}])");

            if (r.DetailNumber.Length == 0)
            {
                common++;

                // 周波数区分は既知集合内。
                Assert.Contains(r.FrequencyKind, AllowedFrequencyKinds);
                hzDist[r.FrequencyKind] = 1 + (hzDist.TryGetValue(r.FrequencyKind, out int c) ? c : 0);

                // 製作仕様区分は ブランク または 2 桁数字。
                Assert.True(
                    r.ManufacturingSpecKind.Length == 0 ||
                    (r.ManufacturingSpecKind.Length == 2 && IsAllDigit(r.ManufacturingSpecKind)),
                    $"製作仕様区分が不正: [{r.ManufacturingSpecKind}] (req=[{r.RequestNumber}])");

                if (r.SalesOfficeName.Length > 0 && r.ProjectName1.Length > 0)
                {
                    populatedJp++;
                }
            }
            else
            {
                detail++;
            }
        }

        // 日本語フィールド(営業所名/件名)が正しくデコードできる物件共通レコードが存在する。
        Assert.True(populatedJp > 0, "日本語フィールドが埋まった物件共通レコードが見つからない(オフセット疑い)。");

        _output.WriteLine($"total={rows.Count} common={common} detail={detail} populatedJp={populatedJp}");
        foreach (KeyValuePair<string, int> kv in hzDist.OrderByDescending(k => k.Value))
        {
            _output.WriteLine($"hzkbn[{kv.Key}] = {kv.Value}");
        }
    }

    [Fact]
    public void 実FYDF801_盤明細レコードのbmeisaiオフセットを満たす()
    {
        string? path = FindMasterFile("FYDF801.data");
        if (path is null)
        {
            _output.WriteLine("master/FYDF801.data 未配置のため検証をスキップします。");
            return;
        }

        IReadOnlyList<PanelDetailInformation> rows = ProjectInformationLoader.ParsePanelDetails(path);

        Assert.True(rows.Count > 1000, $"盤明細レコード数が想定より少ない: {rows.Count}");

        int populatedName = 0;
        int populatedDepth = 0;
        foreach (PanelDetailInformation r in rows)
        {
            // 盤明細レコードは明細番号 非ブランクかつ最大 2 文字。
            Assert.True(
                r.DetailNumber.Length is > 0 and <= 2,
                $"明細番号が不正: [{r.DetailNumber}] (req=[{r.RequestNumber}])");

            // ボックス寸法(タテ/ヨコ/フカサ)は '9' 属性 → 空 または 数字のみ(mm, 5 桁ゼロ詰め)。
            AssertBlankOrDigits(r.BoxHeight, "BoxHeight", r.RequestNumber);
            AssertBlankOrDigits(r.BoxWidth, "BoxWidth", r.RequestNumber);
            AssertBlankOrDigits(r.BoxDepth, "BoxDepth", r.RequestNumber);

            if (r.PanelName.Length > 0)
            {
                populatedName++;
            }
            if (r.BoxDepth.Length > 0)
            {
                populatedDepth++;
            }
        }

        // 盤名称(日本語)・ボックスフカサが埋まった盤明細が存在する(オフセット健全性)。
        Assert.True(populatedName > 0, "盤名称が埋まった盤明細が見つからない(オフセット疑い)。");
        Assert.True(populatedDepth > 0, "ボックスフカサが埋まった盤明細が見つからない(オフセット疑い)。");

        _output.WriteLine($"panelDetails={rows.Count} populatedName={populatedName} populatedDepth={populatedDepth}");
    }

    private static void AssertBlankOrDigits(string value, string field, string requestNumber)
    {
        Assert.True(
            value.Length == 0 || IsAllDigit(value),
            $"{field} が数値でない: [{value}] (req=[{requestNumber}])");
    }

    private static bool IsAllDigit(string s)
    {
        foreach (char c in s)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// テスト実行ディレクトリから上位へ辿り、master 配下の指定データファイルを探す。
    /// 見つからなければ null(検証スキップ)。
    /// </summary>
    private static string? FindMasterFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "master", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
