using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// ＣＴ自動生成(<see cref="CtAutoGenerator"/>)の移植検証。【C原典】Pre_CT_Make(Fyss15.c)。
/// </summary>
public sealed class CtAutoGeneratorTests
{
    private static MainCircuitResult Am(string a2, char kiryoso = '1', string yoyaku = "AM      ")
    {
        var r = new MainCircuitResult();
        r.Data.ReservedWord = yoyaku;
        r.Data.CircuitElement = kiryoso;
        r.Data.ElectricalParameterSlots[1].A2 = a2;
        return r;
    }

    private static MainCircuitResult Dummy()
    {
        var r = new MainCircuitResult();
        r.Data.ReservedWord = "MCB     ";
        r.Data.CircuitElement = '1';
        return r;
    }

    [Fact]
    public void PrepareCtCreationはAMの前後2箇所のCT位置を作る()
    {
        var mains = new[] { Am("00050.000") };

        var result = CtAutoGenerator.PrepareCtCreation(mains);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].CauseDataNumber);
        Assert.Equal(0, result[0].InsertBeforeDataNumber); // 前挿入位置 i
        Assert.Equal(1, result[1].CauseDataNumber);
        Assert.Equal(1, result[1].InsertBeforeDataNumber); // 後挿入位置 i+1
    }

    [Fact]
    public void PrepareCtCreationは定格電流2が30A以下なら生成しない()
    {
        Assert.Empty(CtAutoGenerator.PrepareCtCreation(new[] { Am("00030.000") }));
        Assert.Empty(CtAutoGenerator.PrepareCtCreation(new[] { Am("00000.000") }));
    }

    [Fact]
    public void PrepareCtCreationは回路要素が1以外なら生成しない()
    {
        Assert.Empty(CtAutoGenerator.PrepareCtCreation(new[] { Am("00050.000", kiryoso: '2') }));
    }

    [Fact]
    public void PrepareCtCreationはAM以外なら生成しない()
    {
        Assert.Empty(CtAutoGenerator.PrepareCtCreation(new[] { Am("00050.000", yoyaku: "WH      ") }));
    }

    [Fact]
    public void PrepareCtCreationは複数AMをdatanoCT昇順に整列する()
    {
        var mains = new[] { Dummy(), Am("00050.000"), Dummy(), Am("00050.000") };

        var result = CtAutoGenerator.PrepareCtCreation(mains);

        Assert.Equal(4, result.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Select(c => c.InsertBeforeDataNumber));
    }

    // ---- InsertCtIntoMainCircuit(=Mainfile_CT_Make) --------------------------

    private static MainCircuitResult Main(string yoyaku, char kiryoso, string datano)
    {
        var r = new MainCircuitResult();
        r.SequenceNumber = datano;
        r.Data.ReservedWord = yoyaku;
        r.Data.CircuitElement = kiryoso;
        return r;
    }

    private static List<CtAutoGenerator.CtInfo> SingleAmCtList(int causeDataNumber)
    {
        // AM(旧 index=causeDataNumber-1)の前後(i,i+1)に挿入する位置対。
        return
        [
            new() { CauseDataNumber = causeDataNumber, InsertBeforeDataNumber = causeDataNumber - 1 },
            new() { CauseDataNumber = causeDataNumber, InsertBeforeDataNumber = causeDataNumber },
        ];
    }

    [Fact]
    public void InsertCtIntoMainCircuitはCT対を挿入し通しで再採番する()
    {
        var upstream = Main("MCB     ", '1', "001");
        var am = Main("AM      ", '1', "002");
        var mains = new List<MainCircuitResult> { upstream, am };

        var result = CtAutoGenerator.InsertCtIntoMainCircuit(mains, SingleAmCtList(2));

        Assert.Equal(4, result.Count);
        Assert.Equal(new[] { "001", "002", "003", "004" }, result.Select(r => r.SequenceNumber));
        Assert.Equal("CT", result[1].Data.ReservedWord.TrimEnd());   // 前CT
        Assert.Equal("MCB", result[0].Data.ReservedWord.TrimEnd());
        Assert.Equal("AM", result[2].Data.ReservedWord.TrimEnd());   // 挿入対の間のAM本体
        Assert.Equal("CT", result[3].Data.ReservedWord.TrimEnd());   // 後CT
    }

    [Fact]
    public void InsertCtIntoMainCircuitは回路要素マーキングとep写しを行う()
    {
        var upstream = Main("MCB     ", '1', "001");
        var am = Main("AM      ", '1', "002");
        am.Data.ElectricalParameterSlots[0].A1 = "00000.000";
        am.Data.ElectricalParameterSlots[0].A2 = "00050.000";
        var mains = new List<MainCircuitResult> { upstream, am };

        var result = CtAutoGenerator.InsertCtIntoMainCircuit(mains, SingleAmCtList(2));

        // 前CT='2'(自動生成群)、AM本体='2'、後CT='1'。
        Assert.Equal('2', result[1].Data.CircuitElement);
        Assert.Equal('2', result[2].Data.CircuitElement);
        Assert.Equal('1', result[3].Data.CircuitElement);
        // 定格電流2→1へ写し、2次側は5A。
        Assert.Equal("00050.000", am.Data.ElectricalParameterSlots[0].A1);
        Assert.Equal("00005.000", am.Data.ElectricalParameterSlots[0].A2);
        // 後CTはAM(ctElem)のep[0]を引き継ぐ。
        Assert.Equal("00050.000", result[3].Data.ElectricalParameterSlots[0].A1);
        Assert.Equal("00005.000", result[3].Data.ElectricalParameterSlots[0].A2);
        Assert.Equal('1', result[3].Data.ElectricalParameterSlots[0].Qty);
        Assert.Equal('1', result[3].Data.AutoGenerationKind);
        Assert.Equal("KT     ", result[3].Data.DataType[1]);
    }

    [Fact]
    public void InsertCtIntoMainCircuitは定格電流1が非ゼロなら写さない()
    {
        var upstream = Main("MCB     ", '1', "001");
        var am = Main("AM      ", '1', "002");
        am.Data.ElectricalParameterSlots[0].A1 = "00030.000";
        am.Data.ElectricalParameterSlots[0].A2 = "00050.000";
        var mains = new List<MainCircuitResult> { upstream, am };

        _ = CtAutoGenerator.InsertCtIntoMainCircuit(mains, SingleAmCtList(2));

        Assert.Equal("00030.000", am.Data.ElectricalParameterSlots[0].A1);
        Assert.Equal("00050.000", am.Data.ElectricalParameterSlots[0].A2);
    }

    [Theory]
    [InlineData('1', '2')]
    [InlineData('3', '4')]
    public void InsertCtIntoMainCircuitはCTの並び替え機器区分を1進める(char whamSortKind, char expected)
    {
        var upstream = Main("MCB     ", '1', "001");
        var am = Main("AM      ", '1', "002");
        am.Data.SortKind = whamSortKind;
        var mains = new List<MainCircuitResult> { upstream, am };

        var result = CtAutoGenerator.InsertCtIntoMainCircuit(mains, SingleAmCtList(2));

        Assert.Equal(expected, result[1].Data.SortKind);   // 前CT(narakbn=wham+1)
    }

    [Fact]
    public void InsertCtIntoMainCircuitは親データ追番を新採番へ付け替える()
    {
        var upstream = Main("MCB     ", '1', "001");
        var am = Main("AM      ", '1', "002");
        am.Data.ParentSequenceNumber = "001";        // 親=上流
        var downstream = Main("MCCB    ", '1', "003");
        downstream.Data.ParentSequenceNumber = "002"; // 親=AM(旧追番002)
        var mains = new List<MainCircuitResult> { upstream, am, downstream };

        var result = CtAutoGenerator.InsertCtIntoMainCircuit(mains, SingleAmCtList(2));

        Assert.Equal(5, result.Count);
        Assert.Equal("003", result[2].SequenceNumber);            // AM本体の新追番
        Assert.Equal("001", result[2].Data.ParentSequenceNumber); // 上流の新追番
        Assert.Equal("005", result[4].SequenceNumber);            // 下流の新追番
        Assert.Equal("003", result[4].Data.ParentSequenceNumber); // AMの新追番へ付け替え
    }
}
