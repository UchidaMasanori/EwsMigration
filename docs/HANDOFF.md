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

回路解析（`toku/sekkei` 系）を先行移植中。**全 279 テスト成功 / 1 スキップ / 0 失敗**。

### 移植済み

- **回路文字列チェック** `CircuitStringChecker`（`Fyss11.c`/`Fyss1c.c`/`Fyss1d.c`）
  - 系統/行種/仕様テーブル生成、盤名称・入線・有電源等の行種ディスパッチ
  - 予約語解決（`ResolveReservedWord`。特殊キー 27A/27B/27C・SL・G1-4・FLT の短絡一致）
  - 電気パラメータ → 定格値（key_tbl）格納の配線（`Check_Kikimei`→`Parm_Check_Main` 相当）
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
   - step17 主回路ファイルエリア数量分解: `Fyss12_Make_Main_Sub`/`Main_File_Area_Make`（`Find_Iteration`/`Find_Nobangou`/`Find_Group` → `MainCircuitSegment`）。FYRT800 レコード整形（`mainfile_set`）は決定的スライス＋回路要素区分（`kiryoso`=`Find_Kairo_Kubun`）を移植
   - step17後 入力順固定項目チェック（`Fyss1m_Input_Check`/`Fyss1m_Input_Check_CT_AM`）: 計器回路でない AM の直後が計器回路でない CT のとき FY-645E

### 未移植・TODO（次にやること）

- **step6 の本体（残り）**: CT/VT/WH/ZCT 計器回路の主回路展開は移植済み（`ConsolidateCurrentTransformerCircuit`/`ConsolidateVoltageTransformerCircuit`/`ConsolidateSingleInstrumentCircuit`：同一 G_No の計器区分（K_Kubun=K）群を走査し、計器回路（`Kikitable_Keiki_Make`）／主回路（`Kikitable_Main_Make`）を末尾追加 → step7 で整列。VT は exist_CT/exist_WH の二経路を忠実移植）。**未移植（保留）**：
  - **SEP 追加**（`Kikitable_SEP_Make`, 系統ブレーク時）: `PropChkSEPBox`/`PropChkHbnHB300`（改訂<12> の bukken FYDF801 プロパティ照会）とグループ別 souden 差分判定に依存。データで検証できるようになってから着手（推測移植は忠実性を損なうため保留）。
- **主回路生成の step14/15**: `Kairo_Group_Set`/`Kiki_Equal_Bangou_Set` はいずれも C原典でコメントアウト（無効化）済のため意図的にスキップ（コメントで明示）。step17後の `Fyss1m_Input_Check`（CT/AM 入力順）は移植済み（回路要素区分 `kiryoso`=`Find_Kairo_Kubun` を `MainAreaSet` で設定して判定）。`PropSetInvbpKbn`（改訂<16>/<18> INVBP 区分）は kairsfx/tokkbn と INVBP 追加機器が上流 Fyss13-15 未移植依存のため保留。step17 FYRT800 レコード整形は **決定的スライスを移植済み**：`MainAreaSet` の決定的フィールド（datano/kno/ksyubetu/yoyaku/ysno/yssfx/gyocd/kiryoso/skno/narakbn/doukkno/jagekbn/P 系統座標/**kairsfx**（`Max_Bunno_Find`/`Max_Kbangou_Find`）/**gyono**（`Find_Bangou`=行種名後方数値）/**ep[0]**（`eparm_set` 統合＝`EquipmentParameterFormatter.EparmSet` 呼出）/**epabn**（盤名称状態 epabn/bepabn を `CircuitParseResult` で伝搬）/**epaqty**（F/VT/CT の Kosu→QTY））を Simple/Iteration セグメント経路で `CircuitParseResult.MainCircuits` に生成。**未移植（TODO）**：datatype（KIKITABLE.DTYPE 未モデル化）、fp（付属パラメータ fparmg：DLW/DLV/DLN/DCM/DIT/DSP 未モデル化）、`Make_n` 結線 — いずれも上流 Fyss13-15（機器選定・型式展開）未移植のため保留。
- **上流パラメータ生成（Fyss13-15 機器選定・型式展開）**: 着手。`Fyss14.c`（電圧値の継承）の決定的スライス `Volt_Conv`/`Max_Volt`/`Right_Volt`/`Left_Volt` を `VoltageInheritance`（電圧3要素配列の変換・整列）に移植（純粋関数・単体テスト済）。`Fyss13.c` の `Fyss13_Make_Control`（制御回路エリア生成）は FYRT802/FYRT820（制御仕様・制御回路設計エリア）依存の新規サブシステムのため本格移植は保留。ep/fp 電気パラメータ本体（`Kairo_Parm_Set`/`mcprmcnv`/`Parm_Set_*`）は MCPRMS・ISAM マスタ読込依存で継続保留。
- **電気パラメータエンジン**（key_check の値検証）: **全 key_check 型を移植完了**。データ駆動 `KeyCheckRules`（MCB/MC/MG/THR/… 変流器/リレー/スイッチ/ブザー/フィーダ/インバータ/ユニット化スイッチ等の全型）＋ TR 専用パーサ（`TrCheckMain`）＋ NT(奇数丸め)/WH(n_kigo 副記号)専用ハンドラ。予約語別名は共有ルール配列(GxRules/XlRules/XeryRules/SlxRules/FltxRules/UnitSwitchRules)。STM/SIR/C/R/D/NICA/RE/VVVF/TVX は C 原典 return 0 のため構造検証のみ。
- **型式展開（eparm_set 電気パラメータ整形）**: 着手（Wave1）。`eparm_set`（`Fyss1f.c:2208`）を `EquipmentParameterFormatter.EparmSet`（`RatingValues`→`ElectricalParameters`、key_check の逆写像）に移植。整形ヘルパ `set_9`/`chk_9`/`Stof` を C の atof×multiple＋sprintf 書式（`%09.3f` 等）で固定長化。Wave1 は遮断器系 MCB/ELB/MMCB/ELMB/SB を収録（極数/エレメント(e==0→9)/AF/AT(MCB・ELB は Stof==0 かつ非空で `99999.999`)/感度電流 MA/負荷容量 kW×1000/AC・DC 区分/定格電圧2）。`struct eparmg`→`ElectricalParameters`（全フィールドを Main_Area_Clear 相当の 0 埋めで初期化）。残り予約語（PS/P/UP・MC/計器系 VM/AM/VT/CT・TR 多スロット・ZCT/LGR/… 等）と `MainAreaSet` の ep/fp 統合は後続 Wave。
- **型式展開 Wave2（漏電遮断器系 RMCB/RELB/RMMCB/RELMB）**: `eparm_set`（`Fyss1f.c:2366-2458`）を移植。制御電圧 `vc`/`fvc`→`epavc`（`%03.0f`）/`epavckbn`（A/D 区分）を `SetBreaker` に `hasVc` として追加。R 系は e==0→9 変換なし・AT の Stof 特殊処理なし。AF は from_length=2、AT は RMCB/RELB=2 桁・RMMCB/RELMB=5 桁。RELB/RELMB は感度電流 MA も整形。テスト計 348 pass/1 skip。
- **型式展開 Wave3-5（引込・電磁接触器系・端子台計器系）**: `eparm_set`（`Fyss1f.c:2219-2568`）を移植。Wave3 引込 PS/P/UP（相数/線式 epaph2[0]/epawr2[0]、定格電圧2 の3スロット epav2[0..2]、P は電線サイズ epasq/epaesq と芯数 epac/本数 epaksu を追加）。Wave4 MC/THR/MG/SC（接点数 epaac/epabc、SC は KVAR/UF/HZ）。Wave5 NT/WH/VM/AM/VT/CT/VS/AS（1次側 epaa1/epav1[0]、VT/CT は VA epava、VS/AS は相数/線式のみ）。各機種で AF/AT/A/SA/VA の from_length が fyrt811 のフィールド幅どおり異なる点に注意。共通ヘルパ `FvKbn`（AC/DC 区分）を追加。テスト計 364 pass/1 skip。
- **型式展開 Wave6（TB/CON/TR 多スロット変圧器）**: `eparm_set`（`Fyss1f.c:2560-2617`）を移植。TB（極数/電流/電圧/電線サイズ epasq）・CON（極数1桁/電流2桁/電圧）。TR は最複雑分岐: 1次相数/線式（epaph1/epawr1）+ 定格電圧1 3スロット（epav1[0..2]、4文字目 T でタップ epav1idx）、2次相数 PH2/線式 WR2 を chk_9≠0 のものから順詰め（p2/p3・w2/w3）、AC/DC は fv2 が A/D なら fv2 さもなくば fv3、2次電圧 V2 は v2[i] の chk_9≠0 を epav2[i] へ（T で epav2idx）+ v3[0]→epav2[1]/v3[1]→epav2[2] 上書き、定格容量 va→epava。テスト計 371 pass/1 skip。
- **型式展開 Wave7+（残り全予約語・eparm_set 移植完了）**: `eparm_set`（`Fyss1f.c:2617-3218`）の残り約60予約語を全移植し全99分岐を収録完了。ZCT/LGR(感度電流4スロット)/ELR/HPSB/HSB/RRY/RTR/MCDT/F/LA/DCPW/CR/TM(タイマ)/TS/G系(G/G1-G4/GI/GP/GPN)/表示灯WL・GL・RL・OL・FL・BL/COS/PBS/SSW/TSW/BZ/BEL/CP/RSW/EE/HM/XERY/CKS/CSDT/CU/TU/NHMB/APN/SL系/LGT/BLTR/PLTR/LSW/DSW/SV/MV/KPRY/THSW/L/IDF/HDF/MDF/WDP/MCFR/MGFR/MCSD/MGSD/MGLD/MGCS/INV/DCSIR/DCNI/MCFRSD/MGFRSD/TSU系(TSU/SSWU/PBSU/COSU/2COSU/OLU)。C忠実再現: XERY=接尾一致(memcmp &yoyaku[1]=ERY)/FLTx=先頭3文字一致/TM時間単位倍率(nset・nss・ns=1:×1・2:×60・3:×3600)/BZ・BEL・MV=fwvaでW-VA振分(BELのWは×1000)/CKS・MGFR・MGSD・MGFRSDはepae直接代入/FLは表示灯群優先(後方の単独FLは不到達)/DCSIR・DCNIはfv→fvdcでepav2kbn上書き/STM・SIR・C・R・D・NICA・RE・VVVF・TVは空分岐で0埋めのまま。共通処理はApplyV2/ApplyVc/SetWva/SetTimer/EDirectに集約。テスト計 388 pass/1 skip。
- **ゴールデン突合（実案件データ・型式展開検証）**: 実案件(EWS/WORK配下)のFYDF805(回路内容記述,RL=270,kairoar@+17)→key_check→eparm_set→eparmg固定長化を、実FYDF806(RL=1219,yoyaku@+38,ep[0]@+114)のep[0]と突合する`GoldenComparisonHarnessTests`を新設。`EparmgCodec`(Ews.Domain.Analysis)がElectricalParametersをeparmg構造体宣言順の253バイト固定長へ相互変換。(1)実ep[0..2]を復元→再直列化する往復検証で案件139・11,517レコードがバイト完全一致し、253バイトレイアウト(fycommon.h)の実機一致を実証。(2)単一機器行(複合オプション・数量展開を除く)でeparm_setが書き換えたフィールドのみを実ep[0]群と存在照合し内容不一致0件。QTY/BN等のeparm_set対象外(外部設定)フィールドは比較から除外。CU/TS/PBS等の制御系機器は主回路806に対応レコードが無く主回路対象外として集計。WORK未配置環境はスキップ。テスト計 390 pass/0 skip。
- **MainAreaSet への eparm_set 統合（ep/kairsfx/gyono/epabn/epaqty）**: `mainfile_set`（`Fyss1f.c:1464`）の残りフィールドを移植。(1) **ep[0]**: 定格キー（`RatingValues`）検証済み機器で `EquipmentParameterFormatter.EparmSet` を呼び出し `MainCircuitData.ElectricalParameterSlots[0]` を生成（`eparm_set` 全99分岐が移植済のため解放）。(2) **epabn（盤種類 BN）**: P/SP/MP/UP は盤番号（`Ban`）で確定、それ以外は直前状態（bepabn）継承。C の static epabn/bepabn を `CircuitParseResult.PanelNameKind`/`PanelNameKindPrevious` で伝搬し、`BuildMainCircuitFileArea`（=`Fyss12_Make_Main_Sub`）冒頭で epabn='1' リセット。先行する P（盤）レコードが無い場合の bepabn 初期値 '\0' も C 忠実（DLN→fp.fpaln[1] 代用セットは fp 未モデル化で未実装）。(3) **epaqty（手配数量 QTY）**: F/VT/CT は Kosu（3→'3',2→'2',4→'4',他→'1'）、それ以外は '1'。(4) **kairsfx（生成回路サフィックス）**: `Max_Bunno_Find`/`Max_Kbangou_Find`（同一 G_No 前方走査で最大 B_No/N_No）＋近傍機器（S_Kiki±i=`parse.MainEquipment[kikiIndex±i]`、範囲外は G_No=-1）＋ bunkind（950906 GKosu 差分判定）で 'A' 起点サフィックスを組立。(5) **gyono（行種番号）**: `Find_Bangou`（`Fysscommon.c:407`、行種名原文後方の連続数値抽出、Fyss11.c 版とは別関数）で `LineTypeRaw`→数値→"%02d"。**未移植**: datatype（DTYPE 未モデル化）・fp（fparmg 未モデル化）。テスト計 395 pass/0 skip。
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
- 節目ごとに本ファイルの「5. 進捗」を更新してコミットすること。
