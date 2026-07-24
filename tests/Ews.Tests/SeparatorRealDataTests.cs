using System.Text;
using Ews.Analysis;
using Ews.Data.Seeding;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Ews.Domain.Common;
using Ews.Domain.Masters;
using Ews.Domain.Projects;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// セパレータ(SEP)追加の実データ突合ハーネス(負側検証)。
///
/// 実案件データ(AIX 実機が生成した固定長ファイル)を入力に、移植した回路解析パイプライン
///   FYDF805(回路内容記述) → <see cref="CircuitStringChecker.Check"/>(系統文字列チェック)
///   → <see cref="MainCircuitBuilder.MakeMain"/>(主回路生成 step6・SEP 追加)
/// を実データで通し、生成される SEP 機器数が C 版出力(FYDF806 の yoyaku="SEP" レコード数)と
/// 一致することを検証する。
///
/// 現時点で入手済みの案件(2607AL01/02/03/05)は FYDF806 に SEP レコードを含まない(=0)。
/// したがって本ハーネスは「移植版が SEP を過剰挿入しない」ことを保証する負側検証である。
/// (SEP を含む案件が得られれば、同一比較で正側検証にも拡張できる。)
///
/// 【C原典・レイアウト】
///   - FYDF805(回路内容記述, RL=270): key(12)+gyosyu[5]@+12+kairoar[200]@+17。
///     1 レコード = 1 記述行(<see cref="CircuitDescriptionLine.FromFixedRecord"/> で復元)。
///   - FYDF806(主回路データ, RL=1219): key(12)+syukairo。yoyaku[8]@+38。
///     yoyaku 先頭 3 文字が "SEP" のレコードがセパレータ機器。
///
/// 基準データは本リポジトリ外(EWS/WORK 配下)にあるため、未配置の環境では検証をスキップする
/// (テストは成功扱い)。
/// </summary>
public sealed class SeparatorRealDataTests
{
    private const int Rl805 = 270;
    private const int Rl806 = 1219;
    private const int Yoyaku806Offset = 38;
    private const int Yoyaku806Len = 8;

    /// <summary>検証対象とする案件数の上限(テスト時間を抑えるためのサンプル)。</summary>
    private const int MaxProjects = 150;

    private static readonly Encoding Cp932 = FixedFieldCodec.ShiftJis;

    private readonly ITestOutputHelper _output;

    public SeparatorRealDataTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// 実 FYDF805 を回路解析パイプラインへ流し、主回路生成(SEP 追加込み)で得られる
    /// SEP 機器数が C 版出力(FYDF806 の SEP レコード数)と一致することを検証する。
    /// </summary>
    [Fact]
    public void 実FYDF805からの主回路生成はSEPをC版と同数だけ生成する()
    {
        string? work = FindWorkDir();
        if (work is null)
        {
            _output.WriteLine("WORK ディレクトリ未配置のため検証をスキップします。");
            return;
        }

        string? cnsPath = FindConstFile(work, "unithb300.cns");
        IReadOnlyList<string> hb300 = cnsPath is null
            ? Array.Empty<string>()
            : Hb300UnitPartLoader.Parse(cnsPath);

        int validated = 0;
        int mismatches = 0;
        foreach (string projDir in EnumerateProjects(work))
        {
            string name = Path.GetFileName(projDir);

            byte[]? f805 = ReadRecordFile(projDir, "FYDF805", Rl805);
            byte[]? f806 = ReadRecordFile(projDir, "FYDF806", Rl806);
            if (f805 is null || f806 is null)
            {
                continue;
            }

            // 案件の品番情報(.clh)。無い/サイズ不一致ならこの案件は SEP 検証対象外。
            string clhPath = Path.Combine(projDir, $"{name}.clh");
            PartNumberInfo? partInfo = PartNumberInfoLoader.ReadFromFile(clhPath);
            if (partInfo is null)
            {
                continue;
            }

            // FYDF805(270 バイト固定長)を 1 記述行ずつ復元する。
            var lines = new List<CircuitDescriptionLine>();
            int count805 = f805.Length / Rl805;
            for (int r = 0; r < count805; r++)
            {
                lines.Add(CircuitDescriptionLine.FromFixedRecord(f805.AsSpan(r * Rl805, Rl805)));
            }

            // C 版の SEP レコード数(FYDF806 の yoyaku="SEP")を基準値とする。
            int expectedSep = CountSeparatorRecords(f806);

            // 系統文字列チェック → 主回路生成(SEP 追加込み)。
            // project/projectDetail(bukken1/bukken2)は Check 内で参照されない(受け渡しのみ)ため空でよい。
            var project = new ProjectInfo();
            CircuitParseResult parse = new CircuitStringChecker().Check(project, project, lines);

            // ボックスフカサ(boxsund)は本案件群(boxtyp="BX")では SEP 判定に影響しない
            // (PropChkSEPBox が JBR/JOC 始まり以外は非該当)ため空で渡す。
            var separatorInputs = new SeparatorInputs(partInfo, string.Empty, hb300);
            new MainCircuitBuilder().MakeMain(parse, null, separatorInputs);

            int actualSep = 0;
            foreach (EquipmentTableEntry e in parse.MainEquipment)
            {
                if (e.ReservedWord.StartsWith("SEP", StringComparison.Ordinal))
                {
                    actualSep++;
                }
            }

            _output.WriteLine(
                $"proj={name} 記述行={count805} 主機器={parse.MainEquipment.Count} " +
                $"SEP(C版)={expectedSep} SEP(移植版)={actualSep}");

            if (actualSep != expectedSep)
            {
                mismatches++;
            }

            Assert.Equal(expectedSep, actualSep);
            validated++;
        }

        _output.WriteLine($"検証: 案件={validated} 不一致={mismatches}");
        Assert.True(validated > 0, "検証対象(FYDF805/FYDF806/.clh が揃う案件)が見つかりませんでした。");
    }

    /// <summary>FYDF806 レコード中の SEP(yoyaku 先頭 3 文字="SEP")レコード数を数える。</summary>
    private static int CountSeparatorRecords(byte[] f806)
    {
        int count = f806.Length / Rl806;
        int sep = 0;
        for (int r = 0; r < count; r++)
        {
            int off = (r * Rl806) + Yoyaku806Offset;
            string yoyaku = Cp932.GetString(f806, off, Yoyaku806Len).TrimEnd(' ', '\u3000', '\0');
            if (yoyaku.StartsWith("SEP", StringComparison.Ordinal))
            {
                sep++;
            }
        }

        return sep;
    }

    /// <summary>WORK ディレクトリの親(EWS ルート)から const ファイル(toku/const/sekkei)を探す。</summary>
    private static string? FindConstFile(string workDir, string fileName)
    {
        string? ewsRoot = Directory.GetParent(workDir)?.FullName;
        if (ewsRoot is null)
        {
            return null;
        }

        string path = Path.Combine(ewsRoot, "toku", "const", "sekkei", fileName);
        return File.Exists(path) ? path : null;
    }

    private static byte[]? ReadRecordFile(string projDir, string prefix, int recordLength)
    {
        string name = Path.GetFileName(projDir);
        string path = Path.Combine(projDir, $"{prefix}.{name}");
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] b = File.ReadAllBytes(path);
        if (b.Length == 0 || b.Length % recordLength != 0)
        {
            return null;
        }

        return b;
    }

    private static IEnumerable<string> EnumerateProjects(string workDir)
    {
        return Directory.EnumerateDirectories(workDir)
            .OrderBy(d => d, StringComparer.Ordinal)
            .Take(MaxProjects);
    }

    /// <summary>
    /// テスト実行ディレクトリから上位へ辿り、案件データ(EWS/WORK)を探す。
    /// 見つからなければ null(検証スキップ)。
    /// </summary>
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
