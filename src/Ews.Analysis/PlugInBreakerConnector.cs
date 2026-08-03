using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 特注盤対応 プラグインブレーカの結線処理。
/// 【C原典】Fyss3R.c(toku/sekkei/src/Fyss3R.c, 主回路設計処理／プラグインブレーカ)。
///
/// プラグインタイプ照合(<see cref="IsPlugInType"/>＝<c>FyHcPlugInJdgType</c>)・
/// 電源・分岐グルーピング(<see cref="GroupBySource"/>＝<c>PropGrouping</c>)・
/// 結線処理オーケストレータ(<see cref="SetConnection"/>＝<c>Fyss3R_TokuPlugIn_Kes_Set</c>)と
/// 単相/三相の結線相セット(<see cref="SetSinglePhaseConnection"/>＝<c>PropSetSouFor1sou</c> /
/// <see cref="SetThreePhaseConnection"/>＝<c>PropSetSouFor3sou</c>)を対象とする。
/// NOTHING 判定(<c>PropJdgNothing</c>, 回路記述ファイル FYDF805 依存)は未移植依存のため、
/// 自由文字に "NOTHING" があるかを返すデリゲート(引数注入)で境界化する。
/// 主幹チェック(Fyss3R_TokuPlugIn_MainChk)は後続増分で移植する。
/// </summary>
public static class PlugInBreakerConnector
{
    /// <summary>
    /// プラグインブレーカの機器タイプ照合。
    /// 【C原典】FyHcPlugInJdgType(toku/haitichg/src/common/cmnplugin.c:77)。
    /// 機器タイプ <c>datatype[0]</c> の末尾空白を除去し、有効なプラグインタイプ
    /// (ハーフサイズ CTP/CH/CHP・KC タイプ KP)のいずれかに一致すれば真を返す。
    /// C 原典は照合 OK で 0、NG で -1 を返すが、ここでは真偽で返す(真＝照合 OK)。
    /// </summary>
    /// <param name="dataType">機器タイプ配列。【C原典】datatype[7][7]。index 0 のみ参照。</param>
    /// <returns>プラグインタイプに一致すれば true。</returns>
    public static bool IsPlugInType(IReadOnlyList<string> dataType)
    {
        ArgumentNullException.ThrowIfNull(dataType);

        if (dataType.Count == 0)
        {
            return false;
        }

        // 【C原典】plg_type[] の有効エントリ(改訂<2>でスマート/CV/FL/BB 等は無効化済)。
        // 全エントリ no==0 のため datatype[0] のみを末尾空白除去して比較する。
        string type0 = (dataType[0] ?? string.Empty).TrimEnd(' ');
        return type0 is "CTP" or "CH" or "CHP" or "KP";
    }

    /// <summary>
    /// 電源・分岐固まりでプラグインブレーカをグループ分けする。
    /// 【C原典】PropGrouping(Fyss3R.c:243)。
    ///
    /// 主回路エリアを予約語 "P "(電源)区切りで走査し、プラグインタイプ(先頭文字
    /// 'K':アダプタ / それ以外:'C' ハーフサイズ)が連続する範囲を 1 グループとする。
    /// 改訂&lt;2&gt;: プラグインブレーカを含むグループでのみ新グループ境界を進める
    /// (<paramref name="records"/> の "P " 境界で直前グループにプラグインが存在した
    /// 場合のみ次グループを開始)。呼出元は返却リストの先頭 <c>GroupCount</c> 件のみ参照する。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】maina(件数 mainc)。</param>
    /// <returns>
    /// 構築したグループ列(<c>Groups</c>)とプラグインを含む有効グループ数(<c>GroupCount</c>)。
    /// 【C原典】*grp / *grp_cnt。呼出元は先頭 <c>GroupCount</c> 件を処理する。
    /// </returns>
    public static (IReadOnlyList<PlugInGroup> Groups, int GroupCount) GroupBySource(
        IReadOnlyList<MainCircuitResult> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        // 【C原典】grp = calloc(1) … 初期スロット 1 個(全ゼロ)。j は現グループ index。
        var grp = new List<PlugInGroup> { new() };
        int j = -1;
        int sousen = 0;
        bool plugInInGroup = false; // 【C原典】p_ari。現グループにプラグインが存在したか。
        int groupCount = 0;

        for (int i = 0; i < records.Count; i++)
        {
            MainCircuitData dt = records[i].Data;

            // 【C原典】予約語 "P "(電源)。相線を確定し、改訂<2>のグループ境界処理。
            if (IsPowerSource(dt.ReservedWord))
            {
                sousen = ((dt.CircuitPhaseCount - '0') * 10) + (dt.CircuitWireType - '0');

                if (j == -1)
                {
                    j = 0;
                }
                else if (plugInInGroup)
                {
                    // 改訂<2>: プラグインが存在したグループの後のみ新スロットを起こす。
                    j++;
                    grp.Add(new PlugInGroup());
                    plugInInGroup = false;
                }

                continue;
            }

            // 【C原典】プラグインタイプでなければスキップ。
            if (!IsPlugInType(dt.DataType))
            {
                continue;
            }

            // 【C原典 移植境界注意】"P " が先行せず j==-1 のまま(=データ不整合)は C では
            // grp[-1] への書込(未定義動作)。実データは電源先行が前提のため、ここでは
            // グループ未確立としてスキップする。
            if (j < 0)
            {
                continue;
            }

            plugInInGroup = true; // 【C原典】p_ari = 1。

            char firstChar = FirstChar(dt.DataType[0]);

            // 【C原典】st_idx==0 を「未設定」の番兵として扱う(原典の癖を忠実再現)。
            if (grp[j].StartIndex == 0)
            {
                grp[j].SourcePhaseWire = sousen;
                grp[j].Type = firstChar == 'K' ? 'K' : 'C';
                grp[j].StartIndex = i;
                grp[j].EndIndex = i;
                groupCount++;
            }
            else if (grp[j].Type == firstChar)
            {
                grp[j].EndIndex = i;
            }
            else
            {
                // 【C原典】タイプが変われば新グループ。
                j++;
                grp.Add(new PlugInGroup
                {
                    SourcePhaseWire = sousen,
                    StartIndex = i,
                    EndIndex = i,
                    Type = firstChar == 'K' ? 'K' : 'C',
                });
                groupCount++;
            }
        }

        return (grp, groupCount);
    }

    /// <summary>
    /// プラグインブレーカの結線相・接続相パラメータをセットする(結線処理本体)。
    /// 【C原典】Fyss3R_TokuPlugIn_Kes_Set(Fyss3R.c:405)。
    ///
    /// <see cref="GroupBySource"/> で電源・分岐固まりにグループ分けし、電源相線が
    /// 13(単相3線)なら <see cref="SetSinglePhaseConnection"/>、33(三相3線)なら
    /// <see cref="SetThreePhaseConnection"/> を呼び、対象範囲の <paramref name="records"/> の
    /// 使用相(siyouso)・回路電圧(kpav[0])・機器タイプ(datatype[3])をその場で更新する。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】maina(件数 mainc)。その場で更新される。</param>
    /// <param name="hasNothingInFreeText">
    /// 自由文字(回路記述ファイル FYDF805)に "NOTHING" があるかを返すデリゲート。
    /// 【C原典】PropJdgNothing(FYDF805 依存)の移植境界。true＝指定有(戻り値 1 相当)。
    /// </param>
    public static void SetConnection(
        IReadOnlyList<MainCircuitResult> records,
        Func<MainCircuitResult, bool> hasNothingInFreeText)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(hasNothingInFreeText);

        (IReadOnlyList<PlugInGroup> groups, int groupCount) = GroupBySource(records);

        for (int i = 0; i < groupCount; i++)
        {
            PlugInGroup group = groups[i];
            if (group.SourcePhaseWire == 13)
            {
                SetSinglePhaseConnection(group, records, hasNothingInFreeText);
            }
            else if (group.SourcePhaseWire == 33)
            {
                SetThreePhaseConnection(group, records, hasNothingInFreeText);
            }
        }
    }

    /// <summary>
    /// 単相の結線相・接続相パラメータセット。
    /// 【C原典】PropSetSouFor1sou(Fyss3R.c:349)。
    ///
    /// グループ範囲(<see cref="PlugInGroup.StartIndex"/>～<see cref="PlugInGroup.EndIndex"/>)を走査し、
    /// CT付き(CV)ブレーカの電圧・NOTHING 指定有無に応じて回路電圧 kpav[0](210:RT相結線/
    /// 105:RN・TN相結線)を決め、接続相タイプ未入力機器には XN/YN を交互に、
    /// 接続相タイプ入力済み(RN/TN)機器には対応する使用相をセットする。
    /// </summary>
    private static void SetSinglePhaseConnection(
        PlugInGroup group,
        IReadOnlyList<MainCircuitResult> records,
        Func<MainCircuitResult, bool> hasNothingInFreeText)
    {
        // 【C原典】ptype[2][8]={"RN     ","TN     "}, sou[2][5]={"XN  ","YN  "}。
        string[] ptype = ["RN     ", "TN     "];
        string[] sou = ["XN  ", "YN  "];
        int lr = 0; // 0:XN, 1:YN

        for (int i = group.StartIndex; i <= group.EndIndex; i++)
        {
            MainCircuitData dt = records[i].Data;

            // 改訂<3>: CTP はスキップ。
            if (FieldStarts(dt.DataType[0], "CTP"))
            {
                continue;
            }

            // 【C原典】プラグインタイプでなければスキップ。
            if (!IsPlugInType(dt.DataType))
            {
                continue;
            }

            int nothing = 0; // 0:未判定, 1:自由文字指定有, 2:指定無
            if (FieldStarts(dt.DataType[3], "NOTHING"))
            {
                nothing = hasNothingInFreeText(records[i]) ? 1 : 2;
            }

            // 【C原典】MCB2P2E+(CV) CT付きブレーカの電圧セット。
            if (FieldStarts(dt.DataType[1], "CV "))
            {
                dt.CircuitVoltage[0] = nothing == 1 ? "210" : "105";
            }
            else if (nothing == 1)
            {
                dt.CircuitVoltage[0] = "210"; // RT相結線
            }

            if (FieldStarts(dt.DataType[3], "NOTHING"))
            {
                if (dt.CircuitVoltage[0] == "105")
                {
                    // 接続相のタイプ入力なしの機器。
                    dt.DataType[3] = ptype[lr];
                    dt.UsedPhase = sou[lr];
                }
                else if (dt.CircuitVoltage[0] == "210")
                {
                    dt.UsedPhase = "XY  ";
                }
            }
            else if (dt.CircuitVoltage[0] == "105")
            {
                if (FieldStarts(dt.DataType[3], "RN "))
                {
                    lr = 0;
                    dt.UsedPhase = sou[lr];
                }
                else if (FieldStarts(dt.DataType[3], "TN "))
                {
                    lr = 1;
                    dt.UsedPhase = sou[lr];
                }
            }

            lr = lr == 0 ? 1 : 0; // X->Y, Y->X
        }
    }

    /// <summary>
    /// 三相の結線相・接続相パラメータセット。
    /// 【C原典】PropSetSouFor3sou(Fyss3R.c:471)。
    ///
    /// グループ範囲を走査し、回路電圧 kpav[0] が "210" かつ極数 epap が "003" でない機器のみ処理する。
    /// アラームなし CHP タイプ・接続相タイプ(RN/TN)・NOTHING 指定有無に応じて使用相
    /// (RS/ST/RT)を順に割り当てる。NOTHING 指定無の機器には機器タイプ(datatype[3])も
    /// RN/TN/NOTHING を順にセットする。
    /// </summary>
    private static void SetThreePhaseConnection(
        PlugInGroup group,
        IReadOnlyList<MainCircuitResult> records,
        Func<MainCircuitResult, bool> hasNothingInFreeText)
    {
        // 【C原典】sou[3][5]={"RS  ","ST  ","RT  "}, ptype[3][8]={"RN     ","TN     ","NOTHING"}。
        string[] sou = ["RS  ", "ST  ", "RT  "];
        string[] ptype = ["RN     ", "TN     ", "NOTHING"];
        int idx = 0;

        for (int i = group.StartIndex; i <= group.EndIndex; i++)
        {
            MainCircuitData dt = records[i].Data;

            // 【C原典】kpav[0]!="210" または epap=="003" ならスキップ。
            if (dt.CircuitVoltage[0] != "210" ||
                records[i].Data.ElectricalParameterSlots[0].P == "003")
            {
                continue;
            }

            // 改訂<3>: CTP はスキップ。
            if (FieldStarts(dt.DataType[0], "CTP"))
            {
                continue;
            }

            // 【C原典】プラグインタイプでなければスキップ。
            if (!IsPlugInType(dt.DataType))
            {
                continue;
            }

            int nothing = 0;
            if (FieldStarts(dt.DataType[3], "NOTHING"))
            {
                nothing = hasNothingInFreeText(records[i]) ? 1 : 2;
            }

            if (nothing == 2 &&
                FieldStarts(dt.DataType[0], "CHP ") &&
                FieldStarts(dt.DataType[2], "NOTHING"))
            {
                // 電源3相で、アラームなしCHPタイプの機器。
                dt.UsedPhase = "RT  ";
                idx = 0;
            }
            else if (FieldStarts(dt.DataType[3], "RN"))
            {
                dt.UsedPhase = "RS  ";
                idx = 0;
            }
            else if (FieldStarts(dt.DataType[3], "TN"))
            {
                dt.UsedPhase = "ST  ";
                idx = 0;
            }
            else if (nothing == 1)
            {
                dt.UsedPhase = "RT  ";
                idx = 0;
            }
            else if (nothing == 2)
            {
                // 順に３相の結線相・接続相パラメータをセット。
                dt.UsedPhase = sou[idx];
                dt.DataType[3] = ptype[idx];

                idx++;
                if (idx >= 3)
                {
                    idx = 0;
                }
            }
        }
    }

    /// <summary>予約語が電源("P ")かを判定する。【C原典】memcmp(yoyaku,"P ",2)==0。</summary>
    private static bool IsPowerSource(string? reservedWord) =>
        (reservedWord ?? string.Empty).PadRight(2)[..2] == "P ";

    /// <summary>機器タイプ先頭文字を得る。【C原典】datatype[0][0]。空文字は '\0'。</summary>
    private static char FirstChar(string? value) =>
        string.IsNullOrEmpty(value) ? '\0' : value[0];

    /// <summary>
    /// 固定長フィールドの先頭 <paramref name="prefix"/> 長分が一致するか。
    /// 【C原典】strncmp/memcmp(field, prefix, len)==0。field は空白詰め固定長とみなす。
    /// </summary>
    private static bool FieldStarts(string? value, string prefix) =>
        (value ?? string.Empty).PadRight(prefix.Length)[..prefix.Length] == prefix;
}
