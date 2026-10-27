using UnityEngine;
using UnityEngine.UI;

namespace OddsCab
{
    public partial class UiDirector
    {
        GameObject _ferryBanner, _ferrySkipButton;
        UiText _ferryTitleText, _ferrySubText, _ferrySkipText;

        /// <summary>Banner under the HUD while the taxi crosses on the car ferry: pier to pier, the district ahead, skip.</summary>
        void BuildFerryBanner(Transform root)
        {
            _ferryBanner = MakePanel(root, "FerryBanner", new Color(0.04f, 0.09f, 0.16f, 0.88f), new Vector2(0.5f, 0.80f), new Vector2(0.5f, 0.80f),
                Vector2.zero, new Vector2(900f, 190f));
            var stripe = MakePanel(_ferryBanner.transform, "Stripe", ColTeal, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(900f, 8f));
            stripe.GetComponent<Image>().raycastTarget = false;
            _ferryTitleText = MakeLabel(_ferryBanner.transform, "Title", "", 40, FontStyle.Bold, ColYellow, TextAnchor.MiddleCenter);
            SetRect(_ferryTitleText.rectTransform, new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.70f), Vector2.zero, new Vector2(860f, 60f));
            _ferrySubText = MakeLabel(_ferryBanner.transform, "Sub", "", 27, FontStyle.Bold, ColCream, TextAnchor.MiddleCenter);
            SetRect(_ferrySubText.rectTransform, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(860f, 46f));

            var skip = MakeButton(root, "FerrySkipBtn", ColCream, new Vector2(0.5f, 0.08f), new Vector2(420f, 96f), () => _gm.SkipFerry());
            _ferrySkipText = MakeLabel(skip.transform, "Txt", "", 32, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_ferrySkipText.rectTransform);
            _ferrySkipButton = skip.gameObject;
            _ferrySkipButton.SetActive(false);
            _ferryBanner.SetActive(false);
        }

        public void ShowFerryBanner(bool show, int district)
        {
            if (_ferryBanner == null) return;
            _ferryBanner.SetActive(show);
            _ferrySkipButton.SetActive(show);
            if (!show) return;
            bool fromAsia = TourRoute.FerryFromAsia(district);
            string harem = "Harem", sirkeci = "Sirkeci";
            _ferryTitleText.text = (Loc.IsTR ? "ARABA VAPURU" : "CAR FERRY") + "  ·  " + (fromAsia ? harem + " → " + sirkeci : sirkeci + " → " + harem);
            _ferrySubText.text = (Loc.IsTR ? "Sıradaki semt: " : "Next district: ") + Loc.DistrictLandmark(district);
            _ferrySkipText.text = Loc.IsTR ? "►  KARŞIYA GEÇ" : "►  SKIP CROSSING";
        }
    }
}
