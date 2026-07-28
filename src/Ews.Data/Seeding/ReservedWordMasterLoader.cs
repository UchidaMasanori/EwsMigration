using Ews.Domain.Masters;

namespace Ews.Data.Seeding;

/// <summary>
/// 予約語ファイル(FYDF810)の固定長エクスポートを読み込み、
/// <see cref="ReservedWordMaster"/> 一覧(=メモリ常駐の予約語テーブル)を生成する。
///
/// 【入力】hostdt/FYDF810.data
///   - 旧 EWS-ISAM ファイル FYDF810 を固定長テキストへエクスポートしたもの。
///   - 1 レコード = struct FYDF810(【C原典】fydf810.h, ﾚｺｰﾄﾞ長 14980)を Shift-JIS で
///     出力し、行末を LF(0x0A)で区切る。各フィールドは CHAR[n] の固定幅。
///   - フィールド抽出は <see cref="ReservedWordMaster.FromFixedRecord"/>(バイトオフセット)
///     に委譲する。LF(0x0A)は Shift-JIS の第2バイトに現れないため、バイト単位での
///     行分割は安全。
///
/// 【C原典】Fysk08_Get_YoyakugoFile()(FyIsamOpen → FyIsamSStartR/FyIsamSNextR ループ)
///          + Fysk08_CreYoyakuTbl() による YO_TABLE 生成に相当する。
/// </summary>
public static class ReservedWordMasterLoader
{
    /// <summary>
    /// 型付けするフィールドをすべて読むために必要な最小バイト長。
    /// 【C原典】最後のタイプ枠 tg[6].ksenkbn の終端 = 1445 + 6*1915 + 20 + 1 = 12956。
    /// これに満たない末尾の断片レコードは読み飛ばす。
    /// </summary>
    private const int MinRecordBytes = 12956;

    /// <summary>
    /// FYDF810.data を解析して <see cref="ReservedWordMaster"/> 一覧を返す。
    /// </summary>
    public static IReadOnlyList<ReservedWordMaster> ParseReservedWordMaster(string dataPath)
    {
        byte[] all = File.ReadAllBytes(dataPath);
        var list = new List<ReservedWordMaster>();

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
                list.Add(ReservedWordMaster.FromFixedRecord(all.AsSpan(start, length)));
            }

            start = i + 1;
        }

        return list;
    }
}
