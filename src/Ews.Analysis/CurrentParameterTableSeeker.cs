using System.Globalization;
using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 電流パラメータ関連コンスタントテーブル(amp001～amp004.cns)の検索処理、
/// および負荷容量決定テーブル(FYRT812)のチェック処理。
/// 【C原典】Fyss3G_CnsPrmtpSeek / CnsSQsetSeek / CnsA2setSeek / CnsA1setSeek /
///   Check_fyrt812(toku/sekkei/src/Fyss3G.c)。
///
/// C 原典は各コンスタントを線形リスト(PRMTP_T/SQSET_T/A2SET_T/A1SET_T)として保持し、
/// 先頭から順に走査して最初に条件を満たすノードを返す。本移植は
/// <see cref="Ews.Data.Seeding.CurrentParameterTableLoader"/> が生成した
/// <c>IReadOnlyList</c>(宣言順=リスト順)を同じ順序で走査して忠実に再現する。
///
/// 【段階移植の範囲】本増分では検索/チェックの純関数のみを移植する。これらを利用する
/// 機器セッタ(Set_MC/THR/MG/WH/AM/CT/TB 等)およびディスパッチャ本体
/// Fyss3G_Denryuu_Parm_Set は後続増分で移植する。
/// </summary>
public static class CurrentParameterTableSeeker
{
    /// <summary>
    /// パラメータ設定タイプ(amp001.cns)を予約語で検索する。一致が無ければ <c>null</c>。
    /// 【C原典】Fyss3G_CnsPrmtpSeek。<c>memcmp(chk-&gt;data-&gt;yoyaku, rt800[no].dt.yoyaku, 8)==0</c>
    /// の最初のノードを返す。
    /// </summary>
    /// <param name="table">パラメータ設定タイプ一覧(リスト順)。【C原典】PRMTP_T *ptr。</param>
    /// <param name="row">対象の主回路データ。【C原典】rt800[no]。</param>
    public static ParameterSettingType? SeekParameterSettingType(
        IReadOnlyList<ParameterSettingType> table, MainCircuitResult row)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(row);

        string key = PadReservedWord(row.Data.ReservedWord);
        foreach (ParameterSettingType entry in table)
        {
            if (PadReservedWord(entry.ReservedWord) == key)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// 電線サイズ(amp002.cns)を通電電流値で検索する。該当が無ければ 0。
    /// 【C原典】Fyss3G_CnsSQsetSeek。<c>key = denryu * 1.12</c>。許容電流 &gt;= key かつ
    /// 選定フラグ==0 の最初のノードの電線サイズを返す。
    /// </summary>
    /// <param name="energizingCurrent">通電電流値。【C原典】denryu。</param>
    /// <param name="table">電線サイズ設定一覧(リスト順)。【C原典】SQSET_T *ptr。</param>
    public static double SeekWireSize(double energizingCurrent, IReadOnlyList<WireSizeSetting> table)
    {
        ArgumentNullException.ThrowIfNull(table);

        double key = energizingCurrent * 1.12;
        foreach (WireSizeSetting entry in table)
        {
            if (entry.AllowableCurrent >= key && entry.SelectionFlag == 0)
            {
                return entry.WireSize;
            }
        }

        return 0.0;
    }

    /// <summary>
    /// 定格電流２算出係数(amp003.cns)を検索する。機器選定区分が '1' でない、または
    /// 該当が無ければ 1 を返す。
    /// 【C原典】Fyss3G_CnsA2setSeek。負荷種類(2 文字)一致かつ回路電圧 &gt; 対象回路電圧、
    /// かつ相数が全相(0)または対象相数一致の最初のノードの係数を返す。
    ///
    /// 【C 原典の基点】判定に用いる機器選定区分は <c>rt800-&gt;wk.kikiskbn</c>=<c>rt800[0].wk</c>
    /// (呼び出しは <c>&amp;rt800[0], no</c>)であり、対象データ添字 <paramref name="index"/> ではなく
    /// 先頭データ(records[0])の区分を参照する。この挙動を忠実に再現する。
    /// </summary>
    /// <param name="records">主回路データ一覧。【C原典】rt800(=&amp;maina[0])。</param>
    /// <param name="index">処理対象データ添字。【C原典】no。</param>
    /// <param name="table">定格電流２設定一覧(リスト順)。【C原典】A2SET_T *ptr。</param>
    public static double SeekRatedCurrent2Coefficient(
        IReadOnlyList<MainCircuitResult> records, int index, IReadOnlyList<RatedCurrent2Setting> table)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(table);

        double coefficient = 1.0;

        // 【C原典】if( rt800->wk.kikiskbn == '1') … rt800-> は基点(records[0])の wk。
        if (records[0].Work.EquipmentSelectionKind == '1')
        {
            MainCircuitData target = records[index].Data;
            char circuitPhase = target.CircuitPhaseCount;                 // kpaph
            int circuitVoltage = ScanVoltage(target.CircuitVoltage[0]);   // kpav
            string loadKind = PadLoadKind(target.AttachedParameter.LoadKind);

            foreach (RatedCurrent2Setting entry in table)
            {
                // 【C原典】memcmp(chk->data->fpalw1, rt800[no].dt.fp.fpalw1, 2)==0
                if (PadLoadKind(entry.LoadKind) != loadKind)
                {
                    continue;
                }

                if (entry.CircuitVoltage > circuitVoltage)
                {
                    // 【C原典】chk->data->kpaph == 0 : 相数無指定(全相該当)。
                    if (entry.CircuitPhase == '\0')
                    {
                        coefficient = entry.Coefficient;
                        break;
                    }

                    // 【C原典】chk->data->kpaph == kpaph : 対象相数一致。
                    if (entry.CircuitPhase == circuitPhase)
                    {
                        coefficient = entry.Coefficient;
                        break;
                    }
                }
            }
        }

        return coefficient;
    }

    /// <summary>
    /// 定格電流１(amp004.cns)を検索する。
    /// 【C原典】Fyss3G_CnsA1setSeek。定格電流 &gt; key の最初のノードの定格電流を返す。
    /// 全ノードが key 以下(該当無し)の場合は末尾ノードの定格電流を返す。
    /// </summary>
    /// <param name="key">キーとなる電流値。【C原典】key。</param>
    /// <param name="table">定格電流１設定一覧(リスト順)。【C原典】A1SET_T *ptr。</param>
    public static double SeekRatedCurrent1(double key, IReadOnlyList<RatedCurrent1Setting> table)
    {
        ArgumentNullException.ThrowIfNull(table);

        double ret = 0.0;
        RatedCurrent1Setting? last = null;
        bool matched = false;

        foreach (RatedCurrent1Setting entry in table)
        {
            if (entry.RatedCurrent > key)
            {
                ret = entry.RatedCurrent;
                matched = true;
                break;
            }

            last = entry;
        }

        // 【C原典】if( chk == NULL ) ret = ohk->data->key;(末尾ノードの定格電流)。
        if (!matched && last is not null)
        {
            ret = last.RatedCurrent;
        }

        return ret;
    }

    /// <summary>
    /// 負荷容量決定テーブル(FYRT812)チェック処理。ブレーカ系 14 予約語のいずれかに一致し、
    /// かつ機器選定区分=='1'・負荷種類が空白 2 文字でないとき 1(スキップ)を返す。
    /// 【C原典】Fyss3G_Check_fyrt812(add by yamamoto 94.09.27 の予約語フィルタを含む)。
    /// </summary>
    /// <param name="row">対象の主回路データ。【C原典】rt800[no]。</param>
    public static int CheckLoadCapacityTable(MainCircuitResult row)
    {
        ArgumentNullException.ThrowIfNull(row);

        int result = 0;
        string reservedWord = PadReservedWord(row.Data.ReservedWord);
        bool selected = row.Work.EquipmentSelectionKind == '1';
        bool hasLoadKind = PadLoadKind(row.Data.AttachedParameter.LoadKind) != "  ";

        foreach (LoadCapacityEntry entry in LoadCapacityDecisionTable.Entries)
        {
            string entryWord = PadReservedWord(entry.ReservedWord);

            // 【C原典】ブレーカ系 14 予約語以外は continue(94.09.27 追加)。
            if (!BreakerReservedWords.Contains(entryWord))
            {
                continue;
            }

            if (entryWord == reservedWord && selected && hasLoadKind)
            {
                result = 1;
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Check_fyrt812 のブレーカ系予約語(8 バイト右詰め)。【C原典】94.09.27 追加の memcmp 対象 14 語。
    /// </summary>
    private static readonly HashSet<string> BreakerReservedWords = new(StringComparer.Ordinal)
    {
        "MCB     ", "ELB     ", "MMCB    ", "ELMB    ", "SB      ",
        "RMCB    ", "RELB    ", "RMMCB   ", "RELMB   ", "NHMB    ",
        "HPSB    ", "HSB     ", "CP      ", "CKS     ",
    };

    /// <summary>予約語を 8 バイト右詰め(空白詰め)に整える。【C原典】memcmp(…,8)。</summary>
    private static string PadReservedWord(string? value) =>
        (value ?? string.Empty).PadRight(8)[..8];

    /// <summary>負荷種類を 2 バイト右詰め(空白詰め)に整える。【C原典】memcmp(…,2)。</summary>
    private static string PadLoadKind(string? value) =>
        (value ?? string.Empty).PadRight(2)[..2];

    /// <summary>
    /// 回路電圧[0](3 文字)の先頭整数を取り出す。数字が無ければ 0。
    /// 【C原典】memcpy(work, kpav, 3); work[3]='\0'; sscanf(work, "%ld", &amp;kpav)。
    /// </summary>
    private static int ScanVoltage(string? field)
    {
        string s = (field ?? string.Empty).PadRight(3)[..3];

        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) { i++; }

        int start = i;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) { i++; }

        int digits = 0;
        while (i < s.Length && char.IsAsciiDigit(s[i])) { i++; digits++; }
        if (digits == 0) { return 0; }

        return int.Parse(s[start..i], CultureInfo.InvariantCulture);
    }
}
