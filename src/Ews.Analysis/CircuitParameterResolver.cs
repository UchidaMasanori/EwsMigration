using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 親機器の回路情報と主回路データ(FYRT800)から、自機器の下流回路パラメータを決定する。
/// 【C原典】toku/sekkei/src/Fyss14.c:1728 Kairo_Parm_Set。
///
/// 処理概要:
///   1. 主回路データ ep[0](相/線式/極)・fp.fpalv(負荷電圧)から入力パラメータ inputp を組立。
///   2. 予約語 NT は入力を 1P2W 固定に上書き。
///   3. mcprmcnv(<see cref="MainCircuitParameterConverter"/>)で親+入力→下流を変換。
///   4. 予約語 F(ヒューズ)は回路要素(主回路 kiryoso=='1' / 計器回路)・親の相線式・
///      負荷電圧・近傍機器(LA/VS/VM)に応じて下流パラメータを個別に上書き。
///   5. mcprmcnv が失敗していれば親をそのままコピー。
/// C 原典は常に成功(1)を返す(「エラーは発生させない」)。
///
/// 依存(いずれも移植済・ISAM/マスタ非依存): MainCircuitParameter(MCPRMS) /
/// MainCircuitResult・MainCircuitData(FYRT800) / ElectricalParameters(ep[0]) /
/// AttachedParameters(fp) / <see cref="VoltageInheritance"/>(Volt_Conv) /
/// <see cref="MainCircuitParameterConverter"/>(mcprmcnv)。
/// </summary>
public static class CircuitParameterResolver
{
    /// <summary>
    /// 自機器の下流回路パラメータを設定する。【C原典】Kairo_Parm_Set。
    /// </summary>
    /// <param name="frequency">回路周波数(Helutzu)。C 原典では本関数内で未参照(署名忠実のため保持)。</param>
    /// <param name="parent">親機器の回路情報(pprmp)。破壊的更新はしない。</param>
    /// <param name="result">自機器の回路情報(newpprmp, 出力)。</param>
    /// <param name="mainCircuits">主回路データ列(Smaina 起点の配列に相当)。</param>
    /// <param name="index">自機器の位置。Smaina = mainCircuits[index]。</param>
    /// <param name="remainingCount">主回路残り件数(zmainc)。下流走査は index+k(1&lt;=k&lt;remainingCount)。</param>
    /// <returns>常に 1(成功)。【C原典】return(r?0:1) で r は最終的に 0。</returns>
    public static int SetCircuitParameter(
        short frequency,
        MainCircuitParameter parent,
        MainCircuitParameter result,
        IReadOnlyList<MainCircuitResult> mainCircuits,
        int index,
        int remainingCount)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(mainCircuits);

        _ = frequency; // 【C原典】Helutzu は Kairo_Parm_Set 本体では参照されない。

        MainCircuitData dt = mainCircuits[index].Data;
        ElectricalParameters ep0 = dt.ElectricalParameterSlots[0];
        AttachedParameters fp = dt.AttachedParameter;

        // 【C原典】入力相数/線式データ設定(ep[0] の先頭1バイトを atoi)、極数(epap[3])。
        MainCircuitParameter inputp = new()
        {
            Phase = (short)AtoiC(ep0.Ph2[0]),    // epaph2 先頭1バイト
            WireType = (short)AtoiC(ep0.Wr2[0]), // epawr2 先頭1バイト
            Pole = (short)AtoiC(ep0.P),          // epap[3]
            AcDcKind = parent.AcDcKind,          // 【C原典】inputp.vkbn = pprmp->vkbn
        };

        // 【C原典】入力電圧データ設定。fp.fpalv[0]/[1] を atoi(int v[2])。
        int v0 = AtoiC(fp.LoadVoltage[0]);
        int v1 = AtoiC(fp.LoadVoltage[1]);
        // inputp.v[] は memset 相当で 0(new() により 0 初期化済)。

        // 【C原典】入力電圧データの後ろ詰め正規化。if(v[0]&&!v[1]){ v[1]=v[0]; v[0]=0; }
        if (v0 != 0 && v1 == 0)
        {
            v1 = v0;
            v0 = 0;
        }

        // 【C原典】v[0]==0 && v[1]!=0: 親電圧と比較して v2/v3 の設定個所を判定。
        if (v0 == 0 && v1 != 0)
        {
            short[] kv = [0, parent.Voltage[1], parent.Voltage[2]];
            VoltageInheritance.ConvertVoltage(kv, kv); // 【C原典】Volt_Conv(kv,kv)
            if (kv[1] == v1)
            {
                inputp.Voltage[1] = (short)v1;
            }
            if (kv[2] == v1)
            {
                inputp.Voltage[2] = (short)v1;
            }
        }

        // 【C原典】v[0]!=0 && v[1]!=0: そのままの順で設定。
        if (v0 != 0 && v1 != 0)
        {
            inputp.Voltage[1] = (short)v0;
            inputp.Voltage[2] = (short)v1;
        }

        // 【C原典】予約語 NT は入力を 1P2W(相1/線式2/極0/電圧0)固定に上書き。
        if (dt.ReservedWord == "NT")
        {
            inputp.Phase = 1;
            inputp.WireType = 2;
            inputp.Pole = 0;
            inputp.Voltage[0] = 0;
            inputp.Voltage[1] = 0;
            inputp.Voltage[2] = 0;
            inputp.AcDcKind = parent.AcDcKind;
        }

        // 【C原典】主回路データ変換。r: 正常=0 / 異常=-1。
        int r = MainCircuitParameterConverter.ConvertParameter(parent, inputp, result);

        // 【C原典】F(ヒューズ)の回路情報セット(95.02.01)。
        // 主回路要素は相のバランスを考え、計器回路要素は相を固定とする。
        if (dt.ReservedWord == "F")
        {
            if (dt.CircuitElement == '1')
            {
                ResolveFuseMainCircuit(parent, result, fp, mainCircuits, index);
            }
            else
            {
                ResolveFuseInstrumentCircuit(parent, result, mainCircuits, index, remainingCount);
            }
        }

        // 【C原典】回路電圧の変換に失敗(941004)した場合は親データをそのままコピー
        // (MCPRMS 構造体全体 = vkbn も含む)。
        if (r != 0)
        {
            result.Phase = parent.Phase;
            result.WireType = parent.WireType;
            result.Pole = parent.Pole;
            result.Voltage[0] = parent.Voltage[0];
            result.Voltage[1] = parent.Voltage[1];
            result.Voltage[2] = parent.Voltage[2];
            result.AcDcKind = parent.AcDcKind;
        }

        // 【C原典】エラーは発生させない。r=0; return(r?0:1) → 常に 1。
        return 1;
    }

    /// <summary>
    /// F(ヒューズ)・回路要素=主回路(kiryoso=='1')の下流パラメータ上書き。
    /// 【C原典】Kairo_Parm_Set 内 yoyaku=="F" &amp;&amp; kiryoso=='1' ブロック。
    /// </summary>
    private static void ResolveFuseMainCircuit(
        MainCircuitParameter parent, MainCircuitParameter result,
        AttachedParameters fp, IReadOnlyList<MainCircuitResult> mains, int index)
    {
        // 【C原典】1P2W
        if (parent.Phase == 1 && parent.WireType == 2)
        {
            CopyParentPhaseVoltage(result, parent); // X,N または X,Y相をとる
        }
        // 【C原典】1P3W
        else if (parent.Phase == 1 && parent.WireType == 3)
        {
            // 負荷電圧=200V指定のとき
            if (Memcmp3(fp.LoadVoltage[0], "200") == 0 && Memcmp3(fp.LoadVoltage[1], "000") == 0)
            {
                result.Phase = 1;
                result.WireType = 2;
                result.Pole = 2;
                result.Voltage[0] = 0;
                result.Voltage[1] = 0;
                result.Voltage[2] = parent.Voltage[1]; // X,Y相をとる
            }
            // 負荷電圧=200/100V指定のとき
            else if (Memcmp3(fp.LoadVoltage[0], "200") == 0 && Memcmp3(fp.LoadVoltage[1], "100") == 0)
            {
                CopyParentPhaseVoltage(result, parent); // X,N,Y相をとる
            }
            // 改訂<26> 次機器が LA(SPD分離器用 Fuse)
            else if (NextReservedWordIsLa(mains, index))
            {
                CopyParentPhaseVoltage(result, parent); // X,N,Y相をとる
            }
            // 負荷電圧=100V指定 or 指定なし
            else
            {
                result.Phase = 1;
                result.WireType = 2;
                result.Pole = 1;
                result.Voltage[0] = 0;
                result.Voltage[1] = 0;
                result.Voltage[2] = parent.Voltage[2]; // X,N または Y,N相をとる
            }
        }
        // 【C原典】3P3W
        else if (parent.Phase == 3 && parent.WireType == 3)
        {
            result.Phase = 1;
            result.WireType = 2;
            result.Pole = 2;
            result.Voltage[0] = 0;
            result.Voltage[1] = 0;
            result.Voltage[2] = parent.Voltage[2]; // R,S / S,T / T,R 相をとる

            // 改訂<26> 次機器が LA(SPD分離器用 Fuse)なら親をそのままコピー
            if (NextReservedWordIsLa(mains, index))
            {
                CopyParentPhaseVoltage(result, parent); // X,N,Y相をとる
            }
        }
        // 【C原典】改訂<23> 3相4線
        else if (parent.Phase == 3 && parent.WireType == 4)
        {
            result.Phase = 1;
            result.WireType = 2;
            result.Voltage[0] = 0;
            result.Voltage[1] = 0;
            result.Voltage[2] = parent.Voltage[2]; // R,S / S,T / T,R

            if (Memcmp3(fp.LoadVoltage[0], "100") == 0)
            {
                result.Pole = 1;
            }
            else if (Memcmp3(fp.LoadVoltage[0], "200") == 0)
            {
                result.Pole = 2;
            }
            // それ以外は極数を変更しない(mcprmcnv の値を保持)。
        }
    }

    /// <summary>
    /// F(ヒューズ)・回路要素=計器回路の下流パラメータ上書き。
    /// 【C原典】Kairo_Parm_Set 内 yoyaku=="F" の else(計器回路)ブロック。
    /// 下流に VS/VM があるかを走査して相を固定する。
    /// </summary>
    private static void ResolveFuseInstrumentCircuit(
        MainCircuitParameter parent, MainCircuitParameter result,
        IReadOnlyList<MainCircuitResult> mains, int index, int remainingCount)
    {
        // 【C原典】1P2W はそのままコピー。
        if (parent.Phase == 1 && parent.WireType == 2)
        {
            CopyParentPhaseVoltage(result, parent); // X,N または X,Y相をとる
            return;
        }

        // 【C原典】下流に VS があるか調べる。
        bool vsFound = ScanDownstreamFor(mains, index, remainingCount, "VS");
        // 【C原典】fprintf(stderr,"VS_found=[%d]\n",VS_found); はデバッグ出力のため移植しない。

        // 【C原典】1P3W
        if (parent.Phase == 1 && parent.WireType == 3)
        {
            if (!vsFound) // VS なし
            {
                // 【C原典】下流に VM があるか調べる(950303)。
                bool vmFound = ScanDownstreamFor(mains, index, remainingCount, "VM");
                if (!vmFound) // VM なし → 105V
                {
                    result.Phase = 1;
                    result.WireType = 2;
                    result.Pole = 1;
                    result.Voltage[0] = 0;
                    result.Voltage[1] = 0;
                    result.Voltage[2] = parent.Voltage[2]; // 105V
                }
                else // VM あり → 210V
                {
                    result.Phase = 1;
                    result.WireType = 2;
                    result.Pole = 2;
                    result.Voltage[0] = 0;
                    result.Voltage[1] = 0;
                    result.Voltage[2] = parent.Voltage[1]; // 210V
                }
            }
            else // VS あり
            {
                CopyParentPhaseVoltage(result, parent); // X,N,Y相をとる
            }
        }

        // 【C原典】3P3W(1P3W とは別 if。C の構造を保持)
        if (parent.Phase == 3 && parent.WireType == 3)
        {
            if (!vsFound) // VS なし
            {
                result.Phase = 1;
                result.WireType = 2;
                result.Pole = 2;
                result.Voltage[0] = 0;
                result.Voltage[1] = 0;
                result.Voltage[2] = parent.Voltage[2]; // R,S相をとる
            }
            else // VS あり
            {
                CopyParentPhaseVoltage(result, parent); // R,S,T相をとる
            }
        }
    }

    /// <summary>
    /// 下流に指定予約語(VS/VM)があるかを走査する。
    /// 【C原典】Kairo_Parm_Set 内の下流走査ループ(入線番号/階層/直列番号で打ち切り)。
    /// </summary>
    private static bool ScanDownstreamFor(
        IReadOnlyList<MainCircuitResult> mains, int index, int remainingCount, string target)
    {
        MainCircuitData cur = mains[index].Data;
        for (int k = 1; k < remainingCount; k++)
        {
            MainCircuitData d = mains[index + k].Data;
            // 【C原典】入線番号が異なれば打ち切り。
            if (Memcmp3(cur.IncomingNumber, d.IncomingNumber) != 0)
            {
                break;
            }
            // 【C原典】自身の階層 > 下流の階層 なら打ち切り。
            if (Memcmp3(cur.HierarchyNumber, d.HierarchyNumber) > 0)
            {
                break;
            }
            // 【C原典】階層が同じで下流の直列番号が "001" なら打ち切り。
            if (Memcmp3(cur.HierarchyNumber, d.HierarchyNumber) == 0 &&
                Memcmp3(d.SeriesNumber, "001") == 0)
            {
                break;
            }
            // 【C原典】下流の予約語が target なら検出。
            if (d.ReservedWord == target)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 次機器(Smaina+1)の予約語が LA(避雷器/SPD)かを判定する。
    /// 【C原典】memcmp((Smaina+1)->dt.yoyaku, "LA ", 3) == 0。範囲外は不一致扱い。
    /// </summary>
    private static bool NextReservedWordIsLa(IReadOnlyList<MainCircuitResult> mains, int index)
    {
        int next = index + 1;
        if (next >= mains.Count)
        {
            return false;
        }
        return mains[next].Data.ReservedWord == "LA";
    }

    /// <summary>
    /// 親の相/線式/極数/電圧を出力へコピーする(vkbn は変更しない)。
    /// 【C原典】newpprmp->ph/wr/p/v[0..2] = pprmp の対応値。
    /// </summary>
    private static void CopyParentPhaseVoltage(MainCircuitParameter result, MainCircuitParameter parent)
    {
        result.Phase = parent.Phase;
        result.WireType = parent.WireType;
        result.Pole = parent.Pole;
        result.Voltage[0] = parent.Voltage[0];
        result.Voltage[1] = parent.Voltage[1];
        result.Voltage[2] = parent.Voltage[2];
    }

    /// <summary>
    /// C の <c>memcmp</c>/<c>strncmp</c>(先頭3バイト)相当。固定長3の数値/文字フィールド用。
    /// ASCII 前提でバイト値=文字コード。3文字に満たない側は '\0'(0)として比較する。
    /// </summary>
    private static int Memcmp3(string a, string b)
    {
        for (int i = 0; i < 3; i++)
        {
            int ca = i < a.Length ? a[i] : 0;
            int cb = i < b.Length ? b[i] : 0;
            if (ca != cb)
            {
                return ca - cb;
            }
        }
        return 0;
    }

    /// <summary>
    /// C の <c>atoi()</c> 相当。先頭空白/符号を許容し、数字が続く間だけを整数化する
    /// (非数字で打ち切り)。【C原典】atoi。
    /// </summary>
    private static int AtoiC(string s)
    {
        int i = 0;
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t'))
        {
            i++;
        }
        int sign = 1;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            if (s[i] == '-')
            {
                sign = -1;
            }
            i++;
        }
        long value = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            value = value * 10 + (s[i] - '0');
            i++;
        }
        return (int)(sign * value);
    }
}
