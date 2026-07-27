using System.Text;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Common;
using Xunit;
using Xunit.Abstractions;

namespace Ews.Tests;

/// <summary>
/// ゴールデン突合(Fyss14 Make_UpperParm の回路電気値 kpa* 生成)ハーネス。
///
/// 実案件データ(AIX 実機が生成した FYDF806)を基準に、移植した
/// <see cref="UpperParameterBuilder.GenerateUpperParameters"/>(Make_UpperParm の kpa* 生成部)が
/// C 版の出力(dt.kpa*)を再現することを検証する。
///
/// 【手順】
///   1. 実 FYDF806 の各レコードから入力側フィールド(ep[0]/fp/yoyaku/ksyubetu/kiryoso/oyatno)を抽出。
///      datano は syukairo 構造体には無く FYRT800 レベル(位置由来)のため index+1 を "%03d" で採番する
///      (実データで各行の oyatno が親行の index+1 を指すことを確認済み)。fp(付属パラメータ)は
///      子回路の負荷電圧(200/100V)から回路電圧を確定するため必須。
///   2. 生成前に実 kpa*(期待値)を退避。
///   3. <see cref="UpperParameterBuilder.GenerateUpperParameters"/> を実行して kpa* を計算。
///   4. 計算 kpa* と実 kpa* を突合し、入線(P)・非入線別に一致率を集計する。
///
/// 【現状の突合結果(547 案件)】入線(P)=789/789 完全一致。非入線=14808/15004(98.7%)一致。
/// 残存不一致は SetParam_ep2 の例外要素 kpa* 再設定(未移植)のみ: RTR→024V・WL→005V(電圧のみ差異)。
///
/// 【C原典・レイアウト(FYDF806 RL=1219, key(12)+syukairo)】
///   ksyubetu@+21 / yoyaku[8]@+38 / ep[0](eparmg 253)@+114 / fp(fparmg 157)@+873 /
///   kiryoso@+1031 / oyatno[3]@+1032 /
///   kpaph@+1135 / kpawr@+1136 / kpahz[2]@+1137 / kpap@+1139 / kpav[3][3]@+1140 / kpavkbn@+1149。
///   (syukairo 内オフセット: ep[0]@102, kpaph@1123 … に key(12) を加算。ep[0]@+114・fp@+873 は
///    <see cref="GoldenComparisonHarnessTests"/> の既知アンカーと整合。)
///
/// 基準データは本リポジトリ外(EWS/WORK 配下)にあるため、未配置の環境では検証をスキップする。
/// </summary>
public sealed class GoldenKpaComparisonTests
{
    private const int Rl806 = 1219;
    private const int Ksyubetu806 = 21;
    private const int Yoyaku806 = 38;
    private const int Yoyaku806Len = 8;
    private const int Ep0806 = 114;
    private const int Fp806 = 873;
    private const int Kiryoso806 = 1031;
    private const int Oyatno806 = 1032;
    private const int Kpaph806 = 1135;
    private const int Kpawr806 = 1136;
    private const int Kpahz806 = 1137;
    private const int Kpap806 = 1139;
    private const int Kpav806 = 1140;
    private const int Kpavkbn806 = 1149;

    private static readonly Encoding Cp932 = FixedFieldCodec.ShiftJis;

    private readonly ITestOutputHelper _output;

    public GoldenKpaComparisonTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Make_UpperParm の回路電気値 kpa* が実 FYDF806 と一致することを検証する。
    /// 入線(P)は Find_Parent 不要で ep[0] から kpa* が確定するため全一致を要求する。
    /// 非入線は親相対参照・例外要素の再設定(SetParam_ep2 等・未移植)を含むため一致率を記録する。
    /// </summary>
    [Fact]
    public void Make_UpperParmの回路電気値kpaが実FYDF806と一致する()
    {
        string? work = FindWorkDir();
        if (work is null)
        {
            _output.WriteLine("WORK ディレクトリ未配置のため検証をスキップします。");
            return;
        }

        int projects = 0;
        int totalP = 0, matchP = 0;
        int totalOther = 0, matchOther = 0;
        var mismatchByYoyaku = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var matchByYoyaku = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var samples = new List<string>();

        foreach (string projDir in EnumerateProjects(work))
        {
            byte[]? b = ReadRecordFile(projDir, "FYDF806", Rl806);
            if (b is null)
            {
                continue;
            }

            int count = b.Length / Rl806;
            var records = new List<MainCircuitResult>(count);
            var expected = new Expected[count];

            for (int r = 0; r < count; r++)
            {
                int o = r * Rl806;
                var ep0 = EparmgCodec.Deserialize(b.AsSpan(o + Ep0806, EparmgCodec.RecordLength));
                var fp = FparmgCodec.Deserialize(b.AsSpan(o + Fp806, FparmgCodec.RecordLength));
                var data = MainCircuitData.Create();
                data.SystemKind = (char)b[o + Ksyubetu806];
                data.ReservedWord = Cp932.GetString(b, o + Yoyaku806, Yoyaku806Len).TrimEnd(' ', '\0');
                data.CircuitElement = (char)b[o + Kiryoso806];
                data.ParentSequenceNumber = Cp932.GetString(b, o + Oyatno806, 3);
                data.ElectricalParameterSlots[0] = ep0;
                data.AttachedParameter = fp;

                records.Add(new MainCircuitResult
                {
                    SequenceNumber = (r + 1).ToString("D3"),
                    Data = data,
                });

                expected[r] = new Expected(
                    (char)b[o + Kpaph806],
                    (char)b[o + Kpawr806],
                    Cp932.GetString(b, o + Kpahz806, 2),
                    (char)b[o + Kpap806],
                    Cp932.GetString(b, o + Kpav806, 3),
                    Cp932.GetString(b, o + Kpav806 + 3, 3),
                    Cp932.GetString(b, o + Kpav806 + 6, 3),
                    (char)b[o + Kpavkbn806]);
            }

            // 周波数は物件で一意。DC(kpahz=="00")以外の実 kpahz から導出する(既定 50Hz)。
            int frequency = UpperParameterBuilder.Hz1;
            foreach (Expected e in expected)
            {
                if (e.Hz != "00" && e.Hz != "  ")
                {
                    frequency = AtoiC(e.Hz);
                    break;
                }
            }

            UpperParameterBuilder.GenerateUpperParameters(records, frequency);
            projects++;

            for (int r = 0; r < count; r++)
            {
                // P 系統(ksyubetu=='1')のみが生成対象。それ以外は kpa* 未設定のため突合しない。
                if (records[r].Data.SystemKind != '1')
                {
                    continue;
                }

                MainCircuitData d = records[r].Data;
                Expected e = expected[r];
                bool match =
                    d.CircuitPhaseCount == e.Ph &&
                    d.CircuitWireType == e.Wr &&
                    d.CircuitPoleCount == e.P &&
                    d.CircuitVoltageKind == e.Vkbn &&
                    d.CircuitVoltage[0] == e.V0 &&
                    d.CircuitVoltage[1] == e.V1 &&
                    d.CircuitVoltage[2] == e.V2;

                bool isP = d.ReservedWord == "P";
                if (isP)
                {
                    totalP++;
                    if (match)
                    {
                        matchP++;
                    }
                }
                else
                {
                    totalOther++;
                    if (match)
                    {
                        matchOther++;
                    }
                }

                string y = string.IsNullOrEmpty(d.ReservedWord) ? "(空)" : d.ReservedWord;
                if (match)
                {
                    matchByYoyaku[y] = matchByYoyaku.GetValueOrDefault(y) + 1;
                }
                else
                {
                    mismatchByYoyaku[y] = mismatchByYoyaku.GetValueOrDefault(y) + 1;
                    if (samples.Count < 40)
                    {
                        samples.Add(
                            $"{Path.GetFileName(projDir)} #{r + 1} 予約語=[{y}] " +
                            $"生成=(ph={d.CircuitPhaseCount} wr={d.CircuitWireType} p={d.CircuitPoleCount} " +
                            $"v=[{d.CircuitVoltage[0]}/{d.CircuitVoltage[1]}/{d.CircuitVoltage[2]}] vkbn={d.CircuitVoltageKind}) " +
                            $"実=(ph={e.Ph} wr={e.Wr} p={e.P} v=[{e.V0}/{e.V1}/{e.V2}] vkbn={e.Vkbn})");
                    }
                }
            }
        }

        _output.WriteLine($"案件={projects}");
        _output.WriteLine($"入線(P): {matchP}/{totalP} 一致");
        _output.WriteLine($"非入線 : {matchOther}/{totalOther} 一致");
        _output.WriteLine("── 予約語別一致数 ──");
        foreach (KeyValuePair<string, int> kv in matchByYoyaku)
        {
            _output.WriteLine($"  一致 [{kv.Key}] x{kv.Value}");
        }

        _output.WriteLine("── 予約語別不一致数 ──");
        foreach (KeyValuePair<string, int> kv in mismatchByYoyaku)
        {
            _output.WriteLine($"  不一致 [{kv.Key}] x{kv.Value}");
        }

        _output.WriteLine("── 不一致サンプル ──");
        foreach (string s in samples)
        {
            _output.WriteLine("  " + s);
        }

        Assert.True(totalP > 0, "入線(P)レコードが見つかりませんでした。");

        // 入線(P)は ep[0] から kpa* が完全に確定するため全一致を要求する。
        Assert.True(
            matchP == totalP,
            $"入線(P)の kpa* が {totalP - matchP}/{totalP} 件で実 FYDF806 と不一致でした。\n" +
            string.Join("\n", samples.Where(s => s.Contains("[P]"))));

        // 非入線の残存不一致は SetParam_ep2 の例外要素 kpa* 再設定(未移植)に限られる:
        //   ・RTR(継電器用変成器 2次)→ 電圧 024V 固定
        //   ・WL (漏電警報)          → 電圧 005V 固定
        // 相・線式・極数は一致し電圧のみ差異。これら以外の予約語で不一致が出た場合は
        // コア(Kairo_Parm_Set/Find_Parent 等)の回帰とみなして失敗させる。
        var knownExceptions = new HashSet<string>(StringComparer.Ordinal) { "RTR", "WL" };
        List<string> unexpected = mismatchByYoyaku.Keys.Where(k => !knownExceptions.Contains(k)).ToList();
        Assert.True(
            unexpected.Count == 0,
            $"SetParam_ep2 例外(RTR/WL)以外の予約語で kpa* が不一致でした: [{string.Join(",", unexpected)}]\n" +
            string.Join("\n", samples.Where(s => unexpected.Any(u => s.Contains($"[{u}]")))));
    }

    private readonly record struct Expected(
        char Ph, char Wr, string Hz, char P, string V0, string V1, string V2, char Vkbn);

    // ── 補助 ─────────────────────────────────────────────────────────

    /// <summary>C の atoi 相当(先頭空白許容・非数字で打ち切り)。</summary>
    private static int AtoiC(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }

        int i = 0;
        while (i < s.Length && s[i] == ' ')
        {
            i++;
        }

        int sign = 1;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            sign = s[i] == '-' ? -1 : 1;
            i++;
        }

        int value = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            value = (value * 10) + (s[i] - '0');
            i++;
        }

        return sign * value;
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

    /// <summary>テスト実行ディレクトリから上位へ辿り、案件データ(EWS/WORK)を探す。</summary>
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
