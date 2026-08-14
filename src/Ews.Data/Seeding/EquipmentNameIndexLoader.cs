using Ews.Domain.Masters;

namespace Ews.Data.Seeding;

/// <summary>
/// 機器マスター品名索引(FYDF817)の固定長エクスポートを読み込み、
/// <see cref="EquipmentNameIndex"/> 一覧(=PT 機器選定の索引検索テーブル)を生成する。
///
/// 【入力】hostdt/FYDF817.data
///   - 旧 EWS-ISAM ファイル FYDF817 を固定長テキストへエクスポートしたもの。
///   - 1 レコード = struct FYDF817(【C原典】fydf817.h, ﾚｺｰﾄﾞ長 184)を Shift-JIS で
///     出力し、行末を LF(0x0A)で区切る。各フィールドは CHAR[n] の固定幅。
///   - フィールド抽出は <see cref="EquipmentNameIndex.FromFixedRecord"/>(バイトオフセット)
///     に委譲する。LF(0x0A)は Shift-JIS の第2バイトに現れないため、バイト単位での
///     行分割は安全。
///
/// 【C原典】Fysk01_Kikisearch_PT/PT2 の FyIsamOpen → FyIsamStartR による品名索引読みに
///          相当する(本移行では ISAM の代わりに全レコードをメモリ常駐させる)。
/// </summary>
public static class EquipmentNameIndexLoader
{
    /// <summary>
    /// 全フィールドを読むために必要な最小バイト長。
    /// 【C原典】レコード長 184(hinban[15] の終端 = 169 + 15)。これに満たない末尾の
    /// 断片レコードは読み飛ばす。
    /// </summary>
    private const int MinRecordBytes = 184;

    /// <summary>
    /// FYDF817.data を解析して <see cref="EquipmentNameIndex"/> 一覧を返す。
    /// </summary>
    public static IReadOnlyList<EquipmentNameIndex> ParseEquipmentNameIndex(string dataPath)
    {
        byte[] all = File.ReadAllBytes(dataPath);
        var list = new List<EquipmentNameIndex>();

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
                list.Add(EquipmentNameIndex.FromFixedRecord(all.AsSpan(start, length)));
            }

            start = i + 1;
        }

        return list;
    }
}
