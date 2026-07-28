using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 指示されたデータ追番の下流となるデータ追番の一覧を取得する。
/// 【C原典】<c>Fyss35_Select_Karyu_Sub</c>(toku/sekkei/src/Fyss35.c:69)。
///
/// 主回路エリアは親→子(下流)が連続して並んでおり、指定機器(<paramref name="designationNumber"/>=sijino)の
/// 直後から親データ追番(oyatno)が指定機器のそれより大きいレコードが下流機器として連なる。
/// 指定機器の oyatno 以下(＝兄弟・上位)のレコードが現れた時点で打ち切る。
/// </summary>
public static class DownstreamSelector
{
    /// <summary>データ追番(datano)フィールド幅。【C原典】sizeof(datano[3])。</summary>
    private const int SequenceWidth = 3;

    /// <summary>親データ追番(oyatno)フィールド幅。【C原典】sizeof(oyatno[3])。</summary>
    private const int ParentWidth = 3;

    /// <summary>
    /// 指定データ追番の下流データ追番一覧を返す。【C原典】<c>Fyss35_Select_Karyu_Sub(Pmainc, maina, sijino, kensu, selectno)</c>。
    /// </summary>
    /// <param name="records">主回路エリア(FYRT800 配列相当)。【C原典】maina[](件数 Pmainc)。</param>
    /// <param name="designationNumber">下流を求める元となるデータ追番(1 始まり)。【C原典】sijino。</param>
    /// <returns>
    /// 下流データ追番(datano を整数化)のリスト(該当なしは空リスト)。
    /// 指定機器が系統種別 '1' 以外、または範囲外の場合は <c>null</c>(【C原典】戻り値 1 = siji not found)。
    /// </returns>
    public static IReadOnlyList<int>? SelectDownstream(IReadOnlyList<MainCircuitResult> records, int designationNumber)
    {
        ArgumentNullException.ThrowIfNull(records);

        int count = records.Count;

        // 【C原典】maina[sijino-1].dt.ksyubetu != '1' || sijino > Pmainc → return 1。
        if (designationNumber < 1 || designationNumber > count)
        {
            return null;
        }

        MainCircuitData designation = records[designationNumber - 1].Data;
        if (designation.SystemKind != '1')
        {
            return null;
        }

        // 【C原典】ono = Stoi(maina[sijino-1].dt.oyatno, osz)。
        int parentNumber = EquipmentParameterFormatter.Stoi(designation.ParentSequenceNumber, ParentWidth);

        var result = new List<int>();

        // 【C原典】for(j=sijino;j<Pmainc;j++) … 指定機器の直後から下流を走査。
        for (int j = designationNumber; j < count; j++)
        {
            int childParent = EquipmentParameterFormatter.Stoi(records[j].Data.ParentSequenceNumber, ParentWidth);
            if (parentNumber < childParent)
            {
                result.Add(EquipmentParameterFormatter.Stoi(records[j].SequenceNumber, SequenceWidth));
            }
            else
            {
                // 【C原典】else break … 兄弟・上位が現れた時点で打ち切る。
                break;
            }
        }

        return result;
    }
}
