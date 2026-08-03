# 引き継ぎドキュメント (HANDOFF)

> このファイルは「PC が故障してもプロジェクトの文脈が失われないように」git リポジトリ内へ残す
> **引き継ぎ用の恒久メモ**です。GitHub に push されるため、ローカル環境が失われても内容は残ります。
> 作業を再開する人（人間・AI 問わず）は、まずこのファイルと [README.md](../README.md)、
> [docs/MIGRATION_PLAN.md](MIGRATION_PLAN.md)（libfysek.a 全体移行計画）、
> [docs/name-mapping.csv](name-mapping.csv) を読んでください。

最終更新: 2026-07-31

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

## 5. 移植の進捗（2026-08-03 時点）

回路解析（`toku/sekkei` 系）を先行移植中。**全 1452 テスト成功 / 0 スキップ / 0 失敗**。
`libfysek.a`（76 ソース / 約 109,800 行）の全体像とフェーズ別計画は
[docs/MIGRATION_PLAN.md](MIGRATION_PLAN.md) を参照（総量比 ~12〜15% 移植済）。
なお完全な設計出力には**制御設計 `libfysgy.a`（別ライブラリ, `toku/seigyo/src`, 52 ソース/約 67,000 行）**の
移植も必須（`Fysk10_Main` が `Fysc20_Sekkei_Control` ほか Fysc2* を呼ぶ）。総スコープ ≒ 約 177,000 行。

**フェーズ別状況（MIGRATION_PLAN.md と対応）**:
- ✅ 機器サーチ中核: Fysk00 前処理①/Fysk01 マスタ検索②(SC 除く)/Fysk02/Fysk04/Fysk11
- 🟡 入力解析 Fyss11 / 主回路 Fyss12 / 上流 Fyss14（部分）
- ❌ 制御回路 Fyss13/1k/1l・下流結線 Fyss15・制御電源 Fyss19/1p/U0・検証 Prop 群・線番 Fyss3*・Fysk10_Main 本体結線
- ❌ 制御設計 libfysgy.a（Fysc20_Sekkei_Control 系, 別ライブラリ）… 完全移植に必須

### 2026-08-03 セッション㊸ 追加分（Fyss31 負荷発生元設定 本体移植 → 主要ロジック移植完了）

`Fyss31.c`（1076 行）の未移植だったオーケストレータ本体を移植し、`Fyss31` の主要ロジックを移植完了。
`Fyss31_FukaHassei_Set`（負荷発生元設定の中核）を `LoadSourceSelector.SetLoadSource` として移植、
`set_error` を `MakeError`、SC 系統処理 `SC_Keitou_Proc`（Fyss39/Fyss3A 依存）はデリゲート境界化。
リーフ（`set_denryu`/`set_fky`/`get_ep`/`FYRT812`）は既存移植を利用。忠実移植ポイント:
CT・AM 特殊コピー、改訂 `<1><2><3><4><9>`、951005 上流遡り、1-2 型 `dt_pnt` エラー、
F(ヒューズ)特例で `searchsgy("C")` → `MainCircuitResult.SearchAgainFlag` を新設。テストは 1343 → **1353**（+10）。

### 2026-08-05 セッション㐙 追加分（Fyss36 本体オーケストレータ移植 → Fyss36 完全移植完了）

`Fyss36.c`（832 行）の未移植だった本体オーケストレータ `Fyss36_MattanKairo_Iset`（末端回路の通電電流値算出）を
`AccumulationAreaSetter.SetTerminalCircuitCurrent` として移植し、**Fyss36 を完全移植完了**（サブ関数は前セッションで移植済）。
本体は 4 ループを忠実再現: ①負荷発生元の積算セット（`SetLoadSourceAccumulation`）②通電電流0の末端へ上流伝播（`PropagateCurrentFromLoadSource`）
③先頭機器の通電電流積算（`TerminalCurrentIntegrator.IntegrateCurrent`、失敗時は `FyHcErrFunc` 相当のログデリゲート）
④950927 SC 系統積算（`Fyss3A_Chk_Yoyaku`/`Fyss3A_Prc_Seksan` をデリゲート境界化、Fyss3A 未移植のため null 時は早期 return）。
忠実性メモ: ループ②の SC/NT 予約語・スイッチ 1/2・特殊予約語 '6'(27端子台)スキップ、
ループ④の `maina[i-1]` アクセスに `i>=1` ガード（C原典は UB）、LoadName[1]=="0KW" 分岐。テストは 1353 → **1366**（+13）。

### 2026-08-05 セッション㐙 追加分（Fyss3A ＳＣ／ＮＴ上流積算移植 → Fyss3A 完全移植完了）

`Fyss3A.c`（364 行）のフル移植。ＳＣ(進相コンデンサ)／ＮＴ(中性線)の上流積算処理を
`ScNtUpstreamAccumulator`（新規静的クラス）に 6 関数全移植し、**Fyss3A を完全移植完了**。
これにより Fyss31（`SC_Keitou_Proc`）・Fyss36（ループ④）で境界化したデリゲートの実体が揃った。

- **`AccumulateScNt`**（=`Fyss3A_SC_NT_Sekisan`）: 回路要素='1' で予約語 SC/NT の各要素につき通電電流値を求め、上流積上区分='1' まで遡り積算。負荷名称 0KW は対象外（951002）。
- **`CheckReservedWord`**（=`Fyss3A_Chk_Yoyaku`）: 回路要素='1' かつ予約語 NT→(ret0,flag1)/SC→(ret0,flag2)/他→(ret1,flag0)。`(int Ret, int Flag)` タプル返却で Fyss36 の `Func<int,(int,int)>` 境界に適合。
- **`GetEnergizingCurrent`**（=`Fyss3A_Get_Tsuden`）: NT(flag1)は ep[2] 定格電流2(A2)。SC(flag2)は ep[2] 静電容量(UF)を回路電圧・相数で電流換算（単相=UF/V・三相=UF/(1.732*V)・他=UF）。UF=0 は KVAR 変換、１次側に MC 接続時は SC1次電流換算（`index>=1` ガードで C の `syu[-1]` UB 回避）。
- **`ConvertKvarToUf`**（=`Fyss3A_Chg_KvarUf`）: UF=(KVAR*1000)/(2*3.14*回路周波数*回路電圧^2*0.000001)。
- **`GetScPrimaryCurrent`**（=`Fyss3A_Get_SC_Pc`, 960329）: UF=2*3.14159*回路周波数*UF*回路電圧^2*1E-6（パイ定数が 3.14 と 3.14159 で異なる原典を忠実再現）。
- **`ProcessAccumulation`**（=`Fyss3A_Prc_Seksan`）: 上流積上区分(`StackKind`)='1' まで親データ追番-1 を添字として遡り通電電流値(`%08.2f`)をセット。NT(flag1)は使用相 `UsedPhase`="N   " もセット。
- **忠実性メモ**: C原典は配列添字ベース（データ追番が 1 始まりで配列順に連続する前提）で `oyatno-1` を次の添字とする→この添字ベースの振る舞いを踏襲。テスト`ScNtUpstreamAccumulatorTests.cs`18件。テストは 1366 → **1384**（+18）。

### 2026-08-05 セッション㐙 追加分（Fyss39 ＳＣの特殊処理移植 → Fyss39 生存パス移植完了）

`Fyss39.c`（757 行）の生存パス移植。ＳＣ(進相コンデンサ)の静電容量(UF)／定格容量(KVAR)生成処理を
`ScSpecialProcessor`（新規静的クラス）に移植。`CircuitWork` に `ScProcessedFlag`(=scflg)フィールドを追加。

- **`ProcessSc`**（=`Fyss39_SC_Proc`）: 全要素 scflg クリア → 予約語 SC で UF/KVAR 入力済みは `%08.1f`/`%06.2f` 整形して ep[2] へコピー・scflg='1'。未入力(UF=KVAR=0)はデータ追番を記録し階層番号降順ソート後、電動機容量から静電容量を算出。950928 直近負荷（負荷種類='M'なら fpalw2、空白なら同一行種の最小 ep[0].W1、無ければ上流遡りの M）を Pm に加算。`Get_Seiden` で SCuf 算出し ep[2] へ。2003.05.13 容量0(`000000.0`)時は前方/後方の非0ＳＣから流用。
- **`CheckReservedWord`**（=`Fyss39_Chk_Yoyaku`）: 系統種別='1' かつ予約語 SC の時 ep[0] の UF/KVAR を返す(ret0)、他は ret1。
- **`SortByHierarchy`**（=`Fyss39_Srt_Kaisno`）: データ追番配列を階層番号(kaisono)降順に選択ソート。
- **`GetCapacitance`**（=`Fyss39_Get_Seiden`）: 電動機容量 Pkm=pm/1000 と回路相数/電圧/周波数で係数 a/b を決め SCuf=(Pkm)^a*b（三相≤220/>220×50/60Hz容量帯・単相≤105・他）。1998.05.19 乗数変更(0.91/18.71)含む。
- **デッドコード（未移植）**: `Fyss39_Chk_Heiret`/`Get_ParmSC`/`Chg_KvarUf`/`Set_ParmSC`。SC_Proc の負荷容量合計ループ手前 951002 `continue` により `sc_flag` が常に 0 となり、並列ＳＣ分岐(`if(sc_flag==0)` の else)が到達不能のため未移植（Fyss33 Shori2 の前例に倣う）。
- **忠実性メモ**: C原典は配列添字ベース（`no-1`）で `maina[no-2]` 等 UB を含むため範囲ガードを追加。空白負荷種の探索ループ内 `if(i==j)` は datano 添字 i と配列添字 j の比較（原典どおり）。テスト`ScSpecialProcessorTests.cs`10件。テストは 1384 → **1394**（+10）。

### 2026-08-05 セッションㄐ 追加分（Fyss3F 負荷発生元の変更処理移植 → Fyss3F 完全移植完了）

`Fyss3F.c`（370 行、外部依存なし）のフル移植。負荷発生元の変更処理を
`ScSpecialProcessor` と同じ Ews.Analysis の `LoadSourceChanger`（新規静的クラス）に全移植。

- **`ChangeLoadSource`**（=`Fyss3F_Fuka_Change`）: 先頭機器（回路要素 `CircuitElement`='1' かつ先頭機器フラグ `LeadingEquipmentFlag`='1'）につき負荷発生元区分 `LoadSourceKind`='1' をセットし、使用相 `UsedPhase` 別に積算エリア `AccumulationSlots`(0:R 1:S 2:T 3:X 4:Y) の相間振替を行う。
- **相振替**: XN/YN→`XySet`(dest の 0 相に origin 複写)・RN/SN/TN→`RstSet`(origin1 優先、0 なら origin2)・RS/ST/TR→`Rst2Set`(2 相補完)。振替後に不要な相を `ClearPhase` でクリア。
- **`ClearLoadSourceFlag`**（=`clear_ahassei`）: 先頭機器の datano を親追番として `ParentSequenceNumber` が一致する子孫の `LoadSourceKind` を空白にし、再帰（nest 999 上限）で下流を輿る。
- **実装メモ**: seki_area の 7 相(a/b/c/d/e/m/s)は Get/Set アクセサ配列で反復し原典の 7×展開を集約。テスト`LoadSourceChangerTests.cs`6件。テストは 1394 → **1400**（+6）。

### 2026-08-05 セッションㄐ 追加分（Fyss38 ＮＴの特殊処理移植 → Fyss38 完全移植完了）

`Fyss38.c`（約290 行、外部依存なし）のフル移植。ＮＴ(中性線端子)の特殊処理を
Ews.Analysis の `NtSpecialProcessor`（新規静的クラス）に全移植。

- **`ProcessNt`**（=`Fyss38_NT_Proc`）: ①系統種別 `SystemKind`='1' かつ予約語 `ReservedWord`='NT' の要素につき、並列MCB極数合計(`GetPoleCountSum`)を電気パラメータ[2]極数 `P` へ（ep[0] 極数が非0ならそれを複写）。②予約語='MCB' で ep[2] 極数=1 の要素のトリップ電流 `At` の MAX を求め、使用相 `UsedPhase`[1] をスペースクリア。直列下流(同一階層・並列で直列番号が後続)の ep[0] 極数='001' は使用相[1..3] を削る。③予約語='NT' の定格電流2 `A2` が未入力("00000.000")なら MAX を、入力済ならその値を電気パラメータ[2] `A2` へセット。
- **`GetPoleCountSum`**（=`Fyss38_Get_epap`）: 親追番・階層番号一致で予約語='MCB' かつ電気パラメータ[2]極数=1 の件数を合計し、奇数なら +1 で偶数化して返す。
- **C原典UB**: 960322 の直列番号(chokuno)比較は原典 `memcmp` が長さ引数欠落(K&R)で未定義動作。直列番号の実幅である 3 バイト序数比較で決定化しコメント明記。
- **テスト**: `NtSpecialProcessorTests.cs` 8件。テストは 1400 → **1408**（+8）。

### 2026-08-05 セッションㄐ 追加分（Fyss3D 使用相決定 段階移植(1) 純粋ヘルパー群）

`Fyss3D.c`（約2900行, 改訂&lt;1&gt;～&lt;33&gt;）の使用相決定 `Fyss3D_PH_Kettei` を段階移植。
巨大かつ深く相互依存するため、まず外部依存（物件明細 hycpskbn・エラー通知 FyHcErrFunc/Err_Code_Set）
の無い純粋ヘルパー群を Ews.Analysis の `PhaseAssigner`（新規静的クラス）へ移植。

- **移植した11関数**: `Convert2PhaseTo1Phase`(=Siyousou2to1 2相→1相)／`SortByParallelNumber`(=sort3d 並列追番昇順ソート)／`CountVolt100VDevices`(=PropCount100Vkiki 100V機器index収集, 改訂&lt;7&gt;)／`SetPhase100VDevices`(=PropSetSou100Vkiki XN/YN交互, 210スキップ)／`SetPhase3P3WDevices`(=PropSetSou3P3Wkiki RS/ST/TR)／`SetPhase3P4WDevices`(=PropSetSou3P4Wkiki RN/SN/TN)／`SetPhaseMc`(=PropSetSouMC MC極数→XY/XNY, MCB/SB2P200V→210補正)／`SetPhaseMc2P`(=PropSetSouMC2P MC/TB中抜き)／`SetPhaseMc3P`(=PropSetSouMC3P MC3P直下2P交互)／`GetMcChildMaxVolt`(=PropMcChildVolt 子最大負荷電圧)／`SetParamFor2P200V`(=PropSetPrmFor2P200v XY/210)。
- **datano 依存**: `SetPhaseMc2P`/`SetPhaseMc3P`/`GetMcChildMaxVolt` は親子判定に datano を用いるため引数を `MainCircuitResult`(SequenceNumber 保持)とした。
- **C原典UB**: `SetPhaseMc3P` の入線番号～直列追番15桁キー比較は原典が `-1 == strncmp` と生バイト差に依存。AIX(char=unsigned)相当の先頭不一致バイト符号付き差を返す `StrncmpAix` を実装し `== -1`(直下=直列追番が隣接)を忠実再現。
- **残作業(後続増分)**: 統括本体 `Fyss3D_PH_Kettei`／index収集 `PropGetF800Index`/`34`/`34P`/`33`／改訂&lt;32&gt; `PropChgSiyousou`・`PropConnect3P4W`／`Fyss3D_Katagiri`・`Fyss3D_Keiki_set`・`Fyss3D_ResetRRYsou`／エラー依存チェック `PropCheckUseVolt`・`PropChkElem1P2W`・`PropChkLacslRryFuka`(FyHcErrFunc/Err_Code_Set をデリゲート境界化予定)。
- **テスト**: `PhaseAssignerTests.cs` 20件。テストは 1408 → **1428**（+20）。

### 2026-08-06 セッション 追加分（Fyss3D 使用相決定 段階移植(2) リセット/片切り/エラーチェック）

`PhaseAssigner`（既存）へ Fyss3D のリーフ/サブ関数群を追加移植。

- **移植した5関数**: `ResetRRYPhase`(=Fyss3D_ResetRRYsou 機器選定後にRRY-CTの使用相をN付き2Pへ戻し極数を2Pへ修正, 改訂&lt;26&gt;)／`AdjustKatagiriPhase`(=Fyss3D_Katagiri 片切りMC/SSWの使用相N相削除。2次側機器の有無・同一機器認識番号の共用・極数(ep0.P/kpav/kpap)からN相以降をクリア, 改訂&lt;3&gt;&lt;33&gt;)。
- **エラー依存チェック3種**（`Err_Code_Set`を `CircuitParseError?` 返却で境界化, null=正常）: `CheckElement1P2W`(=PropChkElem1P2W 分岐でCT/CS/ZS/SE/SESのエレメント数1で FY-144E)／`CheckUseVolt`(=PropCheckUseVolt 子200V・親100Vで FY-074E, 子機器の行桁)／`CheckLacslRryLoad`(=PropChkLacslRryFuka RRY-LAで極数1・200Vで FY-074E)。いずれも msgid="FYMEE90"。
- **C原典メモ**: `Fyss3D_Keiki_set` はテーブル駆動で大きく后続増分へ。同関数内に C の代入バグ `ep[2].epaqty = '2'`（== のつもりが =）あり→移植時に忟実再現か修正か要判断。
- **テスト**: `PhaseAssignerTests.cs` +16件。テストは 1428 → **1444**（+16）。

### 2026-08-06 セッション 追加分（Fyss3D 使用相決定 段階移植(3) 計器回路使用相セット）

`PhaseAssigner` へ `SetMeterCircuitPhase`(=`Fyss3D_Keiki_set` 950209)を追加移植。テーブル駆動の計器回路使用相セット。

- **テーブル**: `PwvTbl`(43行+番兵 予約語/手配数量/回路相/回路線/回路電圧→使用相)・`F2Tbl`(3行 VS/VT→RT、番兵=RS)を `readonly record struct` 配列で定義。
- **3ループ**: ①回路要素=2(CT従属 AM/WH/AS/THR): AMは DataType[2]="AS"でKKL他KL、WH/AS/THRは後述バグで常にKKL。②回路要素=3/4(計器/ヒューズ): 上流 "P" を逆行検索しその回路相kpaph/回路線kpawrと自分の予約語/手配数量(ep0.Qty)でPwvTblマッチ、1相2線は回路電圧も一致条件。使用相"F01"は次要素をF2Tbl参照(マッチ無しは番兵RS)、"X01"はoyatnoを上流逆行しF(手配数量2)でXY。他は3文字Overlay(4桁目保持)。③回路要素=5(ZCT従属 LGR/ELR)→KL。
- **★C原典バグ忠実再現**: `if(maina[i].dt.ep[2].epaqty = '2')` は == のつもりが 代入。常に真で "KL  " 分岐は到達不能、かつ CT の ep[2].Qty='2' となる副作用も発生→両方忠実再現しコメント明記。
- **C原典UBガード**: "P"未発見(j<0)は continue、i+1/iNo の配列参照は範囲ガード追加(原典は maina[-1]/maina[Pmainc] 参照でUB)。
- **テスト**: `PhaseAssignerTests.cs` +8件。テストは 1444 → **1452**（+8）。



電流パラメータ設定 `Fyss3G_Denryuu_Parm_Set`（Fyss15 の M2 スライス）を段階移植。CNS マスタ読込4種と
ブレーカ系デバイスセッタ4種、および共通下請け（`Check_fyrt800`/`Set_IM`/`PropSetELBKando`/`Fysk0e_SetELBkando`）を移植。テストは 1126 → **1151**（+25）。

- **電流パラメータ表モデル（commit 予定）**: `src/Ews.Domain/Analysis/CurrentParameterTables.cs`＝`struct prmtp`→`ParameterSettingType`（予約語/seq_no/prm_tp/cod[10]）／`struct sqset`→`WireSizeSetting`（電線サイズ/許容電流/選定フラグ）／`struct a2set`→`RatedCurrent2Setting`（負荷種類/相/電圧/係数）／`struct a1set`→`RatedCurrent1Setting`（定格電流）。出典 `toku/include/sekkei/fyss3g01.h`。
- **CNS ローダー4種（commit 予定）**: `src/Ews.Data/Seeding/CurrentParameterTableLoader.cs`＝`Fyss3G_CnsPrmtpRead`/`CnsSQsetRead`/`CnsA2setRead`/`CnsA1setRead`（amp001〜004.cns）。ヘッダ2行スキップ＋CP932読込。C の `sscanf` を**先頭失敗で全変換打切り**まで忠実再現（`ReadFixedWidthToken`/`TrimAtComma`/`SkipLiteralComma`/`TryScanDouble`/`TryScanInt`）。A2SET の「HA 行が %lf 失敗→係数/相を前行キャリー」癖を再現。A1SET のコメント行(`/* 3.0 */`)がデータ化して先頭0.0になる癖も忠実。
- **ブレーカ系セッタ4種（commit 予定）**: `src/Ews.Analysis/CurrentParameterSetter.cs`＝`Fyss3G_Set_MCB`/`Set_ELB`/`Set_MMCB`/`Set_ELMB`。共通下請け `Check_fyrt800`（=`ComputeParameterFlags` パラメータ設定要否 p1/p2）／`Set_IM`（=`ComputeInductionMotorCurrent` 誘導電動機電流）／`PropSetELBKando`（=`SetElbSensitivity` 親機器の相から ELB 感度電流）／`Fysk0e_SetELBkando`（=`ApplyElbSensitivity`、Ma[0]=0015/0030/0100/0200）。親機器探索は既存 `ParentEquipmentLocator.FindParentPRow` を再利用（親不在は早期 return でガード＝C原典は親存在前提のため意図的な相違）。**C原典の `dwork == Stof(...)`（== 誤記で no-op）バグを Set_MCB HPSB ブロックで忠実再現**（コメントで明示）。
- **忠実性メモ**: `eparm_set` が ep[0] を整形ゼロ（"00000.000"/"0000000.00"）で埋めるため、"未入力" 判定の memcmp が生ゼロ（"000000000"）と一致しない点を再現。テストも整形ゼロで期待値を組む。
- **保留（Fyss3G 残）**: CNS Seek 関数群（`PrmtpSeek`/`SQsetSeek`/`A2setSeek`/`A1setSeek`）／ディスパッチャ本体 `Fyss3G_Denryuu_Parm_Set`／`Check_fyrt812`／非ブレーカ系デバイスセッタ（MC/THR/MG/WH/AM/CT/TB/CON/TR 等）。順次移植後にオーケストレータへ結線。

### 2026-07-31 セッション⑬ 追加分（Fyss3G CNS Seek 関数群4種＋Check_fyrt812）

電流パラメータ設定 `Fyss3G_Denryuu_Parm_Set` の CNS テーブル検索インフラを移植。ローダー（セッション⑫）が
構築した `IReadOnlyList` を C の線形リストと同順で走査する純関数として実装。テストは 1151 → **1170**（+19）。

- **CNS Seek 4種＋Check_fyrt812（commit 予定）**: `src/Ews.Analysis/CurrentParameterTableSeeker.cs`（新規静的クラス）。
  - `SeekParameterSettingType`（=`Fyss3G_CnsPrmtpSeek`）: 予約語8バイト右詰め一致の最初の `ParameterSettingType`（無しは null）。ディスパッチャが `prm_tp` 取得に使用。
  - `SeekWireSize`（=`Fyss3G_CnsSQsetSeek`）: `key=通電電流*1.12`、許容電流≧key かつ選定フラグ0 の最初の電線サイズ（無しは0）。`Set_TB` が使用予定。
  - `SeekRatedCurrent2Coefficient`（=`Fyss3G_CnsA2setSeek`）: 負荷種類2バイト一致・回路電圧＞対象・相数（全相'\0' or 一致）の係数（無しは1）。`Set_MC`/`Set_MG` が使用予定。
  - `SeekRatedCurrent1`（=`Fyss3G_CnsA1setSeek`）: key超の最初の定格電流、該当無しは末尾ノードの定格電流。`Set_WH`/`Set_AM`/`Set_CT` が使用予定。
  - `CheckLoadCapacityTable`（=`Fyss3G_Check_fyrt812`）: ブレーカ系14予約語（MCB/ELB/MMCB/ELMB/SB/RMCB/RELB/RMMCB/RELMB/NHMB/HPSB/HSB/CP/CKS, 94.09.27 追加フィルタ）に一致し機器選定区分'1'・負荷種類非空白なら1（ディスパッチャで continue）。既存 `LoadCapacityDecisionTable`（=fyrt812）を走査。
- **忠実性メモ**: `A2setSeek` の機器選定区分は C 原典が `rt800->wk`（=`&rt800[0]` 呼出のため先頭 records[0].Work）を参照する癖を忠実再現（対象添字 `no` ではなく先頭データの区分）。`A1setSeek` の「全ノード key 以下→末尾定格返却」も再現。予約語/負荷種類の memcmp は右詰め空白比較（`PadReservedWord`/`PadLoadKind`）で再現。
- **保留（Fyss3G 残）**: ディスパッチャ本体 `Fyss3G_Denryuu_Parm_Set`／非ブレーカ系デバイスセッタ（MC/THR/MG/WH/AM/CT/TB/CON/TR/ELR/LGR/RRY/MCDT/F/TS/SU/DCPW/SSW/CKS/L 等）。Seek 群はこれら移植時に結線。

### 2026-07-31 セッション⑭ 追加分（Fyss3G 非ブレーカ系セッタ: THR/MG/WH）

電流パラメータ設定 `Fyss3G_Denryuu_Parm_Set` の非ブレーカ系デバイスセッタのうち、セッション⑬で移植した
CNS Seek 群を消費する3種を移植。既存 `CurrentParameterSetter` に追加（Set_IM/整形ヘルパを再利用）。テストは 1170 → **1188**（+18）。

- **THR/MG/WH セッタ（commit 予定）**: `src/Ews.Analysis/CurrentParameterSetter.cs` に追加。
  - `SetThr`（=`Fyss3G_Set_THR`, THR/2ERY/3ERY/4ERY）: 主回路（`kiryoso=='1'`）は ep[2].AT に通電電流。ep[0].AT 未設定かつ ep[0].W1 有は `Set_IM` で ep[1].AT を算出。負荷発生元は ep[1]→ep[2] コピー。Seek 不使用。
  - `SetMg`（=`Fyss3G_Set_MG`, MG/MGFR/MGSD/MGFRSD）: ep[2].AT＝通電電流、ep[2].A2＝通電電流と `A2setSeek` 係数の積。ep[1] は W1 有で `Set_IM`、無しは ep[2] コピー。
  - `SetWh`（=`Fyss3G_Set_WH`, WH）: 回路要素で分岐。主回路は A1 初期化＋A2 を通電電流(40 以下 30A/超 120A)で決定、計器回路(CT付き)は A1＝`A1setSeek`／A2＝5A 固定。param1 は主回路 A2 を ep[0].A2 で 30/120（150 超は据え置き=C の else コメントアウト再現）。
- **忠実性メモ**: denryu は `%08.2f`（8桁）格納のため、C の `memcpy("00000.000",9)`→`memcpy(denryu,8)` は末尾 '0' が残り `%09.3f` 相当の9桁になる（`AtFromEnergizingCurrent` ヘルパで再現）。回路相数→Set_IM 負荷種別（'3'→1/'1'→2、他は C 未初期化のため決定性 0）は `PhaseToLoadKind` で再現。ep[1] 判定の memcmp は整形ゼロ（"00000.000"）比較のため、テストは ep[1] を整形ゼロで明示設定。
- **保留（Fyss3G 残）**: ディスパッチャ本体 `Fyss3G_Denryuu_Parm_Set`／残デバイスセッタ（MC[Set_MC_SC は Fyss35 下流抽出依存]/AM/CT/TB/CON/TR/ELR/LGR/RRY/MCDT/F/TS/SU/DCPW/SSW/CKS/L 等）。

### 2026-07-31 セッション⑩ 追加分（Fyss3G リーフセッタ10種: CON/MCDT/F/ELR/LGR/TS/SU/SSW/CKS/L）

電流パラメータ設定 `Fyss3G_Denryuu_Parm_Set` の非ブレーカ系デバイスセッタのうち、下流抽出やゾーンコードなど
外部依存のない自完結な10種（リーフセッタ群）を一括移植。既存 `CurrentParameterSetter` に追加。テストは 1188 → **1208**（+20）。

- **リーフセッタ10種（commit 予定）**: `src/Ews.Analysis/CurrentParameterSetter.cs` に追加。いずれも ep[2].A2/MA を中心に設定。
  - `SetCon`（=`Fyss3G_Set_CON`, CON/ZCT）/`SetSsw`（=`Fyss3G_Set_SSW`, SSW/LSW/DSW/TSW）: ep[2].A2 に通電電流値。
  - `SetMcdt`（=`Fyss3G_Set_MCDT`）: ep[2].A2 = 通電電流値×1.25。`SetF`（=`Fyss3G_Set_F`）: 3A未満は3A/以上は通電電流値。`SetL`（=`Fyss3G_Set_L`）: 40A未満は30A/以上は60A。
  - `SetElr`（=`Fyss3G_Set_ELR`）: ep[0].MA 未設定時に通電100A以下30mA/超200mA。`SetLgr`（=`Fyss3G_Set_LGR`）: ep[0].MA 未設定時に200mA。
  - `SetTs`（=`Fyss3G_Set_TS`）/`SetSu`（=`Fyss3G_Set_SU`, PBSU/COSU/2COSU/OLU）: A2 を固定値（15A/1.5A）に設定し、prm1==0 で ep[1].A2 に ep[0].A2 を複写。
  - `SetCks`（=`Fyss3G_Set_CKS`）: prm2==0 のとき、設定電流(setteii)!=0 はその値、それ以外は通電電流値を整形して A2 に設定。
- **ヘルパリネーム変更**: A2 でも使うため `AtFromEnergizingCurrent` → `EnergizingCurrentToNine` にリネーム（機能不変、SetThr/SetMg の呼出しも更新）。
- **移植省略**: `Fyss3G_Set_DCPW` は C 原典が空関数（no-op）のため移植しない（ディスパッチャで no-op 扱い）。

### 2026-08-03 セッション⑫ 追加分（Fyss3G 依存付きセッタ: TB/TR/RRY）

電流パラメータ設定 `Fyss3G_Denryuu_Parm_Set` の非ブレーカ系デバイスセッタのうち、外部依存を伴う３種を移植。
既存 `CurrentParameterSetter` に追加。テストは 1208 → **1224**（+16）。

- **TB/TR/RRY セッタ（commit 予定）**: `src/Ews.Analysis/CurrentParameterSetter.cs` に追加。
  - `SetTb`（=`Fyss3G_Set_TB`, TB/LGT）: ep[2].A2 に通電電流値。電線サイズ SQ は `CurrentParameterTableSeeker.SeekWireSize`（=`Fyss3G_CnsSQsetSeek`）で決定。改訂<9> 動力電源(fpalwkbn=='W')かつ三相(kpaph=='3')は負荷容量帯（2200〜5500→5500〜11000→7.1/11000超→7.5 相当）で通電電流値を補正。改訂<7> 26.669〜26.876 帯は 30.1 に補正。LGT は SQ 非設定で終了。負荷発生元(ahassei=='1')は ep[2].SQ に ep[1].SQ を複写。
  - `SetTr`（=`Fyss3G_Set_TR`, TR）: datatype[0] 未設定時は VA≦500 で "RO" を設定。ep[2].VA 未設定時は下流抽出 `DownstreamSelector.SelectDownstream`（=`Fyss35_Select_Karyu_Sub`）で負荷発生元の負荷種類 "M " を積算し VA を決定。
  - `SetRry`（=`Fyss3G_Set_RRY`, RRY）: 改訂<1> LACSL(datatype[1]=="LA ")は ep[1].A2＝16A 固定で終了。それ以外は親(oyatno)を遡り、同一階層(kaisono)かつ ep[0].AT 設定済の親の AT を ep[2].A2 に採用。見つからなければ通電電流値を A2 に設定。
- **忠実性メモ**: `Set_TR` の C 原典は `if(ret = -1)`（== の誤記の代入）で常に真となり、VA は常に `lw2*1.5` 分岐を通る（`denryu*kpav*1.2` の else は死コード）。この挙動を忠実再現し else 節は移植しない。`Set_TR` の VA 参照元は ep[0].VA を判定しつつ非ゼロ時 ep[1].VA を読む原典の癖も再現。
- **保留（Fyss3G 残）**: ディスパッチャ本体 `Fyss3G_Denryuu_Parm_Set`／残デバイスセッタ（MC[Set_MC_SC は Fyss35 依存]/AM[ゾーンコード gyocd]/CT[同一機器複数ループ]）。
- **保留（Fyss3G 残）**: ディスパッチャ本体 `Fyss3G_Denryuu_Parm_Set`／複雑なセッタ（AM[ゾーンコード/gyocd]/CT[同一機器多重ループ]/TB[SQseek+改訂]/TR[Fyss35 下流抽出]/RRY[親 oyatno 遡行]/MC[Set_MC_SC]）。

### 2026-08-03 セッション⑬ 追加分（Fyss3G 依存付きセッタ: AM）

電流パラメータ設定 `Fyss3G_Denryuu_Parm_Set` のメーカー定格電流セッタ AM を移植。
既存 `CurrentParameterSetter` に追加。テストは 1224 → **1238**（+14）。

- **AM セッタ（commit 予定）**: `src/Ews.Analysis/CurrentParameterSetter.cs` に `SetAm`（=`Fyss3G_Set_AM`）を追加。
  - **タイプ[0] 延長目盛り**: datatype[0] 未設定時のみ決定。行種コード(gyocd)が B/O/M/S かつ同一行種・同一行種グループ(gyoglno)の機器を辿り負荷種類=="M "(電動機)なら "3BK"、行種が変われば打ち切り。決まらなければ "NBK"。改訂<5> 負荷種類=="H "(ヒータ)で同一系統番号(kno)かつ回路相数=='3' の機器があれば "3BK"。改訂<11>/<12> 特定ゾーン(78007/01212/98025/98024/98026)で datatype[2]!="AS" なら "3BK"（C 原典の内側 for は冪等ループのため単一判定と等価）。
  - **タイプ[6] 電源種別**: datatype[6] 未設定時に回路電圧 AC/DC 区分(kpavkbn)で "AC"/"DC"。
  - **ep[2] 設定**: 主回路(kiryoso=='1')は A1 初期化・A2＝通電電流×1.2 に製作仕様別境界補正（改訂<3> 河村標準=1: 0.7921〜1.00→1.01 ほか 6 帯、改訂<6> 公共=0: 10.000〜11.148→10.00 ほか 3 帯）。計器回路 CT 付き(kiryoso=='2')は A1＝A1SET 検索(1.2 倍後・`SeekRatedCurrent1`)、A2＝5 固定。
  - **ep[1] 再設定（prm1==0）**: CT 付きで ep[1].A1 非ゼロなら定格電流を再検索(1.2 倍なし)。負荷発生元(ahassei=='1')は ep[2].A1 に ep[1].A1 を複写。
- **依存**: ゾーンコードは C の `FyGetZoneCD` を `SetAm(..., string zoneCode)` 引数化（既存 `IRuntimeParameterProvider.ZoneCode` から供給）。定格電流１検索は既存 `CurrentParameterTableSeeker.SeekRatedCurrent1`。
- **保留（Fyss3G 残）**: ディスパッチャ本体 `Fyss3G_Denryuu_Parm_Set`／残デバイスセッタ（MC[Set_MC_SC は Fyss35 依存]/CT[同一機器多重ループ]）。

### 2026-08-03 セッション⑮ 追加分（Fyss3G ディスパッチャ本体 → Fyss3G 完全移植完了）

電流パラメータ設定 `Fyss3G_Denryuu_Parm_Set` の**ディスパッチャ本体**を移植（Fyss3G の最終ピース）。系統種別・回路要素・負荷容量表チェックでフィルタし、`prm_tp` を検索して予約語別セッタへ振り分ける。テストは 1258 → **1279**（+21）。**これで Fyss3G は 100% 移植完了**。

- **ディスパッチャ本体（commit 予定）**: `src/Ews.Analysis/CurrentParameterDispatcher.cs`（新規静的クラス `CurrentParameterDispatcher`）。`DispatchCurrentParameters`＝`Fyss3G_Denryuu_Parm_Set`。
  - **引数注入**: CNS テーブル4種（`prmtp_cp`/`sqset_cp`/`a2set_cp`/`a1set_cp`）・製作仕様区分（`sshiykbn`→`seisakusiyou`＝先頭"01"判定）・ゾーンコード（`FyGetZoneCD`）・`kubun`（'M'＝主回路/他＝計器回路）を外部から受け取り、原典のグローバル依存を明示化。
  - **フィルタ3段**: ①系統種別 `SystemKind!='1'` は skip。②`kubun=='M'` は `CircuitElement!='1'` を skip、計器側は `CircuitElement=='1'` を skip。③`CheckLoadCapacityTable`（=`Check_fyrt812`）が非0（ブレーカ系で機器選定区分1かつ負荷容量表該当）なら skip。
  - **prm_tp 振り分け**: `SeekParameterSettingType` で `prm_tp` を取得し `switch`。MCB/ELB/MMCB/ELMB/MC/THR/MG/WH/AM/CT/TB/CON/TR/ELR/LGR/RRY/MCDT/F/TS/SU/SSW/CKS/L を対応セッタへ結線（`ComputeParameterFlags` の prm1 を各セッタへ受け渡し）。
- **忠実性メモ（重大）**: 
  - **MGFR(=72)** は MG 系 case（MG/MGSD/MGFRSD）に**含まれない**ため既定 no-op（原典どおり）。
  - **DCPW** は原典 `Set_DCPW` が空関数のため明示 no-op。
  - **CKS** は原典 `Set_CKS` が仮引数名 `prm2` で定義されるが呼出側は `prm1` を渡すため、C# でも `prm1` を渡す（`SetCks` の仮引数名 `parameter2SetRequired` に惑わされない）。
  - `ComputeParameterFlags` は prm2 も算出するが全 case が prm1 のみ使用するため prm2 は破棄（`out _`）。原典も prm2 を渡さない。
- **完了（Fyss3G）**: Seek/Check/全デバイスセッタ/ディスパッチャ本体を結線し **Fyss3G は完全移植完了**。次候補は `Fyss3B_Breaker_Sentei`（機器選定本体）/`Fyss3R`/`Fyss31` 本体、順次移植後にオーケストレータへ結線。

### 2026-08-03 セッション⑯ 追加分（Fyss3B フラグ設定部）

末端回路ブレーカ機器選定 `Fyss3B_Breaker_Sentei` の**フラグ設定部**を移植。各主回路レコードに対し、機器選定指示フラグ `ksflg` と機器サーチフラグ `kikisflg` を設定する。テストは 1279 → **1302**（+23）。

- **フラグ設定部（commit 予定）**: `src/Ews.Analysis/TerminalBreakerSelector.cs`（新規静的クラス）。`PrepareSelectionFlags`＝`Fyss3B_Breaker_Sentei` のフラグ設定ループ。
  - **第1条件**: 負荷容量決定テーブル該当予約語（MCB/ELB/MMCB/ELMB/SB/RMCB/RELB/RMMCB/RELMB/NHMB/HPSB/HSB/CP/CKSの14語。**MC は明示除外**）かつ外部取付なし（`ExternalMountKind==' '`）・機器選定区分`EquipmentSelectionKind=='1'`・負荷種類非空白なら `ksflg`/`kikisflg` を '1' に設定。
  - **第2条件**: 先頭機器フラグ `sentflg=='1'`（`LeadingEquipmentFlag`）かつ外部取付なしなら、予約語（MC 含む）・負荷種類に関わらずフラグを '1' に設定。
  - **クリア**: どちらの条件も非成立なら両フラグを ' ' に（事前値をクリア）。
- **ドメイン拡張**: `MainCircuitData` に `EquipmentSearchFlag`（=kikisflg）、`WorkArea` に `EquipmentSelectionKind`/`SelectionInstructionFlag`（=ksflg）を追加。
- **保留（Fyss3B 残）**: ブレーカ選定本体（機器サーチ・品番決定）は未移植。本増分はフラグ設定部のみ。

### 2026-08-03 セッション⑰ 追加分（Fyss3B 本体オーケストレータ → Fyss3B 完全移植完了）

末端回路ブレーカ機器選定 `Fyss3B_Breaker_Sentei` の**本体（制御フロー）**を移植。C 原典の Fyss3B 固有ロジックは「フラグ設定 2 ループ → `Fysk00_Kikisearch_SY` へ委譲 → ret 返却」のみで、主回路機器サーチ本体は Fysk00.c（12k 行）にあり Fyss3B にはない。テストは 1302 → **1307**（+5）。**これで Fyss3B 固有ロジックは 100% 移植完了**。

- **本体オーケストレータ（commit 予定）**: `src/Ews.Analysis/TerminalBreakerSelector.cs` に `SelectBreakers`（=`Fyss3B_Breaker_Sentei`）を追加。`PrepareSelectionFlags` でフラグ設定後、主回路機器サーチへ委譲し ret（0:正常/1:異常）をそのまま返す。
- **移植境界（重要）**: `Fysk00_Kikisearch_SY`（Fysk00.c, 大規模 ISAM 依存の機器選定本体・品番決定）は未移植のため、`Func<IReadOnlyList<MainCircuitResult>, int>` デリゲートで注入する境界とした。コードベースはインタフェースを使わず引数注入（CNS テーブル・ゾーンコード等）で外部依存を表現する態標のため、それに合わせた（コードベース全体に interface は 0 件）。
- **テスト**: `TerminalBreakerSelectorTests.cs` +5（フラグ設定後にサーチ呼出/ret そのまま返却 Theory2/records null/equipmentSearch null）。

### 2026-08-03 セッション⑱ 追加分（Fyss3R 基盤部: プラグインタイプ判定＋電源分岐グルーピング）

特注盤プラグインブレーカ結線処理 `Fyss3R.c` の基盤部を移植。プラグインタイプ照合と電源・分岐グルーピングを実装する。テストは 1307 → **1325**（+18）。

- **プラグインタイプ照合（commit 予定）**: `src/Ews.Analysis/PlugInBreakerConnector.cs`（新規静的クラス）の `IsPlugInType`＝`FyHcPlugInJdgType`（cmnplugin.c）。`datatype[0]` の末尾空白を除去し、有効プラグインタイプ（ハーフサイズ CTP/CH/CHP・KC タイプ KP）に一致で真。改訂&lt;2&gt; で無効化された CSP/CP/CPP/BB/CV/FL は非対象。C 原典の 0:照合OK/-1:NG を真偽に読み替え。
- **電源分岐グルーピング（commit 予定）**: `GroupBySource`＝`PropGrouping`（Fyss3R.c）。予約語 "P "（電源）区切りで走査し、プラグインタイプ先頭文字（'K':アダプタ / 他:'C' ハーフサイズ）が連続する範囲を 1 グループ化。改訂&lt;2&gt;: プラグインを含むグループでのみ新グループ境界を前進（`p_ari` ゲート）。返却は全グループ列＋有効グループ数（呼出元は先頭 GroupCount 件を参照）。
  - **忠実性メモ**: `st_idx==0` を「未設定」の番兵として扱う C 原典の癖をそのまま再現。電源 "P " が先行せず `j==-1` のままのプラグインは C では `grp[-1]` 書込（未定義動作）だが、実データは電源先行前提のためスキップにした。

### 2026-08-03 セッション⑧ 追加分（Fyss3R 結線処理: PropSetSouFor1sou/3sou/Kes_Set）

`Fyss3R.c` の結線処理を移植。電源・分岐グループごとに使用相 siyouso（新 `MainCircuitData.UsedPhase`）・回路電圧 kpav[0]・機器タイプ datatype[3] をセットする。テストは 1325 → **1334**（+9）。

- **結線処理本体**: `PlugInBreakerConnector.SetConnection`＝`Fyss3R_TokuPlugIn_Kes_Set`。`GroupBySource` でグループ分けし、電源相線 13（単相3線）なら `SetSinglePhaseConnection`、33（三相3線）なら `SetThreePhaseConnection` を呼び、対象範囲のレコードをその場更新する。
- **単相 `PropSetSouFor1sou`**: CV付（CT付）ブレーカの電圧・NOTHING指定有無で kpav[0]（210:RT相結線/105:RN・TN相結線）を決め、接続相タイプ未入力機器は XN/YN を交互、入力済 RN/TN 機器は対応使用相をセット。改訂&lt;3&gt; で CTP はスキップ。
- **三相 `PropSetSouFor3sou`**: kpav[0]=="210" かつ極数 epap!="003" の機器のみ処理。アラームなしCHP・接続相タイプRN/TN・NOTHING指定有無に応じ使用相（RS/ST/RT）を順割当。NOTHING指定無は datatype[3] も RN/TN/NOTHING を順セット。
- **移植境界（重要）**: `PropJdgNothing`（回路記述ファイル FYDF805 の自由文字に "NOTHING" があるか検索）は未移植のため、`Func<MainCircuitResult, bool>`（true＝指定有）で引数注入し境界化。interface は使わない態標に合わせた。
- **ドメイン追加**: `MainCircuitData.UsedPhase`＝siyouso[4]（使用相 R/S/T/N/X/Y）。
- **テスト**: `PlugInBreakerConnectorTests.cs` +9（単相3/三相4/nullガード2）。

### 2026-08-03 セッション⑧ 追加分（Fyss3R 主幹チェック: MainChk → Fyss3R 完全移植完了）

`Fyss3R.c` の残り `Fyss3R_TokuPlugIn_MainChk`（主幹チェック）を移植し、**Fyss3R は完全移植完了**。テストは 1334 → **1343**（+9）。

- **主幹チェック**: `PlugInBreakerConnector.CheckMainBreaker`＝`Fyss3R_TokuPlugIn_MainChk`。主回路エリアを走査し、プラグイン（親同一なら 1 回）の親機器についてトリップ電流（単相&gt;250AT / 三相&gt;400AT【改訂&lt;4&gt;】で NG）・極数（3P 以外 NG）・メーカー（ELB/MCB は三菱 "M  "/"MN " 以外 NG）を検査。NG で "FY-957E"（メッセージ FYMEE80）を返す。正常は null。改訂&lt;3&gt; で CTP はスキップ。
- **移植境界（重要）**: 親機器検索 `LibTreeSrch`（未移植の汎用二分探索）は `Func<string, MainCircuitResult?>`（キー＝親データ追番 oyatno）で引数注入し境界化。エラーは `Err_Code_Set` 相当で `CircuitParseError`（=FYRT805）を戻り値として返却（*Perrc/Perra の副作用を返値化）。トリップ電流のパースは `EquipmentParameterFormatter.Stof`（=LibCharToDouble 相当）。
- **テスト**: `PlugInBreakerConnectorTests.cs` +9（全OK/三相400超過/単相250超過/極数3P以外/ELB非三菱/三菱MN OK/CTP・非プラグイン・親なしスキップ/nullガード2）。
- **ドメイン追加**: `Ews.Domain/Analysis/PlugInGroup.cs`（=`struct grp_plug`）。SourcePhaseWire（sousen）/Type（'C'/'K'）/StartIndex/EndIndex。
- **保留（Fyss3R 残）**: 単相/三相の結線相セット（PropSetSouFor1sou/3sou, siyouso/kpav/datatype[3] 設定）・NOTHING 判定（PropJdgNothing, 回路記述ファイル FYDF805 依存）・主幹チェック（Fyss3R_TokuPlugIn_MainChk, LibTreeSrch/Err_Code_Set 依存）・結線オーケストレータ（Fyss3R_TokuPlugIn_Kes_Set）は未移植。

### 2026-08-03 セッション⑭ 追加分（Fyss3G 依存付きセッタ: CT/MC）

電流パラメータ設定 `Fyss3G_Denryuu_Parm_Set` の変流器/電磁接触器セッタ CT/MC を移植（Fyss3G 非ディスパッチャ最終分）。
既存 `CurrentParameterSetter` に追加。テストは 1238 → **1258**（+20）。

- **CT/MC セッタ（commit 予定）**: `src/Ews.Analysis/CurrentParameterSetter.cs` に追加。
  - `SetCt`（=`Fyss3G_Set_CT`, CT）: 主回路(kiryoso=='1')は ep[2].A1＝`SeekRatedCurrent1` 検索値。同一階層(kaisono)に予約語 AM の回路要素2があれば通電電流を 1.2 倍してから検索。計器回路 CT 付き(kiryoso=='2')は A2＝5 固定。prm1==0 では ep[1].A1 を検索設定し、同一機器認識番号(ninsno)の相手と ep[2].A1/A2 を相互補完。負荷発生元(ahassei=='1')は ep[2].A1←ep[1].A1。**1996.07.25 追加処理**（prm1==0）: ep[0].A1 未入力かつ計器回路の相手(回路要素2)経由で予約語 WH/AM の ep[0].A1 を ep[0]/ep[1].A1 へ継承。**改訂<4>**（prm1==0）: 同一系統番号(kno)・同一親データ追番(oyatno)の WH で ep[0].VA 未入力なら ep[1]/ep[2].VA＝15VA。
  - `SetMc`（=`Fyss3G_Set_MC`, MC）: ep[2] は `SetMcSc`（=`Fyss3G_Set_MC_SC`, private）が SC 有り時に処理。SC 無しは INVBP(特殊予約語区分=='7', 改訂<13>)なら負荷容量帯で A2 強制（≦2.20→13 … ≦22.00→100/超→125）、他は通電電流×`SeekRatedCurrent2Coefficient` 係数。prm1==0 では ep[0].A2 未設定かつ ep[0].W1 有で ep[1].A2 を `ComputeInductionMotorCurrent` から算出（条件は ep[0].W1・算出は ep[1].W1 を参照する原典の癖を再現）。負荷発生元は ep[2].A2←ep[1].A2。
  - `SetMcSc`（=`Fyss3G_Set_MC_SC`, private）: `DownstreamSelector.SelectDownstream`（=`Fyss35_Select_Karyu_Sub`）で下流を辿り予約語 SC を探索。製作仕様=="01" は kpav(回路電圧)≦220 で `pow(denryu,0.939)*1.65`／超で `denryu*1.2`。それ以外は SC 並列の三相/単相負荷容量帯で A2 を算出。
- **忠実性メモ（重大）**: `Set_MC_SC` の C 原典は `sscanf("%lf", &kpav_lp)` で double(8byte) を SHORT(2byte) `kpav_lp` に書く未定義動作を含む。**本番＝AIX ビッグエンディアン**（repo メモリ）のため SHORT は IEEE754 double の上位16ビットを捕捉し、現実的な非ゼロ電圧では常に >220 となる。`DoubleHighBitsBE(v)=(short)(bits>>48)` で本番バイナリ挙動を忠実再現（golden data 整合）。CT の1周目 lp_1/lp_2 ループは読み捨て変数のみの死コードのため移植せず（コメントで明示）。ElectricalParameters 既定 "000000000" はドット付きゼロ定数と不一致＝"設定済"扱いのため、テストは未入力を "00000.000" で明示。
- **完了（Fyss3G）**: 非ディスパッチャのデバイスセッタは全移植完了。**残るは唯一ディスパッチャ本体 `Fyss3G_Denryuu_Parm_Set`**（Seek/Check/各セッタを結線）。

### 2026-07-31 セッション⑤ 追加分（②マスタ検索の中間結線層 ①〜⑤）

`Fysk01_Kikisearch_S1`（マスタ検索）の中間層を移植し、前処理（Fysk00）と既存の孤立部品
（`NearestRankSearch`/`RatingValueChecker`/`EquipmentSelector`/`RatingKeyBuilder`）を結線した。
機器選定が S1 から end-to-end で疎通（汎用・遮断器・MC/MG/THR・PBS・CT）。テストは 974 → **1004**。
詳細は [docs/name-mapping.csv](name-mapping.csv) と `/memories/repo/ews-migration-roadmap.md`。

- **入力チェック（①, d384a65）**: `ElectricalParameterInputChecker`（=`Fysk0a_EparInput_Check`）。電気パラメータ入力有無 sfg と epno(1/2) を求める。
- **形状タイプ展開（②a, a0bc506）**: `ShapeTypeExpander.Expand/ExpandSecondary`（=`Fysk01_Type_Check2/Type_Check3`＋type_tbl2/tbl3）。予約語別に代替タイプ列へ展開。
- **S1/Chokisearch/ALL 結線（②b, 1d8c5b2）**: `NearestRankSelector.SelectMain/Dispatch/SearchGeneral`（=`Fysk01_Kikisearch_S1`/`Chokisearch`/`Chokisearch_ALL`）。epno判定→キー構築→前方一致検索→定格値チェック→戻り値1/2/3/4。
- **遮断器検索（③, 358a757）**: `SearchBreaker`（=`Chokisearch_BRK`）。二次形状→形状→メーカーの統一順で検索。
- **MC/MG/THR 検索（④, 3f30de9）**: `NearestRankSearch.SearchMotorGroup`（=`Chokkin_Read_Check_MTG`, proc別best選定）＋`SearchMotorSwitch`（=`Chokisearch_MTG`, `EquipmentSelector.CompareCandidate` でcross-key選定）。
- **PBS/CT 検索（⑤, 0bac3bc）**: `SearchPushButton`（=`Chokisearch_PBS`, ti3展開込み）＋`SearchCurrentTransformer`（=`Chokisearch_CT`, 定格電流をn倍スケール探索）。
- **保留（②残）**: SC検索（`Fysk02_Check_Teichi_SC2`/`Chokkin_Read_Check2`/`PropSelChkSc` 未移植）、MC/MG容量選定フィルタ（`PropSelChkMcMg` 系＋cns容量表）、CT/AM/SC の LW 選定パラメータ（`PropSelChk*` cns）、接点計算（`Get_Seten_GoodData`, 制御回路）、`Kikisearch_S2/T/P`。`PropSetSmartType`(①保留)は S1 結線済みで移植可能に。

### 2026-07-31 セッション⑦ 追加分（M2: Fyss15 下流パラメータ生成の着手）

下流パラメータ生成 `Fyss15_Make_LowerParm`（機器サーチを実パイプラインへ結線するマイルストーン M2）の
**先頭ステップ群**を移植。オーケストレータ本体（約20サブ関数）は各サブを順次移植してから組み立てる方針。テストは 1018 → **1126**。

- **末端区分セット（commit 予定）**: `src/Ews.Analysis/TerminalKindSetter.cs`＝`Fyss30_MattanKubun_Set`（Fyss15 の第1ステップ）。P系統で自 datano が親 oyatno に不在なら末端(mattan='1')。SC 単独末端は直前直列機器へ付け直し（`ReattachTerminalToPrevious`）。直前が MC かつ同一行種グループに MGSD/MCSD がある場合は付け直さない（SC より積算するため）。階層/並列一致時は付属パラメータ（fpalw1/2/kbn・fpaln[0..1]）を移送、不一致時は後方に有効負荷が無い場合のみ付け直す。
- **上流積み上げ区分セット（commit 予定）**: `src/Ews.Analysis/UpstreamStackingKindSetter.cs`＝`Fyss32_SC_NT_Tumiage_Set`（Fyss15 第2ステップ）。jagekbn を 'K'→'1' 再セット/他クリアし、P系統の SC/NT の直列最上位（直列追番1は自身、他は i-(cno-1)）へ '1'。SC は直前 MC/MGSD 関係で設定先を切替（951005/951031）。最後に末端でない SC を fpaln[1]≠"0KW" で '1'（950925）。
- **末端回路行種先頭機器フラグセット（commit 予定）**: `src/Ews.Analysis/LeadingEquipmentFlagSetter.cs`＝`Fyss34_MattanGyouSento_Set`（Fyss15 第3ステップ）。末端機器(mattan='1')ごとに同一行種グループ(gyoglno)で回路要素が '1'/'5' のレコードを抽出し、階層×1000+直列追番が最小の機器群へ sentflg='1'。計器 WH(kiryoso='3')・CT(kiryoso='2')は対象外(950405)。`CircuitWork.LeadingEquipmentFlag`(=sentflg)を新設。
- **通電電流値算出（commit 予定）**: `src/Ews.Analysis/EnergizingCurrentCalculator.cs`＝`set_denryu`（Fyss31 の下請けリーフ）。負荷容量(fuka)→通電電流値を負荷種類(M/H/S/HA/FL/NA/TR/YA/YS)別に算出。電動機は回路電圧・相数と容量帯で pow 近似式(MGは0.75〜1.0kWで4.4A固定・INVBP THRは容量帯別強制・製作仕楘2の強制値)、他は fuka/v 系。Fyss31 本体と改訂<4>主幹ループが使用。
- **保留（Fyss31 本体）**: `Fyss31_FukaHassei_Set`本体は FYRT812(負荷容量決定表41件)/`set_fky`/`get_ep`/エラー域(FYRT805)/SC処理(`Fyss39`・`Fyss3A`)依存の大型関数。下請け `set_denryu` を先行移植。残りのサブを順次移植後に本体を組立。
- **機器選定区分セット（commit 予定）**: `src/Ews.Analysis/EquipmentSelectionKindSetter.cs`＝`Fyss33_KikiSentei_Set` 一式（Fyss15 第4ステップ）。機器選定区分(kikiskbn)・始動回路区分(startkbn)を設定。本体＋`Shori1`(下流に独立負荷源が複数→'2')＋`Shori2`＋`Shori3`(負荷容量ある末端から上流へ負荷情報伝播・始動区分)＋`Shori4`(選定'2'の電動機大分類に下流電動機の負荷種類コピー)を集約。下流探索は移植済 `DownstreamSelector`(Fyss35)再利用、Shori4 の機器大分類(kikirui)は解決子注入。★C原典 Shori2 第1ループは `kikiskbn == '3'`(代入=でなく比較==)でデッドコードのため kikiskbn='3' は実質発生しない→忠実に非移植。
- **保留（Fyss15 残ステップ）**: `Fyss36`/`Fyss3G_Denryuu_Parm_Set`/`Fyss3B_Breaker_Sentei`(機器選定本体)/`Fyss3C`〜`Fyss3I`/`Fyss37`/`Fyss38`/`Fyss3A`/`Fyss3H`/`Fyss15_MCB1P_NT`/`Pre_CT_Make`。順次移植後にオーケストレータへ結線。
### 2026-07-31 セッション⑨ 追加分（Fyss37 電流計算リーフ＋FYRT812）

通電電流積算 `Fyss37_I_Set_Sub`（大型・積算状態機械 `Seki_Tsumi` を含む）のうち、
**電流計算の自己完結リーフ群**を先行移植。

- **FYRT812 負荷容量決定表（commit 予定）**: `src/Ews.Analysis/LoadCapacityDecisionTable.cs`（41 エントリ）＝`fyrt812[]`。予約語→電気パラメータ優先順位(ep_pry[5])/予約語優先順位(pry)/負荷電流算出係数(kei)。Fyss31/Fyss37 共用。
- **電流計算リーフ（commit 予定）**: `src/Ews.Analysis/TerminalCurrentIntegrator.cs`＝`Fyss37_Get_Fuka`(FYRT812優先解決)/`Get_DenIa`(優先パラメータ×0.8)/`Get_DenIb`(積算エリア相和 a+b+(c+d+e)×0.8)/`Kei_TR`(V2/V1)/`Set_Tden`(通電電流値)/`Set_Sden`(設定電流値 Is)。Get_Fuka の電気パラメータゼロ判定は整形ゼロ文字列("00000.000" 等)との照合。
- **保留（Fyss37 残）**: 積算状態機械 `Fyss37_Seki_Tsumi`（seki_flag/s_Ia/f_fuka の置換ロジック）＋`Chk_Break`/`Mat_flg`＋本体 `Fyss37_I_Set_Sub`。次回。

### 2026-07-31 セッション⑩ 追加分（Fyss37 積算本体＝完成）

通電電流積算 `Fyss37_I_Set_Sub` の**積算状態機械**を移植し、Fyss37 を完成。

- **積算本体（commit 予定）**: `TerminalCurrentIntegrator.IntegrateCurrent`＝`Fyss37_I_Set_Sub`（＋`Seki_Tsumi`/`Chk_Break`/`Mat_flg`）。下流の負荷発生元('1')の積算エリアを上流へ積み上げ、対象・下流機器へ通電電流値・設定電流値をセット。`seki_flag`（積上フラグ）は `IntegrateCurrent` が `bool[]` を確保し各下請けへ伝播。ブレーカ定格未満の相は定格電流(Ia=優先電流×0.8)で頭打ち置換、末端行種先頭機器フラグ(Mat_flg)・上流積上区分/負荷発生元で積上中断(Chk_Break)、予約語 'TR' は係数倍。C原典の s_area+=fuka[i][5](=m) 等の癖も忠実再現。0ガード無し/配列外参照は安全ガード。
- **これで Fyss37 は完成**（電流計算リーフ群＋積算本体）。TDD5件（1113→1118）。

### 2026-07-31 セッション⑪ 追加分（Fyss31 負荷容量決定 set_fky/get_ep）

負荷発生元設定 `Fyss31_FukaHassei_Set` の下請けのうち、負荷容量決定の中核を移植（FYRT812＋set_denryu を再利用）。

- **負荷容量決定（commit 予定）**: `src/Ews.Analysis/LoadSourceSelector.cs`＝`set_fky`（＋`get_ep`）。`SelectLoadCurrent(mains, candidateIndex, bestPriority, out priority, out current)` が FYRT812(`LoadCapacityDecisionTable`)で予約語を引き、優先順位1→3 の順に電気パラメータを選んで電流化（AT/A1/A2 は係数×値、W/VA は `EnergizingCurrentCalculator`=set_denryu）。戻り電流コード 0選定/1値無/2優先低/3表外。AT がサーチ上限値(99999.999)ならフレーム電流(AF)使用。数値判定は整形ゼロ文字列照合(A1/A2/AT="00000.000"9桁、W/VA="0000000.00"10桁)。
- **保留（Fyss31 残）**: `Fyss31_FukaHassei_Set` 本体はさらに `set_error`(FYRT805 エラー域)と `SC_Keitou_Proc`(外部 `Fyss39`/`Fyss3A`)依存。負荷容量決定中核(set_fky/get_ep)・`set_denryu` は移植済。

### 2026-07-31 セッション⑧ 追加分（Fyss36 第1段: 積算エリアセット）

末端回路の通電電流値算出 `Fyss36_MattanKairo_Iset`（大型・外部 `Fyss37`/`Fyss3A` 依存）のうち、
自己完結した **積算エリアセット**を先行移植。

- **積算エリアセット（commit 予定）**: `src/Ews.Analysis/AccumulationAreaSetter.cs`＝`Fyss36_Set_Seki`（＋`Get_Pdno`/`Get_Are1`/`Get_Are2`）。負荷発生元の通電電流値・負荷容量を相(R/S/T/X/Y/N の6スロット)×機器種別(A/B/C/D/E/M/S)で積算エリア sk_area へ展開（A～E=通電電流値、M/S=負荷容量）。相の判定は回路相数/線式/極数とグループ親・P系統の相数の組合せ15条件。
- **新設モデル**: `CircuitWork.AccumulationSlots`(=sk_area 6スロット)＋`AccumulationArea`(=seki_area A/B/C/D/E/M/S)、`MainCircuitData.GroupParentSequenceNumber`(=goyano)。
- **積算エリア伝播（commit 予定）**: `AccumulationAreaSetter.PropagateCurrentFromLoadSource`＝`Fyss36_Get_Seki`。通電電流値が0の末端は上流の負荷発生元('1')まで oyatno を遡り、その通電電流値・積算エリアを対象データ追番および途中機器へ複写する。C原典の0ガード無しループ/配列外参照は安全終了(正常データでは必ず負荷発生元が存在)。
- **保留（Fyss36 残）**: 本体オーケストレータ `Fyss36_MattanKairo_Iset`(外部 `Fyss37_I_Set_Sub`・`Fyss3A_Chk_Yoyaku`/`Fyss3A_Prc_Seksan` 依存)。自己完結サブ(Set_Seki/Get_Pdno/Get_Are1/Get_Are2/Get_Seki)は移植済。

### 2026-07-31 セッション⑥ 追加分（ゴールデンマスタ 5 ファイル比較コア）
**比較エンジン基盤**を TDD で新設。パイプライン成熟前でも回せる検証ゲートの土台を用意した。テストは 1004 → **1018**。

- **比較エンジン（commit 予定）**: `src/Ews.Domain/Validation/GoldenMasterComparer.cs`。`GoldenMasterComparer.Compare(kind, expected, actual, maskDatajg)` が固定長レコードをレコード単位・バイト単位で比較し、`GoldenMasterComparisonResult`（件数・レコード別最初の差分・差分バイト総数）を返す。
- **レイアウト定義**: `GoldenMasterLayout`（RL: FYDF806=1219 / 807=1219 / 808=1920 / 809=304 / 811=350、`datajg`=末尾36バイト固定）＋ `GoldenMasterFileKind`（主回路/複合/制御/論理/構成機器）。RL は実 WORK データのファイル長で整合検証済み。
- **datajg マスク**: 全 5 ファイルとも登録情報 `datajg`(termid/date/time) がレコード末尾36バイト。既定でマスクし比較対象から除外（非マスク比較も可能）。
- **テスト**: 合成レコードでマスク・件数差・バイト差検出を網羅＋実 WORK データ（FYDF806/808/809/811）で自己比較一致・datajg 改変マスク挙動・RL 整合を実証（未配置環境はスキップ）。
- **後続**: C# 側 `Fysk07` 相当の出力ライタ（結果モデル→FYDF806-811 固定長シリアライズ）をパイプライン成熟に合わせて実装し、本エンジンへ接続する。`_RO` 改訂&lt;1&gt;（出力時変換）は出力ライタ側で再現。

### 2026-07-31 セッション④ 追加分（AQ〜AT: マイルストーン①「機器検索前処理」を全移植）

`Fysk00_Kikisearch_SY_Sub`（機器検索）でマスタ検索（`Fysk01_Kikisearch_S1`）より前段に行われる `Prop*` 前処理補正を、依存が揃っている分すべて移植した（マイルストーン①の移植可能分を完了）。テストは 907 → **959**（+52）。詳細は [docs/name-mapping.csv](name-mapping.csv) と `/memories/repo/ews-migration-roadmap.md`。

- **ブレーカ系タイプ調整（AQ, commit 94d89b7, 920）**: `BreakerTypeAdjuster`（静的）に `PropChgMcbType`（分岐MCBを単相2/3線電源で協約型 KY/KM）/`PropChgOyaMcbType`（1P3Wで3P子分岐の主幹を経済型 ET/KY）/`PropChgPluginType`（プラグイン CH/CHP の接続相 NOTHING→RN）/`PropChgM10AfBreaker`（三菱/協約3PのELB 10AF→50AF）/`PropChgLaClass1Type`（LA CLASS1 のタイプ2 未設定→RS）を集約。
- **メーカー選定順位上書き（AR, commit bd141ce, 936）**: `EquipmentMakerOverrideAdjuster`（静的）に `PropChgRtrMaker`/`PropChgRmcbMaker`（松下Dに固定）/`PropChgNL63Maker`（KM/TLタイプに KKY 挿入）/`PropChgWHMaker`（QrespoPlus 1P2W210 の WH）/`PropChgINVBPMaker`（INVBPの MC/THR を負荷容量で三菱 MN/MS）/`PropChgGPNMaker`（制御 GP/GPN/APN の OM 直前に OMN 挿入）を集約。新規モデル `MainCircuitData.SpecialReservedWordKind`（=`tokkbn` 特殊予約語区分, '7'=INVBP）。
- **WH/MC 電気値タイプ補正（AS, commit b27da7d, 946）**: `McWhElectricalAdjuster`（静的）に `PropChgWHType`（WH KMタイプ有時に表示タイプ2をクリア）/`PropChgMcMaker`（大陸TA製MCを三菱固定し3P50A+SKを選定）/`PropChgTAMC_epav2`（大陸TA製MCの定格/制御電圧を強制設定）/`PropWhmFukaDenFromChild`（1P2WのWHMを子のLV200から定格200に設定）を集約。
- **TS/400V/耐熱ブレーカ補正（AT, commit 未, 959）**: `SpecialBreakerTypeResolver`（`CircuitDescriptionArea`＝KkGet を注入）に `PropChgTsType`（松下製TSでタイプ指定なし時に主回路/制御のタイプ2を MT）/`PropChg400vBreaker`（400V以上ブレーカを経済型 ET と主電源/フレーム容量でメーカー調整・`ko_syuden` フラグ注入）/`PropChgF2Breaker`（耐熱ブレーカ225ATを250AT/250AFで三菱選定）を集約。
- **保留（依存ブロック）**: `PropSetSmartType`（改訂<35>, `Fysk01_Kikisearch_S1`＝マスタ検索②に依存）/`PropSelNewONWhm`（改訂<64>, `PropChkHibknNum`＝`eigyocd.cns`+`whm_sentei.cns` に依存）。次バッチで cns/マスタ依存を先行移植 or ②本体へ着手。

### 2026-07-30 セッション③ 追加分（AL〜AP: ヒューズ簡易版・ランプ系プロパティ）

`PropChgFuseType_SY` の姉妹関数を継続移植。ランプ（既定タイプ・径サイズ・優先メーカー）を主回路・制御回路両方で揃えた。テストは 871 → **907**。LAMP22 は本番定義（`toku/sekkei/src/makefile -DLAMP22`）のためランプ系は LAMP22 版を移植。詳細は [docs/name-mapping.csv](name-mapping.csv) と `/memories/repo/ews-migration-roadmap.md`。

- **ヒューズ既定タイプ簡易版（AL, commit dbeb429, 877）**: `SimpleFuseDefaultTypeResolver`（=`PropChgFuseType_SY2`, Fysk00.c:6959, LAMP22 無効時の簡易版）。`+(` 無し・特注(cpf=0)で機器タイプ GT、`MK=` 無しでメーカー FT。`CircuitDescriptionArea`（KkGet）のみ依存。
- **イズミ製 WL ランプタイプ（AM, commit b82c1ce, 884）**: `WlLampDefaultTypeResolver`（=`PropChgWlLampType`/`PropChgWlTypeAndKei`/`PropChkHbnPEKOB`）。IZ/MAN(水俣)指定 WL で PM/B 行のみ。水俣=AN→RE、他=径サイズ・前段記述(KkGet_Mae)・ヒューズ個数・PEKOB 品番・電源相線から TR/WP タイプ・径 22/25・電圧 110/220 を設定。依存（FyGetFacGrp/KkGet/KkGet_Mae/PropSetDefLampType/品番リポジトリ）を結線。
- **イズミ製制御ランプタイプ（AN, commit 6f9f9c1, 890）**: `ControlLampDefaultTypeResolver`（=`PropChgLampType`/`PropChgSeigyolTypeAndKei`）＋ `ControlEquipmentInfo`（=`kikijg` 最小サブセット）。制御回路 RL/GL/OL/BL の TR/AN/RE/LED タイプ・径 22 を設定。
- **マルヤス製ランプ径サイズ（AO, commit f6e129b, 899）**: `MaruyasuLampRadiusResolver`（=`PropChgMALampType`/`PropChgMALampTypeC`）。MA/MAN 指定で径入力(`P`)無しのとき径サイズを札幌工場=22mm・他=25mm に設定。主回路（WL/RL/GL/OL/BL）＋制御（RL/GL/OL/BL）。
- **ランプ優先メーカー変更（AP, commit 02aafce, 907）**: `LampMakerPriorityResolver`（=`PropChgWlLampMaker`/`PropChgSeigyoLampMaker`）＋ `LampMakerEntry`（=`lamp_seltbl`）＋ `LampMakerTableLoader`（=`PropCnsLampRead` の sel_LAMP.cns 読込部）。メーカー未指定のランプで地区グループ・予約語からメーカー順位を設定し、`sel_LAMP.cns` の一致行で上書きする（水俣=マルヤス優先・他=イズミ優先）。

### 2026-07-30 セッション② 追加分（AI〜AK: `PropChgFuseType_SY` 依存チェーンを下から結線）

上記「`PropChgFuseType_SY` 本体は保留」の依存を leaf から順に移植し、本体まで結線した。テストは 826 → **871**。詳細は [docs/name-mapping.csv](name-mapping.csv) と `/memories/repo/ews-migration-roadmap.md`。

- **地区グループ取得（AI, commit eb6391f, 834）**: `IFacilityAreaResolver`/`InMemoryFacilityAreaResolver`/`FacilityAreaEntry`（Ews.Domain/Configuration）＋ `InterfdtFacilityAreaLoader`（Ews.Data/Configuration, =`FyGetInterTbl`）。`interfdt.inf`（地区情報定義表）を解析し地区コード→地区グループを引く（=`FyGetFacGrp`, getinterfdt.c）。未定義は本社地区(5)。実行時パラメータ基盤と同じ trio パターン。
- **回路設計エリアから行桁で回路内容記述取得（AJ, commit 0c4ed1b, 850）**: `CircuitDescriptionArea`（Ews.Analysis, =`Fysk11_FYDF805_KkGet`/`_Mae`/`_Ato`, Fysk11.c）。桁(keta)はバイト位置のため CP932 固定長 200 バイト＋NUL に復元し `strchr`/`strstr`/添字をバイト単位で忠実再現。改訂<2>（1桁手前 colm-1 から取得）・改訂<4>（削除行スキップは KkGet のみ）・桁 200 超の行折返しを再現。`LibCharToShort`=既存 `Stoi` 流用。
- **PropChgFuseType_SY 連鎖（AK, commit d4f15bb, 871）**: 4 件一括。(1) **スターデルタ MC/THR 容量選定**: `StarDeltaCapacityEntry`（=`mcthr_seltbl`）＋ `StarDeltaCapacityTableLoader`（=`PropGetMcThrTblCnst`, sel_mgsd.cns）＋ `StarDeltaCapacitySelector.ApplyHeaterCapacity`（=`PropSelChkMgsd`, 容量・電圧一致行の MC/THR ヒータ呼び容量を A2/AT へ設定）。(2) **品番情報リポジトリ(.clh)**: `IPartNumberInfoRepository`＋`FilePartNumberInfoRepository`（=`FyCpHbHbnInfFileR`, `<WORK>/<依頼明細番号>/<依頼明細番号>.clh` を解決し既存 `PartNumberInfoLoader.ReadFromFile` へ委譲）。(3) **WL 回路電圧変更**: `WlCircuitVoltageAdjuster.Adjust`（=`PropChangeWlKpav` 改訂<110>, F の子 WL の回路電圧を河村製は "005"・他は F の電圧へ）。(4) **ヒューズ既定機器タイプ設定本体**: `FuseDefaultTypeResolver.Resolve`（=`PropChgFuseType_SY` 改訂<73>〜）。`IFacilityAreaResolver`/`CircuitDescriptionArea`/`IPartNumberInfoRepository`/`MakerCodePriorityAdjuster`（=`PropAdjustMakerCode`）/`WlCircuitVoltageAdjuster` を結線し、回路記述・地区グループ・ヒューズ個数・品番(GWL/GJWL/PEKOB)・後続ランプ径から機器タイプ GT・メーカー FT を調整し子 WL 電圧を変更する。

### 2026-07-30 セッション追加分（サマリ：計器・メーカー分類リーフ AC〜AH ＋ 実行時パラメータ基盤）

`Fysk00.c` 配下のリーフ群を継続移植しつつ、**OS 環境変数依存を設定ファイル経由に置き換える基盤**を新設。テストは 769 → **826**。詳細は [docs/name-mapping.csv](name-mapping.csv) と `/memories/repo/ews-migration-roadmap.md`。

- **計器・LGR・ZCT 機器の分類収集（AC, commit 77f13a2, 783）**: `MeterCircuitClassifier`（=`Keiki_Check`/`LGR_Check`/`ZCT_Check`, Fysk00.c:3815/3857/3890）。主回路走査ループがレコード毎に呼び、計器/LGR/ZCT の 3 リスト（`MeterCircuitEntry`=WK_Keiki）を構築。`TryClassifyMeter`（予約語先頭4文字 CT/F/DSW/VT/PLTR memcmp）、`TryClassifyLeakageGroundRelay`（`Stoi(ep[2].epak,3)>0` かつ `gyocd[1]!='P'`）、`ClassifyZeroCurrentTransformer`（無条件追加）。static k+malloc/realloc 単一リスト→`IList` 追記に置換。
- **LGR/ZCT 共通メーカーコード抽出（AD, commit 9a7902c, 789）**: `CommonMakerResolver.ResolveCommonMakers`（=`Get_Kyotu_Maker`, Fysk00.c:3915）。メーカー指定域（`MakerDesignation`=FYDF802 最小サブセット）から LGR/ZCT のメーカー順位表（咄4件×3桁）を取込み、両者共通の 3 桁コードを収集。**C原典の添字癖（内側 break 条件が `tmpz[i]`＝外側 i の添字）を専用テストで忠実再現**。
- **ランプ既定機器タイプ判定（AE, commit 29ef37a, 796）**: `LampDefaultTypeResolver.ResolveDefaultType`（=`PropSetDefLampType`, Fysk00.c:5290 改訂<65>）。回路内容記述に `+(` 無し→`LED    `(7桁)。有り→`)` で切詰め後に `NP` 無し→`LED    `、有り→現行タイプ据置。C原典の `)` NUL切詰め副作用を忠実再現。純粋文字列（golden 不要）。
- **文字列前後の半角スペース除去（AF, commit 1d59420, 805）**: `PropertyStringTrimmer.TrimSpaces`（=`PropTrimSpace`, Fysk00.c:6253 改訂<70>）。半角スペース(0x20)のみ `value.Trim(' ')`。全角スペース・タブは対象外、null は空文字扱い。純粋文字列。
- **★実行時パラメータ取得基盤（環境変数→設定ファイル集約, commit fd4a340, 813）**: C原典は ZONECD 等を `getenv` で直読みしており OS 依存。ユーザ提案で設定ファイル源に集約した。`IRuntimeParameterProvider`（Ews.Domain/Configuration, `GetValue(name)`+`ZoneCode`）を OS 非依存の境界とし、`RuntimeParameterNames`（ZONECD/LHOST/TERMID/SYMID/HCONHOST/AUTO_TEST/INFPATH/DATAFILE/LOGFILE/LOGFLAG/FILEPATH/GNAME の名前定数）、`InMemoryRuntimeParameterProvider`（辞書ベース・Ordinal 比較＝getenv 大小区別）、`FileRuntimeParameterProvider`（Ews.Data/Configuration, System.Text.Json で UTF-8 JSON 読込。`RuntimeParameters` セクション or 直下オブジェクト両対応、ネストは無視）を新設。`src/Ews.App.Batch/runtime-parameters.json`（UTF-8, CopyToOutputDirectory）を DI 登録し、合成ルート 1 箇所に OS 依存を隔離。**FyGetZoneCD 実体**（toku/lib/libfycom/fyzonecd.c:43）は `getenv("ZONECD"); strcpy` のみ。
- **AL付ハーフサイズブレーカのメーカー変更（AG, commit 343cb12, 821）**: `CtAlBreakerMakerAdjuster.AdjustMakerCodes`（=`PropChgCTALMaker`, Fysk00.c:6294 改訂<72>）。暁第一工場（3F, ゾーンコード 78007）の製作図で CT/AL 付・メーカー無指定・予約語 MCB/ELB のハーフサイズブレーカのメーカーを三菱（M）に強制。**新設パラメータ基盤の初適用**で、`getenv(ZONECD)` を `IRuntimeParameterProvider.ZoneCode` 経由に置換。
- **メーカーコード選定順位調整（AH, commit 8f133b6, 826）**: `MakerCodePriorityAdjuster.RemoveUnlistedCodes`（=`PropAdjustMakerCode`, Fysk00.c:8100 改訂<122>）。保存値（元の順位）に含まれないメーカーコードを選定順位から除去し前詰め（4 スロット固定・順序維持・空白補充）。

#### ※ `PropChgFuseType_SY` 本体は保留（依存ファイル調査結果）

AH で切出した `PropAdjustMakerCode` の親関数 `PropChgFuseType_SY`（ヒューズのデフォルト機器タイプ設定, Fysk00.c:6335 改訂<73>）本体は未移植依存が多く保留。必要ファイル/データの調査結果：

| 依存（C関数/データ） | 用途 | 必要ファイル | 状態 |
|---|---|---|---|
| `FyGetFacGrp` | ZONECD→地区グループ（1札幌/2つくば・相模/3相模/4水俣/5本社）変換 | `interfdt.inf`（地区情報定義表） | ✅ **存在** `TOKUD/interfdt.inf` |
| `mcthr_tbl`（`PropSelChkMgsd`） | スターデルタ MC/THR 容量選定表 | `sel_mgsd.cns`（定数） | ✅ **存在** `toku/const/sekkei/sel_mgsd.cns` |
| `Fysk11_FYDF805_KkGet`/`_Ato`/`_Mae` | 行・桁から回路内容記述を取得 | FYDF805（回路内容記述） | ✅ **C#側に既存**（`CircuitDescriptionLine` + `SqlCircuitDescriptionRepository`） |
| `PropChangeWlKpav` | WL の回路電圧変更 | （ファイル不要） | ⚠️ f800（主回路配列）依存の関数移植のみ |
| `FyCpHbHbnInfFileR`（`struct hbninf`） | 依頼明細番号キーで品番情報を取得 | **品番情報ファイル（.clh）** | △ スキーマ `PartNumberInfo` は既存だがリポジトリ未整備 |

- **hbninf のファイル名（C原典）**: `FyCpFileGet`（cpgtfile.c）が `FyGetFilePath("WORK") + "/" + 依頼明細番号（空白除去） + ".clh"` で構築。拡張子 `clh` 固定（品番系 clh/clb/clu/clm/clp の先頭）。実ファイルは `WORK/<依頼明細番号>/<依頼明細番号>.clh`（例: `WORK/2607AL01/2607AL01.clh`、計6件存在）。`.clh` は `sizeof(struct hbninf)` ちょうどの固定長バイナリ1レコード。
- **推奨の進め方**: `PropChgFuseType_SY` 本体をいきなり移植せず、依存を下から leaf 移植。品番情報ファイル以外は今すぐ前進可（`FyGetFacGrp` → `Fysk11_FYDF805_KkGet` → `sel_mgsd.cns`/`PropSelChkMgsd` → 品番情報リポジトリ）。

### 2026-07-29 セッション追加分（サマリ：Fysk00 統括の決定的リーフ群 V〜AB）

機器選定統括 `Fysk00.c`（12,037 行）配下の、ISAM 非依存で決定的な純粋リーフ関数を増分移植。詳細は [docs/name-mapping.csv](name-mapping.csv) と `/memories/repo/ews-migration-roadmap.md`。テストは 717 → **769** に到達。

- **Fysk02 特殊予約語チェック（V, commit e9d53af, 727）**: `RatingValueChecker.Check` に接点計算フラグを追加し flag1〜13（SC/WH/VM/AM/TR/CR/TM/TS/BZ/BEL/MV/KPRY/THSW）を移植（=`Fysk02_Check_Teichi_*`, Fysk02.c）。VM の dangling-else・TR 項目16固定オフセット・TM/THSW の時間単位判定据置など C の癖を忠実再現。`NearestRankSearch` の 2 箇所へ接点計算フラグを伝播し `_TMS`（TM/THSW）が主/分岐回路で実働。
- **電気パラメータ回路側書き戻し Area_Rewrite（W, commit f877af3, 734）**: `CircuitAreaRewriter`（=`Fysk00_Area_Rewrite`/`Set_Kairo`/`Set_Datachi`, Fysk00.c:3685〜）。WK_STRUCT3 フラグ（`AreaRewriteFlags`=at/a2/af/ma/am 各[2]）が立つ項目を数値 sep から `GetDataValue` で取得→動的書式整形→回路側 eparmg へ書戻し。`kairo_t`（fyrt817.h）忠実転記。**SET 元 `Fysk01_Kikisearch_S1` が未移植のため未結線リーフ**。`SprintfF` を private→internal 化。
- **入線相数取得 Fysk00_ph（X, commit bca7504, 740）**: `IncomingPhaseResolver.Resolve`（Fysk00.c:4413）。主回路レコード列を上流へ遡り最初の入線（予約語 "P"）の相数を返す。入線が三相四線なら自機器 kpaph（No1196）。memcmp を 8 文字空白パディング＋`CompareOrdinal` で忠実再現。
- **下流機器選択 Fyss35_Select_Karyu_Sub（Y, commit 180cd89, 746）**: `DownstreamSelector.SelectDownstream`（Fyss35.c:69, 15 ファイルで使用）。指定機器の直後から親追番が連続する下流レコードの datano を収集。`Stoi`（=Fysk09.c atoi 相当）を `EquipmentParameterFormatter` に public 追加。
- **計器回路 VA/W 設定 Fysk00_Make_Keiki（Z, commit df526c3, 755）**: `MeterCircuitBuilder.AssignCapacities`（Fysk00.c:3974）。計器回路機器（PLTR/VT/CT/F/DSW 前方一致）を予約語順に走査し `DownstreamSelector` で下流 teiwva を積上げ。PLTR の 5.5V/15V 分岐・CT 特殊（同一機器認識番号）を移植。`MeterCircuitEntry`（=WK_Keiki）新規。
- **積上げ BASE 機器の VA/W 設定 Fysk00_Set_VA_W（AA, commit 8c91e97, 761）**: `StackingCapacityResolver.Resolve`（Fysk00.c:4108）。予約語で BASE 機器を判定し定格/負荷容量を teiwva へ。`VA_YO` 末尾 2 件空文字＝未定義予約語の catch-all（フラグ0）を忠実再現。`EquipmentMaster` に hojg.teiva[0]@244/teiw@258（幅7）を展開。
- **機器選定検索用ワーク構造体 Set_WK1（AB, commit a1b0b89, 769）**: `SelectionWorkParametersBuilder.Build`（Fysk00.c:4174）。主回路 1 件から `SelectionWorkParameters`（=WK_STRUCT1）を組み立て（負荷容量/通電電流/相数/電圧/始動区分/発生区分/負荷種類/親P行相数）。`ParentEquipmentLocator.FindParentPRow`（=`Fysk0f_GetOyaP`, Fysk0f.c:35）新規。`MainCircuitData.EnergizingCurrent`（=denryu）追加。
- **次増分候補**: `Fysk00_Get_Syorino`（予約語→処理番号 index。完全な tchi_tbl 約50予約語=fyrt817.h:777 依存で保留中）／`Fysk00_Kikisearch_TB`/`_SY` 本体（大型 ISAM 依存、`Fysk01_Kikisearch_S1`/`MakerCode_Check`/`Type_Check` 等の未移植依存で保留）。Fysk00 統括の他リーフ helper を要調査。

### 2026-07-28 セッション追加分（サマリ：機器選定 Fysk00/01/02/04/08/09 系）

機器選定（`toku/sekkei/src` の `Fysk*.c` 系）の決定的スライスを増分移植。詳細は [docs/name-mapping.csv](name-mapping.csv) と `/memories/repo/ews-migration-roadmap.md`。

- **数値ヘルパ・定格値キー（I〜L）**: `NumericConverter` に `PowerOfTen`/`Ceiling`/`Truncate`/`TrimTrailingZeros`（=`Ketaawase`/`Kiriage`/`Kirisute`/`Chousei`, `Fysk09.c`）を追加。定格値キー生成 `RatingKeyBuilder.MakeRatingKey`（=`Fysk04_Make_Teikakuchi`）/`GetDataValue`（=`Fysk00_Get_Datachi` 項番1〜53）＋ `NumericElectricalParameters`（=`eparmg_s`）/`RatingKeyTableEntry`（=`TCHI_T`）/`RatingKeyTables`（=`tt_xxx`, 遮断器・接触器系14種）を新設。電気パラメータマージ `ElectricalParameterMerger.Merge`（=`Fysk0c_Edit_Epara`, ep[2]ベース+ep[0]上書き）と char→数値変換 `ElectricalParameterConverter.Convert`（=`Fysk01_Change_Epara`, パイプライン結線）。
- **形状タイプチェック（M〜P）**: `ShapeTypeChecker.ResolveShapeTypes`/`ResolveShapeTypesForPbs`（=`Fysk01_Type_Check2`/`_Check3`, type_tbl2/type_tbl3）。無印 `Fysk01_Type_Check` を `ShapeTypeSelector.Select`（=HandleRock_Check + Fysk08_Usetype_Check + Keijyoutype_Check の連鎖、type_tbl 15予約語＋ビットフラグ選択番号）に統合。**Fysk01 の Type_Check 系（無印/2/3）は全完了**。
- **固定長テキストマスタ土台（O/Q）**: 予約語マスタ `ReservedWordMaster`（=FYDF810, 134件）と直近上下位参照 `NearestRankReference`（=FYDF812, 21,544件）を FYDM805 と同方式の固定長テキストローダーで供給（`ReservedWordMasterLoader`/`NearestRankReferenceLoader`, `src/Ews.Data/Seeding`）。実データでオフセット不変条件を検証。
- **共用情報数値化・定格値チェック（R/S）**: `SharedInfoConverter.Convert`（=`Fysk01_Change_Chokin`, kyoyojg→kyoyojg_s）＋ `NumericSharedInfo`/`NearestRankSharedInfo`。`RatingValueChecker`（=`Fysk02_Check_Teikakuchi`）に通常予約語（flag0）の `CheckAll`/`CheckPart`/`GateCheck` を移植。`GetDataValue` に共用情報項番61〜87を追加（**項番85=配列外参照で項番66と同値を忠実再現**）。`RatingComparisonState`（=CMP_1/2/3）追加。**特殊予約語（SC/WH/VM/AM/TR/CR/TM/TS/BZ/BEL/MV/KPRY/THSW=flag1〜13）は未対応で `NotSupportedException`（次増分候補）**。
- **品名チェック（T, commit 6d3165f）**: `ProductNameChecker.Check`（=`Fysk01_Check_Hinmei`, Fysk01.c:4079）。先頭10桁空白なら未指定=絞り込みなし、指定ありは25桁右詰めで一致判定（固定長 memcmp をバイト等価再現）。
- **直近上下位検索ループ（U, commit bb7ad59）**: `NearestRankSearch`（=`Fysk01_Chokkin_Read_Check` ディスパッチャ＋`_ALL`/`_TMS`）。前方一致は ISAM 順次読を `IReadOnlyList<NearestRankReference>` 走査に置換し、`NearestRankReference.BuildComparisonKey()`（KEY62+kteichi50=112文字）で先頭 `siz`（=`(sfg[0]==0?cpsize:ComputeCompareSize)+62`）文字一致。`ComputeCompareSize`（=`Fysk0a_CmpMojisu_Get`）。ドメイン `RatingCheckTable`（=`TCHI_TBL`）＋ `RatingKeyTables` に14種の tchi_tbl を追加。**TM/THSW（flag7/13）は Fysk02 特殊予約語未対応のため `_TMS` は構造のみ**。接点計算 `Get_Seten_GoodData`（制御回路のみ）も未対応。
- **次増分候補**: ①Fysk02 特殊予約語（flag1〜13）→`_TMS` と各特殊機器が実働／②Fysk00 統括（12k行, FYDM805/FYDF816 依存＝準備済）。

### 2026-07-27 セッション追加分（サマリ）

上流パラメータ生成の 2 次側電気値（`SetParam_ep2`）と機器選定（Fysk01/Fysk09）の決定的スライスを増分移植。詳細は [docs/name-mapping.csv](name-mapping.csv) と `/memories/repo/ews-migration-roadmap.md`。

- **SetParam_ep2 ディスパッチャ拡充（`SecondaryParameterSetter`）**: `Fyss14.c` の予約語別 ep[2] 生成を段階拡充。VS/AS/LA/CON → HPSB/HSB（MCB_P+MCB_V2、golden 検証対象）→ TS（V2/VC/AC/BC/CC、`SetTsContactC` リーフ移植）→ 自己完結ケース一括（L/MCFR/MCSD/MCFRSD/MGFR/MGSD/MGFRSD/DCSIR/DCNI/TSU 系）。**ブレーカ系（SB/HPSB/HSB/MCB/CKS/CSDT/SSW/TSW）は V2 安定 → golden 検証可。選定デバイス系（MC/VM/TS/MGFR/DC 系）は V2 が下流の機器選定で上書きされるため golden 非追加・単体テストのみ**。未収録 case（記録列/物件/計算依存）: NHMB（書式付きゼロ sentinel との memcmp 依存で保留）・WL/GL/RL/OL/BL（物件施策区分）・PLTR・WH/VT/TR/TB/LGR/ELR・DCPW。
- **機器選定 候補比較（新クラス `EquipmentSelector`）**: `Fysk01.c` の直近上下位検索から、マスタ・記録列非依存の純粋数値比較を移植。`CompareByRangeCentering`（=`Fysk01_Choki_Cmp1`, THR/MG/XERY 用・基準値の幅内均等度で優劣）／`CompareByMidpointDistance`（=`Fysk01_Choki_Cmp2`, THSW/TM 用・幅中点との距離）／`CompareCandidate`（=`Fysk01_Data_Cmp`, THR/MG の候補選択）。返値 1(入替)/0(GOOD)/-1(SYS_ERR)。定数は `fyrt808.h`（GOOD/SYS_ERR/TOL/PC_1=THR/PC_3=MG）。`FYDF812` は定格値キー `kteichi`（50 バイト）のみ参照するため当該文字列を渡す自己完結シグネチャ（`string.CompareOrdinal` で `memcmp` 再現）。
- **数値ヘルパ（`NumericConverter` へ集約）**: `Fysk09.c` の純粋数値ヘルパを移植。`PowerOfTen`（=`Ketaawase`, 10^keta 桁合わせ係数）／`Ceiling`（=`Kiriage`, 切り上げ）／`Truncate`（=`Kirisute`, ゼロ方向切り捨て）／`TrimTrailingZeros`（=`Chousei`, 小数末尾ゼロ除去）。`Make_Teikakuchi`（定格値キー生成）の前提として先行整備。
- **命名規約の是正**: 上記の機器選定・数値ヘルパは当初 romaji（`Ketaawase`/`ChokiCmp1` 等）で命名していたが、本プロジェクトの「現代英語命名」方針に合わせ上記の英語名へ改名（元 C 名は `【C原典】` コメントと name-mapping.csv に保持）。以後の移植も現代英語命名を徹底する。
- **定格値キー生成（`Fysk04_Make_Teikakuchi`）**: 機器選定のマスタ照合キー（kteichi 50バイト）生成を移植。`NumericElectricalParameters`（=`eparmg_s` 数値版 double）・`RatingKeyTableEntry`（=`TCHI_T`）・`RatingKeyBuilder.MakeRatingKey`（=`Fysk04_Make_Teikakuchi`）/`GetDataValue`（=`Fysk00_Get_Datachi` 項番1～53）を新設。テーブル `RatingKeyTables`（=`tt_xxx`, fyrt817.h）は遮断器・接触器系14種（MCB/ELB/MMCB/ELMB/SB/RMCB/RELB/RMMCB/RELMB/MC/THR/MG/SC/NT）を先行収録。エンジンは s_toku（-3打切/-2スキップ/-1区分読取/0・n採用）分岐と `"%0<len>.0f"` ゼロ埋め整形を忠実再現。**入力 `eparmg_s` の生成器（char→double 変換）が未移植のためパイプライン未結線のリーフ（EquipmentSelector と同様）。計器・変成器系テーブル綄約85種と Get_Datachi 項番61～87（kyoyojg_s）は後続**。
- **電気パラメータのマージ（`Fysk0c_Edit_Epara`）**: 特注機器の電気パラメータ文字列生成の前段で、上流(2次側) ep[2] をベースに機器自身 ep[0] の入力済みフィールド（数値>TOL・区分文字≠空白）だけを上書きしたマージパラメータを生成する純粋関数（`ElectricalParameterMerger.Merge`）。`NumericElectricalParameters.Clone`（=memcpy相当）も追加。`epaqty`/`epabn` はマージ対象外。**※ `Fysk0c_Edit_Epara` は char→double 変換器ではなく、既に数値化済み `eparmg_s` のマージである点に注意**。

### 2026-07-24 セッション追加分（サマリ）

下記は本セッションで追加した移植・検証。詳細は [docs/name-mapping.csv](name-mapping.csv) を参照。

- **物件情報 FYDF801（データ層）**: `ProjectInformation`（物件共通情報, 1200バイト固定長, `FromFixedRecord`）＋ `PanelDetailInformation`（盤明細 bmeisai, `bannm`@55/`boxsund`@285）＋ `ProjectInformationLoader`（`ParseProjectInformation`/`SeedProjectInformation`・`ParsePanelDetails`/`SeedPanelDetails`）＋ SQL テーブル。実データ `master/FYDF801.data`（33,264件）で不変条件検証。`hzkbn` 実値は 1/2/**5/6**/空白（地域別 50/60Hz）、`meisaino` は '0A'-'0D' 英字も実在。
- **品番情報 hbninf（.clh）**: `PartNumberInfo`（`struct hbninf`, 908バイト生バイナリ 1レコード, `inputhb`@0/`boxtyp`@842/`crboxtmp`@864 ほか）＋ `PartNumberInfoLoader.ReadFromFile`。案件ごと `<WORK>/<依頼明細番号>.clh`。SEP 判定の入力。
- **SEP 追加（`Fyss12.c` step6, 改訂<7>/<12>）**: `SeparatorBoxCheck`（`PropChkSEPBox`/`PropChkHbnHB300`）＋ `Hb300UnitPartLoader`（`unithb300.cns`）＋ `SeparatorInsertion`（`Kikitable_SEP_Make`=`CreateSeparatorEntry` / `sep_flg`=`IsSeparatorApplicable` / `sep_del`=`HasSeparatorDeletionCondition` / 系統ブレーク=`TrySeparatorAtBoundary`）。`MainCircuitBuilder.MakeMain` に `SeparatorInputs?`（hbninf+boxsund+hb300）を任意配線（null 時は SEP なし＝従来挙動）。`souden`/`sousen` は既存 `LineTypeTableEntry.PhaseVoltage`/`PhaseWires` を再利用。**保留の SEP 追加をこれで完了**。
- **上流パラメータ生成 Fyss14（電気パラメータ本体・着手）**: `mcprmcnv`→`MainCircuitParameterConverter.ConvertParameter`（変換テーブル145行の完全一致照合・DC補正）／`Kairo_Parm_Set`→`CircuitParameterResolver.SetCircuitParameter`（NTは1P2W固定・mcprmcnv変換・F特殊処理。ISAM非依存の決定的処理）。既存の `Volt_Conv`/`Max_Volt`/`Left/Right_Volt`（`VoltageInheritance`）・`Element_Gen`/`Pole_Gen`（`CircuitElementResolver`）と合わせ、上流パラメータ生成の決定的スライスが進行。
- **実データ突合ハーネス**: `SeparatorRealDataTests`（フルパイプライン FYDF805→`CircuitStringChecker.Check`→`MainCircuitBuilder.MakeMain`(SEP込み) を実4案件で通し、生成SEP数がC版FYDF806のSEPレコード数と一致=負側検証。パイプラインが実データで例外なく完走することを初実証）。既存 `GoldenComparisonHarnessTests`（eparmg/fparmg 往復）と併せ、実機出力に対する回帰基盤を強化。
- **正側検証（SEP挿入あり）は未了**: 入手済4案件（2607AL01/02/03/05）は FYDF806 に SEP=0 のため。SEP 含有案件データが揃えば同一ハーネスで自動的に正側検証となる。

### 移植済み

- **回路文字列チェック** `CircuitStringChecker`（`Fyss11.c`/`Fyss1c.c`/`Fyss1d.c`）
  - 系統/行種/仕様テーブル生成、盤名称・入線・有電源等の行種ディスパッチ
  - 予約語解決（`ResolveReservedWord`。特殊キー 27A/27B/27C・SL・G1-4・FLT の短絡一致）
  - 電気パラメータ → 定格値（key_tbl）格納の配線（`Check_Kikimei`→`Parm_Check_Main` 相当）
  - **代入文パーサ（`Check_Dainyuu` fp サブセット）**: 予約語文の後に続く代入文「(TAG=値)」を検証し機器テーブルへ格納（`CircuitStringChecker.Assignment.cs`＝partial）。`Check_KikiMeisyou` の while ループ（`ProcessAssignmentStatements`）→ `Check_Dainyuu`（`CheckDainyuu`）→ `kikitable_add` タグ（`ApplyAssignmentTag`）。CP932 バイト列カーソル `ByteCursor`（ph/nh/yh・iskanji 全角2バイト）でスキャナ（`FindKikiMeisyou`/`CheckKakko`/`FindName`/`FindDelimetor`/`FindUnit`/`NextKigou`）を忠実再現。対応タグ: MK→DMK/IT→DIT/CM→DCM・DCM2/SP→DSP/LW→DLW/LN→DLN/LV→DLV[0]/UP→DUP/NO→DNO・GNO/HAI→HAI/B→BUN_RETU/WHAI→WHAI/BK・BKO→BIKO/CNCT→'P'/@→ノーチェック。バリデータ `CheckCm/CheckSp/CheckLw/CheckHai/CheckLn/CheckLv/CheckUp/CheckNo/Renketu`＋`CheckNumericC/CheckAlphaNumericC/CheckZenkaku1z`（Fysscommon.c）。予約語部を代入文 '(' の前で分離（`ExtractReservedClause`）し、代入文の '=' を電気パラメータの '=' と誤認しないよう修正。**保留**: Check_IT(FYDF817 ISAM)・Check_MK(FYDM801 ISAM) 照合、Check_LW の Keisan_LW 正規化、Check_NO のハイフン連番展開（PropGetKnoStruct/PropDevelopKno）、Check_Haifunn('S')、PULASU 型式展開（DTYPE, Fysk01 依存）。
- **電気パラメータ検証** `ElectricalParameterChecker`（`Fyss1d.c` の表駆動パーサ）
  - 定格キー表・型非依存パーサ・MCB/ELB/MC/MG/THR 等の型別検証、`RatingValues` へ格納
  - 定格キー表を拡充: '/'(CT/VT付き)・特殊展開を除く単純構造の残り約70表（VS/TB/CON/GL系/CR/TS/MV/KPRY/MCFR/MGFR/STM/VVVF 等）を移植
  - 先頭数字予約語(2ERY/3ERY/4ERY)を解禁。ExtractElectricalParameter を C原典 Check_KikimeiC(Fyss1c.c)に忠実化し、予約語/電気パラメータ(d_parm)の分離を修正(MCB3P→d_parm 3P、2ERY100AF→予約語2ERY+d_parm 100AF)
  - CT/VT付き('/')定格キー表 AM/VT/CT/RTR/BLTR/PLTR/THSW/WH を追加。next_1_get(NextOneGet)を移植し副記号 n_kigo を key_check へ伝搬(消費先 key_check_WH を移植済)。特殊展開 PT/BP も移植済(空記号 len25 プレースホルダ→非空パラメータは FY-699E)。TR は専用パーサ。
  - **TR(変圧器)専用パーサ移植**: `TR_check_main`→`TrCheckMain` / `key_check_TR`→`KeyCheckTr` / `ft_tr`→`TransformerKeyTable`。多スロット(p1/w1・v1[]・p2,p3/w2,w3・fv2,v2[]・fv3,v3[]・va)＋状態(sw_kugiri/sw_v2v3/ior1)を忠実移植。flag2(V/VAC いずれか必須)未受理は FY-889E、KVA は×1000 で va 格納。PT/BP は空記号 len25 プレースホルトで収録済み。
  - **定格キー表の出典是正（重要）**: 検証用の定格キー表を、表示展開モジュール `FySinTkakt.h`(`t_*`/`tkak_tbl`) から検証権威である `toku/include/sekkei/fyrt810.h`(`ft_*`/`fyak_tbl`) へ全面再ベース。`fyak_tbl` 経由で `Check_1_Group` が参照する値に一致。空表(STM/SIR/C/R/D/NICA/RE/VVVF/TVZ/TVB/TVH/TVK/SPACE/AL 等)は忠実に空配列（予約語は存在=構造検証対象、非空パラメータは FY-699E）。予約語 RECB→RMCB 是正。VM/TM を追加移植、PT/BP は空記号 len25 プレースホルダとして収録(非空パラメータは FY-699E)、TR は専用パーサ。`fyak_tbl` マッピングのクセ(G→ft_g1/GI→ft_i/GP→ft_p/GPN→ft_n、SMTKP/SMTSS/SMTRY は ft_tsu 共有)を反映
- **主回路生成** `MainCircuitBuilder`（`Fyss12.c` の 17 ステップのうち）
  - step1 系統構成 / step2 行種階層 / step4 機器情報 / step5 回路区分（`Kairo_Kubun_Set`）
  - step6 `Yoyakugo_Add_Main`: 前段 `D_No*=10` スケーリング + CT/VT/WH/ZCT 計器回路展開（`ConsolidateCurrentTransformerCircuit`/`ConsolidateVoltageTransformerCircuit`/`ConsolidateSingleInstrumentCircuit` ＋ `Kikitable_Main_Make`/`Kikitable_Keiki_Make`）を移植。SEP 追加のみ保留。 / step7 D_No 昇順ソート（`qsort` 相当）
  - step8 `Gyosyu_Rank_Set` 行種ランク/出現数（`Kiki_Suryou_Set/Calc`・`Main_Exist_Check`）
  - step9-13.5 機器ランク系: `Kiki_Rank_Set`/`Kiki_Rank_Update`(TOP_Flg)/`Gyosyu_Rank_Update`(`Find_Max_Rank`)/`Pattern_Rank_Update`/`WH_Rank_Set`(改訂14)/`TR_Rank_Set`
  - step16 電気パラメータ一致チェック（`Ele_Equal_Check`）
   - step14/15 グループセット（`Kairo_Group_Set`）/同一機器認識番号セット（`Kiki_Equal_Bangou_Set`）: C原典でコメントアウト（無効化）済のため意図的にスキップ
   - step17 主回路ファイルエリア数量分解: `Fyss12_Make_Main_Sub`/`Main_File_Area_Make`（`Find_Iteration`/`Find_Nobangou`/`Find_Group` → `MainCircuitSegment`）。FYRT800 レコード整形（`mainfile_set`）は決定的スライス＋回路要素区分（`kiryoso`=`Find_Kairo_Kubun`）を移植。3 分解方式すべてレコード生成を移植: `Main_File_Make_s`(`MainFileMakeSimple`)/`Main_File_Make_d`(`MainFileMakeIteration`)/`Main_File_Make_n`(`MainFileMakeCircuitNumber`)。回路番号文（`Main_File_Make_n`）は DNO トークン展開＋`include`(`Include`)/`Find_Next_Nobangou`(`FindNextCircuitNumber`)による次段機器探索と同一回路番号(N_No)群への負荷電圧(DLV)伝播を移植
   - step17後 入力順固定項目チェック（`Fyss1m_Input_Check`/`Fyss1m_Input_Check_CT_AM`）: 計器回路でない AM の直後が計器回路でない CT のとき FY-645E

### 未移植・TODO（次にやること）

- **step6 の本体（残り）**: CT/VT/WH/ZCT 計器回路の主回路展開は移植済み（`ConsolidateCurrentTransformerCircuit`/`ConsolidateVoltageTransformerCircuit`/`ConsolidateSingleInstrumentCircuit`：同一 G_No の計器区分（K_Kubun=K）群を走査し、計器回路（`Kikitable_Keiki_Make`）／主回路（`Kikitable_Main_Make`）を末尾追加 → step7 で整列。VT は exist_CT/exist_WH の二経路を忠実移植）。**未移植（保留）**：
  - **SEP 追加**（`Kikitable_SEP_Make`, 系統ブレーク時）: `PropChkSEPBox`/`PropChkHbnHB300`（改訂<12> の bukken FYDF801 プロパティ照会）とグループ別 souden 差分判定に依存。データで検証できるようになってから着手（推測移植は忠実性を損なうため保留）。
- **主回路生成の step14/15**: `Kairo_Group_Set`/`Kiki_Equal_Bangou_Set` はいずれも C原典でコメントアウト（無効化）済のため意図的にスキップ（コメントで明示）。step17後の `Fyss1m_Input_Check`（CT/AM 入力順）は移植済み（回路要素区分 `kiryoso`=`Find_Kairo_Kubun` を `MainAreaSet` で設定して判定）。`PropSetInvbpKbn`（改訂<16>/<18> INVBP 区分）は kairsfx/tokkbn と INVBP 追加機器が上流 Fyss13-15 未移植依存のため保留。step17 FYRT800 レコード整形は **決定的スライスを移植済み**：`MainAreaSet` の決定的フィールド（datano/kno/ksyubetu/yoyaku/ysno/yssfx/gyocd/kiryoso/skno/narakbn/doukkno/jagekbn/P 系統座標/**kairsfx**（`Max_Bunno_Find`/`Max_Kbangou_Find`）/**gyono**（`Find_Bangou`=行種名後方数値）/**ep[0]**（`eparm_set` 統合＝`EquipmentParameterFormatter.EparmSet` 呼出）/**epabn**（盤名称状態 epabn/bepabn を `CircuitParseResult` で伝搬）/**epaqty**（F/VT/CT の Kosu→QTY））を Simple/Iteration セグメント経路で `CircuitParseResult.MainCircuits` に生成。**未移植（TODO）**：datatype（KIKITABLE.DTYPE 未モデル化）は上流 Fyss13-15（機器選定・型式展開）未移植のため保留。`Main_File_Make_n`（回路番号文）は移植済み（`MainFileMakeCircuitNumber`＋`Include`/`FindNextCircuitNumber`。DLV 伝播含む）。fp（付属パラメータ fparmg）は移植済み（`FparmSet`／`FparmgCodec`。fpacm2/fpacglno/fpasglno/spkvn上書き/fpaup/tikbn は上流依存で保留）。
- **上流パラメータ生成（Fyss13-15 機器選定・型式展開）**: 着手。`Fyss14.c`（電圧値の継承）の決定的スライス `Volt_Conv`/`Max_Volt`/`Right_Volt`/`Left_Volt` を `VoltageInheritance`（電圧3要素配列の変換・整列）に移植（純粋関数・単体テスト済）。さらに主回路パラメータ構造体 `MCPRMS` を `MainCircuitParameter`（相/線式/極数/電圧v[3]/AC・DC区分）に、決定的テーブル照合の `Element_Gen`/`Pole_Gen` を `CircuitElementResolver.ResolveElement`（相・線式・極数→エレメント数）/`ResolvePole`（相・線式→極数をpへ設定）に移植（静的テーブル完全一致照合、不一致は-1。単体テスト済）。`Fyss13.c` の `Fyss13_Make_Control`（制御回路エリア生成）は FYRT802/FYRT820（制御仕様・制御回路設計エリア）依存の新規サブシステムのため本格移植は保留。ep/fp 電気パラメータ本体（`Kairo_Parm_Set`/`mcprmcnv`/`Parm_Set_*`）は MCPRMS・ISAM マスタ読込依存で継続保留。
- **電気パラメータエンジン**（key_check の値検証）: **全 key_check 型を移植完了**。データ駆動 `KeyCheckRules`（MCB/MC/MG/THR/… 変流器/リレー/スイッチ/ブザー/フィーダ/インバータ/ユニット化スイッチ等の全型）＋ TR 専用パーサ（`TrCheckMain`）＋ NT(奇数丸め)/WH(n_kigo 副記号)専用ハンドラ。予約語別名は共有ルール配列(GxRules/XlRules/XeryRules/SlxRules/FltxRules/UnitSwitchRules)。STM/SIR/C/R/D/NICA/RE/VVVF/TVX は C 原典 return 0 のため構造検証のみ。
- **型式展開（eparm_set 電気パラメータ整形）**: 着手（Wave1）。`eparm_set`（`Fyss1f.c:2208`）を `EquipmentParameterFormatter.EparmSet`（`RatingValues`→`ElectricalParameters`、key_check の逆写像）に移植。整形ヘルパ `set_9`/`chk_9`/`Stof` を C の atof×multiple＋sprintf 書式（`%09.3f` 等）で固定長化。Wave1 は遮断器系 MCB/ELB/MMCB/ELMB/SB を収録（極数/エレメント(e==0→9)/AF/AT(MCB・ELB は Stof==0 かつ非空で `99999.999`)/感度電流 MA/負荷容量 kW×1000/AC・DC 区分/定格電圧2）。`struct eparmg`→`ElectricalParameters`（全フィールドを Main_Area_Clear 相当の 0 埋めで初期化）。残り予約語（PS/P/UP・MC/計器系 VM/AM/VT/CT・TR 多スロット・ZCT/LGR/… 等）と `MainAreaSet` の ep/fp 統合は後続 Wave。
- **型式展開 Wave2（漏電遮断器系 RMCB/RELB/RMMCB/RELMB）**: `eparm_set`（`Fyss1f.c:2366-2458`）を移植。制御電圧 `vc`/`fvc`→`epavc`（`%03.0f`）/`epavckbn`（A/D 区分）を `SetBreaker` に `hasVc` として追加。R 系は e==0→9 変換なし・AT の Stof 特殊処理なし。AF は from_length=2、AT は RMCB/RELB=2 桁・RMMCB/RELMB=5 桁。RELB/RELMB は感度電流 MA も整形。テスト計 348 pass/1 skip。
- **型式展開 Wave3-5（引込・電磁接触器系・端子台計器系）**: `eparm_set`（`Fyss1f.c:2219-2568`）を移植。Wave3 引込 PS/P/UP（相数/線式 epaph2[0]/epawr2[0]、定格電圧2 の3スロット epav2[0..2]、P は電線サイズ epasq/epaesq と芯数 epac/本数 epaksu を追加）。Wave4 MC/THR/MG/SC（接点数 epaac/epabc、SC は KVAR/UF/HZ）。Wave5 NT/WH/VM/AM/VT/CT/VS/AS（1次側 epaa1/epav1[0]、VT/CT は VA epava、VS/AS は相数/線式のみ）。各機種で AF/AT/A/SA/VA の from_length が fyrt811 のフィールド幅どおり異なる点に注意。共通ヘルパ `FvKbn`（AC/DC 区分）を追加。テスト計 364 pass/1 skip。
- **型式展開 Wave6（TB/CON/TR 多スロット変圧器）**: `eparm_set`（`Fyss1f.c:2560-2617`）を移植。TB（極数/電流/電圧/電線サイズ epasq）・CON（極数1桁/電流2桁/電圧）。TR は最複雑分岐: 1次相数/線式（epaph1/epawr1）+ 定格電圧1 3スロット（epav1[0..2]、4文字目 T でタップ epav1idx）、2次相数 PH2/線式 WR2 を chk_9≠0 のものから順詰め（p2/p3・w2/w3）、AC/DC は fv2 が A/D なら fv2 さもなくば fv3、2次電圧 V2 は v2[i] の chk_9≠0 を epav2[i] へ（T で epav2idx）+ v3[0]→epav2[1]/v3[1]→epav2[2] 上書き、定格容量 va→epava。テスト計 371 pass/1 skip。
- **型式展開 Wave7+（残り全予約語・eparm_set 移植完了）**: `eparm_set`（`Fyss1f.c:2617-3218`）の残り約60予約語を全移植し全99分岐を収録完了。ZCT/LGR(感度電流4スロット)/ELR/HPSB/HSB/RRY/RTR/MCDT/F/LA/DCPW/CR/TM(タイマ)/TS/G系(G/G1-G4/GI/GP/GPN)/表示灯WL・GL・RL・OL・FL・BL/COS/PBS/SSW/TSW/BZ/BEL/CP/RSW/EE/HM/XERY/CKS/CSDT/CU/TU/NHMB/APN/SL系/LGT/BLTR/PLTR/LSW/DSW/SV/MV/KPRY/THSW/L/IDF/HDF/MDF/WDP/MCFR/MGFR/MCSD/MGSD/MGLD/MGCS/INV/DCSIR/DCNI/MCFRSD/MGFRSD/TSU系(TSU/SSWU/PBSU/COSU/2COSU/OLU)。C忠実再現: XERY=接尾一致(memcmp &yoyaku[1]=ERY)/FLTx=先頭3文字一致/TM時間単位倍率(nset・nss・ns=1:×1・2:×60・3:×3600)/BZ・BEL・MV=fwvaでW-VA振分(BELのWは×1000)/CKS・MGFR・MGSD・MGFRSDはepae直接代入/FLは表示灯群優先(後方の単独FLは不到達)/DCSIR・DCNIはfv→fvdcでepav2kbn上書き/STM・SIR・C・R・D・NICA・RE・VVVF・TVは空分岐で0埋めのまま。共通処理はApplyV2/ApplyVc/SetWva/SetTimer/EDirectに集約。テスト計 388 pass/1 skip。
- **ゴールデン突合（実案件データ・型式展開検証）**: 実案件(EWS/WORK配下)のFYDF805(回路内容記述,RL=270,kairoar@+17)→key_check→eparm_set→eparmg固定長化を、実FYDF806(RL=1219,yoyaku@+38,ep[0]@+114)のep[0]と突合する`GoldenComparisonHarnessTests`を新設。`EparmgCodec`(Ews.Domain.Analysis)がElectricalParametersをeparmg構造体宣言順の253バイト固定長へ相互変換。(1)実ep[0..2]を復元→再直列化する往復検証で案件139・11,517レコードがバイト完全一致し、253バイトレイアウト(fycommon.h)の実機一致を実証。(2)単一機器行(複合オプション・数量展開を除く)でeparm_setが書き換えたフィールドのみを実ep[0]群と存在照合し内容不一致0件。QTY/BN等のeparm_set対象外(外部設定)フィールドは比較から除外。CU/TS/PBS等の制御系機器は主回路806に対応レコードが無く主回路対象外として集計。WORK未配置環境はスキップ。テスト計 390 pass/0 skip。**fp（付属パラメータ）往復検証を追加**：実 FYDF806 の fp（@+873＝ep[0]@114 + eparmg 253×3）を `FparmgCodec` で復元→再直列化してバイト完全一致を検証し、fparmg 157バイトレイアウト（fycommon.h:77）とレコード内オフセットの実機一致を実証（実案件577件保有環境で全レコード往復一致）。
- **MainAreaSet への eparm_set 統合（ep/kairsfx/gyono/epabn/epaqty）**: `mainfile_set`（`Fyss1f.c:1464`）の残りフィールドを移植。(1) **ep[0]**: 定格キー（`RatingValues`）検証済み機器で `EquipmentParameterFormatter.EparmSet` を呼び出し `MainCircuitData.ElectricalParameterSlots[0]` を生成（`eparm_set` 全99分岐が移植済のため解放）。(2) **epabn（盤種類 BN）**: P/SP/MP/UP は盤番号（`Ban`）で確定、それ以外は直前状態（bepabn）継承。C の static epabn/bepabn を `CircuitParseResult.PanelNameKind`/`PanelNameKindPrevious` で伝搬し、`BuildMainCircuitFileArea`（=`Fyss12_Make_Main_Sub`）冒頭で epabn='1' リセット。先行する P（盤）レコードが無い場合の bepabn 初期値 '\0' も C 忠実（DLN→fp.fpaln[1] 代用セットは fp 未モデル化で未実装）。(3) **epaqty（手配数量 QTY）**: F/VT/CT は Kosu（3→'3',2→'2',4→'4',他→'1'）、それ以外は '1'。(4) **kairsfx（生成回路サフィックス）**: `Max_Bunno_Find`/`Max_Kbangou_Find`（同一 G_No 前方走査で最大 B_No/N_No）＋近傍機器（S_Kiki±i=`parse.MainEquipment[kikiIndex±i]`、範囲外は G_No=-1）＋ bunkind（950906 GKosu 差分判定）で 'A' 起点サフィックスを組立。(5) **gyono（行種番号）**: `Find_Bangou`（`Fysscommon.c:407`、行種名原文後方の連続数値抽出、Fyss11.c 版とは別関数）で `LineTypeRaw`→数値→"%02d"。**未移植**: datatype（DTYPE 未モデル化）・fp（fparmg 未モデル化）。テスト計 395 pass/0 skip。
- **MainAreaSet への fp（付属パラメータ fparmg）統合**: `mainfile_set`（`Fyss1f.c:1957-2205`）の付属パラメータ設定ブロックを移植。`struct fparmg`（`fycommon.h:77`, 157バイト）を `AttachedParameters`（`Ews.Domain.Analysis`）でモデル化し、固定長入出力を `FparmgCodec`（Serialize/Deserialize。既定値は `Main_Area_Clear` の '0'/' ' 混在を忠実再現）に実装。整形は `EquipmentParameterFormatter.FparmSet`（機器テーブル→fp）へ移植：負荷容量 DLW（'K' 接頭・`strpbrk`/`strspn` で数値部抽出、K 単位で ×1000、`%07.0f`、V/W 単位判定 fpalwkbn）、負荷電圧 DLV（fpalv[0/1]、3バイト切詰）、負荷名称（effectiveLoadName→fpaln[0]、予約語 "P" は fpaln[1] 代用＋DLN クリアを `MainAreaSet` の epabn ブロックで実施）、コメント DCM（fpacm1）、品名 DIT（fpaitpt）、SP区分 SP_Flg（spkvn）、寸法 DSP（'*' 分割→fpah/fpaw/fpad の `%04d`）、括弧区分 Kakko1/2（12→fpag'G'/13→fpahu'H'/14→fpak'K'/15→fpas'S'/16→fpamh'M'）、制御電源番号 C_Flg=='1'（fpac `%02d`）、メーカー DMK（fpamk）。全角を含むテキスト系は CP932 バイト幅で切詰（`TruncBytes`、全角2バイト分断回避）。`MainCircuitResult.AttachedParameter` を追加し `MainAreaSet` で `FparmSet` を呼出。**未実装（段階移植）**: fpacm2/fpacglno（GCM/GCM_Group 依存）、fpasglno（SP_Group 依存）、SP_GFlg による spkvn 上書き、fpaup/tikbn（当ブロック外）— いずれも機器選定 Fyss13-15/行種グループ状態が未モデル化のため既定値のまま。単体テスト（`FparmgCodecTests`：157バイトレイアウト/往復/FparmSet 各分岐）を追加。テスト計 428 pass/0 skip。
- **データ層**: `.cns` マスタ取込・ISAM 固定長エクスポート取込・SQL Server スキーマの本格整備。(1) 部署別仕様書一覧(siyosyo.cns)の階層パーサ＋`SpecificationKind`/`SpecificationFile` テーブル＋`SeedSpecificationMaster`。(2) **機器マスタ FYDM805**: `hostdt/FYDM805.data`(600byte/rec・LF区切り・Shift-JIS固定長、完全再エクスポート実データ18,370件・主キー(予約語+メーカ+パラメータ+定格)重複0)を `EquipmentMasterLoader.ParseEquipmentMaster`＋`SeedEquipmentMaster`(SqlBulkCopy)で取込。(3) **品番索引 FYDF816**: `hostdt/FYDF816.data`(184byte/rec・LF区切り、実データ11,424件・キー(品番+追番)一意)を `EquipmentPartNumberIndex.FromFixedRecord`＋`EquipmentMasterLoader.ParsePartNumberIndex`/`SeedPartNumberIndex`で取込。★索引11,424件が100%masterに解決(未解決0)を検証済。★ISAMは参照整合性を強制しないため `EquipmentPartNumberIndex`→`EquipmentMaster` の FOREIGN KEY は張らず結合用の非クラスタ索引のみ(`sql/001_schema.sql`)。未了: siyosyo図面サイズ(`SiyosyoSizeCheck`)、printer.cns取込、機器マスタの補助情報/外形寸法など未展開フィールド。
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
- **更新の粒度（2026-07-31 改定 v2）**:
  - **各増分（1 関数移植）ごとに、git の 3 点**を更新してコミットする:
    - `docs/name-mapping.csv`（移植対応表・追記）
    - 本ファイル「5. 進捗」（全テスト数＋増分サマリ）
    - `docs/MIGRATION_PLAN.md`「0.2 進捗トラッキング」（テスト数・移植エントリ数・移植率・直近コミット）= 進捗率を常時反映
  - `docs/MIGRATION_PLAN.md` のフェーズ表（✅/🟡/❌）はフェーズ/マイルストーン達成時に更新。
  - `/memories/repo/ews-migration-roadmap.md`（git 外のローカル補助）は**恒久記録に数えない**。
    規約・落とし穴・フィールド対応表・「現状」ポインタのみを保持し、**新しい知見が出た時だけ**更新する
    （毎増分のフル記録は不要＝上記 git 3 点が担い、PC 故障時も GitHub から復元可能）。
  - **★メモリ固有情報を作らない（降格のデメリット緩和）**: メモリは git 外＝ワークスペース消失で失われるため、
    新しい落とし穴・フィールド対応・規約は**メモリだけでなく git にも一言残す**（規約/落とし穴は本ファイル §3、
    C名⇔C#名の対応は `docs/name-mapping.csv`）。これでメモリは純粋なキャッシュとなり、消えても git から復元できる。
    また「現状」ポインタは best-effort とし、**唯一の真実は git**（テスト数=`dotnet test`、進捗=MIGRATION_PLAN §0.2）とする。
  - コミット後は `git push origin main` する。
