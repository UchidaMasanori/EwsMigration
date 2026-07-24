/* =====================================================================================
   EWS 移行 パイロット用 SQL Server スキーマ (正規化)

   対象は回路解析(C原典: fyskews / Fysk10_Main)が触れるデータのみ。
   旧 ISAM ファイル / .cns マスタを正規化テーブルへ再設計する。
   元の C 構造体/ファイルIDは各テーブルのコメントに併記する。

   実行例: sqlcmd -S localhost -i sql/001_schema.sql
   ===================================================================================== */

IF DB_ID('Ews') IS NULL
    CREATE DATABASE Ews;
GO
USE Ews;
GO

/* ---------------------------------------------------------------------------
   機器マスター
   【C原典】struct FYDM805 (機器マスター, EWS-ISAM, レコード長 579)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.EquipmentMaster', 'U') IS NOT NULL
    DROP TABLE dbo.EquipmentMaster;
GO
CREATE TABLE dbo.EquipmentMaster
(
    -- 【C原典】PRIMARY キー = struct p805_key (yoyaku + mkcd + ptype + teikkey)。
    ReservedWord         NVARCHAR(8)   NOT NULL,   -- 【C原典】pkey.yoyaku[8]      予約語
    MakerCode            NVARCHAR(3)   NOT NULL,   -- 【C原典】pkey.mkcd[3]        メーカーコード
    ParameterType        NVARCHAR(49)  NOT NULL,   -- 【C原典】pkey.ptype[7][7]    パラメータタイプ
    RatingKey            NVARCHAR(80)  NOT NULL,   -- 【C原典】pkey.teikkey[80]    定格キー
    PartNumber           NVARCHAR(15)  NULL,        -- 【C原典】hinban[15]  (ALTERNATE キー1, 非一意/NULL あり)
    PartName             NVARCHAR(25)  NULL,        -- 【C原典】hinmei[25]  (ALTERNATE キー2)
    ElectricalParameters NVARCHAR(64)  NULL,        -- 【C原典】pstring[64]
    CONSTRAINT PK_EquipmentMaster PRIMARY KEY (ReservedWord, MakerCode, ParameterType, RatingKey)
);
GO

-- 【C原典】ALTERNATE キー1 = hinban (品番)。品番は非UNIQUE(同一品番が複数レコード)。
-- 一意性は品番索引 FYDF816 の「品番 + データ追番」で担保されるため、ここは非一意インデックス。
CREATE INDEX IX_EquipmentMaster_PartNumber
    ON dbo.EquipmentMaster (PartNumber)
    WHERE PartNumber IS NOT NULL;
GO

/* ---------------------------------------------------------------------------
   機器マスター品番索引
   【C原典】struct FYDF816 (機器マスター品番索引ファイル, EWS-ISAM, レコード長 184)
            キー = 品番 + データ追番。同一品番に追番(0001,0002,…)で複数レコードを持ち、
            それぞれが機器マスター(FYDM805)の PRIMARY キー(pkey)を指す。
            品番読み(FyMasFYDM805ByHinban / …ByHinbanPstr)はこの索引を追番順に走査する。
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.EquipmentPartNumberIndex', 'U') IS NOT NULL
    DROP TABLE dbo.EquipmentPartNumberIndex;
GO
CREATE TABLE dbo.EquipmentPartNumberIndex
(
    PartNumber     NVARCHAR(15) NOT NULL,   -- 【C原典】key.hinban[15]   品番
    DataNo         NVARCHAR(4)  NOT NULL,   -- 【C原典】key.datano[4]    データ追番 (0001,0002,…)
    -- 【C原典】pkey (機器マスター PRIMARY キー) を指す。
    ReservedWord   NVARCHAR(8)  NOT NULL,   -- 【C原典】pkey.yoyaku[8]
    MakerCode      NVARCHAR(3)  NOT NULL,   -- 【C原典】pkey.mkcd[3]
    ParameterType  NVARCHAR(49) NOT NULL,   -- 【C原典】pkey.ptype[7][7]
    RatingKey      NVARCHAR(80) NOT NULL,   -- 【C原典】pkey.teikkey[80]
    PartName       NVARCHAR(25) NULL,        -- 【C原典】hinmei[25]
    CONSTRAINT PK_EquipmentPartNumberIndex PRIMARY KEY (PartNumber, DataNo)
);
GO

-- 機器マスター(FYDM805)へ結合するためのインデックス。
-- 【注意】旧 ISAM は FYDF816 と FYDM805 の間に参照整合性を強制しない。実際に
-- FYDF816(品番索引)は FYDM805(機器マスター)に存在しない pkey も参照し得る
-- (エクスポート断面差・別ファイル由来)。このため FOREIGN KEY は張らず、
-- 結合性能のための非クラスタインデックスのみを設ける。
CREATE INDEX IX_EquipmentPartNumberIndex_Pkey
    ON dbo.EquipmentPartNumberIndex (ReservedWord, MakerCode, ParameterType, RatingKey);
GO

/* ---------------------------------------------------------------------------
   物件情報
   【C原典】struct FYDF801 (物件情報ファイル, EWS-ISAM, レコード長 1200)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.ProjectInfo', 'U') IS NOT NULL
    DROP TABLE dbo.ProjectInfo;
GO
CREATE TABLE dbo.ProjectInfo
(
    SalesOfficeCode     NVARCHAR(2)  NOT NULL,   -- 【C原典】key.im.eigyocd[2]  (第1キーの一部)
    ItemNumber          NVARCHAR(2)  NOT NULL,   -- 【C原典】key.meisaino[2]    (第2キー)
    DrawingNumberUpper  NVARCHAR(10) NULL,        -- 【C原典】zubanu10[10]
    DrawingNumberLower  NVARCHAR(5)  NULL,        -- 【C原典】zubanl5[5]
    DataStatus          NCHAR(1)     NULL,        -- 【C原典】datastat
    ManagementKind      NCHAR(1)     NULL,        -- 【C原典】datbukbn
    RegistrationKind    NCHAR(1)     NULL,        -- 【C原典】datzukbn
    NewRequestNumber    NVARCHAR(7)  NULL,        -- 【C原典】rk.airaino[7]
    NewItemNumber       NVARCHAR(2)  NULL,        -- 【C原典】rk.ameisano[2]
    AutoDrawingKind     NCHAR(1)     NULL,        -- 【C原典】autokbn
    CONSTRAINT PK_ProjectInfo PRIMARY KEY (SalesOfficeCode, ItemNumber)
);
GO

/* ---------------------------------------------------------------------------
   回路内容記述
   【C原典】struct FYDF805 (回路内容記述ファイル, EWS-ISAM)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.CircuitDescription', 'U') IS NOT NULL
    DROP TABLE dbo.CircuitDescription;
GO
CREATE TABLE dbo.CircuitDescription
(
    RequestNumber       NVARCHAR(7)   NOT NULL,   -- 【C原典】key.im.airaino[7]
    ItemNumber          NVARCHAR(2)   NOT NULL,   -- 【C原典】key.im.ameisano[2]
    LineNumber          INT           NOT NULL,   -- 【C原典】key.gyono[3]
    LineType            NVARCHAR(5)   NULL,        -- 【C原典】gyosyu[5]
    CircuitText         NVARCHAR(200) NULL,        -- 【C原典】kairoar[KAIROARLEN=200]
    OriginalLineNumber  INT           NULL,        -- 【C原典】orgno[3]
    Command             NCHAR(1)      NULL,        -- 【C原典】cmd
    CONSTRAINT PK_CircuitDescription PRIMARY KEY (RequestNumber, ItemNumber, LineNumber)
);
GO

/* ---------------------------------------------------------------------------
   部門(営業所/支店)マスター
   【C原典】bumon.*.cns (確認用/bumon.gai17.cns, Shift-JIS テキストマスタ)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.DepartmentMaster', 'U') IS NOT NULL
    DROP TABLE dbo.DepartmentMaster;
GO
CREATE TABLE dbo.DepartmentMaster
(
    DepartmentCode  NVARCHAR(8)  NOT NULL,   -- 【C原典】bumon.cns 1列目
    DepartmentName  NVARCHAR(40) NULL,        -- 【C原典】bumon.cns 2列目
    PhoneNumber     NVARCHAR(20) NULL,        -- 【C原典】bumon.cns 3列目
    CONSTRAINT PK_DepartmentMaster PRIMARY KEY (DepartmentCode)
);
GO

/* ---------------------------------------------------------------------------
   データファイル構成レジストリ
   【C原典】TOKUD/datafile.inf (ﾌｧｲﾙID, ﾌｧｲﾙﾊﾟｽID, ﾌｧｲﾙ名, ﾌｧｲﾙ名称)
            旧 FyGetFileName/FyGetFilePath のパス解決を本テーブルへ移送。
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.DataFileRegistry', 'U') IS NOT NULL
    DROP TABLE dbo.DataFileRegistry;
GO
CREATE TABLE dbo.DataFileRegistry
(
    FileId       NVARCHAR(16)  NOT NULL,   -- 【C原典】datafile.inf 1列目 (例 FYDM805)
    PathId       NVARCHAR(16)  NULL,        -- 【C原典】datafile.inf 2列目 (例 MSTCL)
    FileName     NVARCHAR(64)  NULL,        -- 【C原典】datafile.inf 3列目
    DisplayName  NVARCHAR(64)  NULL,        -- 【C原典】datafile.inf 4列目 (ﾌｧｲﾙ名称)
    TableName    NVARCHAR(128) NULL,        -- 新設: 論理ファイルID→SQL テーブル名の対応
    CONSTRAINT PK_DataFileRegistry PRIMARY KEY (FileId)
);
GO

/* ---------------------------------------------------------------------------
   部署別仕様書種別マスター
   【C原典】siyosyo.*.cns (確認用/siyosyo.gai17.cns, Shift-JIS テキストマスタ)
            struct SIYO_INFO(bumon=部署) → SYUBETU_INFO s_info[](仕様書種別) の階層。
            Zs20SiyoInfoRead(toku/interf/zs50/src/Fymzs40Cns.c) が読み込む。
            図面サイズ(no/scale/zmnsyu/kenzu = SiyosyoSizeCheck 依存)は取り込まない。
   KindSeq は s_info[] の並び順(0起点)を保持し、C 原典の配列順を再現する。
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.SpecificationFile', 'U') IS NOT NULL
    DROP TABLE dbo.SpecificationFile;
GO
IF OBJECT_ID('dbo.SpecificationKind', 'U') IS NOT NULL
    DROP TABLE dbo.SpecificationKind;
GO
CREATE TABLE dbo.SpecificationKind
(
    DepartmentCode  NVARCHAR(8)   NOT NULL,   -- 【C原典】SIYO_INFO.bumon        部署:
    KindSeq         INT           NOT NULL,   -- s_info[] のインデックス(0起点)
    KindName        NVARCHAR(128) NOT NULL,   -- 【C原典】SYUBETU_INFO.kaninm    仕様書:
    Description     NVARCHAR(256) NULL,        -- 【C原典】SYUBETU_INFO.explain   仕様書説明:
    Path            NVARCHAR(256) NULL,        -- 【C原典】SYUBETU_INFO.path      仕様書パス:
    CONSTRAINT PK_SpecificationKind PRIMARY KEY (DepartmentCode, KindSeq)
);
GO

CREATE TABLE dbo.SpecificationFile
(
    DepartmentCode  NVARCHAR(8)   NOT NULL,   -- 【C原典】SIYO_INFO.bumon
    KindSeq         INT           NOT NULL,   -- 種別の並び順(SpecificationKind へ対応)
    FileSeq         INT           NOT NULL,   -- 【C原典】file_name[] のインデックス(0起点)
    FileName        NVARCHAR(256) NOT NULL,   -- 【C原典】SYUBETU_INFO.file_name[]  仕様書ファイル:
    CONSTRAINT PK_SpecificationFile PRIMARY KEY (DepartmentCode, KindSeq, FileSeq),
    CONSTRAINT FK_SpecificationFile_Kind
        FOREIGN KEY (DepartmentCode, KindSeq)
        REFERENCES dbo.SpecificationKind (DepartmentCode, KindSeq)
);
GO
/* ---------------------------------------------------------------------------
   物件情報(物件共通情報)
   【C原典】struct FYDF801 (物件情報ファイル, EWS-ISAM, レコード長 1200)
            キー = 依頼明細番号(依頼番号 7 + 明細番号 2)。明細番号ブランクが物件共通情報、
            '01'～'99' は盤明細情報(union redefines)。回路解析エンジンは物件共通情報の
            周波数区分(hzkbn)・製作仕様区分(sshiykbn)を参照する。
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.ProjectInformation', 'U') IS NOT NULL
    DROP TABLE dbo.ProjectInformation;
GO
CREATE TABLE dbo.ProjectInformation
(
    RequestNumber          NVARCHAR(7)   NOT NULL,   -- 【C原典】key.im (eigyocd[2]+filler1[5]) 依頼番号
    DetailNumber           NVARCHAR(2)   NOT NULL,   -- 【C原典】key.meisaino[2]      明細番号 ' '/'01'～'99'
    DrawingNumberUpper     NVARCHAR(10)  NULL,        -- 【C原典】zubanu10[10]         図番(上10桁)
    DrawingNumberLower     NVARCHAR(5)   NULL,        -- 【C原典】zubanl5[5]           図番(下5桁)
    SalesOfficeName        NVARCHAR(30)  NULL,        -- 【C原典】com.kyo.eigyonm[30]  営業所名
    StaffName              NVARCHAR(14)  NULL,        -- 【C原典】com.kyo.tantonm[14]  担当者名
    ProjectName1           NVARCHAR(30)  NULL,        -- 【C原典】com.kyo.kenmei1[30]  件名1
    ProjectName2           NVARCHAR(30)  NULL,        -- 【C原典】com.kyo.kenmei2[30]  件名2
    ManufacturingSpecKind  NVARCHAR(2)   NULL,        -- 【C原典】com.kyo.sshiykbn[2]  製作仕様区分(エンジン参照)
    SpecificationName      NVARCHAR(34)  NULL,        -- 【C原典】com.kyo.shiyonm[34]  仕様名称
    DrawingKind            NVARCHAR(1)   NULL,        -- 【C原典】com.kyo.zumenkbn     図面種別
    DrawingRank            NVARCHAR(1)   NULL,        -- 【C原典】com.kyo.zumenrnk     図面ランク
    FrequencyKind          NVARCHAR(1)   NULL,        -- 【C原典】com.kyo.hzkbn        周波数区分(エンジン参照)
    CONSTRAINT PK_ProjectInformation PRIMARY KEY (RequestNumber, DetailNumber)
);
GO
/* ---------------------------------------------------------------------------
   盤明細情報
   【C原典】struct FYDF801 の union com.mei = struct bmeisai (盤明細情報, fydf801m.h)
            明細番号(meisaino)非ブランク('01'～'99'/'0A'～'0D')のレコード。
            Fyss12 の SEP 作図判定(PropChkSEPBox)がボックスフカサ(BoxDepth=boxsund)を参照する。
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.PanelDetailInformation', 'U') IS NOT NULL
    DROP TABLE dbo.PanelDetailInformation;
GO
CREATE TABLE dbo.PanelDetailInformation
(
    RequestNumber              NVARCHAR(7)   NOT NULL,   -- 【C原典】key.im (eigyocd[2]+filler1[5]) 依頼番号
    DetailNumber               NVARCHAR(2)   NOT NULL,   -- 【C原典】key.meisaino[2]      明細番号 '01'～'99'/'0A'～
    PanelName                  NVARCHAR(30)  NULL,        -- 【C原典】com.mei.bannm[30]    盤名称1
    PanelNameKana              NVARCHAR(30)  NULL,        -- 【C原典】com.mei.bannmkng[30] 盤名称2
    StandardCompoSelectionKind NVARCHAR(1)   NULL,        -- 【C原典】com.mei.hycpskbn     標準・コンポ盤選定区分
    Quantity                   NVARCHAR(2)   NULL,        -- 【C原典】com.mei.suuryo[2]    数量
    BoxPartNumber              NVARCHAR(15)  NULL,        -- 【C原典】com.mei.boxhinbn[15] ボックス品番
    BoxType                    NVARCHAR(8)   NULL,        -- 【C原典】com.mei.boxtype[8]   ボックスタイプ
    BoxHeight                  NVARCHAR(5)   NULL,        -- 【C原典】com.mei.boxsunh[5]   ボックス寸法タテ(mm)
    BoxWidth                   NVARCHAR(5)   NULL,        -- 【C原典】com.mei.boxsunw[5]   ボックス寸法ヨコ(mm)
    BoxDepth                   NVARCHAR(5)   NULL,        -- 【C原典】com.mei.boxsund[5]   ボックス寸法フカサ(mm)(SEP判定で参照)
    CONSTRAINT PK_PanelDetailInformation PRIMARY KEY (RequestNumber, DetailNumber)
);
GO
