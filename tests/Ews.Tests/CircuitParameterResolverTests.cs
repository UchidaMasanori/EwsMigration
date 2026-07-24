using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 親機器と主回路データから自機器の下流回路パラメータを決定する処理の検証。
/// 【C原典】toku/sekkei/src/Fyss14.c:1728 Kairo_Parm_Set。
/// </summary>
public sealed class CircuitParameterResolverTests
{
    private static MainCircuitParameter Prm(
        short ph, short wr, short p, short v0, short v1, short v2, short vkbn = 0)
    {
        var prm = new MainCircuitParameter { Phase = ph, WireType = wr, Pole = p, AcDcKind = vkbn };
        prm.Voltage[0] = v0;
        prm.Voltage[1] = v1;
        prm.Voltage[2] = v2;
        return prm;
    }

    private static MainCircuitResult MainRecord(
        string reservedWord, char circuitElement = '1',
        string incoming = "000", string hierarchy = "000", string series = "000",
        string loadVoltage0 = "000", string loadVoltage1 = "000")
    {
        var data = new MainCircuitData
        {
            ReservedWord = reservedWord,
            CircuitElement = circuitElement,
            IncomingNumber = incoming,
            HierarchyNumber = hierarchy,
            SeriesNumber = series,
        };
        data.AttachedParameter.LoadVoltage[0] = loadVoltage0;
        data.AttachedParameter.LoadVoltage[1] = loadVoltage1;
        return new MainCircuitResult { Data = data };
    }

    private static void AssertPrm(
        MainCircuitParameter actual,
        short ph, short wr, short p, short v0, short v1, short v2, short vkbn)
    {
        Assert.Equal(ph, actual.Phase);
        Assert.Equal(wr, actual.WireType);
        Assert.Equal(p, actual.Pole);
        Assert.Equal(v0, actual.Voltage[0]);
        Assert.Equal(v1, actual.Voltage[1]);
        Assert.Equal(v2, actual.Voltage[2]);
        Assert.Equal(vkbn, actual.AcDcKind);
    }

    [Fact]
    public void 予約語NT_入力を1P2W固定にしてmcprmcnvで変換する()
    {
        // 【C原典】NT は inputp={1,2,0,v0}。親{3,4,4,{0,1,1}}+ti{1,2,0,{0,0,0}} →
        //   線式テーブル {1,2,1,{0,0,1}}。v[2]=親v[2]=105。
        var parent = Prm(3, 4, 4, 0, 210, 105);
        var result = new MainCircuitParameter();
        var mains = new List<MainCircuitResult> { MainRecord("NT") };

        int rc = CircuitParameterResolver.SetCircuitParameter(60, parent, result, mains, 0, mains.Count);

        Assert.Equal(1, rc);
        AssertPrm(result, 1, 2, 1, 0, 0, 105, 0);
    }

    [Fact]
    public void F主回路_1P3Wで負荷電圧200V指定は1P2W2Pに親第2電圧をとる()
    {
        // 【C原典】yoyaku=="F" kiryoso=='1' 1P3W fpalv[0]=="200"&&fpalv[1]=="000" →
        //   {1,2,2, v[2]=pprmp->v[1]}。
        var parent = Prm(1, 3, 3, 0, 210, 105);
        var result = new MainCircuitParameter();
        var mains = new List<MainCircuitResult> { MainRecord("F", '1', loadVoltage0: "200", loadVoltage1: "000") };

        int rc = CircuitParameterResolver.SetCircuitParameter(60, parent, result, mains, 0, 1);

        Assert.Equal(1, rc);
        AssertPrm(result, 1, 2, 2, 0, 0, 210, 0);
    }

    [Fact]
    public void F主回路_1P3Wで負荷電圧指定なしは1P2W1Pに親第3電圧をとる()
    {
        // 【C原典】200/000 でも 200/100 でも次LAでもない → else {1,2,1, v[2]=pprmp->v[2]}。
        var parent = Prm(1, 3, 3, 0, 210, 105);
        var result = new MainCircuitParameter();
        var mains = new List<MainCircuitResult> { MainRecord("F", '1', loadVoltage0: "100", loadVoltage1: "000") };

        int rc = CircuitParameterResolver.SetCircuitParameter(60, parent, result, mains, 0, 1);

        Assert.Equal(1, rc);
        AssertPrm(result, 1, 2, 1, 0, 0, 105, 0);
    }

    [Fact]
    public void F主回路_1P3Wで次機器がLAなら親をそのままコピーする()
    {
        // 【C原典】改訂<26> 次機器 yoyaku=="LA " のとき親コピー(X,N,Y)。
        var parent = Prm(1, 3, 3, 0, 210, 105);
        var result = new MainCircuitParameter();
        var mains = new List<MainCircuitResult>
        {
            MainRecord("F", '1', loadVoltage0: "100", loadVoltage1: "000"),
            MainRecord("LA"),
        };

        int rc = CircuitParameterResolver.SetCircuitParameter(60, parent, result, mains, 0, 2);

        Assert.Equal(1, rc);
        AssertPrm(result, 1, 3, 3, 0, 210, 105, 0);
    }

    [Fact]
    public void F計器回路_1P3WでVSもVMも無いなら105V()
    {
        // 【C原典】計器回路(kiryoso!='1') 1P3W VSなし VMなし → {1,2,1, v[2]=pprmp->v[2]}(105V)。
        var parent = Prm(1, 3, 3, 0, 210, 105);
        var result = new MainCircuitParameter();
        var mains = new List<MainCircuitResult> { MainRecord("F", '2') };

        int rc = CircuitParameterResolver.SetCircuitParameter(60, parent, result, mains, 0, 1);

        Assert.Equal(1, rc);
        AssertPrm(result, 1, 2, 1, 0, 0, 105, 0);
    }

    [Fact]
    public void F計器回路_1P3Wで下流にVMがあれば210V()
    {
        // 【C原典】計器回路 1P3W VSなし VMあり → {1,2,2, v[2]=pprmp->v[1]}(210V)。
        var parent = Prm(1, 3, 3, 0, 210, 105);
        var result = new MainCircuitParameter();
        var mains = new List<MainCircuitResult>
        {
            MainRecord("F", '2', incoming: "001", hierarchy: "001", series: "001"),
            MainRecord("VM", '2', incoming: "001", hierarchy: "002", series: "002"),
        };

        int rc = CircuitParameterResolver.SetCircuitParameter(60, parent, result, mains, 0, 2);

        Assert.Equal(1, rc);
        AssertPrm(result, 1, 2, 2, 0, 0, 210, 0);
    }

    [Fact]
    public void F計器回路_下流にVSがあれば親をそのままコピーする()
    {
        // 【C原典】計器回路 1P3W VSあり → 親コピー(X,N,Y)。
        var parent = Prm(1, 3, 3, 0, 210, 105);
        var result = new MainCircuitParameter();
        var mains = new List<MainCircuitResult>
        {
            MainRecord("F", '2', incoming: "001", hierarchy: "001", series: "001"),
            MainRecord("VS", '2', incoming: "001", hierarchy: "002", series: "002"),
        };

        int rc = CircuitParameterResolver.SetCircuitParameter(60, parent, result, mains, 0, 2);

        Assert.Equal(1, rc);
        AssertPrm(result, 1, 3, 3, 0, 210, 105, 0);
    }

    [Fact]
    public void 通常予約語_入力空なら親構成をそのまま下流へ変換する()
    {
        // 【C原典】F/NT 以外。ep[0]・fpalv が全0 → inputp 全0 → 電圧テーブル先頭行で親をそのまま。
        var parent = Prm(3, 4, 4, 440, 220, 105);
        var result = new MainCircuitParameter();
        var mains = new List<MainCircuitResult> { MainRecord("MCB") };

        int rc = CircuitParameterResolver.SetCircuitParameter(60, parent, result, mains, 0, 1);

        Assert.Equal(1, rc);
        AssertPrm(result, 3, 4, 4, 440, 220, 105, 0);
    }

    [Fact]
    public void 入力電圧_負荷電圧100Vを親電圧変換で第3電圧に配置してmcprmcnvへ渡す()
    {
        // 【C原典】fpalv[1]="100" → v1=100。v0==0&&v1!=0: kv={0,210,105}→Volt_Conv={0,200,100}、
        //   kv[2]==100 で inputp.v[2]=100(フラグ{0,0,1})。親{1,3,3,{0,1,1}}+ti{0,0,0,{0,0,1}} →
        //   電圧テーブル {1,2,1,{0,0,1}}。v[2]=親v[2]=105。
        var parent = Prm(1, 3, 3, 0, 210, 105);
        var result = new MainCircuitParameter();
        var mains = new List<MainCircuitResult> { MainRecord("MCB", '1', loadVoltage0: "000", loadVoltage1: "100") };

        int rc = CircuitParameterResolver.SetCircuitParameter(60, parent, result, mains, 0, 1);

        Assert.Equal(1, rc);
        AssertPrm(result, 1, 2, 1, 0, 0, 105, 0);
    }
}
