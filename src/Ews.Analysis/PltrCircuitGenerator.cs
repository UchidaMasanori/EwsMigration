using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ＰＬＴＲ(表示灯変圧器)回路の自動生成情報を組み立てる。
/// 【C原典】toku/sekkei/src/Fyss14.c Pre_PLTR_Make(5075) / SortPLTRINF(5052) と struct PLTRINF(130)。
///
/// Fyss14_Make_UpperParm の f/r ループが、上流パラメータ生成後の主回路に対して
/// ＰＬＴＲを挿入すべき箇所を判定する(Pre_PLTR_Make)→挿入する(Mainfile_PLTR_Make)。
/// 本クラスは判定部(Pre_PLTR_Make)を移植する。判定の過程で表示灯(GL/RL/OL/BL/FL/WL)の
/// タイプ(datatype[0]=TR/DI)決定と、直前 F(ヒューズ)に連なる場合の回路電圧 005V 上書き
/// といった入力破壊的な副作用も行う(表示灯 005V の後段パスが依存する)。
/// 実際の主回路への挿入(Mainfile_PLTR_Make)は後続増分で移植する。
/// </summary>
public static class PltrCircuitGenerator
{
    /// <summary>データ追番フィールド幅(datano/kpav[3])。</summary>
    private const int FieldWidth = 3;

    /// <summary>表示灯タイプ TR(トランス)。【C原典】"TR     "(7 桁右詰め)。</summary>
    private const string LampTypeTr = "TR     ";

    /// <summary>表示灯タイプ DI(直入)。【C原典】"DI     "(7 桁右詰め)。</summary>
    private const string LampTypeDi = "DI     ";

    /// <summary>自動生成 PLTR の予約語。【C原典】"PLTR    "(本移植ではトリム済みで保持)。</summary>
    private const string PltrWord = "PLTR";

    /// <summary>
    /// ＰＬＴＲを自動生成すべき箇所を判定して一覧を返す。【C原典】Pre_PLTR_Make(Fyss14.c:5075)。
    ///
    /// 対象は予約語が表示灯(GL/RL/OL/BL/FL/WL)の要素。まずタイプ未設定の表示灯へ TR/DI を割り付け、
    /// 直前が PLTR の要素・回路要素が '3' でない・直流・タイプが TR・回路電圧 &lt;100V・盤種類が対象外を
    /// 除外する。直前が F(ヒューズ)でその F がトランス(TR)なら回路電圧を 005V に落として PLTR は付けず、
    /// 特定条件では直前 F を TR 化して 005V に落とす。残った表示灯につき、同一系列に既配置の PLTR が
    /// なければ挿入位置(datano_PLTR)を登録し、datano_PLTR 昇順で整列して返す。
    /// </summary>
    /// <param name="records">主回路レコード列(FYRT800 配列相当)。データ追番は index+1 とみなす。タイプ・回路電圧は書き換わる。【C原典】Pmaina。</param>
    /// <param name="manufacturingSpecKind">製作仕様区分。【C原典】bukken1-&gt;com.kyo.sshiykbn。先頭 2 文字 "01"/"02" を判定に使う。物件情報を引数注入する。</param>
    /// <param name="facilityGroup">地区(工場)グループ。【C原典】FyGetFacGrp(zone_cd)。1:札幌 4:水俣 は WL の LED-TR 特例の対象外。環境依存のため引数注入する。</param>
    /// <returns>ＰＬＴＲ挿入情報の一覧(datano_PLTR 昇順、無ければ空)。【C原典】*p_PLTR(件数 *i_PLTR)。戻り値 i_pltr?1:0 は Count&gt;0 と等価。</returns>
    public static IReadOnlyList<PltrInsertion> PreparePltrInsertions(
        IReadOnlyList<MainCircuitResult> records,
        string? manufacturingSpecKind = null,
        int facilityGroup = 0)
    {
        ArgumentNullException.ThrowIfNull(records);

        var plan = new List<PltrInsertion>();
        int count = records.Count;

        for (int i = 0; i < count; i++)
        {
            MainCircuitData d = records[i].Data;

            // 【C原典】予約語が表示灯(GL/RL/OL/BL/FL/WL)でなければ対象外。
            if (!IsLamp(d.ReservedWord))
            {
                continue;
            }

            MainCircuitData? prev = i > 0 ? records[i - 1].Data : null;

            // 【C原典】タイプ[0]未設定の表示灯へ TR/DI を割り付ける。
            if (IsBlankType(d.DataType[0]))
            {
                // 【C原典】改訂<29>/<30>: WL の LED タイプで直前が PLTR でなく、札幌/水俣工場以外なら TR。
                if (d.ReservedWord == "WL"
                    && (prev is null || prev.ReservedWord != "PLTR")
                    && TrimType(d.DataType[3]) == "LED"
                    && facilityGroup != 1 && facilityGroup != 4)
                {
                    d.DataType[0] = LampTypeTr;
                }

                // 【C原典】回路電圧>=100 かつ AC('A')。
                else if (string.CompareOrdinal(d.CircuitVoltage[0], "100") >= 0
                    && d.CircuitVoltageKind == 'A')
                {
                    char bn2 = d.ElectricalParameterSlots[2].Bn;

                    // 【C原典】ep[2] 盤種類が '1'/'2'/'3'/'4' なら DI。
                    if (bn2 is '1' or '2' or '3' or '4')
                    {
                        d.DataType[0] = LampTypeDi;
                    }
                    else
                    {
                        // 【C原典】直前が F でその F が TR なら DI、それ以外は TR。
                        if (prev is not null && prev.ReservedWord == "F" && TrimType(prev.DataType[0]) == "TR")
                        {
                            d.DataType[0] = LampTypeDi;
                        }
                        else
                        {
                            d.DataType[0] = LampTypeTr;
                        }
                    }

                    // 【C原典】改訂<12>: 制御盤スマートユニット使用の WL20P(径 020.0・盤種類 '5'/'6')は DI。
                    if (d.ReservedWord == "WL"
                        && d.ElectricalParameterSlots[0].Ksize == "020.0"
                        && (d.ElectricalParameterSlots[0].Bn is '5' or '6'))
                    {
                        d.DataType[0] = LampTypeDi;
                    }
                }

                // 【C原典】上記以外(回路電圧<100 または DC)は DI。
                else
                {
                    d.DataType[0] = LampTypeDi;
                }
            }

            // 【C原典】改訂<22>: グレード２対応(sshiykbn=="02")の LED ランプは回路電圧<100 なら DI。
            else if (StartsWithSpec(manufacturingSpecKind, "02"))
            {
                if (EquipmentParameterFormatter.Stoi(d.CircuitVoltage[0], FieldWidth) < 100)
                {
                    d.DataType[0] = LampTypeDi;
                }
            }

            // 【C原典】直前が PLTR なら PLTR を付けない。
            if (prev is not null && prev.ReservedWord == "PLTR")
            {
                continue;
            }

            // 【C原典】回路要素 '3'・AC・タイプが TR でない・回路電圧>=100・盤種類が対象内のみ。
            if (d.CircuitElement != '3')
            {
                continue;
            }

            if (d.CircuitVoltageKind != 'A')
            {
                continue;
            }

            if (TrimType(d.DataType[0]) == "TR")
            {
                continue;
            }

            if (string.CompareOrdinal(d.CircuitVoltage[0], "100") < 0)
            {
                continue;
            }

            char bn0 = d.ElectricalParameterSlots[0].Bn;
            if (bn0 is not ('1' or '2' or '5' or '6' or '4'))
            {
                continue;
            }

            // 【C原典】941121: 直前が F(ヒューズ)の場合の 005V 上書き処理。
            if (prev is not null && prev.ReservedWord == "F")
            {
                // 【C原典】その F が TR なら回路電圧を 005V に落として PLTR は付けない。
                if (TrimType(prev.DataType[0]) == "TR")
                {
                    SetLampVoltage005(d);
                    continue;
                }

                // 【C原典】950206/950209: 直前 F がタイプ未設定・数量 '1'・sshiykbn=="01" の特例。
                if (IsBlankType(prev.DataType[0])
                    && prev.ElectricalParameterSlots[0].Qty == '1'
                    && StartsWithSpec(manufacturingSpecKind, "01"))
                {
                    string pv = prev.CircuitVoltage[0];
                    bool inRange =
                        (string.CompareOrdinal(pv, "100") >= 0 && string.CompareOrdinal(pv, "110") <= 0)
                        || (string.CompareOrdinal(pv, "200") >= 0 && string.CompareOrdinal(pv, "220") <= 0);

                    if (inRange)
                    {
                        // 【C原典】950209: 直後が同一行種の VM ならこの表示灯は素通り(何もしない)。
                        MainCircuitData? next = i + 1 < count ? records[i + 1].Data : null;
                        bool vmSame = next is not null
                            && d.LineTypeCode == next.LineTypeCode
                            && d.LineTypeGroupNumber == next.LineTypeGroupNumber
                            && next.ReservedWord == "VM";

                        if (!vmSame)
                        {
                            prev.DataType[0] = LampTypeTr;
                            SetLampVoltage005(d);
                            continue;
                        }
                    }
                }
            }

            // 【C原典】改訂<12>: 制御盤/警報盤(盤種類 '5'/'6')は上記で通過済みなのでここで停止。
            if (bn0 is '5' or '6')
            {
                continue;
            }

            // 【C原典】主回路の並び順から回路要素 '3' の表示灯先頭を後方に探す(先頭は自分自身 = i)。
            int j = i;
            for (; j >= 0; j--)
            {
                if (!IsLamp(records[j].Data.ReservedWord))
                {
                    continue;
                }

                break;
            }

            // 【C原典】同一系列に既配置の PLTR があれば追加しない。
            int insertBefore = j + 1;
            if (plan.Exists(p => p.InsertBeforeSequenceNumber == insertBefore))
            {
                continue;
            }

            // 【C原典】PLTRINF 設定: datano_xL=i+1 / datano_PLTR=j+1。
            plan.Add(new PltrInsertion(
                CauseSequenceNumber: i + 1,
                InsertBeforeSequenceNumber: insertBefore));
        }

        // 【C原典】qsort(SortPLTRINF): datano_PLTR 昇順。
        return plan
            .OrderBy(x => x.InsertBeforeSequenceNumber)
            .ToList();
    }

    /// <summary>
    /// PLTRINF を元に主回路データブロックへ PLTR を挿入しデータ追番を再採番する。
    /// 【C原典】<c>Mainfile_PLTR_Make</c>(Fyss14.c:5311)。
    ///
    /// 旧主回路を新リストへ複写しつつ、挿入位置(datano_PLTR)の直前へ PLTR 要素を挿入し、
    /// データ追番・親データ追番(oyatno)・グループ親データ追番(goyano)を新採番へ付け替える。
    /// PLTR 要素の各フィールドは発生元(挿入位置の表示灯)から複写する(VT と異なり発生元 narakbn は
    /// 変更しない)。挿入後、自動生成 PLTR('3')の直後で同一階層・同一並列の要素の並び替え機器区分を 1 戻す。
    /// 【C原典】と同じく旧リストの要素は再利用され採番等が書き換わる。
    /// </summary>
    /// <param name="mains">旧主回路エリア。要素は再利用され採番等が書き換わる。【C原典】*Pmaina(件数 *Pmainc)。</param>
    /// <param name="plan"><see cref="PreparePltrInsertions"/> が返す PLTR 挿入情報(datano_PLTR 昇順)。【C原典】p_PLTR(件数 i_PLTR)。</param>
    /// <returns>PLTR 挿入後の新主回路エリア。【C原典】*Pmaina(件数 *Pmainc=mainc+i_PLTR)。</returns>
    public static IReadOnlyList<MainCircuitResult> InsertPltrRecords(
        IReadOnlyList<MainCircuitResult> mains, IReadOnlyList<PltrInsertion> plan)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(plan);

        var newList = new List<MainCircuitResult>(mains.Count + plan.Count);
        int j = 0;   // 処理対象 PLTRINF の位置。

        for (int i = 0; i < mains.Count; i++)
        {
            // 【C原典】現在位置(datano_PLTR の直前)に PLTR を挿入する必要がある場合。
            if (j < plan.Count && plan[j].InsertBeforeSequenceNumber == i + 1)
            {
                MainCircuitData src = mains[i].Data;   // 【C原典】maina[datano_PLTR-1](=挿入位置の表示灯)。
                var pltr = new MainCircuitResult();    // Main_Area_Clear 相当(既定初期値)。
                MainCircuitData d = pltr.Data;

                pltr.SequenceNumber = BranchArraySorter.FormatFixedWidth(newList.Count + 1, FieldWidth);
                d.SystemNumber = src.SystemNumber;
                d.SystemKind = src.SystemKind;
                d.HierarchyNumber = src.HierarchyNumber;
                d.ParallelNumber = src.ParallelNumber;
                d.AutoGenerationKind = '1';
                d.ReservedWord = PltrWord;
                d.LineTypeCode = src.LineTypeCode;
                d.LineTypeNumber = src.LineTypeNumber;
                d.LineTypeGroupNumber = src.LineTypeGroupNumber;
                d.AttachedParameter.DimensionGroupNumber = src.AttachedParameter.DimensionGroupNumber;
                d.AttachedParameter.CommentGroupNumber = src.AttachedParameter.CommentGroupNumber;
                d.AttachedParameter.MakerCode = src.AttachedParameter.MakerCode;   // 【C原典】改訂<24>。
                d.ElectricalParameterSlots[0].Bn = src.ElectricalParameterSlots[0].Bn;
                d.ElectricalParameterSlots[0].Qty = '1';
                d.IncomingNumber = src.IncomingNumber;
                d.SortKind = src.SortKind;              // 【C原典】VT と異なり発生元 narakbn は減算しない。
                d.CircuitClass = src.CircuitClass;
                d.CircuitNumberSuffix = src.CircuitNumberSuffix;
                d.CircuitElement = '3';

                newList.Add(pltr);
                j++;
            }

            // 【C原典】旧データの複写とデータ追番・親/グループ親データ追番の付け替え。
            MainCircuitResult cur = mains[i];
            cur.SequenceNumber = BranchArraySorter.FormatFixedWidth(newList.Count + 1, FieldWidth);
            int n = EquipmentParameterFormatter.Stoi(cur.Data.ParentSequenceNumber, FieldWidth);
            if (n != 0)
            {
                cur.Data.ParentSequenceNumber = mains[n - 1].SequenceNumber;
            }

            n = EquipmentParameterFormatter.Stoi(cur.Data.GroupParentSequenceNumber, FieldWidth);
            if (n != 0)
            {
                cur.Data.GroupParentSequenceNumber = mains[n - 1].SequenceNumber;
            }

            newList.Add(cur);
        }

        // 【C原典】自動生成 PLTR('3')の直後で同一階層・同一並列の要素の並び替え機器区分を 1 戻す。
        for (int i = 0; i < newList.Count; i++)
        {
            MainCircuitData e = newList[i].Data;
            if (e.ReservedWord != PltrWord || e.CircuitElement != '3')
            {
                continue;
            }

            char kiryoso = e.CircuitElement;   // '3'
            int k = EquipmentParameterFormatter.Stoi(e.HierarchyNumber, FieldWidth);
            int t = EquipmentParameterFormatter.Stoi(e.ParallelNumber, FieldWidth);
            bool endFlg = false;

            for (int m = i + 1; m < newList.Count; m++)
            {
                MainCircuitData nd = newList[m].Data;
                if (kiryoso != nd.CircuitElement)
                {
                    break;
                }

                int kn = EquipmentParameterFormatter.Stoi(nd.HierarchyNumber, FieldWidth);
                int tn = EquipmentParameterFormatter.Stoi(nd.ParallelNumber, FieldWidth);
                if (k == kn && t == tn && !endFlg)
                {
                    if (nd.SortKind is '4' or '2')
                    {
                        nd.SortKind = (char)(nd.SortKind - 1);
                    }
                }
                else if (k < kn || t < tn)
                {
                    endFlg = true;
                }
                else
                {
                    break;
                }
            }
        }

        return newList;
    }

    /// <summary>表示灯の回路電圧を 005V(単相)に落とし ep[2].V2[0] を 000005.5 にする。【C原典】941126。</summary>
    private static void SetLampVoltage005(MainCircuitData d)
    {
        d.CircuitVoltage[0] = "005";
        d.CircuitVoltage[1] = "000";
        d.CircuitVoltage[2] = "000";
        d.ElectricalParameterSlots[2].V2[0] = "000005.5";
    }

    /// <summary>予約語が表示灯(GL/RL/OL/BL/FL/WL)か。【C原典】memcmp 6 種。</summary>
    private static bool IsLamp(string? word) =>
        word is "GL" or "RL" or "OL" or "BL" or "FL" or "WL";

    /// <summary>タイプが未設定(空白)か。【C原典】!memcmp(datatype,"       ",7)。</summary>
    private static bool IsBlankType(string? type) => string.IsNullOrEmpty((type ?? string.Empty).Trim());

    /// <summary>タイプの前後空白を除いた比較用文字列。【C原典】datatype[n] の 7 桁右詰め。</summary>
    private static string TrimType(string? type) => (type ?? string.Empty).Trim();

    /// <summary>製作仕様区分の先頭 2 文字一致。【C原典】strncmp/memcmp(sshiykbn, x, 2)==0。</summary>
    private static bool StartsWithSpec(string? spec, string value)
    {
        string s = spec ?? string.Empty;
        return s.Length >= 2 && s[..2] == value;
    }
}

/// <summary>
/// ＰＬＴＲ自動生成の 1 挿入分の情報。【C原典】struct PLTRINF(Fyss14.c:130)。
/// </summary>
/// <param name="CauseSequenceNumber">発生原因となる表示灯(xL)のデータ追番(1 始まり)。【C原典】datano_xL。</param>
/// <param name="InsertBeforeSequenceNumber">ＰＬＴＲを挿入するべき要素の直前のデータ追番。【C原典】datano_PLTR。</param>
public sealed record PltrInsertion(
    int CauseSequenceNumber,
    int InsertBeforeSequenceNumber);
