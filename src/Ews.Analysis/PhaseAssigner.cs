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

    /// <summary>
    /// MC/SSW を片切りで使う時に使用相を見直し N 相を削除する。同一機器認識番号の共用も考慮する。
    /// 【C原典】<c>Fyss3D_Katagiri</c>(950206, 改訂&lt;3&gt;&lt;33&gt;)。
    /// </summary>
    public static void AdjustKatagiriPhase(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;

            if (Matches(di.ReservedWord, "RRY     ", 8))
            {
                // 改訂<33> 両切り 6A リレーは対象外。
                if (Matches(di.DataType[1], "6A4K", 4) || Matches(di.AttachedParameter.LoadVoltage[0], "200", 3))
                {
                    continue;
                }

                if (!Matches(di.IdentityNumber, "00", 2)) // 同一機器認識番号がセットされている
                {
                    di.UsedPhase = ClearPhaseFrom(di.UsedPhase, 1);
                    for (int k = i; k < mains.Count; k++)
                    {
                        if (Matches(di.IdentityNumber, mains[k].Data.IdentityNumber, 2))
                        {
                            mains[k].Data.UsedPhase = ClearPhaseFrom(mains[k].Data.UsedPhase, 1);
                        }
                    }
                }
            }

            // 改訂<3> MC で使用相が XN/YN 以外はパス。
            if (Matches(di.ReservedWord, "MC      ", 8))
            {
                if (!Matches(di.UsedPhase, "XN  ", 4) && !Matches(di.UsedPhase, "YN  ", 4))
                {
                    continue;
                }
            }

            if (!Matches(di.ReservedWord, "MC      ", 8) && !Matches(di.ReservedWord, "SSW     ", 8))
            {
                continue;
            }

            // 2 次側に機器が接続されるかを判定する。
            bool secondaryExists = false;
            for (int j = 0; j < mains.Count; j++)
            {
                if (Matches(mains[i].SequenceNumber, mains[j].Data.ParentSequenceNumber, 3))
                {
                    secondaryExists = true;
                    break;
                }
            }

            // 2 次側に機器がなくても同一機器認識番号があれば他の機器の 2 次側もチェックする。
            if (!secondaryExists && !Matches(di.IdentityNumber, "00", 2))
            {
                for (int j = 0; j < mains.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    MainCircuitData dj = mains[j].Data;
                    if (Matches(di.IdentityNumber, dj.IdentityNumber, 2) &&
                        Matches(di.ReservedWord, dj.ReservedWord, 8))
                    {
                        for (int k = 0; k < mains.Count; k++)
                        {
                            if (Matches(mains[j].SequenceNumber, mains[k].Data.ParentSequenceNumber, 3))
                            {
                                secondaryExists = true;
                                break;
                            }
                        }

                        if (secondaryExists)
                        {
                            break;
                        }
                    }
                }
            }

            int ikpap;
            if (!secondaryExists) // 2 次側に機器なし
            {
                if (Matches(di.IdentityNumber, "00", 2))
                {
                    continue;
                }

                if (Matches(di.ElectricalParameterSlots[0].P, "000", 3)) // 極数の入力が無い
                {
                    ikpap = string.CompareOrdinal(Fixed(di.CircuitVoltage[0], 3), "105") > 0 ? 2 : 1;
                }
                else
                {
                    ikpap = EquipmentParameterFormatter.Stoi(di.ElectricalParameterSlots[0].P, 3);
                    if (ikpap >= 2) // 1996.08.22 電圧による判定
                    {
                        ikpap = KpavConcat9(di) == "210000000" ? 2 : 1;
                    }
                }
            }
            else // 2 次側に機器あり
            {
                if (Matches(di.ElectricalParameterSlots[0].P, "000", 3)) // 極数の入力が無い
                {
                    if (di.DesignationSuffix == ' ' && !Matches(di.DesignationNumber, "00", 2)) // 共用
                    {
                        int icnt100 = 0;
                        int icnt200 = 0;
                        for (int j = 0; j < mains.Count; j++)
                        {
                            MainCircuitData dj = mains[j].Data;
                            if (!Matches(dj.ReservedWord, "MC      ", 8) && !Matches(dj.ReservedWord, "SSW     ", 8))
                            {
                                continue;
                            }

                            if (Matches(di.DesignationNumber, dj.DesignationNumber, 2))
                            {
                                if (string.CompareOrdinal(Fixed(dj.CircuitVoltage[0], 3), "105") > 0)
                                {
                                    icnt200++;
                                }
                                else
                                {
                                    icnt100++;
                                }
                            }
                        }

                        ikpap = icnt100 + (icnt200 * 2) <= 3 ? CharDigit(di.CircuitPoleCount) : 1;
                    }
                    else // 共用しない
                    {
                        ikpap = CharDigit(di.CircuitPoleCount);
                    }
                }
                else // 極数の入力が有る
                {
                    if (di.DesignationSuffix == ' ' && !Matches(di.DesignationNumber, "00", 2)) // 共用
                    {
                        ikpap = EquipmentParameterFormatter.Stoi(di.ElectricalParameterSlots[0].P, 3);
                        if (ikpap >= 2)
                        {
                            ikpap = KpavConcat9(di) == "210000000" ? 2 : 1;
                        }
                    }
                    else // 共用しない
                    {
                        ikpap = EquipmentParameterFormatter.Stoi(di.ElectricalParameterSlots[0].P, 3);
                    }
                }
            }

            if (ikpap > 0 && ikpap < 4)
            {
                di.UsedPhase = ClearPhaseFrom(di.UsedPhase, ikpap);
            }
        }
    }

    /// <summary>
    /// 機器選定後に RRY+(CT) の使用相を 2 極(N 付き)へ戻す。【C原典】<c>Fyss3D_ResetRRYsou</c>(改訂&lt;26&gt;)。
    /// </summary>
    public static void ResetRRYPhase(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (!Matches(d.ReservedWord, "RRY ", 4) || !Matches(d.DataType[1], "CT ", 3))
            {
                continue;
            }

            if (Matches(d.ElectricalParameterSlots[0].P, "001", 3) ||
                (Matches(d.ElectricalParameterSlots[0].P, "000", 3) &&
                 Matches(d.ElectricalParameterSlots[2].P, "001", 3)))
            {
                d.UsedPhase = SetChar(d.UsedPhase, 1, 'N'); // XN,YN へ修正
                d.ElectricalParameterSlots[2].P = SetChar3(d.ElectricalParameterSlots[2].P, 1, '2', 2, '2'); // 極数 2P
            }
        }
    }

    /// <summary>
    /// 単相 2 線の分岐エレメント数チェック。分岐で CT/CS/ZS/SE/SES がエレメント数 1 なら接続不可エラー。
    /// 【C原典】<c>PropChkElem1P2W</c>(改訂&lt;19&gt;)。エラーは <see cref="CircuitParseError"/> を返す。
    /// </summary>
    public static CircuitParseError? CheckElement1P2W(MainCircuitData main)
    {
        ArgumentNullException.ThrowIfNull(main);

        if (First(main.LineTypeCode) == 'B' && Matches(main.ElectricalParameterSlots[0].E, "1", 1))
        {
            string dt0 = main.DataType[0];
            if (Matches(dt0, "CT ", 3) || Matches(dt0, "CS ", 3) || Matches(dt0, "ZS ", 3) ||
                Matches(dt0, "SE ", 3) || Matches(dt0, "SES ", 4))
            {
                return MakeError("FY-144E", main); // エレメント数が間違っています
            }
        }

        return null;
    }

    /// <summary>
    /// 使用電圧指示エラーチェック。子の負荷電圧 200V・親の負荷電圧 100V ならエラー。
    /// 【C原典】<c>PropCheckUseVolt</c>(改訂&lt;3&gt;)。エラーは子機器の行桁で <see cref="CircuitParseError"/> を返す。
    /// </summary>
    public static CircuitParseError? CheckUseVolt(MainCircuitData oya, MainCircuitData ko)
    {
        ArgumentNullException.ThrowIfNull(oya);
        ArgumentNullException.ThrowIfNull(ko);

        if (Matches(ko.AttachedParameter.LoadVoltage[0], "200", 3) &&
            Matches(oya.AttachedParameter.LoadVoltage[0], "100", 3))
        {
            return MakeError("FY-074E", ko); // 親が 100V のためエラー
        }

        return null;
    }

    /// <summary>
    /// LACSL リレーの負荷電圧チェック。RRY(LA) で極数 1・負荷電圧 200V ならエラー。
    /// 【C原典】<c>PropChkLacslRryFuka</c>(改訂&lt;15&gt;)。エラーは <see cref="CircuitParseError"/> を返す。
    /// </summary>
    public static CircuitParseError? CheckLacslRryLoad(MainCircuitData main)
    {
        ArgumentNullException.ThrowIfNull(main);

        if (Matches(main.ReservedWord, "RRY ", 4) && Matches(main.DataType[1], "LA ", 3))
        {
            if (Matches(main.ElectricalParameterSlots[0].P, "001", 3) &&
                Matches(main.AttachedParameter.LoadVoltage[0], "200", 3))
            {
                return MakeError("FY-074E", main);
            }
        }

        return null;
    }

    private static CircuitParseError MakeError(string code, MainCircuitData d) =>
        new(code, EquipmentParameterFormatter.Stoi(d.DescriptionRow, 3),
            EquipmentParameterFormatter.Stoi(d.DescriptionColumn, 3), "FYMEE90");

    // 回路電圧 3 スロット(各3桁)を連結した 9 桁キー。【C原典】memcmp(&kpav[0],"210000000",9)。
    private static string KpavConcat9(MainCircuitData d) =>
        Fixed(d.CircuitVoltage[0], 3) + Fixed(d.CircuitVoltage[1], 3) + Fixed(d.CircuitVoltage[2], 3);

    private static int CharDigit(char c) => c is >= '0' and <= '9' ? c - '0' : 0;

    private static char First(string s) => s.Length > 0 ? s[0] : ' ';

    // 4 桁固定の使用相の start から末尾までをスペースにする。
    private static string ClearPhaseFrom(string phase, int start)
    {
        char[] arr = phase.PadRight(4)[..4].ToCharArray();
        for (int k = start; k < 4; k++)
        {
            arr[k] = ' ';
        }

        return new string(arr);
    }

    // 3 桁固定文字列の 2 か所を指定文字に置換する(strncpy 相当の部分上書き)。
    private static string SetChar3(string s, int i1, char c1, int i2, char c2)
    {
        char[] arr = s.PadRight(3)[..3].ToCharArray();
        arr[i1] = c1;
        arr[i2] = c2;
        return new string(arr);
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
