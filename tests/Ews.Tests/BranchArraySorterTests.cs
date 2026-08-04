using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 分岐配列並べ替え(Fyss3C_Bunki_Sort)基盤ヘルパーのテスト。
/// </summary>
public class BranchArraySorterTests
{
    [Theory]
    [InlineData("MCB     ", BranchArraySorter.ReservedWordKind.MCB)]
    [InlineData("ELB     ", BranchArraySorter.ReservedWordKind.ELB)]
    [InlineData("2ERY    ", BranchArraySorter.ReservedWordKind.ERY2)]
    [InlineData("VVVF    ", BranchArraySorter.ReservedWordKind.VVVF)]
    [InlineData("P       ", BranchArraySorter.ReservedWordKind.P)]
    public void 予約語は識別子へ変換される(string yoyaku, BranchArraySorter.ReservedWordKind expected)
    {
        Assert.Equal(expected, BranchArraySorter.GetReservedWordKind(yoyaku));
    }

    [Fact]
    public void 短い予約語は8バイト右詰めで一致する()
    {
        Assert.Equal(BranchArraySorter.ReservedWordKind.MCB, BranchArraySorter.GetReservedWordKind("MCB"));
    }

    [Fact]
    public void 未知の予約語はNoneを返す()
    {
        Assert.Equal(BranchArraySorter.ReservedWordKind.None, BranchArraySorter.GetReservedWordKind("XXXX"));
    }

    [Fact]
    public void SetDecimalPointは末尾n桁の前に小数点を挿入する()
    {
        Assert.Equal("000.00", BranchArraySorter.SetDecimalPoint("00000", 2));
    }

    [Fact]
    public void SetDecimalPointはn以上の長さで先頭に0点を付す()
    {
        Assert.Equal("0.00000", BranchArraySorter.SetDecimalPoint("00000", 5));
    }

    [Fact]
    public void SetDecimalPointはn0以下で無変更()
    {
        Assert.Equal("00000", BranchArraySorter.SetDecimalPoint("00000", 0));
        Assert.Equal("00000", BranchArraySorter.SetDecimalPoint("00000", -1));
    }

    [Fact]
    public void FormatFixedWidthは幅3の0詰めにする()
    {
        Assert.Equal("007", BranchArraySorter.FormatFixedWidth(7, 3));
        Assert.Equal("123", BranchArraySorter.FormatFixedWidth(123, 3));
    }

    [Fact]
    public void FormatFixedWidthは超過時に先頭幅分を切り出す()
    {
        Assert.Equal("123", BranchArraySorter.FormatFixedWidth(12345, 3));
    }

    // --- 段階20: 作業モデル・収集関数 ---

    private static MainCircuitResult Node(
        string kaisono = "000", string chokuno = "000", string heino = "000",
        string joheino = "000", string oyatno = "000", string gyoglno = "000",
        char kiryoso = '1', string gyocd = "", char epabn = '0', string glheino = "000")
    {
        var r = new MainCircuitResult();
        r.Data.HierarchyNumber = kaisono;
        r.Data.SeriesNumber = chokuno;
        r.Data.ParallelNumber = heino;
        r.Data.UpperParallelNumber = joheino;
        r.Data.ParentSequenceNumber = oyatno;
        r.Data.LineTypeGroupNumber = gyoglno;
        r.Data.CircuitElement = kiryoso;
        r.Data.LineTypeCode = gyocd;
        r.Data.ElectricalParameterSlots[0].Bn = epabn;
        r.Data.GroupParallelNumber = glheino;
        return r;
    }

    [Fact]
    public void InitializeWorkAreaは数値変換しNewをNowの複製にする()
    {
        var mains = new[] { Node(kaisono: "002", heino: "005", kiryoso: '3') };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        Assert.Equal(2, sd[0].Now.HierarchyNumber);
        Assert.Equal(5, sd[0].Now.ParallelNumber);
        Assert.Equal(3, sd[0].Now.CircuitElement);
        Assert.Equal(BranchArraySorter.WorkStatus.NoDone, sd[0].Stat);
        Assert.Equal(sd[0].Now.ParallelNumber, sd[0].New.ParallelNumber);
        Assert.NotSame(sd[0].Now, sd[0].New);
    }

    [Fact]
    public void SetResultsはnodone以外の並列追番等を書き戻す()
    {
        var mains = new[] { Node(heino: "001"), Node(heino: "002") };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        sd[0].New.ParallelNumber = 7;
        sd[0].New.UpperParallelNumber = 4;
        sd[0].New.GroupParallelNumber = 2;
        sd[0].Stat = BranchArraySorter.WorkStatus.Done;
        sd[1].New.ParallelNumber = 9;
        sd[1].Stat = BranchArraySorter.WorkStatus.NoDone;

        BranchArraySorter.SetResults(mains, sd);

        Assert.Equal("007", mains[0].Data.ParallelNumber);
        Assert.Equal("004", mains[0].Data.UpperParallelNumber);
        Assert.Equal("002", mains[0].Data.GroupParallelNumber);
        Assert.Equal("002", mains[1].Data.ParallelNumber); // nodone は無変更
    }

    [Theory]
    [InlineData("B", true)]
    [InlineData("BO", true)]
    [InlineData("O", true)]
    [InlineData("SB", false)]
    [InlineData("", false)]
    public void IsMatchLineTypeCodeはB系のみ真(string gyocd, bool expected)
    {
        Assert.Equal(expected, BranchArraySorter.IsMatchLineTypeCode(Node(gyocd: gyocd)));
    }

    [Theory]
    [InlineData('1', true)]
    [InlineData('4', true)]
    [InlineData('0', false)]
    [InlineData('2', false)]
    public void IsMatchPanelKindは1と4のみ真(char epabn, bool expected)
    {
        Assert.Equal(expected, BranchArraySorter.IsMatchPanelKind(Node(epabn: epabn)));
    }

    [Fact]
    public void GetFloorTopElementsはdoing階層一致直列1のみ()
    {
        var mains = new[]
        {
            Node(kaisono: "001", chokuno: "001"),
            Node(kaisono: "001", chokuno: "002"),
            Node(kaisono: "002", chokuno: "001"),
        };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        foreach (var w in sd) { w.Stat = BranchArraySorter.WorkStatus.Doing; }

        Assert.Equal(new[] { 0 }, BranchArraySorter.GetFloorTopElements(sd, 1));
    }

    [Fact]
    public void GetFloorElementsOfSeriesは直後の連続直列要素を得る()
    {
        var mains = new[]
        {
            Node(joheino: "001", kaisono: "001", heino: "001", chokuno: "001"),
            Node(joheino: "001", kaisono: "001", heino: "001", chokuno: "002"),
            Node(joheino: "001", kaisono: "001", heino: "001", chokuno: "003"),
            Node(joheino: "001", kaisono: "001", heino: "002", chokuno: "001"),
        };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        foreach (var w in sd) { w.Stat = BranchArraySorter.WorkStatus.Doing; }

        Assert.Equal(new[] { 1, 2 }, BranchArraySorter.GetFloorElementsOfSeries(sd, 0));
    }

    [Fact]
    public void GetBrothersは同一親データ追番を集める()
    {
        var mains = new[]
        {
            Node(oyatno: "005"),
            Node(oyatno: "007"),
            Node(oyatno: "005"),
        };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        Assert.Equal(new[] { 0, 2 }, BranchArraySorter.GetBrothers(sd, 0));
    }

    [Fact]
    public void 最小最大階層番号はdoingのみ対象()
    {
        var mains = new[]
        {
            Node(kaisono: "002"),
            Node(kaisono: "005"),
            Node(kaisono: "001"),
        };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        sd[0].Stat = BranchArraySorter.WorkStatus.Doing;
        sd[1].Stat = BranchArraySorter.WorkStatus.Doing;
        sd[2].Stat = BranchArraySorter.WorkStatus.NoDone; // 対象外

        Assert.Equal(2, BranchArraySorter.GetMinimumHierarchyNumber(sd));
        Assert.Equal(5, BranchArraySorter.GetMaximumHierarchyNumber(sd));
    }

    private static BranchArraySorter.SortKey Key(
        int index = 0,
        int joheino = 0,
        char key0 = '0',
        char key1 = '0',
        char key2 = '0',
        string key3 = "000",
        string key4 = "00000000",
        string key5 = "00",
        string key6 = "000000000",
        string key7 = "000000000",
        char key8 = '0',
        char key9 = '0',
        int heino = 0)
        => new()
        {
            Index = index,
            UpperParallelNumber = joheino,
            Key0 = key0,
            Key1 = key1,
            Key2 = key2,
            Key3 = key3,
            Key4 = key4,
            Key5 = key5,
            Key6 = key6,
            Key7 = key7,
            Key8 = key8,
            Key9 = key9,
            ParallelNumber = heino,
        };

    [Fact]
    public void CompareSortIndexは上流並列追番を最優先で比較する()
    {
        Assert.True(BranchArraySorter.CompareSortIndex(Key(joheino: 1), Key(joheino: 2)) < 0);
        Assert.True(BranchArraySorter.CompareSortIndex(Key(joheino: 3), Key(joheino: 2)) > 0);
    }

    [Fact]
    public void CompareSortIndexは予約語種別を電圧より優先する()
    {
        // KEY5(予約語種別)が異なれば KEY4(電圧)を見ずに決まる。
        var a = Key(key5: "01", key4: "00000000");
        var b = Key(key5: "02", key4: "99999999");
        Assert.True(BranchArraySorter.CompareSortIndex(a, b) < 0);
    }

    [Fact]
    public void CompareSortIndexの電圧は逆順で大きいほど先()
    {
        // KEY4(電圧)は逆順(降順)。値が大きい方が先(負)になる。
        var high = Key(key4: "00200000");
        var low = Key(key4: "00100000");
        Assert.True(BranchArraySorter.CompareSortIndex(high, low) < 0);
    }

    [Fact]
    public void CompareSortIndexの極数は逆順で比較する()
    {
        // KEY3(極数)は逆順。大きい方が先(負)。
        Assert.True(BranchArraySorter.CompareSortIndex(Key(key3: "004"), Key(key3: "002")) < 0);
    }

    [Fact]
    public void CompareSortIndexのエレメント数は逆順で比較する()
    {
        // KEY8 は逆順(k2-k1)。大きい方が先(負)。
        Assert.True(BranchArraySorter.CompareSortIndex(Key(key8: '5'), Key(key8: '3')) < 0);
    }

    [Fact]
    public void CompareSortIndexは全キー一致時に並列追番過去値で決める()
    {
        Assert.True(BranchArraySorter.CompareSortIndex(Key(heino: 1), Key(heino: 2)) < 0);
        Assert.Equal(0, BranchArraySorter.CompareSortIndex(Key(heino: 4), Key(heino: 4)));
    }

    [Fact]
    public void GetMinimumParallelNumberは同一階層の最小heinoを返す()
    {
        var mains = new[]
        {
            Node(kaisono: "003", heino: "005"),
            Node(kaisono: "003", heino: "002"),
            Node(kaisono: "007", heino: "001"), // 別階層は対象外
        };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        var klist = new[] { Key(index: 0), Key(index: 1), Key(index: 2) };

        Assert.Equal(2, BranchArraySorter.GetMinimumParallelNumber(sd, klist, 3));
    }

    [Fact]
    public void GetMinimumParallelNumberは該当なしで0x7FFFを返す()
    {
        var mains = new[] { Node(kaisono: "001", heino: "001") };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        var klist = new[] { Key(index: 0) };

        Assert.Equal(0x7FFF, BranchArraySorter.GetMinimumParallelNumber(sd, klist, 9));
    }

    [Theory]
    [InlineData("ELB", "01")]
    [InlineData("ELMB", "02")]
    [InlineData("MCB", "03")]
    [InlineData("MMCB", "04")]
    [InlineData("RELB", "05")]
    [InlineData("RELMB", "06")]
    [InlineData("RMCB", "07")]
    [InlineData("RMMCB", "08")]
    [InlineData("SB", "09")]
    [InlineData("MC", "10")]
    [InlineData("RTR", "11")]
    [InlineData("RRY", "12")]
    [InlineData("THR", "13")]
    [InlineData("MG", "13")]
    public void GetReservedWordSortCategoryは予約語種別コードを返す(string yoyaku, string expected)
    {
        Assert.Equal(expected, BranchArraySorter.GetReservedWordSortCategory(yoyaku));
    }

    [Fact]
    public void GetReservedWordSortCategoryは末尾空白を無視する()
    {
        Assert.Equal("03", BranchArraySorter.GetReservedWordSortCategory("MCB     "));
    }

    [Theory]
    [InlineData("SB", '4')]     // 予約語 SB は即 '4'
    [InlineData("RMCB", '2')]   // 協約系は即 '2'
    [InlineData("RELMB", '2')]
    public void GetTypeKindは予約語で優先判定する(string yoyaku, char expected)
    {
        Assert.Equal(expected, BranchArraySorter.GetTypeKind(yoyaku, new[] { "", "", "", "", "", "", "" }));
    }

    [Theory]
    [InlineData("SB", '4')]
    [InlineData("KM", '2')]
    [InlineData("KY", '2')]
    [InlineData("CT", '3')]
    [InlineData("SEH", '4')]
    [InlineData("ZB", '4')]
    [InlineData("XX", '1')] // 未一致は '1'
    public void GetTypeKindはタイプ配列を走査して種別を返す(string type, char expected)
    {
        // 予約語は特別扱いされない語("MCB")にし、タイプ配列の先頭で判定させる。
        Assert.Equal(expected, BranchArraySorter.GetTypeKind("MCB", new[] { type, "", "", "", "", "", "" }));
    }

    [Fact]
    public void GetTypeKindは先頭一致を優先する()
    {
        // 先頭 KM('2') が CT('3') より前にあるので '2'。
        Assert.Equal('2', BranchArraySorter.GetTypeKind("MCB", new[] { "KM", "CT", "", "", "", "", "" }));
    }

    [Fact]
    public void SelectSortCurrentはスロット1が0ならスロット2を採る()
    {
        Assert.Equal("00000.999", BranchArraySorter.SelectSortCurrent("00000.000", "00000.999"));
    }

    [Fact]
    public void SelectSortCurrentはスロット1が非0ならスロット1を採る()
    {
        Assert.Equal("00100.000", BranchArraySorter.SelectSortCurrent("00100.000", "00000.999"));
    }

    [Fact]
    public void GetFloorElementsForSortは条件一致要素を集める()
    {
        var mains = new[]
        {
            Node(gyocd: "B", epabn: '1', chokuno: "001"),               // 0 一致
            Node(gyocd: "O", epabn: '4', chokuno: "001"),               // 1 一致
            Node(gyocd: "B", epabn: '1', chokuno: "001", kiryoso: '2'), // 2 回路要素≠1
            Node(gyocd: "SB", epabn: '1', chokuno: "001"),              // 3 行種コード不可
            Node(gyocd: "B", epabn: '1', chokuno: "001"),               // 4 グループ親追番違い
        };
        mains[4].Data.GroupParentSequenceNumber = "005";
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        foreach (var w in sd) { w.Stat = BranchArraySorter.WorkStatus.Doing; }

        Assert.Equal(new[] { 0, 1 }, BranchArraySorter.GetFloorElementsForSort(sd, mains, 0));
    }

    [Fact]
    public void GetFloorElementsNotForSortはソート対象を除いた要素と最小新並列追番を返す()
    {
        var mains = new[]
        {
            Node(oyatno: "005", chokuno: "001", kaisono: "001", heino: "004"), // 0 = CT対象
            Node(oyatno: "005", chokuno: "002", kaisono: "001"),               // 1 = 対象外
            Node(oyatno: "007", chokuno: "001", kaisono: "003"),               // 2 = 親追番違い
        };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        foreach (var w in sd) { w.Stat = BranchArraySorter.WorkStatus.Doing; }

        var (elements, minParallel) = BranchArraySorter.GetFloorElementsNotForSort(sd, mains, new[] { 0 });

        Assert.Equal(new[] { 1 }, elements);
        Assert.Equal(4, minParallel);
    }

    [Fact]
    public void GetFloorElementsNotForSortは空入力で空と0x7FFFを返す()
    {
        var mains = new[] { Node() };
        var sd = BranchArraySorter.InitializeWorkArea(mains);

        var (elements, minParallel) = BranchArraySorter.GetFloorElementsNotForSort(sd, mains, System.Array.Empty<int>());

        Assert.Empty(elements);
        Assert.Equal(0x7FFF, minParallel);
    }

    [Fact]
    public void FindComponentByDataNumberはデータ追番一致インデックスを返す()
    {
        var components = new[]
        {
            new ComponentEquipment { DataNumber = "001" },
            new ComponentEquipment { DataNumber = "005" },
            new ComponentEquipment { DataNumber = "007" },
        };

        Assert.Equal(1, BranchArraySorter.FindComponentByDataNumber(components, "005"));
    }

    [Fact]
    public void FindComponentByDataNumberは該当なしでマイナス1を返す()
    {
        var components = new[] { new ComponentEquipment { DataNumber = "001" } };
        Assert.Equal(-1, BranchArraySorter.FindComponentByDataNumber(components, "999"));
    }

    private static ComponentEquipment Component(string reservedWord, string ratingKey)
        => new()
        {
            MachineKey = new MachineMasterKey { ReservedWord = reservedWord, RatingKey = ratingKey },
        };

    [Fact]
    public void SetComponentSortCurrentはMCBのAFATを09_3f形式で設定する()
    {
        // p(1)+e(1)+af[4]@2+at[4]@6。af="0100"→100.0、at="0050"→50.0。
        var key = new BranchArraySorter.SortKey();
        BranchArraySorter.SetComponentSortCurrent(Component("MCB", "3201000050"), key);

        Assert.Equal("00100.000", key.Key6);
        Assert.Equal("00050.000", key.Key7);
    }

    [Fact]
    public void SetComponentSortCurrentはMMCBのATに小数2桁を打つ()
    {
        // p(1)+e(1)+af[3]@2(z0)+at[5]@5(z2)。af="100"→100.0、at="00500"→005.00→5.0。
        var key = new BranchArraySorter.SortKey();
        BranchArraySorter.SetComponentSortCurrent(Component("MMCB", "3210000500"), key);

        Assert.Equal("00100.000", key.Key6);
        Assert.Equal("00005.000", key.Key7);
    }

    [Fact]
    public void SetComponentSortCurrentはHPSBのeなしレイアウトを扱う()
    {
        // p(1)+af[3]@1+at[3]@4(e無し)。af="050"→50.0、at="030"→30.0。
        var key = new BranchArraySorter.SortKey();
        BranchArraySorter.SetComponentSortCurrent(Component("HPSB", "3050030"), key);

        Assert.Equal("00050.000", key.Key6);
        Assert.Equal("00030.000", key.Key7);
    }

    [Fact]
    public void SetComponentSortCurrentはNHMBはATのみ設定しAFは0のまま()
    {
        // p(1)+at[4]@1(z2)。at="1000"→10.00→10.0。af無し→0。
        var key = new BranchArraySorter.SortKey();
        BranchArraySorter.SetComponentSortCurrent(Component("NHMB", "31000"), key);

        Assert.Equal("00000.000", key.Key6);
        Assert.Equal("00010.000", key.Key7);
    }

    [Fact]
    public void SetComponentSortCurrentは対象外予約語で両方0を設定する()
    {
        var key = new BranchArraySorter.SortKey();
        BranchArraySorter.SetComponentSortCurrent(Component("MC", "9999999999"), key);

        Assert.Equal("00000.000", key.Key6);
        Assert.Equal("00000.000", key.Key7);
    }
}
