-- Reviewed import generated from:
--   pods.xlsx (pod and station sheets)
--   Sales Mapping With Dealer and POD.xlsx (Sheet1)
--
-- Review decisions applied here:
--   * All 273 master PODs are inserted with PODStationId = NULL. Station
--     assignment is intentionally left to the web application's POD Assignment
--     Management page; no workbook station pairing is auto-applied.
--   * POD ownership is stored in SalesPersonPOD and the legacy POD.salespersonId
--     column is synchronised from that table.
--   * Dealer.Name = Dealer.Code because the workbook has no dealer names.
--   * Conflicting manager links are left NULL and reported, not guessed.
--   * Sanjib karki is normalized to Sanjib Karki. Station spellings from the
--     mapping sheet (Trisuli, Devinagar) are documented but not applied because
--     station assignment is a separate reviewed workflow.
--
-- The script is idempotent: it matches natural keys (OSS ID / name / code),
-- never overwrites existing names, designations, managers, or ownership, and
-- leaves unrelated/demo rows untouched. Run database/Upgrade-PODMasterData.sql
-- first so the OSS columns and PODStationId have their target types.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.SalesPersonPOD', N'U') IS NULL
BEGIN
    THROW 51002, 'Run database/Upgrade-SalesPersonPOD.sql before this import.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.columns AS c
    INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.POD')
      AND c.name = N'OSSPODID'
      AND t.name = N'int'
)
BEGIN
    THROW 51003, 'dbo.POD.OSSPODID must be INT. Run database/Upgrade-PODMasterData.sql before this import.', 1;
END;

BEGIN TRANSACTION;

DECLARE @Review TABLE
(
    Area NVARCHAR(40) NOT NULL,
    SourceRow INT NULL,
    Item NVARCHAR(200) NOT NULL,
    Detail NVARCHAR(500) NOT NULL
);

-- ---------------------------------------------------------------- POD stations
DECLARE @Stations TABLE (SourceId INT NOT NULL PRIMARY KEY, Name NVARCHAR(150) NOT NULL);
INSERT INTO @Stations (SourceId, Name) VALUES
    (1, N'Dharan POD Station'),
    (2, N'Janakpur POD Station'),
    (3, N'Butwal POD Station'),
    (4, N'Birtamode POD Station'),
    (5, N'Biratnagar POD Station'),
    (6, N'Inaruwa POD Station'),
    (7, N'Itahari POD Station'),
    (8, N'Kakadvitta POD Station'),
    (9, N'Banepa POD Station'),
    (10, N'Mahalaxmi POD Station'),
    (11, N'Kapan POD Station'),
    (12, N'Gongabu POD Station'),
    (13, N'Dakshindhoka POD Station'),
    (14, N'Bhaktapur POD Station'),
    (15, N'Sitapaila POD Station'),
    (16, N'Chitwan POD Station'),
    (17, N'Kathmandu POD Station'),
    (18, N'Pokhara POD Station'),
    (19, N'Bardibas POD Station'),
    (20, N'Birganj POD Station'),
    (21, N'Dhangadhi POD Station'),
    (22, N'Hetauda POD Station'),
    (23, N'Mahendranagar POD Station'),
    (24, N'STN POD Station'),
    (25, N'Nepalgunj POD Station'),
    (26, N'Damak POD Station'),
    (27, N'Kawasoti POD Station'),
    (28, N'Surkhet POD Station'),
    (29, N'Unknown POD Station'),
    (30, N'Bhairawa POD Station'),
    (31, N'Lahan POD Station'),
    (32, N'Dang POD Station'),
    (33, N'Yekantakuna POD Station'),
    (34, N'Lalbandi pod station'),
    (35, N'Gorkha POD Station'),
    (36, N'Palpa POD Station'),
    (38, N'Baundangi POD Station'),
    (40, N'Gaighat Pod Station'),
    (41, N'Dhading POD Station'),
    (42, N'Tandi pod station'),
    (43, N'Bansgadi POD station'),
    (45, N'Nayagaun POD Station'),
    (47, N'Attariya POD Station'),
    (49, N'Surunga POD Station'),
    (51, N'Belbari POD Station'),
    (52, N'Damauli POD Station'),
    (53, N'Chapur POD Station'),
    (54, N'Haldibari POD Station'),
    (55, N'Jhalari POD Station'),
    (56, N'Trishuli POD Station'),
    (57, N'Galchi POD Station'),
    (58, N'SNS Bhaktapur POD Station'),
    (59, N'Bardaghat POD Station'),
    (60, N'Chandrauta POD Station'),
    (61, N'Unverified Smarten POD station'),
    (62, N'Unverified Optinet POD station'),
    (63, N'Dodharachandani POD Station'),
    (64, N'Hasuliya POD Station'),
    (65, N'Unverified FiberKing POD station'),
    (66, N'Unverified Paschimanchal POD station'),
    (67, N'Syangja POD Station'),
    (68, N'Lamahi POD Station'),
    (69, N'Rajbiraj POD Station'),
    (70, N'Devinagar POD Station'),
    (71, N'Malangwa POD Station'),
    (74, N'Kohalpur POD Station'),
    (75, N'Tulsipur POD Station'),
    (76, N'Piluwa POD Station'),
    (77, N'Lamki POD Station'),
    (78, N'LCO POD Station'),
    (87, N'Simara POD Station'),
    (88, N'Test POD Station'),
    (89, N'Nagarkot POD Station');

INSERT INTO @Review (Area, SourceRow, Item, Detail)
SELECT N'Station conflict', NULL, s.Name,
       N'An existing station already uses this name or source ID with different data; left untouched.'
FROM @Stations AS s
WHERE EXISTS (SELECT 1 FROM dbo.PODStation AS x
              WHERE x.Name = s.Name AND x.OSSPODStationId <> s.SourceId)
   OR EXISTS (SELECT 1 FROM dbo.PODStation AS x
              WHERE x.OSSPODStationId = s.SourceId AND x.Name <> s.Name);

INSERT INTO dbo.PODStation (OSSPODStationId, Name)
SELECT s.SourceId, s.Name
FROM @Stations AS s
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.PODStation AS x
    WHERE x.OSSPODStationId = s.SourceId
       OR x.Name = s.Name
);

-- ---------------------------------------------------------------- all master PODs
DECLARE @Pods TABLE (SourceId INT NOT NULL PRIMARY KEY, Name NVARCHAR(150) NOT NULL);
INSERT INTO @Pods (SourceId, Name) VALUES
    (1, N'Samakhusi'),
    (2, N'Inaruwa'),
    (3, N'Halchowk'),
    (4, N'Sitapaila'),
    (5, N'Imadol'),
    (6, N'Manamaiju'),
    (7, N'Gongabu'),
    (8, N'Dhapasi'),
    (9, N'Sanepa'),
    (10, N'Dhumbarahi'),
    (11, N'Budhanilkantha'),
    (12, N'Chapali'),
    (13, N'Golfutar'),
    (14, N'Pepsicola'),
    (15, N'Jorpati'),
    (16, N'Boudha'),
    (17, N'Kapan'),
    (18, N'Bhaisepati'),
    (19, N'Pulchowk(STN)'),
    (20, N'Baneshwor(STN)'),
    (21, N'Tinkune(STN)'),
    (22, N'Dhapakhel'),
    (23, N'Chauni'),
    (24, N'Syuchatar'),
    (25, N'Kirtipur'),
    (26, N'Tikathali'),
    (27, N'Hetauda'),
    (28, N'Bagdole(P)'),
    (29, N'Madhyapur Thimi'),
    (30, N'Hetauda Bhuwan Singh Marga'),
    (31, N'Dhalkebar'),
    (32, N'Tarahara'),
    (33, N'Bhetghat Chowk'),
    (34, N'Pakali'),
    (35, N'Thecho(P)'),
    (36, N'Chapagaun(P)'),
    (37, N'Kalimati'),
    (38, N'Newbaneshwor(P)'),
    (39, N'Lubu(P)'),
    (40, N'Mulpani'),
    (41, N'Bhaisepati1(P)'),
    (42, N'Suryabinayak'),
    (43, N'Taukhel(P)'),
    (44, N'Chandragiri(P)'),
    (45, N'Kamane'),
    (46, N'Chyasal(P)'),
    (47, N'Gwarko(P)'),
    (49, N'Bhaktapur'),
    (50, N'Mahendrachowk'),
    (51, N'Kanchanbari'),
    (52, N'Kamaladi(P)'),
    (53, N'Thamel'),
    (54, N'Sukhanagar'),
    (55, N'Sankhamul'),
    (56, N'Bardibas'),
    (57, N'Shankarnagar'),
    (58, N'Chatara Line'),
    (59, N'Budhasubba'),
    (60, N'Mahadev chowk'),
    (61, N'Subidhanagar'),
    (62, N'Janakpur'),
    (63, N'Pidari'),
    (64, N'Zeromile'),
    (65, N'Lazimpat'),
    (66, N'Birgunj Panitanki'),
    (67, N'Birgunj Adarshanagar'),
    (68, N'Narephat'),
    (69, N'Devinagar'),
    (70, N'Roadcyess'),
    (71, N'Biratnagar'),
    (72, N'Banepa'),
    (73, N'Panauti'),
    (74, N'Sinamangal'),
    (75, N'Dhulikhel'),
    (76, N'Newroad(P)'),
    (77, N'Fulbari(P)'),
    (78, N'Zero(P)'),
    (79, N'Bagar(P)'),
    (80, N'Drivertole'),
    (81, N'Satungal'),
    (82, N'Kalaiya'),
    (83, N'Bhagwan Chowk'),
    (84, N'Tokha Chandeshwori'),
    (85, N'Adwa Khola'),
    (86, N'Thaiba(P)'),
    (87, N'Hattiban(P)'),
    (89, N'Birauta(P)'),
    (90, N'Lahan'),
    (91, N'Taudaha'),
    (92, N'Siddharthanagar'),
    (93, N'Bhairahawa'),
    (94, N'Goldhunga'),
    (95, N'Balkot'),
    (96, N'Bharatpur'),
    (97, N'Sukranagar'),
    (98, N'Gaidakot'),
    (99, N'Ratnanagar'),
    (100, N'Khaireni'),
    (101, N'Dhaijan'),
    (102, N'Tandi'),
    (103, N'Aayabari'),
    (104, N'Manigram'),
    (105, N'Damak'),
    (106, N'Dakshinkali'),
    (107, N'Kuleshwor'),
    (108, N'Nayagaun'),
    (109, N'Matatirtha'),
    (110, N'Dhangadi Uttar Behadi'),
    (111, N'Dhangadi Buddha Chowk'),
    (112, N'Kapan Gamcha'),
    (113, N'Majheripatan'),
    (114, N'Changunarayan'),
    (115, N'Narayanghat'),
    (116, N'Dhamboji'),
    (117, N'Attariya'),
    (118, N'Kudar'),
    (119, N'Kharipati'),
    (129, N'Budibazar'),
    (130, N'Bageshwori'),
    (131, N'Dang Gulariya'),
    (132, N'Mahendranagar Puspa Lal Chowk'),
    (133, N'Mahendranagar Fulbari Chowk'),
    (134, N'Dang Ghorahi'),
    (135, N'Kohalpur Pragatinagar'),
    (136, N'Kohalpur Sajha Chowk'),
    (137, N'Kohalpur Naulapur Panitanki'),
    (138, N'Bhrikuti Chowk'),
    (139, N'Kawasoti Ramnagar Chowk'),
    (140, N'Surkhet Mangalgadhi'),
    (141, N'Surkhet Bhiravsthan'),
    (142, N'Surkhet Kalinchowk'),
    (143, N'Kalinjor'),
    (144, N'Mahendranagar Basantapur Chowk'),
    (145, N'Gorkha'),
    (146, N'Palpa Tansen'),
    (147, N'Gharipatan'),
    (148, N'Bartung'),
    (149, N'Lalbandi'),
    (150, N'Hemja'),
    (151, N'Baundangi'),
    (152, N'Tulsipur Hathikhuwa'),
    (153, N'Tulsipur Tarigau'),
    (154, N'Tulsipur Balapur'),
    (155, N'Aamdangi'),
    (156, N'Churn Customer'),
    (157, N'Lamachaur'),
    (158, N'Mahendranagar Brahmadev'),
    (160, N'Power House'),
    (161, N'Bansgadhi Hashanapur'),
    (162, N'Bansgadhi Deudakala'),
    (163, N'Patihani'),
    (164, N'Hakim Chowk'),
    (165, N'Geetanagar'),
    (166, N'Hariwon'),
    (167, N'Gaighat'),
    (168, N'Arjundhara'),
    (169, N'Dhading Besi'),
    (170, N'Kankai'),
    (171, N'Madanpur'),
    (172, N'Surunga'),
    (173, N'Udayapur Gaighat'),
    (174, N'Beli'),
    (175, N'Kaluwapur'),
    (176, N'Syaule'),
    (177, N'Fulbari Dipobazar'),
    (178, N'Itahari RO'),
    (179, N'Duhabi DWDM'),
    (182, N'Damauli Bhadgau'),
    (183, N'Damauli Simaltar'),
    (184, N'Belbari Betana'),
    (185, N'Belbari'),
    (186, N'Chapur Basantapur'),
    (187, N'Chapur Gaidatar'),
    (188, N'Haldibari Sainikbazar'),
    (189, N'Haldibari Parakhopi'),
    (190, N'Damauli'),
    (191, N'Haldibari'),
    (192, N'Jhalari Bazar'),
    (193, N'Trishuli'),
    (194, N'Trishuli Batar'),
    (195, N'Jhalari Kalapani'),
    (196, N'Belbari Lalbhitti'),
    (197, N'Jhalari Pipladi'),
    (198, N'Galchi'),
    (199, N'SNS Balkot'),
    (200, N'Chapur Bazar'),
    (201, N'Unverified Smarten POD'),
    (202, N'Unverified Optinet POD'),
    (203, N'Hasuliya'),
    (204, N'Adamghat'),
    (205, N'Pame'),
    (206, N'Malekhu'),
    (207, N'Dodhara Chandani'),
    (208, N'Parsauni'),
    (209, N'Unverified Fiberking POD'),
    (210, N'Unverified Paschimanchal POD'),
    (211, N'Lakeside'),
    (212, N'Putalibazar'),
    (213, N'Lamahi Chaulahi'),
    (214, N'Lamahi'),
    (215, N'Rajbiraj'),
    (216, N'Malangwa Bazar'),
    (217, N'Malangwa Gamahariya'),
    (218, N'Urhari'),
    (219, N'Rajbiraj Tilathai Road'),
    (220, N'Optimized Jorpati'),
    (221, N'Bhagwanpur'),
    (222, N'Fiberking Portshift CDR'),
    (223, N'Smarten Portshift BAG'),
    (224, N'Smarten Portshift CDR'),
    (225, N'Smarten Portshift EDR'),
    (226, N'Smarten Portshift MWDR'),
    (227, N'Optinet Portshift EDR'),
    (228, N'DMN Portshift BUTWAL'),
    (229, N'Optinet Portshift Bagmati'),
    (230, N'Optinet Portshift Pokhara'),
    (231, N'Optinet Portshift FWDR'),
    (232, N'Optimized Kapan'),
    (233, N'Optimized Boudha'),
    (234, N'Optimized Pepsicola'),
    (235, N'Unverified Bhakatpur POD'),
    (236, N'Damak NEA SW'),
    (237, N'Pathari Switch'),
    (238, N'Birtamode NEA SW'),
    (239, N'Biratchowk Switch'),
    (240, N'Rupani NEA SW'),
    (241, N'Lahan DWDM'),
    (242, N'Bardaghat DWDM'),
    (243, N'Chandrauta DWDM'),
    (244, N'Goglee'),
    (245, N'Lamahi DWDM'),
    (246, N'Gaurigunj Switch'),
    (247, N'Janakpur DWDM'),
    (248, N'Hetauda DWDM'),
    (249, N'Dhalkebar DWDM'),
    (250, N'Lamki Amplifier'),
    (251, N'Piluwa DWDM'),
    (252, N'Thapathali(P)'),
    (253, N'Gongabu Ganeshsthan'),
    (254, N'Thimi'),
    (255, N'Khowpa'),
    (256, N'Nepaltar'),
    (257, N'Haldibari Goldhap'),
    (258, N'Buddhabare'),
    (259, N'Chandragadhi'),
    (260, N'Dodhara Babathan'),
    (261, N'Hasuliya Mohanibazar'),
    (262, N'Mainapokhar'),
    (263, N'Mangragadhi'),
    (264, N'Parshadap'),
    (265, N'Sardanagar'),
    (266, N'Murgiya'),
    (267, N'Dhikar'),
    (268, N'LCO'),
    (269, N'Padampokhari'),
    (270, N'Bela'),
    (271, N'Dumarwana'),
    (272, N'Simara'),
    (273, N'Sample'),
    (274, N'Geta'),
    (275, N'Tikathali(P)'),
    (276, N'Dhapakhel(P)'),
    (277, N'unverified-narayaghat'),
    (278, N'New Khimti'),
    (279, N'Kawasoti DWDM'),
    (280, N'Butwal DWDM'),
    (281, N'Mirchaya Switch'),
    (282, N'NULL'),
    (283, N'SWH-Datahub'),
    (284, N'SWH-Silverlining'),
    (285, N'SWH-TimesDC'),
    (286, N'Syuchatar Switch'),
    (287, N'Nagarkot');

INSERT INTO @Review (Area, SourceRow, Item, Detail)
SELECT N'POD conflict', NULL, p.Name,
       N'An existing POD already uses this name or source ID with different data; left untouched.'
FROM @Pods AS p
WHERE EXISTS (SELECT 1 FROM dbo.POD AS x
              WHERE x.Name = p.Name AND x.OSSPODID <> p.SourceId)
   OR EXISTS (SELECT 1 FROM dbo.POD AS x
              WHERE x.OSSPODID = p.SourceId AND x.Name <> p.Name);

INSERT INTO dbo.POD (OSSPODID, Name, PODStationId)
SELECT p.SourceId, p.Name, NULL
FROM @Pods AS p
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.POD AS x
    WHERE x.OSSPODID = p.SourceId
       OR x.Name = p.Name
);

-- ---------------------------------------------------------------- salespeople
DECLARE @People TABLE (NameKey NVARCHAR(150) NOT NULL PRIMARY KEY, Name NVARCHAR(150) NOT NULL, Designation NVARCHAR(100) NOT NULL, ManagerName NVARCHAR(150) NULL);
INSERT INTO @People (NameKey, Name, Designation, ManagerName) VALUES
    (N'mahendra bd kc', N'Mahendra Bd Kc', N'CXO', NULL),
    (N'ramesh sapkota', N'Ramesh Sapkota', N'CXO', NULL),
    (N'surya bdr bogati', N'Surya Bdr Bogati', N'CXO', NULL),
    (N'sushil rana thapa', N'Sushil Rana Thapa', N'CXO', NULL),
    (N'bipindra neupane', N'Bipindra Neupane', N'Head', NULL),
    (N'ujjwol neupane', N'Ujjwol Neupane', N'Head', NULL),
    (N'binod subedi', N'Binod Subedi', N'Sales Manager/Supervisor', N'Ujjwol Neupane'),
    (N'bishnu poudel', N'Bishnu Poudel', N'Sales Manager/Supervisor', N'Bipindra Neupane'),
    (N'kamal rimal', N'Kamal Rimal', N'Sales Manager/Supervisor', N'Ujjwol Neupane'),
    (N'madan sapkota', N'Madan sapkota', N'Sales Manager/Supervisor', N'Ujjwol Neupane'),
    (N'niranjan shah', N'Niranjan Shah', N'Sales Manager/Supervisor', N'Bipindra Neupane'),
    (N'nirmal acharya', N'Nirmal Acharya', N'Sales Manager/Supervisor', N'Bipindra Neupane'),
    (N'pradeep subedhi', N'Pradeep Subedhi', N'Sales Manager/Supervisor', N'Ujjwol Neupane'),
    (N'rajaram poudel', N'Rajaram poudel', N'Sales Manager/Supervisor', N'Ujjwol Neupane'),
    (N'sanjib karki', N'Sanjib Karki', N'Sales Manager/Supervisor', N'Bipindra Neupane'),
    (N'anup karki', N'Anup Karki', N'Sales Officer', N'Bishnu Poudel'),
    (N'basanta bahadur khatri', N'Basanta Bahadur Khatri', N'Sales Officer', N'Sanjib Karki'),
    (N'bijayraj panthi', N'Bijayraj Panthi', N'Sales Officer', N'Binod Subedi'),
    (N'bimal maan thapa', N'Bimal Maan Thapa', N'Sales Officer', N'Pradeep Subedhi'),
    (N'dambar airee', N'Dambar Airee', N'Sales Officer', N'Kamal Rimal'),
    (N'dillip bhandari', N'Dillip Bhandari', N'Sales Officer', N'Sanjib Karki'),
    (N'dipak adhikari', N'Dipak Adhikari', N'Sales Officer', N'Bishnu Poudel'),
    (N'hritik mishra', N'Hritik Mishra', N'Sales Officer', NULL),
    (N'kuldip kumar das', N'Kuldip Kumar Das', N'Sales Officer', N'Nirmal Acharya'),
    (N'laxmiraj joshi', N'Laxmiraj Joshi', N'Sales Officer', N'Kamal Rimal'),
    (N'manoj ghimire', N'Manoj Ghimire', N'Sales Officer', N'Nirmal Acharya'),
    (N'prasant karki', N'Prasant Karki', N'Sales Officer', N'Madan sapkota'),
    (N'ram bahadur kunwar', N'Ram Bahadur Kunwar', N'Sales Officer', N'Pradeep Subedhi'),
    (N'ram sundar silakar', N'Ram Sundar Silakar', N'Sales Officer', N'Sanjib Karki'),
    (N'samrat bam', N'Samrat Bam', N'Sales Officer', N'Rajaram poudel'),
    (N'sangit bista', N'Sangit Bista', N'Sales Officer', N'Rajaram poudel'),
    (N'santosh choudhary', N'Santosh Choudhary', N'Sales Officer', N'Nirmal Acharya'),
    (N'shiva prasad niroula', N'Shiva Prasad Niroula', N'Sales Officer', N'Binod Subedi'),
    (N'suman khanal', N'Suman Khanal', N'Sales Officer', N'Madan sapkota');

INSERT INTO @Review (Area, SourceRow, Item, Detail)
SELECT N'Person conflict', NULL, p.Name,
       N'A salesperson with this name already exists with a different designation; left untouched.'
FROM @People AS p
INNER JOIN dbo.SalesPerson AS x ON x.Name = p.Name
WHERE x.Designation <> p.Designation;

INSERT INTO dbo.SalesPerson (Name, Designation, ManagerId)
SELECT p.Name, p.Designation, NULL
FROM @People AS p
WHERE NOT EXISTS (SELECT 1 FROM dbo.SalesPerson AS x WHERE x.Name = p.Name);

-- Resolve manager links only when the person has no manager yet.
UPDATE x
SET x.ManagerId = m.ID
FROM dbo.SalesPerson AS x
INNER JOIN @People AS p ON p.Name = x.Name
INNER JOIN dbo.SalesPerson AS m ON m.Name = p.ManagerName
WHERE x.ManagerId IS NULL
  AND p.ManagerName IS NOT NULL;

-- ---------------------------------------------------------------- POD ownership -> Sales Officer
DECLARE @PodOwners TABLE (PodSourceId INT NOT NULL, Officer NVARCHAR(150) NOT NULL, PRIMARY KEY (PodSourceId, Officer));
INSERT INTO @PodOwners (PodSourceId, Officer) VALUES
    (1, N'Suman Khanal'),
    (2, N'Anup Karki'),
    (3, N'Suman Khanal'),
    (4, N'Suman Khanal'),
    (5, N'Basanta Bahadur Khatri'),
    (6, N'Suman Khanal'),
    (7, N'Suman Khanal'),
    (8, N'Suman Khanal'),
    (9, N'Basanta Bahadur Khatri'),
    (10, N'Prasant Karki'),
    (11, N'Prasant Karki'),
    (12, N'Prasant Karki'),
    (13, N'Prasant Karki'),
    (14, N'Prasant Karki'),
    (15, N'Prasant Karki'),
    (16, N'Prasant Karki'),
    (17, N'Prasant Karki'),
    (18, N'Basanta Bahadur Khatri'),
    (22, N'Basanta Bahadur Khatri'),
    (23, N'Suman Khanal'),
    (24, N'Suman Khanal'),
    (25, N'Suman Khanal'),
    (26, N'Basanta Bahadur Khatri'),
    (27, N'Manoj Ghimire'),
    (28, N'Ram Sundar Silakar'),
    (29, N'Ram Sundar Silakar'),
    (30, N'Manoj Ghimire'),
    (31, N'Hritik Mishra'),
    (32, N'Anup Karki'),
    (33, N'Anup Karki'),
    (34, N'Anup Karki'),
    (35, N'Basanta Bahadur Khatri'),
    (36, N'Basanta Bahadur Khatri'),
    (37, N'Suman Khanal'),
    (38, N'Ram Sundar Silakar'),
    (39, N'Basanta Bahadur Khatri'),
    (40, N'Prasant Karki'),
    (41, N'Basanta Bahadur Khatri'),
    (42, N'Ram Sundar Silakar'),
    (43, N'Basanta Bahadur Khatri'),
    (44, N'Ram Sundar Silakar'),
    (45, N'Manoj Ghimire'),
    (46, N'Ram Sundar Silakar'),
    (47, N'Basanta Bahadur Khatri'),
    (49, N'Ram Sundar Silakar'),
    (50, N'Anup Karki'),
    (51, N'Anup Karki'),
    (52, N'Ram Sundar Silakar'),
    (53, N'Suman Khanal'),
    (54, N'Bijayraj Panthi'),
    (55, N'Basanta Bahadur Khatri'),
    (56, N'Hritik Mishra'),
    (57, N'Bijayraj Panthi'),
    (58, N'Anup Karki'),
    (59, N'Anup Karki'),
    (60, N'Anup Karki'),
    (61, N'Basanta Bahadur Khatri'),
    (62, N'Hritik Mishra'),
    (63, N'Hritik Mishra'),
    (64, N'Hritik Mishra'),
    (65, N'Prasant Karki'),
    (66, N'Santosh Choudhary'),
    (67, N'Santosh Choudhary'),
    (68, N'Basanta Bahadur Khatri'),
    (69, N'Bijayraj Panthi'),
    (70, N'Anup Karki'),
    (71, N'Anup Karki'),
    (72, N'Dillip Bhandari'),
    (73, N'Dillip Bhandari'),
    (74, N'Basanta Bahadur Khatri'),
    (75, N'Dillip Bhandari'),
    (76, N'Bimal Maan Thapa'),
    (77, N'Bimal Maan Thapa'),
    (78, N'Bimal Maan Thapa'),
    (79, N'Bimal Maan Thapa'),
    (80, N'Bijayraj Panthi'),
    (81, N'Ram Sundar Silakar'),
    (82, N'Santosh Choudhary'),
    (83, N'Dipak Adhikari'),
    (84, N'Suman Khanal'),
    (85, N'Dipak Adhikari'),
    (86, N'Basanta Bahadur Khatri'),
    (87, N'Basanta Bahadur Khatri'),
    (89, N'Bimal Maan Thapa'),
    (90, N'Hritik Mishra'),
    (91, N'Suman Khanal'),
    (92, N'Shiva Prasad Niroula'),
    (93, N'Shiva Prasad Niroula'),
    (94, N'Suman Khanal'),
    (95, N'Ram Sundar Silakar'),
    (96, N'Manoj Ghimire'),
    (97, N'Manoj Ghimire'),
    (98, N'Manoj Ghimire'),
    (99, N'Manoj Ghimire'),
    (100, N'Manoj Ghimire'),
    (101, N'Dipak Adhikari'),
    (102, N'Manoj Ghimire'),
    (103, N'Dipak Adhikari'),
    (104, N'Shiva Prasad Niroula'),
    (105, N'Dipak Adhikari'),
    (106, N'Suman Khanal'),
    (107, N'Ram Sundar Silakar'),
    (108, N'Bijayraj Panthi'),
    (109, N'Ram Sundar Silakar'),
    (110, N'Laxmiraj Joshi'),
    (111, N'Laxmiraj Joshi'),
    (112, N'Prasant Karki'),
    (113, N'Bimal Maan Thapa'),
    (114, N'Ram Sundar Silakar'),
    (115, N'Manoj Ghimire'),
    (116, N'Samrat Bam'),
    (117, N'Laxmiraj Joshi'),
    (118, N'Bimal Maan Thapa'),
    (119, N'Ram Sundar Silakar'),
    (129, N'Bimal Maan Thapa'),
    (130, N'Samrat Bam'),
    (131, N'Sangit Bista'),
    (132, N'Dambar Airee'),
    (133, N'Dambar Airee'),
    (134, N'Sangit Bista'),
    (135, N'Samrat Bam'),
    (136, N'Samrat Bam'),
    (137, N'Samrat Bam'),
    (138, N'Dipak Adhikari'),
    (139, N'Manoj Ghimire'),
    (140, N'Samrat Bam'),
    (141, N'Samrat Bam'),
    (142, N'Samrat Bam'),
    (143, N'Hritik Mishra'),
    (144, N'Dambar Airee'),
    (145, N'Ram Bahadur Kunwar'),
    (146, N'Shiva Prasad Niroula'),
    (147, N'Bimal Maan Thapa'),
    (148, N'Shiva Prasad Niroula'),
    (149, N'Hritik Mishra'),
    (150, N'Bimal Maan Thapa'),
    (151, N'Dipak Adhikari'),
    (152, N'Sangit Bista'),
    (153, N'Sangit Bista'),
    (154, N'Sangit Bista'),
    (155, N'Dipak Adhikari'),
    (157, N'Bimal Maan Thapa'),
    (158, N'Dambar Airee'),
    (160, N'Bimal Maan Thapa'),
    (161, N'Samrat Bam'),
    (162, N'Samrat Bam'),
    (163, N'Manoj Ghimire'),
    (164, N'Manoj Ghimire'),
    (165, N'Manoj Ghimire'),
    (166, N'Hritik Mishra'),
    (167, N'Kuldip Kumar Das'),
    (168, N'Dipak Adhikari'),
    (169, N'Suman Khanal'),
    (170, N'Dipak Adhikari'),
    (171, N'Dipak Adhikari'),
    (172, N'Dipak Adhikari'),
    (173, N'Kuldip Kumar Das'),
    (174, N'Laxmiraj Joshi'),
    (175, N'Dambar Airee'),
    (177, N'Laxmiraj Joshi'),
    (182, N'Ram Bahadur Kunwar'),
    (183, N'Ram Bahadur Kunwar'),
    (184, N'Anup Karki'),
    (185, N'Anup Karki'),
    (186, N'Santosh Choudhary'),
    (187, N'Santosh Choudhary'),
    (188, N'Dipak Adhikari'),
    (189, N'Dipak Adhikari'),
    (190, N'Ram Bahadur Kunwar'),
    (191, N'Dipak Adhikari'),
    (192, N'Dambar Airee'),
    (193, N'Ram Sundar Silakar'),
    (194, N'Ram Sundar Silakar'),
    (195, N'Dambar Airee'),
    (196, N'Anup Karki'),
    (197, N'Dambar Airee'),
    (198, N'Suman Khanal'),
    (199, N'Ram Sundar Silakar'),
    (200, N'Santosh Choudhary'),
    (203, N'Laxmiraj Joshi'),
    (204, N'Suman Khanal'),
    (205, N'Bimal Maan Thapa'),
    (206, N'Suman Khanal'),
    (207, N'Dambar Airee'),
    (208, N'Santosh Choudhary'),
    (211, N'Bimal Maan Thapa'),
    (212, N'Bimal Maan Thapa'),
    (213, N'Sangit Bista'),
    (214, N'Sangit Bista'),
    (215, N'Kuldip Kumar Das'),
    (216, N'Hritik Mishra'),
    (217, N'Hritik Mishra'),
    (218, N'Sangit Bista'),
    (219, N'Kuldip Kumar Das'),
    (220, N'Prasant Karki'),
    (221, N'Kuldip Kumar Das'),
    (232, N'Prasant Karki'),
    (233, N'Prasant Karki'),
    (234, N'Prasant Karki'),
    (257, N'Dipak Adhikari'),
    (258, N'Dipak Adhikari'),
    (260, N'Dambar Airee'),
    (261, N'Laxmiraj Joshi'),
    (266, N'Bijayraj Panthi'),
    (269, N'Manoj Ghimire'),
    (270, N'Laxmiraj Joshi'),
    (271, N'Santosh Choudhary'),
    (272, N'Santosh Choudhary');

INSERT INTO dbo.SalesPersonPOD (SalesPersonId, PODId)
SELECT sp.ID, pod.ID
FROM @PodOwners AS o
INNER JOIN dbo.POD AS pod ON pod.OSSPODID = o.PodSourceId
INNER JOIN dbo.SalesPerson AS sp ON sp.Name = o.Officer
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.SalesPersonPOD AS x
    WHERE x.SalesPersonId = sp.ID AND x.PODId = pod.ID
);

-- Best-effort legacy sync; never overwrite a different owner.
UPDATE pod
SET pod.salespersonId = sp.ID
FROM dbo.POD AS pod
INNER JOIN @PodOwners AS o ON pod.OSSPODID = o.PodSourceId
INNER JOIN dbo.SalesPerson AS sp ON sp.Name = o.Officer
WHERE pod.salespersonId IS NULL OR pod.salespersonId = sp.ID;

-- ---------------------------------------------------------------- dealers (code used as name)
DECLARE @Dealers TABLE (Code NVARCHAR(50) NOT NULL PRIMARY KEY, Name NVARCHAR(150) NOT NULL);
INSERT INTO @Dealers (Code, Name) VALUES
    (N'BDHDNG-D', N'BDHDNG-D'),
    (N'BKAV-D', N'BKAV-D'),
    (N'BKTM-A', N'BKTM-A'),
    (N'BKTM-D', N'BKTM-D'),
    (N'BLALIT-A', N'BLALIT-A'),
    (N'BLALIT-D', N'BLALIT-D'),
    (N'BNUW-D', N'BNUW-D'),
    (N'BNUW1-A', N'BNUW1-A'),
    (N'BSKUT-D', N'BSKUT-D'),
    (N'GPKR-D', N'GPKR-D'),
    (N'GSYANG-D', N'GSYANG-D'),
    (N'GTAN-A', N'GTAN-A'),
    (N'JJNK1-A', N'JJNK1-A'),
    (N'JMOH1-A', N'JMOH1-A'),
    (N'JSAR-A', N'JSAR-A'),
    (N'JSAR3-A', N'JSAR3-A'),
    (N'KBT-A', N'KBT-A'),
    (N'KMRN1-A', N'KMRN1-A'),
    (N'KSUN2-A', N'KSUN2-A'),
    (N'KSUN3-A', N'KSUN3-A'),
    (N'LBHW-A', N'LBHW-A'),
    (N'LBTWL-A', N'LBTWL-A'),
    (N'LBTWL-D', N'LBTWL-D'),
    (N'LPAL-D', N'LPAL-D'),
    (N'MJHAP-A', N'MJHAP-A'),
    (N'MJHAP-D', N'MJHAP-D'),
    (N'MTAP-D', N'MTAP-D'),
    (N'NBRJ-A', N'NBRJ-A'),
    (N'NCHTW-D', N'NCHTW-D'),
    (N'NMAK-A', N'NMAK-A'),
    (N'NPAJ-A', N'NPAJ-A'),
    (N'NPAJ-D', N'NPAJ-D'),
    (N'NRAHT-A', N'NRAHT-A'),
    (N'RDANG-A', N'RDANG-A'),
    (N'SDHN-A', N'SDHN-A'),
    (N'SDHN-D', N'SDHN-D'),
    (N'SSIRA-A', N'SSIRA-A'),
    (N'SSIRA-D', N'SSIRA-D'),
    (N'SSPTRI-A', N'SSPTRI-A'),
    (N'SUDPUR2-A', N'SUDPUR2-A');

INSERT INTO @Review (Area, SourceRow, Item, Detail)
SELECT N'Dealer conflict', NULL, d.Code,
       N'An existing dealer with this code has a different name; left untouched.'
FROM @Dealers AS d
INNER JOIN dbo.Dealer AS x ON x.Code = d.Code
WHERE x.Name <> d.Name;

INSERT INTO dbo.Dealer (Name, Code)
SELECT d.Name, d.Code
FROM @Dealers AS d
WHERE NOT EXISTS (SELECT 1 FROM dbo.Dealer AS x WHERE x.Code = d.Code);

-- ---------------------------------------------------------------- dealer mappings -> Sales Officer
DECLARE @DealerOwners TABLE (Code NVARCHAR(50) NOT NULL, Officer NVARCHAR(150) NOT NULL, PRIMARY KEY (Code, Officer));
INSERT INTO @DealerOwners (Code, Officer) VALUES
    (N'BDHDNG-D', N'Suman Khanal'),
    (N'BKAV-D', N'Dillip Bhandari'),
    (N'BKTM-A', N'Basanta Bahadur Khatri'),
    (N'BKTM-A', N'Bimal Maan Thapa'),
    (N'BKTM-A', N'Prasant Karki'),
    (N'BKTM-A', N'Ram Sundar Silakar'),
    (N'BKTM-A', N'Suman Khanal'),
    (N'BKTM-D', N'Basanta Bahadur Khatri'),
    (N'BKTM-D', N'Bimal Maan Thapa'),
    (N'BKTM-D', N'Prasant Karki'),
    (N'BKTM-D', N'Ram Sundar Silakar'),
    (N'BKTM-D', N'Suman Khanal'),
    (N'BLALIT-A', N'Basanta Bahadur Khatri'),
    (N'BLALIT-A', N'Bimal Maan Thapa'),
    (N'BLALIT-A', N'Manoj Ghimire'),
    (N'BLALIT-A', N'Prasant Karki'),
    (N'BLALIT-A', N'Ram Sundar Silakar'),
    (N'BLALIT-A', N'Suman Khanal'),
    (N'BLALIT-D', N'Basanta Bahadur Khatri'),
    (N'BLALIT-D', N'Bimal Maan Thapa'),
    (N'BLALIT-D', N'Prasant Karki'),
    (N'BLALIT-D', N'Ram Sundar Silakar'),
    (N'BLALIT-D', N'Suman Khanal'),
    (N'BNUW-D', N'Ram Sundar Silakar'),
    (N'BNUW1-A', N'Ram Sundar Silakar'),
    (N'BSKUT-D', N'Samrat Bam'),
    (N'GPKR-D', N'Bimal Maan Thapa'),
    (N'GSYANG-D', N'Bimal Maan Thapa'),
    (N'GTAN-A', N'Ram Bahadur Kunwar'),
    (N'JJNK1-A', N'Hritik Mishra'),
    (N'JMOH1-A', N'Hritik Mishra'),
    (N'JSAR-A', N'Hritik Mishra'),
    (N'JSAR3-A', N'Hritik Mishra'),
    (N'KBT-A', N'Anup Karki'),
    (N'KMRN1-A', N'Anup Karki'),
    (N'KSUN2-A', N'Anup Karki'),
    (N'KSUN3-A', N'Anup Karki'),
    (N'LBHW-A', N'Bijayraj Panthi'),
    (N'LBHW-A', N'Manoj Ghimire'),
    (N'LBHW-A', N'Shiva Prasad Niroula'),
    (N'LBTWL-A', N'Bijayraj Panthi'),
    (N'LBTWL-A', N'Manoj Ghimire'),
    (N'LBTWL-A', N'Shiva Prasad Niroula'),
    (N'LBTWL-D', N'Bijayraj Panthi'),
    (N'LBTWL-D', N'Manoj Ghimire'),
    (N'LBTWL-D', N'Shiva Prasad Niroula'),
    (N'LPAL-D', N'Shiva Prasad Niroula'),
    (N'MJHAP-A', N'Dipak Adhikari'),
    (N'MJHAP-D', N'Dipak Adhikari'),
    (N'MTAP-D', N'Dipak Adhikari'),
    (N'NBRJ-A', N'Santosh Choudhary'),
    (N'NCHTW-D', N'Manoj Ghimire'),
    (N'NMAK-A', N'Manoj Ghimire'),
    (N'NPAJ-A', N'Samrat Bam'),
    (N'NPAJ-D', N'Samrat Bam'),
    (N'NRAHT-A', N'Santosh Choudhary'),
    (N'RDANG-A', N'Sangit Bista'),
    (N'SDHN-A', N'Dambar Airee'),
    (N'SDHN-A', N'Laxmiraj Joshi'),
    (N'SDHN-D', N'Dambar Airee'),
    (N'SDHN-D', N'Laxmiraj Joshi'),
    (N'SSIRA-A', N'Hritik Mishra'),
    (N'SSIRA-A', N'Kuldip Kumar Das'),
    (N'SSIRA-D', N'Hritik Mishra'),
    (N'SSIRA-D', N'Kuldip Kumar Das'),
    (N'SSPTRI-A', N'Kuldip Kumar Das'),
    (N'SUDPUR2-A', N'Kuldip Kumar Das');

INSERT INTO dbo.salesdealermapping (SalesPersonId, DealerId)
SELECT sp.ID, d.UniqueID
FROM @DealerOwners AS o
INNER JOIN dbo.Dealer AS d ON d.Code = o.Code
INNER JOIN dbo.SalesPerson AS sp ON sp.Name = o.Officer
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.salesdealermapping AS x
    WHERE x.SalesPersonId = sp.ID AND x.DealerId = d.UniqueID
);

-- ---------------------------------------------------------------- review report
INSERT INTO @Review (Area, SourceRow, Item, Detail) VALUES
    (N'Station alias', NULL, N'Devinagar POD station: mapping sheet casing differs from the master row.', N'No station assignment was applied.'),
    (N'Station alias', NULL, N'Trisuli POD Station: mapping sheet spelling differs; master spelling is Trishuli POD Station.', N'No station assignment was applied.'),
    (N'Hierarchy conflict', NULL, N'Bipindra Neupane (Head) has 3 possible managers: Ramesh Sapkota, Surya Bdr Bogati, Sushil Rana Thapa.', N'ManagerId left NULL.'),
    (N'Hierarchy conflict', NULL, N'Ujjwol Neupane (Head) has 3 possible managers: Mahendra Bd Kc, Ramesh Sapkota, Sushil Rana Thapa.', N'ManagerId left NULL.'),
    (N'Hierarchy conflict', NULL, N'Hritik Mishra (Sales Officer) has 2 possible managers: Niranjan Shah, Nirmal Acharya.', N'ManagerId left NULL.'),
    (N'Name normalization', NULL, N'Sanjib Karki: source variants Sanjib Karki and Sanjib karki normalized to ''Sanjib Karki''.', N'No action required.'),
    (N'POD assignment', NULL, N'All 273 PODs inserted with PODStationId = NULL by design.', N'Use the POD Assignment Management page to review and apply station pairings.'),
    (N'Ownership', NULL, N'POD ownership comes from the mapping sheet Sales Officer column.', N'Stored in SalesPersonPOD.');

SELECT Area, SourceRow, Item, Detail
FROM @Review
ORDER BY Area, SourceRow, Item;

SELECT N'PODStations' AS Metric, (SELECT COUNT(*) FROM dbo.PODStation) AS Value
UNION ALL SELECT N'PODs', (SELECT COUNT(*) FROM dbo.POD)
UNION ALL SELECT N'SalesPeople', (SELECT COUNT(*) FROM dbo.SalesPerson)
UNION ALL SELECT N'SalesPersonPODs', (SELECT COUNT(*) FROM dbo.SalesPersonPOD)
UNION ALL SELECT N'Dealers', (SELECT COUNT(*) FROM dbo.Dealer)
UNION ALL SELECT N'DealerMappings', (SELECT COUNT(*) FROM dbo.salesdealermapping);

SELECT N'Manager conflicts' AS Category, 3 AS Count, N'Bipindra Neupane, Ujjwol Neupane, Hritik Mishra' AS Details
UNION ALL
SELECT N'PODs imported unassigned' AS Category, 273 AS Count, N'All master PODs have PODStationId NULL' AS Details
UNION ALL
SELECT N'Workbook station pairings not applied' AS Category, 208 AS Count, N'Assign station/POD relationships in the web UI' AS Details
UNION ALL
SELECT N'Station aliases documented' AS Category, 2 AS Count, N'Trisuli POD Station, Devinagar POD station' AS Details
UNION ALL
SELECT N'Dealer mapping conflicts' AS Category, 0 AS Count, N'No dealer code/name conflicts detected' AS Details;

COMMIT TRANSACTION;
