namespace Ews.Analysis;

/// <summary>
/// 主回路パラメータ生成における電圧値(定格電圧 kv1/kv2/kv3 の 3 要素配列)の
/// 変換・整列を行う決定的ユーティリティ。
///
/// 【C原典】toku/sekkei/src/Fyss14.c(主回路設計・上流からのパラメータ生成／電圧値の継承)
///   - Volt_Conv  (Fyss14.c:2105) 電圧変換テーブル照合
///   - Max_Volt   (Fyss14.c:2156) 3 要素の最大値を先頭へ
///   - Right_Volt (Fyss14.c:2175) 非ゼロ値を右詰め
///   - Left_Volt  (Fyss14.c:5608) 非ゼロ値を左詰め(ゼロ除去)
///
/// いずれも外部データに依存しない純粋な配列操作であり、C 原典の副作用
/// (引数配列の破壊的更新)をそのまま再現する。要素数は常に 3 を前提とする。
/// </summary>
public static class VoltageInheritance
{
    /// <summary>
    /// 電圧変換テーブル。[n][0] = 変換前電圧配列 / [n][1] = 変換後電圧配列。
    /// 【C原典】Fyss14.c:2113 static SHORT varr[][2][3]。番兵 {-1,-1,-1} は
    /// 探索終了条件(varr[i][0][0] &gt;= 0)で表現し、ここには含めない。
    /// </summary>
    private static readonly short[][][] ConversionTable =
    [
        [[  0,   0, 105], [  0,   0, 100]],
        [[  0,   0, 210], [  0,   0, 200]],
        [[  0, 210, 105], [  0, 200, 100]],
        [[  0,   0, 400], [  0,   0, 400]],
        [[  0,   0, 410], [  0,   0, 400]],
        [[  0,   0, 420], [  0,   0, 420]],
        [[  0,   0, 440], [  0,   0, 440]],
        [[  0, 173, 100], [  0, 173, 100]],
        [[  0, 380, 220], [  0, 380, 220]],
        [[210, 210, 105], [200, 200, 100]],
        [[  0,   0, 100], [  0,   0, 100]],
        [[  0,   0, 380], [  0,   0, 380]],
    ];

    /// <summary>
    /// 変換前電圧配列 <paramref name="kv"/> をテーブルと突き合わせ、一致すれば
    /// 変換後電圧配列を <paramref name="v"/> へコピーする。一致しなければ
    /// <paramref name="v"/> は変更しない。C 原典と同じく <paramref name="v"/> を返す。
    /// 【C原典】Fyss14.c:2105 Volt_Conv。memcmp による 3 要素完全一致で先頭一致を採用。
    /// </summary>
    /// <param name="kv">変換前電圧配列(長さ 3)。</param>
    /// <param name="v">変換後を書き込む配列(長さ 3)。一致しない場合は不変。</param>
    /// <returns>引数 <paramref name="v"/> をそのまま返す。</returns>
    public static short[] ConvertVoltage(short[] kv, short[] v)
    {
        ArgumentNullException.ThrowIfNull(kv);
        ArgumentNullException.ThrowIfNull(v);

        // 【C原典】for( i=0 ; varr[i][0][0]>=0 ; i++ ) の完全一致探索。
        foreach (short[][] entry in ConversionTable)
        {
            short[] from = entry[0];
            if (kv[0] == from[0] && kv[1] == from[1] && kv[2] == from[2])
            {
                short[] to = entry[1];
                v[0] = to[0];
                v[1] = to[1];
                v[2] = to[2];
                break;
            }
        }

        return v;
    }

    /// <summary>
    /// 3 要素の最大値を先頭要素へ設定し、残り 2 要素を 0 にする。
    /// 【C原典】Fyss14.c:2156 Max_Volt。
    /// </summary>
    /// <param name="kv">対象電圧配列(長さ 3)。破壊的に更新する。</param>
    public static void MaxVoltage(short[] kv)
    {
        ArgumentNullException.ThrowIfNull(kv);

        // 【C原典】*kv = max(*kv,max(*(kv+1),*(kv+2)));
        kv[0] = Math.Max(kv[0], Math.Max(kv[1], kv[2]));
        kv[1] = 0;
        kv[2] = 0;
    }

    /// <summary>
    /// 非ゼロの電圧値を配列の右側へ寄せる。
    /// 【C原典】Fyss14.c:2175 Right_Volt。C 原典の条件分岐(先頭 1 要素のみ非ゼロ／
    /// 先頭 2 要素が非ゼロ／その他)をそのまま再現し、代入順序も一致させる。
    /// </summary>
    /// <param name="kv">対象電圧配列(長さ 3)。破壊的に更新する。</param>
    public static void RightAlignVoltage(short[] kv)
    {
        ArgumentNullException.ThrowIfNull(kv);

        if (kv[0] == 0 && kv[1] == 0 && kv[2] == 0)
        {
            // 【C原典】全要素 0 は 0 のまま。
            kv[2] = 0;
            kv[1] = 0;
            kv[0] = 0;
        }
        else if (kv[0] != 0 && kv[1] == 0 && kv[2] == 0)
        {
            // 【C原典】kv[2]=kv[0]; kv[1]=0; kv[0]=0;
            kv[2] = kv[0];
            kv[1] = 0;
            kv[0] = 0;
        }
        else if (kv[0] != 0 && kv[1] != 0 && kv[2] == 0)
        {
            // 【C原典】kv[2]=kv[1]; kv[1]=kv[0]; kv[0]=0;
            kv[2] = kv[1];
            kv[1] = kv[0];
            kv[0] = 0;
        }
        else
        {
            // 【C原典】その他は変更なし。
        }
    }

    /// <summary>
    /// 非ゼロの電圧値を配列の左側へ寄せる(ゼロを詰める)。
    /// 【C原典】Fyss14.c:5608 Left_Volt。3 つの逐次 if(各更新後に再判定)を再現。
    /// </summary>
    /// <param name="kv">対象電圧配列(長さ 3)。破壊的に更新する。</param>
    public static void LeftAlignVoltage(short[] kv)
    {
        ArgumentNullException.ThrowIfNull(kv);

        // 【C原典】if(kv[0]==0){ kv[0]=kv[1]; kv[1]=kv[2]; kv[2]=0; }
        if (kv[0] == 0)
        {
            kv[0] = kv[1];
            kv[1] = kv[2];
            kv[2] = 0;
        }

        // 【C原典】2 回目の同一判定(先頭 2 要素が 0 の場合の詰め直し)。
        if (kv[0] == 0)
        {
            kv[0] = kv[1];
            kv[1] = kv[2];
            kv[2] = 0;
        }

        // 【C原典】if(kv[1]==0){ kv[1]=kv[2]; kv[2]=0; }
        if (kv[1] == 0)
        {
            kv[1] = kv[2];
            kv[2] = 0;
        }
    }
}
