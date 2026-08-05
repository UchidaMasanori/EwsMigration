using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路の電気パラメータをコンポ対応で展開する。
/// 【C原典】toku/sekkei/src/Fyss40.c <c>Fyss40_Compo_DenryuuParm</c>(89)。
///
/// 各機器につき ep[2]→ep[1] を再セット(Ele_Area2_Copy)した後、ep[0] の各フィールドが
/// 非ゼロなら ep[1]・ep[2] へ複写する。系統種別が '1' の機器は最後に ep[1] をクリアする
/// (Ele_Area1_Clear)。private ヘルパ Ele_Area2_Copy / Ele_Area1_Clear / TakeTest / IsZero を含む。
/// </summary>
public static class CompositeElectricalParameterExpander
{
    /// <summary>
    /// 電気パラメータを ep[0]→ep[1][2] に展開する。【C原典】Fyss40_Compo_DenryuuParm(Fyss40.c:89)。
    /// </summary>
    /// <param name="mains">主回路レコード列。ElectricalParameterSlots を in-place 更新する。</param>
    public static void Expand(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            ElectricalParameters ep0 = d.ElectricalParameterSlots[0];
            ElectricalParameters ep1 = d.ElectricalParameterSlots[1];
            ElectricalParameters ep2 = d.ElectricalParameterSlots[2];

            // 【C原典】電気パラメータ[2]→[1]の再セット。
            EleArea2Copy(ep0, ep1, ep2);

            // 相数１(ＰＨ１) TR の1次側相数。
            if (ep0.Ph1 != "0")
            {
                ep2.Ph1 = ep1.Ph1 = ep0.Ph1;
            }
            // 相数２(ＰＨ２)。
            if (string.Concat(ep0.Ph2) != "00")
            {
                ep1.Ph2 = (string[])ep0.Ph2.Clone();
                ep2.Ph2 = (string[])ep0.Ph2.Clone();
            }
            // 線式１(ＷＲ１) TR の1次側線式。
            if (ep0.Wr1 != "0")
            {
                ep2.Wr1 = ep1.Wr1 = ep0.Wr1;
            }
            // 線式２(ＷＲ２)。
            if (string.Concat(ep0.Wr2) != "00")
            {
                ep1.Wr2 = (string[])ep0.Wr2.Clone();
                ep2.Wr2 = (string[])ep0.Wr2.Clone();
            }
            // 周波数(ＨＺ)。
            if (ep0.Hz != "00")
            {
                ep1.Hz = ep0.Hz;
                ep2.Hz = ep0.Hz;
            }

            // 極数(Ｐ)。改訂<1>: MC で ep[2]=001 かつ TakeTest 成立なら ep[0] を 000 にする。
            if (ep0.P != "000")
            {
                ep1.P = ep0.P;
                if (d.ReservedWord == "MC" && ep2.P == "001" && TakeTest(mains, i) == 1)
                {
                    ep0.P = "000";
                }
                else
                {
                    ep2.P = ep0.P;
                }
            }

            // エレメント数(Ｅ)。
            if (ep0.E != "0")
            {
                ep1.E = ep2.E = ep0.E;
            }
            if (ep0.E == "9")
            {
                ep1.E = ep2.E = "0";
            }

            // フレーム電流(ＡＦ)。
            if (ep0.Af != "00000.000")
            {
                ep1.Af = ep0.Af;
                ep2.Af = ep0.Af;
            }
            // トリップ電流(ＡＴ)。99999.999 は 00000.000 として展開。
            if (ep0.At != "00000.000")
            {
                ep1.At = ep0.At;
                ep2.At = ep0.At;
            }
            if (ep0.At == "99999.999")
            {
                ep1.At = "00000.000";
                ep2.At = "00000.000";
            }
            // 定格電流１(Ａ１)。
            if (ep0.A1 != "00000.000")
            {
                ep1.A1 = ep0.A1;
                ep2.A1 = ep0.A1;
            }
            // 定格電流２(Ａ２)。
            if (ep0.A2 != "00000.000")
            {
                ep1.A2 = ep0.A2;
                ep2.A2 = ep0.A2;
            }
            // 負荷容量(Ｗ)。
            if (ep0.W1 != "0000000.00")
            {
                ep1.W1 = ep0.W1;
                ep2.W1 = ep0.W1;
            }
            // 負荷容量(ＶＡ)。
            if (ep0.Va != "0000000.00")
            {
                ep1.Va = ep0.Va;
                ep2.Va = ep0.Va;
            }
            // 定格容量(ＫＶＡＲ)。
            if (ep0.Kvar != "000.00")
            {
                ep1.Kvar = ep0.Kvar;
                ep2.Kvar = ep0.Kvar;
            }
            // 静電容量(ＵＦ)。
            if (ep0.Uf != "000000.0")
            {
                ep1.Uf = ep0.Uf;
                ep2.Uf = ep0.Uf;
            }
            // 感度電流(ＭＡ)。
            if (ep0.Ma[0] != "0000")
            {
                ep1.Ma = (string[])ep0.Ma.Clone();
                ep2.Ma = (string[])ep0.Ma.Clone();
            }
            // 定格電圧1(Ｖ１)。
            if (ep0.V1[0] != "000000.0")
            {
                ep1.V1 = (string[])ep0.V1.Clone();
                ep2.V1 = (string[])ep0.V1.Clone();
            }
            if (ep0.V1Idx != "0")
            {
                ep1.V1Idx = ep2.V1Idx = ep0.V1Idx;
            }
            // 定格電圧2(Ｖ２)。
            if (ep0.V2[0] != "000000.0")
            {
                ep1.V2 = (string[])ep0.V2.Clone();
                ep2.V2 = (string[])ep0.V2.Clone();
            }
            if (ep0.V2Idx != "0")
            {
                ep1.V2Idx = ep2.V2Idx = ep0.V2Idx;
            }
            // 定格電圧2 ＡＣ／ＤＣ区分。
            if (ep0.V2Kbn != ' ')
            {
                ep1.V2Kbn = ep2.V2Kbn = ep0.V2Kbn;
            }
            // メーター定格(ＡＭ)。
            if (ep0.Am != "000")
            {
                ep1.Am = ep0.Am;
                ep2.Am = ep0.Am;
            }
            // 制御電圧(ＶＣ)。
            if (ep0.Vc != "000")
            {
                ep1.Vc = ep0.Vc;
                ep2.Vc = ep0.Vc;
            }
            // 制御電圧 ＡＣ／ＤＣ区分。
            if (ep0.VcKbn != ' ')
            {
                ep1.VcKbn = ep2.VcKbn = ep0.VcKbn;
            }
            // セット時間(ＳＳＥＴ)。
            if (ep0.Sset != "000000000.000")
            {
                ep1.Sset = ep0.Sset;
                ep2.Sset = ep0.Sset;
            }
            // 設定範囲時間(Ｓ／)。
            if (ep0.Ss != "000000000.000")
            {
                ep1.Ss = ep0.Ss;
                ep2.Ss = ep0.Ss;
            }
            // 設定範囲時間(Ｓ)。
            if (ep0.S != "000000000.000")
            {
                ep1.S = ep0.S;
                ep2.S = ep0.S;
            }
            // ａ／ｂ／ｃ接点数(ＡＣ／ＢＣ／ＣＣ)。ep[1] は個別に複写(941130 で ep[2] は下の一括ブロック)。
            if (ep0.Ac != "00")
            {
                ep1.Ac = ep0.Ac;
            }
            if (ep0.Bc != "00")
            {
                ep1.Bc = ep0.Bc;
            }
            if (ep0.Cc != "00")
            {
                ep1.Cc = ep0.Cc;
            }
            if (ep0.Ac != "00" || ep0.Bc != "00" || ep0.Cc != "00")   // 941130
            {
                ep2.Ac = ep0.Ac;
                ep2.Bc = ep0.Bc;
                ep2.Cc = ep0.Cc;
            }
            // 板厚(Ｔ)。
            if (ep0.T != "000.0")
            {
                ep1.T = ep0.T;
                ep2.T = ep0.T;
            }
            // 回路数(Ｋ)。
            if (ep0.K != "000")
            {
                ep1.K = ep0.K;
                ep2.K = ep0.K;
            }
            // 手配数量(ＱＴＹ)。
            if (ep0.Qty != '0')
            {
                ep1.Qty = ep2.Qty = ep0.Qty;
            }
            // 盤種類(ＢＮ)。
            if (ep0.Bn != ' ')
            {
                ep1.Bn = ep2.Bn = ep0.Bn;
            }
            // 電線サイズ(ＳＱ)。
            if (ep0.Sq != "000.00")
            {
                ep1.Sq = ep0.Sq;
                ep2.Sq = ep0.Sq;
            }
            // 芯数(Ｃ)。
            if (ep0.C != '0')
            {
                ep1.C = ep2.C = ep0.C;
            }
            // 回線数。
            if (ep0.Ksu != '0')
            {
                ep1.Ksu = ep2.Ksu = ep0.Ksu;
            }
            // 定格電流(ＭＡＨ)。
            if (ep0.Mah != "00000")
            {
                ep1.Mah = ep0.Mah;
                ep2.Mah = ep0.Mah;
            }
            // 抵抗値(Ｏ)。
            if (ep0.O != "0000.0")
            {
                ep1.O = ep0.O;
                ep2.O = ep0.O;
            }
            // 幅(Ｗ)。
            if (ep0.W2 != "000")
            {
                ep1.W2 = ep0.W2;
                ep2.W2 = ep0.W2;
            }
            // 径サイズ。
            if (ep0.Ksize != "000.0")
            {
                ep1.Ksize = ep0.Ksize;
                ep2.Ksize = ep0.Ksize;
            }
            // セット温度(ＣＳＥＴ)。
            if (ep0.Cset != "000")
            {
                ep1.Cset = ep0.Cset;
                ep2.Cset = ep0.Cset;
            }
            // 設定範囲温度(Ｃ／)。
            if (ep0.C1 != "000")
            {
                ep1.C1 = ep0.C1;
                ep2.C1 = ep0.C1;
            }
            // 設定範囲温度(Ｃ)。
            if (ep0.C2 != "000")
            {
                ep1.C2 = ep0.C2;
                ep2.C2 = ep0.C2;
            }

            // 【C原典】系統種別 '1' なら ep[1] をクリア。
            if (d.SystemKind == '1')
            {
                EleArea1Clear(ep0, ep1);
            }
        }
    }

    /// <summary>
    /// 電気パラメータ[2]を[1]へ再セットする。【C原典】Ele_Area2_Copy(Fyss40.c)。
    /// ep[0] の指定フィールドが全ゼロでないとき ep[1]←ep[2](接点数は元の ep[1] を保持し得る)。
    /// </summary>
    private static void EleArea2Copy(ElectricalParameters ep0, ElectricalParameters ep1, ElectricalParameters ep2)
    {
        int flag = 0;
        if (!IsZero(ep0.Af)) flag++;
        if (!IsZero(ep0.At)) flag++;
        if (!IsZero(ep0.A1)) flag++;
        if (!IsZero(ep0.A2)) flag++;
        if (!IsZero(ep0.W1)) flag++;
        if (!IsZero(ep0.Ma[0])) flag++;
        if (!IsZero(ep0.Ma[1])) flag++;
        if (!IsZero(ep0.Ma[2])) flag++;
        if (!IsZero(ep0.Ma[3])) flag++;
        if (!IsZero(ep0.Uf)) flag++;
        if (!IsZero(ep0.Kvar)) flag++;

        if (flag != 0)
        {
            // 【C原典】接点数(ＡＣ/ＢＣ/ＣＣ)を退避してから ep[1]←ep[2] の一括複写(941130)。
            string ac = ep1.Ac;
            string bc = ep1.Bc;
            string cc = ep1.Cc;

            CopyInto(ep2, ep1);

            if (ep0.Ac != "00" || ep0.Bc != "00" || ep0.Cc != "00")
            {
                ep1.Ac = ac;
                ep1.Bc = bc;
                ep1.Cc = cc;
            }
        }
    }

    /// <summary>
    /// 電気パラメータ[1]をクリアする。【C原典】Ele_Area1_Clear(Fyss40.c)。
    /// ep[0] の指定フィールドが全ゼロのとき ep[1] を '0' 埋め＋小数点付与でクリアする。
    /// </summary>
    private static void EleArea1Clear(ElectricalParameters ep0, ElectricalParameters ep1)
    {
        int flag = 0;
        if (!IsZero(ep0.Af)) flag++;
        if (!IsZero(ep0.At)) flag++;
        if (!IsZero(ep0.A1)) flag++;
        if (!IsZero(ep0.A2)) flag++;
        if (!IsZero(ep0.W1)) flag++;
        if (!IsZero(ep0.Ma[0])) flag++;
        if (!IsZero(ep0.Ma[1])) flag++;
        if (!IsZero(ep0.Ma[2])) flag++;
        if (!IsZero(ep0.Ma[3])) flag++;

        if (flag == 0)
        {
            ClearInto(ep1);
        }
    }

    /// <summary>
    /// MC の極数決定用判定。【C原典】TakeTest(Fyss40.c、改訂&lt;1&gt;)。
    /// 自身を親(oyatno)とする要素(datano 一致)で ep[0] エレメント数が '1' のものがあれば 1。
    /// </summary>
    private static int TakeTest(IReadOnlyList<MainCircuitResult> mains, int i)
    {
        string parent = mains[i].Data.ParentSequenceNumber;
        for (int j = 0; j < mains.Count; j++)
        {
            if (parent == mains[j].SequenceNumber && mains[j].Data.ElectricalParameterSlots[0].E == "1")
            {
                return 1;
            }
        }
        return 0;
    }

    /// <summary>指定フィールドが全て '0'(小数点は無視)かを判定する。【C原典】IsZero(Fyss40.c)。</summary>
    private static bool IsZero(string field)
    {
        foreach (char c in field)
        {
            if (c == '.')
            {
                continue;
            }
            if (c != '0')
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>電気パラメータ 1 件を全フィールド深いコピーする。【C原典】memcpy(&amp;ep[1],&amp;ep[2],sizeof(struct eparmg))。</summary>
    private static void CopyInto(ElectricalParameters src, ElectricalParameters dst)
    {
        dst.Ph1 = src.Ph1;
        dst.Ph2 = (string[])src.Ph2.Clone();
        dst.Wr1 = src.Wr1;
        dst.Wr2 = (string[])src.Wr2.Clone();
        dst.Hz = src.Hz;
        dst.P = src.P;
        dst.E = src.E;
        dst.Af = src.Af;
        dst.At = src.At;
        dst.A1 = src.A1;
        dst.A2 = src.A2;
        dst.W1 = src.W1;
        dst.Va = src.Va;
        dst.Kvar = src.Kvar;
        dst.Uf = src.Uf;
        dst.Ma = (string[])src.Ma.Clone();
        dst.V1 = (string[])src.V1.Clone();
        dst.V1Idx = src.V1Idx;
        dst.V2 = (string[])src.V2.Clone();
        dst.V2Idx = src.V2Idx;
        dst.V2Kbn = src.V2Kbn;
        dst.Am = src.Am;
        dst.Vc = src.Vc;
        dst.VcKbn = src.VcKbn;
        dst.Sset = src.Sset;
        dst.Ss = src.Ss;
        dst.S = src.S;
        dst.Ac = src.Ac;
        dst.Bc = src.Bc;
        dst.Cc = src.Cc;
        dst.T = src.T;
        dst.K = src.K;
        dst.Qty = src.Qty;
        dst.Bn = src.Bn;
        dst.Sq = src.Sq;
        dst.Esq = src.Esq;
        dst.C = src.C;
        dst.Ksu = src.Ksu;
        dst.Mah = src.Mah;
        dst.O = src.O;
        dst.W2 = src.W2;
        dst.Ksize = src.Ksize;
        dst.Cset = src.Cset;
        dst.C1 = src.C1;
        dst.C2 = src.C2;
    }

    /// <summary>
    /// 電気パラメータ 1 件を '0' 埋め＋規定桁の小数点付与でクリアする。
    /// 【C原典】memset('0') + SetPeri。盤種類/制御電圧区分/V2 区分は ' '。
    /// </summary>
    private static void ClearInto(ElectricalParameters ep)
    {
        ep.Ph1 = "0";
        ep.Ph2 = ["0", "0"];
        ep.Wr1 = "0";
        ep.Wr2 = ["0", "0"];
        ep.Hz = "00";
        ep.P = "000";
        ep.E = "0";
        ep.Af = "00000.000";
        ep.At = "00000.000";
        ep.A1 = "00000.000";
        ep.A2 = "00000.000";
        ep.W1 = "0000000.00";
        ep.Va = "0000000.00";
        ep.Kvar = "000.00";
        ep.Uf = "000000.0";
        ep.Ma = ["0000", "0000", "0000", "0000"];
        ep.V1 = ["000000.0", "000000.0", "000000.0"];
        ep.V1Idx = "0";
        ep.V2 = ["000000.0", "000000.0", "000000.0"];
        ep.V2Idx = "0";
        ep.Am = "000";
        ep.Vc = "000";
        ep.Sset = "000000000.000";
        ep.Ss = "000000000.000";
        ep.S = "000000000.000";
        ep.Ac = "00";
        ep.Bc = "00";
        ep.Cc = "00";
        ep.T = "000.0";
        ep.K = "000";
        ep.Sq = "000.00";
        ep.Esq = "000000";
        ep.Mah = "00000";
        ep.O = "0000.0";
        ep.W2 = "000";
        ep.Ksize = "000.0";
        ep.Cset = "000";
        ep.C1 = "000";
        ep.C2 = "000";
        ep.Qty = '0';
        ep.C = '0';
        ep.Ksu = '0';
        ep.Bn = ' ';
        ep.VcKbn = ' ';
        ep.V2Kbn = ' ';
    }
}
