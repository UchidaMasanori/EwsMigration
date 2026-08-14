namespace Ews.Analysis;

using Ews.Domain.Analysis;
using Ews.Domain.Masters;

/// <summary>
/// INV オプション機器の構成機器エリア(FYRT804 配列)を作成する。
/// 【C原典】Fysk01_Make_Koukiki_INV_OP(toku/sekkei/src/Fysk01.c:6119, 改訂&lt;28&gt;)。
///
/// <see cref="HeatResistantBoxComponentBuilder"/>/<see cref="MechanicalInterlockComponentBuilder"/> と
/// 同型の FYRT804 構築 + キー昇順ソート挿入(<see cref="ComponentBufferInserter"/>=Fysk01_Mem_Control)だが、
/// キー部(発生区分/データ追番/仕様名称追番/生成追番/行種)は引数、機器マスタキーの予約語・メーカー
/// コード・パラメータタイプは直近上下位該当データ(<see cref="NearestRankReference"/>=ck)、定格キー・
/// 電気パラメータ・品名・補助情報は機器マスタ(<see cref="EquipmentMaster"/>=kk)から採る点が異なる。
///
/// 手配数量はラインノイズフィルタ(<see cref="InverterOptionState.Current"/> == 3)の場合のみ負荷容量
/// (fparmg.fpalw2)から算出し、それ以外は '1' 固定。
/// </summary>
public static class InverterOptionComponentBuilder
{
    /// <summary>生産管理データ転送対象区分。【C原典】wk.dt.btnkubn = 'Y'。</summary>
    public const char ProductionTransferKind = 'Y';

    /// <summary>扉取付区分。【C原典】wk.dt.tikbn = 'I'(中)。</summary>
    public const char DoorMountKind = 'I';

    /// <summary>手配数量を 4 個にする負荷容量(kW)の下限しきい値。【C原典】if(inputKW &gt; 15.0)。</summary>
    public const double MultipleQuantityThresholdKw = 15.0;

    /// <summary>負荷容量が大きい場合の手配数量。【C原典】wk.dt.epaqty = '4'。</summary>
    public const char MultipleOrderQuantity = '4';

    /// <summary>通常時の手配数量。【C原典】wk.dt.epaqty = '1'。</summary>
    public const char SingleOrderQuantity = '1';

    private const int ParameterTypeSlotCount = 7;

    /// <summary>
    /// INV オプション機器の構成機器レコード(FYRT804)を組み立てる。
    /// 【C原典】Fysk01_Make_Koukiki_INV_OP の wk 構築部。
    /// </summary>
    /// <param name="equipmentOccurrenceKind">機器発生区分。【C原典】kikih。</param>
    /// <param name="dataNumber">データ追番。【C原典】dono。</param>
    /// <param name="controlSpecNumber">制御回路仕様名称追番。【C原典】smno。</param>
    /// <param name="generationNumber">生成追番。【C原典】seno。</param>
    /// <param name="lineType">行種。【C原典】gs。</param>
    /// <param name="reference">直近上下位該当データ。【C原典】struct FYDF812 *ck。</param>
    /// <param name="master">機器マスタ。【C原典】struct FYDM805 *kk。</param>
    /// <param name="powerSystemNumber">電源系統番号。【C原典】kno。</param>
    /// <param name="loadCapacityRaw">負荷容量(fparmg.fpalw2, 7 桁)。【C原典】fp-&gt;fpalw2。</param>
    public static ComponentEquipment Build(
        char equipmentOccurrenceKind,
        string dataNumber,
        string controlSpecNumber,
        string generationNumber,
        string lineType,
        NearestRankReference reference,
        EquipmentMaster master,
        string powerSystemNumber,
        string loadCapacityRaw)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(master);

        return new ComponentEquipment
        {
            EquipmentOccurrenceKind = equipmentOccurrenceKind,
            DataNumber = dataNumber,
            ControlSpecNumber = controlSpecNumber,
            GenerationNumber = generationNumber,
            LineType = lineType,
            MachineKey = new MachineMasterKey
            {
                ReservedWord = reference.ReservedWord,                 // ck->key.yoyaku
                MakerCode = reference.MakerCode,                       // ck->key.mkcd
                ParameterTypes = TrimParameterTypes(reference.ParameterTypes), // &ck->key.tjg
                RatingKey = master.RatingKey,                         // kk->pkey.teikkey
            },
            ElectricalParameterString = master.ElectricalParameters,  // kk->pstring
            PartName = master.PartName,                               // kk->hinmei
            SearchResultCode = string.Empty,                          // memset(ksrhkcd, ' ')
            OrderQuantity = ResolveOrderQuantity(loadCapacityRaw),
            ProductionTransferKind = ProductionTransferKind,          // btnkubn = 'Y'
            PowerSystemNumber = powerSystemNumber,                    // dt.kno
            DoorMountKind = DoorMountKind,                            // tikbn = 'I'
            RatedCapacityAcVa = master.RatedCapacityAcVa,             // memcpy(&wk.hojg, &kk->hojg)
            RatedCapacityDcW = master.RatedCapacityDcW,
        };
    }

    /// <summary>
    /// 構成機器エリアへ INV オプション機器を 1 件、キー昇順を保って割り込み挿入し、挿入後の件数を返す。
    /// 【C原典】Fysk01_Make_Koukiki_INV_OP の重複ﾁｪｯｸ/挿入部(return nn)。
    /// </summary>
    public static int Append(
        IList<ComponentEquipment> components,
        char equipmentOccurrenceKind,
        string dataNumber,
        string controlSpecNumber,
        string generationNumber,
        string lineType,
        NearestRankReference reference,
        EquipmentMaster master,
        string powerSystemNumber,
        string loadCapacityRaw)
    {
        ArgumentNullException.ThrowIfNull(components);
        ComponentEquipment wk = Build(
            equipmentOccurrenceKind, dataNumber, controlSpecNumber, generationNumber,
            lineType, reference, master, powerSystemNumber, loadCapacityRaw);
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
    /// 手配数量を求める。【C原典】inv_opno==3 のとき負荷容量(fpalw2/10/100.0)が 15.0kW 超で '4'、
    /// それ以外は '1'。inv_opno!=3 は常に '1'。
    /// </summary>
    private static char ResolveOrderQuantity(string loadCapacityRaw)
    {
        if (InverterOptionState.Current != InverterOptionState.LineNoiseFilter)
        {
            return SingleOrderQuantity;
        }

        int fpalw2 = ParseLoadCapacity(loadCapacityRaw); // LibCharToInt(fp->fpalw2, 7)
        double inputKW = (fpalw2 / 10) / 100.0;          // sprintf("%05d", fpalw2/10) → atoi → /100.0
        return inputKW > MultipleQuantityThresholdKw ? MultipleOrderQuantity : SingleOrderQuantity;
    }

    /// <summary>負荷容量フィールドを整数化する。【C原典】LibCharToInt。空白のみは 0。</summary>
    private static int ParseLoadCapacity(string loadCapacityRaw)
    {
        string trimmed = (loadCapacityRaw ?? string.Empty).Trim();
        return int.TryParse(trimmed, out int value) ? value : 0;
    }

    /// <summary>直近上下位データのパラメータタイプ(7 枠)を末尾空白を除いて写す。</summary>
    private static string[] TrimParameterTypes(IReadOnlyList<string> parameterTypes)
    {
        var result = new string[ParameterTypeSlotCount];
        for (int i = 0; i < ParameterTypeSlotCount; i++)
        {
            string value = parameterTypes is not null && i < parameterTypes.Count
                ? parameterTypes[i] ?? string.Empty
                : string.Empty;
            result[i] = value.TrimEnd();
        }
        return result;
    }
}
