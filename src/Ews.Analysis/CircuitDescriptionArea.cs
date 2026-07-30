using System.Text;
using Ews.Domain.Circuits;

namespace Ews.Analysis;

/// <summary>
/// 回路設計エリア(回路内容記述 FYDF805 の集合)を保持し、行・桁から機器の回路内容記述を取得する。
///
/// 【C原典】Fysk11.c(回路設計エリアの保持)。static <c>f805</c>/<c>f805_num</c> に FYDF805 を退避し、
///   <c>Fysk11_FYDF805_KkGet</c>(対象機器の記述)/<c>_Mae</c>(1 個前)/<c>_Ato</c>(1 個後)で
///   桁位置の回路内容記述を切り出す。<c>Keep</c>/<c>Free</c>(calloc/free)は GC に委ね
///   コンストラクタでの保持に置換する。
///
/// 回路内容記述エリア(kairoar)は CP932 の固定長 CHAR[200] であり、桁(keta)は
/// バイト位置。C の strchr/strstr/添字演算をバイト単位で忠実に再現する。
/// </summary>
public sealed class CircuitDescriptionArea
{
    /// <summary>回路内容記述エリアのバイト長。【C原典】#define KAIROARLEN 200。</summary>
    private const int CircuitTextLength = CircuitDescriptionLine.CircuitTextLength;

    private const byte Comma = (byte)',';
    private const byte Hyphen = (byte)'-';
    private const char DeletedCommand = 'D';

    private static readonly Encoding Cp932;

    private readonly IReadOnlyList<CircuitDescriptionLine> _lines;

    static CircuitDescriptionArea()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp932 = Encoding.GetEncoding(932);
    }

    /// <summary>【C原典】Fysk11_FYDF805_Keep(img, num): 回路設計エリアを保持する。</summary>
    public CircuitDescriptionArea(IReadOnlyList<CircuitDescriptionLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _lines = lines;
    }

    /// <summary>
    /// 指定した行・桁の対象機器の回路内容記述を取得する。
    /// 【C原典】Fysk11_FYDF805_KkGet(改訂&lt;2&gt;/&lt;4&gt;)。削除行(cmd='D')はスキップし、
    /// 桁位置の直前(colm-1)から次の ',' までを切り出す。
    /// </summary>
    public string GetDescriptionAt(string lineNo, string columnNo)
    {
        ArgumentNullException.ThrowIfNull(lineNo);
        ArgumentNullException.ThrowIfNull(columnNo);
        (int line, int column) = ResolvePosition(lineNo, columnNo);

        foreach (CircuitDescriptionLine record in _lines)
        {
            if (record.Command == DeletedCommand)   // 改訂<4> 削除行はスキップ
            {
                continue;
            }

            if (record.LineNumber != line)
            {
                continue;
            }

            byte[] buffer = BuildBuffer(record.CircuitText);

            // 【C原典】strchr(&kairoar[colm], ','): 桁位置から次の ',' で切り詰め。
            int comma = IndexOfByte(buffer, column, Comma);
            if (comma >= 0)
            {
                buffer[comma] = 0;
            }

            // 【C原典】strcpy(kkstr, &kairoar[colm-1]) 改訂<2>(1 桁手前から取得)。
            return ReadString(buffer, column - 1);
        }

        return string.Empty;
    }

    /// <summary>
    /// 対象機器の 1 個前の回路内容記述を取得する。
    /// 【C原典】Fysk11_FYDF805_KkGet_Mae(改訂&lt;3&gt;)。区切り(',' または '--')で前の記述を
    /// 切り出す。前に記述が無ければ空。削除行スキップは行わない(C 原典に無い)。
    /// </summary>
    public string GetPrecedingDescription(string lineNo, string columnNo)
    {
        ArgumentNullException.ThrowIfNull(lineNo);
        ArgumentNullException.ThrowIfNull(columnNo);
        (int line, int column) = ResolvePosition(lineNo, columnNo);

        foreach (CircuitDescriptionLine record in _lines)
        {
            if (record.LineNumber != line)
            {
                continue;
            }

            byte[] buffer = BuildBuffer(record.CircuitText);
            int c2 = column - 2;
            int c3 = column - 3;

            if (c2 >= 0 && buffer[c2] == Comma)          // ',' 区切り
            {
                buffer[c2] = 0;
            }
            else if (c2 >= 0 && c3 >= 0 &&                // '--' 区切り
                     buffer[c2] == Hyphen && buffer[c3] == Hyphen)
            {
                buffer[c3] = 0;
            }
            else                                          // 対象機器の前に記述なし
            {
                break;
            }

            // 【C原典】前の前に ',' があればその次から、無ければ先頭から取得。
            int comma = IndexOfByte(buffer, 0, Comma);
            return comma >= 0 ? ReadString(buffer, comma + 1) : ReadString(buffer, 0);
        }

        return string.Empty;
    }

    /// <summary>
    /// 対象機器の 1 個後の回路内容記述を取得する。
    /// 【C原典】Fysk11_FYDF805_KkGet_Ato(改訂&lt;3&gt;)。桁位置以降の最初の区切り(',' か '--')の
    /// 次から、次の区切りまでを切り出す。以降に記述が無ければ空。
    /// </summary>
    public string GetFollowingDescription(string lineNo, string columnNo)
    {
        ArgumentNullException.ThrowIfNull(lineNo);
        ArgumentNullException.ThrowIfNull(columnNo);
        (int line, int column) = ResolvePosition(lineNo, columnNo);

        foreach (CircuitDescriptionLine record in _lines)
        {
            if (record.LineNumber != line)
            {
                continue;
            }

            byte[] buffer = BuildBuffer(record.CircuitText);
            int start = column - 1;

            int comma = IndexOfByte(buffer, start, Comma);            // chrp1
            int doubleHyphen = IndexOfDoubleHyphen(buffer, start);    // chrp2

            int separator;
            int skip;
            if (comma >= 0 && doubleHyphen >= 0)
            {
                if (comma < doubleHyphen)
                {
                    separator = comma;
                    skip = 1;
                }
                else
                {
                    separator = doubleHyphen;
                    skip = 2;
                }
            }
            else if (comma < 0 && doubleHyphen < 0)   // 以降に記述なし
            {
                return string.Empty;
            }
            else if (comma >= 0)
            {
                separator = comma;
                skip = 1;
            }
            else
            {
                separator = doubleHyphen;
                skip = 2;
            }

            // 【C原典】区切りの次から取得し、次の区切り(',' か '--')で切り詰め。
            byte[] tail = BuildTail(buffer, separator + skip);
            int cut = IndexOfByte(tail, 0, Comma);
            if (cut >= 0)
            {
                tail[cut] = 0;
            }
            else
            {
                int cutHyphen = IndexOfDoubleHyphen(tail, 0);
                if (cutHyphen >= 0)
                {
                    tail[cutHyphen] = 0;
                }
            }

            return ReadString(tail, 0);
        }

        return string.Empty;
    }

    /// <summary>
    /// 行・桁を数値化し、桁が 1 行分(KAIROARLEN)を超える場合の折返し(改訂&lt;2&gt;)を解決する。
    /// 【C原典】LibCharToShort(=Stoi) と colm/KAIROARLEN による行送り。
    /// </summary>
    private static (int line, int column) ResolvePosition(string lineNo, string columnNo)
    {
        int line = EquipmentParameterFormatter.Stoi(lineNo, 3);
        int column = EquipmentParameterFormatter.Stoi(columnNo, 3);

        if (column >= CircuitTextLength)
        {
            int wrap = column / CircuitTextLength;
            column -= CircuitTextLength * wrap;
            line += wrap;
        }

        // 正規化後 column は必ず [0, KAIROARLEN) に収まる(C の改訂<1>再チェックは常に偽)。
        return (line, column);
    }

    /// <summary>
    /// 回路内容記述を CP932 の固定長 200 バイト + NUL 終端バッファへ復元する。
    /// 【C原典】memset(0)+strncpy(kairoar, f805->kairoar, 200)(空白埋めの固定長 + '\0')。
    /// </summary>
    private static byte[] BuildBuffer(string circuitText)
    {
        byte[] encoded = Cp932.GetBytes(circuitText);
        byte[] buffer = new byte[CircuitTextLength + 1];   // 末尾 1 バイトは NUL 終端。
        buffer.AsSpan(0, CircuitTextLength).Fill((byte)' ');
        buffer[CircuitTextLength] = 0;
        Array.Copy(encoded, buffer, Math.Min(encoded.Length, CircuitTextLength));
        return buffer;
    }

    /// <summary>指定位置から NUL 終端までを新規バッファへ複写する(末尾に NUL を持つ)。【C原典】strcpy。</summary>
    private static byte[] BuildTail(byte[] buffer, int from)
    {
        if (from < 0)
        {
            from = 0;
        }

        int end = from;
        while (end < buffer.Length && buffer[end] != 0)
        {
            end++;
        }

        byte[] tail = new byte[end - from + 1];
        Array.Copy(buffer, from, tail, 0, end - from);
        return tail;
    }

    /// <summary>【C原典】strchr: 指定位置から NUL までの間で最初に target が現れる位置。無ければ -1。</summary>
    private static int IndexOfByte(byte[] buffer, int from, byte target)
    {
        if (from < 0)
        {
            from = 0;
        }

        for (int i = from; i < buffer.Length && buffer[i] != 0; i++)
        {
            if (buffer[i] == target)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>【C原典】strstr(buf, "--"): 指定位置から NUL までの間で最初の "--" の位置。無ければ -1。</summary>
    private static int IndexOfDoubleHyphen(byte[] buffer, int from)
    {
        if (from < 0)
        {
            from = 0;
        }

        for (int i = from; i + 1 < buffer.Length && buffer[i] != 0; i++)
        {
            if (buffer[i] == Hyphen && buffer[i + 1] == Hyphen)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>指定位置から NUL 終端までを CP932 文字列として取り出す。【C原典】strcpy 後の kkstr。</summary>
    private static string ReadString(byte[] buffer, int from)
    {
        if (from < 0)
        {
            from = 0;
        }

        int end = from;
        while (end < buffer.Length && buffer[end] != 0)
        {
            end++;
        }

        return Cp932.GetString(buffer, from, end - from);
    }
}
