namespace Ews.Analysis;

/// <summary>
/// 予約語と形状タイプ(データタイプ)から電気パラメータ算出用のグループ番号(L=1/M=2/H=3)を引く。
///
/// 【C原典】Get_Group(toku/sekkei/src/Fysk01.c:5110, static SHORT)。
///   静的テーブル stype_tbl(fyrt819.h:687)を走査し、予約語(先頭6バイト)一致行の形状タイプ表
///   stype_t を検索する。行の形状タイプが "ALL"(先頭3バイト)ならワイルドカードで一致、そうで
///   なければ dtype(先頭4バイト)と一致した行のグループ番号 gno を返す。該当なしは 0。
///   グループ番号のコメント: 1-&gt;L / 2-&gt;M / 3-&gt;H。
///
/// Get_Ibs_M/Get_Ibs_TR/Get_Ibs_YA(負荷容量取得)の基盤ヘルパ。
/// </summary>
public static class ShapeTypeGroupResolver
{
    /// <summary>該当グループなし。【C原典】ret=0。</summary>
    private const int GroupNotFound = 0;

    /// <summary>予約語の照合バイト数。【C原典】memcmp(yo, ..., 6)。</summary>
    private const int ReservedWordWidth = 6;

    /// <summary>形状タイプの照合バイト数。【C原典】memcmp(dtype, stype, 4)。</summary>
    private const int TypeWidth = 4;

    /// <summary>ワイルドカード判定のバイト数。【C原典】memcmp("ALL", stype, 3)。</summary>
    private const int WildcardWidth = 3;

    /// <summary>全形状タイプ一致を表すワイルドカード。【C原典】"ALL"。</summary>
    private const string Wildcard = "ALL";

    /// <summary>形状タイプ1行(タイプ→グループ番号)。【C原典】STYPE_T。</summary>
    private sealed record TypeGroup(string ShapeType, int Group);

    /// <summary>予約語1行(予約語→形状タイプ表)。【C原典】STYPE_TBL。</summary>
    private sealed record ReservedWordGroup(string ReservedWord, IReadOnlyList<TypeGroup> Types);

    // 【C原典】stype_tbl(fyrt819.h:687)。グループ番号 1=L / 2=M / 3=H。
    private static readonly IReadOnlyList<ReservedWordGroup> Table =
    [
        new ReservedWordGroup("MCB   ",
        [
            new TypeGroup("KY  ", 1), new TypeGroup("KM  ", 2), new TypeGroup("KN  ", 1),
            new TypeGroup("KT  ", 2), new TypeGroup("ET  ", 2), new TypeGroup("ST  ", 3),
            new TypeGroup("HT  ", 3),
        ]),
        new ReservedWordGroup("ELB   ",
        [
            new TypeGroup("KY  ", 1), new TypeGroup("KM  ", 2), new TypeGroup("SB  ", 1),
            new TypeGroup("JI  ", 1), new TypeGroup("ET  ", 2), new TypeGroup("ST  ", 3),
            new TypeGroup("HT  ", 3),
        ]),
        new ReservedWordGroup("MMCB  ",
        [
            new TypeGroup("KM  ", 2), new TypeGroup("ET  ", 2),
            new TypeGroup("ST  ", 3), new TypeGroup("HT  ", 3),
        ]),
        new ReservedWordGroup("ELMB  ",
        [
            new TypeGroup("KM  ", 2), new TypeGroup("ET  ", 2),
            new TypeGroup("ST  ", 3), new TypeGroup("HT  ", 3),
        ]),
        new ReservedWordGroup("SB    ", [new TypeGroup("ALL ", 1)]),
        new ReservedWordGroup("RMCB  ", [new TypeGroup("ALL ", 2)]),
        new ReservedWordGroup("RELB  ", [new TypeGroup("ALL ", 2)]),
        new ReservedWordGroup("RMMCB ", [new TypeGroup("ALL ", 2)]),
        new ReservedWordGroup("RELMB ", [new TypeGroup("ALL ", 2)]),
        new ReservedWordGroup("NHMB  ", [new TypeGroup("ALL ", 2)]),
        new ReservedWordGroup("HPSB  ", [new TypeGroup("ALL ", 2)]),
        new ReservedWordGroup("HSB   ", [new TypeGroup("ALL ", 2)]),
        new ReservedWordGroup("CP    ", [new TypeGroup("ET  ", 2), new TypeGroup("ST  ", 3)]),
        new ReservedWordGroup("CKS   ", [new TypeGroup("ALL ", 2)]),
    ];

    /// <summary>
    /// 予約語と形状タイプからグループ番号(L=1/M=2/H=3)を返す。該当なしは 0。
    /// </summary>
    /// <param name="reservedWord">予約語(先頭6バイトを照合)。</param>
    /// <param name="dataType">形状タイプ(先頭4バイトを照合)。</param>
    public static int Resolve(string reservedWord, string dataType)
    {
        ArgumentNullException.ThrowIfNull(reservedWord);
        ArgumentNullException.ThrowIfNull(dataType);

        foreach (ReservedWordGroup entry in Table)
        {
            // 【C原典】予約語一致は先頭6バイト。
            if (!FixedEquals(reservedWord, entry.ReservedWord, ReservedWordWidth))
            {
                continue;
            }

            foreach (TypeGroup type in entry.Types)
            {
                // 【C原典】行の形状タイプが "ALL" ならワイルドカード、そうでなければ dtype と先頭4バイト一致。
                if (FixedEquals(type.ShapeType, Wildcard, WildcardWidth) ||
                    FixedEquals(dataType, type.ShapeType, TypeWidth))
                {
                    return type.Group;
                }
            }

            // 【C原典】予約語は一致したが形状タイプ非該当は 0(外側ループも break)。
            return GroupNotFound;
        }

        return GroupNotFound;
    }

    // 固定幅バイト比較。不足分は空白扱い(C の空白埋め固定長バッファ memcmp と等価)。
    private static bool FixedEquals(string a, string b, int width)
    {
        for (int i = 0; i < width; i++)
        {
            char ca = i < a.Length ? a[i] : ' ';
            char cb = i < b.Length ? b[i] : ' ';
            if (ca != cb)
            {
                return false;
            }
        }

        return true;
    }
}
