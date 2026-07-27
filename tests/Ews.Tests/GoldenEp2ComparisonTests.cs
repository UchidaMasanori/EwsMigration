using System.Text;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Common;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// ゴールデン突合(Fyss14 SetParam_ep2 の ep[2]=システム側生成値)ハーネス。
///
/// 実 FYDF806 の各レコードから入力側(ep[0]/回路電気値 kpa*/yoyaku)を抽出し、移植した
/// <see cref="SecondaryParameterSetter.SetParam_ep2"/> ディスパッチャ(自己完結ケース)が
/// C 版の出力(ep[2])を再現することを、ep[2] の電圧フィールド(V2[0..2]/V2区分)で検証する。
///
/// 【重要・P/E を検証しない理由】ep[2] の極数(epap)・エレメント(epae)は Make_UpperParm の
/// SetParam_ep2 が回路極数 kpap から生成する暫定値だが、最終 FYDF806 の ep[2] はその後の
/// 機器選定(eparm_set 相当)が選定機器の実極数・実エレメント数で上書きする。実データ計測でも
/// ep[2].epae は SetParam_ep2 の算出値より ep[0].epae(=入力機器のエレメント数)に一致する
/// 割合が高い(MCB/ELB 標本で ep[2].E==ep[0].E が約 97%、==SetParam_ep2_MCB_E は約 52%)。
/// 特に 105V・単相2線(kpap=1)で SetParam_ep2 は epae='1' とするが、実機は 2 極のため ep[2].E='2'。
/// このため P/E は最終 FYDF806 では SetParam_ep2 の出力を反映せず突合できない
/// (ディスパッチャの P/E ロジックは C 原典に忠実で単体テストで検証)。
/// 電圧 V2 は回路電圧そのもので機器選定後も不変のため実データで堂々と突合できる。
///
/// 【C原典・レイアウト(FYDF806 RL=1219, key(12)+syukairo)】
///   yoyaku[8]@+38 / ep[0](eparmg 253)@+114 / ep[2](eparmg 253)@+620(=114+253×2) /
///   kpahz@+1137 / kpav[3][3]@+1140 / kpavkbn@+1149。
///
/// 記録列/物件/未移植リーフ依存の予約語(RTR/WH/VM/VT/TR/TB/WL/LGR/ELR/TS/DCPW/NHMB 等)は
/// ディスパッチャ未収録のため突合対象外。電圧を設定しない LGT(極数のみ)も対象外。
/// MC はディスパッチャ収録済(V2=MCB_V2/AC/BC)だが、ep[2].V2 も後段の機器選定が 2 次側(子機器)の
/// 実電圧で上書きするため突合対象外(実測: MC の記録側 kpav=105 に対し ep[2].V2=220)。
/// VM もディスパッチャ収録済だが、本ハーネスが VM の入力(kiryoso/kpakv1/datatype[1])を供給しない
/// ため突合対象外(単体テストで検証)。
/// 基準データ未配置の環境ではスキップする。
/// </summary>
public sealed class GoldenEp2ComparisonTests
{
    private const int Rl806 = 1219;
    private const int Ksyubetu806 = 21;
    private const int Yoyaku806 = 38;
    private const int Yoyaku806Len = 8;
    private const int Ep0806 = 114;
    private const int Ep2806 = 620;
    private const int Kpaph806 = 1135;
    private const int Kpawr806 = 1136;
    private const int Kpahz806 = 1137;
    private const int Kpap806 = 1139;
    private const int Kpav806 = 1140;
    private const int Kpavkbn806 = 1149;

    /// <summary>ディスパッチャが収録済みかつ電圧 V2 を設定する自己完結予約語(LGT=極数のみ/VS・AS=相線式のみは除く)。</summary>
    private static readonly HashSet<string> Dispatched = new(StringComparer.Ordinal)
    {
        "MCB", "ELB", "MMCB", "ELMB", "RMCB", "RELB", "RMMCB", "RELMB",
        "SB", "THR", "MG", "SC", "NT", "RRY", "MCDT", "F", "CP",
        "HM", "ZCT", "CKS", "CSDT", "SSW", "TSW", "FL", "LSW", "DSW",
        "LA", "CON", "HPSB", "HSB",
    };

    private static readonly Encoding Cp932 = FixedFieldCodec.ShiftJis;

    private readonly ITestOutputHelper _output;

    public GoldenEp2ComparisonTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void SetParam_ep2のep2が実FYDF806と一致する()
    {
        string? work = FindWorkDir();
        if (work is null)
        {
            _output.WriteLine("WORK ディレクトリ未配置のため検証をスキップします。");
            return;
        }

        int projects = 0;
        int total = 0;
        int match = 0;
        var mismatchByYoyaku = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var fieldMismatch = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var samples = new List<string>();

        foreach (string projDir in EnumerateProjects(work))
        {
            byte[]? b = ReadRecordFile(projDir, "FYDF806", Rl806);
            if (b is null)
            {
                continue;
            }

            projects++;
            int count = b.Length / Rl806;
            for (int r = 0; r < count; r++)
            {
                int o = r * Rl806;

                // P 系統のみ(ksyubetu=='1')。
                if (b[o + Ksyubetu806] != (byte)'1')
                {
                    continue;
                }

                string yoyaku = Cp932.GetString(b, o + Yoyaku806, Yoyaku806Len).TrimEnd(' ', '\0');
                if (!Dispatched.Contains(yoyaku))
                {
                    continue;
                }

                var data = MainCircuitData.Create();
                data.ReservedWord = yoyaku;
                data.ElectricalParameterSlots[0] = EparmgCodec.Deserialize(b.AsSpan(o + Ep0806, EparmgCodec.RecordLength));
                data.ElectricalParameterSlots[1] = new ElectricalParameters();
                data.ElectricalParameterSlots[2] = new ElectricalParameters();
                data.CircuitPhaseCount = (char)b[o + Kpaph806];
                data.CircuitWireType = (char)b[o + Kpawr806];
                data.CircuitFrequency = Cp932.GetString(b, o + Kpahz806, 2);
                data.CircuitPoleCount = (char)b[o + Kpap806];
                data.CircuitVoltage[0] = Cp932.GetString(b, o + Kpav806, 3);
                data.CircuitVoltage[1] = Cp932.GetString(b, o + Kpav806 + 3, 3);
                data.CircuitVoltage[2] = Cp932.GetString(b, o + Kpav806 + 6, 3);
                data.CircuitVoltageKind = (char)b[o + Kpavkbn806];

                ElectricalParameters expected = EparmgCodec.Deserialize(b.AsSpan(o + Ep2806, EparmgCodec.RecordLength));

                SecondaryParameterSetter.SetParam_ep2(data);
                ElectricalParameters actual = data.ElectricalParameterSlots[2];

                total++;
                var diffs = new List<string>();
                // 電圧 V2 のみ突合する。P/E は機器選定が実機値で上書きするため
                // 最終 FYDF806 では SetParam_ep2 の出力を反映しない(クラス doc 参照)。
                CheckField(diffs, "V2_0", actual.V2[0], expected.V2[0]);
                CheckField(diffs, "V2_1", actual.V2[1], expected.V2[1]);
                CheckField(diffs, "V2_2", actual.V2[2], expected.V2[2]);
                CheckField(diffs, "V2Kbn", actual.V2Kbn.ToString(), expected.V2Kbn.ToString());

                if (diffs.Count == 0)
                {
                    match++;
                }
                else
                {
                    mismatchByYoyaku[yoyaku] = mismatchByYoyaku.GetValueOrDefault(yoyaku) + 1;
                    foreach (string d in diffs)
                    {
                        string key = $"{yoyaku}.{d.Split('=')[0]}";
                        fieldMismatch[key] = fieldMismatch.GetValueOrDefault(key) + 1;
                    }

                    if (samples.Count < 40)
                    {
                        samples.Add($"{Path.GetFileName(projDir)} #{r + 1} [{yoyaku}] {string.Join(" ", diffs)}");
                    }
                }
            }
        }

        _output.WriteLine($"案件={projects} 突合対象={total} 一致={match}");
        _output.WriteLine("── 予約語別不一致数 ──");
        foreach (KeyValuePair<string, int> kv in mismatchByYoyaku)
        {
            _output.WriteLine($"  [{kv.Key}] x{kv.Value}");
        }

        _output.WriteLine("── フィールド別不一致数 ──");
        foreach (KeyValuePair<string, int> kv in fieldMismatch)
        {
            _output.WriteLine($"  {kv.Key} x{kv.Value}");
        }

        _output.WriteLine("── 不一致サンプル ──");
        foreach (string s in samples)
        {
            _output.WriteLine("  " + s);
        }

        Assert.True(total > 0, "ディスパッチャ対象の ep[2] レコードが見つかりませんでした。");

        // ep[2] の電圧 V2(V2[0..2]/V2区分)は kpap 非依存のため全一致を要求する。
        Assert.True(
            match == total,
            $"ep[2] の電圧 V2 が {total - match}/{total} 件で実 FYDF806 と不一致でした(先頭はログ参照)。\n" +
            string.Join("\n", samples));
    }

    private static void CheckField(List<string> diffs, string name, string actual, string expected)
    {
        // eparmg の同一フィールドを Deserialize 済み文字列で比較(桁詰め・末尾差異は Codec が吸収)。
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            diffs.Add($"{name}=(計[{actual}]≠実[{expected}])");
        }
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
            .OrderBy(d => d, StringComparer.Ordinal);
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
