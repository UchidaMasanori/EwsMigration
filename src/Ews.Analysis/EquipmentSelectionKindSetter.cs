using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 機器選定区分(kikiskbn)・始動回路区分(startkbn)をセットする。下流パラメータ生成
/// (<c>Fyss15_Make_LowerParm</c>)の第4ステップ。
///
/// 【C原典】<c>Fyss33_KikiSentei_Set</c> 一式(toku/sekkei/src/Fyss33.c)。
/// 本体＋<c>Fukahatsu_Shori1～4</c>＋<c>Get_Fukakisu(_Sub)</c>＋<c>Yo_Check</c> を集約する。
/// 下流探索(<c>Fyss35_Select_Karyu_Sub</c>)は移植済みの <see cref="DownstreamSelector"/> を再利用。
///
/// 【注意】C 原典 Shori2 の第1ループは <c>maina[..].wk.kikiskbn == '3';</c> と代入(=)ではなく
/// 比較(==)になっており副作用が無い(デッドコード)。よって当該ループと専用ヘルパ
/// <c>Fyss33_Get_Chokakino</c> は移植しない(挙動不変)。
/// </summary>
public static class EquipmentSelectionKindSetter
{
    private const double Tol = 0.001;

    /// <summary>
    /// 機器選定区分・始動回路区分をセットする(<paramref name="mains"/> を破壊的に更新)。
    /// 【C原典】Fyss33_KikiSentei_Set(Pmainc, maina)。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】maina(件数 Pmainc)。</param>
    /// <param name="majorClassResolver">
    /// 予約語→機器大分類(kikirui)の解決子。【C原典】YOYAKU_TBL(FYDF810)の kikirui。
    /// 電動機大分類は '1'。未登録・非該当は '1' 以外を返す(Shori4 の対象外)。
    /// </param>
    public static void SetEquipmentSelectionKind(
        IReadOnlyList<MainCircuitResult> mains,
        Func<string, char> majorClassResolver)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(majorClassResolver);

        // 初期化: 負荷発生元('1')かつ制御電源番号が空の主回路末端機器を選定対象('1')にする。
        int fcnt = 0;
        foreach (MainCircuitResult m in mains)
        {
            MainCircuitData d = m.Data;
            if (d.SystemKind != '1' || d.CircuitElement != '1')
            {
                continue;
            }

            if (d.LoadSourceKind == '1' && Matches(d.AttachedParameter.ControlPowerNumber, "  ", 2))
            {
                m.Work.EquipmentSelectionKind = '1';
                fcnt++;
            }
            else
            {
                m.Work.EquipmentSelectionKind = ' ';
            }

            m.Work.StartCircuitKind = ' ';
        }

        if (fcnt < 2)
        {
            // 選定対象が1件以下なら全ての主回路機器を選定対象('1')にする。
            foreach (MainCircuitResult m in mains)
            {
                if (m.Data.SystemKind == '1' && m.Data.CircuitElement == '1')
                {
                    m.Work.EquipmentSelectionKind = '1';
                }
            }
        }
        else
        {
            FukahatsuShori1(mains);
        }

        FukahatsuShori2(mains);
        FukahatsuShori3(mains);
        FukahatsuShori4(mains, majorClassResolver);
    }

    /// <summary>
    /// 未設定の主回路機器について、下流の独立した負荷発生元が複数あれば '2'、無ければ '1' を設定。
    /// 【C原典】Fyss33_Fukahatsu_Shori1。
    /// </summary>
    private static void FukahatsuShori1(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (d.SystemKind == '1' && d.CircuitElement == '1' && mains[i].Work.EquipmentSelectionKind == ' ')
            {
                mains[i].Work.EquipmentSelectionKind = HasMultipleLoadSources(mains, i + 1) ? '2' : '1';
            }
        }
    }

    /// <summary>
    /// 指定機器の下流にある独立した負荷発生元(kikiskbn=='1')が 2 件以上あるか。
    /// 互いに下流関係にあるものは重複排除する。【C原典】Fyss33_Get_Fukakisu(ret=1 が「複数」)。
    /// </summary>
    private static bool HasMultipleLoadSources(IReadOnlyList<MainCircuitResult> mains, int designationNumber)
    {
        List<int> sno = SelectDownstreamByKind(mains, designationNumber, '1');
        int ken = sno.Count;
        if (ken <= 1)
        {
            return false;
        }

        int[] tmp = sno.ToArray();
        for (int i = 0; i < ken; i++)
        {
            if (tmp[i] == -1)
            {
                continue;
            }

            int no = ToDataNumber(mains, tmp[i]);
            foreach (int sub in SelectDownstreamByKind(mains, no, '1'))
            {
                for (int n = i + 1; n < ken; n++)
                {
                    if (tmp[n] == sub)
                    {
                        tmp[n] = -1;
                        break;
                    }
                }
            }
        }

        return tmp.Count(x => x != -1) > 1;
    }

    /// <summary>
    /// 選定区分('2')の機器で、下流に選定区分('3')があれば自身を '3' にする。
    /// 【C原典】Fyss33_Fukahatsu_Shori2 の第2ループ(第1ループは C の == 誤りでデッドコード)。
    /// </summary>
    private static void FukahatsuShori2(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            if (mains[i].Work.EquipmentSelectionKind != '2')
            {
                continue;
            }

            if (SelectDownstreamByKind(mains, i + 1, '3').Count > 0)
            {
                mains[i].Work.EquipmentSelectionKind = '3';
            }
        }
    }

    /// <summary>
    /// 負荷容量のある末端機器から上流(kikiskbn=='1' が続く間)を辿り、負荷情報を伝播しつつ
    /// 始動回路区分をセットする(スターデルタ系予約語を含めば '2'、無ければ '1')。
    /// 【C原典】Fyss33_Fukahatsu_Shori3。
    /// </summary>
    private static void FukahatsuShori3(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (Math.Abs(EquipmentParameterFormatter.Stof(di.AttachedParameter.LoadCapacity, 7)) <= Tol ||
                di.TerminalKind != '1')
            {
                continue;
            }

            bool starDelta = IsStarDelta(di.ReservedWord);
            var chain = new List<int> { i };
            int kno = i;
            int j = EquipmentParameterFormatter.Stoi(di.ParentSequenceNumber, 3) - 1;

            while (j >= 0 && j < mains.Count)
            {
                MainCircuitData dj = mains[j].Data;
                if (mains[j].Work.EquipmentSelectionKind != '1')
                {
                    break;
                }

                if (IsStarDelta(dj.ReservedWord))
                {
                    starDelta = true;
                }

                chain.Add(j);

                if (Math.Abs(EquipmentParameterFormatter.Stof(dj.AttachedParameter.LoadCapacity, 7)) < Tol)
                {
                    // 負荷種類・負荷容量・負荷単位区分(fpalw1[2]+fpalw2[7]+fpalwkbn[1]=10 バイト)を伝播。
                    AttachedParameters src = mains[kno].Data.AttachedParameter;
                    AttachedParameters dst = dj.AttachedParameter;
                    dst.LoadKind = src.LoadKind;
                    dst.LoadCapacity = src.LoadCapacity;
                    dst.LoadUnitKind = src.LoadUnitKind;
                    dj.EnergizingCurrent = mains[kno].Data.EnergizingCurrent; // 改訂<1>
                }
                else
                {
                    kno = j;
                }

                int ono = EquipmentParameterFormatter.Stoi(dj.ParentSequenceNumber, 3);
                if (ono == 0)
                {
                    break;
                }

                j = ono - 1;
            }

            char startKind = starDelta ? '2' : '1';
            foreach (int idx in chain)
            {
                mains[idx].Work.StartCircuitKind = startKind;
            }
        }
    }

    /// <summary>
    /// 選定区分('2')の電動機大分類の機器について、下流に同一行種の電動機負荷発生元があれば
    /// その負荷種類(fpalw1[2])を自身へコピーする(電動機扱い)。【C原典】Fyss33_Fukahatsu_Shori4。
    /// </summary>
    private static void FukahatsuShori4(IReadOnlyList<MainCircuitResult> mains, Func<string, char> majorClassResolver)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;
            if (mains[i].Work.EquipmentSelectionKind != '2')
            {
                continue;
            }

            if (majorClassResolver(di.ReservedWord) != '1')
            {
                continue;
            }

            IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, i + 1);
            if (downstream is null)
            {
                continue;
            }

            int hit = -1;
            foreach (int datano in downstream)
            {
                if (datano < 1 || datano > mains.Count)
                {
                    continue;
                }

                MainCircuitData dj = mains[datano - 1].Data;
                if (dj.LoadSourceKind == '1' &&
                    Matches(dj.AttachedParameter.LoadKind, "M ", 2) &&
                    Matches(di.LineTypeCode, dj.LineTypeCode, 3) &&
                    Matches(di.LineTypeGroupNumber, dj.LineTypeGroupNumber, 3))
                {
                    hit = datano;
                    break;
                }
            }

            if (hit != -1)
            {
                di.AttachedParameter.LoadKind = mains[hit - 1].Data.AttachedParameter.LoadKind;
            }
        }
    }

    /// <summary>
    /// 指定機器の下流のうち、機器選定区分が <paramref name="flag"/> のデータ追番(1始まり)を返す。
    /// 【C原典】Fyss33_Get_Fukakisu_Sub(Fyss35_Select_Karyu_Sub の結果を kikiskbn で絞込)。
    /// </summary>
    private static List<int> SelectDownstreamByKind(IReadOnlyList<MainCircuitResult> mains, int designationNumber, char flag)
    {
        var result = new List<int>();
        IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, designationNumber);
        if (downstream is null)
        {
            return result;
        }

        foreach (int datano in downstream)
        {
            if (datano >= 1 && datano <= mains.Count && mains[datano - 1].Work.EquipmentSelectionKind == flag)
            {
                result.Add(datano);
            }
        }

        return result;
    }

    /// <summary>予約語がスターデルタ系(MCSD/MGSD/MCFRSD/MGFRSD)か。【C原典】Yo_Check(先頭7バイト)。</summary>
    private static bool IsStarDelta(string reservedWord) =>
        Matches(reservedWord, "MCSD", 7) || Matches(reservedWord, "MGSD", 7) ||
        Matches(reservedWord, "MCFRSD", 7) || Matches(reservedWord, "MGFRSD", 7);

    /// <summary>データ追番(1始まり)の主回路レコードの datano を数値化する。【C原典】Stoi(maina[no-1].datano)。</summary>
    private static int ToDataNumber(IReadOnlyList<MainCircuitResult> mains, int datano) =>
        datano >= 1 && datano <= mains.Count
            ? EquipmentParameterFormatter.Stoi(mains[datano - 1].SequenceNumber, 3)
            : 0;

    // 【C原典】memcmp/strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
