using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 計器回路機器(PLTR/VT/CT/F/DSW)の負荷容量(VA)・二次側電流(A2)値を、下流機器の
/// 定格容量(teiwva)を積み上げて主回路の電気パラメータへ設定する。
/// 【C原典】<c>Fysk00_Make_Keiki</c>(toku/sekkei/src/Fysk00.c:3974)。
///
/// 1 回の呼び出しで、予約語順(PLTR→VT→CT→F→DSW)に最初に該当したタイプの機器を
/// すべて処理して <c>katei</c> を 0→1 にする(呼び出し側が機器サーチ後に 2 へ)。
/// 全機器が <c>katei==2</c> になれば 0(完了)、残りが有れば 1 を返す。
/// </summary>
public static class MeterCircuitBuilder
{
    /// <summary>入力有無判定のしきい値。【C原典】TOL(fyrt808.h) = 0.001。</summary>
    private const double Tol = 0.001;

    /// <summary>
    /// 積み上げ対象の計器予約語(前方一致キー、末尾空白を含む)。
    /// 【C原典】static CHAR Keiki_Yo[5][6] = { "PLTR ","VT ","CT ","F  ","DSW " }。
    /// </summary>
    private static readonly string[] MeterReservedWords = ["PLTR ", "VT ", "CT ", "F  ", "DSW "];

    /// <summary>
    /// 計器回路機器の VA・W 値を設定する。【C原典】<c>Fysk00_Make_Keiki(kc, km, cnt, sk)</c>。
    /// </summary>
    /// <param name="meters">計器回路機器の該当レコード一覧。【C原典】km[](件数 kc)。</param>
    /// <param name="records">主回路エリア。【C原典】sk[](件数 cnt)。datano は 1 始まりの通し番号。</param>
    /// <returns>0:全機器処理済(katei==2) / 1:残り有。【C原典】chk。</returns>
    public static short AssignCapacities(IReadOnlyList<MeterCircuitEntry> meters, IReadOnlyList<MainCircuitResult> records)
    {
        ArgumentNullException.ThrowIfNull(meters);
        ArgumentNullException.ThrowIfNull(records);

        int meterCount = meters.Count;
        int recordCount = records.Count;
        int matchedType = -1;   // 【C原典】chk(-1 = 未マッチ)

        for (int k = 0; k < MeterReservedWords.Length; k++)
        {
            string prefix = MeterReservedWords[k];
            for (int i = 0; i < meterCount; i++)
            {
                MeterCircuitEntry meter = meters[i];
                if (meter.Katei != 0)
                {
                    continue;
                }

                MainCircuitData self = records[meter.Rec].Data;

                // 【C原典】memcmp(Keiki_Yo[k], yoyaku, strlen(Keiki_Yo[k]))==0(前方一致)。
                if (!PaddedReservedWord(self.ReservedWord).StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                // 【C原典】ep[1] に定格値が無ければ eno=2、有れば eno=1。
                ElectricalParameters ep1 = self.ElectricalParameterSlots[1];
                int eno = (Stof(ep1.At, 9) < Tol && Stof(ep1.Af, 9) < Tol &&
                           Stof(ep1.A1, 9) < Tol && Stof(ep1.A2, 9) < Tol &&
                           Stof(ep1.Ma[0], 4) < Tol && Stof(ep1.W1, 10) < Tol) ? 2 : 1;

                int kpoint = meter.Rec + 1;   // 【C原典】km[i].rec+1(1 始まり)。

                // 【C原典】950531: CT は回路要素が '1' 以外の同一機器(CT)の下流を参照する。
                if (IsExactReservedWord(self.ReservedWord, "CT      "))
                {
                    for (int j = 0; j < recordCount; j++)
                    {
                        MainCircuitData other = records[j].Data;
                        if (IsSameReservedWord(self.ReservedWord, other.ReservedWord) &&
                            IsSameIdentity(self.IdentityNumber, other.IdentityNumber) &&
                            other.CircuitElement != '1')
                        {
                            kpoint = j + 1;
                            break;
                        }
                    }
                }

                matchedType = k;
                IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(records, kpoint);
                int ken = downstream?.Count ?? 0;
                ElectricalParameters epOut = self.ElectricalParameterSlots[eno];

                if (k <= 2)   // 【C原典】k>=0 && k<=2 : PLTR, VT, CT
                {
                    if (Stof(ep1.Va, 10) < Tol)
                    {
                        if (ken > 0)
                        {
                            double all = AccumulateAndClear(records, downstream!, ken);
                            epOut.Va = Fit(EquipmentParameterFormatter.SprintfF("%010.2f", all), 10);
                            if (k == 0)   // 【C原典】PLTR は二次電圧未入力なら 1VA で 5.5V/それ以外 15.0V。
                            {
                                if (Stof(epOut.V2[0], 8) < Tol)
                                {
                                    double v = Math.Abs(all - 1.0) < Tol ? 5.5 : 15.0;
                                    epOut.V2[0] = Fit(EquipmentParameterFormatter.SprintfF("%08.1f", v), 8);
                                }
                            }
                        }
                    }
                    else
                    {
                        ClearDownstream(records, downstream, ken);
                    }
                    meter.Katei = 1;
                }
                else   // 【C原典】k==3 || k==4 : F, DSW
                {
                    if (Stof(ep1.A2, 9) < Tol)
                    {
                        double all = 0.0;
                        if (ken > 0)
                        {
                            all = AccumulateAndClear(records, downstream!, ken);
                        }
                        records[meter.Rec].Work.RatedCapacity = all;
                        all /= Stof(epOut.V2[0], 8);
                        epOut.A2 = Fit(EquipmentParameterFormatter.SprintfF("%09.3f", all), 9);
                    }
                    else
                    {
                        records[meter.Rec].Work.RatedCapacity = 0.0;
                        ClearDownstream(records, downstream, ken);
                    }
                    meter.Katei = 1;
                }
            }

            // 【C原典】あるタイプで 1 件でも処理したら k ループを抜ける。
            if (matchedType != -1)
            {
                break;
            }
        }

        // 【C原典】全 katei==2 なら 0、残りが有れば 1。
        foreach (MeterCircuitEntry meter in meters)
        {
            if (meter.Katei != 2)
            {
                return 1;
            }
        }

        return 0;
    }

    /// <summary>下流機器の定格容量(teiwva)を合算し、合算元をクリアする。</summary>
    private static double AccumulateAndClear(IReadOnlyList<MainCircuitResult> records, IReadOnlyList<int> downstream, int ken)
    {
        double all = 0.0;
        for (int j = 0; j < ken; j++)
        {
            MainCircuitResult d = records[downstream[j] - 1];
            all += d.Work.RatedCapacity;
            d.Work.RatedCapacity = 0.0;
        }

        return all;
    }

    /// <summary>下流機器の定格容量(teiwva)をクリアするのみ(合算しない)。</summary>
    private static void ClearDownstream(IReadOnlyList<MainCircuitResult> records, IReadOnlyList<int>? downstream, int ken)
    {
        if (ken <= 0 || downstream is null)
        {
            return;
        }

        for (int j = 0; j < ken; j++)
        {
            records[downstream[j] - 1].Work.RatedCapacity = 0.0;
        }
    }

    /// <summary>予約語を 8 文字空白パディングする(memcmp 用)。</summary>
    private static string PaddedReservedWord(string? reservedWord) => (reservedWord ?? string.Empty).PadRight(8);

    /// <summary>予約語が指定 8 バイト値と完全一致するか。【C原典】memcmp(yoyaku, target, 8)==0。</summary>
    private static bool IsExactReservedWord(string? reservedWord, string target)
        => string.CompareOrdinal(PaddedReservedWord(reservedWord), 0, target, 0, 8) == 0;

    /// <summary>2 つの予約語が一致するか。【C原典】memcmp(a.yoyaku, b.yoyaku, sizeof(yoyaku)=8)==0。</summary>
    private static bool IsSameReservedWord(string? a, string? b)
        => string.CompareOrdinal(PaddedReservedWord(a), 0, PaddedReservedWord(b), 0, 8) == 0;

    /// <summary>2 つの同一機器認識番号が一致するか。【C原典】memcmp(a.doukkno, b.doukkno, sizeof(doukkno)=2)==0。</summary>
    private static bool IsSameIdentity(string? a, string? b)
        => string.CompareOrdinal((a ?? string.Empty).PadRight(2), 0, (b ?? string.Empty).PadRight(2), 0, 2) == 0;

    /// <summary>固定長フィールドへの memcpy 相当(幅超過は先頭 width で切り詰め)。</summary>
    private static string Fit(string s, int width) => s.Length > width ? s[..width] : s;

    private static double Stof(string? s, int size) => EquipmentParameterFormatter.Stof(s, size);
}
