# C→C# 移行方針: ポインタ処理と `\0`(NUL終端)・固定長文字列の扱い

本書は EWS の C 資産を C# へ移植する際の、**ポインタ処理**と **`\0`(NUL終端)・固定長文字列**に関する基本方針を明文化したものです。原則は「C 固有のメモリ/文字列表現は C# の言語機構へ置き換える。ただし**挙動(意味論)は保存**し、原文名を `【C原典】` コメント＋ `docs/name-mapping.csv` に併記して追跡可能にする」です。

## 1. ポインタ処理 ? C# の参照・コレクションへ置換

C のポインタは「メモリアドレス」ではなく「**参照 / コレクション要素 / null 可能性 / 出力引数**」という意味に読み替えて置換します。

| C原典のパターン | C# での扱い | 実例 |
|---|---|---|
| 構造体ポインタ渡し `KIKITABLE*` / `GYOSYU*` | クラス(参照型)。値更新は呼び出し元へ自然に反映 | `EquipmentTableEntry` / `LineTypeTableEntry` は `class`。`SetCircuitDivision` が `kiki[i].CircuitDivision = 'K'` で直接更新 |
| 配列＋ポインタ演算 `S_Kiki+i` / `kiki[i]` | `List<T>` / 配列のインデックスアクセス | `SetCircuitDivision` の `while (i < count) { s = kiki[i]; ... }` |
| NULL ポインタ返却 `Find_Gyosyu(G_No==0)→NULL` | Nullable 参照型(`<Nullable>enable</Nullable>`)＋ null 合体演算子 | `FindLineType(...)` が `LineTypeTableEntry?` を返し `?.LineType ?? string.Empty` |
| 件数＋配列ペア `imagec, imagea` | `IReadOnlyList<T>`(件数は `.Count`) | `CircuitAnalyzer.Analyze(bukken1, bukken2, IReadOnlyList<CircuitDescriptionLine> lines)` |
| ポインタのポインタ / 出力引数 `struct**`, `SHORT*`, `&i_Keitou` | 戻り値・タプル・`out` | 解析結果は戻り値の `record`(`CircuitParseResult` 等)へ集約 |
| 手動メモリ管理 `calloc`/`free`/`MemFree` | GC に委譲。**対応コードを作らない** | `Fysk10.c` の `MemFree(...)` は C# 側に対応物なし |
| 連結リスト `PATTERN *next` の走査 | 静的配列 + `foreach` | `def_ptn` 連結リスト → `Patterns[]` を `foreach` |
| 関数ポインタ的分岐 | `switch` / パターンマッチ | 予約語種別ディスパッチ等 |

要点:
- `malloc`/`free`/`calloc`/`MemFree` は GC が担うため移植しません(意図的にドロップ)。
- NULL チェックは `nullable` 参照型で表現し、C の `if (ptr == NULL)` を `?.` / `?? 既定値` へ置換します。

## 2. `\0`(NUL終端)・固定長文字列 ? 境界で吸収し、内部は普通の `string`

原則:「**C 表現(`\0` 終端・空白埋め・バイト長)との変換は DAL / 固定長レコード境界に閉じ込め、それ以降は長さ管理された `string` で扱う**」。

| C原典のパターン | C# での扱い |
|---|---|
| 固定長 `CHAR[n]`(右空白/NUL 埋め)の読み書き | `FixedFieldCodec.ReadText/WriteText` に集約。読込は `Trim(' ', '\u3000', '\0', '\r', '\n')`(＝`CpyNullStop`/`LibCharBackSpaceCut`)、書込は右空白パディング＋`Min(len)` 切り詰め |
| `strcmp`/`memcmp`(`\0` 前提、`max(len,strlen)`) | C# の `==`(序数一致)。例: E.5 の `memcmp(gyosyu,"PM",max(2,strlen))` → `gyosyu == "PM"` |
| `strcat` による組立 | 文字列連結・補間。例: `SetCircuitDivision` の `candidate += "," + MeterName(...)` |
| `NULLSTRING`(先頭 `\0` で空判定) | `string.IsNullOrEmpty` / `CircuitStringChecker.IsNullString` |
| 文字コード | CP932(Shift-JIS)境界で確定し、内部は UTF-16 `string` |

要点:
- 内部ロジック層では `string` の長さ・不変性に任せ、`\0` を意識しません。
- バイト長(全角=2バイト等)や固定長切り詰めが必要な処理は `FixedFieldCodec` に閉じ込めます。

## 3. "考えなくてよい" の例外 ? 挙動を保存する箇所

C の文字列/数値関数には C# 標準 API と挙動が異なるものがあり、そのまま置換すると結果が変わるため、**あえて C 互換ヘルパで意味論を移植**します。

- `atoi` / `atof`: 「先頭の数値部だけ解釈し、非数字・`\0` で停止」。C# の `int.Parse` は全体が数値でないと失敗するので、`ElectricalParameterChecker.AtoiC` / `AtofC` で C セマンティクスを再現。
- `Get_1_Group` 等の**桁数カウント**: 全角=2バイト等のバイト長前提が残るため、バイト計算を固定長コーデック/専用ロジックに閉じ込めて保存。
- 固定長への書き戻し時の切り詰め(`Min(encoded.Length, length)`)は、C の `CHAR[n]` 溢れ挙動を保つ。

## 4. まとめ(チェックリスト)

1. ポインタ = 参照 / コレクション / null 可能性 / 出力引数 へ読み替える。`malloc`/`free` は移植しない。
2. `\0`・固定長・バイト長は **DAL・`FixedFieldCodec` の境界に隔離**し、業務ロジック層は `string` / `List<T>` で書く。
3. **C の文字列/数値関数の "途中解釈・桁計上" は挙動保存が必要** → C 互換ヘルパで明示的に移植する。
4. 置換した箇所は原文名を `【C原典】` コメント＋ `docs/name-mapping.csv` に併記して追跡可能にする。

## 関連

- 固定長コーデック: `src/Ews.Domain/Common/FixedFieldCodec.cs`(【C原典】`cmnchar.c` / `getfpath.c` の `CpyNullStop`)
- 名称対応表: `docs/name-mapping.csv`
- ソース文字コード: `EwsMigration/**/*.cs` および `.csv` は **CP932 / BOMなし**(本ドキュメントは Markdown のため UTF-8)
