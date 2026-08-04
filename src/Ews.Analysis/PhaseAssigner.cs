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

    /// <summary>
    /// 計器回路(CT/計器/表示灯)の使用相をセットする。【C原典】<c>Fyss3D_Keiki_set</c>(950209)。
    /// 回路要素 2(CT 従属計器)・3/4(計器/ヒューズ)・5(ZCT 従属継電器)を <c>PwvTbl</c>/<c>F2Tbl</c> で解決する。
    /// </summary>
    public static void SetMeterCircuitPhase(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);
        int count = mains.Count;

        for (int i = 0; i < count; i++)
        {
            MainCircuitData di = mains[i].Data;

            // 回路要素==2 の使用相セット(CT 従属の AM/WH/AS/THR)
            if (Matches(di.ReservedWord, "CT      ", 8) && di.CircuitElement == '2')
            {
                for (int k = i + 1; k < count; k++)
                {
                    MainCircuitData dk = mains[k].Data;
                    if (dk.CircuitElement != '2')
                    {
                        break;
                    }

                    if (Matches(dk.ReservedWord, "AM      ", 8))
                    {
                        dk.UsedPhase = Matches(dk.DataType[2], "AS     ", 7) ? "KKL " : "KL  ";
                    }

                    if (Matches(dk.ReservedWord, "WH      ", 8) ||
                        Matches(dk.ReservedWord, "AS      ", 8) ||
                        Matches(dk.ReservedWord, "THR     ", 8))
                    {
                        // 【C原典バグ】if(maina[i].dt.ep[2].epaqty = '2') は == のつもりの代入。
                        // 常に真となり "KL  " 分岐は到達不能。代入副作用ごと忠実再現する。
                        di.ElectricalParameterSlots[2].Qty = '2';
                        dk.UsedPhase = "KKL ";
                    }
                }
            }
            // 回路要素==3 or 4 の使用相セット(950215)
            else if (di.CircuitElement == '3' || di.CircuitElement == '4')
            {
                int j = i - 1;
                while (j >= 0 && !Matches(mains[j].Data.ReservedWord, "P       ", 8))
                {
                    j--;
                }

                if (j < 0)
                {
                    continue; // 上流電源 "P" が無い場合はスキップ(C原典は maina[-1] 参照でUB)
                }

                MainCircuitData dj = mains[j].Data;

                for (int k = 0; !string.IsNullOrEmpty(PwvTbl[k].Yoyaku); k++)
                {
                    PwvEntry e = PwvTbl[k];
                    if (!Matches(e.Yoyaku, di.ReservedWord, 8) ||
                        e.Qty != di.ElectricalParameterSlots[0].Qty ||
                        e.Ph != dj.CircuitPhaseCount ||
                        e.Wr != dj.CircuitWireType)
                    {
                        continue;
                    }

                    // 1相2線は回路電圧(105/210)も一致条件に加える
                    if (e.Ph == '1' && e.Wr == '2' && !Matches(e.Kpav, di.CircuitVoltage[0], 3))
                    {
                        continue;
                    }

                    if (e.Siyousou == "F01") // F01 -> F2Tbl 参照(950911)
                    {
                        bool ibrk = false;
                        int l = 0;
                        for (; !string.IsNullOrEmpty(F2Tbl[l].Yoyaku); l++)
                        {
                            if (i + 1 < count &&
                                Matches(mains[i + 1].Data.ReservedWord, F2Tbl[l].Yoyaku, 8) &&
                                mains[i + 1].Data.ElectricalParameterSlots[0].Qty == F2Tbl[l].Qty &&
                                Matches(mains[i + 1].Data.ParentSequenceNumber, mains[i].SequenceNumber, 3))
                            {
                                di.UsedPhase = Overlay(di.UsedPhase, F2Tbl[l].Siyousou);
                                ibrk = true;
                                break;
                            }
                        }

                        if (!ibrk)
                        {
                            di.UsedPhase = Overlay(di.UsedPhase, F2Tbl[l].Siyousou); // 番兵=既定 RS
                        }
                    }
                    else if (e.Siyousou == "X01") // 上流にある F を探し相を決定する(961101)
                    {
                        int iNo = EquipmentParameterFormatter.Stoi(di.ParentSequenceNumber, 3) - 1;
                        while (true)
                        {
                            if (iNo <= 0 || iNo >= count)
                            {
                                break;
                            }

                            MainCircuitData du = mains[iNo].Data;
                            if (Matches(du.ReservedWord, "F       ", 8))
                            {
                                if (du.ElectricalParameterSlots[0].Qty == '2')
                                {
                                    di.UsedPhase = Overlay(di.UsedPhase, "XY ");
                                }

                                break;
                            }

                            iNo = EquipmentParameterFormatter.Stoi(du.ParentSequenceNumber, 3) - 1;
                        }
                    }
                    else
                    {
                        di.UsedPhase = Overlay(di.UsedPhase, e.Siyousou);
                    }

                    break;
                }
            }
        }

        // ZCT, LGR or ELR の使用相(950525)
        for (int i = 0; i < count - 1; i++)
        {
            if (!Matches(mains[i].Data.ReservedWord, "ZCT     ", 8) || mains[i].Data.CircuitElement != '5')
            {
                continue;
            }

            for (int j = i + 1; j < count; j++)
            {
                MainCircuitData dj = mains[j].Data;
                if (dj.CircuitElement != '5')
                {
                    break;
                }

                if (Matches(dj.ReservedWord, "LGR     ", 8) || Matches(dj.ReservedWord, "ELR     ", 8))
                {
                    dj.UsedPhase = "KL  ";
                }
            }
        }
    }

    // 【C原典】Fyss3D_Keiki_set の F2Tbl(950911)。yoyaku[0]=='\0' が番兵(既定 siyousou="RS ")。
    private readonly record struct F2Entry(string Yoyaku, char Qty, string Siyousou);

    private static readonly F2Entry[] F2Tbl =
    {
        new("VS      ", '1', "RT "),
        new("VT      ", '2', "RT "),
        new("", '\0', "RS "),
    };

    // 【C原典】Fyss3D_Keiki_set の PwvTbl。yoyaku/手配数量/回路相/回路線/回路電圧→使用相。
    private readonly record struct PwvEntry(string Yoyaku, char Qty, char Ph, char Wr, string Kpav, string Siyousou);

    private static readonly PwvEntry[] PwvTbl =
    {
        new("F       ", '1', '1', '3', "   ", "X  "),
        new("F       ", '1', '3', '3', "   ", "R  "),
        new("F       ", '1', '1', '2', "105", "X  "),
        new("F       ", '1', '1', '2', "210", "X  "),
        new("F       ", '2', '1', '3', "   ", "XY "),
        new("F       ", '2', '3', '3', "   ", "F01"), // F01 -> F2Tbl 参照
        new("F       ", '2', '1', '2', "105", "XN "),
        new("F       ", '2', '1', '2', "210", "XY "),
        new("F       ", '3', '1', '3', "   ", "XNY"),
        new("F       ", '3', '3', '3', "   ", "RST"),
        new("VT      ", '1', '1', '2', "105", "XN "),
        new("VT      ", '1', '1', '2', "210", "XY "),
        new("VT      ", '2', '1', '3', "   ", "XNY"),
        new("VT      ", '2', '3', '3', "   ", "RST"),
        new("VS      ", '1', '1', '3', "   ", "XNY"),
        new("VS      ", '1', '3', '3', "   ", "RST"),
        new("VM      ", '1', '1', '3', "   ", "XY "),
        new("VM      ", '1', '3', '3', "   ", "RS "),
        new("VM      ", '1', '1', '2', "105", "XN "),
        new("VM      ", '1', '1', '2', "210", "XY "),
        new("WL      ", '1', '1', '3', "   ", "X01"), // 961101
        new("WL      ", '1', '3', '3', "   ", "RS "),
        new("WL      ", '1', '1', '2', "105", "XN "),
        new("WL      ", '1', '1', '2', "210", "XY "),
        new("GL      ", '1', '1', '3', "   ", "X01"), // 961101
        new("GL      ", '1', '3', '3', "   ", "RS "),
        new("GL      ", '1', '1', '2', "105", "XN "),
        new("GL      ", '1', '1', '2', "210", "XY "),
        new("RL      ", '1', '1', '3', "   ", "X01"), // 961101
        new("RL      ", '1', '3', '3', "   ", "RS "),
        new("RL      ", '1', '1', '2', "105", "XN "),
        new("RL      ", '1', '1', '2', "210", "XY "),
        new("OL      ", '1', '1', '3', "   ", "X01"), // 961101
        new("OL      ", '1', '3', '3', "   ", "RS "),
        new("OL      ", '1', '1', '2', "105", "XN "),
        new("OL      ", '1', '1', '2', "210", "XY "),
        new("BL      ", '1', '3', '3', "   ", "X01"), // 961101
        new("BL      ", '1', '1', '2', "105", "XN "),
        new("BL      ", '1', '1', '2', "210", "XY "),
        new("WH      ", '1', '1', '3', "   ", "XNY"),
        new("WH      ", '1', '3', '3', "   ", "RST"),
        new("WH      ", '1', '1', '2', "105", "XN "),
        new("WH      ", '1', '1', '2', "210", "XY "),
        new("", '\0', '\0', '\0', "", ""),
    };

    /// <summary>
    /// 同じ親追番を持つ機器の主回路データ index を用途別(主回路 t／分岐送り ta／その他 tb／ヒューズ tf)に収集する。
    /// 【C原典】<c>PropGetF800Index</c>(改訂&lt;11&gt;&lt;16&gt;&lt;17&gt;&lt;27&gt;)。
    /// </summary>
    public static F800IndexResult CollectF800Index(
        IReadOnlyList<MainCircuitResult> mains, string oyatno, char hycpskbn, char kpaph, char kpawr, char kpap)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(oyatno);

        var r = new F800IndexResult();
        int count = mains.Count;

        for (int m = 0; m < count; m++)
        {
            MainCircuitResult rec = mains[m];
            MainCircuitData d = rec.Data;

            if (Matches(d.ReservedWord, "WH      ", 8) && (d.CircuitElement == '3' || d.CircuitElement == '4'))
            {
                continue; // WH の計器用回路(VT無/付)はパス
            }

            if (Matches(d.ReservedWord, "CT      ", 8) && d.CircuitElement == '2')
            {
                continue; // CT の計器用回路(CT付)はパス
            }

            if (!Matches(d.ParentSequenceNumber, oyatno, 3))
            {
                continue; // 親データ追番が異なる
            }

            if (d.CircuitPhaseCount != kpaph || d.CircuitWireType != kpawr || d.CircuitPoleCount != kpap)
            {
                if ((hycpskbn == '3' || hycpskbn == '7') && Matches(d.LineTypeCode, "BO ", 3))
                {
                    continue; // 特注の "BO" は単独配置のため対象外
                }

                if (kpap == '1')
                {
                    r.Ta.Add(m); // 100V 機器の相振付時に 200V 機器をパスするため登録
                }

                continue; // 回路相数/線式/極数が異なる
            }

            if (IsMc3PParentAlreadySet(mains, oyatno, d))
            {
                continue; // 親が分岐 MC3P で子が 2P なら親と同じ使用相で設定済み
            }

            r.T.Add(m);

            if (IsBranchSender(d, rec))
            {
                if (Matches(d.ReservedWord, "F ", 2))
                {
                    r.Tf.Add(m); // ヒューズは個別に相セット
                }
                else
                {
                    r.Ta.Add(m); // 分岐/送り機器
                }
            }
            else
            {
                r.Tb.Add(m); // その他
            }
        }

        return r;
    }

    /// <summary>
    /// 同じ親追番を持つ機器の index を収集する(3相4線対応)。ヒューズ識別・MC3P 親判定は行わない。
    /// 【C原典】<c>PropGetF800Index34</c>(改訂&lt;11&gt;)。t=主回路／ta=分岐送り／tb=その他。
    /// </summary>
    public static F800IndexResult CollectF800Index34(
        IReadOnlyList<MainCircuitResult> mains, string oyatno, char kpaph, char kpawr, char kpap)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(oyatno);

        var r = new F800IndexResult();
        int count = mains.Count;

        for (int m = 0; m < count; m++)
        {
            MainCircuitResult rec = mains[m];
            MainCircuitData d = rec.Data;

            if (Matches(d.ReservedWord, "WH      ", 8) && (d.CircuitElement == '3' || d.CircuitElement == '4'))
            {
                continue;
            }

            if (Matches(d.ReservedWord, "CT      ", 8) && d.CircuitElement == '2')
            {
                continue;
            }

            if (d.CircuitPhaseCount != kpaph || d.CircuitWireType != kpawr || d.CircuitPoleCount != kpap)
            {
                continue;
            }

            if (!Matches(d.ParentSequenceNumber, oyatno, 3))
            {
                continue;
            }

            r.T.Add(m);

            if (IsBranchSender(d, rec))
            {
                r.Ta.Add(m);
            }
            else
            {
                r.Tb.Add(m);
            }
        }

        return r;
    }

    /// <summary>
    /// 同じ親追番を持つ機器の index を収集する(3相4線・極数別)。
    /// 【C原典】<c>PropGetF800Index34P</c>(改訂&lt;18&gt;)。t=その他／ta=2P 分岐送り／tb=3P 分岐送り。
    /// </summary>
    public static F800IndexResult CollectF800Index34P(
        IReadOnlyList<MainCircuitResult> mains, string oyatno, char kpaph, char kpawr, char kpap)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(oyatno);

        var r = new F800IndexResult();
        int count = mains.Count;

        for (int m = 0; m < count; m++)
        {
            MainCircuitResult rec = mains[m];
            MainCircuitData d = rec.Data;

            if (Matches(d.ReservedWord, "WH      ", 8) && (d.CircuitElement == '3' || d.CircuitElement == '4'))
            {
                continue;
            }

            if (Matches(d.ReservedWord, "CT      ", 8) && d.CircuitElement == '2')
            {
                continue;
            }

            if (d.CircuitPhaseCount != kpaph || d.CircuitWireType != kpawr || d.CircuitPoleCount != kpap)
            {
                continue;
            }

            if (!Matches(d.ParentSequenceNumber, oyatno, 3))
            {
                continue;
            }

            if (IsBranchSender(d, rec))
            {
                if (Matches(d.ElectricalParameterSlots[0].P, "002", 3))
                {
                    r.Ta.Add(m); // 2P 分岐/送り機器
                }
                else if (Matches(d.ElectricalParameterSlots[0].P, "003", 3))
                {
                    r.Tb.Add(m); // 3P 分岐/送り機器
                }
            }
            else
            {
                r.T.Add(m); // その他
            }
        }

        return r;
    }

    /// <summary>
    /// 同じ親追番を持つ機器の index を収集する(3P 機器数も計上)。
    /// 【C原典】<c>PropGetF800Index33</c>(改訂&lt;29&gt;)。t/ta/tb/tf は <see cref="CollectF800Index"/> と同義、
    /// <see cref="F800IndexResult.Count3P"/> に極数 3P の機器数を積む。
    /// </summary>
    public static F800IndexResult CollectF800Index33(
        IReadOnlyList<MainCircuitResult> mains, string oyatno, char hycpskbn, char kpaph, char kpawr, char kpap)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(oyatno);

        var r = new F800IndexResult();
        int count = mains.Count;

        for (int m = 0; m < count; m++)
        {
            MainCircuitResult rec = mains[m];
            MainCircuitData d = rec.Data;

            if (Matches(d.ReservedWord, "WH      ", 8) && (d.CircuitElement == '3' || d.CircuitElement == '4'))
            {
                continue;
            }

            if (Matches(d.ReservedWord, "CT      ", 8) && d.CircuitElement == '2')
            {
                continue;
            }

            if (!Matches(d.ParentSequenceNumber, oyatno, 3))
            {
                continue;
            }

            if (d.CircuitPoleCount == '3')
            {
                r.Count3P++; // 極数 3P 機器数
            }

            if (d.CircuitPhaseCount != kpaph || d.CircuitWireType != kpawr || d.CircuitPoleCount != kpap)
            {
                if ((hycpskbn == '3' || hycpskbn == '7') && Matches(d.LineTypeCode, "BO ", 3))
                {
                    continue;
                }

                if (kpap == '1')
                {
                    r.Ta.Add(m);
                }

                continue;
            }

            if (IsMc3PParentAlreadySet(mains, oyatno, d))
            {
                continue;
            }

            r.T.Add(m);

            if (IsBranchSender(d, rec))
            {
                if (Matches(d.ReservedWord, "F ", 2))
                {
                    r.Tf.Add(m);
                }
                else
                {
                    r.Ta.Add(m);
                }
            }
            else
            {
                r.Tb.Add(m);
            }
        }

        return r;
    }

    // 分岐/送り機器(行種 B/BO/O・回路要素'1'・先頭機器フラグ'1')か。
    private static bool IsBranchSender(MainCircuitData d, MainCircuitResult rec) =>
        (Matches(d.LineTypeCode, "B  ", 3) || Matches(d.LineTypeCode, "BO ", 3) || Matches(d.LineTypeCode, "O  ", 3)) &&
        d.CircuitElement == '1' && rec.Work.LeadingEquipmentFlag == '1';

    // 親が分岐 MC3P で子が 2P、かつ直下(15桁キー strncmp==-1)なら親と同じ使用相で設定済み。
    private static bool IsMc3PParentAlreadySet(IReadOnlyList<MainCircuitResult> mains, string oyatno, MainCircuitData d)
    {
        int oyano = EquipmentParameterFormatter.Stoi(oyatno, 3) - 1;
        if (oyano < 0 || oyano >= mains.Count)
        {
            return false; // C原典は maina[oyano] を無条件参照(範囲外はUB)
        }

        MainCircuitData oya = mains[oyano].Data;
        return Matches(oya.ReservedWord, "MC ", 3) && oya.CircuitPoleCount == '3' &&
               Matches(oya.LineTypeCode, "B  ", 3) && Matches(d.ElectricalParameterSlots[0].P, "002", 3) &&
               StrncmpAix(SeriesKey(oya), SeriesKey(d), 15) == -1;
    }

    // 使用相未設定の WH/CT を同一機器認識番号(doukkno)の設定済み機器の使用相で埋める。【C原典】Fyss3D_PH_Kettei 941227 ループ。
    public static void CopyMeterPhaseByIdentity(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (!Matches(di.UsedPhase, "    ", 4))
            {
                continue; // 使用相が設定済みなら対象外
            }

            if (!Matches(di.ReservedWord, "WH      ", 8) && !Matches(di.ReservedWord, "CT      ", 8))
            {
                continue;
            }

            if (Matches(di.IdentityNumber, "00", 2))
            {
                continue; // 同一機器認識番号なし
            }

            for (int j = 0; j < mains.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                MainCircuitData dj = mains[j].Data;
                if (Matches(di.IdentityNumber, dj.IdentityNumber, 2) && !Matches(dj.UsedPhase, "    ", 4))
                {
                    di.UsedPhase = Fixed(dj.UsedPhase, 4);
                }
            }
        }
    }

    // RRY/RMCB の 2 極を 1 極へ変更し(下流も追随)、AM(電流計)の使用相を整える。【C原典】Fyss3D_PH_Kettei RRY/AM ループ。
    public static void ReducePhaseForRelayAndAmmeter(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (Matches(di.ReservedWord, "RRY     ", 8) || Matches(di.ReservedWord, "RMCB    ", 8))
            {
                if (!Matches(di.ElectricalParameterSlots[0].P, "001", 3) &&
                    !(Matches(di.ElectricalParameterSlots[0].P, "000", 3) &&
                      Matches(di.ElectricalParameterSlots[2].P, "001", 3)))
                {
                    continue;
                }

                // RRY のコンパクトタイプ(CT)/LACSLタイプ(LA)は極数変更なし。【改訂15】
                if (Matches(di.ReservedWord, "RRY ", 4) &&
                    (Matches(di.DataType[1], "CT ", 3) || Matches(di.DataType[1], "LA ", 3)))
                {
                    continue;
                }

                if (Convert2PhaseTo1Phase(di) == 0)
                {
                    continue; // 2極→1極変換対象外なら下流も処理しない
                }

                IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, i + 1);
                if (downstream is not null)
                {
                    foreach (int datano in downstream)
                    {
                        int k = datano - 1;
                        if (k >= 0 && k < mains.Count)
                        {
                            Convert2PhaseTo1Phase(mains[k].Data);
                        }
                    }
                }
            }
            else if (Matches(di.ReservedWord, "AM      ", 8) && di.CircuitElement == '1')
            {
                di.UsedPhase = Matches(di.UsedPhase, "RST ", 4) ? Fixed("S   ", 4) : ClearPhaseFrom(di.UsedPhase, 1);
            }
        }
    }

    // 電源が DC または 1相2線 極数2 のケースの使用相セット(P と同系統の下流を XY)。【C原典】Fyss3D_PH_Kettei DC/1P2W-2 ケース。
    public static void AssignPhaseDcOr1P2WPole2(IReadOnlyList<MainCircuitResult> mains, int pIdx)
    {
        MainCircuitData pd = mains[pIdx].Data;
        pd.UsedPhase = Fixed("XY  ", 4); // P の使用相セット
        for (int j = pIdx + 1; j < mains.Count; j++)
        {
            MainCircuitData dj = mains[j].Data;
            if (!Matches(pd.SystemNumber, dj.SystemNumber, 3))
            {
                break; // 系統番号が変わったら終了
            }

            dj.UsedPhase = Fixed("XY  ", 4); // 下流の使用相セット
        }
    }

    // 電源が 1相2線 極数1 のケースの使用相セット(P と同系統の下流を XN)。【C原典】Fyss3D_PH_Kettei 1P2W-1 ケース(改訂19)。
    public static CircuitParseError? AssignPhase1P2WPole1(IReadOnlyList<MainCircuitResult> mains, int pIdx)
    {
        MainCircuitData pd = mains[pIdx].Data;
        pd.UsedPhase = Fixed("XN  ", 4); // P の使用相セット
        for (int j = pIdx + 1; j < mains.Count; j++)
        {
            MainCircuitData dj = mains[j].Data;
            if (!Matches(pd.SystemNumber, dj.SystemNumber, 3))
            {
                break; // 系統番号が変わったら終了
            }

            dj.UsedPhase = Fixed("XN  ", 4); // 下流の使用相セット

            CircuitParseError? err = CheckElement1P2W(dj); // 改訂19: エレメント数チェック
            if (err is not null)
            {
                return err;
            }
        }

        return null;
    }

    // 1P3W 電源で親が 1P3W 極3 の子機器(childIndex)の使用相をセットする。【C原典】Fyss3D_PH_Kettei 1P3W-親1P3W3 ケース。
    public static void AssignParent1P3WPole3(
        IReadOnlyList<MainCircuitResult> mains, int childIndex, int parentIndex,
        char hycpskbn, ref int mcCount, HashSet<string> processedParents,
        Action<int>? reportDesignError = null)
    {
        MainCircuitData dj = mains[childIndex].Data;
        MainCircuitData dk = mains[parentIndex].Data;

        if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '3' && dj.CircuitPoleCount == '3')
        {
            // MC,TB のケースは MC の相を TB にセットしない(SetPhaseMc2P でセット)。改訂<20><21>
            if (Matches(dk.ReservedWord, "MC ", 3) && Matches(dj.ReservedWord, "TB ", 3) &&
                !Matches(dj.UsedPhase, "   ", 3))
            {
                // なにもしない
            }
            else
            {
                dj.UsedPhase = Fixed(dk.UsedPhase, 4); // 親の使用相をセット
            }

            if (childIndex + 1 < mains.Count) // 原典 j+1 参照の範囲ガード
            {
                SetPhaseMc2P(mains[childIndex], mains[childIndex + 1]); // 改訂<20>
                SetPhaseMc3P(mains[childIndex], mains[childIndex + 1], ref mcCount); // 改訂<16>
            }
        }
        else if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '2' && dj.CircuitPoleCount == '2')
        {
            dj.UsedPhase = Fixed("XY  ", 4);
        }
        else if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '2' && dj.CircuitPoleCount == '1')
        {
            if (!processedParents.Add(Fixed(dj.ParentSequenceNumber, 3)))
            {
                return; // 特殊処理済みならば次のデータへ
            }

            F800IndexResult r = CollectF800Index(mains, dj.ParentSequenceNumber, hycpskbn, '1', '2', '1');
            int[] t = [.. r.T];
            int[] ta = [.. r.Ta];
            int[] tb = [.. r.Tb];
            int[] tf = [.. r.Tf];
            int count = t.Length;
            int countf = tf.Length;

            if (count <= 2)
            {
                SortByParallelNumber(mains, t, count);
                for (int m = 0; m < count; m++)
                {
                    MainCircuitData dm = mains[t[m]].Data;
                    dm.UsedPhase = m % 2 == 0 ? Fixed("XN  ", 4) : Fixed("YN  ", 4);

                    // ヒューズの直後が WL のケースは XN 固定。改訂<10>
                    if (Matches(dm.ReservedWord, "F       ", 8) &&
                        t[m] + 1 < mains.Count && // 原典 1+t[m] 参照の範囲ガード
                        Matches(mains[t[m] + 1].Data.ReservedWord, "WL      ", 8))
                    {
                        dm.UsedPhase = Fixed("XN  ", 4);
                    }

                    SetPhaseMc(dm); // 改訂<3>
                }

                // ヒューズ有りの場合、分岐・送り機器を追い番順に相設定。改訂<11>
                if (countf > 0)
                {
                    SortByParallelNumber(mains, ta, ta.Length);
                    SetPhase100VDevices(mains, ta, ta.Length);
                }
            }
            else
            {
                SortByParallelNumber(mains, ta, ta.Length); // 分岐・送り機器(A Group)
                SetPhase100VDevices(mains, ta, ta.Length);
                SortByParallelNumber(mains, tb, tb.Length); // 分岐・送り機器以外(B Group)
                SetPhase100VDevices(mains, tb, tb.Length);
            }

            SortByParallelNumber(mains, tf, countf); // ヒューズ機器。改訂<11>
            SetPhase100VDevices(mains, tf, countf);
        }
        else
        {
            reportDesignError?.Invoke(1); // FyHcErrFunc(ER_SEKKEI, err_func, 1)
        }
    }

    // 1P3W 電源で親が 1P2W 極2 の子機器(childIndex)の使用相をセットする。【C原典】Fyss3D_PH_Kettei 1P3W-親1P2W2 ケース。
    public static CircuitParseError? AssignParent1P2WPole2(
        IReadOnlyList<MainCircuitResult> mains, int childIndex, int parentIndex,
        HashSet<string> processedParents, Action<int>? reportDesignError = null)
    {
        MainCircuitData dj = mains[childIndex].Data;
        MainCircuitData dk = mains[parentIndex].Data;

        if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '2' &&
            (dj.CircuitPoleCount == '2' || dj.CircuitPoleCount == '1'))
        {
            // 改訂<3><4>: 親 MC で負荷電圧指定無しなら親に合わせて終了。
            if (Matches(dk.ReservedWord, "MC ", 3))
            {
                if (!Matches(dk.AttachedParameter.LoadVoltage[0], "000", 3) &&
                    Matches(dj.AttachedParameter.LoadVoltage[0], "000", 3))
                {
                    dj.UsedPhase = Fixed(dk.UsedPhase, 4);
                    dj.CircuitVoltage[0] = Fixed(dk.CircuitVoltage[0], 3);
                    return null; // 原典 continue
                }
            }

            // LACSL リレーの負荷電圧チェック。改訂<15>
            CircuitParseError? err = CheckLacslRryLoad(dj);
            if (err is not null)
            {
                return err;
            }

            if (dj.CircuitPoleCount == '2') // 回路極数 = 2
            {
                // 改訂<2>: 親が MC/003 で分岐の負荷電圧が 200V のケース。
                if (Matches(dk.ReservedWord, "MC ", 3) ||
                    Matches(dk.ElectricalParameterSlots[0].P, "003", 3))
                {
                    err = CheckUseVolt(dk, dj);
                    if (err is not null)
                    {
                        return err; // 親が 100V のためエラー
                    }

                    if (Matches(dj.AttachedParameter.LoadVoltage[0], "200", 3))
                    {
                        SetParamFor2P200V(dj);
                    }
                }

                // 改訂<1>: 親が MC/003 で分岐の負荷電圧が 100V のケース。
                if (Matches(dk.ReservedWord, "MC ", 3) ||
                    Matches(dk.ElectricalParameterSlots[0].P, "003", 3))
                {
                    if (!processedParents.Add(Fixed(dj.ParentSequenceNumber, 3)))
                    {
                        return null; // 特殊処理済みならば次のデータへ
                    }

                    int count = 0;
                    int[] t = new int[mains.Count];
                    CountVolt100VDevices(mains, dj.ParentSequenceNumber, dk.ReservedWord, t, ref count);
                    if (count > 0)
                    {
                        SortByParallelNumber(mains, t, count);
                        SetPhase100VDevices(mains, t, count);
                        for (int m = 0; m < count; m++)
                        {
                            mains[t[m]].Data.CircuitVoltage[0] = "105"; // 回路電圧
                        }
                    }
                    else
                    {
                        dj.UsedPhase = Fixed(dk.UsedPhase, 4); // 親の使用相をセット
                    }
                }
                else
                {
                    dj.UsedPhase = Fixed(dk.UsedPhase, 4); // 親の使用相をセット
                }
            }
            else // 回路極数 = 1 のケース
            {
                if (Matches(dk.UsedPhase, "XN  ", 4))
                {
                    dj.UsedPhase = Fixed("X   ", 4);
                }
                else if (Matches(dk.UsedPhase, "YN  ", 4))
                {
                    dj.UsedPhase = Fixed("Y   ", 4);
                }
                else if (Matches(dk.UsedPhase, "XY  ", 4))
                {
                    dj.UsedPhase = Fixed("X   ", 4);
                }
                else if (Matches(dk.UsedPhase, "YX  ", 4))
                {
                    dj.UsedPhase = Fixed("X   ", 4);
                }
            }
        }
        else
        {
            reportDesignError?.Invoke(2); // FyHcErrFunc(ER_SEKKEI, err_func, 2)
        }

        return null;
    }

    // 1P3W 電源で親が 1P2W 極1 の子機器(childIndex)の使用相をセットする。【C原典】Fyss3D_PH_Kettei 1P3W-親1P2W1 ケース。
    public static CircuitParseError? AssignParent1P2WPole1(
        IReadOnlyList<MainCircuitResult> mains, int childIndex, int parentIndex,
        HashSet<string> processedParents, Action<int>? reportDesignError = null)
    {
        MainCircuitData dj = mains[childIndex].Data;
        MainCircuitData dk = mains[parentIndex].Data;

        if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '2' && dj.CircuitPoleCount == '1')
        {
            // 改訂<3>: 親が MC/003 で分岐の負荷電圧が 200V のケース。
            if (Matches(dk.ReservedWord, "MC ", 3) ||
                Matches(dk.ElectricalParameterSlots[0].P, "003", 3))
            {
                CircuitParseError? err = CheckUseVolt(dk, dj);
                if (err is not null)
                {
                    return err; // 親が 100V のためエラー
                }

                if (Matches(dj.AttachedParameter.LoadVoltage[0], "200", 3))
                {
                    SetParamFor2P200V(dj);
                }
            }

            // 改訂<3>: MC の子機器の使用相設定。
            if (Matches(dk.ReservedWord, "MC ", 3))
            {
                // 改訂<4>: MC 配下の最大負荷電圧が 200V なら親を 200V 設定。
                if (GetMcChildMaxVolt(mains, mains[parentIndex]) == 200)
                {
                    SetParamFor2P200V(dk);
                }

                if (!processedParents.Add(Fixed(dj.ParentSequenceNumber, 3)))
                {
                    return null; // 特殊処理済みならば次のデータへ
                }

                int count = 0;
                int[] t = new int[mains.Count];
                CountVolt100VDevices(mains, dj.ParentSequenceNumber, dk.ReservedWord, t, ref count); // 改訂<7>
                if (count > 0)
                {
                    SortByParallelNumber(mains, t, count);
                    SetPhase100VDevices(mains, t, count);
                }
                // 改訂<13>: TB3P 対応。
                else if (Matches(dk.ElectricalParameterSlots[0].P, "002", 3) &&
                         Matches(dj.ReservedWord, "TB ", 3) &&
                         Matches(dj.ElectricalParameterSlots[0].P, "003", 3))
                {
                    dk.UsedPhase = Overlay(dk.UsedPhase, "X Y");
                    dj.UsedPhase = Overlay(dj.UsedPhase, "XNY");
                }
                else
                {
                    // MC が負荷電圧指示無しの時、回路電圧100V・使用相XN と出るケース対策。改訂<4>
                    if (Matches(dj.AttachedParameter.LoadVoltage[0], "000", 3))
                    {
                        dj.UsedPhase = Fixed(dk.UsedPhase, 4);
                    }
                }

                // 改訂<8>: 未設定ならば親の使用相をセット。
                if (Matches(dj.UsedPhase, "    ", 4))
                {
                    dj.UsedPhase = Fixed(dk.UsedPhase, 4);
                }
            }
            else
            {
                dj.UsedPhase = Fixed(dk.UsedPhase, 4); // 親の使用相をセット
            }
        }
        else
        {
            // 改訂<9>: 子が 1P3W3 で親が MC(中抜き)なら XNY。
            if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '3' && dj.CircuitPoleCount == '3')
            {
                if (Matches(dk.ReservedWord, "MC ", 3))
                {
                    dj.UsedPhase = Fixed("XNY ", 4);
                }
            }
            else
            {
                reportDesignError?.Invoke(3); // FyHcErrFunc(ER_SEKKEI, err_func, 3)
            }
        }

        return null;
    }

    // 3P3W 電源(powerIndex)配下の同系統機器の使用相をセットする。【C原典】Fyss3D_PH_Kettei 3P3W ケース。
    public static CircuitParseError? AssignPhase3P3W(
        IReadOnlyList<MainCircuitResult> mains, int powerIndex, char hycpskbn,
        Action<int>? reportDesignError = null)
    {
        var processed = new HashSet<string>(); // sumi[1]/sumino[1]
        MainCircuitData dp = mains[powerIndex].Data;
        dp.UsedPhase = "RST "; // P の使用相セット

        for (int j = powerIndex + 1; j < mains.Count; j++)
        {
            MainCircuitData dj = mains[j].Data;
            if (!Matches(dp.SystemNumber, dj.SystemNumber, 3))
            {
                break; // 系統番号が変わったら終了
            }

            // 改訂<22><28>: プラグインタイプは設定不要。
            if ((hycpskbn == '3' || hycpskbn == '7') &&
                (Matches(dj.DataType[0], "CH", 2) || Matches(dj.DataType[0], "KP", 2)))
            {
                continue;
            }

            int k = EquipmentParameterFormatter.Stoi(dj.ParentSequenceNumber, 3) - 1;
            if (k < 0 || k >= mains.Count)
            {
                continue; // 親要素番号の範囲ガード(原典 UB)
            }

            MainCircuitData dk = mains[k].Data;

            // 親が 3相3線 極数3 のケース。
            if (dk.CircuitPhaseCount == '3' && dk.CircuitWireType == '3' && dk.CircuitPoleCount == '3')
            {
                if (dj.CircuitPhaseCount == '3' && dj.CircuitWireType == '3' && dj.CircuitPoleCount == '3')
                {
                    dj.UsedPhase = Fixed(dk.UsedPhase, 4); // 親の使用相をセット
                }
                else if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '2' && dj.CircuitPoleCount == '2')
                {
                    if (!processed.Add(Fixed(dj.ParentSequenceNumber, 3)))
                    {
                        continue; // 特殊処理済みならば次のデータへ
                    }

                    F800IndexResult r = CollectF800Index33(mains, dj.ParentSequenceNumber, hycpskbn, '1', '2', '2');
                    int[] t = [.. r.T];
                    int[] ta = [.. r.Ta];
                    int[] tb = [.. r.Tb];
                    int[] tf = [.. r.Tf];
                    int count = t.Length, counta = ta.Length, countf = tf.Length;

                    if (count <= 2)
                    {
                        SortByParallelNumber(mains, t, count);
                        SetPhase3P3WDevices(mains, t, count); // RS/ST セット
                        // 改訂<29><30>: 3P 機器混在かつ分岐/送りとヒューズ有りなら先頭2件を RS 固定。
                        if (count >= 2 && r.Count3P > 0 && counta >= 1 && countf >= 1)
                        {
                            mains[t[0]].Data.UsedPhase = "RS  ";
                            mains[t[1]].Data.UsedPhase = "RS  ";
                        }
                    }
                    else
                    {
                        SortByParallelNumber(mains, ta, counta);
                        SetPhase3P3WDevices(mains, ta, counta); // RS/ST/TR セット
                        SortByParallelNumber(mains, tb, tb.Length);
                        SetPhase3P3WDevices(mains, tb, tb.Length);
                    }

                    // ヒューズ機器の使用相設定。改訂<11>
                    SortByParallelNumber(mains, tf, countf);
                    SetPhase3P3WDevices(mains, tf, countf);
                }
                // 改訂<5>: 親が MC で子が 3P のケース。
                else if (Matches(dk.ReservedWord, "MC ", 3) &&
                         Matches(dj.ElectricalParameterSlots[0].P, "003", 3))
                {
                    if (Matches(dj.AttachedParameter.LoadVoltage[0], "000", 3))
                    {
                        dj.CircuitPoleCount = dk.CircuitPoleCount;             // 回路極数
                        dj.CircuitVoltage[0] = Fixed(dk.CircuitVoltage[0], 3); // 回路電圧
                        dj.UsedPhase = Fixed(dk.UsedPhase, 4);                 // 使用相
                    }
                }
                else
                {
                    reportDesignError?.Invoke(4); // FyHcErrFunc(ER_SEKKEI, err_func, 4)
                }
            }

            // 親が 1相2線 極数2 のケース(原典は別 if)。
            if (dk.CircuitPhaseCount == '1' && dk.CircuitWireType == '2' && dk.CircuitPoleCount == '2')
            {
                // 改訂<5>: 親が MC で子が 3P のケース。
                if (Matches(dk.ReservedWord, "MC ", 3) &&
                    Matches(dj.ElectricalParameterSlots[0].P, "003", 3))
                {
                    dj.CircuitPhaseCount = '3';   // 回路相数
                    dj.CircuitWireType = '3';     // 回路線式
                    dj.CircuitPoleCount = '3';    // 回路極数
                    dj.CircuitVoltage[0] = "210"; // 回路電圧
                    dj.UsedPhase = "RST ";        // 使用相
                }
                else if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '2' && dj.CircuitPoleCount == '2')
                {
                    dj.UsedPhase = Fixed(dk.UsedPhase, 4); // 親の使用相をセット
                }
                else
                {
                    reportDesignError?.Invoke(5); // FyHcErrFunc(ER_SEKKEI, err_func, 5)
                }
            }
        }

        return null;
    }

    // 3P4W(回路電圧 v2 なし)電源(powerIndex)配下の同系統機器の使用相をセットする。【C原典】Fyss3D_PH_Kettei 3P4Wv=0 ケース。
    public static CircuitParseError? AssignPhase3P4WNoV2(
        IReadOnlyList<MainCircuitResult> mains, int powerIndex,
        Action<int>? reportDesignError = null)
    {
        var processed2 = new HashSet<string>(); // sumi[2]/sumino[2]
        var processed3 = new HashSet<string>(); // sumi[3]/sumino[3]
        var processed4 = new HashSet<string>(); // sumi[4]/sumino[4]
        MainCircuitData dp = mains[powerIndex].Data;
        dp.UsedPhase = "RSTN"; // P の使用相セット

        for (int j = powerIndex + 1; j < mains.Count; j++)
        {
            MainCircuitData dj = mains[j].Data;
            if (!Matches(dp.SystemNumber, dj.SystemNumber, 3))
            {
                break; // 系統番号が変わったら終了
            }

            int k = EquipmentParameterFormatter.Stoi(dj.ParentSequenceNumber, 3) - 1;
            if (k < 0 || k >= mains.Count)
            {
                continue; // 親要素番号の範囲ガード(原典 UB)
            }

            MainCircuitData dk = mains[k].Data;

            // 親が 3相4線 極数4 v=0 のケース。
            if (dk.CircuitPhaseCount == '3' && dk.CircuitWireType == '4' && dk.CircuitPoleCount == '4' &&
                Matches(dk.CircuitVoltage[2], "000", 3))
            {
                if (dj.CircuitPhaseCount == '3' && dj.CircuitWireType == '4' && dj.CircuitPoleCount == '4')
                {
                    dj.UsedPhase = Fixed(dk.UsedPhase, 4); // 親の使用相をセット
                }
                else if (dj.CircuitPhaseCount == '3' && dj.CircuitWireType == '3' && dj.CircuitPoleCount == '3')
                {
                    dj.UsedPhase = "RST ";
                }
                else if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '2' && dj.CircuitPoleCount == '2')
                {
                    if (!processed2.Add(Fixed(dj.ParentSequenceNumber, 3)))
                    {
                        continue; // 特殊処理済みならば次のデータへ
                    }

                    F800IndexResult r = CollectF800Index34(mains, dj.ParentSequenceNumber, '1', '2', '2');
                    int[] t = [.. r.T];
                    int[] ta = [.. r.Ta];
                    int[] tb = [.. r.Tb];
                    if (t.Length <= 2)
                    {
                        SortByParallelNumber(mains, t, t.Length);
                        SetPhase3P3WDevices(mains, t, t.Length); // RS/ST セット
                    }
                    else
                    {
                        SortByParallelNumber(mains, ta, ta.Length);
                        SetPhase3P3WDevices(mains, ta, ta.Length); // RS/ST/TR セット
                        SortByParallelNumber(mains, tb, tb.Length);
                        SetPhase3P3WDevices(mains, tb, tb.Length);
                    }
                }
                else if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '2' && dj.CircuitPoleCount == '1')
                {
                    if (!processed3.Add(Fixed(dj.ParentSequenceNumber, 3)))
                    {
                        continue; // 特殊処理済みならば次のデータへ
                    }

                    F800IndexResult r = CollectF800Index34(mains, dj.ParentSequenceNumber, '1', '2', '1');
                    int[] t = [.. r.T];
                    int[] ta = [.. r.Ta];
                    int[] tb = [.. r.Tb];
                    if (t.Length <= 2)
                    {
                        SortByParallelNumber(mains, t, t.Length);
                        SetPhase3P4WDevices(mains, t, t.Length); // RN/SN セット
                    }
                    else
                    {
                        SortByParallelNumber(mains, ta, ta.Length);
                        SetPhase3P4WDevices(mains, ta, ta.Length); // RN/SN/TN セット
                        SortByParallelNumber(mains, tb, tb.Length);
                        SetPhase3P4WDevices(mains, tb, tb.Length);
                    }
                }
                else
                {
                    reportDesignError?.Invoke(6); // FyHcErrFunc(ER_SEKKEI, err_func, 6)
                }
            }

            // 親が 3相3線 極数3 のケース(原典は別 if)。
            if (dk.CircuitPhaseCount == '3' && dk.CircuitWireType == '3' && dk.CircuitPoleCount == '3')
            {
                if (dj.CircuitPhaseCount == '3' && dj.CircuitWireType == '3' && dj.CircuitPoleCount == '3')
                {
                    dj.UsedPhase = Fixed(dk.UsedPhase, 4); // 親の使用相をセット
                }
                else if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '2' && dj.CircuitPoleCount == '2')
                {
                    if (!processed4.Add(Fixed(dj.ParentSequenceNumber, 3)))
                    {
                        continue; // 特殊処理済みならば次のデータへ
                    }

                    F800IndexResult r = CollectF800Index34(mains, dj.ParentSequenceNumber, '1', '2', '2');
                    int[] t = [.. r.T];
                    int[] ta = [.. r.Ta];
                    int[] tb = [.. r.Tb];
                    if (t.Length <= 2)
                    {
                        SortByParallelNumber(mains, t, t.Length);
                        SetPhase3P3WDevices(mains, t, t.Length); // RS/ST セット
                    }
                    else
                    {
                        SortByParallelNumber(mains, ta, ta.Length);
                        SetPhase3P3WDevices(mains, ta, ta.Length); // RS/ST/TR セット
                        SortByParallelNumber(mains, tb, tb.Length);
                        SetPhase3P3WDevices(mains, tb, tb.Length);
                    }
                }
                else
                {
                    reportDesignError?.Invoke(7); // FyHcErrFunc(ER_SEKKEI, err_func, 7)
                }
            }

            // 親が 1相2線 極数2 のケース(原典は別 if)。
            if (dk.CircuitPhaseCount == '1' && dk.CircuitWireType == '2' && dk.CircuitPoleCount == '2')
            {
                if (dj.CircuitPhaseCount == '1' && dj.CircuitWireType == '2' && dj.CircuitPoleCount == '2')
                {
                    dj.UsedPhase = Fixed(dk.UsedPhase, 4); // 親の使用相をセット
                }
                else
                {
                    reportDesignError?.Invoke(8); // FyHcErrFunc(ER_SEKKEI, err_func, 8)
                }
            }
        }

        return null;
    }

    // 3P4W と繋がっている 1P3W の使用相 XNY を RNS に変更する。【C原典】PropChgSiyousou(改訂32)。
    public static void ChangeSiyousouFor3P4W(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (!Matches(di.ReservedWord, "P       ", 8))
            {
                continue;
            }

            if (di.CircuitPhaseCount != '1' || di.CircuitWireType != '3')
            {
                continue; // 電源が1相3線のみ対象
            }

            if (IsConnectedTo3P4W(mains, i) != 1)
            {
                continue; // 3P4Wとの繋がり無し
            }

            // 処理中電源以降に同じ行種番号を持つ電源があればパスする。
            bool laterSameLine = false;
            for (int j = i + 1; j < mains.Count; j++)
            {
                MainCircuitData dj = mains[j].Data;
                if (Matches(dj.ReservedWord, "P       ", 8) &&
                    Matches(dj.LineTypeNumber, di.LineTypeNumber, 2))
                {
                    laterSameLine = true;
                    break;
                }
            }

            if (laterSameLine)
            {
                continue;
            }

            di.UsedPhase = Fixed("RNS ", 4);

            for (int j = i + 1; j < mains.Count; j++)
            {
                MainCircuitData dj = mains[j].Data;
                if (!Matches(di.SystemNumber, dj.SystemNumber, 3))
                {
                    break; // 系統番号が変わったら終了
                }

                char[] arr = dj.UsedPhase.PadRight(4)[..4].ToCharArray();
                for (int k = 0; k < 4; k++)
                {
                    if (arr[k] == 'X')
                    {
                        arr[k] = 'R';
                    }
                    else if (arr[k] == 'Y')
                    {
                        arr[k] = 'S';
                    }
                }

                dj.UsedPhase = new string(arr);
            }
        }
    }

    // 3相4線と繋がりがある電源か判定する。【C原典】PropConnect3P4W(改訂32)。0:無し 1:有り。
    private static int IsConnectedTo3P4W(IReadOnlyList<MainCircuitResult> mains, int pIdx)
    {
        MainCircuitData p = mains[pIdx].Data;
        if (p.CircuitPhaseCount != '1' || p.CircuitWireType != '3')
        {
            return 0;
        }

        if (EquipmentParameterFormatter.Stoi(p.LineTypeNumber, 2) == 0)
        {
            return 0; // 行種番号がない場合は繋がりなし
        }

        for (int m = 0; m < mains.Count; m++)
        {
            if (m == pIdx)
            {
                continue;
            }

            MainCircuitData dm = mains[m].Data;
            if (Matches(dm.LineTypeCode, "P  ", 3) &&
                Matches(dm.LineTypeNumber, p.LineTypeNumber, 2) &&
                dm.CircuitPhaseCount == '3' && dm.CircuitWireType == '4')
            {
                return 1;
            }
        }

        return 0;
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

/// <summary>
/// <c>PropGetF800Index*</c> が収集する主回路データ index の用途別テーブル。
/// 【C原典】Fyss3D.c の t/ta/tb/tf/count3P 出力引数群に対応する。
/// </summary>
public sealed class F800IndexResult
{
    /// <summary>主回路 index。【C原典】t[]／PropGetF800Index34P では「その他」。</summary>
    public List<int> T { get; } = new();

    /// <summary>分岐/送り機器 index。【C原典】ta[]／PropGetF800Index34P では「2P」。</summary>
    public List<int> Ta { get; } = new();

    /// <summary>その他機器 index。【C原典】tb[]／PropGetF800Index34P では「3P」。</summary>
    public List<int> Tb { get; } = new();

    /// <summary>ヒューズ機器 index。【C原典】tf[]。</summary>
    public List<int> Tf { get; } = new();

    /// <summary>極数 3P の機器数。【C原典】count3P(PropGetF800Index33 のみ)。</summary>
    public int Count3P { get; set; }
}
