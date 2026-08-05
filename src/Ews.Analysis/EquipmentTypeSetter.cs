using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// F のトランス種別(GT/ST)と、主幹 MCB/ELB の機器タイプ(NT/TLA)を設定する。
/// 【C原典】Type_Set / PropGetSenSou / PropSearch2PBrk(toku/sekkei/src/Fyss14.c:5844, 941121 改訂3ほか)。
///
/// (1)予約語 F でタイプ0未設定なら、製作仕様区分 sshiykbn が "01"/"02" なら "GT"、他は "ST" を設定。
/// (2)予約語 ELB/MCB でタイプ1未設定なら既定 "NT" を設定し、系統の相数/線数が 1P3W か 3P4W、
///    P 系統、行種 M/TM/SM/S、極数 3P(または未入力)を満たし、直下～下流に 1P/2P ブレーカ(子)が
///    あり、かつ AT が 600A 以下なら欠相保護付きブレーカ "TLA" を設定する(改訂32/38)。
/// Fyss14_Make_UpperParm のループ後処理群の 1 つ。
/// </summary>
public static class EquipmentTypeSetter
{
    /// <summary>TLA タイプ機器に接続できる 2P ブレーカ機器(予約語, トリム済み)。【C原典】buntype[][9]。</summary>
    private static readonly string[] BreakerTypes =
        ["SB", "MCB", "ELB", "MMCB", "ELMB", "RMCB", "RMMCB", "RELB", "RELMB"];

    /// <summary>
    /// 機器タイプを設定する(in-place)。
    /// 【C原典】Type_Set(Fyss14.c:5844)。
    /// </summary>
    /// <param name="mains">主回路レコード列。DataType が in-place 更新される。【C原典】*Pmaina(件数 Pmainc)。</param>
    /// <param name="manufacturingSpecKind">製作仕様区分。【C原典】bukken1-&gt;com.kyo.sshiykbn。先頭 2 文字 "01"/"02" を判定に使う。</param>
    public static void Set(IReadOnlyList<MainCircuitResult> mains, string? manufacturingSpecKind = null)
    {
        ArgumentNullException.ThrowIfNull(mains);

        // 【C原典】memset(kno/sen/sou, 0)。PropGetSenSou の取得結果はループを跨いでキャッシュされる。
        string cachedKno = string.Empty;
        char sou = '\0';
        char sen = '\0';

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;

            // 【C原典】F のトランス種別 GT/ST 設定(改訂28)。
            if (Matches(d.ReservedWord, "F       ", 8) && Matches(d.DataType[0], "       ", 7))
            {
                d.DataType[0] = (Matches(manufacturingSpecKind, "01", 2) || Matches(manufacturingSpecKind, "02", 2))
                    ? "GT     "
                    : "ST     ";
            }

            // 【C原典】主幹の TLA タイプ設定(改訂3)。ELB/MCB 以外は対象外。
            if (!Matches(d.ReservedWord, "ELB ", 4) && !Matches(d.ReservedWord, "MCB ", 4))
            {
                continue;
            }
            if (!Matches(d.DataType[1], "       ", 7))
            {
                continue;   // 機器タイプ設定済み
            }

            // デフォルト設定
            d.DataType[1] = "NT     ";

            PropGetSenSou(mains, mains[i], ref cachedKno, ref sou, ref sen);
            if (Atoi(sou) != 1 || Atoi(sen) != 3)
            {
                if (Atoi(sou) != 3 || Atoi(sen) != 4)   // 改訂18
                {
                    continue;   // 1相3線, 3相4線でない
                }
            }

            if (d.SystemKind != '1')
            {
                continue;   // 系統種別がＰ系統でない
            }
            if (!Matches(d.LineTypeCode, "M  ", 3) && !Matches(d.LineTypeCode, "TM ", 3) &&
                !Matches(d.LineTypeCode, "SM ", 3) && !Matches(d.LineTypeCode, "S  ", 3))
            {
                continue;   // 主幹が付かない対象外の行種
            }

            // 極数チェック (3P, 未入力は通す)
            if (!Matches(d.ElectricalParameterSlots[0].P, "003", 3))
            {
                if (!Matches(d.ElectricalParameterSlots[0].P, "000", 3))   // 改訂11
                {
                    continue;   // 機器極数が違う
                }
            }

            // MCB/ELB への機器タイプ "TLA" のセット
            if (PropSearch2PBrk(mains, mains[i]) == 0)
            {
                // TLA 機器は最大 600A のため、600AT を超える場合には設定しない(改訂32/38)
                double at = EquipmentParameterFormatter.Stof(d.ElectricalParameterSlots[0].At, 9);
                if (at <= 600.0)
                {
                    d.DataType[1] = "TLA    ";
                }
            }
        }
    }

    // 【C原典】PropGetSenSou: 系統の P 行から回路相数/線数を取得。取得結果はキャッシュ(kno)される。
    private static void PropGetSenSou(IReadOnlyList<MainCircuitResult> mains, MainCircuitResult now,
                                      ref string kno, ref char sou, ref char sen)
    {
        if (Matches(kno, now.Data.SystemNumber, 3))
        {
            return;   // すでに取得済み
        }

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData e = mains[i].Data;
            if (!Matches(now.Data.SystemNumber, e.SystemNumber, 3))
            {
                continue;
            }
            if (Matches(e.ReservedWord, "P ", 2))
            {
                kno = Pad(now.Data.SystemNumber, 3);
                sou = e.CircuitPhaseCount;   // 回路相数
                sen = e.CircuitWireType;     // 回路線式
                return;
            }
        }
    }

    // 【C原典】PropSearch2PBrk: 直下～下流に 1P/2P ブレーカの子が居れば 0(親が TLA)、居なければ -1。
    private static int PropSearch2PBrk(IReadOnlyList<MainCircuitResult> mains, MainCircuitResult oya)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData child = mains[i].Data;

            // データ追番で子を探査
            if (!Matches(oya.SequenceNumber, child.ParentSequenceNumber, 3))
            {
                continue;
            }

            bool is2PBreaker = false;
            foreach (string bt in BreakerTypes)
            {
                if (Matches(child.ReservedWord, bt, 8))
                {
                    is2PBreaker = true;
                    break;
                }
            }

            if (is2PBreaker)
            {
                if (Matches(child.ElectricalParameterSlots[0].P, "002", 3) ||
                    Matches(child.ElectricalParameterSlots[0].P, "001", 3))   // 改訂5
                {
                    return 0;   // 親が TLA となる子供発見(1P or 2P ブレーカ)
                }
                continue;   // 他の機器を探す
            }

            // 更に下流の子を探査
            if (PropSearch2PBrk(mains, mains[i]) == 0)
            {
                return 0;
            }
        }

        return -1;
    }

    // 【C原典】memcmp/strncmp(a, b, width): 空白右詰めで先頭 width バイトを序数比較。
    private static bool Matches(string? value, string expected, int width) =>
        string.CompareOrdinal(Pad(value, width), Pad(expected, width)) == 0;

    private static string Pad(string? s, int width) => (s ?? string.Empty).PadRight(width)[..width];

    // 【C原典】atoi(1 文字): 数字なら値、他は 0。
    private static int Atoi(char c) => c is >= '0' and <= '9' ? c - '0' : 0;
}
