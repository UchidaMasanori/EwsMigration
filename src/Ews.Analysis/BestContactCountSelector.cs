using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// 直近上下位該当データ群から、使用接点数(ａ／ｂ／ｃ)に対して最適な接点数を持つ
/// レコードの件数番目(0 始まり)を返す。接点数を key に持つ予約語
/// (MC/MG/MCFR/MGFR/CR/TM/TS/KPRY)のみが対象で、接点数以外のパラメータは変更しない。
///
/// 【C原典】<c>Fysc29_Best_Cont_Count</c>(toku/seigyo/src/Fysc29.c:87)。
///   - 引数: kensu(件数), tyok(FYDF812 先頭), a_con/b_con/c_con(使用ａ／ｂ／ｃ接点数)。
///   - 戻り: 該当番目データ(0 始まり) / &lt; 0 は NG。
///
/// 接点数は候補レコードの定格値キー(<see cref="NearestRankReference.RatingKey"/> = key.kteichi[50])を
/// 予約語別の <c>union FYRT702</c> レイアウトで位置参照して取り出す。予約語の判定は
/// 先頭レコードの <see cref="NearestRankReference.ReservedWord"/> による前方一致(先出し優先)。
/// </summary>
public static class BestContactCountSelector
{
    /// <summary>
    /// 予約語判定テーブル。【C原典】<c>static CHAR yo_tbl[][8]</c>。
    /// 先頭から前方一致で最初に一致したものを採用するため、"MCFR"/"MGFR" は
    /// 先に並ぶ "MC"/"MG" に一致する(原典の挙動をそのまま再現)。
    /// </summary>
    private static readonly string[] ReservedWordTable =
        { "MC", "MG", "MCFR", "MGFR", "CR", "TM", "TS", "KPRY" };

    /// <summary>
    /// 最適接点数レコードの件数番目(0 始まり)を返す。該当なし/対象外予約語は -1。
    /// </summary>
    /// <param name="candidates">直近上下位該当データ群(【C原典】tyok, FYDF812 配列)。</param>
    /// <param name="usedA">使用ａ接点数(【C原典】a_con)。</param>
    /// <param name="usedB">使用ｂ接点数(【C原典】b_con)。</param>
    /// <param name="usedC">使用ｃ接点数(【C原典】c_con)。</param>
    public static int Select(IReadOnlyList<NearestRankReference> candidates, int usedA, int usedB, int usedC)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        int count = candidates.Count;

        // 予約語(先頭レコード)を前方一致で判定。該当なしは -1。
        string yo = count > 0 ? candidates[0].ReservedWord ?? string.Empty : string.Empty;
        int no = -1;
        for (int i = 0; i < ReservedWordTable.Length; i++)
        {
            if (yo.StartsWith(ReservedWordTable[i], StringComparison.Ordinal))
            {
                no = i;
                break;
            }
        }

        if (no == -1)
        {
            return -1;
        }

        if (count == 1)
        {
            return 0;
        }

        // flg_a/b/c/t は「-1 で memset された添字テーブル」を忠実に再現する。
        int[] flgA = CreateFilled(count);
        int[] flgB = CreateFilled(count);
        int[] flgC = CreateFilled(count);
        int[] flgT = CreateFilled(count);
        int[] seA = new int[count];
        int[] seB = new int[count];
        int[] seC = new int[count];
        int ia = 0, ib = 0, ic = 0, it = 0;
        bool noContact = false; // settennasi

        for (int i = 0; i < count; i++)
        {
            string k = (candidates[i].RatingKey ?? string.Empty).PadRight(50);
            int a;
            int b;
            int c = -1;

            switch (no)
            {
                case 0: // MC   : ac@13, bc@14 (1 桁)
                    a = k[13] - '0';
                    b = k[14] - '0';
                    break;
                case 1: // MG   : ac@21, bc@22 (1 桁)
                    a = k[21] - '0';
                    b = k[22] - '0';
                    break;
                case 2: // MCFR : ac@14, bc@15 (1 桁)
                    a = k[14] - '0';
                    b = k[15] - '0';
                    break;
                case 3: // MGFR : ac@15, bc@16 (1 桁)
                    a = k[15] - '0';
                    b = k[16] - '0';
                    break;
                case 4: // CR   : ac@13, bc@15, cc@17 (2 桁, Stoi)
                    a = Stoi(k, 13, 2);
                    b = Stoi(k, 15, 2);
                    c = Stoi(k, 17, 2);
                    break;
                case 5: // TM   : ac@37, bc@39, cc@41 (2 桁, Stoi)
                    a = Stoi(k, 37, 2);
                    b = Stoi(k, 39, 2);
                    c = Stoi(k, 41, 2);
                    break;
                case 6: // TS   : ac@13, bc@15, cc@17 (2 桁, Stoi)
                    a = Stoi(k, 13, 2);
                    b = Stoi(k, 15, 2);
                    c = Stoi(k, 17, 2);
                    // 94.11.02 add: 使用接点が 1 接点以下のとき機器マスタに 1c があればそれを優先。
                    if ((usedA == 1 && usedB == 0 && usedC == 0) ||
                        (usedA == 0 && usedB == 1 && usedC == 0) ||
                        (usedA == 0 && usedB == 0 && usedC == 1))
                    {
                        if (a == 0 && b == 0 && c == 1)
                        {
                            return i; // 直近上下位に 1C がある
                        }
                    }

                    break;
                case 7: // KPRY : ac@13, bc@14, cc@15 (1 桁)
                    a = k[13] - '0';
                    b = k[14] - '0';
                    c = k[15] - '0';
                    break;
                default:
                    a = 0;
                    b = 0;
                    break;
            }

            seA[i] = a;
            seB[i] = b;
            seC[i] = c;

            if (a > 0)
            {
                if (b > 0)
                {
                    flgT[it++] = i;
                }
                else
                {
                    flgA[ia++] = i;
                }
            }
            else if (b > 0)
            {
                flgB[ib++] = i;
            }
            else if (c > 0)
            {
                flgC[ic++] = i;
            }
            else
            {
                noContact = true; // 95.03.09
            }
        }

        int ret;

        // a>0 AND b>0 AND c>0
        if (usedA > 0 && usedB > 0 && usedC > 0)
        {
            for (int i = 0; i < ic; i++)
            {
                if (seC[flgC[i]] >= usedA + usedB + usedC)
                {
                    return flgC[i];
                }
            }

            for (int i = 0; i < it; i++)
            {
                if (seA[flgT[i]] >= usedA + usedC && seB[flgT[i]] >= usedB + usedC)
                {
                    return flgT[i];
                }
            }

            for (int i = 0; i < ia; i++)
            {
                if (seA[flgA[i]] >= usedA + usedC + 1)
                {
                    return flgA[i];
                }
            }

            if (ic > 0)
            {
                ret = flgC[ic - 1];
            }
            else if (it > 0)
            {
                ret = flgT[it - 1];
            }
            else if (ia > 0)
            {
                ret = flgA[ia - 1];
            }
            else
            {
                ret = -1;
            }
        }

        // a=0 AND b=0 AND c>0
        else if (usedA == 0 && usedB == 0 && usedC > 0)
        {
            for (int i = 0; i < ic; i++)
            {
                if (seC[flgC[i]] >= usedC)
                {
                    return flgC[i];
                }
            }

            for (int i = 0; i < it; i++)
            {
                if (seA[flgT[i]] >= usedC && seB[flgT[i]] >= usedC)
                {
                    return flgT[i];
                }
            }

            for (int i = 0; i < ia; i++)
            {
                if (seA[flgA[i]] >= usedC + 1)
                {
                    return flgA[i];
                }
            }

            if (ic > 0)
            {
                ret = flgC[ic - 1];
            }
            else if (it > 0)
            {
                ret = flgT[it - 1];
            }
            else if (ia > 0)
            {
                ret = flgA[ia - 1];
            }
            else
            {
                ret = -1;
            }
        }

        // a>0 AND b=0 AND c=0
        else if (usedA > 0 && usedB == 0 && usedC == 0)
        {
            // 1997.10.13 add: 使用接点数と同じ A 接点数を優先(保持 B 接点数が最小のものを選ぶ)。
            for (int i = 0; i < ia; i++)
            {
                if (seA[flgA[i]] == usedA)
                {
                    return flgA[i];
                }
            }

            for (int i = 0; i < it; i++)
            {
                if (seA[flgT[i]] == usedA)
                {
                    return flgT[i];
                }
            }

            for (int i = 0; i < ia; i++)
            {
                if (seA[flgA[i]] >= usedA)
                {
                    return flgA[i];
                }
            }

            for (int i = 0; i < it; i++)
            {
                if (seA[flgT[i]] >= usedA)
                {
                    return flgT[i];
                }
            }

            for (int i = 0; i < ic; i++)
            {
                if (seC[flgC[i]] >= usedA)
                {
                    return flgC[i];
                }
            }

            if (ia > 0)
            {
                ret = flgA[ia - 1];
            }
            else if (it > 0)
            {
                ret = flgT[it - 1];
            }
            else if (ic > 0)
            {
                ret = flgC[ic - 1];
            }
            else
            {
                ret = -1;
            }
        }

        // a=0 AND b>0 AND c=0
        else if (usedA == 0 && usedB > 0 && usedC == 0)
        {
            for (int i = 0; i < ib; i++)
            {
                if (seB[flgB[i]] >= usedB)
                {
                    return flgB[i];
                }
            }

            for (int i = 0; i < it; i++)
            {
                if (seB[flgT[i]] >= usedB)
                {
                    return flgT[i];
                }
            }

            for (int i = 0; i < ic; i++)
            {
                if (seC[flgC[i]] >= usedB)
                {
                    return flgC[i];
                }
            }

            if (ia > 0)
            {
                return flgA[0];
            }

            // 【C原典】原典のフォールバックは flg_a を ib 添字で参照する(未書込は -1)。忠実に再現する。
            if (ib > 0)
            {
                ret = flgA[ib - 1];
            }
            else if (it > 0)
            {
                ret = flgT[it - 1];
            }
            else if (ic > 0)
            {
                ret = flgC[ic - 1];
            }
            else if (ia > 0)
            {
                ret = flgA[ia - 1];
            }
            else
            {
                ret = -1;
            }
        }

        // a>0 AND b>0 AND c=0
        else if (usedA > 0 && usedB > 0 && usedC == 0)
        {
            for (int i = 0; i < it; i++)
            {
                if (seA[flgT[i]] >= usedA && seB[flgT[i]] >= usedB)
                {
                    return flgT[i];
                }
            }

            for (int i = 0; i < ic; i++)
            {
                if (seC[flgC[i]] >= usedA + usedB)
                {
                    return flgC[i];
                }
            }

            for (int i = 0; i < ia; i++)
            {
                if (seA[flgA[i]] >= usedA + 1)
                {
                    return flgA[i];
                }
            }

            if (it > 0)
            {
                ret = flgT[it - 1];
            }
            else if (ic > 0)
            {
                ret = flgC[ic - 1];
            }
            else if (ia > 0)
            {
                ret = flgA[ia - 1];
            }
            else
            {
                ret = -1;
            }
        }

        // a=0 AND b=0 AND c=0 : 使用接点がないときは 1 件目を常に返す。
        else if (usedA == 0 && usedB == 0 && usedC == 0)
        {
            if (no > 1)
            {
                // MC,MG 以外
                return 0;
            }

            // 96.01.17: MC,MG で使用接点がない場合。接点なしがあればそれ(1 件目)、
            // なければ A 接点を 1 つ以上持つ最初のもの、それも無ければ 1 件目。
            if (noContact)
            {
                ret = 0;
            }
            else
            {
                ret = -1;
                for (int i = 0; i < count; i++)
                {
                    if (seA[i] >= 1)
                    {
                        ret = i;
                        break;
                    }
                }

                if (ret == -1)
                {
                    ret = 0;
                }
            }
        }

        // 上記以外
        else
        {
            ret = -1;
        }

        return ret;
    }

    /// <summary>添字テーブルを -1 で初期化して生成する(【C原典】memset(flg,-1,kensu))。</summary>
    private static int[] CreateFilled(int length)
    {
        int[] array = new int[length];
        Array.Fill(array, -1);
        return array;
    }

    /// <summary>定格値キーの指定位置 <paramref name="length"/> 桁を Stoi する(【C原典】Stoi(tkey-&gt;x.ac,size))。</summary>
    private static int Stoi(string ratingKey, int offset, int length) =>
        EquipmentParameterFormatter.Stoi(ratingKey.Substring(offset, length), length);
}
