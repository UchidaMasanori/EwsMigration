# 引き継ぎドキュメント (HANDOFF)

> このファイルは「PC が故障してもプロジェクトの文脈が失われないように」git リポジトリ内へ残す
> **引き継ぎ用の恒久メモ**です。GitHub に push されるため、ローカル環境が失われても内容は残ります。
> 作業を再開する人（人間・AI 問わず）は、まずこのファイルと [README.md](../README.md)、
> [docs/name-mapping.csv](name-mapping.csv) を読んでください。

最終更新: 2026-07-03

---

## 1. プロジェクトの目的

EWS（特注盤 電気設計支援システム）配下の **C 言語プログラム群を C# / .NET へ移植**し、
データ層（独自 ISAM・固定長ファイル・`.cns` テキストマスタ）を **SQL Server** へ切り替える。

### 絶対に守る制約（原文）

> 「CSharp のディレクトリ内の内容は使用せず、新たに生成してください。C 言語の資産の変数名・関数名・
> ソースファイル名の意味合いがわかりづらいため、コメントから予測して現代的な表現にしてください。
> ただし、生成したソースと C 言語資産との比較ができるように、コメントに C 言語資産の名前を記載しておいてください。」

これを運用ルールに落とすと:

- 既存 `EWS/CSharp/` は**使わない**。すべて `EWS/EwsMigration/` に新規構築する。
- C の挙動（意味論）は保持しつつ、命名は**現代的な英語**へ。
- 生成コードには `【C原典】` コメントで元の C 名を記載し、加えて
  [docs/name-mapping.csv](name-mapping.csv) に対応表を**追記専用**で蓄積する
  （列: `種別,元C名,新C#名,出典ファイル,意味`）。

---

## 2. リポジトリと環境

- **GitHub**: https://github.com/UchidaMasanori/EwsMigration
- **ローカル**: `EWS/EwsMigration/`（マルチルートワークスペースの一部。C 原典は同じ `EWS/` 配下）
- **.NET 9 (`net9.0`)**。共通設定は [Directory.Build.props](../Directory.Build.props)（`ImplicitUsings`/`Nullable`/`LangVersion`）、
  SDK は [global.json](../global.json) で `9.0` 固定。
- **ビルド/テスト**（`EwsMigration/` で実行）:
  ```powershell
  dotnet build Ews.Migration.sln
  dotnet test  Ews.Migration.sln
  ```
  テスト結果サマリは端末上で文字化けするが `失敗:/合計:/スキップ:` の数字は読める。

### C 原典ソースの正（重要）

- **最新の C 原典は `toku/sekkei/src`**（2025-10-30 更新）。
  旧 `toku/qrespo/sekkei/qre_sekkei/src` は同一ファイルの旧版（2024-01-26）なので**参照しない**。
- インクルードは `toku/include/sekkei/` と `toku/include/common/`。
- 主な原典ファイルと関数位置:
  - `Fyss11.c` … 回路文字列チェック上位（`Fyss11_Mojiretu_Check` / `Fyss11_Check_Main` / `Fyss11_Table_Set` ほか）
  - `Fyss1c.c` … `Check_KikimeiC`, `Find_Delimetor`
  - `Fyss1d.c` … `Check_Kikimei`, `Parm_Check_Main`（電気パラメータ→定格値パーサ、約100関数の巨大サブシステム）
  - `Fyss12.c` … `Fyss12_Make_Main`（主回路生成の 17 ステップ）, `Yoyakugo_Add_Main`, `cmp`, `Kairo_Kubun_Set`,
    `Kikitable_*_Make`, `Find_Keiki_Type`
  - `Fyss1f.c` … `Find_Keitou`（K_No 検索）, `Find_Gyosyu`（G_No 検索）

---

## 3. 文字エンコーディング（最重要・事故多発ポイント）

- **ソースコード（`.cs` / `.csv` / `.sql`）は Shift-JIS / CP932（BOM なし）** で保存する。
  旧システムの固定長・`.cns` データが CP932 前提のため。
- **UTF-8 で読み書きすると日本語コメントが U+FFFD に化けて破壊される**（過去に実際に破壊 → 復旧した）。
  PowerShell で読み書きする際は **必ず** `[System.Text.Encoding]::GetEncoding(932)` を使う。
- **ドキュメント（`.md` / `.json` / `.props` / `.editorconfig`）は UTF-8（BOM なし）**。
  GitHub が UTF-8 前提でレンダリングするため。README/本ファイルは UTF-8。
- 編集後は必ず U+FFFD 混入チェック（0 であるべき）:
  ```powershell
  $enc=[System.Text.Encoding]::GetEncoding(932)
  $t=[System.IO.File]::ReadAllText((Resolve-Path "path\to\file.cs").Path,$enc)
  [regex]::Matches($t,[char]0xFFFD).Count
  ```

### その他の落とし穴

- ワークスペースは **OneDrive 配下**。まれにエディタバッファとディスクが乖離し、
  編集がディスクへ反映されないことがある（`read_file` は編集済みに見えるが `dotnet build` は旧内容を使う）。
  疑わしいときは PowerShell の `Select-String` / `LastWriteTime` で実ディスクを確認する。
- PowerShell の cwd はツール呼び出し間で保持されないことがある。毎回 `cd` し、
  .NET の `ReadAllText` 等には `Resolve-Path` で絶対パスを渡す。
- Git コミットメッセージ（日本語）は UTF-8 のメッセージファイル経由で渡す:
  ```powershell
  $m="件名`n`n本文"; $p="COMMIT_MSG_DOC.txt"
  [System.IO.File]::WriteAllText((Join-Path $PWD $p),$m,[System.Text.UTF8Encoding]::new($false))
  git add <files>; git commit -F $p; Remove-Item $p
  ```

### name-mapping.csv の取り扱い（過去に破損事故あり）

- **追記専用**。既存全文を CP932 で読み → 末尾に行追加 → CP932 で書き戻す。LF 改行を維持。
- 編集後、必ず (1) 先頭行が `種別,元C名,新C#名,出典ファイル,意味` (2) C# 断片（`using`/`namespace`/`[Fact]`/`Assert`）が 0 件
  (3) 行数が想定通り、を確認してからコミットする。
- 実際に commit `78b1df5` で本ファイルがテスト .cs の内容で丸ごと上書きされる事故が発生し、`8dd7e1a` で復旧した。

---

## 4. ソリューション構成

```
EwsMigration/
├── Ews.Migration.sln
├── README.md / global.json / Directory.Build.props / .editorconfig / .gitignore
├── docs/
│   ├── HANDOFF.md                              ← 本ファイル
│   ├── migration-policy-pointers-and-strings.md ← ポインタ/固定長/NUL の移植方針
│   └── name-mapping.csv                        ← C ? C# 名称対応表（追記専用）
├── sql/
│   └── 001_schema.sql
├── src/
│   ├── Ews.Domain/    ← ドメイン層（作業テーブル/マスタ/値オブジェクト）。依存なし
│   ├── Ews.Data/      ← データ層（SQL Server/Dapper・.inf 構成・.cns 取込・ISAM 抽象）→ Domain
│   ├── Ews.Analysis/  ← 回路解析（toku/sekkei の移植先）→ Domain, Data
│   └── Ews.App.Batch/ ← 実行形式（DI ホスト・バッチ入口）→ Domain, Data, Analysis
└── tests/
    └── Ews.Tests/     ← xUnit
```

- ライブラリ／実行形式は `OutputType` の違いのみで、いずれも `src/` に置く（.NET の標準規約）。
- 今後 `seigyo` / `sakuzu` 等を移植する際は、C の 1 ディレクトリ＝1 プロジェクトと機械的に写さず、
  **ドメイン境界（同じ理由で一緒に変わるか）でプロジェクト粒度を決める**方針で合意済み。

---

## 5. 移植の進捗（2026-07-03 時点）

回路解析（`toku/sekkei` 系）を先行移植中。**全 207 テスト成功 / 1 スキップ / 0 失敗**。

### 移植済み

- **回路文字列チェック** `CircuitStringChecker`（`Fyss11.c`/`Fyss1c.c`/`Fyss1d.c`）
  - 系統/行種/仕様テーブル生成、盤名称・入線・有電源等の行種ディスパッチ
  - 予約語解決（`ResolveReservedWord`。特殊キー 27A/27B/27C・SL・G1-4・FLT の短絡一致）
  - 電気パラメータ → 定格値（key_tbl）格納の配線（`Check_Kikimei`→`Parm_Check_Main` 相当）
- **電気パラメータ検証** `ElectricalParameterChecker`（`Fyss1d.c` の表駆動パーサ）
  - 定格キー表・型非依存パーサ・MCB/ELB/MC/MG/THR 等の型別検証、`RatingValues` へ格納
  - 定格キー表を拡充: '/'(CT/VT付き)・特殊展開を除く単純構造の残り約70表（VS/TB/CON/GL系/CR/TS/MV/KPRY/MCFR/MGFR/STM/VVVF 等）を移植
  - 先頭数字予約語(2ERY/3ERY/4ERY)を解禁。ExtractElectricalParameter を C原典 Check_KikimeiC(Fyss1c.c)に忠実化し、予約語/電気パラメータ(d_parm)の分離を修正(MCB3P→d_parm 3P、2ERY100AF→予約語2ERY+d_parm 100AF)
  - CT/VT付き('/')定格キー表 AM/VT/CT/RTR/BLTR/PLTR/THSW/WH を追加。next_1_get(NextOneGet)を移植し副記号 n_kigo を key_check へ伝搬(消費先 key_check_WH は E.2)。残る特殊展開 TR/PT/BP は後続
  - **定格キー表の出典是正（重要）**: 検証用の定格キー表を、表示展開モジュール `FySinTkakt.h`(`t_*`/`tkak_tbl`) から検証権威である `toku/include/sekkei/fyrt810.h`(`ft_*`/`fyak_tbl`) へ全面再ベース。`fyak_tbl` 経由で `Check_1_Group` が参照する値に一致。空表(STM/SIR/C/R/D/NICA/RE/VVVF/TVZ/TVB/TVH/TVK/SPACE/AL 等)は忠実に空配列（予約語は存在=構造検証対象、非空パラメータは FY-699E）。予約語 RECB→RMCB 是正。VM/TM を追加移植、PT/BP/TR は保留。`fyak_tbl` マッピングのクセ(G→ft_g1/GI→ft_i/GP→ft_p/GPN→ft_n、SMTKP/SMTSS/SMTRY は ft_tsu 共有)を反映
- **主回路生成** `MainCircuitBuilder`（`Fyss12.c` の 17 ステップのうち）
  - step1 系統構成 / step2 行種階層 / step4 機器情報 / step5 回路区分（`Kairo_Kubun_Set`）
  - step6 `Yoyakugo_Add_Main` の前段 `D_No*=10` スケーリング / step7 D_No 昇順ソート（`qsort` 相当）
  - step8 `Gyosyu_Rank_Set` 行種ランク/出現数（`Kiki_Suryou_Set/Calc`・`Main_Exist_Check`）
  - step9-13.5 機器ランク系: `Kiki_Rank_Set`/`Kiki_Rank_Update`(TOP_Flg)/`Gyosyu_Rank_Update`(`Find_Max_Rank`)/`Pattern_Rank_Update`/`WH_Rank_Set`(改訂14)/`TR_Rank_Set`
  - step16 電気パラメータ一致チェック（`Ele_Equal_Check`）
  - step17 主回路ファイルエリア数量分解: `Fyss12_Make_Main_Sub`/`Main_File_Area_Make`（`Find_Iteration`/`Find_Nobangou`/`Find_Group` → `MainCircuitSegment`）。FYRT800 レコード整形（`mainfile_set`）は保留

### 未移植・TODO（次にやること）

- **step6 の本体**: SEP/CT/WH/ZCT 機器の挿入（`Kikitable_SEP/Keiki/Main_Make`）。
  依存: `PropChkSEPBox`/`PropChkHbnHB300`（改訂<12> の bukken FYDF801 プロパティ照会）、
  グループ別 souden の全面設定、`Find_Keitou`。消費側の後段ステップも未移植のため、
  データで検証できるようになってから着手する（推測移植は忠実性を損なうため保留）。
- **主回路生成の step14/15, 19**: `Kairo_Group_Set`(無効化) 等（TODO コメントで明示済み）。step17 は数量分解（`Find_Iteration/Nobangou/Group` + `Main_File_Area_Make`）を移植済み。FYRT800 レコード整形（`mainfile_set`）は最難関の作図系依存のため保留。
- **電気パラメータエンジンの残り型**（約90種の `key_check_XXX`）、TR 系パーサ。
- **データ層**: `.cns` マスタ取込・SQL Server スキーマの本格整備（`sql/001_schema.sql` は初期のみ）。
- **作図系（DWI）**: 最難関。DLL 化 + P/Invoke か C# 再実装かは未決。

詳細な関数単位の対応は [docs/name-mapping.csv](name-mapping.csv) を参照。

---

## 6. 移植の詳細方針

- ポインタ／`\0`（NUL 終端）・固定長文字列の扱いは
  [docs/migration-policy-pointers-and-strings.md](migration-policy-pointers-and-strings.md) に明文化済み。
  - 構造体ポインタ → クラス（参照型）、`kiki[i]`/`S_Kiki+i` → `List<T>`/配列 index、
    NULL 返却 → nullable 参照型、件数+配列 → `IReadOnlyList<T>`、`calloc/free` は移植しない（GC 任せ）。
  - 固定長/NUL は `FixedFieldCodec` 境界に隔離し、以降は通常の `string`。
  - `atoi`/`atof` の「先頭数値部のみ解釈」挙動は `AtoiC`/`AtofC` で再現（`int.Parse` では不可）。

---

## 7. このセッションを失わないための運用

- **一次バックアップ = この git リポジトリ**。作業のたびにコミットし、`git push origin main` する。
  push 済みなら PC が壊れても GitHub に残る。
- 恒久的な文脈（本ファイル・README・name-mapping.csv・migration-policy）はすべてリポジトリ内に置く
  ＝ GitHub に残る。ローカルの `/memories/` は補助であり、失われても本ファイル群から復元できる状態を保つ。
- 節目ごとに本ファイルの「5. 進捗」を更新してコミットすること。
