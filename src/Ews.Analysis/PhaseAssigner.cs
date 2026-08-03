using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 使用相決定ルーチン(Fyss3D_PH_Kettei)の移植。主回路エリアの各機器へ使用相(R/S/T/N/X/Y)を割り付ける。
/// 【C原典】toku/sekkei/src/Fyss3D.c(約2900行, 改訂&lt;1&gt;～&lt;33&gt;)。
///
/// 本クラスは段階移植であり、まず外部依存(物件明細 hycpskbn・エラー通知 FyHcErrFunc/Err_Code_Set)の
/// 無い純粋ヘルパー群を移植する。統括本体 <c>Fyss3D_PH_Kettei</c>／index 収集
/// <c>PropGetF800Index*</c>／改訂&lt;32&gt; <c>PropChgSiyousou</c>・<c>PropConnect3P4W</c>／
/// <c>Fyss3D_Katagiri</c>・<c>Fyss3D_Keiki_set</c>・<c>Fyss3D_ResetRRYsou</c>／エラー依存チェック
/// (<c>PropCheckUseVolt</c>・<c>PropChkElem1P2W</c>・<c>PropChkLacslRryFuka</c>)は後続増分で移植する。
/// </summary>
public static class PhaseAssigner
{
    /// <summary>
    /// 2 相の使用相(XN/YN/XY/YX)を 1 相(X/Y)へ変換する。変換したら 1、対象外なら 0 を返す。
    /// 【C原典】<c>Siyousou2to1</c>。
    /// </summary>
    public static int Convert2PhaseTo1Phase(MainCircuitData main)
    {
        ArgumentNullException.ThrowIfNull(main);

        if (Matches(main.UsedPhase, "XN  ", 4))
        {
            main.UsedPhase = "X   ";
        }
        else if (Matches(main.UsedPhase, "YN  ", 4))
        {
            main.UsedPhase = "Y   ";
        }
        else if (Matches(main.UsedPhase, "XY  ", 4))
        {
            main.UsedPhase = "X   ";
        }
        else if (Matches(main.UsedPhase, "YX  ", 4))
        {
            main.UsedPhase = "X   ";
        }
        else
        {
            return 0;
        }

        return 1;
    }

    /// <summary>
    /// index テーブル <paramref name="t"/> の先頭 <paramref name="count"/> 件を、指す機器の
    /// 並列追番(heino)昇順に並べ替える(選択ソート)。【C原典】<c>sort3d</c>。
    /// </summary>
    public static void SortByParallelNumber(IReadOnlyList<MainCircuitResult> mains, int[] t, int count)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(t);

        for (int n = 0; n < count - 1; n++)
        {
            int tmp1 = EquipmentParameterFormatter.Stoi(mains[t[n]].Data.ParallelNumber, 3);
            for (int p = n + 1; p < count; p++)
            {
                int tmp2 = EquipmentParameterFormatter.Stoi(mains[t[p]].Data.ParallelNumber, 3);
                if (tmp1 > tmp2)
                {
                    (t[n], t[p]) = (t[p], t[n]);
                    tmp1 = EquipmentParameterFormatter.Stoi(mains[t[n]].Data.ParallelNumber, 3);
                }
            }
        }
    }

    /// <summary>
    /// 同じ親追い番を持つ 100V(または回路電圧 105) 機器の主回路 index を収集する。
    /// 【C原典】<c>PropCount100Vkiki</c>(改訂&lt;7&gt;)。親が MC の時はブレーカ系のみ対象。
    /// </summary>
    public static void CountVolt100VDevices(
        IReadOnlyList<MainCircuitResult> mains, string oyatno, string oyaName, int[] t, ref int count)
    {
        ArgumentNullException.ThrowIfNull(mains);

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (!Matches(oyatno, d.ParentSequenceNumber, 3))
            {
                continue;
            }

            // 改訂<7> MC の下の機器はブレーカ以外パス。
            if (Matches(oyaName, "MC ", 3))
            {
                if (!(Matches(d.ReservedWord, "MCB  ", 5) ||
                      Matches(d.ReservedWord, "ELB  ", 5) ||
                      Matches(d.ReservedWord, "MMCB ", 5) ||
                      Matches(d.ReservedWord, "ELMB ", 5) ||
                      Matches(d.ReservedWord, "SB   ", 5)))
                {
                    continue;
                }
            }

            if (Matches(d.AttachedParameter.LoadVoltage[0], "100", 3))
            {
                t[count++] = i; // 負荷電圧が 100V
            }
            else if (Matches(d.AttachedParameter.LoadVoltage[0], "000", 3))
            {
                if (Matches(d.CircuitVoltage[0], "105", 3))
                {
                    t[count++] = i; // 回路電圧が 100V
                }
            }
        }
    }

    /// <summary>
    /// 100V 機器へ使用相を交互(XN/YN)にセットする。回路電圧 210(200V)はスキップ。
    /// 【C原典】<c>PropSetSou100Vkiki</c>(改訂&lt;3&gt;&lt;17&gt;)。
    /// </summary>
    public static void SetPhase100VDevices(IReadOnlyList<MainCircuitResult> mains, int[] tbl, int count)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(tbl);

        for (int i = 0; i < count; i++)
        {
            MainCircuitData d = mains[tbl[i]].Data;
            if (Matches(d.CircuitVoltage[0], "210", 3)) // 改訂<17> 200V は使用相セット不要
            {
                continue;
            }

            d.UsedPhase = i % 2 == 0 ? "XN  " : "YN  ";
        }
    }

    /// <summary>
    /// 3 相 3 線機器へ使用相(RS/ST/TR)を i%3 で循環セットする。【C原典】<c>PropSetSou3P3Wkiki</c>(改訂&lt;12&gt;)。
    /// </summary>
    public static void SetPhase3P3WDevices(IReadOnlyList<MainCircuitResult> mains, int[] tbl, int count)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(tbl);

        for (int i = 0; i < count; i++)
        {
            mains[tbl[i]].Data.UsedPhase = (i % 3) switch
            {
                0 => "RS  ",
                1 => "ST  ",
                _ => "TR  ",
            };
        }
    }

    /// <summary>
    /// 3 相 4 線機器へ使用相(RN/SN/TN)を i%3 で循環セットする。【C原典】<c>PropSetSou3P4Wkiki</c>(改訂&lt;12&gt;)。
    /// </summary>
    public static void SetPhase3P4WDevices(IReadOnlyList<MainCircuitResult> mains, int[] tbl, int count)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(tbl);

        for (int i = 0; i < count; i++)
        {
            mains[tbl[i]].Data.UsedPhase = (i % 3) switch
            {
                0 => "RN  ",
                1 => "SN  ",
                _ => "TN  ",
            };
        }
    }

    /// <summary>
    /// MC の使用相を電気パラメータ極数から設定する。MCB/SB 2P・負荷電圧 200V は回路電圧 210V へ補正。
    /// 【C原典】<c>PropSetSouMC</c>(改訂&lt;3&gt;&lt;9&gt;)。極数は電気パラメータ[0]の極数(P)を参照。
    /// </summary>
    public static void SetPhaseMc(MainCircuitData main)
    {
        ArgumentNullException.ThrowIfNull(main);

        if (Matches(main.ReservedWord, "MC ", 3))
        {
            if (Matches(main.ElectricalParameterSlots[0].P, "002", 3))
            {
                main.UsedPhase = "XY  ";
            }
            else if (Matches(main.ElectricalParameterSlots[0].P, "003", 3))
            {
                main.UsedPhase = "XNY ";
            }
        }

        // 改訂<9> MCB/SB の 2P・負荷電圧 200V をスポット修正。
        if (Matches(main.ReservedWord, "MCB", 3) || Matches(main.ReservedWord, "SB ", 3))
        {
            if (Matches(main.ElectricalParameterSlots[0].P, "002", 3))
            {
                if (Matches(main.AttachedParameter.LoadVoltage[0], "200", 3))
                {
                    main.CircuitVoltage[0] = "210"; // 回路電圧を 210V
                    main.UsedPhase = "XY  ";
                }
            }
        }
    }

    /// <summary>
    /// 1P3W 時、MC の前後が 3P で MC が 2P の場合に MC を中抜きにする。
    /// 【C原典】<c>PropSetSouMC2P</c>(改訂&lt;20&gt;&lt;24&gt;&lt;25&gt;)。極数は電気パラメータ[2]を参照。
    /// </summary>
    public static void SetPhaseMc2P(MainCircuitResult mc, MainCircuitResult tb)
    {
        ArgumentNullException.ThrowIfNull(mc);
        ArgumentNullException.ThrowIfNull(tb);

        MainCircuitData mcd = mc.Data;
        MainCircuitData tbd = tb.Data;
        if (Matches(mcd.ReservedWord, "MC ", 3) &&
            (Matches(mcd.ElectricalParameterSlots[2].P, "002", 3) ||
             Matches(mcd.ElectricalParameterSlots[2].P, "003", 3)) &&
            Matches(tbd.ReservedWord, "TB ", 3) &&
            Matches(tbd.ElectricalParameterSlots[2].P, "003", 3))
        {
            // 改訂<25> MC と TB が親子でなければ処理しない。
            if (!Matches(mc.SequenceNumber, tbd.ParentSequenceNumber, 3))
            {
                return;
            }

            mcd.UsedPhase = Overlay(mcd.UsedPhase, "X Y"); // MC の使用相(3 桁, 4 桁目保持)
            tbd.UsedPhase = Overlay(tbd.UsedPhase, "XNY"); // TB の使用相(3 桁, 4 桁目保持)
        }
    }

    /// <summary>
    /// MC3P 直下が 2P の場合に "XN"/"YN" を交互に設定する。【C原典】<c>PropSetSouMC3P</c>(改訂&lt;16&gt;&lt;31&gt;)。
    /// </summary>
    /// <param name="mc">MC 機器の主回路データ。</param>
    /// <param name="next">直下(配列で MC の直後)の機器の主回路データ。</param>
    /// <param name="mcCount">XN/YN 設定順を決めるカウンタ(参照更新)。</param>
    public static void SetPhaseMc3P(MainCircuitResult mc, MainCircuitResult next, ref int mcCount)
    {
        ArgumentNullException.ThrowIfNull(mc);
        ArgumentNullException.ThrowIfNull(next);

        MainCircuitData mcd = mc.Data;
        MainCircuitData nd = next.Data;
        if (!Matches(mcd.ReservedWord, "MC ", 3) ||
            !Matches(mcd.UsedPhase, "XNY ", 4) ||
            !Matches(mcd.LineTypeCode, "B ", 2))
        {
            return;
        }

        // 入線番号+上流並列追番+階層番号+並列追番+直列追番(各3桁=15桁)を連結して比較する。
        // 【C原典】strncmp が -1(直下=直列追番が1つ違い)の時のみ設定。AIX の生バイト差で判定。
        string mcKey = SeriesKey(mcd);
        string nextKey = SeriesKey(nd);
        if (StrncmpAix(mcKey, nextKey, 15) != -1)
        {
            return;
        }

        // 改訂<31> MC と直下の機器が親子でなければ処理しない。
        if (!Matches(mc.SequenceNumber, nd.ParentSequenceNumber, 3))
        {
            return;
        }

        if (Matches(nd.ElectricalParameterSlots[0].P, "002", 3)) // 自由文字入力が 2P
        {
            mcd.UsedPhase = mcCount % 2 == 0 ? "X   " : "Y   ";

            // 直下の機器の使用相を親と同じにし、2 桁目を 'N' に。
            nd.UsedPhase = SetChar(mcd.UsedPhase, 1, 'N');
            mcCount++;
        }
    }

    /// <summary>
    /// 親 MC が負荷電圧無指定の時、子機器の最大負荷電圧を返す。【C原典】<c>PropMcChildVolt</c>(改訂&lt;4&gt;)。
    /// </summary>
    public static int GetMcChildMaxVolt(IReadOnlyList<MainCircuitResult> mains, MainCircuitResult oya)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(oya);

        if (!Matches(oya.Data.ReservedWord, "MC ", 3) ||
            !Matches(oya.Data.AttachedParameter.LoadVoltage[0], "000", 3))
        {
            return 0;
        }

        int ans = 0;
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData ko = mains[i].Data;
            if (!Matches(oya.SequenceNumber, ko.ParentSequenceNumber, 3))
            {
                continue; // MC の子機器でない
            }

            ans = Math.Max(ans, EquipmentParameterFormatter.Stoi(ko.AttachedParameter.LoadVoltage[0], 3));
        }

        return ans;
    }

    /// <summary>
    /// 2P・200V 対応へ使用相(XY)と回路電圧(210)を変更する。【C原典】<c>PropSetPrmFor2P200v</c>(改訂&lt;4&gt;)。
    /// </summary>
    public static void SetParamFor2P200V(MainCircuitData main)
    {
        ArgumentNullException.ThrowIfNull(main);

        if (Matches(main.ElectricalParameterSlots[0].P, "002", 3))
        {
            main.UsedPhase = "XY  ";
            main.CircuitVoltage[0] = "210";
        }
    }

    // 入線番号+上流並列追番+階層番号+並列追番+直列追番 を各3桁固定で連結した15桁キー。
    private static string SeriesKey(MainCircuitData d) =>
        Fixed(d.IncomingNumber, 3) + Fixed(d.UpperParallelNumber, 3) + Fixed(d.HierarchyNumber, 3) +
        Fixed(d.ParallelNumber, 3) + Fixed(d.SeriesNumber, 3);

    // dest の先頭 src.Length 文字を src で上書きし、残りは元の値を保持(strncpy(dst, src, n) 相当)。
    private static string Overlay(string dest, string src)
    {
        char[] arr = dest.PadRight(4)[..4].ToCharArray();
        for (int k = 0; k < src.Length && k < 4; k++)
        {
            arr[k] = src[k];
        }

        return new string(arr);
    }

    private static string SetChar(string s, int idx, char c)
    {
        char[] arr = s.PadRight(4)[..4].ToCharArray();
        arr[idx] = c;
        return new string(arr);
    }

    private static string Fixed(string s, int width) => s.PadRight(width)[..width];

    // 【C原典】AIX strncmp: 先頭の不一致バイトの符号付き差(char は AIX で unsigned)。一致は 0。
    private static int StrncmpAix(string a, string b, int width)
    {
        string pa = a.PadRight(width);
        string pb = b.PadRight(width);
        for (int k = 0; k < width; k++)
        {
            int diff = pa[k] - pb[k];
            if (diff != 0)
            {
                return diff;
            }
        }

        return 0;
    }

    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
