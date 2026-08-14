namespace Ews.Tests;

using Ews.Analysis;
using Xunit;

/// <summary>
/// <see cref="NearestRankDataComparer"/>(=Fysk01_Data_Cmp)の移植テスト。
/// </summary>
public sealed class NearestRankDataComparerTests
{
    [Fact]
    public void MG今回値が前回値より小さいなら入れ替える1を返す()
    {
        int k = NearestRankDataComparer.Compare(NearestRankDataComparer.MgKind,
            5.0, 0.0, 10.0, 5.0, "A", 0.0, 10.0, 10.0, "A");
        Assert.Equal(1, k);
    }

    [Fact]
    public void MG今回値が前回値より大きく差がTOL以上なら0を返す()
    {
        int k = NearestRankDataComparer.Compare(NearestRankDataComparer.MgKind,
            5.0, 0.0, 10.0, 10.0, "A", 0.0, 10.0, 5.0, "A");
        Assert.Equal(0, k);
    }

    [Fact]
    public void MG値がほぼ同じで幅一致し定格値が小さいなら1を返す()
    {
        int k = NearestRankDataComparer.Compare(NearestRankDataComparer.MgKind,
            5.0, 0.0, 10.0, 5.0, "A", 0.0, 10.0, 5.0, "B");
        Assert.Equal(1, k);
    }

    [Fact]
    public void MG値がほぼ同じで幅一致し定格値同一かつ中央寄りなら1を返す()
    {
        // 今回幅[0,10]は基準5で完全中央、前回幅[0.0003,10]はわずかに偏り(幅差はTOL未満)
        int k = NearestRankDataComparer.Compare(NearestRankDataComparer.MgKind,
            5.0, 0.0, 10.0, 5.0, "A", 0.0003, 10.0, 5.0, "A");
        Assert.Equal(1, k);
    }

    [Fact]
    public void MG値がほぼ同じで幅一致し定格値同一かつ中央寄りでないなら0を返す()
    {
        int k = NearestRankDataComparer.Compare(NearestRankDataComparer.MgKind,
            5.0, 0.0, 10.0, 5.0, "A", 0.0, 10.0, 5.0, "A");
        Assert.Equal(0, k);
    }

    [Fact]
    public void MG値がほぼ同じでも幅不一致なら0を返す()
    {
        int k = NearestRankDataComparer.Compare(NearestRankDataComparer.MgKind,
            5.0, 0.0, 10.0, 5.0, "A", 0.0, 20.0, 5.0, "A");
        Assert.Equal(0, k);
    }

    [Fact]
    public void THR幅一致し定格値が小さいなら1を返す()
    {
        int k = NearestRankDataComparer.Compare(NearestRankDataComparer.ThrKind,
            5.0, 0.0, 10.0, 0.0, "A", 0.0, 10.0, 0.0, "B");
        Assert.Equal(1, k);
    }

    [Fact]
    public void THR幅一致し定格値同一かつ中央寄りなら1を返す()
    {
        int k = NearestRankDataComparer.Compare(NearestRankDataComparer.ThrKind,
            5.0, 0.0, 10.0, 0.0, "A", 0.0003, 10.0, 0.0, "A");
        Assert.Equal(1, k);
    }

    [Fact]
    public void THR幅不一致なら0を返す()
    {
        int k = NearestRankDataComparer.Compare(NearestRankDataComparer.ThrKind,
            5.0, 0.0, 10.0, 0.0, "A", 0.0, 20.0, 0.0, "B");
        Assert.Equal(0, k);
    }

    [Fact]
    public void 予約語区分がTHRでもMGでもないなら0を返す()
    {
        int k = NearestRankDataComparer.Compare(12,
            5.0, 0.0, 10.0, 5.0, "A", 0.0, 10.0, 5.0, "B");
        Assert.Equal(0, k);
    }
}
