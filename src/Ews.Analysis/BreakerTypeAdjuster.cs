using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 機器検索の前処理(Fysk00_Kikisearch_SY_Sub)で行われるブレーカ系の機器タイプ調整をまとめる。
/// いずれもマスタ検索(Fysk01_Kikisearch_S1)より前段の補正で、外部データに依存しない純粋処理。
/// 【C原典】
///   - <see cref="AdjustBranchMcbType"/>  : PropChgMcbType(Fysk00.c:2662, 改訂&lt;2&gt;)
///   - <see cref="AdjustParentMcbType"/>   : PropChgOyaMcbType(Fysk00.c:2766, 改訂&lt;48&gt;)
///   - <see cref="AdjustPluginType"/>      : PropChgPluginType(Fysk00.c:2937, 改訂&lt;34&gt;)
///   - <see cref="AdjustM10AfBreaker"/>    : PropChgM10AfBreaker(Fysk00.c:7938, 改訂&lt;118&gt;)
///   - <see cref="AdjustLaClass1Type"/>    : PropChgLaClass1Type(Fysk00.c:11850, 改訂&lt;143&gt;)
/// </summary>
public static class BreakerTypeAdjuster
{
    private const string Blank7 = "       ";

    /// <summary>
    /// 分岐 MCB の表示タイプを、電源が単相2線/単相3線なら協約型(KY/KM)へ変更する。
    /// 【C原典】PropChgMcbType(Fysk00.c:2662, 改訂&lt;2&gt;/&lt;3&gt;/&lt;7&gt;)。
    /// </summary>
    /// <param name="mcb">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="records">主回路レコード列(親検索用)。【C原典】f800。</param>
    /// <param name="dataTypes">主回路ファイルの機器タイプ。【C原典】dtype[][7]。</param>
    /// <param name="displayTypes">表示用出力機器タイプ(変更対象)。【C原典】wtype[][7]。</param>
    public static void AdjustBranchMcbType(MainCircuitResult mcb,
                                           IReadOnlyList<MainCircuitResult> records,
                                           string[] dataTypes, string[] displayTypes)
    {
        ArgumentNullException.ThrowIfNull(mcb);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(displayTypes);

        // 【C原典】dtype[0] が空白でなければ対象外(既にタイプ確定済み)。
        if (!Matches(dataTypes[0], Blank7, 7))
        {
            return;
        }

        // 【C原典】改訂<7> gyocd[0] が 'B'(分岐) or 'O'(オプション) かつ 予約語 MCB のみ対象。
        char lineTypeHead = FirstChar(mcb.Data.LineTypeCode);
        if ((lineTypeHead != 'B' && lineTypeHead != 'O') ||
            !Matches(mcb.Data.ReservedWord, "MCB", 3))
        {
            return;
        }

        MainCircuitResult? power = FindPowerSource(mcb, records);
        if (power is null)
        {
            return;
        }

        // 【C原典】電源が単相2線 or 単相3線のとき、制御盤(epabn=='5')以外は協約型に変更。
        MainCircuitData p = power.Data;
        if ((p.CircuitPhaseCount == '1' && p.CircuitWireType == '2') ||
            (p.CircuitPhaseCount == '1' && p.CircuitWireType == '3'))
        {
            if (p.ElectricalParameterSlots[0].Bn != '5')
            {
                displayTypes[0] = "KY     ";
                displayTypes[1] = "KM     ";
            }
        }
    }

    /// <summary>
    /// 1P3W で 3P 分岐を持つ主幹ブレーカの表示タイプを経済型(ET/KY)へ変更する。
    /// 【C原典】PropChgOyaMcbType(Fysk00.c:2766, 改訂&lt;48&gt;)。
    /// </summary>
    /// <param name="main">対象の主幹レコード。【C原典】sk。</param>
    /// <param name="records">主回路レコード列(子検索用)。【C原典】f800。</param>
    /// <param name="dataTypes">主回路ファイルの機器タイプ。【C原典】dtype[][7]。</param>
    /// <param name="displayTypes">表示用出力機器タイプ(変更対象)。【C原典】wtype[][7]。</param>
    public static void AdjustParentMcbType(MainCircuitResult main,
                                           IReadOnlyList<MainCircuitResult> records,
                                           string[] dataTypes, string[] displayTypes)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(displayTypes);

        // 【C原典】既に経済型(ET)なら対象外。
        if (Matches(displayTypes[0], "ET ", 3))
        {
            return;
        }
        // 【C原典】dtype[0] が空白でなければ対象外。
        if (!Matches(dataTypes[0], Blank7, 7))
        {
            return;
        }

        MainCircuitData d = main.Data;
        // 【C原典】1P3W でなければ対象外。
        if (d.CircuitPhaseCount != '1' || d.CircuitWireType != '3')
        {
            return;
        }
        // 【C原典】回路分類が主幹('M')でなければ対象外。
        if (d.CircuitClass != 'M')
        {
            return;
        }
        // 【C原典】ブレーカ(MCB/ELB)でなければ対象外。
        if (!Matches(d.ReservedWord, "MCB", 3) && !Matches(d.ReservedWord, "ELB", 3))
        {
            return;
        }

        // 【C原典】子(oyatno==自分の datano)を datano の位置から順に検索。
        string sequenceNumber = main.SequenceNumber;
        for (int i = EquipmentParameterFormatter.Stoi(sequenceNumber, 3); i < records.Count; i++)
        {
            if (i < 0)
            {
                continue;
            }

            MainCircuitData child = records[i].Data;
            if (Matches(child.ParentSequenceNumber, sequenceNumber, 3))
            {
                // 【C原典】子がブレーカで ep[0].epap=="003"(3極) のとき経済型へ変更。
                if (Matches(child.ReservedWord, "MCB", 3) || Matches(child.ReservedWord, "ELB", 3))
                {
                    if (Matches(child.ElectricalParameterSlots[0].P, "003", 3))
                    {
                        displayTypes[0] = "ET     ";
                        displayTypes[1] = "KY     ";
                        break;
                    }
                }
            }
            else if (string.CompareOrdinal(PadRight3(d.IncomingNumber), PadRight3(child.IncomingNumber)) > 0)
            {
                // 【C原典】次の系統機器なので終了。
                break;
            }
        }
    }

    /// <summary>
    /// プラグインブレーカ(CH/CHP)の接続相タイプ NOTHING を RN へ変更する。
    /// 【C原典】PropChgPluginType(Fysk00.c:2937, 改訂&lt;34&gt;)。
    /// </summary>
    /// <param name="breaker">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="dataTypes">表示用出力機器タイプ(変更対象)。【C原典】dtype[][7]。</param>
    public static void AdjustPluginType(MainCircuitResult breaker, string[] dataTypes)
    {
        ArgumentNullException.ThrowIfNull(breaker);
        ArgumentNullException.ThrowIfNull(dataTypes);

        MainCircuitData d = breaker.Data;
        // 【C原典】ep[0].epae=='1'(1E) かつ タイプ0 が CH/CHP のとき。
        if (FirstChar(d.ElectricalParameterSlots[0].E) == '1' &&
            (Matches(d.DataType[0], "CH  ", 4) || Matches(d.DataType[0], "CHP ", 4)))
        {
            // 【C原典】タイプ3 が NOTHING なら RN に変更。
            if (Matches(d.DataType[3], "NOTHING", 7))
            {
                dataTypes[3] = "RN     ";
            }
        }
    }

    /// <summary>
    /// 三菱/協約(M/KN) の 3P ELB について、10AF を 50AF として機器選定する。
    /// 【C原典】PropChgM10AfBreaker(Fysk00.c:7938, 改訂&lt;118&gt;)。
    /// </summary>
    /// <param name="elb">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCode0">メーカーコード選定順位の先頭。【C原典】mcod[0]。</param>
    /// <param name="sep">電気パラメータ(変更対象)。【C原典】sep[]。</param>
    public static void AdjustM10AfBreaker(MainCircuitResult elb, string makerCode0,
                                          NumericElectricalParameters[] sep)
    {
        ArgumentNullException.ThrowIfNull(elb);
        ArgumentNullException.ThrowIfNull(makerCode0);
        ArgumentNullException.ThrowIfNull(sep);

        // 【C原典】ELB のみ対象。
        if (!Matches(elb.Data.ReservedWord, "ELB     ", 8))
        {
            return;
        }
        // 【C原典】三菱(M) or 協約(KN) のみ対象。
        if (!Matches(makerCode0, "M  ", 3) && !Matches(makerCode0, "KN ", 3))
        {
            return;
        }
        // 【C原典】3P のみ対象。
        if (elb.Data.CircuitPoleCount != '3')
        {
            return;
        }
        // 【C原典】10AF のみ対象 → 50AF として選定。
        if (sep[1].Af == 10.0)
        {
            sep[1].Af = 50.0;
            sep[2].Af = 50.0;
        }
    }

    /// <summary>
    /// LA(避雷器)の CLASS1(タイプ0="1C") でタイプ2 未設定(NOTHING)なら RS を設定する。
    /// 【C原典】PropChgLaClass1Type(Fysk00.c:11850, 改訂&lt;143&gt;)。
    /// </summary>
    /// <param name="la">対象の主回路レコード。【C原典】sk。</param>
    public static void AdjustLaClass1Type(MainCircuitResult la)
    {
        ArgumentNullException.ThrowIfNull(la);

        MainCircuitData d = la.Data;
        // 【C原典】LA 以外は対象外。
        if (!Matches(d.ReservedWord, "LA ", 3))
        {
            return;
        }
        // 【C原典】タイプ0 が "1C" のとき。
        if (Matches(d.DataType[0], "1C", 2))
        {
            // 【C原典】タイプ2 が NOTHING なら RS を設定。
            if (Matches(d.DataType[2], "NOTHING", 7))
            {
                d.DataType[2] = "RS     ";
            }
        }
    }

    // 【C原典】親を辿り電源(予約語 "P  ")まで遡る(PropChgMcbType 内 while ループ)。
    private static MainCircuitResult? FindPowerSource(MainCircuitResult start,
                                                      IReadOnlyList<MainCircuitResult> records)
    {
        string parentNumber = start.Data.ParentSequenceNumber;
        // 無限ループ防止のため探索回数を主回路件数+1 で上限とする(不正データ保護)。
        for (int guard = records.Count + 1; guard > 0; guard--)
        {
            MainCircuitResult? parent = null;
            foreach (MainCircuitResult record in records)
            {
                if (Matches(record.SequenceNumber, parentNumber, 3))
                {
                    parent = record;
                    break;
                }
            }

            if (parent is null)
            {
                return null;
            }
            if (Matches(parent.Data.ReservedWord, "P  ", 3))
            {
                return parent;
            }
            parentNumber = parent.Data.ParentSequenceNumber;
        }
        return null;
    }

    private static char FirstChar(string value) => value.Length > 0 ? value[0] : ' ';

    private static string PadRight3(string value) => value.PadRight(3)[..3];

    // 【C原典】strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
