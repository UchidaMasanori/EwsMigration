namespace Ews.Analysis;

using System.Collections.Generic;
using Ews.Domain.Analysis;

/// <summary>
/// 主回路エリア(FYRT800)から制御電源の系統番号を取得するリーフ。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>getCtlDenKno</c>(改訂&lt;20&gt;)。
/// </summary>
public static class ControlPowerSystemLocator
{
    /// <summary>
    /// 主回路エリアを走査し、制御電源番号(fpac)が検索キーと一致する最初のレコードの
    /// 系統番号(kno)を取得する。
    /// 【C原典】getCtlDenKno(Fyss1k.c:3619, 改訂&lt;20&gt;)。
    /// fpac[2] と検索キーを 2 バイト(memcmp 相当)で比較し、一致すれば kno[3] を返す。
    /// </summary>
    /// <param name="searchKey">
    /// 検索キー。【C原典】gyono(呼出元は FYRT820.gyono)。制御電源番号(fpac[2])と 2 バイト比較する。
    /// </param>
    /// <param name="mainCircuits">主回路エリア。【C原典】maina(件数 mainc)。</param>
    /// <param name="systemNumber">一致時に取得する系統番号(kno)。【C原典】出力引数 kno。</param>
    /// <returns>0:取得成功、-1:該当なし。【C原典】0/-1。</returns>
    public static int GetControlPowerSystemNumber(
        string? searchKey,
        IReadOnlyList<MainCircuitResult> mainCircuits,
        out string systemNumber)
    {
        ArgumentNullException.ThrowIfNull(mainCircuits);

        string key = Normalize2(searchKey);

        foreach (MainCircuitResult record in mainCircuits)
        {
            // 【C原典】memcmp(fpac, gyono, 2)。制御電源番号を 2 バイトで比較。
            if (string.Equals(Normalize2(record.Data.AttachedParameter.ControlPowerNumber), key, StringComparison.Ordinal))
            {
                systemNumber = record.Data.SystemNumber;   // 【C原典】memcpy(kno, dt.kno, 3)。
                return 0;
            }
        }

        systemNumber = string.Empty;
        return -1;
    }

    // 【C原典】fpac[2] 固定長比較。空白詰め 2 文字へ正規化する(既存 PadRight(2)[..2] 慣習)。
    private static string Normalize2(string? value)
    {
        return (value ?? string.Empty).PadRight(2)[..2];
    }
}
