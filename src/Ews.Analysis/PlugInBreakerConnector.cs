using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 特注盤対応 プラグインブレーカの結線処理。
/// 【C原典】Fyss3R.c(toku/sekkei/src/Fyss3R.c, 主回路設計処理／プラグインブレーカ)。
///
/// 本移植は基盤部として、プラグインタイプ照合(<see cref="IsPlugInType"/>＝
/// <c>FyHcPlugInJdgType</c>)と電源・分岐グルーピング(<see cref="GroupBySource"/>＝
/// <c>PropGrouping</c>)を対象とする。単相/三相の結線相セット(PropSetSouFor1sou/3sou)・
/// 主幹チェック(Fyss3R_TokuPlugIn_MainChk)・NOTHING 判定(PropJdgNothing, 回路記述
/// ファイル FYDF805 依存)は後続増分で移植する。
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

    /// <summary>予約語が電源("P ")かを判定する。【C原典】memcmp(yoyaku,"P ",2)==0。</summary>
    private static bool IsPowerSource(string? reservedWord) =>
        (reservedWord ?? string.Empty).PadRight(2)[..2] == "P ";

    /// <summary>機器タイプ先頭文字を得る。【C原典】datatype[0][0]。空文字は '\0'。</summary>
    private static char FirstChar(string? value) =>
        string.IsNullOrEmpty(value) ? '\0' : value[0];
}
