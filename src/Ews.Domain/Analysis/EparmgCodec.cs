using System.Text;
using Ews.Domain.Common;

namespace Ews.Domain.Analysis;

/// <summary>
/// <see cref="ElectricalParameters"/>(=C <c>struct eparmg</c>)を FYDF806 の固定長バイト列へ
/// 相互変換するコーデック。【C原典】toku/include/common/fycommon.h の eparmg(253 バイト)。
///
/// フィールドの並び・桁数は eparmg 構造体の宣言順に一致させる(合計 <see cref="RecordLength"/> バイト)。
/// エンコードは Shift-JIS(CP932)。eparm_set が生成する値は ASCII 数字・'.'・空白・区分文字のみのため
/// 1 文字 = 1 バイトで固定長が保たれる。char フィールド('\0' を含む)はそのまま 1 バイトで格納する。
/// </summary>
public static class EparmgCodec
{
    /// <summary>eparmg 1 個分の固定長(バイト)。【C原典】sizeof(struct eparmg)。</summary>
    public const int RecordLength = 253;

    private static readonly Encoding Cp932 = FixedFieldCodec.ShiftJis;

    /// <summary>
    /// <paramref name="ep"/> を eparmg 構造体順の 253 バイト固定長レコードへ直列化する。
    /// </summary>
    public static byte[] Serialize(ElectricalParameters ep)
    {
        ArgumentNullException.ThrowIfNull(ep);

        byte[] buf = new byte[RecordLength];
        int pos = 0;

        void PutStr(string value, int width)
        {
            byte[] b = Cp932.GetBytes(value ?? string.Empty);
            for (int i = 0; i < width; i++)
            {
                buf[pos + i] = i < b.Length ? b[i] : (byte)'0';
            }
            pos += width;
        }

        void PutChar(char value)
        {
            buf[pos] = (byte)value;
            pos += 1;
        }

        PutStr(ep.Ph1, 1);
        PutStr(ep.Ph2[0], 1);
        PutStr(ep.Ph2[1], 1);
        PutStr(ep.Wr1, 1);
        PutStr(ep.Wr2[0], 1);
        PutStr(ep.Wr2[1], 1);
        PutStr(ep.Hz, 2);
        PutStr(ep.P, 3);
        PutStr(ep.E, 1);
        PutStr(ep.Af, 9);
        PutStr(ep.At, 9);
        PutStr(ep.A1, 9);
        PutStr(ep.A2, 9);
        PutStr(ep.W1, 10);
        PutStr(ep.Va, 10);
        PutStr(ep.Kvar, 6);
        PutStr(ep.Uf, 8);
        PutStr(ep.Ma[0], 4);
        PutStr(ep.Ma[1], 4);
        PutStr(ep.Ma[2], 4);
        PutStr(ep.Ma[3], 4);
        PutStr(ep.V1[0], 8);
        PutStr(ep.V1[1], 8);
        PutStr(ep.V1[2], 8);
        PutStr(ep.V1Idx, 1);
        PutStr(ep.V2[0], 8);
        PutStr(ep.V2[1], 8);
        PutStr(ep.V2[2], 8);
        PutStr(ep.V2Idx, 1);
        PutChar(ep.V2Kbn);
        PutStr(ep.Am, 3);
        PutStr(ep.Vc, 3);
        PutChar(ep.VcKbn);
        PutStr(ep.Sset, 13);
        PutStr(ep.Ss, 13);
        PutStr(ep.S, 13);
        PutStr(ep.Ac, 2);
        PutStr(ep.Bc, 2);
        PutStr(ep.Cc, 2);
        PutStr(ep.T, 5);
        PutStr(ep.K, 3);
        PutChar(ep.Qty);
        PutChar(ep.Bn);
        PutStr(ep.Sq, 6);
        PutStr(ep.Esq, 6);
        PutChar(ep.C);
        PutChar(ep.Ksu);
        PutStr(ep.Mah, 5);
        PutStr(ep.O, 6);
        PutStr(ep.W2, 3);
        PutStr(ep.Ksize, 5);
        PutStr(ep.Cset, 3);
        PutStr(ep.C1, 3);
        PutStr(ep.C2, 3);

        if (pos != RecordLength)
        {
            throw new InvalidOperationException($"eparmg 直列化長が {pos} バイトで {RecordLength} と一致しません。");
        }

        return buf;
    }

    /// <summary>
    /// 253 バイトの固定長レコードを <see cref="ElectricalParameters"/> へ復元する(往復検証用)。
    /// </summary>
    public static ElectricalParameters Deserialize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < RecordLength)
        {
            throw new ArgumentException($"eparmg レコードは {RecordLength} バイト必要ですが {bytes.Length} バイトです。", nameof(bytes));
        }

        ElectricalParameters ep = new();
        int pos = 0;

        static string GetStr(ReadOnlySpan<byte> b, ref int pos, int width)
        {
            string s = Cp932.GetString(b.Slice(pos, width));
            pos += width;
            return s;
        }

        static char GetChar(ReadOnlySpan<byte> b, ref int pos)
        {
            char c = (char)b[pos];
            pos += 1;
            return c;
        }

        ep.Ph1 = GetStr(bytes, ref pos, 1);
        ep.Ph2[0] = GetStr(bytes, ref pos, 1);
        ep.Ph2[1] = GetStr(bytes, ref pos, 1);
        ep.Wr1 = GetStr(bytes, ref pos, 1);
        ep.Wr2[0] = GetStr(bytes, ref pos, 1);
        ep.Wr2[1] = GetStr(bytes, ref pos, 1);
        ep.Hz = GetStr(bytes, ref pos, 2);
        ep.P = GetStr(bytes, ref pos, 3);
        ep.E = GetStr(bytes, ref pos, 1);
        ep.Af = GetStr(bytes, ref pos, 9);
        ep.At = GetStr(bytes, ref pos, 9);
        ep.A1 = GetStr(bytes, ref pos, 9);
        ep.A2 = GetStr(bytes, ref pos, 9);
        ep.W1 = GetStr(bytes, ref pos, 10);
        ep.Va = GetStr(bytes, ref pos, 10);
        ep.Kvar = GetStr(bytes, ref pos, 6);
        ep.Uf = GetStr(bytes, ref pos, 8);
        ep.Ma[0] = GetStr(bytes, ref pos, 4);
        ep.Ma[1] = GetStr(bytes, ref pos, 4);
        ep.Ma[2] = GetStr(bytes, ref pos, 4);
        ep.Ma[3] = GetStr(bytes, ref pos, 4);
        ep.V1[0] = GetStr(bytes, ref pos, 8);
        ep.V1[1] = GetStr(bytes, ref pos, 8);
        ep.V1[2] = GetStr(bytes, ref pos, 8);
        ep.V1Idx = GetStr(bytes, ref pos, 1);
        ep.V2[0] = GetStr(bytes, ref pos, 8);
        ep.V2[1] = GetStr(bytes, ref pos, 8);
        ep.V2[2] = GetStr(bytes, ref pos, 8);
        ep.V2Idx = GetStr(bytes, ref pos, 1);
        ep.V2Kbn = GetChar(bytes, ref pos);
        ep.Am = GetStr(bytes, ref pos, 3);
        ep.Vc = GetStr(bytes, ref pos, 3);
        ep.VcKbn = GetChar(bytes, ref pos);
        ep.Sset = GetStr(bytes, ref pos, 13);
        ep.Ss = GetStr(bytes, ref pos, 13);
        ep.S = GetStr(bytes, ref pos, 13);
        ep.Ac = GetStr(bytes, ref pos, 2);
        ep.Bc = GetStr(bytes, ref pos, 2);
        ep.Cc = GetStr(bytes, ref pos, 2);
        ep.T = GetStr(bytes, ref pos, 5);
        ep.K = GetStr(bytes, ref pos, 3);
        ep.Qty = GetChar(bytes, ref pos);
        ep.Bn = GetChar(bytes, ref pos);
        ep.Sq = GetStr(bytes, ref pos, 6);
        ep.Esq = GetStr(bytes, ref pos, 6);
        ep.C = GetChar(bytes, ref pos);
        ep.Ksu = GetChar(bytes, ref pos);
        ep.Mah = GetStr(bytes, ref pos, 5);
        ep.O = GetStr(bytes, ref pos, 6);
        ep.W2 = GetStr(bytes, ref pos, 3);
        ep.Ksize = GetStr(bytes, ref pos, 5);
        ep.Cset = GetStr(bytes, ref pos, 3);
        ep.C1 = GetStr(bytes, ref pos, 3);
        ep.C2 = GetStr(bytes, ref pos, 3);

        return ep;
    }
}
