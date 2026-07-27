using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 機器選定の候補比較(<see cref="EquipmentSelector"/>)の検証。
/// 【C原典】toku/sekkei/src/Fysk01.c Fysk01_Choki_Cmp1 / Fysk01_Choki_Cmp2。
/// いずれもマスタ・記録列非依存の純粋数値関数で、複数候補から「よりよい候補」を選ぶ判定に使う。
/// 定数(fyrt808.h): GOOD=0 / SYS_ERR=-1。
/// </summary>
public sealed class EquipmentSelectorTests
{
    // ── Fysk01_Choki_Cmp1(THR/MG/XERY): 正規化位置の偏り差で判定 ──

    [Fact]
    public void ChokiCmp1_今回幅がより中央に収まるなら入れ替え1を返す()
    {
        // sentchi=50。今回[0,100]は中央(偏り0)、前回[0,200]は下端寄り(偏り0.5)。今回が良好=1。
        short ret = EquipmentSelector.ChokiCmp1(50.0, [0.0, 100.0], [0.0, 200.0]);
        Assert.Equal(1, ret);
    }

    [Fact]
    public void ChokiCmp1_前回幅がより中央なら入れ替えず0を返す()
    {
        // sentchi=50。今回[0,200]は下端寄り、前回[0,100]は中央。今回は劣る=0。
        short ret = EquipmentSelector.ChokiCmp1(50.0, [0.0, 200.0], [0.0, 100.0]);
        Assert.Equal(0, ret);
    }

    [Fact]
    public void ChokiCmp1_偏りが等しいなら0を返す()
    {
        // 両幅とも sentchi が中央。wk3==wk4 のため wk3<wk4 は偽=0。
        short ret = EquipmentSelector.ChokiCmp1(50.0, [0.0, 100.0], [25.0, 75.0]);
        Assert.Equal(0, ret);
    }

    [Theory]
    [InlineData(50.0, 100.0, 100.0)]    // dt1[0] == dt1[1]
    [InlineData(50.0, 60.0, 40.0)]      // dt1[0] > dt1[1]
    public void ChokiCmp1_今回幅が不正なら_SYS_ERR_を返す(double s, double lo, double hi)
    {
        short ret = EquipmentSelector.ChokiCmp1(s, [lo, hi], [0.0, 100.0]);
        Assert.Equal(-1, ret);
    }

    [Theory]
    [InlineData(-1.0)]   // 下端未満
    [InlineData(101.0)]  // 上端超過
    public void ChokiCmp1_基準値が今回幅の範囲外なら_SYS_ERR_を返す(double sentchi)
    {
        short ret = EquipmentSelector.ChokiCmp1(sentchi, [0.0, 100.0], [0.0, 100.0]);
        Assert.Equal(-1, ret);
    }

    [Fact]
    public void ChokiCmp1_基準値が前回幅の範囲外なら_SYS_ERR_を返す()
    {
        // sentchi=50 は今回[0,100]内だが前回[60,100]の範囲外。
        short ret = EquipmentSelector.ChokiCmp1(50.0, [0.0, 100.0], [60.0, 100.0]);
        Assert.Equal(-1, ret);
    }

    // ── Fysk01_Choki_Cmp2(THSW/TM): 中点との距離で判定 ──

    [Fact]
    public void ChokiCmp2_今回幅の中点がより近いなら入れ替え1を返す()
    {
        // sentchi=50。今回[0,100]中点50(距離0)、前回[0,200]中点100(距離50)。今回が近い=1。
        short ret = EquipmentSelector.ChokiCmp2(50.0, [0.0, 100.0], [0.0, 200.0]);
        Assert.Equal(1, ret);
    }

    [Fact]
    public void ChokiCmp2_前回幅の中点がより近いなら入れ替えず0を返す()
    {
        // sentchi=50。今回[0,200]中点100(距離50)、前回[0,100]中点50(距離0)。今回は遠い=0。
        short ret = EquipmentSelector.ChokiCmp2(50.0, [0.0, 200.0], [0.0, 100.0]);
        Assert.Equal(0, ret);
    }

    [Fact]
    public void ChokiCmp2_中点距離が等しいなら0を返す()
    {
        // 両中点が sentchi から等距離。wk2==wk4 のため wk2<wk4 は偽=0。
        short ret = EquipmentSelector.ChokiCmp2(50.0, [0.0, 100.0], [20.0, 80.0]);
        Assert.Equal(0, ret);
    }

    [Theory]
    [InlineData(50.0, 50.0, 50.0)]   // dt1[0] >= dt1[1]
    [InlineData(50.0, 80.0, 60.0)]   // dt1[0] > dt1[1]
    public void ChokiCmp2_今回幅が不正なら_SYS_ERR_を返す(double s, double lo, double hi)
    {
        short ret = EquipmentSelector.ChokiCmp2(s, [lo, hi], [0.0, 100.0]);
        Assert.Equal(-1, ret);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(101.0)]
    public void ChokiCmp2_基準値が今回幅の範囲外なら_SYS_ERR_を返す(double sentchi)
    {
        short ret = EquipmentSelector.ChokiCmp2(sentchi, [0.0, 100.0], [0.0, 100.0]);
        Assert.Equal(-1, ret);
    }

    [Fact]
    public void ChokiCmp2_基準値が前回幅の範囲外なら_SYS_ERR_を返す()
    {
        short ret = EquipmentSelector.ChokiCmp2(50.0, [0.0, 100.0], [60.0, 100.0]);
        Assert.Equal(-1, ret);
    }

    // ── Fysk01_Data_Cmp(THR/MG): 候補選択 ──

    [Fact]
    public void DataCmp_MGは代表値が小さい今回候補で入れ替え1を返す()
    {
        // MG(PC_3): dat1v(50) < dat2v(60) → 即 k=1。
        short ret = EquipmentSelector.DataCmp(
            EquipmentSelector.Pc3Mg, 50.0, [0.0, 100.0], 50.0, "A",
            [0.0, 100.0], 60.0, "B");
        Assert.Equal(1, ret);
    }

    [Fact]
    public void DataCmp_MGは代表値が大きい今回候補では入れ替えず0を返す()
    {
        // MG: dat1v(60) > dat2v(50)、差>TOL → k=0。
        short ret = EquipmentSelector.DataCmp(
            EquipmentSelector.Pc3Mg, 50.0, [0.0, 100.0], 60.0, "A",
            [0.0, 100.0], 50.0, "B");
        Assert.Equal(0, ret);
    }

    [Fact]
    public void DataCmp_MGは代表値同値かつ幅一致で定格値キーが小さければ1を返す()
    {
        // dat1v==dat2v、幅一致、kteichi "A" < "B" → k=1。
        short ret = EquipmentSelector.DataCmp(
            EquipmentSelector.Pc3Mg, 50.0, [0.0, 100.0], 50.0, "A",
            [0.0, 100.0], 50.0, "B");
        Assert.Equal(1, ret);
    }

    [Fact]
    public void DataCmp_MGは代表値同値でも幅不一致なら0を返す()
    {
        // 幅が異なる(|dat1a[1]-dat2a[1]|=100 > TOL) → 幅一致条件を満たさず k=0。
        short ret = EquipmentSelector.DataCmp(
            EquipmentSelector.Pc3Mg, 50.0, [0.0, 100.0], 50.0, "B",
            [0.0, 200.0], 50.0, "A");
        Assert.Equal(0, ret);
    }

    [Fact]
    public void DataCmp_MGは代表値同値_幅一致_キー大_均等度同点なら0を返す()
    {
        // 幅一致([0,100]==[0,100])、kteichi "B">"A" → Cmp1。両幅同一で偏り等しく Cmp1=0 → k=0。
        short ret = EquipmentSelector.DataCmp(
            EquipmentSelector.Pc3Mg, 50.0, [0.0, 100.0], 50.0, "B",
            [0.0, 100.0], 50.0, "A");
        Assert.Equal(0, ret);
    }

    [Fact]
    public void DataCmp_THRは幅一致でキーが小さければ1を返す()
    {
        // THR(PC_1): 代表値比較なし。幅一致、kteichi "A"<"B" → k=1。
        short ret = EquipmentSelector.DataCmp(
            EquipmentSelector.Pc1Thr, 50.0, [0.0, 100.0], 999.0, "A",
            [0.0, 100.0], 111.0, "B");
        Assert.Equal(1, ret);
    }

    [Fact]
    public void DataCmp_THRは幅不一致なら0を返す()
    {
        // 幅が異なる([0,100] vs [0,200]) → 幅一致条件を満たさず k=0。
        short ret = EquipmentSelector.DataCmp(
            EquipmentSelector.Pc1Thr, 50.0, [0.0, 100.0], 0.0, "A",
            [0.0, 200.0], 0.0, "B");
        Assert.Equal(0, ret);
    }

    [Fact]
    public void DataCmp_対象外の処理番号は常に0を返す()
    {
        short ret = EquipmentSelector.DataCmp(
            99, 50.0, [0.0, 100.0], 50.0, "A",
            [0.0, 100.0], 60.0, "B");
        Assert.Equal(0, ret);
    }
}
