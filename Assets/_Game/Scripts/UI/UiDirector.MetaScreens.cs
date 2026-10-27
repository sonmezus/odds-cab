using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using OddsCab.Puzzles;

namespace OddsCab
{
    public partial class UiDirector
    {
        void BuildJourneyMapModal(Transform root)
        {
            _journeyMapModal = MakePanel(root, "JourneyMapBackdrop", new Color(0.02f, 0.05f, 0.09f, 0.90f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var card = MakePanel(_journeyMapModal.transform, "JourneyMapCard", ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1340f));
            _journeyMapTitleText = MakeLabel(card.transform, "Title", Loc.T("journey_map_title"), 44, FontStyle.Bold, ColYellow, TextAnchor.MiddleLeft);
            SetRect(_journeyMapTitleText.rectTransform, new Vector2(0.5f, 0.94f), new Vector2(0.5f, 0.94f), Vector2.zero, new Vector2(820f, 64f));
            _journeyMapSubText = MakeLabel(card.transform, "Sub", Loc.T("journey_map_sub"), 24, FontStyle.Normal, ColCream, TextAnchor.MiddleLeft);
            SetRect(_journeyMapSubText.rectTransform, new Vector2(0.5f, 0.885f), new Vector2(0.5f, 0.885f), Vector2.zero, new Vector2(820f, 44f));

            BuildTourMap(card.transform);

            // Compact district list under the map: number, landmark, how far along / ferry crossing on the way
            for (int district = 0; district < GameConfig.DistrictCount; district++)
            {
                int col = district % 2;
                int row = district / 2;
                float x = col == 0 ? 0.26f : 0.74f;
                float y = 0.44f - row * 0.08f;
                var node = MakePanel(card.transform, "District_" + district, ColNavySoft, new Vector2(x, y), new Vector2(x, y), Vector2.zero, new Vector2(460f, 108f));
                _journeyMapDistrictTexts[district] = MakeLabel(node.transform, "Text", "", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
                SetRect(_journeyMapDistrictTexts[district].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(430f, 100f));
            }

            var close = MakeButton(card.transform, "CloseBtn", ColYellow, new Vector2(0.5f, 0.055f), new Vector2(820f, 92f), () => OpenJourneyMap(false));
            var closeText = MakeLabel(close.transform, "Txt", Loc.T("close_btn"), 34, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(closeText.rectTransform);
            _journeyMapModal.SetActive(false);
        }

        public void OpenJourneyMap(bool open)
        {
            SynthAudio.Instance?.PlayClick();
            if (open) DismissTransientToast();
            if (_journeyMapModal == null) return;
            _journeyMapModal.SetActive(open);
            if (open) RefreshJourneyMap();
        }

        void RefreshJourneyMap()
        {
            if (_journeyMapTitleText != null) _journeyMapTitleText.text = Loc.T("journey_map_title");
            if (_journeyMapSubText != null)
            {
                int stop = Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops);
                int district = StopPlanner.DistrictIndex(stop);
                _journeyMapSubText.text = Loc.F("journey_map_next_stop", stop, Loc.DistrictLandmark(district));
            }

            var solved = SaveSystem.Data.solvedStops;
            int currentDistrict = StopPlanner.DistrictIndex(Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops));
            for (int district = 0; district < GameConfig.DistrictCount; district++)
            {
                int first = district * GameConfig.StopsPerDistrict + 1;
                int last = first + GameConfig.StopsPerDistrict - 1;
                int solvedCount = 0;
                for (int i = 0; i < solved.Count; i++)
                    if (solved[i] >= first && solved[i] <= last) solvedCount++;
                string status = solvedCount >= GameConfig.StopsPerDistrict ? Loc.T("journey_map_complete")
                    : district == currentDistrict ? Loc.T("journey_map_current") : Loc.T("journey_map_ahead");
                string ferry = TourRoute.ApproachHasFerry(district) ? (Loc.IsTR ? "  · vapurla" : "  · by ferry") : "";
                _journeyMapDistrictTexts[district].text = string.Format("{0}. {1}{2}\n<size=24>{3}  ·  {4}/{5}</size>",
                    district + 1, Loc.DistrictLandmark(district), ferry, status, solvedCount, GameConfig.StopsPerDistrict);
                _journeyMapDistrictTexts[district].color = district == currentDistrict ? ColYellow : Color.white;
            }
            RefreshTourMap(currentDistrict, SaveSystem.Data.currentStop);
        }

        TourMapGraphic _tourMap;
        RectTransform[] _tourPins;
        Image[] _tourPinFills;
        UiText[] _tourPinTexts;
        RectTransform _tourTaxi;

        /// <summary>The real Istanbul tour (TourRoute) on a stylised map, with district pins and the taxi.</summary>
        void BuildTourMap(Transform card)
        {
            var frame = MakePanel(card, "TourMapFrame", ColCream, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(920f, 540f));
            frame.AddComponent<RectMask2D>();   // the straits run on past the map's edges
            var go = new GameObject("TourMap", typeof(RectTransform), typeof(CanvasRenderer), typeof(TourMapGraphic));
            go.transform.SetParent(frame.transform, false);
            SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(908f, 528f));
            _tourMap = go.GetComponent<TourMapGraphic>();
            _tourMap.raycastTarget = false;
            if (s_discSprite == null) s_discSprite = CreateDiscSprite();

            // Water names, small and soft
            void WaterLabel(string text, float lat, float lon)
            {
                var l = MakeLabel(go.transform, "Water", text, 19, FontStyle.Bold, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
                SetRect(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), _tourMap.MapToLocal(TourRoute.ToKm(new Vector2(lat, lon))), new Vector2(200f, 30f));
                l.raycastTarget = false;
            }
            WaterLabel("MARMARA", 40.9950f, 28.9600f);
            WaterLabel(Loc.IsTR ? "BOĞAZ" : "BOSPHORUS", 41.0300f, 29.0000f);
            WaterLabel(Loc.IsTR ? "HALİÇ" : "GOLDEN HORN", 41.0330f, 28.9490f);
            void LandLabel(string text, float lat, float lon)
            {
                var l = MakeLabel(go.transform, "Land", text, 18, FontStyle.Bold, new Color(0.45f, 0.36f, 0.26f, 0.8f), TextAnchor.MiddleCenter);
                SetRect(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), _tourMap.MapToLocal(TourRoute.ToKm(new Vector2(lat, lon))), new Vector2(240f, 30f));
                l.raycastTarget = false;
            }
            LandLabel(Loc.IsTR ? "AVRUPA YAKASI" : "EUROPEAN SIDE", 41.0470f, 28.9750f);
            LandLabel(Loc.IsTR ? "ANADOLU YAKASI" : "ASIAN SIDE", 41.0300f, 29.0330f);

            int n = GameConfig.DistrictCount;
            _tourPins = new RectTransform[n];
            _tourPinFills = new Image[n];
            _tourPinTexts = new UiText[n];
            for (int d = 0; d < n; d++)
            {
                var ring = MakePanel(go.transform, "Pin_" + d, ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    _tourMap.MapToLocal(TourRoute.ToKm(TourRoute.Districts[d].pin)), new Vector2(44f, 44f));
                ring.GetComponent<Image>().sprite = s_discSprite;
                ring.GetComponent<Image>().raycastTarget = false;
                _tourPins[d] = ring.GetComponent<RectTransform>();
                var fill = MakePanel(ring.transform, "Fill", ColNavySoft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
                fill.GetComponent<Image>().sprite = s_discSprite;
                fill.GetComponent<Image>().raycastTarget = false;
                _tourPinFills[d] = fill.GetComponent<Image>();
                _tourPinTexts[d] = MakeLabel(ring.transform, "Num", (d + 1).ToString(), 21, FontStyle.Bold, ColCream, TextAnchor.MiddleCenter);
                Stretch(_tourPinTexts[d].rectTransform);
                _tourPinTexts[d].raycastTarget = false;
            }

            var taxi = MakePanel(go.transform, "TourTaxi", ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));
            taxi.GetComponent<Image>().sprite = s_discSprite;
            taxi.GetComponent<Image>().raycastTarget = false;
            var taxiFill = MakePanel(taxi.transform, "Fill", ColCream, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            taxiFill.GetComponent<Image>().sprite = s_discSprite;
            taxiFill.GetComponent<Image>().raycastTarget = false;
            MakeIcon(taxi.transform, "Cab", TaxiIconSprite, new Vector2(36f, 27f), Color.white);
            _tourTaxi = taxi.GetComponent<RectTransform>();
        }

        void RefreshTourMap(int currentDistrict, int stop)
        {
            if (_tourMap == null) return;
            bool toured = SaveSystem.Data.campaignComplete;
            int first = currentDistrict * GameConfig.StopsPerDistrict + 1;
            float f = Mathf.Clamp01((float)(Mathf.Clamp(stop, 1, GameConfig.TotalStops) - first) / GameConfig.StopsPerDistrict);
            // In district d the taxi works its way from pin d towards pin d + 1
            float from = _tourMap.DistrictDistance(currentDistrict);
            float to = currentDistrict + 1 < GameConfig.DistrictCount ? _tourMap.DistrictDistance(currentDistrict + 1) : from;
            float km = toured ? _tourMap.TotalKm : Mathf.Lerp(from, to, f);
            _tourMap.ProgressKm = km;
            _tourTaxi.anchoredPosition = _tourMap.MapToLocal(_tourMap.PointAt(km)) + new Vector2(0f, 30f);
            for (int d = 0; d < _tourPins.Length; d++)
            {
                bool done = d < currentDistrict || toured;
                bool current = d == currentDistrict && !toured;
                _tourPinFills[d].color = done ? ColTeal : current ? ColYellow : ColNavySoft;
                _tourPinTexts[d].text = (d + 1).ToString();   // done = teal pin
                _tourPinTexts[d].color = current ? ColTextDark : ColCream;
            }
        }

        void BuildDistrictFinaleModal(Transform root)
        {
            _districtFinaleModal = MakePanel(root, "DistrictFinaleBackdrop", new Color(0.02f, 0.05f, 0.09f, 0.92f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var card = MakePanel(_districtFinaleModal.transform, "DistrictFinaleCard", ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1340f));
            _districtFinaleTitleText = MakeLabel(card.transform, "Title", "", 46, FontStyle.Bold, ColYellow, TextAnchor.MiddleLeft);
            SetRect(_districtFinaleTitleText.rectTransform, new Vector2(0.5f, 0.93f), new Vector2(0.5f, 0.93f), Vector2.zero, new Vector2(820f, 72f));
            _districtFinaleSubText = MakeLabel(card.transform, "Summary", "", 25, FontStyle.Normal, ColCream, TextAnchor.MiddleLeft);
            SetRect(_districtFinaleSubText.rectTransform, new Vector2(0.5f, 0.865f), new Vector2(0.5f, 0.865f), Vector2.zero, new Vector2(820f, 56f));
            var logPanel = MakePanel(card.transform, "AnswerJournal", ColNavySoft, new Vector2(0.5f, 0.49f), new Vector2(0.5f, 0.49f), Vector2.zero, new Vector2(840f, 790f));
            _districtFinaleHeadingText = MakeLabel(logPanel.transform, "JournalHeading", Loc.T("district_finale_log"), 29, FontStyle.Bold, ColTeal, TextAnchor.MiddleLeft);
            SetRect(_districtFinaleHeadingText.rectTransform, new Vector2(0.5f, 0.94f), new Vector2(0.5f, 0.94f), Vector2.zero, new Vector2(760f, 52f));
            var viewport = MakePanel(logPanel.transform, "JournalViewport", ColNavySoft, new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.47f), Vector2.zero, new Vector2(790f, 690f));
            var mask = viewport.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;
            _districtFinaleLogScrollRect = viewport.AddComponent<UnityEngine.UI.ScrollRect>();
            _districtFinaleLogScrollRect.viewport = viewport.GetComponent<RectTransform>();
            _districtFinaleLogScrollRect.horizontal = false;
            _districtFinaleLogScrollRect.vertical = true;
            _districtFinaleLogScrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            _districtFinaleLogScrollRect.scrollSensitivity = 42f;
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _districtFinaleLogContent = content.GetComponent<RectTransform>();
            _districtFinaleLogContent.anchorMin = new Vector2(0f, 1f);
            _districtFinaleLogContent.anchorMax = new Vector2(1f, 1f);
            _districtFinaleLogContent.pivot = new Vector2(0.5f, 1f);
            _districtFinaleLogContent.offsetMin = new Vector2(10f, 0f);
            _districtFinaleLogContent.offsetMax = new Vector2(-10f, 0f);
            var layout = content.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(8, 8, 10, 10);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var sizeFitter = content.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            sizeFitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            _districtFinaleLogScrollRect.content = _districtFinaleLogContent;

            var continueButton = MakeButton(card.transform, "ContinueBtn", ColYellow, new Vector2(0.5f, 0.075f), new Vector2(820f, 96f), ContinueAfterDistrictFinale);
            _districtFinaleContinueText = MakeLabel(continueButton.transform, "Text", Loc.T("district_finale_continue"), 30, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_districtFinaleContinueText.rectTransform);
            _districtFinaleModal.SetActive(false);
        }

        public void ShowDistrictFinale(int district)
        {
            if (_districtFinaleModal == null) return;
            DismissTransientToast();
            _activeDistrictFinale = Mathf.Clamp(district, 0, GameConfig.DistrictCount - 1);
            RefreshDistrictFinale();
            _districtFinaleModal.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if (_districtFinaleLogScrollRect != null) _districtFinaleLogScrollRect.verticalNormalizedPosition = 1f;
        }

        public void OpenDistrictFinale(bool open)
        {
            if (_districtFinaleModal != null) _districtFinaleModal.SetActive(open);
        }

        void ContinueAfterDistrictFinale()
        {
            SynthAudio.Instance?.PlayClick();
            _gm.ContinueAfterDistrictFinale();
        }

        void RefreshDistrictFinale()
        {
            if (_activeDistrictFinale < 0 || _districtFinaleTitleText == null || _districtFinaleLogContent == null) return;
            var entries = SaveSystem.Data.districtAnswerLog.FindAll(entry => entry != null && (entry.stop - 1) / GameConfig.StopsPerDistrict == _activeDistrictFinale);
            _districtFinaleTitleText.text = Loc.F("district_finale_title", Loc.DistrictLandmark(_activeDistrictFinale));
            _districtFinaleSubText.text = Loc.F("district_finale_sub", entries.Count);
            for (int i = _districtFinaleLogContent.childCount - 1; i >= 0; i--)
                Destroy(_districtFinaleLogContent.GetChild(i).gameObject);

            if (entries.Count == 0)
            {
                AddDistrictAnswerJournalRow(Loc.T("district_finale_no_answers"), 76f);
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    string prompt = Loc.Pick(entry.promptTR, entry.promptEN);
                    string answer = Loc.Pick(entry.answerTR, entry.answerEN);
                    var category = (PuzzleCategory)Mathf.Clamp(entry.category, 0, Enum.GetValues(typeof(PuzzleCategory)).Length - 1);
                    string rowText = string.Format("{0} · {1}\n{2}\n→ {3}", entry.stop, Loc.T("category_" + category), prompt, answer);
                    AddDistrictAnswerJournalRow(rowText, 216f);
                }
            }

            Canvas.ForceUpdateCanvases();
            _districtFinaleLogScrollRect.verticalNormalizedPosition = 1f;
        }

        void AddDistrictAnswerJournalRow(string content, float rowHeight)
        {
            var row = new GameObject("AnswerEntry", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            row.transform.SetParent(_districtFinaleLogContent, false);
            var image = row.GetComponent<UnityEngine.UI.Image>();
            image.color = ColNavy;
            image.raycastTarget = false;
            var layout = row.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = rowHeight;
            layout.preferredHeight = rowHeight;
            var label = MakeLabel(row.transform, "Text", content, 28, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            float readableHeight = Mathf.Max(rowHeight, label.GetPreferredValues(content, 860f, Mathf.Infinity).y + 24f);
            layout.minHeight = layout.preferredHeight = readableHeight;
        }

        void BuildTaxiShopModal(Transform root)
        {
            _taxiShopModal = MakePanel(root, "TaxiShopBackdrop", new Color(0.02f, 0.05f, 0.09f, 0.90f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var card = MakePanel(_taxiShopModal.transform, "TaxiShopCard", ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1340f));
            _taxiShopTitleText = MakeLabel(card.transform, "Title", Loc.T("taxi_shop_title"), 48, FontStyle.Bold, ColYellow, TextAnchor.MiddleLeft);
            SetRect(_taxiShopTitleText.rectTransform, new Vector2(0.5f, 0.94f), new Vector2(0.5f, 0.94f), Vector2.zero, new Vector2(820f, 64f));
            _taxiShopSubText = MakeLabel(card.transform, "Sub", Loc.T("taxi_shop_sub"), 23, FontStyle.Normal, ColCream, TextAnchor.MiddleLeft);
            SetRect(_taxiShopSubText.rectTransform, new Vector2(0.5f, 0.885f), new Vector2(0.5f, 0.885f), Vector2.zero, new Vector2(820f, 44f));
            var wallet = MakePanel(card.transform, "Wallet", ColNavySoft, new Vector2(0.5f, 0.81f), new Vector2(0.5f, 0.81f), Vector2.zero, new Vector2(820f, 62f));
            _taxiShopCoinsText = MakeLabel(wallet.transform, "Coins", "", 25, FontStyle.Bold, ColYellow, TextAnchor.MiddleCenter);
            Stretch(_taxiShopCoinsText.rectTransform);

            for (int id = 0; id < TaxiCosmeticCatalog.Count; id++)
            {
                int cosmeticId = id;
                int col = id % 2;
                int row = id / 2;
                float x = col == 0 ? 0.28f : 0.72f;
                float y = 0.705f - row * 0.155f;
                var option = MakeButton(card.transform, "TaxiCosmetic_" + id, ColNavySoft, new Vector2(x, y), new Vector2(424f, 210f), () => SelectTaxiCosmetic(cosmeticId));
                var swatch = MakePanel(option.transform, "ColorSwatch", TaxiCosmeticCatalog.Swatch(id), new Vector2(0.20f, 0.5f), new Vector2(0.20f, 0.5f), Vector2.zero, new Vector2(96f, 96f));
                _taxiShopSwatches[id] = swatch.GetComponent<Image>();
                _taxiShopOptionTexts[id] = MakeLabel(option.transform, "Text", "", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
                SetRect(_taxiShopOptionTexts[id].rectTransform, new Vector2(0.66f, 0.5f), new Vector2(0.66f, 0.5f), Vector2.zero, new Vector2(260f, 180f));
            }

            var close = MakeButton(card.transform, "CloseBtn", ColYellow, new Vector2(0.5f, 0.055f), new Vector2(820f, 92f), () => OpenTaxiShop(false));
            var closeText = MakeLabel(close.transform, "Txt", Loc.T("close_btn"), 34, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(closeText.rectTransform);
            _taxiShopModal.SetActive(false);
        }

        public void OpenTaxiShop(bool open)
        {
            SynthAudio.Instance?.PlayClick();
            if (open) DismissTransientToast();
            if (_taxiShopModal == null) return;
            _taxiShopModal.SetActive(open);
            if (open) RefreshTaxiShop();
        }

        void RefreshTaxiShop()
        {
            if (_taxiShopTitleText != null) _taxiShopTitleText.text = Loc.T("taxi_shop_title");
            if (_taxiShopSubText != null) _taxiShopSubText.text = Loc.T("taxi_shop_sub");
            if (_taxiShopCoinsText != null) _taxiShopCoinsText.text = Loc.F("taxi_shop_coins", SaveSystem.Data.coins);
            for (int id = 0; id < TaxiCosmeticCatalog.Count; id++)
            {
                bool unlocked = SaveSystem.IsTaxiCosmeticUnlocked(id);
                bool equipped = SaveSystem.Data.equippedTaxiCosmetic == id;
                string state = equipped ? Loc.T("taxi_shop_equipped")
                    : unlocked ? Loc.T("taxi_shop_equip")
                    : Loc.F("taxi_shop_unlock", TaxiCosmeticCatalog.Cost(id));
                _taxiShopOptionTexts[id].text = TaxiCosmeticCatalog.Name(id) + "\n" + state;
                _taxiShopSwatches[id].color = TaxiCosmeticCatalog.Swatch(id);
            }
        }

        void SelectTaxiCosmetic(int id)
        {
            if (!SaveSystem.TryPurchaseOrEquipTaxiCosmetic(id))
            {
                ShowToast(Loc.T("no_coins"));
                return;
            }
            _gm.Taxi?.ApplyTaxiCosmetic(id);
            RefreshTaxiShop();
            ShowToast(Loc.T("taxi_cosmetic_equipped_toast"));
            SynthAudio.Instance?.PlayClick();
        }

        void BuildChatModal(Transform root)
        {
            var backdrop = MakePanel(root, "ChatModalBackdrop", new Color(0.02f, 0.05f, 0.09f, 0.88f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _chatModal = backdrop;

            var card = MakePanel(backdrop.transform, "ChatCard", ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1340f));

            _chatTitleText = MakeLabel(card.transform, "Title", Loc.T("chat_title"), 48, FontStyle.Bold, new Color(0.24f, 0.78f, 0.56f, 1f), TextAnchor.MiddleLeft);
            SetRect(_chatTitleText.rectTransform, new Vector2(0.5f, 0.93f), new Vector2(0.5f, 0.93f), Vector2.zero, new Vector2(820f, 64f));

            _chatSubText = MakeLabel(card.transform, "Sub", Loc.T("chat_sub"), 24, FontStyle.Normal, ColCream, TextAnchor.MiddleLeft);
            SetRect(_chatSubText.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f), Vector2.zero, new Vector2(820f, 44f));

            var statusPill = MakePanel(card.transform, "StatusPill", ColNavySoft, new Vector2(0.5f, 0.80f), new Vector2(0.5f, 0.80f), Vector2.zero, new Vector2(820f, 68f));
            _chatSpeakerText = MakeLabel(statusPill.transform, "StTxt", "", 26, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            Stretch(_chatSpeakerText.rectTransform);

            var dialogueBox = MakePanel(card.transform, "DialogueBox", ColNavySoft, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(820f, 490f));
            _chatDialogueText = MakeLabel(dialogueBox.transform, "DialogueTxt", "", 28, FontStyle.Normal, Color.white, TextAnchor.UpperLeft);
            SetRect(_chatDialogueText.rectTransform, new Vector2(0.5f, 0.43f), new Vector2(0.5f, 0.43f), Vector2.zero, new Vector2(740f, 350f));
            _chatChapterText = MakeLabel(dialogueBox.transform, "ChapterTxt", "", 22, FontStyle.Bold, ColYellow, TextAnchor.MiddleLeft);
            SetRect(_chatChapterText.rectTransform, new Vector2(0.5f, 0.94f), new Vector2(0.5f, 0.94f), Vector2.zero, new Vector2(740f, 48f));

            var nextChatBtn = MakeButton(card.transform, "NextChatBtn", ColTeal, new Vector2(0.5f, 0.20f), new Vector2(820f, 96f), AdvancePassengerStory);
            _chatNextButtonText = MakeLabel(nextChatBtn.transform, "Txt", "", 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_chatNextButtonText.rectTransform);

            var closeBtn = MakeButton(card.transform, "CloseBtn", ColYellow, new Vector2(0.5f, 0.055f), new Vector2(820f, 96f), () => OpenChatModal(false));
            var closeTxt = MakeLabel(closeBtn.transform, "Txt", Loc.T("close_btn"), 34, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(closeTxt.rectTransform);

            _chatModal.SetActive(false);
        }

        void RefreshPassengerStory()
        {
            if (_chatSpeakerText != null)
                _chatSpeakerText.text = "●  " + Loc.Upper(PassengerCatalog.Get(_chatPassengerId).DisplayName);
            int unlocked = SaveSystem.GetPassengerStoryChapters(_chatPassengerId);
            _currentChatIndex = Mathf.Clamp(_currentChatIndex, 0, unlocked - 1);
            if (_chatDialogueText != null)
                _chatDialogueText.text = PassengerStoryCatalog.Get(_chatPassengerId, _currentChatIndex);
            if (_chatChapterText != null)
                _chatChapterText.text = string.Format(Loc.T("story_chapter"), _currentChatIndex + 1, PassengerStoryCatalog.ChapterCount);
            if (_chatNextButtonText != null)
            {
                if (unlocked >= PassengerStoryCatalog.ChapterCount)
                    _chatNextButtonText.text = Loc.T("story_complete");
                else if (_currentChatIndex < unlocked - 1)
                    _chatNextButtonText.text = Loc.T("story_next");
                else
                    _chatNextButtonText.text = string.Format(Loc.T("story_unlock"), PassengerStoryCatalog.ChapterUnlockCost);
            }
        }

        void AdvancePassengerStory()
        {
            int unlocked = SaveSystem.GetPassengerStoryChapters(_chatPassengerId);
            if (_currentChatIndex < unlocked - 1)
                _currentChatIndex++;
            else if (unlocked < PassengerStoryCatalog.ChapterCount)
            {
                if (!SaveSystem.TryUnlockPassengerStoryChapter(_chatPassengerId, PassengerStoryCatalog.ChapterUnlockCost))
                {
                    ShowToast(Loc.T("no_coins"));
                    return;
                }
                _currentChatIndex++;
                ShowToast(Loc.T("story_unlocked_toast"));
            }
            RefreshPassengerStory();
            SynthAudio.Instance?.PlayClick();
        }

        public void OpenChatModal(bool open)
        {
            SynthAudio.Instance?.PlayClick();
            if (open) DismissTransientToast();
            if (_chatModal != null)
            {
                _chatModal.SetActive(open);
                if (open)
                {
                    _chatPassengerId = PassengerCatalog.ForStop(SaveSystem.Data.currentStop).id;
                    _currentChatIndex = 0;
                    RefreshPassengerStory();
                }
            }
        }
    }
}
