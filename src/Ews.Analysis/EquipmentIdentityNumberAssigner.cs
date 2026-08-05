using Ews.Domain.Analysis;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// 主回路設計エリアに同一機器認識番号(doukkno)を付与する。
/// 【C原典】toku/sekkei/src/Fyss14.c <c>Kiki_Equal_Bangou_Set</c>(5635)。
///
/// CT(2 要素)/ZCT(自動生成)/WH(自動生成)/PS(→TR 化)の特例と、予約語マスタの
/// 同一機器指定可能区分(douskkbn='1')に基づく汎用の予約語番号一致付与を行う。
/// 全要素の doukkno を "00" に初期化した後、対になる要素へ 2 桁の連番("01","02",…)を割り当てる。
/// </summary>
public static class EquipmentIdentityNumberAssigner
{
    /// <summary>未割当を表す同一機器認識番号。【C原典】doukkno="00"。</summary>
    private const string Unassigned = "00";

    /// <summary>
    /// 同一機器認識番号を付与する。【C原典】Kiki_Equal_Bangou_Set(Fyss14.c:5635)。
    /// </summary>
    /// <param name="mains">主回路レコード列。doukkno(および PS→TR の予約語)を in-place 更新する。</param>
    /// <param name="reservedWords">予約語マスタ(YOYAKU_TBL)。汎用付与の douskkbn 判定に使う。</param>
    public static void Assign(IReadOnlyList<MainCircuitResult> mains, IReadOnlyList<ReservedWordMaster> reservedWords)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(reservedWords);

        // 【C原典】初期化: 全要素 doukkno="00"。
        foreach (MainCircuitResult m in mains)
        {
            m.Data.IdentityNumber = Unassigned;
        }

        int n = 1;                 // 次に設定する同一機器認識番号
        int kaisiFlg = 0;          // 【C原典】kaisi_flg(941125)
        int k1 = 0;                // 【C原典】k1(WH 前方対象の遡り幅)

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;

            if (d.IdentityNumber != Unassigned)
            {
                continue;
            }

            if (d.ReservedWord == "CT")
            {
                // 【C原典】CT(kiryoso='1')の前方で doukkno 未割当かつ kiryoso='2' の CT と対にする。
                if (d.CircuitElement == '1')
                {
                    for (int k = i - 1; k >= 0; k--)
                    {
                        MainCircuitData dk = mains[k].Data;
                        if (dk.ReservedWord == "CT" && dk.IdentityNumber == Unassigned)
                        {
                            if (dk.CircuitElement == '2')
                            {
                                string buff = FormatNumber(n++);
                                d.IdentityNumber = buff;
                                dk.IdentityNumber = buff;
                                break;
                            }
                        }
                    }
                }
            }
            else if (d.ReservedWord == "ZCT" && d.AutoGenerationKind == '1')
            {
                // 【C原典】ZCT(自動生成)の前方で doukkno 未割当の ZCT と対にする(950310)。
                for (int k = i - 1; k >= 0; k--)
                {
                    MainCircuitData dk = mains[k].Data;
                    if (dk.ReservedWord == "ZCT" && dk.IdentityNumber == Unassigned)
                    {
                        string buff = FormatNumber(n++);
                        d.IdentityNumber = buff;
                        dk.IdentityNumber = buff;
                        break;
                    }
                }
            }
            else if (d.ReservedWord == "WH" && d.AutoGenerationKind == '1')
            {
                if (d.CircuitElement == '4' || d.CircuitElement == '3')
                {
                    // 【C原典】後に対象データ有り: 後方の最初の WH(未割当かつ非自動生成)と対にする。
                    for (int k = i + 1; k < mains.Count; k++)
                    {
                        MainCircuitData dk = mains[k].Data;
                        if (dk.ReservedWord == "WH")
                        {
                            if (dk.IdentityNumber == Unassigned && dk.AutoGenerationKind == ' ')
                            {
                                string buff = FormatNumber(n++);
                                d.IdentityNumber = buff;
                                dk.IdentityNumber = buff;
                            }
                            break;
                        }
                    }
                }
                else
                {
                    // 【C原典】kiryoso='1' 前に対象データ有り。連続する自動生成 WH(kiryoso='1')数から遡り幅 k1 を算定。
                    if (kaisiFlg == 0)
                    {
                        for (int k = i + 1; k < mains.Count; k++)
                        {
                            MainCircuitData dk = mains[k].Data;
                            if (dk.ReservedWord == "WH" && dk.AutoGenerationKind == '1' && dk.CircuitElement == '1')
                            {
                                kaisiFlg++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        k1 = kaisiFlg != 0 ? ++kaisiFlg : 1;
                    }

                    for (int k = i - k1; k >= 0; k--)
                    {
                        MainCircuitData dk = mains[k].Data;
                        if (dk.ReservedWord == "WH")
                        {
                            if (dk.IdentityNumber == Unassigned && dk.AutoGenerationKind == ' ')
                            {
                                string buff = FormatNumber(n++);
                                d.IdentityNumber = buff;
                                dk.IdentityNumber = buff;
                                if (kaisiFlg > 0)
                                {
                                    kaisiFlg--;
                                }
                            }
                            break;
                        }
                    }
                }
            }
            else if (d.ReservedWord == "PS")
            {
                // 【C原典】PS の後方で未割当の PS と対にし、双方を TR へ書き換える。
                for (int k = i + 1; k < mains.Count; k++)
                {
                    MainCircuitData dk = mains[k].Data;
                    if (dk.ReservedWord == "PS" && dk.IdentityNumber == Unassigned)
                    {
                        string buff = FormatNumber(n++);
                        d.IdentityNumber = buff;
                        dk.IdentityNumber = buff;
                        d.ReservedWord = "TR";
                        dk.ReservedWord = "TR";
                        break;
                    }
                }
            }
            else
            {
                // 【C原典】予約語マスタ(YOYAKU_TBL)を検索し、同一機器指定可能区分='1' の予約語のみを対象とする。
                ReservedWordMaster? master = null;
                foreach (ReservedWordMaster w in reservedWords)
                {
                    if (w.ReservedWord == d.ReservedWord)
                    {
                        master = w;
                        break;
                    }
                }

                // 【C原典】ELR は非該当/区分判定を素通りさせる。ELR 以外は未登録なら無視、douskkbn!='1' なら無視。
                if (d.ReservedWord != "ELR")
                {
                    if (master is null)
                    {
                        continue;
                    }

                    if (master.SameEquipmentAssignableKind != '1')
                    {
                        continue;
                    }
                }

                // 【C原典】現要素より前で予約語・予約語指定番号(ysno!="00")が等しい要素を探す。
                int found = i;
                for (int k = 0; k < i; k++)
                {
                    MainCircuitData dk = mains[k].Data;
                    if (dk.ReservedWord == d.ReservedWord &&
                        dk.DesignationNumber == d.DesignationNumber &&
                        dk.DesignationNumber != Unassigned)
                    {
                        found = k;
                        break;
                    }
                }

                if (found < i)
                {
                    MainCircuitData dk = mains[found].Data;
                    if (dk.IdentityNumber == Unassigned)
                    {
                        // 【C原典】前要素が未割当かつサフィックス一致なら双方へ新番号(941123)。
                        if (d.DesignationSuffix == dk.DesignationSuffix)
                        {
                            string buff = FormatNumber(n++);
                            dk.IdentityNumber = buff;
                            d.IdentityNumber = buff;
                        }
                    }
                    else
                    {
                        // 【C原典】前要素が割当済なら同じ番号を継承(941121)。
                        d.IdentityNumber = dk.IdentityNumber;
                    }
                }
            }
        }
    }

    /// <summary>【C原典】sprintf(buff,"%02d",E_No)。2 桁ゼロ詰め。</summary>
    private static string FormatNumber(int value) => value.ToString("00");
}
