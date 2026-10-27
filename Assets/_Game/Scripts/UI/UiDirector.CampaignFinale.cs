using UnityEngine;
using UnityEngine.UI;

namespace OddsCab
{
    public partial class UiDirector
    {
        GameObject _campaignFinaleModal;
        UiText _campaignTitleText, _campaignSubText, _campaignStatsText, _campaignThanksText, _campaignButtonText;
        readonly Image[] _campaignPortraits = new Image[5];
        RectTransform _campaignConfetti;

        /// <summary>The end of the 512-stop journey: a celebration card with the run's numbers and all five passengers.</summary>
        void EnsureCampaignFinaleModal()
        {
            if (_campaignFinaleModal != null) return;
            _campaignFinaleModal = MakePanel(_portraitRoot, "CampaignFinaleBackdrop", new Color(0.02f, 0.05f, 0.09f, 0.94f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var card = MakePanel(_campaignFinaleModal.transform, "CampaignFinaleCard", ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MobileUiLayout.PanelWidth, 1540f));

            var confettiGo = new GameObject("CampaignConfetti", typeof(RectTransform));
            confettiGo.transform.SetParent(card.transform, false);
            _campaignConfetti = confettiGo.GetComponent<RectTransform>();
            Stretch(_campaignConfetti);
            var rng = new System.Random(512);
            for (int i = 0; i < 36; i++)
            {
                Color col = (i % 3) switch { 0 => ColYellow, 1 => ColTeal, _ => new Color(0.93f, 0.40f, 0.36f, 1f) };
                var chip = MakePanel(_campaignConfetti, "Confetti_" + i, col, new Vector2((float)rng.NextDouble(), 0.93f + (float)rng.NextDouble() * 0.06f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f + rng.Next(14), 9f));
                var rt = chip.GetComponent<RectTransform>();
                rt.anchorMax = rt.anchorMin;
                rt.localRotation = Quaternion.Euler(0f, 0f, rng.Next(-80, 80));
                chip.GetComponent<Image>().raycastTarget = false;
            }

            _campaignTitleText = MakeLabel(card.transform, "Title", "", 58, FontStyle.Bold, ColYellow, TextAnchor.MiddleCenter);
            SetRect(_campaignTitleText.rectTransform, new Vector2(0.5f, 0.885f), new Vector2(0.5f, 0.885f), Vector2.zero, new Vector2(880f, 90f));
            _campaignSubText = MakeLabel(card.transform, "Sub", "", 28, FontStyle.Normal, ColCream, TextAnchor.MiddleCenter);
            SetRect(_campaignSubText.rectTransform, new Vector2(0.5f, 0.815f), new Vector2(0.5f, 0.815f), Vector2.zero, new Vector2(840f, 90f));

            // Five passengers, smiling, in a row
            for (int i = 0; i < _campaignPortraits.Length; i++)
            {
                float x = 0.14f + i * 0.18f;
                var frame = MakePanel(card.transform, "PassengerFrame_" + i, ColCream, new Vector2(x, 0.66f), new Vector2(x, 0.66f), Vector2.zero, new Vector2(160f, 160f));
                var img = MakePanel(frame.transform, "Portrait", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
                _campaignPortraits[i] = img.GetComponent<Image>();
            }

            var statsBox = MakePanel(card.transform, "Stats", ColNavySoft, new Vector2(0.5f, 0.405f), new Vector2(0.5f, 0.405f), Vector2.zero, new Vector2(840f, 420f));
            _campaignStatsText = MakeLabel(statsBox.transform, "StatsText", "", 38, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_campaignStatsText.rectTransform, 24f);

            _campaignThanksText = MakeLabel(card.transform, "Thanks", "", 26, FontStyle.Italic, ColTeal, TextAnchor.MiddleCenter);
            SetRect(_campaignThanksText.rectTransform, new Vector2(0.5f, 0.185f), new Vector2(0.5f, 0.185f), Vector2.zero, new Vector2(840f, 80f));

            var button = MakeButton(card.transform, "CampaignMenuBtn", ColYellow, new Vector2(0.5f, 0.075f), new Vector2(840f, 110f), () => OpenCampaignFinale(false));
            _campaignButtonText = MakeLabel(button.transform, "Text", "", 36, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_campaignButtonText.rectTransform);
            _campaignFinaleModal.SetActive(false);
        }

        public void ShowCampaignFinale()
        {
            DismissTransientToast();
            EnsureCampaignFinaleModal();
            var d = SaveSystem.Data;
            _campaignTitleText.text = Loc.T("campaign_title");
            _campaignSubText.text = Loc.T("campaign_sub");
            _campaignStatsText.text = Loc.F("campaign_stats", d.totalCorrect, d.totalWrong, d.bestStreak, d.coins, d.campaignsCompleted);
            _campaignThanksText.text = Loc.T("campaign_thanks");
            _campaignButtonText.text = Loc.T("campaign_menu");
            for (int i = 0; i < _campaignPortraits.Length && i < PassengerCatalog.All.Length; i++)
            {
                var sprites = GetOrRenderExpressionPortraits(PassengerCatalog.All[i]);
                if (sprites != null && sprites.Length > (int)PassengerExpression.Satisfied)
                    _campaignPortraits[i].sprite = sprites[(int)PassengerExpression.Satisfied];
            }
            _campaignFinaleModal.transform.SetAsLastSibling();
            _campaignFinaleModal.SetActive(true);
            SynthAudio.Instance?.PlayCorrect();
            VfxDirector.Instance?.PlaySuccess(_gm.Taxi != null ? _gm.Taxi.transform.position : Vector3.zero);
        }

        public void OpenCampaignFinale(bool open)
        {
            SynthAudio.Instance?.PlayClick();
            if (_campaignFinaleModal != null) _campaignFinaleModal.SetActive(open);
            if (!open) RefreshForState();
        }

        void AnimateCampaignConfetti()
        {
            if (_campaignConfetti == null || !_campaignFinaleModal.activeSelf) return;
            for (int i = 0; i < _campaignConfetti.childCount; i++)
            {
                var rt = (RectTransform)_campaignConfetti.GetChild(i);
                var a = rt.anchorMin;
                a.y -= Time.unscaledDeltaTime * (0.06f + (i % 5) * 0.012f);
                // Confetti rains down the side margins only, so it never crosses the title or stats
                if (a.x > 0.12f && a.x < 0.88f) a.x = a.x < 0.5f ? a.x * 0.12f / 0.5f : 0.88f + (a.x - 0.5f) * 0.24f;
                if (a.y < 0.04f) a.y = 0.99f;
                rt.anchorMin = rt.anchorMax = a;
                rt.localRotation = Quaternion.Euler(0f, 0f, rt.localEulerAngles.z + Time.unscaledDeltaTime * (60f + i * 7f));
            }
        }
    }
}
