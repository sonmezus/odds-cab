using System;
using System.Collections.Generic;

namespace OddsCab
{
    public enum Language { TR = 0, EN = 1 }

    /// <summary>Two-language string table for UI text. Puzzle content carries its own TR/EN text.</summary>
    public static class Loc
    {
        public static event Action Changed;

        static Language _lang = Language.TR;
        public static Language Lang
        {
            get => _lang;
            set
            {
                if (_lang == value) return;
                _lang = value;
                Changed?.Invoke();
            }
        }

        public static bool IsTR => _lang == Language.TR;

        static readonly System.Globalization.CultureInfo TurkishCulture = new System.Globalization.CultureInfo("tr-TR");

        /// <summary>Upper-case with Turkish rules in TR (i → İ), whatever the phone's own language is.</summary>
        public static string Upper(string s) => string.IsNullOrEmpty(s) ? s
            : s.ToUpper(_lang == Language.TR ? TurkishCulture : System.Globalization.CultureInfo.InvariantCulture);

        public static string Pick(string tr, string en) => _lang == Language.TR ? tr : (string.IsNullOrEmpty(en) ? tr : en);

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            { "play", new[] { "YOLA ÇIK", "HIT THE ROAD" } },
            { "new_tour", new[] { "YENİ TURA ÇIK", "START A NEW TOUR" } },
            { "album_note", new[] { "{0} çekti · {1}. durak", "Taken by {0} · stop {1}" } },
            { "album_locked", new[] { "Kilitli · {0} / {1} durak", "Locked · {0} / {1} stops" } },
            { "valve_status", new[] { "Akan su: {0} litre", "Flowing: {0} litres" } },
            { "lantern_status", new[] { "Yanan fener: {0}", "Lanterns lit: {0}" } },
            { "campaign_title", new[] { "512 DURAK TAMAMLANDI!", "ALL 512 STOPS DONE!" } },
            { "campaign_sub", new[] { "İstanbul'un sekiz semtini yolcularınla bir uçtan bir uca geçtin.", "You crossed all eight districts of Istanbul with your passengers." } },
            { "campaign_stats", new[] { "Doğru cevap: {0}\nYanlış deneme: {1}\nEn uzun seri: {2}\nToplam jeton: {3}\nTamamlanan tur: {4}", "Correct answers: {0}\nWrong attempts: {1}\nLongest streak: {2}\nTotal coins: {3}\nTours completed: {4}" } },
            { "campaign_thanks", new[] { "Defne, Selin, Çınar Dede, Ava ve Ozan sana teşekkür ediyor.", "Defne, Selin, Grandpa Çınar, Ava and Ozan say thank you." } },
            { "campaign_menu", new[] { "ANA MENÜYE DÖN", "BACK TO MAIN MENU" } },
            { "campaign_done_badge", new[] { "512/512 · {0}. TUR", "512/512 · TOUR {0}" } },
            { "continue_play", new[] { "YOLA ÇIK", "HIT THE ROAD" } },
            { "menu_title_line1", new[] { "512 DURAKLIK", "512-STOP" } },
            { "menu_title_line2", new[] { "YOLCULUK", "JOURNEY" } },
            { "next_stop_header", new[] { "SONRAKİ DURAK", "NEXT STOP" } },
            { "maiden_tower", new[] { "KIZ KULESİ", "MAIDEN'S TOWER" } },
            { "pickup_passenger", new[] { "YOLCUYU AL", "PICK UP PASSENGER" } },
            { "continue_btn", new[] { "DEVAM ET", "CONTINUE" } },
            { "route_puzzle_title", new[] { "ROTA BULMACASI", "ROUTE PUZZLE" } },
            { "route_puzzle_sub", new[] { "YOLLARI ÇEVİR, TAKSİYİ GÖTÜR", "TURN ROADS, GUIDE THE TAXI" } },
            { "settings", new[] { "AYARLAR", "SETTINGS" } },
            { "stops_total", new[] { "{0} DURAK", "{0} STOPS" } },
            { "next_stop", new[] { "Sıradaki durak: {0}", "Next stop: {0}" } },
            { "music", new[] { "Müzik", "Music" } },
            { "effects", new[] { "Efektler", "Effects" } },
            { "graphics", new[] { "Grafik", "Graphics" } },
            { "q_auto", new[] { "Oto", "Auto" } },
            { "q_low", new[] { "Düşük", "Low" } },
            { "q_med", new[] { "Orta", "Med" } },
            { "q_high", new[] { "Yüksek", "High" } },
            { "q_ultra", new[] { "Ultra", "Ultra" } },
            { "guide_on", new[] { "SESLİ ANLATIM  ·  AÇIK", "SPOKEN GUIDE  ·  ON" } },
            { "guide_off", new[] { "SESLİ ANLATIM  ·  KAPALI", "SPOKEN GUIDE  ·  OFF" } },
            { "back_to_game", new[] { "OYUNA DÖN", "BACK TO GAME" } },
            { "reset_progress", new[] { "İlerlemeyi sıfırla", "Reset progress" } },
            { "reset_confirm", new[] { "Emin misin? Tekrar dokun", "Sure? Tap again" } },
            { "paused", new[] { "DURAKLATILDI", "PAUSED" } },
            { "resume", new[] { "DEVAM", "RESUME" } },
            { "main_menu", new[] { "ANA MENÜ", "MAIN MENU" } },
            { "kmh", new[] { "km/sa", "km/h" } },
            { "hint", new[] { "İpucu  -{0}", "Hint  -{0}" } },
            { "why_label", new[] { "NEDEN?", "WHY?" } },
            { "order_reset", new[] { "SIFIRLA", "RESET" } },
            { "letters_delete", new[] { "←  SİL", "←  DELETE" } },
            { "grid_select_clue", new[] { "Önce bir sayı seç", "Select a numbered clue" } },
            { "grid_pick_corners", new[] { "Dikdörtgenin ilk köşesine dokun", "Tap the first rectangle corner" } },
            { "grid_pick_second_corner", new[] { "Karşı köşeyi seç", "Tap the opposite corner" } },
            { "grid_invalid_rectangle", new[] { "Bu dikdörtgen sayıyla uyuşmuyor", "That rectangle does not match the clue" } },
            { "grid_region_set", new[] { "Dikdörtgen yerleştirildi", "Rectangle placed" } },
            { "grid_fill_all", new[] { "{0} hücre boş. Sayı seçip iki köşeyi işaretle.", "{0} cells left. Select a clue, then two corners." } },
            { "grid_fill_singles", new[] { "{0} hücre boş. Sarı 1'lere dokun.", "{0} cells left. Tap the yellow 1s." } },
            { "grid_ready", new[] { "Tüm hücreler dolu. KONTROL ET'e bas.", "All cells covered. Press CHECK." } },
            { "grid_hint_applied", new[] { "Bir dikdörtgen ipucuyla dolduruldu", "One rectangle was filled as a hint" } },
            { "grid_clear", new[] { "SIFIRLA", "RESET" } },
            { "grid_submit", new[] { "KONTROL ET", "CHECK" } },
            { "akari_status", new[] { "Lamba {0}/2 · Aydınlanan hücre {1}/8", "Lamps {0}/2 · Cells lit {1}/8" } },
            { "akari_wall", new[] { "DUVAR", "WALL" } },
            { "akari_hint", new[] { "Çözümdeki lambalardan biri yerleştirildi", "One lamp from a valid solution was placed" } },
            { "door_cycle_status", new[] { "Kapıların eşzamanlı açıldığı ilk pozitif anı seç", "Choose the first positive time both doors are open" } },
            { "door_open", new[] { "AÇIK", "OPEN" } },
            { "door_closed", new[] { "KAPALI", "CLOSED" } },
            { "door_cycle_hint", new[] { "Kapıların ilk ortak açık anı seçildi", "The first shared opening time was selected" } },
            { "valve_cause_progress", new[] { "Su adımı {0}/{1}", "Water step {0}/{1}" } },
            { "valve_cause_left", new[] { "VANA: SOL → ÇEŞME", "VALVE: LEFT → FOUNTAIN" } },
            { "valve_cause_right", new[] { "VANA: SAĞ → ÇARK", "VALVE: RIGHT → WHEEL" } },
            { "valve_cause_fountain", new[] { "ÇEŞME\nSu: {0}/{1}", "FOUNTAIN\nWater: {0}/{1}" } },
            { "valve_cause_wheel", new[] { "ÇARK\nSu: {0}/{1}", "WHEEL\nWater: {0}/{1}" } },
            { "valve_cause_bridge_open", new[] { "KÖPRÜ AÇILDI", "BRIDGE OPEN" } },
            { "valve_cause_bridge_closed", new[] { "KÖPRÜ KAPALI", "BRIDGE CLOSED" } },
            { "valve_cause_step", new[] { "SUYU AKIT  ({0}/{1})", "FLOW WATER  ({0}/{1})" } },
            { "valve_cause_steps_done", new[] { "ÜÇ ADIM TAMAMLANDI", "THREE STEPS COMPLETE" } },
            { "valve_cause_hint", new[] { "Vana sağa çevrildi; üç su adımını çalıştır", "The valve is set right; run all three water steps" } },
            { "compare_before", new[] { "ÖNCE", "BEFORE" } },
            { "compare_after", new[] { "SONRA — DEĞİŞENİ BUL", "AFTER — FIND THE CHANGE" } },
            { "compare_hint", new[] { "Değişen konum ipucuyla işaretlendi", "The changed position is marked" } },
            { "timing_progress", new[] { "KAPI {0} / {1}", "GATE {0} / {1}" } },
            { "timing_stop", new[] { "DURDUR", "STOP" } },
            { "timing_missed", new[] { "Gösterge kapıyı kaçırdı", "The marker missed the gate" } },
            { "timing_hint", new[] { "Sonraki kapı için zaman aralığı genişletildi", "The timing window is wider for the next gate" } },
            { "mini_check", new[] { "KONTROL ET", "CHECK" } },
            { "mini_selected", new[] { "Açık vanalar / fenerler: {0}", "Open valves / lanterns: {0}" } },
            { "mini_hint_applied", new[] { "Bir doğru vana/fener ipucuyla ayarlandı", "One valve/lantern was set correctly as a hint" } },
            { "shadow_choose", new[] { "Gölgeye uygun fener yönünü seç", "Choose the lantern direction for the shadow" } },
            { "shadow_hint", new[] { "Doğru fener yönü gösterildi", "The correct lantern direction is shown" } },
            { "practice_title", new[] { "BULMACA ARŞİVİ", "PUZZLE ARCHIVE" } },
            { "practice_sub", new[] { "Çözdüğün duraklar ve yolculuk istatistiklerin", "Solved stops and journey stats" } },
            { "practice_stats", new[] { "Doğru: {0}  ·  En İyi Seri: {1}  ·  Durak: {2}/{3}", "Correct: {0}  ·  Best Streak: {1}  ·  Stop: {2}/{3}" } },
            { "practice_archive_heading", new[] { "ÇÖZÜLEN BULMACALAR", "SOLVED PUZZLES" } },
            { "practice_archive_empty", new[] { "Henüz çözülen bulmaca yok. İlk durağı tamamladığında kayıtların burada görünür.", "No solved puzzles yet. Your entries will appear here after your first stop." } },
            { "puzzle_archive_button", new[] { "ARŞİV", "ARCHIVE" } },
            { "close_btn", new[] { "KAPAT", "CLOSE" } },
            { "chat_title", new[] { "YOLCU HİKÂYELERİ", "PASSENGER STORIES" } },
            { "chat_sub", new[] { "Beş yolcunun hikâyesini yolculuk boyunca keşfet", "Discover five passenger stories along your journey" } },
            { "journey_map_title", new[] { "İSTANBUL YOLCULUK HARİTASI", "ISTANBUL JOURNEY MAP" } },
            { "journey_map_sub", new[] { "512 durak · 8 semt · sıradaki durağın yeri burada", "512 stops · 8 districts · find your next stop here" } },
            { "journey_map_current", new[] { "• ŞİMDİ BURADASIN", "• YOU ARE HERE" } },
            { "journey_map_complete", new[] { "• TAMAMLANDI", "• COMPLETE" } },
            { "journey_map_ahead", new[] { "· YOLUN DEVAMINDA", "· AHEAD" } },
            { "journey_map_progress", new[] { "DURAKLAR {0}–{1}  ·  {2}/{3} ÇÖZÜLDÜ", "STOPS {0}–{1}  ·  {2}/{3} SOLVED" } },
            { "journey_map_button", new[] { "ROTA", "MAP" } },
            { "journey_map_next_stop", new[] { "SIRADAKİ: {0}. DURAK · {1}", "NEXT: STOP {0} · {1}" } },
            { "menu_next_stop", new[] { "SONRAKİ DURAK · {0}", "NEXT STOP · {0}" } },
            { "taxi_shop_button", new[] { "GARAJ", "GARAGE" } },
            { "taxi_shop_title", new[] { "TAKSİ GARAJI", "TAXI GARAGE" } },
            { "taxi_shop_sub", new[] { "Taksi lambası ve kapı çerçevesi renklerini jetonla kişiselleştir", "Personalize roof-sign and door-trim colors with coins" } },
            { "taxi_shop_coins", new[] { "JETON · {0}", "COINS · {0}" } },
            { "taxi_shop_equipped", new[] { "TAKILI", "EQUIPPED" } },
            { "taxi_shop_equip", new[] { "KULLAN", "EQUIP" } },
            { "taxi_shop_unlock", new[] { "AÇ · {0} JETON", "UNLOCK · {0} COINS" } },
            { "taxi_cosmetic_equipped_toast", new[] { "Taksi görünümü güncellendi", "Taxi look updated" } },
            { "story_chapter", new[] { "HİKÂYE · BÖLÜM {0} / {1}", "STORY · CHAPTER {0} / {1}" } },
            { "story_next", new[] { "►  SONRAKİ BÖLÜM", "►  NEXT CHAPTER" } },
            { "story_unlock", new[] { "SONRAKİ BÖLÜMÜ AÇ · {0} JETON", "UNLOCK NEXT CHAPTER · {0} COINS" } },
            { "story_complete", new[] { "TÜM BÖLÜMLER AÇILDI", "ALL CHAPTERS UNLOCKED" } },
            { "story_unlocked_toast", new[] { "Yolcu hikâyesi bölümü açıldı", "Passenger story chapter unlocked" } },
            { "no_coins", new[] { "Yeterli jeton yok", "Not enough coins" } },
            { "correct", new[] { "DOĞRU CEVAP!", "CORRECT ANSWER!" } },
            { "wrong", new[] { "YANLIŞ CEVAP", "WRONG ANSWER" } },
            { "time_up", new[] { "SÜRE BİTTİ", "TIME'S UP" } },
            { "passenger_disembarked_sub", new[] { "YOLCU İNDİ", "PASSENGER LEFT" } },
            { "stop_pill_fmt", new[] { "{0} / {1} DURAK", "{0} / {1} STOPS" } },
            { "return_to_stop_fmt", new[] { "{0} / {1} DURAĞA DÖN", "RETURN TO STOP {0} / {1}" } },
            { "reward_jeton_fmt", new[] { "+{0} JETON", "+{0} COINS" } },
            { "penalty_jeton_fmt", new[] { "0 JETON  ·  HIZ DÜŞTÜ", "0 COINS  ·  SPEED DROPPED" } },
            { "try_again_btn", new[] { "TEKRAR DENE", "TRY AGAIN" } },
            { "why", new[] { "Neden: ", "Why: " } },
            { "tap_continue", new[] { "Sıradaki durağa devam et", "Continue to next stop" } },
            { "passenger_in", new[] { "{0} arka koltuğa bindi", "{0} got in the back seat" } },
            { "passenger_stay", new[] { "{0} memnun kaldı, sonraki durağa kadar araçta!", "{0} is satisfied & stays until the next stop!" } },
            { "passenger_left", new[] { "{0} hayal kırıklığıyla indi, hız düştü!", "{0} stepped out disappointed, speed dropped!" } },
            { "district", new[] { "{0}. Bölge · {1}", "District {0} · {1}" } },
            { "district_finale_title", new[] { "{0} tamamlandı", "{0} district complete" } },
            { "district_finale_sub", new[] { "{0} durak yanıtı bu semtin günlüğüne kaydedildi.", "{0} stop answers are saved in this district's journal." } },
            { "district_finale_log", new[] { "Yolculuktan notlar", "Notes from the ride" } },
            { "district_finale_continue", new[] { "YOLCULUĞA DEVAM ET", "CONTINUE THE JOURNEY" } },
            { "district_finale_no_answers", new[] { "Bu semtte henüz kaydedilmiş yanıt yok.", "No answers have been recorded for this district yet." } },
            { "district_finale_earlier_entries", new[] { "Önceki {0} yanıt da günlüğe kaydedildi.", "The previous {0} answers are also saved in the journal." } },
            { "streak", new[] { "Seri x{0}", "Streak x{0}" } },
            { "prob_shift", new[] { "OLASILIK KAYMASI", "PROBABILITY SHIFT" } },
            { "demo_playing", new[] { "MELODİ ÇALIYOR (SÜRE DURAKLATILDI)", "MELODY PLAYING (TIMER PAUSED)" } },
            { "finished", new[] { "Tebrikler! 512 durağın hepsini tamamladın!", "Congratulations! You completed all 512 stops!" } },
            { "lift_bridge", new[] { "KÖPRÜYÜ KALDIR", "RAISE BRIDGE" } },
            { "torch_status", new[] { "Fener {0} kıyısında · {1} / {2} dk", "Torch on the {0} bank · {1} / {2} min" } },
            { "torch_left_bank", new[] { "sol", "left" } },
            { "torch_right_bank", new[] { "karşı", "far" } },
            { "torch_traveler_time", new[] { "{0} dk", "{0} min" } },
            { "torch_cross", new[] { "FENERLE KÖPRÜDEN GEÇ", "CROSS WITH THE TORCH" } },
            { "torch_cross_minutes", new[] { "FENERLE GEÇ · {0} DK", "CROSS WITH TORCH · {0} MIN" } },
            { "torch_clear", new[] { "SEÇİMİ TEMİZLE", "CLEAR SELECTION" } },
            { "clear", new[] { "TEMİZLE", "CLEAR" } },
            { "listen_again", new[] { "TEKRAR DİNLE", "LISTEN AGAIN" } },
            { "weather_sunny", new[] { "Güneşli", "Sunny" } },
            { "weather_sunset", new[] { "Altın Gün Batımı", "Golden Sunset" } },
            { "weather_fog", new[] { "Sisli Sabah", "Foggy Morning" } },
            { "weather_rain", new[] { "Yağmurlu", "Rain" } },
            { "weather_night", new[] { "Gece", "Night" } },
            { "coastal_badge", new[] { "{0} · {1} · Filtre için dokun", "{0} · {1} · Tap for Filter" } },
            { "expr_curious", new[] { "Meraklı", "Curious" } },
            { "expr_thoughtful", new[] { "Düşünceli", "Thoughtful" } },
            { "expr_satisfied", new[] { "Memnun", "Satisfied" } },
            { "expr_disappointed", new[] { "Hayal Kırıklığı", "Disappointed" } },
            { "category_Soru", new[] { "Soru", "Trivia" } },
            { "category_Bilmece", new[] { "Bilmece", "Riddle" } },
            { "category_Hesap", new[] { "Hesap", "Math" } },
            { "category_Kelime", new[] { "Kelime", "Word" } },
            { "category_Cikarim", new[] { "Çıkarım", "Logic" } },
            { "category_Gozlem", new[] { "Gözlem", "Observation" } },
            { "category_Minyatur", new[] { "Minyatür", "Miniature" } },
            { "difficulty_0", new[] { "Kolay", "Easy" } },
            { "difficulty_1", new[] { "Orta", "Medium" } },
            { "difficulty_2", new[] { "Zor", "Hard" } },
        };

        public static string T(string key)
        {
            if (Table.TryGetValue(key, out var v)) return v[(int)_lang];
            return key;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        public static readonly string[] DistrictNamesTR = { "Sahil", "Çarşı", "Köprü", "Liman", "Kule", "Değirmen", "Müze", "Gece" };
        public static readonly string[] DistrictNamesEN = { "Coast", "Bazaar", "Bridge", "Harbor", "Tower", "Mill", "Museum", "Night" };
        static readonly string[] DistrictLandmarksTR = { "Kız Kulesi", "Mısır Çarşısı", "Galata Köprüsü", "Karaköy Rıhtımı", "Galata Kulesi", "Yeldeğirmeni", "Haliç Müzesi", "Ortaköy" };
        static readonly string[] DistrictLandmarksEN = { "Maiden's Tower", "Spice Bazaar", "Galata Bridge", "Karaköy Pier", "Galata Tower", "Yeldeğirmeni", "Golden Horn Museum", "Ortaköy" };

        public static string DistrictName(int district)
        {
            district = Math.Max(0, Math.Min(GameConfig.DistrictCount - 1, district));
            return IsTR ? DistrictNamesTR[district] : DistrictNamesEN[district];
        }

        public static string DistrictLandmark(int district)
        {
            district = Math.Max(0, Math.Min(GameConfig.DistrictCount - 1, district));
            return IsTR ? DistrictLandmarksTR[district] : DistrictLandmarksEN[district];
        }
    }
}
