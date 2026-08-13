namespace Ews.Analysis;

using Ews.Domain.Analysis;
using Ews.Domain.Masters;

/// <summary>
/// 耐熱盤BOX の構成機器エリア(FYRT804 配列)を作成する。
/// 【C原典】Fysk01_Make_Koukiki_TainetuBox(toku/sekkei/src/Fysk01.c:6881, 改訂&lt;31/32&gt;)。
///
/// 機器マスタ(FYDM805)から構成機器レコード(FYRT804)を組み立て、キー部
/// (FYRT804KEY=機器発生区分+データ追番+制御回路仕様名称追番+生成追番=10 バイト)の
/// 昇順を保つよう割り込み挿入し、挿入後の件数を返す。
/// 挿入は <see cref="ComponentBufferInserter"/>(=Fysk01_Mem_Control)を用いる。
///
/// C原典末尾の <c>*num = Fysk01_Make_Koukiki('S', dono, ...)</c> は 'S' モードで
/// 静的カウンタ nn を <c>LibCharToShort(dono)</c>(=挿入後件数)に同期させ返すだけのため、
/// 移植では挿入後件数の返却で等価となる。
/// </summary>
public static class HeatResistantBoxComponentBuilder
{
    /// <summary>機器発生区分。【C原典】wk.key.kkhkbn = '4'。</summary>
    public const char EquipmentOccurrenceKind = '4';

    /// <summary>行種。【C原典】wk.dt.gyo = "B    "(5 桁)。</summary>
    public const string LineType = "B    ";

    /// <summary>手配数量。【C原典】wk.dt.epaqty = '1'。</summary>
    public const char OrderQuantity = '1';

    /// <summary>生産管理データ転送対象区分。【C原典】wk.dt.btnkubn = 'Y'。</summary>
    public const char ProductionTransferKind = 'Y';

    /// <summary>扉取付区分。【C原典】wk.dt.tikbn = 'I'(中)。</summary>
    public const char DoorMountKind = 'I';

    /// <summary>
    /// 機器マスタ(FYDM805)から耐熱盤BOX の構成機器レコード(FYRT804)を組み立てる。
    /// 【C原典】Fysk01_Make_Koukiki_TainetuBox の wk 構築部(fixed リテラル + kk からの写像)。
    /// </summary>
    public static ComponentEquipment Build(EquipmentMaster kk)
    {
        ArgumentNullException.ThrowIfNull(kk);
        return new ComponentEquipment
        {
            EquipmentOccurrenceKind = EquipmentOccurrenceKind,
            DataNumber = "000",       // wk.key.datano = "000"
            ControlSpecNumber = "000", // wk.key.cnameno = "000"
            GenerationNumber = "000",  // wk.key.seino  = "000"
            LineType = LineType,
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
            DoorMountKind = DoorMountKind,
            RatedCapacityAcVa = kk.RatedCapacityAcVa, // memcpy(&wk.hojg, &kk->hojg)
            RatedCapacityDcW = kk.RatedCapacityDcW,
        };
    }

    /// <summary>
    /// 構成機器エリアへ耐熱盤BOX 機器を 1 件、キー昇順を保って割り込み挿入し、挿入後の件数を返す。
    /// 【C原典】Fysk01_Make_Koukiki_TainetuBox の重複ﾁｪｯｸ/挿入部と 'S' カウンタ同期(*num)。
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
    /// 【C原典】memcpy(wk.dt.km_key.ptype[0], &amp;kk-&gt;pkey.ptype[0], sizeof(ptype))。
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
