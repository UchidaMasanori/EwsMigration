using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 親データ追番(oyatno)から親機器(P 行)の主回路データを取得する。
/// 親追番を辿り、行種コードが 'P' で始まる最初の機器を親 P 行として返す。
/// 【C原典】<c>Fysk0f_GetOyaP</c>(toku/sekkei/src/Fysk0f.c:35)。
/// BA-468 漏電ブレーカの感度電流変更により作成。呼び出し元: Set_WK1() / PropChkKando()。
/// </summary>
public static class ParentEquipmentLocator
{
    /// <summary>親データ追番の桁数。【C原典】sizeof(dt.oyatno)=3。</summary>
    private const int ParentSequenceWidth = 3;

    /// <summary>
    /// 親追番を辿って親機器(P 行)の主回路データを取得する。【C原典】<c>Fysk0f_GetOyaP(rt800, oyatno, &amp;oya)</c>。
    /// 親 P 行が見つからない場合は <c>null</c> を返す(【C原典】*oya = NULL)。
    /// </summary>
    /// <param name="records">主回路データ配列。【C原典】rt800 (FYRT800 *)。</param>
    /// <param name="parentSequenceNumber">親データ追番。【C原典】oyatno (CHAR *)。</param>
    /// <returns>親機器(P 行)の主回路データ。見つからなければ <c>null</c>。</returns>
    public static MainCircuitResult? FindParentPRow(IReadOnlyList<MainCircuitResult> records, string? parentSequenceNumber)
    {
        ArgumentNullException.ThrowIfNull(records);

        // 【C原典】i = oya_oibn = LibCharToShort(oyatno, sizeof(oyatno))。
        int parentOibn = EquipmentParameterFormatter.Stoi(parentSequenceNumber, ParentSequenceWidth);
        int index = parentOibn;
        char lineTypeCode = ' ';
        MainCircuitResult? parent = null;

        // 【C原典】while(gyocd != 'P' && i > 0){ i = oya_oibn-1; gyocd=(rt800+i)->dt.gyocd[0]; ... }
        while (lineTypeCode != 'P' && index > 0)
        {
            index = parentOibn - 1;
            if (index < 0 || index >= records.Count)
            {
                // 【C原典】は配列外参照(異常データ)だが、本移行では親 P 行なし扱いで打ち切る。
                parent = null;
                break;
            }

            MainCircuitResult candidate = records[index];
            string code = candidate.Data.LineTypeCode ?? string.Empty;
            lineTypeCode = code.Length > 0 ? code[0] : ' ';
            parentOibn = EquipmentParameterFormatter.Stoi(candidate.Data.ParentSequenceNumber, ParentSequenceWidth);
            parent = candidate;
        }

        // 【C原典】if(gyocd != 'P') *oya = NULL。
        if (lineTypeCode != 'P')
        {
            parent = null;
        }

        return parent;
    }
}
