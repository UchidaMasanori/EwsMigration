using Ews.Domain.Masters;

namespace Ews.Data.Seeding;

/// <summary>
/// 品番情報ファイル(hbninf / .clh)を読み込むローダ。
///
/// 【C原典】FyCpHbHbnInfFileR(toku/compo/lib/clhbn_dir/clfilerw.c)。
/// 案件ごとの生バイナリ 1 レコードファイル <c>&lt;WORK&gt;/&lt;依頼明細番号&gt;.clh</c> を読み込む。
/// C 原典は「ファイルサイズが sizeof(struct hbninf) と異なる」場合 NULL を返すため、
/// 本ローダも <see cref="PartNumberInfo.RecordLength"/>(908) と一致しなければ null を返す。
/// </summary>
public sealed class PartNumberInfoLoader
{
    /// <summary>
    /// .clh ファイルを読み込み <see cref="PartNumberInfo"/> を返す。ファイルが存在しない、
    /// またはサイズが 908(sizeof(struct hbninf))でない場合は null。
    /// 【C原典】FyCpHbHbnInfFileR: hbn_flsiz &lt;= 0 または != sizeof(struct hbninf) で NULL。
    /// </summary>
    public static PartNumberInfo? ReadFromFile(string clhPath)
    {
        if (!File.Exists(clhPath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(clhPath);
        if (bytes.Length != PartNumberInfo.RecordLength)
        {
            return null;
        }

        return PartNumberInfo.FromFixedRecord(bytes);
    }
}
