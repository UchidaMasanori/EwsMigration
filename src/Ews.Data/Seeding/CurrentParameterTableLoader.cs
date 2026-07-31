using System.Globalization;
using System.Text;
using Ews.Domain.Analysis;

namespace Ews.Data.Seeding;

/// <summary>
/// 電流パラメータ関連コンスタントファイル(amp001.cns～amp004.cns)を読み込み、
/// <see cref="ParameterSettingType"/> / <see cref="WireSizeSetting"/> /
/// <see cref="RatedCurrent2Setting"/> / <see cref="RatedCurrent1Setting"/> の一覧を生成する。
///
/// 【C原典】Fyss3G_CnsPrmtpRead / CnsSQsetRead / CnsA2setRead / CnsA1setRead
///   (toku/sekkei/src/Fyss3G.c)。各関数は先頭 2 行を読み飛ばし(iflg&gt;2)、
///   3 行目以降を <c>sscanf</c> で解析して線形リストへ追加する。
///
/// 【忠実性の要点】
///   ・C は <c>fgets</c> で取得した各行に対し(<c>iflg&gt;2</c> なら) <c>sscanf</c> の成否に関わらず
///     必ずノードを 1 個追加する。<c>sscanf</c> が途中で失敗すると、以降のスタック変数は
///     直前行の値をそのまま持ち越す(桁落ちキャリー)。本移植はこの挙動を再現する。
///   ・特に amp003.cns の 2 文字負荷種類(HA/FL/…)は書式 <c>"%2s %lf,%hd,%2s"</c> の
///     幅 2 指定で負荷種類直後のカンマが %lf に渡り変換失敗するため、係数・電圧・相数が
///     直前行から持ち越される(例: HA は係数 1.4 ではなく直前 S 行の 1.0 になる)。
///   ・amp004.cns はコメントが 3 行あるが C は先頭 2 行のみ読み飛ばすため、3 行目の
///     "/*    3.0   */" は数値行として解析され変換失敗→係数 0 の先頭エントリが生じる。
///   本移植では末尾の改行由来の空要素のみ落とし、それ以外の(空白行含む)行は C 同様に処理する。
/// </summary>
public static class CurrentParameterTableLoader
{
    /// <summary>先頭に読み飛ばす行数(コメント 2 行)。【C原典】iflg &gt; 2。</summary>
    private const int HeaderLineCount = 2;

    // ---------------------------------------------------------------------
    //  amp001.cns : パラメータ設定タイプ
    // ---------------------------------------------------------------------

    /// <summary>amp001.cns を CP932 として読み込み、パラメータ設定タイプ一覧を返す。</summary>
    public static IReadOnlyList<ParameterSettingType> LoadParameterSettingTypes(string path) =>
        ParseParameterSettingTypes(ReadCp932(path, "パラメータ設定タイプ"));

    /// <summary>amp001.cns のテキスト内容を解析する。【C原典】Fyss3G_CnsPrmtpRead。</summary>
    public static IReadOnlyList<ParameterSettingType> ParseParameterSettingTypes(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var result = new List<ParameterSettingType>();

        // sscanf のスタック変数持ち越しを再現するキャリー領域。
        string reservedWord = string.Empty;
        int seqNo = 0;
        int prmTp = 0;
        int[] cod = new int[10];

        foreach (string line in DataLines(content))
        {
            int pos = 0;
            // 【C原典】%8s(予約語)。Strset で先頭カンマまでを採用。
            string? token = ReadFixedWidthToken(line, ref pos, 8);
            if (token is not null)
            {
                reservedWord = TrimAtComma(token);
            }

            // 【C原典】" %hd,%hd,%hd×10"(seq_no, prm_tp, cod[0..9])。%8s が読んだ
            // 予約語トークンはカンマを含むため、先頭 %hd の前にカンマ照合は不要。
            // sscanf は最初の変換失敗で以降を中断するため、失敗以降は直前値を持ち越す。
            if (token is not null && TryScanInt(line, ref pos, out int seq))
            {
                seqNo = seq;
                if (SkipLiteralComma(line, ref pos) && TryScanInt(line, ref pos, out int pt))
                {
                    prmTp = pt;
                    for (int i = 0; i < 10; i++)
                    {
                        if (SkipLiteralComma(line, ref pos) && TryScanInt(line, ref pos, out int cv))
                        {
                            cod[i] = cv;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            result.Add(new ParameterSettingType(reservedWord, seqNo, prmTp, (int[])cod.Clone()));
        }

        return result;
    }

    // ---------------------------------------------------------------------
    //  amp002.cns : 電線サイズ
    // ---------------------------------------------------------------------

    /// <summary>amp002.cns を CP932 として読み込み、電線サイズ設定一覧を返す。</summary>
    public static IReadOnlyList<WireSizeSetting> LoadWireSizeSettings(string path) =>
        ParseWireSizeSettings(ReadCp932(path, "電線サイズ設定"));

    /// <summary>amp002.cns のテキスト内容を解析する。【C原典】Fyss3G_CnsSQsetRead。</summary>
    public static IReadOnlyList<WireSizeSetting> ParseWireSizeSettings(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var result = new List<WireSizeSetting>();

        double sq = 0.0;
        double denryu = 0.0;
        int sentei = 0;

        foreach (string line in DataLines(content))
        {
            // 【C原典】sscanf(buff, "%lf,%lf,%hd", &sq, &denryu, &sentei)。
            // 最初の変換失敗で以降を中断し、直前値を持ち越す。
            int pos = 0;
            if (TryScanDouble(line, ref pos, out double d0))
            {
                sq = d0;
                if (SkipLiteralComma(line, ref pos) && TryScanDouble(line, ref pos, out double d1))
                {
                    denryu = d1;
                    if (SkipLiteralComma(line, ref pos) && TryScanInt(line, ref pos, out int i0))
                    {
                        sentei = i0;
                    }
                }
            }

            result.Add(new WireSizeSetting(sq, denryu, sentei));
        }

        return result;
    }

    // ---------------------------------------------------------------------
    //  amp003.cns : 定格電流２
    // ---------------------------------------------------------------------

    /// <summary>amp003.cns を CP932 として読み込み、定格電流２設定一覧を返す。</summary>
    public static IReadOnlyList<RatedCurrent2Setting> LoadRatedCurrent2Settings(string path) =>
        ParseRatedCurrent2Settings(ReadCp932(path, "定格電流２設定"));

    /// <summary>amp003.cns のテキスト内容を解析する。【C原典】Fyss3G_CnsA2setRead。</summary>
    public static IReadOnlyList<RatedCurrent2Setting> ParseRatedCurrent2Settings(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var result = new List<RatedCurrent2Setting>();

        // 【C原典】work/a2/kpa/kpaph はスタック変数で行をまたいで値を持ち越す。
        string work = string.Empty;
        double a2 = 0.0;
        int kpa = 0;
        string kpaph = string.Empty;

        foreach (string line in DataLines(content))
        {
            // 【C原典】sscanf(buff, "%2s %lf,%hd,%2s", work, &a2, &kpa, kpaph)。
            // 幅 2 指定のため 2 文字負荷種類(HA/FL/…)は直後のカンマが %lf に渡り変換失敗し、
            // 以降(a2/kpa/kpaph)を中断=直前行の値を持ち越す(HA は係数 1.4 でなく直前値)。
            int pos = 0;
            string? token = ReadFixedWidthToken(line, ref pos, 2);
            if (token is not null)
            {
                work = token;
                if (TryScanDouble(line, ref pos, out double d))
                {
                    a2 = d;
                    if (SkipLiteralComma(line, ref pos) && TryScanInt(line, ref pos, out int v))
                    {
                        kpa = v;
                        if (SkipLiteralComma(line, ref pos))
                        {
                            string? phaseToken = ReadFixedWidthToken(line, ref pos, 2);
                            if (phaseToken is not null) { kpaph = phaseToken; }
                        }
                    }
                }
            }

            // 【C原典】Strset(fpalw1, work, 2, 0)=先頭カンマまで。kpaph=kpaph[0]。
            string loadKind = TrimAtComma(work);
            char phase = kpaph.Length > 0 ? kpaph[0] : '\0';
            result.Add(new RatedCurrent2Setting(loadKind, phase, kpa, a2));
        }

        return result;
    }

    // ---------------------------------------------------------------------
    //  amp004.cns : 定格電流１
    // ---------------------------------------------------------------------

    /// <summary>amp004.cns を CP932 として読み込み、定格電流１設定一覧を返す。</summary>
    public static IReadOnlyList<RatedCurrent1Setting> LoadRatedCurrent1Settings(string path) =>
        ParseRatedCurrent1Settings(ReadCp932(path, "定格電流１設定"));

    /// <summary>amp004.cns のテキスト内容を解析する。【C原典】Fyss3G_CnsA1setRead。</summary>
    public static IReadOnlyList<RatedCurrent1Setting> ParseRatedCurrent1Settings(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var result = new List<RatedCurrent1Setting>();

        double key = 0.0;

        foreach (string line in DataLines(content))
        {
            // 【C原典】sscanf(buff, "%lf", &key)。変換失敗時は直前値を持ち越す。
            int pos = 0;
            if (TryScanDouble(line, ref pos, out double d)) { key = d; }

            result.Add(new RatedCurrent1Setting(key));
        }

        return result;
    }

    // ---------------------------------------------------------------------
    //  共通ヘルパ
    // ---------------------------------------------------------------------

    /// <summary>コメント 2 行を除いた解析対象行(fgets 相当)を列挙する。</summary>
    private static IEnumerable<string> DataLines(string content)
    {
        string[] raw = content.Split('\n');

        // 末尾の改行由来の空要素を 1 個だけ落とす(fgets は最終改行の後に行を返さない)。
        int count = raw.Length;
        if (count > 0 && raw[count - 1].Length == 0)
        {
            count--;
        }

        for (int i = HeaderLineCount; i < count; i++)
        {
            string line = raw[i];
            yield return line.EndsWith('\r') ? line[..^1] : line;
        }
    }

    /// <summary>指定パスを CP932 として読み込む。</summary>
    private static string ReadCp932(string path, string label)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{label}コンスタントが見つかりません: {path}", path);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return File.ReadAllText(path, Encoding.GetEncoding(932));
    }

    /// <summary>
    /// C の <c>%Ns</c> 相当。先頭空白を読み飛ばし、最大 <paramref name="width"/> 文字の
    /// 非空白トークンを読む。非空白が 1 文字も無ければ <c>null</c>(照合失敗=持ち越し)。
    /// </summary>
    private static string? ReadFixedWidthToken(string line, ref int pos, int width)
    {
        while (pos < line.Length && char.IsWhiteSpace(line[pos])) { pos++; }
        if (pos >= line.Length) { return null; }

        int start = pos;
        int taken = 0;
        while (pos < line.Length && !char.IsWhiteSpace(line[pos]) && taken < width)
        {
            pos++;
            taken++;
        }
        return line[start..pos];
    }

    /// <summary>先頭カンマまでを採用する(Strset の ',' 打ち切り相当)。</summary>
    private static string TrimAtComma(string token)
    {
        int comma = token.IndexOf(',');
        return comma >= 0 ? token[..comma] : token;
    }

    /// <summary>現在位置のリテラル ',' を消費する(空白は読み飛ばさない)。</summary>
    private static bool SkipLiteralComma(string line, ref int pos)
    {
        if (pos < line.Length && line[pos] == ',')
        {
            pos++;
            return true;
        }
        return false;
    }

    /// <summary>C の <c>%lf</c> 相当。先頭空白を読み飛ばし浮動小数点を読む。</summary>
    private static bool TryScanDouble(string line, ref int pos, out double value)
    {
        value = 0.0;
        int p = pos;
        while (p < line.Length && char.IsWhiteSpace(line[p])) { p++; }

        int start = p;
        if (p < line.Length && (line[p] == '+' || line[p] == '-')) { p++; }

        int digits = 0;
        while (p < line.Length && char.IsAsciiDigit(line[p])) { p++; digits++; }
        if (p < line.Length && line[p] == '.')
        {
            p++;
            while (p < line.Length && char.IsAsciiDigit(line[p])) { p++; digits++; }
        }
        if (digits == 0) { return false; }

        if (p < line.Length && (line[p] == 'e' || line[p] == 'E'))
        {
            int q = p + 1;
            if (q < line.Length && (line[q] == '+' || line[q] == '-')) { q++; }
            int expDigits = 0;
            while (q < line.Length && char.IsAsciiDigit(line[q])) { q++; expDigits++; }
            if (expDigits > 0) { p = q; }
        }

        value = double.Parse(line[start..p], CultureInfo.InvariantCulture);
        pos = p;
        return true;
    }

    /// <summary>C の <c>%hd</c>/<c>%d</c> 相当。先頭空白を読み飛ばし整数を読む。</summary>
    private static bool TryScanInt(string line, ref int pos, out int value)
    {
        value = 0;
        int p = pos;
        while (p < line.Length && char.IsWhiteSpace(line[p])) { p++; }

        int start = p;
        if (p < line.Length && (line[p] == '+' || line[p] == '-')) { p++; }

        int digits = 0;
        while (p < line.Length && char.IsAsciiDigit(line[p])) { p++; digits++; }
        if (digits == 0) { return false; }

        value = int.Parse(line[start..p], CultureInfo.InvariantCulture);
        pos = p;
        return true;
    }
}
