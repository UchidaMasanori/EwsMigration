using Ews.Domain.Masters;

namespace Ews.Data.Seeding;

/// <summary>
/// 直近上下位参照ファイル(FYDF812)の固定長エクスポートを読み込み、
/// <see cref="NearestRankReference"/> 一覧(=機器選定の候補検索テーブル)を生成する。
///
/// 【入力】hostdt/FYDF812.data
///   - 旧 EWS-ISAM ファイル FYDF812 を固定長テキストへエクスポートしたもの。
///   - 1 レコード = struct FYDF812(【C原典】fydf812.h, ﾚｺｰﾄﾞ長 300)を Shift-JIS で
///     出力し、行末を LF(0x0A)で区切る。各フィールドは CHAR[n] の固定幅。
///   - フィールド抽出は <see cref="NearestRankReference.FromFixedRecord"/>(バイトオフセット)
///     に委譲する。LF(0x0A)は Shift-JIS の第2バイトに現れないため、バイト単位での
///     行分割は安全。
///
/// 【C原典】Fysk01_Chokkin_Read_Check(_ALL/_TMS)の FyIsamOpen →
///          FyIsamGStartR/FyIsamGNextR ループによる直近上下位ファイル走査に相当する
///          (本移行では ISAM の代わりに全レコードをメモリ常駐させる)。
/// </summary>
public static class NearestRankReferenceLoader
{
    /// <summary>
    /// 型付けするフィールドをすべて読むために必要な最小バイト長。
    /// 【C原典】レコード長 300。制御電圧適応範囲(to) vcto[3] の終端 = 284 + 3 = 287。
    /// これに満たない末尾の断片レコードは読み飛ばす。
    /// </summary>
    private const int MinRecordBytes = 287;

    /// <summary>
    /// FYDF812.data を解析して <see cref="NearestRankReference"/> 一覧を返す。
    /// </summary>
    public static IReadOnlyList<NearestRankReference> ParseNearestRankReference(string dataPath)
    {
        byte[] all = File.ReadAllBytes(dataPath);
        var list = new List<NearestRankReference>();

        int start = 0;
        for (int i = 0; i <= all.Length; i++)
        {
            if (i != all.Length && all[i] != (byte)'\n')
            {
                continue;
            }

            // レコード = [start, i)。末尾 CR(0x0D)があれば除外する。
            int end = i;
            if (end > start && all[end - 1] == (byte)'\r')
            {
                end--;
            }

            int length = end - start;
            if (length >= MinRecordBytes)
            {
                list.Add(NearestRankReference.FromFixedRecord(all.AsSpan(start, length)));
            }

            start = i + 1;
        }

        return list;
    }
}
