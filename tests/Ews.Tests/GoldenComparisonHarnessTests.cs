using System.Text;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Common;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// ゴールデン突合(型式展開の C 版出力 vs C# 版出力)ハーネス。
///
/// 実案件データ(AIX 実機が生成した固定長ファイル)を基準に、移植した
/// <c>key_check</c>(<see cref="ElectricalParameterChecker"/>)→<c>eparm_set</c>
/// (<see cref="EquipmentParameterFormatter"/>)→ eparmg 固定長化(<see cref="EparmgCodec"/>)の
/// パイプラインが C 版の出力(FYDF806 の ep[0])を再現することを検証する。
///
/// 【C原典・レイアウト】
///   - FYDF805(回路内容記述, RL=270): key(12)+gyosyu[5]@+12+kairoar[200]@+17。
///   - FYDF806(主回路データ, RL=1219): key(12)+syukairo。yoyaku[8]@+38、ep[0]@+114(253 バイト)。
///     ep[1]@+367、ep[2]@+620(いずれも eparmg 253 バイト)。
///
/// 基準データは本リポジトリ外(EWS/WORK 配下)にあるため、未配置の環境では
/// 検証をスキップする(テストは成功扱い)。
/// </summary>
public sealed class GoldenComparisonHarnessTests
{
    private const int Rl805 = 270;
    private const int Rl806 = 1219;
    private const int Yoyaku806Offset = 38;
    private const int Yoyaku806Len = 8;
    private const int Ep0Offset = 114;
    private const int Kairoar805Offset = 17;
    private const int Kairoar805Len = 200;

    /// <summary>検証対象とする案件数の上限(テスト時間を抑えるためのサンプル)。</summary>
    private const int MaxProjects = 150;

    private static readonly Encoding Cp932 = FixedFieldCodec.ShiftJis;

    private readonly ITestOutputHelper _output;

    public GoldenComparisonHarnessTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// eparmg の固定長レイアウト(253 バイト・宣言順)が実機出力と一致することを、
    /// 実 FYDF806 の ep[0..2] を「復元→再直列化」して往復一致で検証する。
    /// </summary>
    [Fact]
    public void Eparmg固定長コーデックは実FYDF806のバイト列を往復再現する()
    {
        string? work = FindWorkDir();
        if (work is null)
        {
            _output.WriteLine("WORK ディレクトリ未配置のため検証をスキップします。");
            return;
        }

        int projects = 0;
        int records = 0;
        foreach (string projDir in EnumerateProjects(work))
        {
            byte[]? b = ReadRecordFile(projDir, "FYDF806", Rl806);
            if (b is null)
            {
                continue;
            }

            int count = b.Length / Rl806;
            for (int r = 0; r < count; r++)
            {
                int recOff = r * Rl806;
                for (int slot = 0; slot < 3; slot++)
                {
                    int off = recOff + Ep0Offset + (slot * EparmgCodec.RecordLength);
                    ReadOnlySpan<byte> slice = b.AsSpan(off, EparmgCodec.RecordLength);
                    ElectricalParameters ep = EparmgCodec.Deserialize(slice);
                    byte[] round = EparmgCodec.Serialize(ep);
                    if (!slice.SequenceEqual(round))
                    {
                        Assert.Fail(
                            $"往復不一致: proj={Path.GetFileName(projDir)} rec={r} slot={slot}\n" +
                            $"  in =[{Cp932.GetString(slice)}]\n" +
                            $"  out=[{Cp932.GetString(round)}]");
                    }

                    records++;
                }
            }

            projects++;
        }

        _output.WriteLine($"往復検証: 案件={projects} eparmg レコード={records}");
        Assert.True(records > 0, "検証対象の FYDF806 レコードが見つかりませんでした。");
    }

    /// <summary>
    /// 単一機器の回路行(複合オプション・数量展開を含まない行)について、
    /// key_check→eparm_set で得た eparmg が、実 FYDF806 の同一予約語 ep[0] 群のいずれかと
    /// 「eparm_set が実際に書き換えたフィールド」で一致することを検証する。
    /// (手配数量 QTY・盤種類 BN 等は eparm_set 対象外で外部設定のため比較対象から除外される。)
    /// </summary>
    [Fact]
    public void 単一機器回路はkey_checkとeparm_setで実FYDF806のep0を再現する()
    {
        string? work = FindWorkDir();
        if (work is null)
        {
            _output.WriteLine("WORK ディレクトリ未配置のため検証をスキップします。");
            return;
        }

        var checker = new ElectricalParameterChecker();
        var formatter = new EquipmentParameterFormatter();
        byte[] defaultBytes = EparmgCodec.Serialize(new ElectricalParameters());

        int considered = 0;
        int matched = 0;
        var contentMismatches = new List<string>();
        int noCounterpart = 0;
        var noCounterpartSamples = new HashSet<string>(StringComparer.Ordinal);

        foreach (string projDir in EnumerateProjects(work))
        {
            byte[]? b805 = ReadRecordFile(projDir, "FYDF805", Rl805);
            byte[]? b806 = ReadRecordFile(projDir, "FYDF806", Rl806);
            if (b805 is null || b806 is null)
            {
                continue;
            }

            // 予約語 → 実 ep[0] バイト列群
            var map = new Dictionary<string, List<byte[]>>(StringComparer.Ordinal);
            int rec806 = b806.Length / Rl806;
            for (int r = 0; r < rec806; r++)
            {
                int off = r * Rl806;
                string yoy = Cp932.GetString(b806, off + Yoyaku806Offset, Yoyaku806Len).TrimEnd();
                if (yoy.Length == 0)
                {
                    continue;
                }

                byte[] ep0 = new byte[EparmgCodec.RecordLength];
                Array.Copy(b806, off + Ep0Offset, ep0, 0, EparmgCodec.RecordLength);
                if (!map.TryGetValue(yoy, out List<byte[]>? list))
                {
                    list = new List<byte[]>();
                    map[yoy] = list;
                }

                list.Add(ep0);
            }

            int rec805 = b805.Length / Rl805;
            for (int r = 0; r < rec805; r++)
            {
                int off = r * Rl805;
                string kairoar = Cp932.GetString(b805, off + Kairoar805Offset, Kairoar805Len).TrimEnd();
                if (!TrySimpleDevice(kairoar, out string device))
                {
                    continue;
                }

                if (!TryResolveReserved(checker, device, out string yoyaku, out string parameter))
                {
                    continue;
                }

                short rc = checker.CheckParameters(yoyaku, parameter, out RatingValues values, out _);
                if (rc == -1)
                {
                    continue;
                }

                ElectricalParameters myEp = formatter.EparmSet(values, yoyaku);
                byte[] myBytes = EparmgCodec.Serialize(myEp);

                // eparm_set が既定値から書き換えたバイト位置のみを比較対象にする。
                List<int> changed = new();
                for (int i = 0; i < myBytes.Length; i++)
                {
                    if (myBytes[i] != defaultBytes[i])
                    {
                        changed.Add(i);
                    }
                }

                if (changed.Count == 0)
                {
                    continue; // 空分岐(eparm_set が何も設定しない予約語)は検証対象外。
                }

                considered++;

                if (!map.TryGetValue(yoyaku, out List<byte[]>? candidates))
                {
                    // 予約語が主回路 FYDF806 に存在しない = 制御回路のみに現れる機器(CU/TS/PBS 等)。
                    // 主回路の ep[0] 検証の対象外として情報記録のみ行う(不一致とはしない)。
                    noCounterpart++;
                    noCounterpartSamples.Add(yoyaku);
                    continue;
                }

                bool found = candidates.Any(real => changed.All(i => real[i] == myBytes[i]));
                if (found)
                {
                    matched++;
                }
                else
                {
                    contentMismatches.Add(
                        $"{Path.GetFileName(projDir)} [{kairoar}] 予約語={yoyaku} パラメータ={parameter}: " +
                        $"生成=[{Cp932.GetString(myBytes)}]");
                }
            }
        }

        _output.WriteLine(
            $"単一機器突合: 対象={considered} 一致={matched} 内容不一致={contentMismatches.Count} " +
            $"主回路対象外(予約語={string.Join(",", noCounterpartSamples.OrderBy(s => s, StringComparer.Ordinal))})={noCounterpart}");
        foreach (string m in contentMismatches.Take(30))
        {
            _output.WriteLine("  " + m);
        }

        Assert.True(considered > 0, "検証対象の単一機器回路が見つかりませんでした。");
        Assert.True(matched > 0, "1 件も突合できませんでした。");
        Assert.True(
            contentMismatches.Count == 0,
            $"{contentMismatches.Count}/{considered} 件で eparm_set 出力が実 FYDF806 の ep[0] と不一致でした(先頭は出力ログ参照)。");
    }

    // ── 補助 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 回路内容記述から単一機器の記述を取り出す。複合オプション('+'・'('・'-'・'='・'/')や
    /// 全角文字を含む行、非機器行(NP/P 等)は対象外(false)。'*' 以降(数量)は切り捨てる。
    /// </summary>
    private static bool TrySimpleDevice(string kairoar, out string device)
    {
        device = string.Empty;
        int star = kairoar.IndexOf('*');
        string head = (star >= 0 ? kairoar[..star] : kairoar).Trim();
        if (head.Length == 0)
        {
            return false;
        }

        foreach (char c in head)
        {
            bool ok = c is '.' || (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
            if (!ok)
            {
                return false;
            }
        }

        device = head;
        return true;
    }

    /// <summary>
    /// 機器記述の先頭最長一致で予約語を確定する(定格キー表に存在する最長の接頭辞)。
    /// 【C原典】Yoyaku_Check_Main の最長一致。残りを電気パラメータとする。
    /// </summary>
    private static bool TryResolveReserved(ElectricalParameterChecker checker, string device, out string yoyaku, out string parameter)
    {
        int max = Math.Min(device.Length, Yoyaku806Len);
        for (int len = max; len >= 1; len--)
        {
            string prefix = device[..len];
            if (checker.IsSupported(prefix))
            {
                yoyaku = prefix;
                parameter = device[len..];
                return true;
            }
        }

        yoyaku = string.Empty;
        parameter = string.Empty;
        return false;
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
