namespace Ews.Analysis;

using Ews.Domain.Analysis;
using Ews.Domain.Masters;

/// <summary>
/// 機械連動子(MI)の構成機器エリア(FYRT804 配列)を作成する。
/// 【C原典】Fysk01_Make_Koukiki_MI(toku/sekkei/src/Fysk01.c:3880, 改訂&lt;35&gt;)。
///
/// 機器マスタ(FYDM805)から構成機器レコード(FYRT804)を組み立て、キー部
/// (FYRT804KEY=機器発生区分+データ追番+制御回路仕様名称追番+生成追番=10 バイト)の
/// 昇順を保つよう割り込み挿入し、挿入後の件数を返す。挿入は
/// <see cref="ComponentBufferInserter"/>(=Fysk01_Mem_Control)を用いる。
///
/// <see cref="HeatResistantBoxComponentBuilder"/>(=Make_Koukiki_TainetuBox)と同型だが、
/// データ追番/生成追番は "999"、制御回路仕様名称追番・行種・扉取付区分は memset の空白のまま。
/// 末尾の Make_Koukiki('S') カウンタ同期も無く、純粋に挿入後件数を返す。
/// </summary>
public static class MechanicalInterlockComponentBuilder
{
    /// <summary>機器発生区分。【C原典】wk.key.kkhkbn = '4'。</summary>
    public const char EquipmentOccurrenceKind = '4';

    /// <summary>データ追番。【C原典】wk.key.datano = "999"。</summary>
    public const string DataNumber = "999";

    /// <summary>生成追番。【C原典】wk.key.seino = "999"。</summary>
    public const string GenerationNumber = "999";

    /// <summary>手配数量。【C原典】wk.dt.epaqty = '1'。</summary>
    public const char OrderQuantity = '1';

    /// <summary>生産管理データ転送対象区分。【C原典】wk.dt.btnkubn = 'Y'。</summary>
    public const char ProductionTransferKind = 'Y';

    /// <summary>
    /// 機器マスタ(FYDM805)から機械連動子(MI)の構成機器レコード(FYRT804)を組み立てる。
    /// 【C原典】Fysk01_Make_Koukiki_MI の wk 構築部。
    /// </summary>
    public static ComponentEquipment Build(EquipmentMaster kk)
    {
        ArgumentNullException.ThrowIfNull(kk);
        return new ComponentEquipment
        {
            EquipmentOccurrenceKind = EquipmentOccurrenceKind,
            DataNumber = DataNumber,
            ControlSpecNumber = string.Empty, // cnameno は未設定(memset ' ')
            GenerationNumber = GenerationNumber,
            LineType = string.Empty,          // gyo は未設定(memset ' ')
            MachineKey = new MachineMasterKey
            {
                ReservedWord = kk.ReservedWord,
                MakerCode = kk.MakerCode,
                ParameterTypes = SplitParameterTypes(kk.ParameterType),
                RatingKey = kk.RatingKey,
            },
            ElectricalParameterString = kk.ElectricalParameters,
            PartName = kk.PartName,
            SearchResultCode = string.Empty, // memset(ksrhkcd, ' ')
            OrderQuantity = OrderQuantity,
            ProductionTransferKind = ProductionTransferKind,
            RatedCapacityAcVa = kk.RatedCapacityAcVa, // memcpy(&wk.hojg, &kk->hojg)
            RatedCapacityDcW = kk.RatedCapacityDcW,
        };
    }

    /// <summary>
    /// 構成機器エリアへ MI 機器を 1 件、キー昇順を保って割り込み挿入し、挿入後の件数を返す。
    /// 【C原典】Fysk01_Make_Koukiki_MI の重複ﾁｪｯｸ/挿入部(return nn)。
    /// </summary>
    public static int Append(IList<ComponentEquipment> components, EquipmentMaster kk)
    {
        ArgumentNullException.ThrowIfNull(components);
        ComponentEquipment wk = Build(kk);
        int nn = components.Count;

        if (nn == 0)
        {
            components.Add(wk);
            return 1;
        }

        string wkKey = wk.ComponentKey;
        for (int i = 0; i < nn; i++)
        {
            if (string.CompareOrdinal(wkKey, components[i].ComponentKey) < 0)
            {
                ComponentBufferInserter.Insert(components, wk, i, nn);
                return nn + 1;
            }
        }

        ComponentBufferInserter.Insert(components, wk, nn, nn);
        return nn + 1;
    }

    /// <summary>
    /// 機器マスタのパラメータタイプ(49 桁)を 7 桁×7 面に分割する。
    /// 【C原典】memcpy(wk.dt.km_key.ptype[0], &amp;kk-&gt;pkey.ptype, sizeof(ptype))。
    /// </summary>
    private static string[] SplitParameterTypes(string parameterType)
    {
        string padded = (parameterType ?? string.Empty).PadRight(49)[..49];
        var result = new string[7];
        for (int i = 0; i < 7; i++)
        {
            result[i] = padded.Substring(i * 7, 7).TrimEnd();
        }
        return result;
    }
}
