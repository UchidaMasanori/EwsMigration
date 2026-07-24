using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// セパレータ(SEP)作図の要否判定。【C原典】toku/sekkei/src/Fyss12.c(改訂&lt;12&gt;)。
///   - <c>PropChkSEPBox</c>   … 適用 BOX タイプ(または生成 BOX 品番)とボックスフカサから
///                              セパレータ作図 BOX か否かを判定する。
///   - <c>PropChkHbnHB300</c> … 入力品番(hbninf.inputhb)が幅300用ユニット品番のいずれかを
///                              含むか(部分一致)で HB300 対象か否かを判定する。
///
/// C 原典はいずれも案件の品番情報(hbninf)を <c>FyCpHbHbnInfFileR</c> で読み、FYDF801 の
/// 盤明細情報(boxsund)や設計コンスタント(unithb300.cns)と突き合わせる。本移行では
/// 読込済みの <see cref="PartNumberInfo"/>・ボックスフカサ・幅300品番一覧を受け取り、
/// 純粋な判定ロジックとして再現する(ISAM/ファイル入出力は呼出側で解決)。
/// </summary>
public static class SeparatorBoxCheck
{
    /// <summary>
    /// セパレータ作図 BOX チェック。【C原典】PropChkSEPBox(bukken1, bukken2)。
    /// 生成 BOX 品番(crboxtmp)があればそれ、無ければ適用 BOX タイプ(boxtyp)を BOX タイプとし、
    /// タイプが "JBR" または "JOC" で始まり かつ ボックスフカサが 350 のとき SEP 作図対象。
    /// </summary>
    /// <param name="partInfo">案件の品番情報(hbninf)。</param>
    /// <param name="boxDepth">ボックスフカサ(FYDF801 盤明細 boxsund。例 "00350")。</param>
    /// <returns>0:SEP 作図あり / -1:SEP 作図なし。【C原典】sep_flg。</returns>
    public static int CheckSepBox(PartNumberInfo partInfo, string boxDepth)
    {
        ArgumentNullException.ThrowIfNull(partInfo);

        // 【C原典】box_type = crboxtmp(生成BOX品番)があればそれ、無ければ boxtyp(適用BOXタイプ)。
        string boxType = partInfo.GeneratedBoxPartNumber.Length > 0
            ? partInfo.GeneratedBoxPartNumber
            : partInfo.BoxType;

        int boxDept = AtoiC(boxDepth ?? string.Empty); // 【C原典】box_dept = atoi(boxsund)

        // 【C原典】(strncmp(box_type,"JBR",3)==0 && box_dept==350) ||
        //          (strncmp(box_type,"JOC",3)==0 && box_dept==350) → sep_flg=0。
        bool sep = boxDept == 350
            && (boxType.StartsWith("JBR", StringComparison.Ordinal)
                || boxType.StartsWith("JOC", StringComparison.Ordinal));

        return sep ? 0 : -1;
    }

    /// <summary>
    /// HB300(幅300)チェック。【C原典】PropChkHbnHB300(bukken1, bukken2)。
    /// 幅300用ユニット品番(unithb300.cns 由来)のいずれかが入力品番(inputhb)に
    /// 部分一致(strstr)すれば HB300 対象(0)。
    /// </summary>
    /// <param name="partInfo">案件の品番情報(hbninf)。</param>
    /// <param name="hb300UnitParts">幅300用ユニット品番一覧(unithb300.cns 由来)。</param>
    /// <returns>0:該当(HB300) / -1:非該当。【C原典】ret。</returns>
    public static int CheckHb300(PartNumberInfo partInfo, IReadOnlyList<string> hb300UnitParts)
    {
        ArgumentNullException.ThrowIfNull(partInfo);
        ArgumentNullException.ThrowIfNull(hb300UnitParts);

        // 【C原典】各品番 buf について strstr(hbn_p->inputhb, buf) が非NULLなら ret=0。
        foreach (string part in hb300UnitParts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            if (partInfo.InputPartNumber.Contains(part, StringComparison.Ordinal))
            {
                return 0;
            }
        }

        return -1;
    }

    /// <summary>
    /// C の <c>atoi()</c> 相当。先頭空白/符号を許容し、数字が続く間だけを整数化する。
    /// 【C原典】atoi。
    /// </summary>
    private static int AtoiC(string s)
    {
        int i = 0;
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t'))
        {
            i++;
        }
        int sign = 1;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            if (s[i] == '-')
            {
                sign = -1;
            }
            i++;
        }
        long value = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            value = value * 10 + (s[i] - '0');
            i++;
        }
        return (int)(sign * value);
    }
}
