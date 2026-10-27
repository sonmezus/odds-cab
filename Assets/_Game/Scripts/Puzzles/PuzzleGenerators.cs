using System;
using System.Collections.Generic;
using System.Linq;

namespace OddsCab.Puzzles
{
    /// <summary>
    /// Seeded, parametric puzzle generators. Every generator produces exactly one correct option
    /// and three distinct distractors, in both Turkish and English.
    /// </summary>
    public static partial class PuzzleGenerators
    {
        public delegate PuzzleData Generator(Random r, int difficulty);

        static Generator WithMechanic(string mechanicId, Generator generator) => (r, difficulty) =>
        {
            var puzzle = generator(r, difficulty);
            puzzle.mechanicId = mechanicId;
            return puzzle;
        };

        // ---------------------------------------------------------------- helpers

        static string S(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Builds a choice puzzle; options are shuffled with the seeded RNG.</summary>
        public static PuzzleData Choice(Random r, string id, PuzzleCategory cat, int diff, int seconds,
            string qTR, string qEN, string[] answer, IList<string[]> distractors, string wTR, string wEN)
        {
            var opts = new List<string[]> { answer };
            foreach (var d in distractors)
            {
                if (opts.Count >= 4) break;
                if (opts.Any(o => o[0] == d[0] || o[1] == d[1])) continue;
                opts.Add(d);
            }
            if (opts.Count < 4) throw new InvalidOperationException("Not enough distinct options for " + id);

            int[] order = { 0, 1, 2, 3 };
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var p = new PuzzleData
            {
                id = id,
                category = cat,
                kind = PuzzleKind.Choice,
                difficulty = diff,
                seconds = seconds,
                promptTR = qTR,
                promptEN = qEN,
                optionsTR = new string[4],
                optionsEN = new string[4],
                explainTR = wTR,
                explainEN = wEN,
            };
            for (int i = 0; i < 4; i++)
            {
                p.optionsTR[i] = opts[order[i]][0];
                p.optionsEN[i] = opts[order[i]][1];
                if (order[i] == 0) p.correct = i;
            }
            return p;
        }

        static string[] Same(string s) => new[] { s, s };

        /// <summary>Three distinct positive integer distractors, preferring the given "mistake" values.</summary>
        static List<string[]> Numeric(int answer, Random r, Func<int, string> fmt, params int[] preferred)
        {
            var set = new List<int>();
            foreach (var p in preferred)
                if (p > 0 && p != answer && !set.Contains(p)) set.Add(p);
            int step = Math.Max(1, answer / 10);
            int k = 1;
            while (set.Count < 6)
            {
                int c = answer + (r.Next(2) == 0 ? -1 : 1) * k * step;
                if (c > 0 && c != answer && !set.Contains(c)) set.Add(c);
                k++;
            }
            return set.Take(3).Select(v => Same(fmt(v))).ToList();
        }

        static List<string[]> Numeric(int answer, Random r, params int[] preferred) => Numeric(answer, r, S, preferred);

        static readonly string[] NamesBase = { "Ada", "Bora", "Cem", "Deniz", "Ece", "Mert", "Selin", "Kerem", "Zeynep", "Arda", "Nil", "Umut", "Ayşe", "Can", "Elif", "Barış" };
        static readonly string[] NamesAblative = { "Ada'dan", "Bora'dan", "Cem'den", "Deniz'den", "Ece'den", "Mert'ten", "Selin'den", "Kerem'den", "Zeynep'ten", "Arda'dan", "Nil'den", "Umut'tan", "Ayşe'den", "Can'dan", "Elif'ten", "Barış'tan" };

        /// <summary>Four different passenger names for one logic puzzle, so the campaign is not always Ada/Bora/Cem/Deniz.</summary>
        static (string[] nom, string[] abl) PickNames(Random r)
        {
            var idx = Enumerable.Range(0, NamesBase.Length).OrderBy(_ => r.Next()).Take(4).ToArray();
            return (idx.Select(i => NamesBase[i]).ToArray(), idx.Select(i => NamesAblative[i]).ToArray());
        }
        static readonly string[] OrdTR = { "Birinci", "İkinci", "Üçüncü", "Dördüncü" };
        static readonly string[] OrdEN = { "first", "second", "third", "fourth" };

        // ---------------------------------------------------------------- generators

        static readonly (string tr, string en, int ans, int[] wrong)[] UnitFacts =
        {
            ("Bir saatte kaç dakika vardır?", "How many minutes are in an hour?", 60, new[] { 30, 45, 90 }),
            ("Bir dakikada kaç saniye vardır?", "How many seconds are in a minute?", 60, new[] { 100, 30, 120 }),
            ("Bir haftada kaç gün vardır?", "How many days are in a week?", 7, new[] { 5, 6, 8 }),
            ("Bir günde kaç saat vardır?", "How many hours are in a day?", 24, new[] { 12, 20, 48 }),
            ("Bir kilometre kaç metredir?", "How many metres are in a kilometre?", 1000, new[] { 100, 500, 10000 }),
            ("Bir yılda kaç ay vardır?", "How many months are in a year?", 12, new[] { 10, 11, 13 }),
            ("Yarım saat kaç dakikadır?", "How many minutes is half an hour?", 30, new[] { 15, 45, 50 }),
            ("Çeyrek saat kaç dakikadır?", "How many minutes is a quarter of an hour?", 15, new[] { 25, 20, 10 }),
            ("İki saat kaç dakikadır?", "How many minutes are in two hours?", 120, new[] { 100, 200, 90 }),
            ("Bir metre kaç santimetredir?", "How many centimetres are in a metre?", 100, new[] { 10, 1000, 50 }),
            ("Bir düzine kaç tanedir?", "How many items are in a dozen?", 12, new[] { 10, 6, 20 }),
            ("Bir yüzyıl kaç yıldır?", "How many years are in a century?", 100, new[] { 10, 50, 1000 }),
        };

        public static PuzzleData UnitConversion(Random r, int diff) => UnitFact(r, r.Next(UnitFacts.Length));

        public static PuzzleData UnitFact(Random r, int index)
        {
            var f = UnitFacts[index % UnitFacts.Length];
            return Choice(r, "GEN-UNIT-" + index, PuzzleCategory.Soru, 0, 25, f.tr, f.en, Same(S(f.ans)),
                f.wrong.Select(w => Same(S(w))).ToList(),
                UnitFactWhyTR[index % UnitFactWhyTR.Length], UnitFactWhyEN[index % UnitFactWhyEN.Length]);
        }

        // The NEDEN? card states the fact itself, not a generic "remember your units".
        static readonly string[] UnitFactWhyTR =
        {
            "1 saat = 60 dakika; saat kadranındaki 60 dakika çizgisi bir tam turdur.",
            "1 dakika = 60 saniye; saniye ibresi bir turu 60 saniyede atar.",
            "Hafta 7 gündür: pazartesiden pazara.",
            "Dünya kendi çevresinde 24 saatte bir tur döner; bu bir gündür.",
            "Kilo- 'bin' demektir: 1 km = 1000 m.",
            "Ocak'tan Aralık'a 12 ay vardır.",
            "Yarım saat, 60 dakikanın yarısı: 30 dakika.",
            "Çeyrek, dörtte bir demektir: 60 / 4 = 15 dakika.",
            "2 × 60 = 120 dakika.",
            "Santi- 'yüzde bir' demektir: 1 m = 100 cm.",
            "Düzine 12 tanedir; yumurtalar bu yüzden 12'li paketlenir.",
            "Yüzyıl 100 yıldır; asır da aynı anlamdadır.",
        };
        static readonly string[] UnitFactWhyEN =
        {
            "1 hour = 60 minutes; the minute hand makes one full turn.",
            "1 minute = 60 seconds; the second hand turns once in 60 seconds.",
            "A week is 7 days: Monday to Sunday.",
            "Earth spins once in 24 hours; that is one day.",
            "Kilo- means 'thousand': 1 km = 1000 m.",
            "January to December makes 12 months.",
            "Half of 60 minutes is 30 minutes.",
            "A quarter is one fourth: 60 / 4 = 15 minutes.",
            "2 × 60 = 120 minutes.",
            "Centi- means 'one hundredth': 1 m = 100 cm.",
            "A dozen is 12, which is why eggs come in twelves.",
            "A century is 100 years.",
        };

        public static PuzzleData Taximeter(Random r, int diff)
        {
            int[] opens = { 20, 24, 30, 36, 40 };
            int a = opens[r.Next(opens.Length)];
            int b = 5 + r.Next(6);
            int k = 2 + r.Next(diff == 0 ? 5 : 10);
            int fare = a + b * k;
            if (diff == 0 || r.Next(2) == 0)
            {
                return Choice(r, $"GEN-TAXI-F-{a}-{b}-{k}", PuzzleCategory.Hesap, diff, 30 + diff * 5,
                    $"Açılış {a} TL, kilometresi {b} TL. {k} km gidersek taksimetre kaç TL gösterir?",
                    $"Flag fall is {a} TL and each km costs {b} TL. What does the meter show after {k} km?",
                    Same(S(fare) + " TL"),
                    Numeric(fare, r, v => S(v) + " TL", b * k, a + b * (k + 1), (a + b) * k, a + b * (k - 1)),
                    $"{a} + {b} × {k} = {fare}.", $"{a} + {b} × {k} = {fare}.");
            }
            return Choice(r, $"GEN-TAXI-K-{a}-{b}-{k}", PuzzleCategory.Hesap, diff, 35 + diff * 5,
                $"Açılış {a} TL, kilometresi {b} TL. Bekleme ücreti yok. Taksimetre {fare} TL gösteriyorsa kaç km gidildi?",
                $"Flag fall {a} TL, {b} TL per km, no waiting fee. The meter shows {fare} TL. How many km did we drive?",
                Same(S(k) + " km"),
                Numeric(k, r, v => S(v) + " km", k + 1, k - 1, fare / b, k + 2),
                $"({fare} − {a}) / {b} = {k}.", $"({fare} − {a}) / {b} = {k}.");
        }

        public static PuzzleData Discount(Random r, int diff)
        {
            int[] prices = { 100, 200, 300, 400, 500, 800, 1000 };
            int[] pcts = { 10, 20, 25, 50 };
            for (int guard = 0; guard < 100; guard++)
            {
                int p = prices[r.Next(prices.Length)];
                int x = pcts[r.Next(pcts.Length)];
                if (p * x * x % 10000 != 0) continue;
                int final = p - p * x * x / 10000;
                bool upFirst = r.Next(2) == 0;
                string orderTR = upFirst ? $"önce %{x} zam, sonra yeni fiyat üzerinden %{x} indirim" : $"önce %{x} indirim, sonra yeni fiyat üzerinden %{x} zam";
                string orderEN = upFirst ? $"first raised by {x}%, then the new price is cut by {x}%" : $"first cut by {x}%, then the new price is raised by {x}%";
                return Choice(r, $"GEN-DISC-{p}-{x}-{upFirst}", PuzzleCategory.Hesap, Math.Max(1, diff), 40,
                    $"{p} liralık bir hediyeye {orderTR} yapılıyor. Son fiyat kaç lira?",
                    $"A {p} lira gift is {orderEN}. What is the final price?",
                    Same(S(final)),
                    Numeric(final, r, p, p + p * x / 100, p - p * x / 100, final - p * x / 200),
                    $"Yüzdeler farklı tabanlara uygulanır: {p} × {(100 + x) / 100f:0.##} × {(100 - x) / 100f:0.##} = {final}.",
                    $"The percentages apply to different bases: {p} × {(100 + x) / 100f:0.##} × {(100 - x) / 100f:0.##} = {final}.");
            }
            return Taximeter(r, diff);
        }

        static readonly (int v1, int v2, int avg)[] SpeedPairs =
        {
            (30, 60, 40), (20, 30, 24), (40, 60, 48), (30, 45, 36), (60, 90, 72), (20, 60, 30), (36, 45, 40), (40, 120, 60), (45, 90, 60)
        };

        public static PuzzleData AverageSpeed(Random r, int diff)
        {
            var sp = SpeedPairs[r.Next(SpeedPairs.Length)];
            int d = 2 + r.Next(5);
            return Choice(r, $"GEN-AVG-{sp.v1}-{sp.v2}-{d}", PuzzleCategory.Hesap, 2, 50,
                $"Taksi {d} km'yi {sp.v1} km/sa ile gidip aynı {d} km'yi {sp.v2} km/sa ile dönüyor. Hiç durmuyor. Tüm yolun ortalama hızı kaç km/sa?",
                $"The taxi drives {d} km at {sp.v1} km/h and returns the same {d} km at {sp.v2} km/h without stopping. What is the average speed for the whole trip?",
                Same(S(sp.avg)),
                Numeric(sp.avg, r, (sp.v1 + sp.v2) / 2, sp.v1, sp.v2, sp.avg + 5),
                "Ortalama hız = toplam yol / toplam süre. Yavaş giden kısım daha uzun sürdüğü için sonuç aritmetik ortalamadan küçüktür.",
                "Average speed = total distance / total time. The slow leg takes longer, so the result is below the simple mean.");
        }

        public static PuzzleData Pigeonhole(Random r, int diff)
        {
            int c = 2 + r.Next(diff == 0 ? 2 : 4);
            int n = diff >= 2 ? 3 : 2;
            int ans = (n - 1) * c + 1;
            string[] colTR = { "kırmızı", "mavi", "sarı", "yeşil", "mor" };
            string[] colEN = { "red", "blue", "yellow", "green", "purple" };
            string listTR = string.Join(", ", colTR.Take(c));
            string listEN = string.Join(", ", colEN.Take(c));
            return Choice(r, $"GEN-PIGEON-{c}-{n}", PuzzleCategory.Cikarim, Math.Max(1, diff), 40,
                $"Torbada bol miktarda {listTR} jeton var. Bakmadan, geri koymadan en az {n} aynı renk jetonu KESİN almak için kaç jeton çekmelisin?",
                $"A bag holds plenty of {listEN} tokens. Without looking or putting any back, how many must you draw to be CERTAIN of {n} tokens of the same colour?",
                Same(S(ans)),
                Numeric(ans, r, ans - 1, n * c, c, ans + 1),
                $"En kötü durumda her renkten {n - 1} tane gelir ({(n - 1) * c} jeton). Bir sonraki jeton mutlaka tekrar eder: {ans}.",
                $"Worst case you get {n - 1} of each colour ({(n - 1) * c} tokens). The next one must repeat: {ans}.");
        }

        public static PuzzleData Swap(Random r, int diff)
        {
            string[] colTR = { "kırmızı", "mavi", "sarı", "yeşil" };
            string[] colEN = { "red", "blue", "yellow", "green" };
            int people = 4;
            var (nm, _) = PickNames(r);
            int swaps = diff == 0 ? 2 : (diff == 1 ? 3 : 4);
            int[] holder = { 0, 1, 2, 3 }; // holder[color] = person
            var stepsTR = new List<string>();
            var stepsEN = new List<string>();
            int lastA = -1, lastB = -1;
            for (int s = 0; s < swaps; s++)
            {
                int a, b;
                do { a = r.Next(people); b = r.Next(people); } while (a == b || (Math.Min(a, b) == Math.Min(lastA, lastB) && Math.Max(a, b) == Math.Max(lastA, lastB)));
                lastA = a; lastB = b;
                for (int c = 0; c < 4; c++)
                {
                    if (holder[c] == a) holder[c] = b;
                    else if (holder[c] == b) holder[c] = a;
                }
                stepsTR.Add($"{nm[a]} ile {nm[b]}");
                stepsEN.Add($"{nm[a]} and {nm[b]}");
            }
            int target = 0;
            for (int c = 0; c < 4; c++) if (holder[c] != c) { target = c; break; }
            int ans = holder[target];
            var distract = Enumerable.Range(0, 4).Where(i => i != ans).Select(i => Same(nm[i])).ToList();
            return Choice(r, $"GEN-SWAP-{string.Join("", nm)}-{string.Join("", holder)}-{swaps}", PuzzleCategory.Cikarim, diff, 35 + swaps * 5,
                $"Başlangıçta {nm[0]} kırmızı, {nm[1]} mavi, {nm[2]} sarı, {nm[3]} yeşil kutuyu tutuyor. Sırayla takas ediyorlar: {string.Join("; ", stepsTR)}. Şimdi {colTR[target]} kutu kimde?",
                $"{nm[0]} holds the red box, {nm[1]} blue, {nm[2]} yellow, {nm[3]} green. They swap in order: {string.Join("; ", stepsEN)}. Who has the {colEN[target]} box now?",
                Same(nm[ans]), distract,
                "Takasları tek tek izle; yalnız sorulan kutunun yolunu takip etmek yeterli.",
                "Follow the swaps one by one; tracking only the asked box is enough.");
        }

        public static PuzzleData Order(Random r, int diff)
        {
            var (NamesBase, NamesAblative) = PickNames(r);
            int[] perm = { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = r.Next(i + 1); (perm[i], perm[j]) = (perm[j], perm[i]); }
            var cluesTR = new List<string>();
            var cluesEN = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                int x = perm[i], y = perm[i + 1];
                if (r.Next(2) == 0 || diff == 0)
                {
                    cluesTR.Add($"{NamesBase[x]}, {NamesAblative[y]} önce indi");
                    cluesEN.Add($"{NamesBase[x]} got off before {NamesBase[y]}");
                }
                else
                {
                    cluesTR.Add($"{NamesBase[y]}, {NamesAblative[x]} sonra indi");
                    cluesEN.Add($"{NamesBase[y]} got off after {NamesBase[x]}");
                }
            }
            // shuffle clue order for harder levels
            if (diff > 0)
            {
                for (int i = 2; i > 0; i--)
                {
                    int j = r.Next(i + 1);
                    (cluesTR[i], cluesTR[j]) = (cluesTR[j], cluesTR[i]);
                    (cluesEN[i], cluesEN[j]) = (cluesEN[j], cluesEN[i]);
                }
            }
            int k = 1 + r.Next(3);
            int ans = perm[k];
            var distract = Enumerable.Range(0, 4).Where(i => i != ans).Select(i => Same(NamesBase[i])).ToList();
            string order = string.Join(" → ", perm.Select(i => NamesBase[i]));
            return Choice(r, $"GEN-ORDER-{string.Join("", NamesBase)}-{string.Join("", perm)}-{k}", PuzzleCategory.Cikarim, diff, 40,
                $"Dört yolcu farklı duraklarda indi. {string.Join(". ", cluesTR)}. {OrdTR[k]} inen kim?",
                $"Four passengers got off at different stops. {string.Join(". ", cluesEN)}. Who got off {OrdEN[k]}?",
                Same(NamesBase[ans]), distract,
                "İpuçları tek bir sıra verir: " + order + ".",
                "The clues give a single order: " + order + ".");
        }

        /// <summary>
        /// F36 "Olay sırası" as a real ordering task: the player taps all four passengers in the order they got off.
        /// Three chained before/after clues always fix a single order.
        /// </summary>
        public static PuzzleData OrderFull(Random r, int diff)
        {
            var (NamesBase, NamesAblative) = PickNames(r);
            int[] perm = { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = r.Next(i + 1); (perm[i], perm[j]) = (perm[j], perm[i]); }
            var cluesTR = new List<string>();
            var cluesEN = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                int x = perm[i], y = perm[i + 1];
                if (diff == 0 || r.Next(2) == 0)
                {
                    cluesTR.Add($"{NamesBase[x]}, {NamesAblative[y]} önce indi");
                    cluesEN.Add($"{NamesBase[x]} got off before {NamesBase[y]}");
                }
                else
                {
                    cluesTR.Add($"{NamesBase[y]}, {NamesAblative[x]} sonra indi");
                    cluesEN.Add($"{NamesBase[y]} got off after {NamesBase[x]}");
                }
            }
            if (diff > 0)
            {
                for (int i = 2; i > 0; i--)
                {
                    int j = r.Next(i + 1);
                    (cluesTR[i], cluesTR[j]) = (cluesTR[j], cluesTR[i]);
                    (cluesEN[i], cluesEN[j]) = (cluesEN[j], cluesEN[i]);
                }
            }

            // Buttons show the four names in a shuffled order; ints = correct order as button indices.
            int[] shown = { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = r.Next(i + 1); (shown[i], shown[j]) = (shown[j], shown[i]); }
            var names = shown.Select(i => NamesBase[i]).ToArray();
            var ints = perm.Select(n => Array.IndexOf(shown, n)).ToArray();
            string order = string.Join(" → ", perm.Select(i => NamesBase[i]));

            return new PuzzleData
            {
                id = $"GEN-ORDFULL-{string.Join("", NamesBase)}-{string.Join("", perm)}-{diff}",
                category = PuzzleCategory.Cikarim,
                kind = PuzzleKind.Ordering,
                difficulty = diff,
                seconds = 45 + diff * 5,
                promptTR = $"Dört yolcu farklı duraklarda indi. {string.Join(". ", cluesTR)}. İlk inenden başlayarak sırayla dokun.",
                promptEN = $"Four passengers got off at different stops. {string.Join(". ", cluesEN)}. Tap them in order, first off first.",
                optionsTR = names,
                optionsEN = (string[])names.Clone(),
                ints = ints,
                correct = ints[0],
                explainTR = "İpuçları tek bir sıra verir: " + order + ".",
                explainEN = "The clues give a single order: " + order + ".",
            };
        }

        /// <summary>F39 Shikaku: partition a small numbered grid into rectangles.</summary>
        public static PuzzleData Shikaku(Random r, int diff)
        {
            var layout = ShikakuLayout.Generate(r.Next(), diff);
            return new PuzzleData
            {
                id = "GEN-SHIKAKU-" + r.Next(),
                category = PuzzleCategory.Cikarim,
                kind = PuzzleKind.Grid,
                difficulty = diff,
                seconds = 45 + diff * 10,
                promptTR = "Sayılar dikdörtgenlerin alanını gösterir. Izgarayı üst üste binmeyen dikdörtgenlere ayır.",
                promptEN = "Each number is the area of its rectangle. Divide the grid into non-overlapping rectangles.",
                explainTR = "Her sayı kendi alanı kadar hücre kaplayan tek bir dikdörtgenin içinde kalır; dikdörtgenler tüm ızgarayı örter.",
                explainEN = "Each clue belongs to one rectangle with that many cells; the rectangles cover the whole grid.",
                gridWidth = layout.Width,
                gridHeight = layout.Height,
                gridClues = layout.Clues,
                gridSolutionOwners = layout.Owners,
            };
        }

        /// <summary>F41/F43: compare two snapshots and identify the one changed position.</summary>
        public static PuzzleData BeforeAfter(Random r, int diff)
        {
            int size = diff >= 2 ? 4 : 3;
            int count = size * size;
            var before = Enumerable.Range(1, count).ToArray();
            int changed = r.Next(count);
            int replacement = count + 1 + r.Next(5);
            var after = (int[])before.Clone();
            after[changed] = replacement;
            return new PuzzleData
            {
                id = $"GEN-COMPARE-{size}-{changed}-{replacement}",
                category = PuzzleCategory.Gozlem,
                kind = PuzzleKind.Compare,
                difficulty = diff,
                seconds = 30 + diff * 10,
                promptTR = "Yolcunun eşyalarına bak. SONRA görüntüsünde değişen kutunun yerini seç.",
                promptEN = "Study the passenger's items. Select the position that changed in AFTER.",
                explainTR = "İki görüntü arasındaki tek fark bu kutudaki parçadır.",
                explainEN = "This tile is the only item that differs between the two snapshots.",
                correct = changed,
                compareBefore = before,
                compareAfter = after,
            };
        }

        /// <summary>F24/O34: catch a ferry by tapping the moving marker at successive gate positions.</summary>
        public static PuzzleData TimingGate(Random r, int diff)
        {
            int gates = diff >= 2 ? 3 : 2;
            var targets = new int[gates];
            for (int i = 0; i < gates; i++)
            {
                int candidate;
                int attempts = 0;
                do
                {
                    candidate = 220 + r.Next(561); // keep targets away from the hard-to-read endpoints
                    attempts++;
                } while (attempts < 20 && targets.Take(i).Any(x => Math.Abs(x - candidate) < 170));
                targets[i] = candidate;
            }
            return new PuzzleData
            {
                id = $"GEN-TIMING-{diff}-{string.Join("-", targets)}",
                category = PuzzleCategory.Gozlem,
                kind = PuzzleKind.Timing,
                difficulty = diff,
                seconds = 35 + diff * 10,
                promptTR = gates == 2
                    ? "Vapura yetişmek için hareketli göstergeyi iki kapıdan da tam geçerken durdur."
                    : "Vapura yetişmek için hareketli göstergeyi üç kapıdan da tam geçerken durdur.",
                promptEN = gates == 2
                    ? "Catch the ferry: stop the moving marker inside both gates, one at a time."
                    : "Catch the ferry: stop the moving marker inside all three gates, one at a time.",
                explainTR = "Her kapıda gösterge hedef aralığa girdiğinde dokun. Üçüncü kapı zor seviyede açılır.",
                explainEN = "Tap while the moving marker is inside each target gate. Hard puzzles add a third gate.",
                timingTargets = targets,
                timingTolerance = diff == 0 ? 70 : diff == 1 ? 52 : 38,
                answerValue = 0,
            };
        }

        public static PuzzleData WaterValve(Random r, int diff)
        {
            int mask = 1 + r.Next(7);
            int wanted = ((mask & 1) != 0 ? 1 : 0) + ((mask & 2) != 0 ? 2 : 0) + ((mask & 4) != 0 ? 4 : 0);
            return new PuzzleData
            {
                id = $"GEN-VALVE-{mask}-{diff}", category = PuzzleCategory.Minyatur, kind = PuzzleKind.Valve,
                difficulty = diff, seconds = 40 + diff * 10, answerValue = mask,
                ints = new[] { 1, 2, 4 },
                promptTR = $"Olasılığı {wanted} birim artırmak için 1, 2 ve 4 birimlik vanaları aç. Doğru vanaları seç.",
                promptEN = $"Raise the chance by {wanted} units. Open the 1, 2 and 4-unit valves to match the target.",
                explainTR = "Her vana sırasıyla 1, 2 veya 4 birim ekler; seçilen vanaların toplamı hedefe eşit olmalı.",
                explainEN = "The valves add 1, 2 and 4 units; the selected valves must sum to the target."
            };
        }

        public static PuzzleData LanternBridge(Random r, int diff)
        {
            int mask = new[] { 3, 12, 15 }[r.Next(3)];
            string promptTR = mask == 3 ? "Kıyı A fenerini ve eşi B'yi yak; diğerlerini kapalı tut."
                : mask == 12 ? "Köprü C fenerini ve eşi D'yi yak; diğerlerini kapalı tut."
                : "Köprünün iki fener çiftini de yak: A-B ve C-D.";
            string promptEN = mask == 3 ? "Light shore lantern A and its pair B; leave the others off."
                : mask == 12 ? "Light bridge lantern C and its pair D; leave the others off."
                : "Light both lantern pairs on the bridge: A-B and C-D.";
            return new PuzzleData
            {
                id = $"GEN-LANTERN-{mask}-{diff}", category = PuzzleCategory.Minyatur, kind = PuzzleKind.Lantern,
                difficulty = diff, seconds = 40 + diff * 10, answerValue = mask,
                optionsTR = new[] { "A", "B", "C", "D" },
                optionsEN = new[] { "A", "B", "C", "D" },
                promptTR = promptTR,
                promptEN = promptEN,
                explainTR = "İstenen ışık düzeni, köprünün giriş ve çıkışını dengeli bir fener çiftiyle bağlar.",
                explainEN = "The correct lantern pattern connects the bridge entrance and exit with balanced pairs."
            };
        }

        public static PuzzleData ShadowLamp(Random r, int diff)
        {
            string[] tr = { "Kuzey", "Doğu", "Güney", "Batı" };
            string[] en = { "North", "East", "South", "West" };
            int shadowDirection = r.Next(4);
            int correct = (shadowDirection + 2) % 4;
            return new PuzzleData
            {
                id = $"GEN-SHADOW-{shadowDirection}-{diff}", category = PuzzleCategory.Minyatur, kind = PuzzleKind.Shadow,
                difficulty = diff, seconds = 35 + diff * 10, correct = correct,
                optionsTR = tr, optionsEN = en,
                promptTR = $"Gölge {tr[shadowDirection].ToLowerInvariant()} uzasın. Feneri hangi yönde konumlandırmalısın?",
                promptEN = $"The shadow must point {en[shadowDirection].ToLowerInvariant()}. Which direction should the lantern face?",
                explainTR = "Gölge ışığın ters yönüne düşer; seçeneklerden doğru lamba yönünü belirle.",
                explainEN = "A shadow falls opposite the light; choose the lantern direction that creates the requested shadow."
            };
        }

        public static PuzzleData BakeryClock(Random r, int diff)
        {
            var trBase = new[] { "Simit (6 dk)", "Börek (18 dk)", "Kurabiye (12 dk)", "Ekmek (24 dk)" };
            var enBase = new[] { "Bagel (6 min)", "Börek (18 min)", "Cookie (12 min)", "Bread (24 min)" };
            int[] duration = { 6, 18, 12, 24 };
            int[] shown = { 0, 1, 2, 3 };
            for (int i = shown.Length - 1; i > 0; i--) { int j = r.Next(i + 1); (shown[i], shown[j]) = (shown[j], shown[i]); }
            var optionsTR = shown.Select(i => trBase[i]).ToArray();
            var optionsEN = shown.Select(i => enBase[i]).ToArray();
            int[] order = Enumerable.Range(0, 4).OrderBy(i => duration[shown[i]]).ToArray();
            string explainTR = "Pişirme sürelerini küçükten büyüğe sırala: 6, 12, 18 ve 24 dakika.";
            string explainEN = "Order the baking times from shortest to longest: 6, 12, 18 and 24 minutes.";
            return new PuzzleData
            {
                id = "GEN-BAKERY-CLOCK-" + r.Next(), category = PuzzleCategory.Minyatur, kind = PuzzleKind.Ordering,
                difficulty = diff, seconds = 45 + diff * 10, optionsTR = optionsTR, optionsEN = optionsEN,
                ints = order, correct = order[0],
                promptTR = "Pastanenin fırın saatine göre ürünleri en kısa pişenden en uzuna sırala.",
                promptEN = "Use the bakery clock: tap the pastries from shortest baking time to longest.",
                explainTR = explainTR, explainEN = explainEN
            };
        }

        public static PuzzleData Clock(Random r, int diff)
        {
            int m = 1 + r.Next(11);
            int mm = m * 5;
            int h = 8 + r.Next(12);
            string F(int hh, int mi) => $"{hh:00}:{mi:00}";
            var wrong = new List<string[]>
            {
                Same(F(h, m)),
                Same(F(h + 1, mm)),
                Same(F(h, (60 - mm) % 60 == mm ? (mm + 15) % 60 : (60 - mm) % 60)),
                Same(F(m, h % 60 == 0 ? 5 : Math.Min(55, h))),
                Same(F(h - 1, mm)),
            };
            return Choice(r, $"GEN-CLOCK-{h}-{m}", PuzzleCategory.Gozlem, diff, 30,
                $"Saatte yalnız yelkovan kalmış ve {m} rakamını gösteriyor. Yolcunun buluşması {h:00}:00'dan sonra, {h + 1:00}:00'dan önce. Saat kaç?",
                $"Only the minute hand is left on the clock and it points at {m}. The passenger's meeting is after {h:00}:00 and before {h + 1:00}:00. What time is it?",
                Same(F(h, mm)), wrong,
                $"Yelkovan {m} rakamında: {m} × 5 = {mm} dakika. Zaman aralığı saati belirler.",
                $"Minute hand on {m}: {m} × 5 = {mm} minutes. The time window fixes the hour.");
        }

        public static PuzzleData Luggage(Random r, int diff)
        {
            string[] shelves = { "A", "B", "C", "D" };
            int start = r.Next(4);
            int steps = 3 + r.Next(diff == 0 ? 4 : 12);
            int ans = (start + steps) % 4;
            var distract = Enumerable.Range(0, 4).Where(i => i != ans).Select(i => Same(shelves[i])).ToList();
            return Choice(r, $"GEN-LUG-{start}-{steps}", PuzzleCategory.Gozlem, diff, 35,
                $"Dört raf soldan sağa A, B, C, D. Her adımda bavullar bir sağa kayıyor, D'deki A'ya dönüyor. Kırmızı bavul {shelves[start]} rafında başlıyor. {steps} adım sonra nerede?",
                $"Four shelves left to right: A, B, C, D. Each step every suitcase moves one shelf right, and D wraps to A. The red suitcase starts on {shelves[start]}. Where is it after {steps} steps?",
                Same(shelves[ans]), distract,
                $"4 adımda bir tur tamamlanır: {steps} mod 4 = {steps % 4} kayma.",
                $"Every 4 steps is a full lap: {steps} mod 4 = {steps % 4} shifts.");
        }

        static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

        public static PuzzleData DoorTiming(Random r, int diff)
        {
            int a, b;
            do { a = 2 + r.Next(diff == 0 ? 4 : 8); b = 2 + r.Next(diff == 0 ? 4 : 8); } while (a == b || a % b == 0 || b % a == 0);
            int lcm = a * b / Gcd(a, b);
            return Choice(r, $"GEN-DOOR-{a}-{b}", PuzzleCategory.Hesap, diff, 40,
                $"Kapı A her {a} saniyede, kapı B her {b} saniyede bir açılıyor. Şimdi ikisi birden açık. İkisi en erken kaç saniye sonra yine birlikte açılır?",
                $"Gate A opens every {a} seconds and gate B every {b} seconds. Both are open right now. After how many seconds are they next open together?",
                Same(S(lcm)),
                Numeric(lcm, r, a * b == lcm ? a + b : a * b, a + b, lcm + Math.Min(a, b), Math.Max(a, b)),
                $"{a} ve {b}'nin en küçük ortak katı {lcm}.", $"The least common multiple of {a} and {b} is {lcm}.");
        }

        public static PuzzleData CoinChange(Random r, int diff)
        {
            int[] coins = { 50, 20, 10, 5, 2, 1 };
            int target = 7 + r.Next(diff == 0 ? 30 : 93);
            int rest = target, count = 0;
            var used = new List<string>();
            foreach (var c in coins)
            {
                int n = rest / c;
                if (n > 0) { used.Add($"{n}×{c}"); count += n; rest -= n * c; }
            }
            return Choice(r, $"GEN-COIN-{target}", PuzzleCategory.Hesap, diff, 35,
                $"Kasada 1, 2, 5, 10, 20 ve 50 liralık bol para var. {target} lira para üstünü EN AZ kaç parça ile verebilirsin?",
                $"The till has plenty of 1, 2, 5, 10, 20 and 50 lira pieces. What is the FEWEST pieces needed to give {target} lira change?",
                Same(S(count)),
                Numeric(count, r, count + 1, count - 1, count + 2, target / 10),
                "En büyük parçadan başla: " + string.Join(" + ", used) + ".",
                "Start from the largest piece: " + string.Join(" + ", used) + ".");
        }

        public static PuzzleData TimeWindow(Random r, int diff)
        {
            int nowH = 7 + r.Next(13), nowM = r.Next(12) * 5;
            int slack = 10 + r.Next(15);
            string[] routeTR = { "Sahil yolu", "Tünel", "Çarşı içi" };
            string[] routeEN = { "Coast road", "Tunnel", "Bazaar streets" };
            int good = r.Next(3);
            int[] dur = new int[3];
            for (int i = 0; i < 3; i++) dur[i] = i == good ? slack - r.Next(1, 4) : slack + r.Next(1, 8);
            int Total(int h, int m) => h * 60 + m;
            string T(int t) => $"{t / 60 % 24:00}:{t % 60:00}";
            int now = Total(nowH, nowM);
            int deadline = now + slack;
            var distract = new List<string[]>();
            for (int i = 0; i < 3; i++) if (i != good) distract.Add(new[] { routeTR[i], routeEN[i] });
            distract.Add(new[] { "Hiçbiri", "None of them" });
            return Choice(r, $"GEN-TIME-{now}-{slack}-{good}-{dur[0]}{dur[1]}{dur[2]}", PuzzleCategory.Hesap, Math.Max(1, diff), 45,
                $"Saat {T(now)}. Vapura yetişmek için en geç {T(deadline)}'de iskelede olmalıyız. {routeTR[0]} {dur[0]}, {routeTR[1]} {dur[1]}, {routeTR[2]} {dur[2]} dakika sürüyor. Hangi rota yetiştirir?",
                $"It's {T(now)}. We must reach the pier by {T(deadline)} for the ferry. {routeEN[0]} takes {dur[0]}, {routeEN[1]} {dur[1]}, {routeEN[2]} {dur[2]} minutes. Which route makes it?",
                new[] { routeTR[good], routeEN[good] }, distract,
                $"Elimizde {slack} dakika var; yalnız {routeTR[good]} ({dur[good]} dk) bu süreye sığar.",
                $"We have {slack} minutes; only the {routeEN[good]} ({dur[good]} min) fits.");
        }

        public static PuzzleData Sequence(Random r, int diff)
        {
            int[] seq = new int[5];
            int ans;
            string ruleTR, ruleEN;
            int type = diff == 0 ? 0 : (diff == 1 ? r.Next(2) : 1 + r.Next(3));
            switch (type)
            {
                case 0:
                {
                    int a = 1 + r.Next(10), d = 2 + r.Next(6);
                    for (int i = 0; i < 5; i++) seq[i] = a + d * i;
                    ans = a + d * 5; ruleTR = $"her seferinde {d} eklenir"; ruleEN = $"add {d} each time"; break;
                }
                case 1:
                {
                    int a = 1 + r.Next(4);
                    for (int i = 0; i < 5; i++) seq[i] = a << i;
                    ans = a << 5; ruleTR = "her sayı iki katına çıkar"; ruleEN = "each number doubles"; break;
                }
                case 2:
                {
                    int a = 1 + r.Next(5), p = 2 + r.Next(4), q = 5 + r.Next(5);
                    seq[0] = a;
                    for (int i = 1; i < 5; i++) seq[i] = seq[i - 1] + (i % 2 == 1 ? p : q);
                    ans = seq[4] + p; ruleTR = $"sırayla +{p} ve +{q}"; ruleEN = $"alternately +{p} and +{q}"; break;
                }
                default:
                {
                    int o = r.Next(4);
                    for (int i = 0; i < 5; i++) seq[i] = (i + 1 + o) * (i + 1 + o);
                    ans = (6 + o) * (6 + o); ruleTR = "ardışık sayıların kareleri"; ruleEN = "squares of consecutive numbers"; break;
                }
            }
            int diffLast = seq[4] - seq[3];
            string s = string.Join(", ", seq.Select(S));
            return Choice(r, $"GEN-SEQ-{type}-{s}", PuzzleCategory.Gozlem, diff, 30 + diff * 5,
                $"Taksi plakalarında bir dizi var: {s}, ? Sıradaki sayı kaç?",
                $"The licence plates follow a sequence: {s}, ? What comes next?",
                Same(S(ans)),
                Numeric(ans, r, seq[4] + diffLast + 1, seq[4] + diffLast - 1, seq[4] * 2, ans + diffLast),
                "Kural: " + ruleTR + ".", "Rule: " + ruleEN + ".");
        }

        static readonly (string w, string d)[] WordsTR =
        {
            ("LİMON", "ekşi, sarı turunçgil"), ("SİMİT", "susamlı halka ekmek"), ("VAPUR", "denizde yolcu taşıyan gemi"),
            ("KÖPRÜ", "iki yakayı birbirine bağlayan yapı"), ("DURAK", "yolcuların araç beklediği yer"), ("HARİTA", "katlanıp cebe sığan şehir"),
            ("FENER", "karanlıkta yol gösteren ışık"), ("MARTI", "kıyıda yaşayan beyaz deniz kuşu"), ("PAZAR", "tezgâhlarda alışveriş yapılan yer"),
            ("BALIK", "suda yaşayan, solungaçla nefes alan canlı"), ("KAHVE", "telvesiyle fal bakılan içecek"), ("ÇANTA", "eşya taşınan kulplu kap"),
            ("KEMAN", "yaylı bir çalgı"), ("BİLET", "yolculuk için alınan kâğıt"), ("ÇİÇEK", "bitkinin renkli, kokulu kısmı"),
            ("KALEM", "yazı yazmaya yarayan araç"), ("TAKSİ", "sarı, ücretli şehir arabası"), ("KULE", "yüksek, ince yapı"),
        };

        static readonly (string w, string d)[] WordsEN =
        {
            ("LEMON", "a sour yellow citrus fruit"), ("BAGEL", "a ring-shaped bread roll"), ("FERRY", "a boat that carries passengers across water"),
            ("BRIDGE", "a structure joining two shores"), ("TICKET", "paper you buy to travel"), ("MAP", "a city you can fold into your pocket"),
            ("LAMP", "a light that shows the way at night"), ("GULL", "a white seabird of the coast"), ("MARKET", "a place with stalls for shopping"),
            ("TRAIN", "a vehicle that runs on rails"), ("COFFEE", "a dark drink made from roasted beans"), ("PURSE", "a small bag for money"),
            ("VIOLIN", "a bowed string instrument"), ("CLOCK", "it has hands but no fingers"), ("FLOWER", "the colourful, scented part of a plant"),
            ("PENCIL", "a tool for writing with graphite"), ("TAXI", "a yellow city car you pay to ride"), ("TOWER", "a tall, narrow building"),
        };

        const string DecoyPoolTR = "AEİKLMNORSTUY";
        const string DecoyPoolEN = "AEIKLMNORSTUY";

        /// <summary>Shuffled tiles for a word plus decoy letters; never returns the word in reading order.</summary>
        static string TileLetters(string word, int decoys, string pool, Random r)
        {
            var c = new List<char>(word.ToCharArray());
            for (int i = 0; i < decoys; i++) c.Add(pool[r.Next(pool.Length)]);
            var arr = c.ToArray();
            for (int guard = 0; guard < 20; guard++)
            {
                for (int i = arr.Length - 1; i > 0; i--) { int j = r.Next(i + 1); (arr[i], arr[j]) = (arr[j], arr[i]); }
                if (!new string(arr).StartsWith(word)) break;
            }
            return new string(arr);
        }

        /// <summary>
        /// F26 "Tanımlı anagram" as a letter-tile task: tap the tiles to spell the defined word.
        /// Medium adds one decoy letter, hard adds two.
        /// </summary>
        public static PuzzleData AnagramTiles(Random r, int diff)
        {
            int i = r.Next(WordsTR.Length);
            var tr = WordsTR[i];
            var en = WordsEN[i];
            int decoys = diff <= 0 ? 0 : (diff == 1 ? 1 : 2);
            return new PuzzleData
            {
                id = $"GEN-TILES-{i}-{diff}",
                category = PuzzleCategory.Kelime,
                kind = PuzzleKind.Letters,
                difficulty = diff,
                seconds = 35 + diff * 10,
                promptTR = $"Harf taşlarına sırayla dokun: '{tr.d}'." + (decoys > 0 ? " Fazladan harf var." : ""),
                promptEN = $"Tap the letter tiles in order: '{en.d}'." + (decoys > 0 ? " Some letters are extra." : ""),
                answerTR = tr.w,
                answerEN = en.w,
                lettersTR = TileLetters(tr.w, decoys, DecoyPoolTR, r),
                lettersEN = TileLetters(en.w, decoys, DecoyPoolEN, r),
                optionsTR = new[] { tr.w },
                optionsEN = new[] { en.w },
                correct = 0,
                explainTR = $"{tr.w}: {tr.d}.",
                explainEN = $"{en.w}: {en.d}.",
            };
        }

        static string Scramble(string w, Random r)
        {
            var c = w.ToCharArray();
            for (int guard = 0; guard < 20; guard++)
            {
                for (int i = c.Length - 1; i > 0; i--) { int j = r.Next(i + 1); (c[i], c[j]) = (c[j], c[i]); }
                if (new string(c) != w) break;
            }
            return string.Join(" ", c);
        }

        public static PuzzleData Anagram(Random r, int diff)
        {
            int i = r.Next(WordsTR.Length);
            var tr = WordsTR[i];
            var en = WordsEN[i];
            // Distractors: other words, paired by index so TR/EN line up.
            var pool = Enumerable.Range(0, WordsTR.Length).Where(x => x != i).OrderBy(_ => r.Next()).Take(3)
                .Select(x => new[] { WordsTR[x].w, WordsEN[x].w }).ToList();
            return Choice(r, $"GEN-ANA-{i}", PuzzleCategory.Kelime, diff, 30,
                $"{Scramble(tr.w, r)} harflerini birer kez kullan: '{tr.d}'. Hangi sözcük?",
                $"Use the letters {Scramble(en.w, r)} once each: '{en.d}'. Which word?",
                new[] { tr.w, en.w }, pool,
                $"{tr.w}: {tr.d}.", $"{en.w}: {en.d}.");
        }

        // ---------------------------------------------------------------- miniatures

        public static PuzzleData Bells(Random r, int diff)
        {
            // Rule types per difficulty: 0 up, 1 down, 4 each bell twice | 1, 2 climb-and-back, 4 | 2, 3 two-fwd-one-back, 5 cycling +1/+2/+3 steps
            int[][] typesByDiff = { new[] { 0, 1, 4 }, new[] { 1, 2, 4 }, new[] { 2, 3, 5 } };
            var pool = typesByDiff[Math.Max(0, Math.Min(2, diff))];
            int type = pool[r.Next(pool.Length)];
            int len = diff == 2 ? 6 : 5;
            var seq = new int[len];
            int ans;
            string ruleTR, ruleEN;
            int start = 1 + r.Next(4);
            int Wrap(int v) => ((v - 1) % 4 + 4) % 4 + 1;
            switch (type)
            {
                case 0:
                    for (int i = 0; i < len; i++) seq[i] = Wrap(start + i);
                    ans = Wrap(start + len); ruleTR = "her vuruşta bir sonraki çan, 4'ten sonra 1"; ruleEN = "each strike is the next bell, 1 after 4"; break;
                case 1:
                    for (int i = 0; i < len; i++) seq[i] = Wrap(start - i);
                    ans = Wrap(start - len); ruleTR = "çanlar geriye doğru çalıyor, 1'den sonra 4"; ruleEN = "bells ring backwards, 4 after 1"; break;
                case 2:
                {
                    int[] ping = { 1, 2, 3, 4, 3, 2 };
                    int o = r.Next(6);
                    for (int i = 0; i < len; i++) seq[i] = ping[(o + i) % 6];
                    ans = ping[(o + len) % 6]; ruleTR = "1'den 4'e çıkıp geri dönüyor"; ruleEN = "it climbs from 1 to 4 and back down"; break;
                }
                case 4:
                    for (int i = 0; i < len; i++) seq[i] = Wrap(start + i / 2);
                    ans = Wrap(start + len / 2); ruleTR = "her çan iki kez çalıyor, sonra bir sonrakine geçiliyor"; ruleEN = "each bell rings twice, then the next one"; break;
                case 5:
                {
                    // steps cycle +1, +2, +3 (a +4 step would land on the same bell, so it is never used)
                    int v = start;
                    for (int i = 0; i < len; i++) { seq[i] = v; v = Wrap(v + (i % 3) + 1); }
                    ans = v; ruleTR = "adımlar sırayla +1, +2, +3 ve yeniden +1"; ruleEN = "the steps go +1, +2, +3 and then +1 again"; break;
                }
                default:
                {
                    // two steps forward, one step back
                    int v = start;
                    for (int i = 0; i < len; i++) { seq[i] = v; v = Wrap(i % 2 == 0 ? v + 2 : v - 1); }
                    ans = v; ruleTR = "iki ileri, bir geri"; ruleEN = "two forward, one back"; break;
                }
            }
            return new PuzzleData
            {
                id = $"MIN-BELL-{type}-{string.Join("", seq)}",
                category = PuzzleCategory.Minyatur,
                kind = PuzzleKind.Bells,
                difficulty = diff,
                seconds = 45,
                promptTR = "Çan kulesinin melodisi yarım kaldı. Dinle, gizli kuralı bul ve sıradaki çana dokun.",
                promptEN = "The bell tower's melody stopped halfway. Listen, find the hidden rule and tap the next bell.",
                optionsTR = new[] { "1", "2", "3", "4" },
                optionsEN = new[] { "1", "2", "3", "4" },
                correct = ans - 1,
                ints = seq,
                answerValue = ans,
                explainTR = "Kural: " + ruleTR + ". Sıradaki çan " + ans + ".",
                explainEN = "Rule: " + ruleEN + ". The next bell is " + ans + ".",
            };
        }

        public static PuzzleData Bridge(Random r, int diff)
        {
            int count = diff == 2 ? 6 : 5;
            for (int guard = 0; guard < 500; guard++)
            {
                var weights = Enumerable.Range(1, 12).OrderBy(_ => r.Next()).Take(count).OrderBy(x => x).ToArray();
                int targetMask = 0;
                int pick = diff == 0 ? 2 : (diff == 1 ? 3 : 3 + r.Next(2));
                var idx = Enumerable.Range(0, count).OrderBy(_ => r.Next()).Take(pick).ToArray();
                int target = 0;
                foreach (var i in idx) { targetMask |= 1 << i; target += weights[i]; }
                // must be unique subset
                int solutions = 0;
                for (int m = 1; m < 1 << count; m++)
                {
                    int s = 0;
                    for (int i = 0; i < count; i++) if ((m & (1 << i)) != 0) s += weights[i];
                    if (s == target) solutions++;
                }
                if (solutions != 1) continue;
                var parts = idx.OrderBy(i => weights[i]).Select(i => S(weights[i]));
                return new PuzzleData
                {
                    id = $"MIN-BRIDGE-{target}-{string.Join("_", weights)}",
                    category = PuzzleCategory.Minyatur,
                    kind = PuzzleKind.Bridge,
                    difficulty = diff,
                    seconds = 50 + diff * 10,
                    promptTR = $"Asma köprü {target} kg karşı ağırlıkla tam dengelenir. Kefeye doğru ağırlıkları koy ve köprüyü kaldır.",
                    promptEN = $"The drawbridge balances with exactly {target} kg of counterweight. Put the right weights on the pan and raise it.",
                    optionsTR = new string[0],
                    optionsEN = new string[0],
                    ints = weights,
                    answerValue = target,
                    correct = targetMask,
                    explainTR = $"Tek doğru kombinasyon: {string.Join(" + ", parts)} = {target} kg.",
                    explainEN = $"The only exact combination: {string.Join(" + ", parts)} = {target} kg.",
                };
            }
            return Bells(r, diff);
        }

        public static PuzzleData Route(Random r, int diff)
        {
            return new PuzzleData
            {
                id = $"MIN-ROUTE-{diff}-{r.Next(1000, 9999)}",
                category = PuzzleCategory.Minyatur,
                kind = PuzzleKind.Route,
                difficulty = diff,
                seconds = diff <= 0 ? 40 : (diff == 1 ? 60 : 75),
                promptTR = "Yol karelerine dokunup çevir; taksiden hedefe kesintisiz bir yol kur.",
                promptEN = "Tap road tiles to turn them and join the taxi to the target.",
                optionsTR = new[] { "Sol Kemerli Yol (Kapalı)", "Taş Köprü Rotası (Açık)", "Liman Çıkmazı", "Geri Dön" },
                optionsEN = new[] { "Left Archway Road (Blocked)", "Stone Bridge Route (Open)", "Harbor Dead-End", "Turn Back" },
                correct = 1,
                ints = new[] { 0, 1, 2 },
                answerValue = r.Next(1, 1000000),   // layout seed for RouteTileLayout
                explainTR = "Her kare bir çeyrek tur döner; yol, taksinin girdiği yerden hedefin olduğu çıkışa kadar kopmadan uzanmalı.",
                explainEN = "Each tile turns a quarter at a time; the road must run unbroken from the taxi's entry to the target exit.",
            };
        }

        /// <summary>Generators grouped by category (miniatures handled separately by the planner).</summary>
        public static readonly Dictionary<PuzzleCategory, (Generator gen, int minDiff)[]> ByCategory =
            new Dictionary<PuzzleCategory, (Generator, int)[]>
            {
                { PuzzleCategory.Soru, new (Generator, int)[] { (WithMechanic("F01", UnitConversion), 0) } },
                { PuzzleCategory.Bilmece, new (Generator, int)[0] },
                { PuzzleCategory.Hesap, new (Generator, int)[]
                    {
                        (WithMechanic("F17", Taximeter), 0),
                        (WithMechanic("F23", CoinChange), 0),
                        (WithMechanic("F24", DoorTiming), 0),
                        (WithMechanic("F18", Discount), 1),
                        (WithMechanic("F24", TimeWindow), 1),
                        (WithMechanic("F19", AverageSpeed), 2),
                    } },
                { PuzzleCategory.Kelime, new (Generator, int)[]
                    {
                        (WithMechanic("F26", AnagramTiles), 0),
                        (WithMechanic("F26", Anagram), 0),
                    } },
                { PuzzleCategory.Cikarim, new (Generator, int)[]
                    {
                        (WithMechanic("F35", Swap), 0),
                        (WithMechanic("F36", OrderFull), 0),
                        (WithMechanic("F36", Order), 0),
                        (WithMechanic("F20", Pigeonhole), 1),
                        (WithMechanic("F22", Shikaku), 0),
                    } },
                { PuzzleCategory.Gozlem, new (Generator, int)[]
                    {
                        (WithMechanic("F42", Clock), 0),
                        (WithMechanic("F47", Luggage), 0),
                        (WithMechanic("F38", Sequence), 0),
                        (WithMechanic("F41", BeforeAfter), 0),
                        (WithMechanic("F24", TimingGate), 0),
                    } },
            };
    }
}
