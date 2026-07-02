using System.Text;

namespace Ews.Domain.Common;

/// <summary>
/// 固定長 Shift-JIS レコードのエンコード/デコードを担うユーティリティ。
///
/// 【C原典】
///   - toku/lib/libfycom/cmnchar.c  (SJIS 全角/半角判定 LibIsZenChar 等)
///   - toku/tool/src/getfpath.c     (CpyNullStop : 前後空白/改行除去コピー)
///
/// レガシー C のレコードは AIX 上で Shift-JIS(CP932) の固定長バイト列として
/// ISAM / 固定長ファイルに格納されている。各フィールドは CHAR[n] の固定幅で、
/// 余りは半角空白(0x20)もしくは全角空白(0x8140)でパディングされる。
/// </summary>
public static class FixedFieldCodec
{
    /// <summary>Shift-JIS (Windows CP932)。AIX 側の DBCS と互換。</summary>
    public static readonly Encoding ShiftJis;

    static FixedFieldCodec()
    {
        // .NET Core 以降では CodePages プロバイダの登録が必要。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ShiftJis = Encoding.GetEncoding(932);
    }

    /// <summary>
    /// 固定長バイト範囲を文字列へデコードし、前後の空白/NUL を除去する。
    /// 【C原典】CpyNullStop() 相当。
    /// </summary>
    /// <param name="record">レコード全体のバイト列。</param>
    /// <param name="offset">フィールド開始オフセット（バイト）。</param>
    /// <param name="length">フィールド長（バイト）。</param>
    public static string ReadText(ReadOnlySpan<byte> record, int offset, int length)
    {
        ReadOnlySpan<byte> slice = record.Slice(offset, length);
        string decoded = ShiftJis.GetString(slice);
        // C 側は半角/全角空白・NUL 終端を含むため両端をトリムする。
        return decoded.Trim(' ', '\u3000', '\0', '\r', '\n');
    }

    /// <summary>
    /// 文字列を固定長 Shift-JIS バイト列へエンコードし、右側を半角空白でパディングする。
    /// C の CHAR[n] フィールドへ書き戻す際に使用。
    /// </summary>
    public static void WriteText(Span<byte> record, int offset, int length, string? value)
    {
        Span<byte> field = record.Slice(offset, length);
        field.Fill((byte)' ');
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        byte[] encoded = ShiftJis.GetBytes(value);
        int copy = Math.Min(encoded.Length, length);
        encoded.AsSpan(0, copy).CopyTo(field);
    }

    /// <summary>
    /// 数値文字列フィールド（C の "9" 属性 = 右詰めゼロ埋め想定）を long として読む。
    /// 空白のみの場合は <paramref name="defaultValue"/> を返す。
    /// </summary>
    public static long ReadNumber(ReadOnlySpan<byte> record, int offset, int length, long defaultValue = 0)
    {
        string text = ReadText(record, offset, length);
        return long.TryParse(text, out long value) ? value : defaultValue;
    }
}
