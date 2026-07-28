using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 定格値チェック。【C原典】<c>Fysk02_Check_Teikakuchi</c>(toku/sekkei/src/Fysk02.c:125)および
/// その下請け <c>Fysk02_Check_Teichi_ALL</c>(:214) / <c>Fysk02_Check_Teichi_Part</c>(:1195) /
/// <c>GLE_Check</c>(:1153)。
///
/// 機器選定の候補(直近上下位参照ファイル FYDF812)ごとに、電気パラメータの値と
/// 候補側定格値(kteichi 50 バイト)・共用情報を予約語別の展開情報テーブルで突き合わせ、
/// 採否(OK/NG)を判定する。<c>Fysk01_Chokkin_Read_Check_ALL/_TMS/_MTG</c> から呼ばれる。
///
/// 本移植は通常予約語(<c>tbl.flag == 0</c>)の経路 <c>Check_Teichi_ALL</c> を先行整備する。
/// 特殊予約語(SC/WH/VM/AM/TR/CR/TM/TS/BZ/BEL/MV/KPRY/THSW、flag 1～13)は今後の増分で追加する。
/// </summary>
public static class RatingValueChecker
{
    /// <summary>チェック OK。【C原典】GOOD == 0(fyrt808.h:31)。</summary>
    public const int Good = 0;

    /// <summary>チェック NG。【C原典】NOGOOD == 1(fyrt808.h:32)。</summary>
    public const int NoGood = 1;

    /// <summary>再試行(ma 入力なし・1 回目以外の不一致)。【C原典】REPEAT == 2(fyrt808.h:33)。</summary>
    public const int Repeat = 2;

    /// <summary>システムエラー(比較記号不正)。【C原典】SYS_ERR == -1(fyrt808.h:35)。</summary>
    public const int SystemError = -1;

    // 【C原典】比較種別 E=1 GE=2 LE=3(fyrt808.h:27-29)。
    private const short Equal = 1;
    private const short GreaterEqual = 2;
    private const short LessEqual = 3;

    /// <summary>数値一致許容誤差。【C原典】TOL == 0.001(fyrt808.h:25)。</summary>
    private const double Tolerance = 0.001;

    /// <summary>ma 処理で参照する電気パラメータ側項番(epama[0..2]/epama[3])。【C原典】no1[4]={16,17,18,53}。</summary>
    private static readonly short[] MaOwnItems = { 16, 17, 18, 53 };

    /// <summary>ma 処理で参照する共用情報側項番(km_s.kyomad[0..3])。【C原典】no2[4]={63,64,65,85}。</summary>
    private static readonly short[] MaSharedItems = { 63, 64, 65, 85 };

    /// <summary>
    /// 定格値チェックの入口(予約語種別で下請けへ分岐)。【C原典】<c>Fysk02_Check_Teikakuchi</c>(Fysk02.c:125)。
    /// 本移植は通常チェック(<paramref name="flag"/> == 0)のみ対応する。
    /// </summary>
    /// <param name="flag">チェック種別。【C原典】tbl.flag。0=通常。1～13=特殊予約語(未対応)。</param>
    /// <param name="table">予約語別の定格値展開情報。【C原典】tbl.tchi_t[]。</param>
    /// <param name="parameters">数値化済み電気パラメータ。【C原典】struct eparmg_s *sep。</param>
    /// <param name="inputFlags">入力有無チェック。【C原典】CHAR sfg[](index 0=有効フラグ, index=項番)。</param>
    /// <param name="ratingKeyPart">候補側の定格値部(50 バイト)。【C原典】CHAR tc[]。</param>
    /// <param name="sharedInfo">候補側の共用情報。【C原典】struct kyoyojg_s scd。</param>
    /// <param name="loopTimes">ループ回数(ma 判定用)。【C原典】SHORT times。</param>
    /// <param name="comparison">比較用グローバル値の受け皿。【C原典】CMP_1/CMP_2/CMP_3。</param>
    /// <returns>OK(0)/NG(1)/REPEAT(2)/SYS_ERR(-1)。</returns>
    public static int Check(
        short flag,
        RatingKeyTableEntry[] table,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        string ratingKeyPart,
        NumericSharedInfo sharedInfo,
        int loopTimes,
        RatingComparisonState comparison)
    {
        if (flag == 0)
        {
            return CheckAll(table, parameters, inputFlags, ratingKeyPart, sharedInfo, loopTimes, comparison);
        }

        throw new NotSupportedException(
            $"特殊予約語の定格値チェック(flag={flag})は未移植です。今後の増分で追加します。" +
            "(1=SC 2=WH 3=VM 4=AM 5=TR 6=CR 7=TM 8=TS 9=BZ 10=BEL 11=MV 12=KPRY 13=THSW)");
    }

    /// <summary>
    /// 通常予約語の定格値チェック。【C原典】<c>Fysk02_Check_Teichi_ALL</c>(Fysk02.c:214)。
    /// テーブルを先頭から走査し、各項目を <see cref="CheckPart"/> で判定する。
    /// 定格値部の読み取り位置 kk は各項目の幅(len)ぶん進める。1 項目でも GOOD 以外なら打ち切る。
    /// </summary>
    public static int CheckAll(
        RatingKeyTableEntry[] table,
        NumericElectricalParameters parameters,
        IReadOnlyList<int> inputFlags,
        string ratingKeyPart,
        NumericSharedInfo sharedInfo,
        int loopTimes,
        RatingComparisonState comparison)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(inputFlags);
        ArgumentNullException.ThrowIfNull(sharedInfo);
        ArgumentNullException.ThrowIfNull(comparison);
        ratingKeyPart ??= string.Empty;

        int kk = 0;              // 【C原典】kk(定格値部の読み取り位置)
        int ret = Good;

        foreach (RatingKeyTableEntry entry in table)
        {
            // 【C原典】if(tbl.tchi_t[j].len == -1) break;
            if (entry.IsEnd)
            {
                break;
            }

            // 【C原典】ch = (sfg[0]==1) ? sfg[tbl.tchi_t[j].kouno] : 0;
            int checkFlag = Flag(inputFlags, 0) == 1 ? Flag(inputFlags, entry.ItemNo) : 0;

            ret = CheckPart(entry, checkFlag, parameters, ratingKeyPart, kk, sharedInfo, inputFlags, loopTimes, comparison);
            if (ret != Good)
            {
                break;
            }

            // 【C原典】kk = kk + tbl.tchi_t[j].len;
            kk += entry.Width;
        }

        return ret;
    }

    /// <summary>
    /// 各項目ごとの定格値チェック。【C原典】<c>Fysk02_Check_Teichi_Part</c>(Fysk02.c:1195)。
    /// 候補側定格値(または別項番の取得値)を直近上下位データ値 aac として求め、電気パラメータ側の
    /// 値 aak と比較する。格納区分(kakunou)に応じて比較用グローバル値へ退避する。
    /// 項番 16(ma)は感度電流の集合一致判定を追加で行う。
    /// </summary>
    /// <param name="entry">定格値展開情報の 1 項目。【C原典】TCHI_T t。</param>
    /// <param name="checkFlag">入力有無フラグ。【C原典】SHORT chkflg。</param>
    /// <param name="parameters">数値化済み電気パラメータ。【C原典】struct eparmg_s *sep。</param>
    /// <param name="ratingKeyPart">候補側定格値部(全体)。【C原典】CHAR tc[]。</param>
    /// <param name="offset">定格値部の読み取り開始位置。【C原典】&amp;tc[kk] のオフセット kk。</param>
    /// <param name="sharedInfo">候補側共用情報。【C原典】struct kyoyojg_s scd。</param>
    /// <param name="inputFlags">入力有無チェック。【C原典】CHAR sfg[]。</param>
    /// <param name="loopTimes">ループ回数。【C原典】SHORT times。</param>
    /// <param name="comparison">比較用グローバル値の受け皿。【C原典】CMP_1/CMP_2/CMP_3。</param>
    public static int CheckPart(
        RatingKeyTableEntry entry,
        int checkFlag,
        NumericElectricalParameters parameters,
        string ratingKeyPart,
        int offset,
        NumericSharedInfo sharedInfo,
        IReadOnlyList<int> inputFlags,
        int loopTimes,
        RatingComparisonState comparison)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(sharedInfo);
        ArgumentNullException.ThrowIfNull(inputFlags);
        ArgumentNullException.ThrowIfNull(comparison);
        ratingKeyPart ??= string.Empty;

        double aac = 0.0;   // 直近上下位データ
        double p;
        int ret = Good;

        // 【C原典】if(t.len != 0 || t.d_len != 0) …
        if (entry.Width != 0 || entry.DecimalScale != 0)
        {
            if (entry.Width == 0)
            {
                // 【C原典】len==0 のとき d_len を項番として別データを取得する。
                aac = RatingKeyBuilder.GetDataValue(entry.DecimalScale, parameters, sharedInfo).Numeric;
            }
            else
            {
                // 【C原典】aac = Stof(str, t.len) / Ketaawase(t.d_len);
                aac = EquipmentParameterFormatter.Stof(Slice(ratingKeyPart, offset), entry.Width)
                    / NumericConverter.PowerOfTen(entry.DecimalScale);
            }

            // 【C原典】switch(t.fromto){ case1:p=scd.vcfrom; case2:p=scd.vcto; default:p=1.0; }
            p = entry.RangeSide switch
            {
                1 => sharedInfo.ControlVoltageRangeFrom,
                2 => sharedInfo.ControlVoltageRangeTo,
                _ => 1.0,
            };
            aac *= p;
        }

        // 【C原典】ifc = Fysk00_Get_Datachi(t.kouno, sep, scd); aak = ifc.su.fsu;
        double aak = RatingKeyBuilder.GetDataValue(entry.ItemNo, parameters, sharedInfo).Numeric;

        // 【C原典】格納区分(kakunou)に応じて比較用グローバル値へ退避。
        if (entry.StorageKind == 1 || entry.StorageKind == 2)
        {
            comparison.AmpereTripPair[entry.StorageKind - 1] = aac;
        }
        else if (entry.StorageKind == 3)
        {
            comparison.AmpereTripSecond = aac;
        }
        else if (entry.StorageKind == 4)
        {
            comparison.Voltage = aac;
        }

        // 【C原典】if(t.check != 0 && chkflg != 2) … chkflg==2 は TM,THSW 時のみ。
        if (entry.Comparison != 0 && checkFlag != 2)
        {
            short gateFlag;
            if (checkFlag == 1 && entry.SelectFlag != -3 && entry.SelectFlag != -1)
            {
                gateFlag = Equal;
            }
            else
            {
                gateFlag = entry.Comparison;
            }

            ret = GateCheck(gateFlag, aac, aak);
        }

        if (ret != Good)
        {
            return NoGood;
        }

        // 【C原典】if(t.kouno == 16) … ma 処理(感度電流の集合一致判定)。
        if (entry.ItemNo == 16)
        {
            ret = CheckSensitivityCurrents(parameters, sharedInfo, inputFlags, loopTimes);
        }

        return ret;
    }

    /// <summary>
    /// 比較記号による判定。【C原典】<c>GLE_Check</c>(Fysk02.c:1153)。
    /// </summary>
    /// <param name="gateFlag">比較記号。1=一致(E) 2=以上(GE) 3=以下(LE)。</param>
    /// <param name="candidate">直近上下位データ値(aac)。</param>
    /// <param name="key">電気パラメータ側の値(aak)。</param>
    private static int GateCheck(short gateFlag, double candidate, double key)
    {
        return gateFlag switch
        {
            Equal => Math.Abs(candidate - key) > Tolerance ? NoGood : Good,
            GreaterEqual => candidate < key ? NoGood : Good,
            LessEqual => candidate > key ? NoGood : Good,
            _ => SystemError,
        };
    }

    /// <summary>
    /// 項番 16(ma)の感度電流集合一致判定。【C原典】<c>Fysk02_Check_Teichi_Part</c> 内 ma 処理ブロック。
    /// 電気パラメータ側の感度電流(epama[0..3]=項番 16/17/18/53)が、候補側の感度電流
    /// (km_s.kyomad[0..3]=項番 63/64/65/85)のいずれかと一致するかを調べる。
    /// </summary>
    private static int CheckSensitivityCurrents(
        NumericElectricalParameters parameters,
        NumericSharedInfo sharedInfo,
        IReadOnlyList<int> inputFlags,
        int loopTimes)
    {
        int ret = Good;
        int maFlag = Flag(inputFlags, 16);

        // 【C原典】if((sfg[16]==1) || (sfg[16]!=1 && times==1))
        if (maFlag == 1 || (maFlag != 1 && loopTimes == 1))
        {
            Span<short> usedBox = stackalloc short[10];
            int usedCount = 0;

            for (int i = 0; i < 4; i++)
            {
                double aak = RatingKeyBuilder.GetDataValue(MaOwnItems[i], parameters, sharedInfo).Numeric;
                int hit = 0;

                for (int j = 0; j < 4; j++)
                {
                    // 【C原典】既に一致に使った枠(iCBox)はスキップ。
                    bool same = false;
                    for (int k = 0; k < usedCount; k++)
                    {
                        if (usedBox[k] == MaSharedItems[j])
                        {
                            same = true;
                            break;
                        }
                    }

                    if (same)
                    {
                        continue;
                    }

                    double aac = RatingKeyBuilder.GetDataValue(MaSharedItems[j], parameters, sharedInfo).Numeric;
                    if (Math.Abs(aak - aac) < Tolerance)
                    {
                        hit = 1;
                        usedBox[usedCount++] = MaSharedItems[j];
                        break;
                    }
                }

                if (hit == 0)
                {
                    // 【C原典】sfg[16]==1 なら NOGOOD、それ以外は REPEAT。
                    ret = maFlag == 1 ? NoGood : Repeat;
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                double aak = RatingKeyBuilder.GetDataValue(MaOwnItems[i], parameters, sharedInfo).Numeric;
                if (aak < Tolerance)
                {
                    break;
                }

                int j;
                for (j = 0; j < 4; j++)
                {
                    double aac = RatingKeyBuilder.GetDataValue(MaSharedItems[j], parameters, sharedInfo).Numeric;
                    if (Math.Abs(aak - aac) < Tolerance)
                    {
                        break;
                    }
                }

                if (j == 4)
                {
                    ret = NoGood;
                    break;
                }
            }
        }

        return ret;
    }

    /// <summary>入力有無チェック配列から範囲内の要素を取り出す(範囲外は 0)。【C原典】CHAR sfg[]。</summary>
    private static int Flag(IReadOnlyList<int> inputFlags, int index)
        => index >= 0 && index < inputFlags.Count ? inputFlags[index] : 0;

    /// <summary>定格値部の指定位置以降を取り出す(範囲外は空文字)。【C原典】&amp;tc[kk]。</summary>
    private static string Slice(string source, int offset)
        => offset >= 0 && offset < source.Length ? source[offset..] : string.Empty;
}
