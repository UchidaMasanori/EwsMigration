using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// INV(インバータ)オプション機器かどうかを判定する。機器選定で構成機器を登録する際、
/// 通常の登録経路(Make_Koukiki 本体)ではなく INV オプション専用経路へ振り分けるための判定。
/// 【C原典】PropChkInvOPKiki(toku/sekkei/src/Fysk01.c:6282, 改訂&lt;28&gt;)。
///
/// C原典は「予約語(直近上下位キー)が PT で、機器マスタ品名が FR- で始まる」場合に 0、
/// それ以外は -1 を返す。呼び出し側(Fysk01_Make_Koukiki:3593)は 0 のとき INV オプション登録へ分岐する。
/// </summary>
public static class InverterOptionEquipmentChecker
{
    /// <summary>
    /// INV オプション機器(予約語 PT かつ品名 FR- 始まり)かどうかを返す。
    /// 【C原典】PropChkInvOPKiki(ck, kk) == 0 に相当。
    /// </summary>
    /// <param name="reference">直近上下位該当データ。【C原典】ck(FYDF812)。</param>
    /// <param name="master">機器マスタ該当データ。【C原典】kk(FYDM805)。</param>
    public static bool IsInverterOptionEquipment(NearestRankReference reference, EquipmentMaster master)
    {
        // 【C原典】strncmp(ck->key.yoyaku,"PT ",3)==0 && strncmp(kk->hinmei,"FR-",3)==0
        return StartsWithFixed(reference.ReservedWord, "PT ")
            && StartsWithFixed(master.PartName, "FR-");
    }

    // 固定長フィールドの strncmp 相当: 末尾を空白で埋めて先頭 prefix.Length 文字を比較。
    private static bool StartsWithFixed(string? value, string prefix)
    {
        string padded = (value ?? string.Empty).PadRight(prefix.Length);
        return padded.AsSpan(0, prefix.Length).SequenceEqual(prefix);
    }
}
