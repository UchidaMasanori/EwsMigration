using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 負荷発生元設定(<c>Fyss31_FukaHassei_Set</c>)。末端機器から負荷容量・通電電流値を辿り、
/// 各主回路データに負荷発生元区分(<c>ahassei</c>)と通電電流値(<c>denryu</c>)を設定する。
///
/// 【C原典】toku/sekkei/src/Fyss31.c。本体オーケストレータ(<see cref="SetLoadSource"/>＝
/// <c>Fyss31_FukaHassei_Set</c>)・エラー生成(<see cref="MakeError"/>＝<c>set_error</c>)・
/// 負荷容量決定(<see cref="SelectLoadCurrent"/>＝<c>set_fky</c>／<c>get_ep</c>)を対象とする。
/// 係数×値(AT/A1/A2)または <see cref="EnergizingCurrentCalculator"/>(=set_denryu, W/VA)で電流化する。
/// ＳＣ(系統)の通電電流値算出(<c>SC_Keitou_Proc</c>, Fyss39/Fyss3A の未移植関数群依存)は
/// 添字を受け取るデリゲート(引数注入)で境界化する。
/// </summary>
public static class LoadSourceSelector
{
    /// <summary>選定成功(電流値・優先順位を設定)。【C原典】set_fky 戻り値 0。</summary>
    public const int Selected = 0;

    /// <summary>電気パラメータの入力が無い。【C原典】set_fky 戻り値 1。</summary>
    public const int NoValue = 1;

    /// <summary>候補予約語の優先順位が現best より低い。【C原典】set_fky 戻り値 2。</summary>
    public const int LowerPriority = 2;

    /// <summary>負荷容量決定テーブルに予約語が無い(C原典は未 return=UB、本移行は明示コード)。</summary>
    public const int NotInTable = 3;

    /// <summary>
    /// 候補機器(candidateIndex)の負荷電流値と予約語優先順位を負荷容量決定テーブルから求める。
    /// 【C原典】set_fky(maina, fky)。fky[0].pry = <paramref name="bestPriority"/>、fky[1].fno = candidateIndex。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】maina。</param>
    /// <param name="candidateIndex">候補の主回路添字(0始まり)。【C原典】fky[1].fno。</param>
    /// <param name="bestPriority">現時点で最良の予約語優先順位。【C原典】fky[0].pry。</param>
    /// <param name="priority">求めた候補の予約語優先順位。【C原典】fky[1].pry。</param>
    /// <param name="current">求めた負荷電流値。【C原典】fky[1].denryu。</param>
    public static int SelectLoadCurrent(
        IReadOnlyList<MainCircuitResult> mains,
        int candidateIndex,
        int bestPriority,
        out int priority,
        out double current)
    {
        ArgumentNullException.ThrowIfNull(mains);

        priority = 0;
        current = 0.0;

        MainCircuitResult candidate = mains[candidateIndex];
        LoadCapacityEntry? entry = LoadCapacityDecisionTable.Find(candidate.Data.ReservedWord);
        if (entry is null)
        {
            return NotInTable;
        }

        // 候補予約語の優先順位が best より低い(数値が大きい)なら不採用。
        if (entry.WordPriority > bestPriority)
        {
            return LowerPriority;
        }

        // 優先順位 1→3 の順に、対応する電気パラメータで電流化を試みる。
        for (int k = 1; k <= 3; k++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (entry.ElectricalPriority[j] != k)
                {
                    continue;
                }

                if (TryGetParameterCurrent(candidate, entry, j, out current))
                {
                    priority = entry.WordPriority;
                    return Selected;
                }

                break; // 当該優先順位の電気パラメータは入力無し → 次の優先順位へ。
            }
        }

        return NoValue;
    }

    /// <summary>
    /// 負荷発生元区分(<c>ahassei</c>)と通電電流値(<c>denryu</c>)を全主回路データに設定する。
    /// 末端機器から負荷容量を辿り、直近末端→同一並列/階層→上流遡り(同一行種グループ)の順で
    /// 負荷発生元を決定する。異常時は <see cref="CircuitParseError"/> を返す(C原典の return(2) 相当)。
    /// 正常時は <c>null</c>(return(0))。
    ///
    /// 【C原典】Fyss31_FukaHassei_Set(Pmainc, maina, Perrc, erra)(Fyss31.c)。
    /// C原典の戻り値 1(構成機器 NOT FOUND)は実際には返らないため本移行では扱わない。
    /// </summary>
    /// <param name="mains">主回路エリア(有効件数分)。【C原典】maina[0..Pmainc)。</param>
    /// <param name="productionSpec">製作仕様(改訂&lt;5&gt;)。set_denryu へ委譲。【C原典】seisakushiyou。</param>
    /// <param name="processSystemCircuit">
    /// ＳＣ(系統)の通電電流値算出(<c>SC_Keitou_Proc</c>)。Fyss39/Fyss3A(未移植)依存のため
    /// 添字を受け取るデリゲートで境界化する(引数注入)。null なら SC 分岐は処理しない。
    /// </param>
    public static CircuitParseError? SetLoadSource(
        IReadOnlyList<MainCircuitResult> mains,
        int productionSpec = 1,
        Action<int>? processSystemCircuit = null)
    {
        ArgumentNullException.ThrowIfNull(mains);

        int pmainc = mains.Count;

        // １．初期化(負荷発生元区分・通電電流値)。sprintf("%08.2f",0.0) = "00000.00"。
        string zeroCurrent = Denryu8(0.0);
        for (int i = 0; i < pmainc; i++)
        {
            MainCircuitData d = mains[i].Data;
            d.LoadSourceKind = ' ';
            d.EnergizingCurrent = zeroCurrent;
        }

        // 950428 １－２型(CSDT/MCDT)で両方とも負荷発生元エラーになる場合の記憶。
        List<int> dtPnt = [];

        // 1997.03.31 CT,AM(LW=1.75KW)特殊処理:
        // CT が有る時、ペアの計器回路 AM に負荷容量記述があれば CT へコピーする。
        for (int i = 0; i < pmainc; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (Eq(di.ReservedWord, "CT      ", 8) &&
                !Eq(di.IdentityNumber, "00", 2) &&
                di.CircuitElement == '1' &&
                Eq(di.AttachedParameter.LoadCapacity, "0000000", 7))
            {
                for (int j = 0; j < i; j++)
                {
                    MainCircuitData dj = mains[j].Data;
                    if (Eq(di.HierarchyNumber, dj.HierarchyNumber, 3) &&
                        Eq(di.DescriptionRow, dj.DescriptionRow, 3) &&
                        Eq(dj.ReservedWord, "AM      ", 8) &&
                        dj.CircuitElement == '2' &&
                        !Eq(dj.AttachedParameter.LoadCapacity, "0000000", 7))
                    {
                        di.AttachedParameter.LoadKind = dj.AttachedParameter.LoadKind;
                        di.AttachedParameter.LoadCapacity = dj.AttachedParameter.LoadCapacity;
                        di.AttachedParameter.LoadUnitKind = dj.AttachedParameter.LoadUnitKind;
                        di.AttachedParameter.LoadName[0] = dj.AttachedParameter.LoadName[0];
                        di.AttachedParameter.LoadName[1] = dj.AttachedParameter.LoadName[1];
                    }
                }
            }
        }

        // ２．負荷発生元区分・通電電流値設定
        for (int i = 0; i < pmainc; i++)
        {
            MainCircuitData di = mains[i].Data;

            // 改訂<1>: 自分の子が全て計器類(kiryoso=='3')なら通電電流値設定を行う(ELB/MCB 限定)。
            int sw = 0;
            if ((Eq(di.ReservedWord, "ELB     ", 8) || Eq(di.ReservedWord, "MCB     ", 8)) &&
                (di.CircuitElement != '1' || di.TerminalKind != '1'))
            {
                sw = 1;
                for (int j = 0; j < pmainc; j++)
                {
                    if (Eq(mains[i].SequenceNumber, mains[j].Data.ParentSequenceNumber, 3))
                    {
                        if (mains[j].Data.CircuitElement != '3')
                        {
                            sw = 0; // 計器回路以外が子にあった
                            break;
                        }
                    }
                }
            }

            // 改訂<3>: MMCB が 3P だけの記述で二次側に F がある場合の容量セット漏れ対応。
            if (Eq(di.ReservedWord, "MMCB    ", 8) && !Eq(di.AttachedParameter.LoadCapacity, "0000000", 7))
            {
                sw = 1;
            }

            bool terminal = di.CircuitElement == '1' && di.TerminalKind == '1' &&
                            !Eq(di.ReservedWord, "SC      ", 8) && !Eq(di.ReservedWord, "NT      ", 8);
            if (terminal || sw == 1)
            {
                // 並列追番・階層番号・入線番号が同じで、負荷容量が０でないものを検索。
                int fno = -1;
                for (int j = 0; j < pmainc; j++)
                {
                    MainCircuitData dj = mains[j].Data;
                    if (Eq(dj.ParallelNumber, di.ParallelNumber, 3) &&
                        Eq(dj.HierarchyNumber, di.HierarchyNumber, 3) &&
                        Eq(dj.IncomingNumber, di.IncomingNumber, 3) &&
                        !Eq(dj.AttachedParameter.LoadCapacity, "0000000", 7))
                    {
                        fno = j;
                        break;
                    }
                }

                if (fno != -1)
                {
                    // 発見: 負荷容量から通電電流値を算出(set_denryu)。
                    MainCircuitData df = mains[fno].Data;
                    double fuka = EquipmentParameterFormatter.Stof(df.AttachedParameter.LoadCapacity, 7);
                    if (EnergizingCurrentCalculator.TryCalculate(
                            df, fuka, df.AttachedParameter.LoadKind, out double denryu, productionSpec))
                    {
                        df.LoadSourceKind = '1';
                        df.EnergizingCurrent = Denryu8(denryu);
                    }
                    else
                    {
                        return MakeError(mains, fno); // FY-560E "001"
                    }
                }
                else
                {
                    // 未発見: 同一並列・階層・入線の中で電気パラメータを持つものを検索(set_fky)。
                    int count = 0;
                    int bestFno = -1;
                    double bestDenryu = 99999.99;
                    int bestPry = 4;
                    for (int j = 0; j < pmainc; j++)
                    {
                        MainCircuitData dj = mains[j].Data;
                        if (Eq(dj.ParallelNumber, di.ParallelNumber, 3) &&
                            Eq(dj.IncomingNumber, di.IncomingNumber, 3) &&
                            Eq(dj.HierarchyNumber, di.HierarchyNumber, 3))
                        {
                            if (SelectLoadCurrent(mains, j, bestPry, out int pry, out double cur) == Selected &&
                                (bestPry > pry || (bestPry == pry && bestDenryu > cur)))
                            {
                                bestFno = j;
                                bestPry = pry;
                                bestDenryu = cur;
                                count++;
                            }
                        }
                    }

                    // 1996.01.10: F の場合は 951005 追加分の処理を行わない。
                    if (!Eq(di.ReservedWord, "F       ", 8))
                    {
                        // 951005: MCB2P -- MC,TB2P … の記述時、負荷発生元を MCB にするため上流を辿る。
                        if (count != 0)
                        {
                            int fkyFno = bestFno;
                            double fkyDenryu = bestDenryu;

                            count = 0;
                            bestFno = -1;
                            bestDenryu = 99999.99;
                            bestPry = 4;

                            int j = EquipmentParameterFormatter.Stoi(di.ParentSequenceNumber, 3) - 1;
                            while (j >= 0 && j < pmainc)
                            {
                                MainCircuitData dj = mains[j].Data;
                                if (!Eq(di.LineTypeCode, dj.LineTypeCode, 3) ||
                                    !Eq(di.LineTypeGroupNumber, dj.LineTypeGroupNumber, 3))
                                {
                                    break;
                                }

                                if (SelectLoadCurrent(mains, j, bestPry, out int pry, out double cur) == Selected &&
                                    (bestPry > pry || (bestPry == pry && bestDenryu > cur)))
                                {
                                    bestFno = j;
                                    bestPry = pry;
                                    bestDenryu = cur;
                                    count++;
                                }

                                j = EquipmentParameterFormatter.Stoi(dj.ParentSequenceNumber, 3) - 1;
                            }

                            if (count == 0)
                            {
                                bestFno = fkyFno;
                                bestDenryu = fkyDenryu;
                                count = 1;
                            }
                            else
                            {
                                // 改訂<2>: TB の親が食い違う時、先の親(MC)を有効とする。
                                if (bestFno != fkyFno)
                                {
                                    if (!Eq(mains[fkyFno].Data.ReservedWord, "MC ", 3) ||
                                        !Eq(di.ReservedWord, "TB ", 3))
                                    {
                                        count = 0;
                                    }
                                }
                            }
                        }
                    }

                    if (count != 0)
                    {
                        // 発見
                        mains[bestFno].Data.LoadSourceKind = '1';
                        mains[bestFno].Data.EnergizingCurrent = Denryu8(bestDenryu);
                    }
                    else
                    {
                        // 未発見: 同一行種グループを上流方向へ検索する。
                        List<int> interElem = [];
                        int j = i;
                        while (true)
                        {
                            interElem.Add(j);
                            j = EquipmentParameterFormatter.Stoi(mains[j].Data.ParentSequenceNumber, 3) - 1;
                            if (j < 0)
                            {
                                return MakeError(mains, i); // FY-560E "002"
                            }

                            bestDenryu = 99999.99;
                            bestPry = 4;
                            if (j < pmainc && Eq(mains[j].Data.LineTypeGroupNumber, di.LineTypeGroupNumber, 3))
                            {
                                if (SelectLoadCurrent(mains, j, bestPry, out int pry, out double cur) == Selected)
                                {
                                    int srcFno = j;
                                    double srcDenryu = cur;
                                    if (bestPry > pry || (bestPry == pry && bestDenryu > cur))
                                    {
                                        srcFno = j;
                                        srcDenryu = cur;
                                    }

                                    mains[srcFno].Data.LoadSourceKind = '1';
                                    string buff = Denryu8(srcDenryu);
                                    mains[srcFno].Data.EnergizingCurrent = buff;
                                    foreach (int elem in interElem)
                                    {
                                        mains[elem].Data.EnergizingCurrent = buff;
                                    }

                                    break;
                                }
                            }
                            else
                            {
                                // １－２型(CSDT/MCDT)の考慮 950428。
                                if ((Eq(di.ReservedWord, "CSDT    ", 8) || Eq(di.ReservedWord, "MCDT    ", 8)) &&
                                    !Eq(di.DesignationNumber, "00", 2))
                                {
                                    dtPnt.Add(i);
                                    break;
                                }

                                if (Eq(di.ReservedWord, "F       ", 8) &&
                                    !Eq(di.AttachedParameter.ControlPowerNumber, "  ", 2))
                                {
                                    di.EnergizingCurrent = "00000.80";
                                    di.SearchAgainFlag = '1';
                                    break;
                                }

                                // 改訂<9>: 27 端子台(tokkbn=='6')は負荷発生元エラーとしない。
                                if (di.SpecialReservedWordKind == '6')
                                {
                                    break;
                                }

                                return MakeError(mains, i); // FY-560E "002"
                            }
                        }
                    }
                }
            }
            else
            {
                // 950929 ＳＣ(系統)の通電電流値設定。Fyss39/Fyss3A 依存はデリゲートで境界化。
                if (di.CircuitElement == '1' && di.TerminalKind == '1' &&
                    Eq(di.ReservedWord, "SC      ", 8) &&
                    Eq(di.AttachedParameter.LoadName[1], "0KW", 3))
                {
                    processSystemCircuit?.Invoke(i);
                }
            }
        }

        // 950428 １－２型で両方とも負荷発生元エラーになる場合、エラー処理を行う。
        for (int a = 0; a < dtPnt.Count - 1; a++)
        {
            for (int b = a + 1; b < dtPnt.Count; b++)
            {
                MainCircuitData da = mains[dtPnt[a]].Data;
                MainCircuitData db = mains[dtPnt[b]].Data;
                if (Eq(da.ReservedWord, db.ReservedWord, 8) &&
                    Eq(da.DesignationNumber, db.DesignationNumber, 2))
                {
                    return MakeError(mains, dtPnt[a]); // FY-560E "002"
                }
            }
        }

        // 改訂<4>: 主幹機器の通電電流をセット。
        for (int i = 0; i < pmainc; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (di.CircuitClass != 'M' || di.CircuitElement != '1')
            {
                continue;
            }

            // 負荷容量有りで、かつ通電電流がなければ通電電流をセット。
            if (!Eq(di.EnergizingCurrent, "00000.00", 8) || Eq(di.AttachedParameter.LoadCapacity, "0000000", 7))
            {
                continue; // 通電電流算出済み or 負荷容量なし
            }

            double fuka = EquipmentParameterFormatter.Stof(di.AttachedParameter.LoadCapacity, 7);
            if (EnergizingCurrentCalculator.TryCalculate(
                    di, fuka, di.AttachedParameter.LoadKind, out double denryu, productionSpec))
            {
                di.EnergizingCurrent = Denryu8(denryu);
            }
        }

        return null;
    }

    /// <summary>
    /// エラー情報を生成する。【C原典】set_error(errcode, maina, no, mapid, Perrc, erra, debug)。
    /// 行番号 = gyo、桁 = atoi(keta)。デバッグ情報("001"/"002")は <see cref="CircuitParseError"/> に
    /// 対応フィールドが無いため保持しない(MainChk 先例に準拠)。
    /// </summary>
    private static CircuitParseError MakeError(IReadOnlyList<MainCircuitResult> mains, int no)
    {
        MainCircuitData d = mains[no].Data;
        return new CircuitParseError("FY-560E", ParseNumber(d.DescriptionRow),
            EquipmentParameterFormatter.Stoi(d.DescriptionColumn, 3), "FYMEE80");
    }

    // 【C原典】sprintf(buff,"%08.2f",value)。8 バイト固定長(strncpy(,,8) 相当)へ整形。
    private static string Denryu8(double value)
    {
        string s = EquipmentParameterFormatter.SprintfF("%08.2f", value);
        return s.Length > 8 ? s[..8] : s;
    }

    // 【C原典】strncmp(a, b, width) == 0。空白右詰めで先頭 width バイトを序数比較。
    private static bool Eq(string value, string expected, int width) => Matches(value, expected, width);

    // 数値文字列を int へ(atoi 相当)。空/非数値は 0。
    private static int ParseNumber(string? value) => int.TryParse(value, out int n) ? n : 0;

    /// <summary>
    /// 電気パラメータ種別(paramIndex: 0=AT/1=W/2=VA/3=A1/4=A2)から負荷電流値を求める。
    /// 入力が無ければ false。【C原典】get_ep(maina, fyrt812, fky, i, j)。
    /// </summary>
    private static bool TryGetParameterCurrent(MainCircuitResult candidate, LoadCapacityEntry entry, int paramIndex, out double current)
    {
        current = 0.0;
        ElectricalParameters ep = candidate.Data.ElectricalParameterSlots[0];
        string rw = candidate.Data.ReservedWord;

        switch (paramIndex)
        {
            case 0: // AT (トリップ電流)
                if (IsZero(ep.At, "00000.000", 9))
                {
                    return false;
                }

                if (Matches(ep.At, "99999.999", 9))
                {
                    // AT がサーチ上限値のときはフレーム電流(AF)を用いる。
                    if (IsZero(ep.Af, "00000.000", 9))
                    {
                        return false;
                    }

                    current = entry.Coefficient * EquipmentParameterFormatter.Stof(ep.Af, 9);
                    return true;
                }

                current = entry.Coefficient * EquipmentParameterFormatter.Stof(ep.At, 9);
                return true;

            case 1: // W (負荷容量)
                if (IsZero(ep.W1, "0000000.00", 10))
                {
                    return false;
                }

                {
                    double fuka = EquipmentParameterFormatter.Stof(ep.W1, 10);
                    string kind = Matches(rw, "MG", 8) || Matches(rw, "MMCB", 8) || Matches(rw, "ELMB", 8) ||
                                  Matches(rw, "RMMCB", 8) || Matches(rw, "RELMB", 8)
                        ? "M "
                        : "TR";
                    return EnergizingCurrentCalculator.TryCalculate(candidate.Data, fuka, kind, out current);
                }

            case 2: // VA (負荷容量)
                if (IsZero(ep.Va, "0000000.00", 10))
                {
                    return false;
                }

                {
                    double fuka = EquipmentParameterFormatter.Stof(ep.Va, 10);
                    string kind;
                    if (Matches(rw, "MMCB", 8) || Matches(rw, "ELMB", 8) || Matches(rw, "RMMCB", 8) || Matches(rw, "RELMB", 8))
                    {
                        kind = "M ";
                    }
                    else
                    {
                        kind = candidate.Data.CircuitPhaseCount == '3' ? "M " : "H ";
                    }

                    return EnergizingCurrentCalculator.TryCalculate(candidate.Data, fuka, kind, out current);
                }

            case 3: // A1 (定格電流1)
                if (IsZero(ep.A1, "00000.000", 9))
                {
                    return false;
                }

                current = entry.Coefficient * EquipmentParameterFormatter.Stof(ep.A1, 9);
                return true;

            default: // A2 (定格電流2)
                if (IsZero(ep.A2, "00000.000", 9))
                {
                    return false;
                }

                current = entry.Coefficient * EquipmentParameterFormatter.Stof(ep.A2, 9);
                return true;
        }
    }

    // 【C原典】strncmp(field, zero, width) == 0: ゼロ整形文字列と一致(=入力無し)。
    private static bool IsZero(string value, string zero, int width) => Matches(value, zero, width);

    // 【C原典】strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
