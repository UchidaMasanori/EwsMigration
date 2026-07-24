using Ews.Domain.Analysis;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// セパレータ(SEP)機器の追加ロジック。【C原典】toku/sekkei/src/Fyss12.c(改訂&lt;7&gt;/&lt;12&gt;)。
/// 主回路生成 step6(Yoyakugo_Add_Main)の系統ブレーク時に、系統末尾へ合成の "SEP" 機器を
/// 追加する処理の中核を移植する。
///
/// 構成:
///   - <see cref="CreateSeparatorEntry"/>       … Kikitable_SEP_Make(合成 SEP 機器の生成)。
///   - <see cref="IsSeparatorApplicable"/>       … sep_flg(PropChkSEPBox / PropChkHbnHB300 の合成)。
///   - <see cref="HasSeparatorDeletionCondition"/> … sep_del==sep_num(2系統以上 かつ 1P3W と 3P3W 混在)。
///   - <see cref="IsSeparatorInsertionAllowed"/> … 追加ゲート(sep_del != sep_num)。
///
/// 系統ブレーク時の souden(相電圧)差分走査と機器テーブルへの実挿入(step6 本体への配線)は
/// 別途 <c>MainCircuitBuilder</c> 側で行う。本クラスは判定・生成の純粋ロジックを提供する。
/// </summary>
public static class SeparatorInsertion
{
    /// <summary>セパレータ削除条件の項目数。【C原典】sep_num = 3。</summary>
    private const int SeparatorDeletionItemCount = 3;

    /// <summary>
    /// 合成の "SEP" 機器を生成する。【C原典】Kikitable_SEP_Make(Fyss12.c:4076)。
    /// <paramref name="lastEquipment"/> は系統の最後の機器(R_Kiki)。系統番号・文字列連番・
    /// 回路番号連番・盤名称を引き継ぎ、機器No は R_Kiki の D_No + 5、予約語は "SEP"、
    /// 回路要素 'M'、自動生成区分 '1'、先頭フラグ '1' とする。他は既定(0/空/空白)。
    /// </summary>
    public static EquipmentTableEntry CreateSeparatorEntry(EquipmentTableEntry lastEquipment)
    {
        ArgumentNullException.ThrowIfNull(lastEquipment);

        return new EquipmentTableEntry
        {
            SystemNumber = lastEquipment.SystemNumber,                       // K_No
            GroupNumber = 0,                                                 // G_No
            SpecNumber = 0,                                                  // S_No
            StringSequence = lastEquipment.StringSequence,                   // B_No
            CircuitNumberSequence = lastEquipment.CircuitNumberSequence,     // N_No
            EquipmentNumber = (short)(lastEquipment.EquipmentNumber + 5),    // D_No + 5
            Rank = 0,                                                        // Rank
            EquipmentIdentityNumber = 0,                                     // E_No
            TopFlag = '1',                                                   // TOP_Flg
            CircuitDivision = 'M',                                           // K_Kubun
            AutoGenerationKind = '1',                                        // yoyakkbn
            Ban = lastEquipment.Ban,                                         // ban
            DescriptionRow = "000",                                          // K_Gyo
            DescriptionColumn = "000",                                       // K_Ket
            ControlGroupNumber = 0,                                          // GroupNo
            Kakko1 = 0,
            Kakko2 = 0,
            ReservedWord = "SEP",                                            // yoyaku
            ReservedWordNumber = "00",                                       // ysno
            Quantity = 0,                                                    // Kosu
            PowerSourceFlag = ' ',                                           // C_Flg
            PowerSourceNumber = 0,                                           // C_No
            SpecialFlag = ' ',                                               // SP_Flg
            // 残り(Maker/ItemName/Comment/Comment2/SpecialDimension/負荷系 等)は既定(空)。
        };
    }

    /// <summary>
    /// セパレータ作図フラグ(sep_flg==0=作図あり)を求める。【C原典】Fyss12.c:3729-3735。
    ///   sep_flg = PropChkSEPBox(...);  if( PropChkHbnHB300(...) != 0 ) sep_flg = 0;
    /// すなわち「BOX が SEP 対象」または「幅300ユニット非該当」のとき作図あり。
    /// </summary>
    /// <param name="partInfo">案件の品番情報(hbninf)。</param>
    /// <param name="boxDepth">ボックスフカサ(FYDF801 盤明細 boxsund)。</param>
    /// <param name="hb300UnitParts">幅300用ユニット品番一覧(unithb300.cns)。</param>
    /// <returns>true:SEP 作図あり(sep_flg==0) / false:なし(sep_flg==-1)。</returns>
    public static bool IsSeparatorApplicable(
        PartNumberInfo partInfo, string boxDepth, IReadOnlyList<string> hb300UnitParts)
    {
        // 【C原典】sep_flg = PropChkSEPBox(bukken1, bukken2)。0=作図あり。
        if (SeparatorBoxCheck.CheckSepBox(partInfo, boxDepth) == 0)
        {
            return true;
        }

        // 【C原典】ret = PropChkHbnHB300(...); if( ret != 0 ) sep_flg = 0;
        // 幅300ユニット非該当(-1)なら作図あり。
        return SeparatorBoxCheck.CheckHb300(partInfo, hb300UnitParts) != 0;
    }

    /// <summary>
    /// セパレータ削除条件(sep_del == sep_num)を満たすか。【C原典】Fyss12.c:3738-3811。
    /// 「2系統以上」かつ「行種の相線(sousen)に 1P3W と 3P3W が両方存在」で true。
    /// この条件のとき、SEP は作成しない(削除)。
    /// </summary>
    public static bool HasSeparatorDeletionCondition(IReadOnlyList<LineTypeTableEntry> lineTypes)
    {
        ArgumentNullException.ThrowIfNull(lineTypes);

        // 【C原典】K_Num=最終行種の K_No、相線(sousen[0]!='0')を重複排除して収集。
        short systemCount = 0;
        var distinctPhaseWires = new List<string>();
        foreach (LineTypeTableEntry g in lineTypes)
        {
            systemCount = g.SystemNumber;

            string sousen = g.PhaseWires;
            if (sousen.Length > 0 && sousen[0] != '0')
            {
                string s4 = sousen.Length >= 4 ? sousen[..4] : sousen;
                if (!distinctPhaseWires.Contains(s4))
                {
                    distinctPhaseWires.Add(s4);
                }
            }
        }

        // 【C原典】if( K_Num >= 2 ) sep_chk[0]=1; 以降で 1P3W/3P3W を判定。
        if (systemCount < 2)
        {
            return false;
        }

        bool has1P3W = false;
        bool has3P3W = false;
        // 【C原典】for(icnt=0; icnt<K_Num; icnt++) sousen[icnt] を判定(未収集分は空)。
        for (int i = 0; i < systemCount; i++)
        {
            string s = i < distinctPhaseWires.Count ? distinctPhaseWires[i] : string.Empty;
            if (s == "1P3W")
            {
                has1P3W = true;
            }
            if (s == "3P3W")
            {
                has3P3W = true;
            }
        }

        // 【C原典】sep_del == sep_num(3) ? sep_chk[0]&[1]&[2](2系統以上 かつ 1P3W かつ 3P3W)。
        return has1P3W && has3P3W;
    }

    /// <summary>
    /// セパレータ追加ゲート(sep_del != sep_num)。【C原典】Fyss12.c:3854。
    /// sep_flg==0(作図あり)のとき sep_del=0 で常に許可。sep_flg==-1 のときは
    /// 削除条件(<see cref="HasSeparatorDeletionCondition"/>)を満たさなければ許可。
    /// </summary>
    public static bool IsSeparatorInsertionAllowed(
        bool separatorApplicable, IReadOnlyList<LineTypeTableEntry> lineTypes)
    {
        if (separatorApplicable)
        {
            return true; // sep_del=0 → 0 != sep_num → 追加。
        }

        return !HasSeparatorDeletionCondition(lineTypes);
    }
}
