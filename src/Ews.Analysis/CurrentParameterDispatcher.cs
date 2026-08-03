using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 電流に関するパラメータ設定処理のディスパッチャ本体。
/// 【C原典】Fyss3G_Denryuu_Parm_Set(toku/sekkei/src/Fyss3G.c)。
///
/// 主回路エリア(<see cref="MainCircuitResult"/> の一覧)を走査し、系統種別/回路要素/
/// 負荷容量決定テーブル(<c>Check_fyrt812</c>)でフィルタしたレコードについて、
/// パラメータ設定タイプ(amp001.cns の <c>prm_tp</c>)を検索し、予約語種別に応じた
/// 個別セッタ(<see cref="CurrentParameterSetter"/>)へ振り分けて電気パラメータ
/// (AT/AF/A1/A2/MA/SQ/VA 等)を設定する。
///
/// 【依存の注入】C 原典は 4 つのコンスタントファイル(amp001～amp004.cns)を
/// <c>FyGetFilePath("SEKKEI")</c> + <c>CnsXxxRead</c> でその場に読み込むが、本移植は
/// <see cref="Ews.Data.Seeding.CurrentParameterTableLoader"/> が構築済みの一覧を引数で受け取る。
/// また <c>Set_AM</c> が内部で参照するゾーンコード(<c>FyGetZoneCD</c>)、
/// <c>Set_MC</c> と改訂&lt;3&gt;の製作仕様判定が参照する製作仕様区分
/// (<c>bukken1-&gt;com.kyo.sshiykbn</c>)も引数注入とする。
///
/// 【C 原典の忠実再現】switch は C 原典の case 構成をそのまま写す。
///   ・<c>MGFR</c>(prm_tp=72)は MG 系の case 列(MG/MGSD/MGFRSD)に含まれないため既定(no-op)。
///   ・<c>SC</c>/<c>NT</c>/<c>TSW</c>/<c>2ERY</c>/<c>LGT</c>/<c>LSW</c>/<c>DSW</c> 等、
///     switch に無い prm_tp はすべて既定(no-op)。
///   ・<c>DCPW</c> は C 原典が空関数 <c>Fyss3G_Set_DCPW</c> を呼ぶため no-op(明示 case)。
///   ・<c>CKS</c> は C 原典が <c>Fyss3G_Set_CKS( prm1, ... )</c> と呼ぶ(定義側の第 1 引数名は
///     <c>prm2</c> だが、渡す実引数は <c>prm1</c>)。本移植も <see cref="CurrentParameterSetter.SetCks"/>
///     に <c>prm1</c> の値を渡してこの挙動を忠実再現する。
///   ・<c>prm2</c> は <c>Check_fyrt800</c> が算出するが、ディスパッチャは全 case で <c>prm1</c> のみを
///     渡す(prm2 は未使用)。本移植も破棄する。
/// </summary>
public static class CurrentParameterDispatcher
{
    // ---- パラメータ設定タイプ(PRM_* 定数, fyss3g01.h)----
    private const int Mcb = 1;
    private const int Elb = 2;
    private const int Mmcb = 3;
    private const int Elmb = 4;
    private const int Sb = 5;
    private const int Rmcb = 6;
    private const int Mc = 10;
    private const int Thr = 11;
    private const int Mg = 12;
    private const int Wh = 15;
    private const int Am = 17;
    private const int Ct = 19;
    private const int Tb = 22;
    private const int Con = 23;
    private const int Tr = 24;
    private const int Zct = 25;
    private const int Lgr = 26;
    private const int Elr = 27;
    private const int Hpsb = 28;
    private const int Hsb = 29;
    private const int Rry = 30;
    private const int Mcdt = 32;
    private const int F = 33;
    private const int Ts = 38;
    private const int Dcpw = 42;
    private const int Ssw = 43;
    private const int Cp = 47;
    private const int Cks = 49;
    private const int L = 70;
    private const int Mcfr = 71;
    private const int Mcsd = 73;
    private const int Mgsd = 74;
    private const int Mcfrsd = 75;
    private const int Mgfrsd = 76;
    private const int Tsu = 77;
    private const int Sswu = 78;
    private const int Pbsu = 79;
    private const int Cosu = 80;
    private const int TwoCosu = 81;
    private const int Olu = 82;

    /// <summary>
    /// 電流パラメータ設定のディスパッチ。【C原典】Fyss3G_Denryuu_Parm_Set。
    /// </summary>
    /// <param name="manufacturingSpecKind">
    /// 製作仕様区分。【C原典】bukken1-&gt;com.kyo.sshiykbn。先頭 2 文字が "01"(河村標準)なら
    /// 改訂&lt;3&gt;の <c>seisakusiyou=1</c>。<see cref="CurrentParameterSetter.SetMc"/> にもそのまま渡す。
    /// </param>
    /// <param name="count">主回路データ件数。【C原典】Pmainc。ループ上限かつ AM/CT/MC へ渡す件数。</param>
    /// <param name="records">主回路エリア。【C原典】maina[]。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    /// <param name="kubun">
    /// 処理区分。【C原典】kubun。'M'(主回路)は回路要素 <c>kiryoso=='1'</c> のみ処理、
    /// それ以外(計器回路)は <c>kiryoso!='1'</c> のみ処理する。
    /// </param>
    /// <param name="parameterSettingTable">パラメータ設定タイプ一覧(amp001.cns)。【C原典】prmtp_cp。</param>
    /// <param name="wireSizeTable">電線サイズ設定一覧(amp002.cns)。【C原典】sqset_cp。</param>
    /// <param name="ratedCurrent2Table">定格電流２設定一覧(amp003.cns)。【C原典】a2set_cp。</param>
    /// <param name="ratedCurrent1Table">定格電流１設定一覧(amp004.cns)。【C原典】a1set_cp。</param>
    /// <param name="zoneCode">
    /// ゾーンコード。【C原典】Fyss3G_Set_AM 内 FyGetZoneCD(zone_cd)。改訂&lt;11&gt;/&lt;12&gt;の
    /// 特定ゾーン延長目盛り判定に使用。
    /// </param>
    public static void DispatchCurrentParameters(
        string manufacturingSpecKind,
        int count,
        IReadOnlyList<MainCircuitResult> records,
        int inputFlag,
        char kubun,
        IReadOnlyList<ParameterSettingType> parameterSettingTable,
        IReadOnlyList<WireSizeSetting> wireSizeTable,
        IReadOnlyList<RatedCurrent2Setting> ratedCurrent2Table,
        IReadOnlyList<RatedCurrent1Setting> ratedCurrent1Table,
        string zoneCode)
    {
        ArgumentNullException.ThrowIfNull(manufacturingSpecKind);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(parameterSettingTable);
        ArgumentNullException.ThrowIfNull(wireSizeTable);
        ArgumentNullException.ThrowIfNull(ratedCurrent2Table);
        ArgumentNullException.ThrowIfNull(ratedCurrent1Table);
        ArgumentNullException.ThrowIfNull(zoneCode);

        // 【C原典 改訂<3>】製作仕様区分の先頭 2 文字が "01" なら河村標準(seisakusiyou=1)。
        int seisakusiyou = manufacturingSpecKind.StartsWith("01", StringComparison.Ordinal) ? 1 : 0;

        // 【C原典】for( lp = 0 ; lp < Pmainc ; lp++ )。
        for (int lp = 0; lp < count; lp++)
        {
            MainCircuitData dt = records[lp].Data;

            // 【C原典】系統種別 '1'(P系統)以外はスキップ。
            if (dt.SystemKind != '1')
            {
                continue;
            }

            // 【C原典】kubun=='M' は主回路(kiryoso=='1')のみ、それ以外は計器回路(kiryoso!='1')のみ。
            if (kubun == 'M')
            {
                if (dt.CircuitElement != '1')
                {
                    continue;
                }
            }
            else
            {
                if (dt.CircuitElement == '1')
                {
                    continue;
                }
            }

            // 【C原典】負荷容量決定テーブルチェック(sret!=0 でスキップ)。
            if (CurrentParameterTableSeeker.CheckLoadCapacityTable(records[lp]) != 0)
            {
                continue;
            }

            // 【C原典】設定電流値のチェック(prm1/prm2 算出)。prm2 はディスパッチャでは未使用。
            CurrentParameterSetter.ComputeParameterFlags(records[lp], out int prm1, out _);

            // 【C原典】パラメータ設定タイプをセット(該当無しは prm_tp=0)。
            ParameterSettingType? prmEntry =
                CurrentParameterTableSeeker.SeekParameterSettingType(parameterSettingTable, records[lp]);
            int prmType = prmEntry?.SettingType ?? 0;

            switch (prmType)
            {
                // 【C原典】CP/RMCB/HPSB/HSB/MCB/SB。
                case Cp:
                case Rmcb:
                case Hpsb:
                case Hsb:
                case Mcb:
                case Sb:
                    CurrentParameterSetter.SetMcb(prm1, records, lp, inputFlag);
                    break;

                case Elb:
                    CurrentParameterSetter.SetElb(prm1, records, lp, inputFlag);
                    break;

                case Mmcb:
                    CurrentParameterSetter.SetMmcb(prm1, records, lp, inputFlag);
                    break;

                case Elmb:
                    CurrentParameterSetter.SetElmb(prm1, records, lp, inputFlag);
                    break;

                // 【C原典】MC/MCFR/MCSD/MCFRSD。
                case Mc:
                case Mcfr:
                case Mcsd:
                case Mcfrsd:
                    CurrentParameterSetter.SetMc(
                        prm1, records, lp, count, ratedCurrent2Table, inputFlag, manufacturingSpecKind);
                    break;

                case Thr:
                    CurrentParameterSetter.SetThr(prm1, records, lp, inputFlag);
                    break;

                // 【C原典】MG/MGSD/MGFRSD(MGFR は含まない = 既定 no-op)。
                case Mg:
                case Mgsd:
                case Mgfrsd:
                    CurrentParameterSetter.SetMg(prm1, records, lp, ratedCurrent2Table, inputFlag);
                    break;

                case Wh:
                    CurrentParameterSetter.SetWh(prm1, records, lp, ratedCurrent1Table, inputFlag);
                    break;

                // 【C原典 改訂<3>】seisakusiyou を追加で渡す。
                case Am:
                    CurrentParameterSetter.SetAm(
                        prm1, records, lp, count, ratedCurrent1Table, inputFlag, seisakusiyou, zoneCode);
                    break;

                case Ct:
                    CurrentParameterSetter.SetCt(prm1, records, lp, count, ratedCurrent1Table, inputFlag);
                    break;

                case Tb:
                    CurrentParameterSetter.SetTb(prm1, records, lp, wireSizeTable, inputFlag);
                    break;

                // 【C原典】CON/ZCT。
                case Con:
                case Zct:
                    CurrentParameterSetter.SetCon(records, lp, inputFlag);
                    break;

                case Tr:
                    CurrentParameterSetter.SetTr(records, lp, inputFlag);
                    break;

                case Elr:
                    CurrentParameterSetter.SetElr(records, lp, inputFlag);
                    break;

                case Lgr:
                    CurrentParameterSetter.SetLgr(records, lp, inputFlag);
                    break;

                case Rry:
                    CurrentParameterSetter.SetRry(records, lp, inputFlag);
                    break;

                case Mcdt:
                    CurrentParameterSetter.SetMcdt(records, lp, inputFlag);
                    break;

                case F:
                    CurrentParameterSetter.SetF(records, lp, inputFlag);
                    break;

                case Ts:
                    CurrentParameterSetter.SetTs(prm1, records, lp, inputFlag);
                    break;

                // 【C原典】Fyss3G_Set_DCPW は空関数のため no-op。
                case Dcpw:
                    break;

                case Ssw:
                    CurrentParameterSetter.SetSsw(records, lp, inputFlag);
                    break;

                // 【C原典】Fyss3G_Set_CKS( prm1, ... )。定義側の第 1 引数名は prm2 だが実引数は prm1。
                case Cks:
                    CurrentParameterSetter.SetCks(prm1, records, lp, inputFlag);
                    break;

                case L:
                    CurrentParameterSetter.SetL(records, lp, inputFlag);
                    break;

                // 【C原典】TSU/SSWU は Set_TS を呼ぶ。
                case Tsu:
                case Sswu:
                    CurrentParameterSetter.SetTs(prm1, records, lp, inputFlag);
                    break;

                // 【C原典】PBSU/COSU/2COSU/OLU は Set_SU を呼ぶ。
                case Pbsu:
                case Cosu:
                case TwoCosu:
                case Olu:
                    CurrentParameterSetter.SetSu(prm1, records, lp, inputFlag);
                    break;

                default:
                    break;
            }
        }
    }
}
