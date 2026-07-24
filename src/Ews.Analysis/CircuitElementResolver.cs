using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路パラメータ(<see cref="MainCircuitParameter"/> = struct MCPRMS)の
/// 相・線式・極数からエレメント数/極数を決定する決定的テーブル照合ユーティリティ。
///
/// 【C原典】toku/sekkei/src/Fyss14.c
///   - Element_Gen (Fyss14.c:1682) 回路情報(相/線式/極)よりエレメント情報を得る
///   - Pole_Gen    (Fyss14.c:2054) 相・線式情報より極情報を得て設定する
///
/// いずれも外部データ(ISAM/マスタ)に依存しない静的テーブル照合であり、
/// C 原典の memcmp による先頭一致・番兵({0,...} 終端)判定をそのまま再現する。
/// </summary>
public static class CircuitElementResolver
{
    /// <summary>
    /// 相・線式・極数からエレメント数を得る。【C原典】Fyss14.c:1682 Element_Gen。
    /// 相(ph)・線式(wr)・極数(p)の 3 要素をテーブルと完全一致照合し、対応する
    /// エレメント数を返す。一致しなければ -1(異常終了)を返す。
    /// </summary>
    /// <param name="prm">相・線式・極数を持つ主回路パラメータ。</param>
    /// <returns>エレメント数。一致しない場合は -1。</returns>
    public static short ResolveElement(MainCircuitParameter prm)
    {
        ArgumentNullException.ThrowIfNull(prm);

        // 【C原典】static prmtable[] = {{{3,4,4},3},{{3,3,3},3},{{1,3,3},2},
        //   {{1,2,2},2},{{1,2,1},1},{{0,0,0},0}}; の (ph,wr,p) 完全一致照合。
        foreach ((short ph, short wr, short p, short result) in ElementTable)
        {
            if (prm.Phase == ph && prm.WireType == wr && prm.Pole == p)
            {
                return result;
            }
        }

        // 【C原典】終端({0,0,0})に到達 = 該当なし → -1。
        return -1;
    }

    /// <summary>
    /// 相・線式から極数を得て <see cref="MainCircuitParameter.Pole"/> へ設定する。
    /// 【C原典】Fyss14.c:2054 Pole_Gen。相(ph)・線式(wr)の 2 要素をテーブルと完全一致
    /// 照合し、一致すれば極数を <paramref name="prm"/> の Pole へ書き込み 0 を返す。
    /// 一致しなければ Pole は変更せず -1(異常終了)を返す。
    /// </summary>
    /// <param name="prm">相・線式を持つ主回路パラメータ(極数を破壊的に更新)。</param>
    /// <returns>正常終了は 0、該当なしは -1。</returns>
    public static int ResolvePole(MainCircuitParameter prm)
    {
        ArgumentNullException.ThrowIfNull(prm);

        // 【C原典】static prmtable[] = {{{3,4},4},{{3,3},3},{{1,3},3},{{1,2},2},
        //   {{0,0},0}}; の (ph,wr) 完全一致照合。一致時 pprmp->p=result。
        foreach ((short ph, short wr, short result) in PoleTable)
        {
            if (prm.Phase == ph && prm.WireType == wr)
            {
                prm.Pole = result;
                return 0;
            }
        }

        // 【C原典】終端({0,0})に到達 = 該当なし → -1(Pole は不変)。
        return -1;
    }

    /// <summary>【C原典】Element_Gen の prmtable(相,線式,極 → エレメント数)。</summary>
    private static readonly (short Phase, short WireType, short Pole, short Result)[] ElementTable =
    [
        (3, 4, 4, 3),
        (3, 3, 3, 3),
        (1, 3, 3, 2),
        (1, 2, 2, 2),
        (1, 2, 1, 1),
    ];

    /// <summary>【C原典】Pole_Gen の prmtable(相,線式 → 極数)。</summary>
    private static readonly (short Phase, short WireType, short Result)[] PoleTable =
    [
        (3, 4, 4),
        (3, 3, 3),
        (1, 3, 3),
        (1, 2, 2),
    ];
}
