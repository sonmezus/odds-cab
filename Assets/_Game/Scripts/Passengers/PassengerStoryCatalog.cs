namespace OddsCab
{
    /// <summary>Short bilingual passenger arcs; chapter access is saved and unlocked with journey coins.</summary>
    public static class PassengerStoryCatalog
    {
        public const int ChapterCount = 3;
        public const int ChapterUnlockCost = 120;

        static readonly string[,] Turkish =
        {
            {
                "Defne: \"Haritama eski bir vapur iskelesi işaretledim. Büyükannem orada çocukken fotoğraf çekermiş. Bir gün izini bulacağım.\"",
                "Defne: \"Arşivde aynı iskelenin bir kartpostalını buldum. Arkasına büyükannemin el yazısıyla bir sokak adı düşülmüş. Bu rota beni oraya götürebilir.\"",
                "Defne: \"İskeleyi bulduk! Büyükannemin fotoğrafındaki merdivenler hâlâ duruyor. Şimdi bu hikâyeyi kendi haritama ekleyeceğim.\""
            },
            {
                "Dede Çınar: \"Gençken bu sahilde her sabah vapuru beklerdim. Şu eski saat kulesi, buluşma yerimizdi.\"",
                "Dede Çınar: \"Cebimde yıllardır sakladığım biletin arkasında bir not var: son seferden önce çay bahçesinde buluşalım. Orası hâlâ açık mıdır?\"",
                "Dede Çınar: \"Çay bahçesi yerinde, dostumun adı da duvarda. Biletimi masaya bıraktım; bu kez anı, vedadan çok bir kavuşma oldu.\""
            },
            {
                "Ava: \"Ailemizin fırını bir süredir kapalı. Büyükannemin tarif defteri kayıp; açılış günü için onun ünlü çöreğini yapmak istiyorum.\"",
                "Ava: \"Defterin bir sayfası komşu pastanede çıkmış! Ölçüler eksik ama kenarına annem bir saat ve üç malzeme not etmiş. Tarifi tamamlayacağım.\"",
                "Ava: \"İlk tepsi fırından çıktı; çörek tam çocukluğumdaki gibi kokuyor. Yarın kepengi açıyoruz—bu kez tarif defteri de kasanın yanında.\""
            },
            {
                "Ozan: \"Dedemin ukulelesinde yarım kalmış bir ezgi var. Nakaratın son notasını kimse hatırlamıyor. Belki bu şehir bana ipucu verir.\"",
                "Ozan: \"Eski bir vapur kaydında ezginin üç notasını duydum. Rıhtımdaki müzisyen, son notanın martıların ötüşüne benzediğini söyledi.\"",
                "Ozan: \"Ezgi tamamlandı! Gün batımında rıhtımda çaldım; dinleyenlerden biri dedemi tanıyormuş. Şarkı şimdi ikimizin de hatırası.\""
            },
            {
                "Selin: \"İlk davamı kazandığım gün babam bana pirinç bir anahtar vermişti. Anahtarın hangi kapıyı açtığını hâlâ bilmiyorum.\"",
                "Selin: \"Eski adliye kayıtlarında anahtara benzeyen bir amblem buldum. Kayıt, yıllar önce kapanan mahalle kütüphanesini gösteriyor.\"",
                "Selin: \"Kütüphane kapısı açıldı. İçeride babamın bağışladığı kitaplar ve bana bıraktığı bir not vardı: \"Hakkı koru, merakı da.\"\""
            }
        };

        static readonly string[,] English =
        {
            {
                "Defne: \"I marked an old ferry pier on my map. My grandmother used to take photographs there as a child. One day I’ll find it.\"",
                "Defne: \"I found a postcard of that pier in the archive. On the back, my grandmother wrote a street name. This route might lead me there.\"",
                "Defne: \"We found the pier! The steps in my grandmother’s photo are still here. I’m adding this story to my own map.\""
            },
            {
                "Grandpa Cinar: \"When I was young, I waited for the ferry here every morning. That old clock tower was our meeting point.\"",
                "Grandpa Cinar: \"There’s a note on the back of the ticket I’ve kept for years: meet at the tea garden before the last ferry. Is it still open?\"",
                "Grandpa Cinar: \"The tea garden is still here, and my friend’s name is on the wall. I left my ticket on the table; this time, memory became a reunion.\""
            },
            {
                "Ava: \"My family bakery has been closed for a while. Grandma’s recipe book is missing, and I want her famous buns for opening day.\"",
                "Ava: \"A page from the book turned up at the neighborhood bakery! The measurements are gone, but Mom wrote down a time and three ingredients.\"",
                "Ava: \"The first tray is out; the buns smell just like my childhood. We open tomorrow—and the recipe book will sit by the register.\""
            },
            {
                "Ozan: \"There’s an unfinished tune on my grandfather’s ukulele. Nobody remembers the final note. Maybe the city can give me a clue.\"",
                "Ozan: \"I heard three notes in an old ferry recording. A musician at the pier said the last one sounds like a gull’s call.\"",
                "Ozan: \"The tune is complete! I played it at the pier at sunset; someone in the crowd knew my grandfather. Now the song belongs to us both.\""
            },
            {
                "Selin: \"On the day I won my first case, my father gave me a brass key. I still don’t know which door it opens.\"",
                "Selin: \"Old courthouse records show an emblem like the one on the key. The entry points to a neighborhood library that closed years ago.\"",
                "Selin: \"The library door opened. Inside were books my father donated and a note: \"Protect what’s fair, and stay curious.\"\""
            }
        };

        public static string Get(PassengerId passenger, int chapter)
        {
            int passengerIndex = (int)passenger;
            if (passengerIndex < 0 || passengerIndex >= PassengerCatalog.All.Length) passengerIndex = 0;
            chapter = UnityEngine.Mathf.Clamp(chapter, 0, ChapterCount - 1);
            return Loc.Pick(Turkish[passengerIndex, chapter], English[passengerIndex, chapter]);
        }
    }
}
