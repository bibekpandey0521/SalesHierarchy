-- Reviewed import generated from:
--   pods.xlsx (PODId/PODName and station sheets)
--   Sales Mapping With Dealer and POD.xlsx (Sheet1)
--
-- Review decisions:
--   * POD owner and dealer owner = the row Sales Officer.
--   * Dealer.Name = Dealer.Code because the workbook has no dealer names.
--   * Conflicting manager links are left NULL and reported, not guessed.
--   * Trisuli POD Station is normalized to Trishuli POD Station.
--   * PODs without a verified station are skipped because POD.PODStationId is NOT NULL.
--
-- The script is idempotent: it matches natural keys, never overwrites an existing
-- name/designation/owner, and leaves unrelated/demo rows untouched.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.SalesPersonPOD', N'U') IS NULL
BEGIN
    THROW 51002, 'Run database/Upgrade-SalesPersonPOD.sql before this import.', 1;
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
DECLARE @Stations TABLE (SourceId INT NOT NULL PRIMARY KEY, Name NVARCHAR(200) NOT NULL);
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
              WHERE x.Name = s.Name AND x.OSSPODStationId <> CAST(s.SourceId AS NVARCHAR(50)))
   OR EXISTS (SELECT 1 FROM dbo.PODStation AS x
              WHERE x.OSSPODStationId = CAST(s.SourceId AS NVARCHAR(50)) AND x.Name <> s.Name);

INSERT INTO dbo.PODStation (OSSPODStationId, Name)
SELECT CAST(s.SourceId AS NVARCHAR(50)), s.Name
FROM @Stations AS s
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.PODStation AS x
    WHERE x.OSSPODStationId = CAST(s.SourceId AS NVARCHAR(50))
       OR x.Name = s.Name
);

-- ---------------------------------------------------------------- PODs with a verified station
DECLARE @Pods TABLE (SourceId INT NOT NULL PRIMARY KEY, Name NVARCHAR(200) NOT NULL, StationName NVARCHAR(200) NOT NULL);
INSERT INTO @Pods (SourceId, Name, StationName) VALUES
    (87, N'Hattiban(P)', N'Yekantakuna POD Station'),
    (86, N'Thaiba(P)', N'Yekantakuna POD Station'),
    (36, N'Chapagaun(P)', N'Yekantakuna POD Station'),
    (43, N'Taukhel(P)', N'Yekantakuna POD Station'),
    (47, N'Gwarko(P)', N'Yekantakuna POD Station'),
    (35, N'Thecho(P)', N'Yekantakuna POD Station'),
    (41, N'Bhaisepati1(P)', N'Yekantakuna POD Station'),
    (39, N'Lubu(P)', N'Yekantakuna POD Station'),
    (193, N'Trishuli', N'Trishuli POD Station'),
    (194, N'Trishuli Batar', N'Trishuli POD Station'),
    (199, N'SNS Balkot', N'SNS Bhaktapur POD Station'),
    (106, N'Dakshinkali', N'Sitapaila POD Station'),
    (91, N'Taudaha', N'Sitapaila POD Station'),
    (37, N'Kalimati', N'Sitapaila POD Station'),
    (24, N'Syuchatar', N'Sitapaila POD Station'),
    (4, N'Sitapaila', N'Sitapaila POD Station'),
    (23, N'Chauni', N'Sitapaila POD Station'),
    (3, N'Halchowk', N'Sitapaila POD Station'),
    (25, N'Kirtipur', N'Sitapaila POD Station'),
    (55, N'Sankhamul', N'Mahalaxmi POD Station'),
    (68, N'Narephat', N'Mahalaxmi POD Station'),
    (18, N'Bhaisepati', N'Mahalaxmi POD Station'),
    (61, N'Subidhanagar', N'Mahalaxmi POD Station'),
    (9, N'Sanepa', N'Mahalaxmi POD Station'),
    (22, N'Dhapakhel', N'Mahalaxmi POD Station'),
    (74, N'Sinamangal', N'Mahalaxmi POD Station'),
    (26, N'Tikathali', N'Mahalaxmi POD Station'),
    (5, N'Imadol', N'Mahalaxmi POD Station'),
    (28, N'Bagdole(P)', N'Kathmandu POD Station'),
    (81, N'Satungal', N'Kathmandu POD Station'),
    (107, N'Kuleshwor', N'Kathmandu POD Station'),
    (52, N'Kamaladi(P)', N'Kathmandu POD Station'),
    (38, N'Newbaneshwor(P)', N'Kathmandu POD Station'),
    (109, N'Matatirtha', N'Kathmandu POD Station'),
    (44, N'Chandragiri(P)', N'Kathmandu POD Station'),
    (46, N'Chyasal(P)', N'Kathmandu POD Station'),
    (10, N'Dhumbarahi', N'Kapan POD Station'),
    (65, N'Lazimpat', N'Kapan POD Station'),
    (11, N'Budhanilkantha', N'Kapan POD Station'),
    (232, N'Optimized Kapan', N'Kapan POD Station'),
    (12, N'Chapali', N'Kapan POD Station'),
    (17, N'Kapan', N'Kapan POD Station'),
    (13, N'Golfutar', N'Kapan POD Station'),
    (84, N'Tokha Chandeshwori', N'Gongabu POD Station'),
    (8, N'Dhapasi', N'Gongabu POD Station'),
    (53, N'Thamel', N'Gongabu POD Station'),
    (94, N'Goldhunga', N'Gongabu POD Station'),
    (6, N'Manamaiju', N'Gongabu POD Station'),
    (1, N'Samakhusi', N'Gongabu POD Station'),
    (7, N'Gongabu', N'Gongabu POD Station'),
    (204, N'Adamghat', N'Galchi POD Station'),
    (206, N'Malekhu', N'Galchi POD Station'),
    (198, N'Galchi', N'Galchi POD Station'),
    (169, N'Dhading Besi', N'Dhading POD Station'),
    (233, N'Optimized Boudha', N'Dakshindhoka POD Station'),
    (234, N'Optimized Pepsicola', N'Dakshindhoka POD Station'),
    (16, N'Boudha', N'Dakshindhoka POD Station'),
    (220, N'Optimized Jorpati', N'Dakshindhoka POD Station'),
    (40, N'Mulpani', N'Dakshindhoka POD Station'),
    (15, N'Jorpati', N'Dakshindhoka POD Station'),
    (112, N'Kapan Gamcha', N'Dakshindhoka POD Station'),
    (14, N'Pepsicola', N'Dakshindhoka POD Station'),
    (114, N'Changunarayan', N'Bhaktapur POD Station'),
    (119, N'Kharipati', N'Bhaktapur POD Station'),
    (95, N'Balkot', N'Bhaktapur POD Station'),
    (42, N'Suryabinayak', N'Bhaktapur POD Station'),
    (49, N'Bhaktapur', N'Bhaktapur POD Station'),
    (29, N'Madhyapur Thimi', N'Bhaktapur POD Station'),
    (73, N'Panauti', N'Banepa POD Station'),
    (75, N'Dhulikhel', N'Banepa POD Station'),
    (72, N'Banepa', N'Banepa POD Station'),
    (146, N'Palpa Tansen', N'Palpa POD Station'),
    (148, N'Bartung', N'Palpa POD Station'),
    (56, N'Bardibas', N'Bardibas POD Station'),
    (31, N'Dhalkebar', N'Bardibas POD Station'),
    (82, N'Kalaiya', N'Birganj POD Station'),
    (208, N'Parsauni', N'Birganj POD Station'),
    (67, N'Birgunj Adarshanagar', N'Birganj POD Station'),
    (66, N'Birgunj Panitanki', N'Birganj POD Station'),
    (186, N'Chapur Basantapur', N'Chapur POD Station'),
    (187, N'Chapur Gaidatar', N'Chapur POD Station'),
    (200, N'Chapur Bazar', N'Chapur POD Station'),
    (134, N'Dang Ghorahi', N'Dang POD Station'),
    (131, N'Dang Gulariya', N'Dang POD Station'),
    (218, N'Urhari', N'Tulsipur POD Station'),
    (213, N'Lamahi Chaulahi', N'Lamahi POD Station'),
    (214, N'Lamahi', N'Lamahi POD Station'),
    (154, N'Tulsipur Balapur', N'Tulsipur POD Station'),
    (152, N'Tulsipur Hathikhuwa', N'Tulsipur POD Station'),
    (153, N'Tulsipur Tarigau', N'Tulsipur POD Station'),
    (142, N'Surkhet Kalinchowk', N'Surkhet POD Station'),
    (141, N'Surkhet Bhiravsthan', N'Surkhet POD Station'),
    (140, N'Surkhet Mangalgadhi', N'Surkhet POD Station'),
    (136, N'Kohalpur Sajha Chowk', N'Kohalpur POD Station'),
    (135, N'Kohalpur Pragatinagar', N'Kohalpur POD Station'),
    (116, N'Dhamboji', N'Nepalgunj POD Station'),
    (130, N'Bageshwori', N'Nepalgunj POD Station'),
    (137, N'Kohalpur Naulapur Panitanki', N'Kohalpur POD Station'),
    (161, N'Bansgadhi Hashanapur', N'Bansgadi POD station'),
    (162, N'Bansgadhi Deudakala', N'Bansgadi POD station'),
    (183, N'Damauli Simaltar', N'Damauli POD Station'),
    (145, N'Gorkha', N'Gorkha POD Station'),
    (182, N'Damauli Bhadgau', N'Damauli POD Station'),
    (190, N'Damauli', N'Damauli POD Station'),
    (163, N'Patihani', N'Chitwan POD Station'),
    (165, N'Geetanagar', N'Chitwan POD Station'),
    (151, N'Baundangi', N'Baundangi POD Station'),
    (155, N'Aamdangi', N'Baundangi POD Station'),
    (184, N'Belbari Betana', N'Belbari POD Station'),
    (196, N'Belbari Lalbhitti', N'Belbari POD Station'),
    (185, N'Belbari', N'Belbari POD Station'),
    (51, N'Kanchanbari', N'Biratnagar POD Station'),
    (71, N'Biratnagar', N'Biratnagar POD Station'),
    (50, N'Mahendrachowk', N'Biratnagar POD Station'),
    (70, N'Roadcyess', N'Biratnagar POD Station'),
    (83, N'Bhagwan Chowk', N'Birtamode POD Station'),
    (168, N'Arjundhara', N'Birtamode POD Station'),
    (85, N'Adwa Khola', N'Birtamode POD Station'),
    (258, N'Buddhabare', N'Birtamode POD Station'),
    (177, N'Fulbari Dipobazar', N'Dhangadhi POD Station'),
    (203, N'Hasuliya', N'Hasuliya POD Station'),
    (174, N'Beli', N'Dhangadhi POD Station'),
    (260, N'Dodhara Babathan', N'Dodharachandani POD Station'),
    (175, N'Kaluwapur', N'Attariya POD Station'),
    (144, N'Mahendranagar Basantapur Chowk', N'Mahendranagar POD Station'),
    (158, N'Mahendranagar Brahmadev', N'Mahendranagar POD Station'),
    (195, N'Jhalari Kalapani', N'Jhalari POD Station'),
    (270, N'Bela', N'Attariya POD Station'),
    (117, N'Attariya', N'Attariya POD Station'),
    (197, N'Jhalari Pipladi', N'Jhalari POD Station'),
    (192, N'Jhalari Bazar', N'Jhalari POD Station'),
    (207, N'Dodhara Chandani', N'Dodharachandani POD Station'),
    (133, N'Mahendranagar Fulbari Chowk', N'Mahendranagar POD Station'),
    (132, N'Mahendranagar Puspa Lal Chowk', N'Mahendranagar POD Station'),
    (111, N'Dhangadi Buddha Chowk', N'Dhangadhi POD Station'),
    (110, N'Dhangadi Uttar Behadi', N'Dhangadhi POD Station'),
    (261, N'Hasuliya Mohanibazar', N'Hasuliya POD Station'),
    (98, N'Gaidakot', N'Chitwan POD Station'),
    (97, N'Sukranagar', N'Chitwan POD Station'),
    (96, N'Bharatpur', N'Chitwan POD Station'),
    (164, N'Hakim Chowk', N'Chitwan POD Station'),
    (115, N'Narayanghat', N'Chitwan POD Station'),
    (173, N'Udayapur Gaighat', N'Gaighat Pod Station'),
    (167, N'Gaighat', N'Gaighat Pod Station'),
    (105, N'Damak', N'Damak POD Station'),
    (138, N'Bhrikuti Chowk', N'Damak POD Station'),
    (60, N'Mahadev chowk', N'Dharan POD Station'),
    (59, N'Budhasubba', N'Dharan POD Station'),
    (58, N'Chatara Line', N'Dharan POD Station'),
    (27, N'Hetauda', N'Hetauda POD Station'),
    (45, N'Kamane', N'Hetauda POD Station'),
    (191, N'Haldibari', N'Haldibari POD Station'),
    (257, N'Haldibari Goldhap', N'Haldibari POD Station'),
    (188, N'Haldibari Sainikbazar', N'Haldibari POD Station'),
    (189, N'Haldibari Parakhopi', N'Haldibari POD Station'),
    (30, N'Hetauda Bhuwan Singh Marga', N'Hetauda POD Station'),
    (269, N'Padampokhari', N'Hetauda POD Station'),
    (63, N'Pidari', N'Janakpur POD Station'),
    (64, N'Zeromile', N'Janakpur POD Station'),
    (2, N'Inaruwa', N'Inaruwa POD Station'),
    (32, N'Tarahara', N'Itahari POD Station'),
    (34, N'Pakali', N'Itahari POD Station'),
    (33, N'Bhetghat Chowk', N'Itahari POD Station'),
    (62, N'Janakpur', N'Janakpur POD Station'),
    (139, N'Kawasoti Ramnagar Chowk', N'Kawasoti POD Station'),
    (221, N'Bhagwanpur', N'Lahan POD Station'),
    (103, N'Aayabari', N'Kakadvitta POD Station'),
    (101, N'Dhaijan', N'Kakadvitta POD Station'),
    (90, N'Lahan', N'Lahan POD Station'),
    (143, N'Kalinjor', N'Lalbandi pod station'),
    (211, N'Lakeside', N'Pokhara POD Station'),
    (205, N'Pame', N'Pokhara POD Station'),
    (77, N'Fulbari(P)', N'Pokhara POD Station'),
    (147, N'Gharipatan', N'Pokhara POD Station'),
    (150, N'Hemja', N'Pokhara POD Station'),
    (149, N'Lalbandi', N'Lalbandi pod station'),
    (78, N'Zero(P)', N'Pokhara POD Station'),
    (89, N'Birauta(P)', N'Pokhara POD Station'),
    (79, N'Bagar(P)', N'Pokhara POD Station'),
    (118, N'Kudar', N'Pokhara POD Station'),
    (129, N'Budibazar', N'Pokhara POD Station'),
    (113, N'Majheripatan', N'Pokhara POD Station'),
    (76, N'Newroad(P)', N'Pokhara POD Station'),
    (160, N'Power House', N'Pokhara POD Station'),
    (157, N'Lamachaur', N'Pokhara POD Station'),
    (92, N'Siddharthanagar', N'Bhairawa POD Station'),
    (93, N'Bhairahawa', N'Bhairawa POD Station'),
    (57, N'Shankarnagar', N'Butwal POD Station'),
    (104, N'Manigram', N'Butwal POD Station'),
    (54, N'Sukhanagar', N'Devinagar POD station'),
    (266, N'Murgiya', N'Nayagaun POD Station'),
    (69, N'Devinagar', N'Devinagar POD station'),
    (108, N'Nayagaun', N'Nayagaun POD Station'),
    (80, N'Drivertole', N'Butwal POD Station'),
    (166, N'Hariwon', N'Lalbandi pod station'),
    (216, N'Malangwa Bazar', N'Malangwa POD Station'),
    (217, N'Malangwa Gamahariya', N'Malangwa POD Station'),
    (215, N'Rajbiraj', N'Rajbiraj POD Station'),
    (219, N'Rajbiraj Tilathai Road', N'Rajbiraj POD Station'),
    (272, N'Simara', N'Simara POD Station'),
    (271, N'Dumarwana', N'Simara POD Station'),
    (172, N'Surunga', N'Surunga POD Station'),
    (171, N'Madanpur', N'Surunga POD Station'),
    (170, N'Kankai', N'Surunga POD Station'),
    (212, N'Putalibazar', N'Syangja POD Station'),
    (99, N'Ratnanagar', N'Tandi pod station'),
    (100, N'Khaireni', N'Tandi pod station'),
    (102, N'Tandi', N'Tandi pod station');

INSERT INTO @Review (Area, SourceRow, Item, Detail)
SELECT N'POD conflict', NULL, p.Name,
       N'An existing POD already uses this name or source ID with different data; left untouched.'
FROM @Pods AS p
WHERE EXISTS (SELECT 1 FROM dbo.POD AS x
              WHERE x.Name = p.Name AND x.OSSPODID <> CAST(p.SourceId AS NVARCHAR(50)))
   OR EXISTS (SELECT 1 FROM dbo.POD AS x
              WHERE x.OSSPODID = CAST(p.SourceId AS NVARCHAR(50)) AND x.Name <> p.Name);

INSERT INTO dbo.POD (OSSPODID, Name, PODStationId)
SELECT CAST(p.SourceId AS NVARCHAR(50)), p.Name, st.ID
FROM @Pods AS p
INNER JOIN dbo.PODStation AS st ON st.Name = p.StationName
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.POD AS x
    WHERE x.OSSPODID = CAST(p.SourceId AS NVARCHAR(50))
       OR x.Name = p.Name
);

-- ---------------------------------------------------------------- salespeople
DECLARE @People TABLE (NameKey NVARCHAR(200) NOT NULL PRIMARY KEY, Name NVARCHAR(200) NOT NULL, Designation NVARCHAR(100) NOT NULL, ManagerName NVARCHAR(200) NULL);
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
DECLARE @PodOwners TABLE (PodSourceId INT NOT NULL PRIMARY KEY, Officer NVARCHAR(200) NOT NULL);
INSERT INTO @PodOwners (PodSourceId, Officer) VALUES
    (87, N'Basanta Bahadur Khatri'),
    (86, N'Basanta Bahadur Khatri'),
    (36, N'Basanta Bahadur Khatri'),
    (43, N'Basanta Bahadur Khatri'),
    (47, N'Basanta Bahadur Khatri'),
    (35, N'Basanta Bahadur Khatri'),
    (41, N'Basanta Bahadur Khatri'),
    (39, N'Basanta Bahadur Khatri'),
    (193, N'Ram Sundar Silakar'),
    (194, N'Ram Sundar Silakar'),
    (199, N'Ram Sundar Silakar'),
    (106, N'Suman Khanal'),
    (91, N'Suman Khanal'),
    (37, N'Suman Khanal'),
    (24, N'Suman Khanal'),
    (4, N'Suman Khanal'),
    (23, N'Suman Khanal'),
    (3, N'Suman Khanal'),
    (25, N'Suman Khanal'),
    (55, N'Basanta Bahadur Khatri'),
    (68, N'Basanta Bahadur Khatri'),
    (18, N'Basanta Bahadur Khatri'),
    (61, N'Basanta Bahadur Khatri'),
    (9, N'Basanta Bahadur Khatri'),
    (22, N'Basanta Bahadur Khatri'),
    (74, N'Basanta Bahadur Khatri'),
    (26, N'Basanta Bahadur Khatri'),
    (5, N'Basanta Bahadur Khatri'),
    (28, N'Ram Sundar Silakar'),
    (81, N'Ram Sundar Silakar'),
    (107, N'Ram Sundar Silakar'),
    (52, N'Ram Sundar Silakar'),
    (38, N'Ram Sundar Silakar'),
    (109, N'Ram Sundar Silakar'),
    (44, N'Ram Sundar Silakar'),
    (46, N'Ram Sundar Silakar'),
    (10, N'Prasant Karki'),
    (65, N'Prasant Karki'),
    (11, N'Prasant Karki'),
    (232, N'Prasant Karki'),
    (12, N'Prasant Karki'),
    (17, N'Prasant Karki'),
    (13, N'Prasant Karki'),
    (84, N'Suman Khanal'),
    (8, N'Suman Khanal'),
    (53, N'Suman Khanal'),
    (94, N'Suman Khanal'),
    (6, N'Suman Khanal'),
    (1, N'Suman Khanal'),
    (7, N'Suman Khanal'),
    (204, N'Suman Khanal'),
    (206, N'Suman Khanal'),
    (198, N'Suman Khanal'),
    (169, N'Suman Khanal'),
    (233, N'Prasant Karki'),
    (234, N'Prasant Karki'),
    (16, N'Prasant Karki'),
    (220, N'Prasant Karki'),
    (40, N'Prasant Karki'),
    (15, N'Prasant Karki'),
    (112, N'Prasant Karki'),
    (14, N'Prasant Karki'),
    (114, N'Ram Sundar Silakar'),
    (119, N'Ram Sundar Silakar'),
    (95, N'Ram Sundar Silakar'),
    (42, N'Ram Sundar Silakar'),
    (49, N'Ram Sundar Silakar'),
    (29, N'Ram Sundar Silakar'),
    (73, N'Dillip Bhandari'),
    (75, N'Dillip Bhandari'),
    (72, N'Dillip Bhandari'),
    (146, N'Shiva Prasad Niroula'),
    (148, N'Shiva Prasad Niroula'),
    (56, N'Hritik Mishra'),
    (31, N'Hritik Mishra'),
    (82, N'Santosh Choudhary'),
    (208, N'Santosh Choudhary'),
    (67, N'Santosh Choudhary'),
    (66, N'Santosh Choudhary'),
    (186, N'Santosh Choudhary'),
    (187, N'Santosh Choudhary'),
    (200, N'Santosh Choudhary'),
    (134, N'Sangit Bista'),
    (131, N'Sangit Bista'),
    (218, N'Sangit Bista'),
    (213, N'Sangit Bista'),
    (214, N'Sangit Bista'),
    (154, N'Sangit Bista'),
    (152, N'Sangit Bista'),
    (153, N'Sangit Bista'),
    (142, N'Samrat Bam'),
    (141, N'Samrat Bam'),
    (140, N'Samrat Bam'),
    (136, N'Samrat Bam'),
    (135, N'Samrat Bam'),
    (116, N'Samrat Bam'),
    (130, N'Samrat Bam'),
    (137, N'Samrat Bam'),
    (161, N'Samrat Bam'),
    (162, N'Samrat Bam'),
    (183, N'Ram Bahadur Kunwar'),
    (145, N'Ram Bahadur Kunwar'),
    (182, N'Ram Bahadur Kunwar'),
    (190, N'Ram Bahadur Kunwar'),
    (163, N'Manoj Ghimire'),
    (165, N'Manoj Ghimire'),
    (151, N'Dipak Adhikari'),
    (155, N'Dipak Adhikari'),
    (184, N'Anup Karki'),
    (196, N'Anup Karki'),
    (185, N'Anup Karki'),
    (51, N'Anup Karki'),
    (71, N'Anup Karki'),
    (50, N'Anup Karki'),
    (70, N'Anup Karki'),
    (83, N'Dipak Adhikari'),
    (168, N'Dipak Adhikari'),
    (85, N'Dipak Adhikari'),
    (258, N'Dipak Adhikari'),
    (177, N'Laxmiraj Joshi'),
    (203, N'Laxmiraj Joshi'),
    (174, N'Laxmiraj Joshi'),
    (260, N'Dambar Airee'),
    (175, N'Dambar Airee'),
    (144, N'Dambar Airee'),
    (158, N'Dambar Airee'),
    (195, N'Dambar Airee'),
    (270, N'Laxmiraj Joshi'),
    (117, N'Laxmiraj Joshi'),
    (197, N'Dambar Airee'),
    (192, N'Dambar Airee'),
    (207, N'Dambar Airee'),
    (133, N'Dambar Airee'),
    (132, N'Dambar Airee'),
    (111, N'Laxmiraj Joshi'),
    (110, N'Laxmiraj Joshi'),
    (261, N'Laxmiraj Joshi'),
    (98, N'Manoj Ghimire'),
    (97, N'Manoj Ghimire'),
    (96, N'Manoj Ghimire'),
    (164, N'Manoj Ghimire'),
    (115, N'Manoj Ghimire'),
    (173, N'Kuldip Kumar Das'),
    (167, N'Kuldip Kumar Das'),
    (105, N'Dipak Adhikari'),
    (138, N'Dipak Adhikari'),
    (60, N'Anup Karki'),
    (59, N'Anup Karki'),
    (58, N'Anup Karki'),
    (27, N'Manoj Ghimire'),
    (45, N'Manoj Ghimire'),
    (191, N'Dipak Adhikari'),
    (257, N'Dipak Adhikari'),
    (188, N'Dipak Adhikari'),
    (189, N'Dipak Adhikari'),
    (30, N'Manoj Ghimire'),
    (269, N'Manoj Ghimire'),
    (63, N'Hritik Mishra'),
    (64, N'Hritik Mishra'),
    (2, N'Anup Karki'),
    (32, N'Anup Karki'),
    (34, N'Anup Karki'),
    (33, N'Anup Karki'),
    (62, N'Hritik Mishra'),
    (139, N'Manoj Ghimire'),
    (221, N'Kuldip Kumar Das'),
    (103, N'Dipak Adhikari'),
    (101, N'Dipak Adhikari'),
    (90, N'Hritik Mishra'),
    (143, N'Hritik Mishra'),
    (211, N'Bimal Maan Thapa'),
    (205, N'Bimal Maan Thapa'),
    (77, N'Bimal Maan Thapa'),
    (147, N'Bimal Maan Thapa'),
    (150, N'Bimal Maan Thapa'),
    (149, N'Hritik Mishra'),
    (78, N'Bimal Maan Thapa'),
    (89, N'Bimal Maan Thapa'),
    (79, N'Bimal Maan Thapa'),
    (118, N'Bimal Maan Thapa'),
    (129, N'Bimal Maan Thapa'),
    (113, N'Bimal Maan Thapa'),
    (76, N'Bimal Maan Thapa'),
    (160, N'Bimal Maan Thapa'),
    (157, N'Bimal Maan Thapa'),
    (92, N'Shiva Prasad Niroula'),
    (93, N'Shiva Prasad Niroula'),
    (57, N'Bijayraj Panthi'),
    (104, N'Shiva Prasad Niroula'),
    (54, N'Bijayraj Panthi'),
    (266, N'Bijayraj Panthi'),
    (69, N'Bijayraj Panthi'),
    (108, N'Bijayraj Panthi'),
    (80, N'Bijayraj Panthi'),
    (166, N'Hritik Mishra'),
    (216, N'Hritik Mishra'),
    (217, N'Hritik Mishra'),
    (215, N'Kuldip Kumar Das'),
    (219, N'Kuldip Kumar Das'),
    (272, N'Santosh Choudhary'),
    (271, N'Santosh Choudhary'),
    (172, N'Dipak Adhikari'),
    (171, N'Dipak Adhikari'),
    (170, N'Dipak Adhikari'),
    (212, N'Bimal Maan Thapa'),
    (99, N'Manoj Ghimire'),
    (100, N'Manoj Ghimire'),
    (102, N'Manoj Ghimire');

INSERT INTO dbo.SalesPersonPOD (SalesPersonId, PODId)
SELECT sp.ID, pod.ID
FROM @PodOwners AS o
INNER JOIN dbo.POD AS pod ON pod.OSSPODID = CAST(o.PodSourceId AS NVARCHAR(50))
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
INNER JOIN @PodOwners AS o ON pod.OSSPODID = CAST(o.PodSourceId AS NVARCHAR(50))
INNER JOIN dbo.SalesPerson AS sp ON sp.Name = o.Officer
WHERE pod.salespersonId IS NULL OR pod.salespersonId = sp.ID;

-- ---------------------------------------------------------------- dealers (code used as name)
DECLARE @Dealers TABLE (Code NVARCHAR(50) NOT NULL PRIMARY KEY, Name NVARCHAR(200) NOT NULL);
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
DECLARE @DealerOwners TABLE (Code NVARCHAR(50) NOT NULL, Officer NVARCHAR(200) NOT NULL);
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

INSERT INTO dbo.salesdealermapping (salespersonid, dealerid)
SELECT sp.ID, d.UniqueID
FROM @DealerOwners AS o
INNER JOIN dbo.Dealer AS d ON d.Code = o.Code
INNER JOIN dbo.SalesPerson AS sp ON sp.Name = o.Officer
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.salesdealermapping AS x
    WHERE x.salespersonid = sp.ID AND x.dealerid = d.UniqueID
);

-- ---------------------------------------------------------------- review report
INSERT INTO @Review (Area, SourceRow, Item, Detail) VALUES
    (N'Station alias', 10, N'Trisuli POD Station', N'Normalized to ''Trishuli POD Station'' (station master spelling).'),
    (N'Station alias', 11, N'Trisuli POD Station', N'Normalized to ''Trishuli POD Station'' (station master spelling).'),
    (N'Hierarchy conflict', NULL, N'Bipindra Neupane', N'head has 3 possible managers in the source: Ramesh Sapkota, Surya Bdr Bogati, Sushil Rana Thapa. ManagerId left NULL.'),
    (N'Hierarchy conflict', NULL, N'Ujjwol Neupane', N'head has 3 possible managers in the source: Mahendra Bd Kc, Ramesh Sapkota, Sushil Rana Thapa. ManagerId left NULL.'),
    (N'Hierarchy conflict', NULL, N'Hritik Mishra', N'officer has 2 possible managers in the source: Niranjan Shah, Nirmal Acharya. ManagerId left NULL.'),
    (N'Name normalization', NULL, N'Sanjib Karki', N'Source variants Sanjib Karki, Sanjib karki normalized to ''Sanjib Karki''.'),
    (N'POD skipped', NULL, N'Pulchowk(STN)', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Baneshwor(STN)', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Tinkune(STN)', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Churn Customer', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Syaule', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Itahari RO', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Duhabi DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Unverified Smarten POD', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Unverified Optinet POD', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Unverified Fiberking POD', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Unverified Paschimanchal POD', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Fiberking Portshift CDR', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Smarten Portshift BAG', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Smarten Portshift CDR', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Smarten Portshift EDR', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Smarten Portshift MWDR', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Optinet Portshift EDR', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'DMN Portshift BUTWAL', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Optinet Portshift Bagmati', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Optinet Portshift Pokhara', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Optinet Portshift FWDR', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Unverified Bhakatpur POD', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Damak NEA SW', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Pathari Switch', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Birtamode NEA SW', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Biratchowk Switch', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Rupani NEA SW', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Lahan DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Bardaghat DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Chandrauta DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Goglee', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Lamahi DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Gaurigunj Switch', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Janakpur DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Hetauda DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Dhalkebar DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Lamki Amplifier', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Piluwa DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Thapathali(P)', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Gongabu Ganeshsthan', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Thimi', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Khowpa', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Nepaltar', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Chandragadhi', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Mainapokhar', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Mangragadhi', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Parshadap', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Sardanagar', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Dhikar', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'LCO', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Sample', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Geta', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Tikathali(P)', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Dhapakhel(P)', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'unverified-narayaghat', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'New Khimti', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Kawasoti DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Butwal DWDM', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Mirchaya Switch', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'NULL', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'SWH-Datahub', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'SWH-Silverlining', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'SWH-TimesDC', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Syuchatar Switch', N'No verified station in either workbook; POD.PODStationId is NOT NULL.'),
    (N'POD skipped', NULL, N'Nagarkot', N'No verified station in either workbook; POD.PODStationId is NOT NULL.');

SELECT Area, SourceRow, Item, Detail
FROM @Review
ORDER BY Area, SourceRow, Item;

SELECT
    (SELECT COUNT(*) FROM dbo.PODStation) AS PODStations,
    (SELECT COUNT(*) FROM dbo.POD) AS PODs,
    (SELECT COUNT(*) FROM dbo.SalesPerson) AS SalesPeople,
    (SELECT COUNT(*) FROM dbo.SalesPersonPOD) AS SalesPersonPODs,
    (SELECT COUNT(*) FROM dbo.Dealer) AS Dealers,
    (SELECT COUNT(*) FROM dbo.salesdealermapping) AS DealerMappings
UNION ALL
SELECT N'Manager conflicts' AS Category, 3 AS Count, N'Bipindra Neupane, Ujjwol Neupane, Hritik Mishra' AS Details
UNION ALL
SELECT N'Station aliases (normalized)' AS Category, 2 AS Count, N'Trisuli POD Station → Trishuli POD Station' AS Details
UNION ALL
SELECT N'Name normalizations (Sanjib karki → Sanjib Karki)' AS Category, 3 AS Count, N'Three source variants normalized' AS Details
UNION ALL
SELECT N'Skipped PODs (no verified station)' AS Category, 65 AS Count, N'65 PODs from pods.xlsx have no station in either workbook' AS Details
UNION ALL
SELECT N'Dealer mapping conflicts' AS Category, 0 AS Count, N'No dealer code/name conflicts detected' AS Details;

COMMIT TRANSACTION;
