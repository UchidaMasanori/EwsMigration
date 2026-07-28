using Ews.Domain.Analysis;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// タイプチェック(変換形状タイプ一覧作成)。
/// 【C原典】Fysk01_Type_Check / Fysk01_HandleRock_Check / Fysk08_Usetype_Check /
///          Fysk01_Keijyoutype_Check (toku/sekkei/src/Fysk01.c, Fysk08.c)。
///
/// 各回路データの形状タイプをチェックし、デフォルト値が無い時は盤種別・回路相数・
/// 行種・封印区分・製作仕様から決まる「選択番号(ii)」で規定タイプを設定する。
///
/// <c>Fysk01_Type_Check</c> の下請け 3 関数を統合:
///   1. <see cref="CheckHandleLock"/>   (=Fysk01_HandleRock_Check)  … ハンドルロック位置を返す
///   2. <see cref="ApplyUseTypeMask"/>  (=Fysk08_Usetype_Check)     … 予約語マスタで未使用枠をクリア
///   3. <see cref="BuildConvertedShapeTypes"/>(=Fysk01_Keijyoutype_Check) … 変換形状タイプ一覧作成
///
/// 姉妹関数の <see cref="ShapeTypeChecker"/>(Type_Check2/3)は候補選択用の簡易版。
/// 本クラスは盤条件を用いる本体版で、予約語マスタ(<see cref="ReservedWordMaster"/>)に依存する。
/// </summary>
public static class ShapeTypeSelector
{
    /// <summary>タイプ枠幅。【C原典】TSIZE=7 (fyrt808.h:46)。</summary>
    private const int TypeSize = 7;

    /// <summary>データタイプ枠数。【C原典】dtype[][7] は 7 枠を前提(Usetype_Check の k&lt;7)。</summary>
    private const int SlotCount = 7;

    /// <summary>
    /// タイプチェック本体。
    /// 【C原典】Fysk01_Type_Check(Fysk01.c:2977)。
    /// </summary>
    /// <param name="reservedWord">指定予約語。【C原典】yo。</param>
    /// <param name="dataTypes">データタイプ(7 枠, 各 7 文字)。【C原典】dtype[][7]。</param>
    /// <param name="panelKind">盤種別('1'分電盤/'2'引込盤/…/'8'BOX内機付)。【C原典】bn。</param>
    /// <param name="circuitPhases">回路相数。【C原典】ks。</param>
    /// <param name="lineKind">行種。【C原典】gs。</param>
    /// <param name="sealKind">封印区分。【C原典】fi。</param>
    /// <param name="manufacturingKind">物件情報 制作区分。【C原典】ss。</param>
    /// <param name="specialFlag">特別処理(MCB 時のみ、以外は 0)。【C原典】tfg。</param>
    /// <param name="reservedWordMaster">予約語マスタ(タイプ使用有無)。【C原典】YOYAKU_TBL。</param>
    public static ShapeTypeCheckResult Select(
        string reservedWord,
        IReadOnlyList<string> dataTypes,
        char panelKind,
        char circuitPhases,
        string lineKind,
        char sealKind,
        string manufacturingKind,
        int specialFlag,
        IReadOnlyList<ReservedWordMaster> reservedWordMaster)
    {
        // 7 枠へ正規化(各枠 7 文字にパディング)。
        string[] slots = new string[SlotCount];
        for (int k = 0; k < SlotCount; k++)
        {
            slots[k] = k < dataTypes.Count ? PadSlot(dataTypes[k]) : Blank();
        }

        // 1. ハンドルロックチェック(クリア前の dtype を参照)。【C原典】*fg = HandleRock_Check(...)
        int handleLockPosition = CheckHandleLock(reservedWord, slots);

        // 2. 使用有無チェック(予約語マスタで未使用枠をクリア)。
        (bool found, IReadOnlyList<string> masked) = ApplyUseTypeMask(reservedWord, slots, reservedWordMaster);
        if (!found)
        {
            // 【C原典】ret != GOOD → return NOGOOD(wtype/tsu/ti は未設定)。
            return new ShapeTypeCheckResult(slots, [], 0, 0, handleLockPosition, false);
        }

        // 3. 変換形状タイプ一覧作成。
        (IReadOnlyList<string> converted, int typeCount, int typePosition) =
            BuildConvertedShapeTypes(reservedWord, masked, panelKind, circuitPhases, lineKind, sealKind, manufacturingKind, specialFlag);

        return new ShapeTypeCheckResult(masked, converted, typeCount, typePosition, handleLockPosition, true);
    }

    /// <summary>
    /// ハンドルロック区分をチェックし、対象位置(なければ -1)を返す。
    /// 【C原典】Fysk01_HandleRock_Check(Fysk01.c:5150)。
    /// 予約語を前方一致で表引きし、hdlchk 位置のデータタイプが "HL " なら位置を返す。
    /// </summary>
    public static int CheckHandleLock(string reservedWord, IReadOnlyList<string> dataTypes)
    {
        foreach (SelectionShapeTableEntry entry in ShapeTypeTablesForSelection.ConversionTable)
        {
            if (!MatchesPrefix(reservedWord, entry.ReservedWord))
            {
                continue;
            }

            int position = entry.HandleLockPosition;
            if (position < 0)
            {
                break;
            }

            if (MatchesPrefix(Slot(dataTypes, position), "HL "))
            {
                return position;
            }
        }

        return -1;
    }

    /// <summary>
    /// タイプの使用有無チェック。予約語マスタで一致レコードを探し、
    /// 機器選定要素区分が ' '(未使用)の枠のデータタイプをブランクにクリアする。
    /// 【C原典】Fysk08_Usetype_Check(Fysk08.c:295)。
    /// </summary>
    /// <returns>found=予約語がマスタに存在したか / 更新後のデータタイプ(7 枠)。</returns>
    public static (bool Found, IReadOnlyList<string> DataTypes) ApplyUseTypeMask(
        string reservedWord,
        IReadOnlyList<string> dataTypes,
        IReadOnlyList<ReservedWordMaster> reservedWordMaster)
    {
        string key = PadTo(reservedWord, 8);
        foreach (ReservedWordMaster master in reservedWordMaster)
        {
            // 【C原典】memcmp(yo, YOYAKU_TBL[i].yoyaku, 8) == 0 (8 バイト完全一致)。
            if (PadTo(master.ReservedWord, 8) != key)
            {
                continue;
            }

            string[] result = new string[SlotCount];
            for (int k = 0; k < SlotCount; k++)
            {
                string current = k < dataTypes.Count ? PadSlot(dataTypes[k]) : Blank();
                bool used = k < master.SelectionElementKinds.Count && master.SelectionElementKinds[k] != ' ';
                result[k] = used ? current : Blank();
            }

            return (true, result);
        }

        return (false, dataTypes);
    }

    /// <summary>
    /// 変換形状タイプ一覧を作成する。
    /// 【C原典】Fysk01_Keijyoutype_Check(Fysk01.c:3059)。
    /// </summary>
    /// <returns>変換形状タイプ(7 枠) / タイプ数(tsu) / タイプ位置(ti)。</returns>
    public static (IReadOnlyList<string> ConvertedTypes, int TypeCount, int TypePosition) BuildConvertedShapeTypes(
        string reservedWord,
        IReadOnlyList<string> dataTypes,
        char panelKind,
        char circuitPhases,
        string lineKind,
        char sealKind,
        string manufacturingKind,
        int specialFlag)
    {
        string[] wtype = new string[SlotCount];
        for (int k = 0; k < SlotCount; k++)
        {
            wtype[k] = Blank();
        }

        int typePosition = 0;
        int typeCount = 1;

        foreach (SelectionShapeTableEntry entry in ShapeTypeTablesForSelection.ConversionTable)
        {
            if (!MatchesPrefix(reservedWord, entry.ReservedWord))
            {
                continue;
            }

            typePosition = entry.Position;
            string word = entry.ReservedWord.TrimEnd();
            SelectionShapeVariant first = entry.Variants[0];

            if (word == "PBS")
            {
                // 【C原典】wtype[0]=ktype[ichi]; wtype[1]=typ[0]; tsu=su。
                wtype[0] = Slot(dataTypes, typePosition);
                wtype[1] = PadSlot(first.Types[0]);
                typeCount = first.Types.Count;
                return (wtype, typeCount, typePosition);
            }

            if (word == "WH")
            {
                // 【C原典】ktype[ichi] が "KE " でなければ全 su タイプ、そうなら ktype[ichi]。
                if (!MatchesPrefix(Slot(dataTypes, typePosition), "KE "))
                {
                    for (int j = 0; j < first.Types.Count; j++)
                    {
                        wtype[j] = PadSlot(first.Types[j]);
                    }
                    typeCount = first.Types.Count;
                }
                else
                {
                    wtype[0] = Slot(dataTypes, typePosition);
                }
                return (wtype, typeCount, typePosition);
            }

            if (word == "EE")
            {
                // 【C原典・改訂<21>】全 su タイプを設定。この分岐は break が無く
                // ループが終端({NULL})へ落ちて wtype[0]=ktype[0] で上書きされる。
                for (int j = 0; j < first.Types.Count; j++)
                {
                    wtype[j] = PadSlot(first.Types[j]);
                }
                typeCount = first.Types.Count;
                wtype[0] = Slot(dataTypes, 0); // 終端落ちによる wtype[0] 上書きを再現。
                return (wtype, typeCount, typePosition);
            }

            // 既定分岐(MCB/ELB/MMCB/ELMB/SB/RMCB/RELB/RMMCB/RELMB/MC/TR/CR)。
            if (IsBlankSlot(Slot(dataTypes, typePosition)))
            {
                SelectionShapeVariant? variant;
                if (word == "TR")
                {
                    // 【C原典】n=0; if(bn=='1') n=1; type_t[n] を採用(seleno 非使用)。
                    int n = panelKind == '1' ? 1 : 0;
                    variant = entry.Variants[n];
                }
                else if (word == "CR")
                {
                    variant = entry.Variants[0];
                }
                else
                {
                    int selectionNumber = ComputeSelectionNumber(
                        panelKind, circuitPhases, lineKind, sealKind, manufacturingKind, specialFlag);
                    variant = entry.Variants.FirstOrDefault(v => v.SelectionNumber == selectionNumber);
                }

                if (variant is not null)
                {
                    for (int k = 0; k < variant.Types.Count; k++)
                    {
                        wtype[k] = PadSlot(variant.Types[k]);
                    }
                    typeCount = variant.Types.Count;
                }
                else
                {
                    // 【C原典】seleno 未ヒット → 終端の su=0(wtype はブランクのまま)。
                    typeCount = 0;
                }
            }
            else
            {
                // 【C原典】ktype[ichi] が非ブランク → そのまま採用(tsu は既定 1)。
                wtype[0] = Slot(dataTypes, typePosition);
            }

            return (wtype, typeCount, typePosition);
        }

        // 【C原典】どの予約語にも一致せず終端 → wtype[0]=ktype[0](tsu=1, ti=0)。
        wtype[0] = Slot(dataTypes, 0);
        return (wtype, typeCount, typePosition);
    }

    /// <summary>
    /// 選択番号(ii)を組み立てる。
    /// 【C原典】Fysk01_Keijyoutype_Check の i1..i5 ビットフラグ演算。
    ///   0x10 回路3相 / 0x08 行種(分岐) / 0x04 盤種別 / 0x02 封印 / 0x01 製作仕様。
    /// </summary>
    private static int ComputeSelectionNumber(
        char panelKind, char circuitPhases, string lineKind, char sealKind, string manufacturingKind, int specialFlag)
    {
        if (specialFlag == 1)
        {
            return 99; // MCB 特別処理時
        }
        if (specialFlag == 2)
        {
            return 8;  // ELB 特別処理時
        }

        int i1 = circuitPhases == '3' ? 16 : 0; // 回路3相

        string gs = PadTo(lineKind, 3);
        bool notSpecialLine =
            !Eq(gs, "TM ", 3) && !Eq(gs, "M  ", 3) && !Eq(gs, "S  ", 3) &&
            !Eq(gs, "SM ", 3) && !(gs[1] == 'S' && gs[2] == 'M');
        int i2 = notSpecialLine ? 8 : 0; // 行種(分岐)

        int i3 = 0;
        int i4 = 0;
        if (sealKind == 'H')
        {
            i4 = 2; // 封印
        }
        else if (panelKind < '1' || panelKind > '4')
        {
            i3 = 4; // 盤種別
        }

        int i5 = Eq(PadTo(manufacturingKind, 2), "01", 2) ? 0 : 1; // 河村標準以外

        return i1 | i2 | i3 | i4 | i5;
    }

    /// <summary>予約語(src)の先頭 pattern.Length 文字が pattern と一致するか(memcmp 相当, 空白パディング)。</summary>
    private static bool MatchesPrefix(string src, string pattern)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = i < src.Length ? src[i] : ' ';
            if (c != pattern[i])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>先頭 length 文字の一致(memcmp 相当)。src は length 以上の長さを前提。</summary>
    private static bool Eq(string src, string pattern, int length)
    {
        for (int i = 0; i < length; i++)
        {
            char c = i < src.Length ? src[i] : ' ';
            if (c != pattern[i])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>データタイプ配列の index 番目の 7 文字枠(範囲外はブランク)。【C原典】ktype[index]。</summary>
    private static string Slot(IReadOnlyList<string> dataTypes, int index)
        => index >= 0 && index < dataTypes.Count ? PadSlot(dataTypes[index]) : Blank();

    /// <summary>値を 7 文字枠へ(右空白詰め/切り詰め)。</summary>
    private static string PadSlot(string value) => PadTo(value, TypeSize);

    /// <summary>値を width 文字へ(右空白詰め/切り詰め)。</summary>
    private static string PadTo(string value, int width)
    {
        value ??= string.Empty;
        return value.Length >= width ? value[..width] : value.PadRight(width);
    }

    /// <summary>7 文字ブランク枠。</summary>
    private static string Blank() => new(' ', TypeSize);

    /// <summary>7 文字枠がすべて空白か。【C原典】memcmp(ktype[ichi],"       ",TSIZE)==0。</summary>
    private static bool IsBlankSlot(string slot) => slot.TrimEnd().Length == 0;
}
