-- Assigns the 208 reviewed workbook POD -> POD station pairs.
-- Source: Sales Mapping With Dealer and POD.xlsx (POD Name / POD Station).
-- Import leaves every POD unassigned; this is the reviewed follow-up step.
-- Only reviewed spelling alias: Trisuli POD Station -> Trishuli POD Station.
-- Re-runnable: rows already assigned correctly are skipped; conflicts abort.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRANSACTION;

DECLARE @Pairs TABLE
(
    PodName NVARCHAR(150) NOT NULL PRIMARY KEY,
    StationName NVARCHAR(150) NOT NULL
);

INSERT INTO @Pairs (PodName, StationName) VALUES
    (N'Hattiban(P)', N'Yekantakuna POD Station'),
    (N'Thaiba(P)', N'Yekantakuna POD Station'),
    (N'Chapagaun(P)', N'Yekantakuna POD Station'),
    (N'Taukhel(P)', N'Yekantakuna POD Station'),
    (N'Gwarko(P)', N'Yekantakuna POD Station'),
    (N'Thecho(P)', N'Yekantakuna POD Station'),
    (N'Bhaisepati1(P)', N'Yekantakuna POD Station'),
    (N'Lubu(P)', N'Yekantakuna POD Station'),
    (N'Trishuli', N'Trisuli POD Station'),
    (N'Trishuli Batar', N'Trisuli POD Station'),
    (N'SNS Balkot', N'SNS Bhaktapur POD Station'),
    (N'Dakshinkali', N'Sitapaila POD Station'),
    (N'Taudaha', N'Sitapaila POD Station'),
    (N'Kalimati', N'Sitapaila POD Station'),
    (N'Syuchatar', N'Sitapaila POD Station'),
    (N'Sitapaila', N'Sitapaila POD Station'),
    (N'Chauni', N'Sitapaila POD Station'),
    (N'Halchowk', N'Sitapaila POD Station'),
    (N'Kirtipur', N'Sitapaila POD Station'),
    (N'Sankhamul', N'Mahalaxmi POD Station'),
    (N'Narephat', N'Mahalaxmi POD Station'),
    (N'Bhaisepati', N'Mahalaxmi POD Station'),
    (N'Subidhanagar', N'Mahalaxmi POD Station'),
    (N'Sanepa', N'Mahalaxmi POD Station'),
    (N'Dhapakhel', N'Mahalaxmi POD Station'),
    (N'Sinamangal', N'Mahalaxmi POD Station'),
    (N'Tikathali', N'Mahalaxmi POD Station'),
    (N'Imadol', N'Mahalaxmi POD Station'),
    (N'Bagdole(P)', N'Kathmandu POD Station'),
    (N'Satungal', N'Kathmandu POD Station'),
    (N'Kuleshwor', N'Kathmandu POD Station'),
    (N'Kamaladi(P)', N'Kathmandu POD Station'),
    (N'Newbaneshwor(P)', N'Kathmandu POD Station'),
    (N'Matatirtha', N'Kathmandu POD Station'),
    (N'Chandragiri(P)', N'Kathmandu POD Station'),
    (N'Chyasal(P)', N'Kathmandu POD Station'),
    (N'Dhumbarahi', N'Kapan POD Station'),
    (N'Lazimpat', N'Kapan POD Station'),
    (N'Budhanilkantha', N'Kapan POD Station'),
    (N'Optimized Kapan', N'Kapan POD Station'),
    (N'Chapali', N'Kapan POD Station'),
    (N'Kapan', N'Kapan POD Station'),
    (N'Golfutar', N'Kapan POD Station'),
    (N'Tokha Chandeshwori', N'Gongabu POD Station'),
    (N'Dhapasi', N'Gongabu POD Station'),
    (N'Thamel', N'Gongabu POD Station'),
    (N'Goldhunga', N'Gongabu POD Station'),
    (N'Manamaiju', N'Gongabu POD Station'),
    (N'Samakhusi', N'Gongabu POD Station'),
    (N'Gongabu', N'Gongabu POD Station'),
    (N'Adamghat', N'Galchi POD Station'),
    (N'Malekhu', N'Galchi POD Station'),
    (N'Galchi', N'Galchi POD Station'),
    (N'Dhading Besi', N'Dhading POD Station'),
    (N'Optimized Boudha', N'Dakshindhoka POD Station'),
    (N'Optimized Pepsicola', N'Dakshindhoka POD Station'),
    (N'Boudha', N'Dakshindhoka POD Station'),
    (N'Optimized Jorpati', N'Dakshindhoka POD Station'),
    (N'Mulpani', N'Dakshindhoka POD Station'),
    (N'Jorpati', N'Dakshindhoka POD Station'),
    (N'Kapan Gamcha', N'Dakshindhoka POD Station'),
    (N'Pepsicola', N'Dakshindhoka POD Station'),
    (N'Changunarayan', N'Bhaktapur POD Station'),
    (N'Kharipati', N'Bhaktapur POD Station'),
    (N'Balkot', N'Bhaktapur POD Station'),
    (N'Suryabinayak', N'Bhaktapur POD Station'),
    (N'Bhaktapur', N'Bhaktapur POD Station'),
    (N'Madhyapur Thimi', N'Bhaktapur POD Station'),
    (N'Panauti', N'Banepa POD Station'),
    (N'Dhulikhel', N'Banepa POD Station'),
    (N'Banepa', N'Banepa POD Station'),
    (N'Palpa Tansen', N'Palpa POD Station'),
    (N'Bartung', N'Palpa POD Station'),
    (N'Bardibas', N'Bardibas POD Station'),
    (N'Dhalkebar', N'Bardibas POD Station'),
    (N'Kalaiya', N'Birganj POD Station'),
    (N'Parsauni', N'Birganj POD Station'),
    (N'Birgunj Adarshanagar', N'Birganj POD Station'),
    (N'Birgunj Panitanki', N'Birganj POD Station'),
    (N'Chapur Basantapur', N'Chapur POD Station'),
    (N'Chapur Gaidatar', N'Chapur POD Station'),
    (N'Chapur Bazar', N'Chapur POD Station'),
    (N'Dang Ghorahi', N'Dang POD Station'),
    (N'Dang Gulariya', N'Dang POD Station'),
    (N'Urhari', N'Tulsipur POD Station'),
    (N'Lamahi Chaulahi', N'Lamahi POD Station'),
    (N'Lamahi', N'Lamahi POD Station'),
    (N'Tulsipur Balapur', N'Tulsipur POD Station'),
    (N'Tulsipur Hathikhuwa', N'Tulsipur POD Station'),
    (N'Tulsipur Tarigau', N'Tulsipur POD Station'),
    (N'Surkhet Kalinchowk', N'Surkhet POD Station'),
    (N'Surkhet Bhiravsthan', N'Surkhet POD Station'),
    (N'Surkhet Mangalgadhi', N'Surkhet POD Station'),
    (N'Kohalpur Sajha Chowk', N'Kohalpur POD Station'),
    (N'Kohalpur Pragatinagar', N'Kohalpur POD Station'),
    (N'Dhamboji', N'Nepalgunj POD Station'),
    (N'Bageshwori', N'Nepalgunj POD Station'),
    (N'Kohalpur Naulapur Panitanki', N'Kohalpur POD Station'),
    (N'Bansgadhi Hashanapur', N'Bansgadi POD station'),
    (N'Bansgadhi Deudakala', N'Bansgadi POD station'),
    (N'Damauli Simaltar', N'Damauli POD Station'),
    (N'Gorkha', N'Gorkha POD Station'),
    (N'Damauli Bhadgau', N'Damauli POD Station'),
    (N'Damauli', N'Damauli POD Station'),
    (N'Patihani', N'Chitwan POD Station'),
    (N'Geetanagar', N'Chitwan POD Station'),
    (N'Baundangi', N'Baundangi POD Station'),
    (N'Aamdangi', N'Baundangi POD Station'),
    (N'Belbari Betana', N'Belbari POD Station'),
    (N'Belbari Lalbhitti', N'Belbari POD Station'),
    (N'Belbari', N'Belbari POD Station'),
    (N'Kanchanbari', N'Biratnagar POD Station'),
    (N'Biratnagar', N'Biratnagar POD Station'),
    (N'Mahendrachowk', N'Biratnagar POD Station'),
    (N'Roadcyess', N'Biratnagar POD Station'),
    (N'Bhagwan Chowk', N'Birtamode POD Station'),
    (N'Arjundhara', N'Birtamode POD Station'),
    (N'Adwa Khola', N'Birtamode POD Station'),
    (N'Buddhabare', N'Birtamode POD Station'),
    (N'Fulbari Dipobazar', N'Dhangadhi POD Station'),
    (N'Hasuliya', N'Hasuliya POD Station'),
    (N'Beli', N'Dhangadhi POD Station'),
    (N'Dodhara Babathan', N'Dodharachandani POD Station'),
    (N'Kaluwapur', N'Attariya POD Station'),
    (N'Mahendranagar Basantapur Chowk', N'Mahendranagar POD Station'),
    (N'Mahendranagar Brahmadev', N'Mahendranagar POD Station'),
    (N'Jhalari Kalapani', N'Jhalari POD Station'),
    (N'Bela', N'Attariya POD Station'),
    (N'Attariya', N'Attariya POD Station'),
    (N'Jhalari Pipladi', N'Jhalari POD Station'),
    (N'Jhalari Bazar', N'Jhalari POD Station'),
    (N'Dodhara Chandani', N'Dodharachandani POD Station'),
    (N'Mahendranagar Fulbari Chowk', N'Mahendranagar POD Station'),
    (N'Mahendranagar Puspa Lal Chowk', N'Mahendranagar POD Station'),
    (N'Dhangadi Buddha Chowk', N'Dhangadhi POD Station'),
    (N'Dhangadi Uttar Behadi', N'Dhangadhi POD Station'),
    (N'Hasuliya Mohanibazar', N'Hasuliya POD Station'),
    (N'Gaidakot', N'Chitwan POD Station'),
    (N'Sukranagar', N'Chitwan POD Station'),
    (N'Bharatpur', N'Chitwan POD Station'),
    (N'Hakim Chowk', N'Chitwan POD Station'),
    (N'Narayanghat', N'Chitwan POD Station'),
    (N'Udayapur Gaighat', N'Gaighat Pod Station'),
    (N'Gaighat', N'Gaighat Pod Station'),
    (N'Damak', N'Damak POD Station'),
    (N'Bhrikuti Chowk', N'Damak POD Station'),
    (N'Mahadev chowk', N'Dharan POD Station'),
    (N'Budhasubba', N'Dharan POD Station'),
    (N'Chatara Line', N'Dharan POD Station'),
    (N'Hetauda', N'Hetauda POD Station'),
    (N'Kamane', N'Hetauda POD Station'),
    (N'Haldibari', N'Haldibari POD Station'),
    (N'Haldibari Goldhap', N'Haldibari POD Station'),
    (N'Haldibari Sainikbazar', N'Haldibari POD Station'),
    (N'Haldibari Parakhopi', N'Haldibari POD Station'),
    (N'Hetauda Bhuwan Singh Marga', N'Hetauda POD Station'),
    (N'Padampokhari', N'Hetauda POD Station'),
    (N'Pidari', N'Janakpur POD Station'),
    (N'Zeromile', N'Janakpur POD Station'),
    (N'Inaruwa', N'Inaruwa POD Station'),
    (N'Tarahara', N'Itahari POD Station'),
    (N'Pakali', N'Itahari POD Station'),
    (N'Bhetghat Chowk', N'Itahari POD Station'),
    (N'Janakpur', N'Janakpur POD Station'),
    (N'Kawasoti Ramnagar Chowk', N'Kawasoti POD Station'),
    (N'Bhagwanpur', N'Lahan POD Station'),
    (N'Aayabari', N'Kakadvitta POD Station'),
    (N'Dhaijan', N'Kakadvitta POD Station'),
    (N'Lahan', N'Lahan POD Station'),
    (N'Kalinjor', N'Lalbandi pod station'),
    (N'Lakeside', N'Pokhara POD Station'),
    (N'Pame', N'Pokhara POD Station'),
    (N'Fulbari(P)', N'Pokhara POD Station'),
    (N'Gharipatan', N'Pokhara POD Station'),
    (N'Hemja', N'Pokhara POD Station'),
    (N'Lalbandi', N'Lalbandi pod station'),
    (N'Zero(P)', N'Pokhara POD Station'),
    (N'Birauta(P)', N'Pokhara POD Station'),
    (N'Bagar(P)', N'Pokhara POD Station'),
    (N'Kudar', N'Pokhara POD Station'),
    (N'Budibazar', N'Pokhara POD Station'),
    (N'Majheripatan', N'Pokhara POD Station'),
    (N'Newroad(P)', N'Pokhara POD Station'),
    (N'Power House', N'Pokhara POD Station'),
    (N'Lamachaur', N'Pokhara POD Station'),
    (N'Siddharthanagar', N'Bhairawa POD Station'),
    (N'Bhairahawa', N'Bhairawa POD Station'),
    (N'Shankarnagar', N'Butwal POD Station'),
    (N'Manigram', N'Butwal POD Station'),
    (N'Sukhanagar', N'Devinagar POD station'),
    (N'Murgiya', N'Nayagaun POD Station'),
    (N'Devinagar', N'Devinagar POD station'),
    (N'Nayagaun', N'Nayagaun POD Station'),
    (N'Drivertole', N'Butwal POD Station'),
    (N'Hariwon', N'Lalbandi pod station'),
    (N'Malangwa Bazar', N'Malangwa POD Station'),
    (N'Malangwa Gamahariya', N'Malangwa POD Station'),
    (N'Rajbiraj', N'Rajbiraj POD Station'),
    (N'Rajbiraj Tilathai Road', N'Rajbiraj POD Station'),
    (N'Simara', N'Simara POD Station'),
    (N'Dumarwana', N'Simara POD Station'),
    (N'Surunga', N'Surunga POD Station'),
    (N'Madanpur', N'Surunga POD Station'),
    (N'Kankai', N'Surunga POD Station'),
    (N'Putalibazar', N'Syangja POD Station'),
    (N'Ratnanagar', N'Tandi pod station'),
    (N'Khaireni', N'Tandi pod station'),
    (N'Tandi', N'Tandi pod station');

UPDATE @Pairs
SET StationName = N'Trishuli POD Station'
WHERE StationName = N'Trisuli POD Station';

IF EXISTS (SELECT 1 FROM @Pairs AS p WHERE NOT EXISTS (SELECT 1 FROM dbo.POD AS x WHERE x.Name = p.PodName))
BEGIN
    THROW 52001, 'A workbook POD name is missing from dbo.POD.', 1;
END;

IF EXISTS (SELECT 1 FROM @Pairs AS p WHERE NOT EXISTS (SELECT 1 FROM dbo.PODStation AS s WHERE s.Name = p.StationName))
BEGIN
    THROW 52002, 'A workbook station name is missing from dbo.PODStation.', 1;
END;

IF EXISTS (SELECT 1 FROM dbo.POD AS x JOIN @Pairs AS p ON p.PodName = x.Name JOIN dbo.PODStation AS s ON s.Name = p.StationName WHERE x.PODStationId IS NOT NULL AND x.PODStationId <> s.ID)
BEGIN
    THROW 52003, 'A POD is already assigned to a different station. Review before re-applying.', 1;
END;

DECLARE @Applied INT;

UPDATE x
SET x.PODStationId = s.ID
FROM dbo.POD AS x
JOIN @Pairs AS p ON p.PodName = x.Name
JOIN dbo.PODStation AS s ON s.Name = p.StationName
WHERE x.PODStationId IS NULL;

SET @Applied = @@ROWCOUNT;

IF EXISTS (SELECT 1
           FROM @Pairs AS p
           JOIN dbo.POD AS x ON x.Name = p.PodName
           JOIN dbo.PODStation AS s ON s.Name = p.StationName
           WHERE x.PODStationId IS NULL OR x.PODStationId <> s.ID)
BEGIN
    THROW 52004, 'A reviewed pair was not applied correctly.', 1;
END;

IF EXISTS (SELECT 1 FROM dbo.POD
           WHERE PODStationId IS NOT NULL
           GROUP BY PODStationId, Name
           HAVING COUNT(*) > 1)
BEGIN
    THROW 52005, 'Duplicate POD name within a station after assignment.', 1;
END;

COMMIT TRANSACTION;

SELECT N'Pairs applied this run' AS Metric, @Applied AS Value
UNION ALL SELECT N'PODs assigned (total)', COUNT(*) FROM dbo.POD WHERE PODStationId IS NOT NULL
UNION ALL SELECT N'PODs still unassigned', COUNT(*) FROM dbo.POD WHERE PODStationId IS NULL
UNION ALL SELECT N'Stations in use', COUNT(DISTINCT PODStationId) FROM dbo.POD WHERE PODStationId IS NOT NULL;
