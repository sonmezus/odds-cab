using UnityEngine;

namespace OddsCab
{
    public partial class UiDirector
    {
        GameObject _districtLoading;
        RectTransform _districtLoadingIndicator;
        UiText _districtLoadingLabel;

        void BuildDistrictLoading(Transform parent)
        {
            _districtLoading = MakePanel(parent, "DistrictArrival", ColNavy,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _districtLoading.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            _districtLoadingLabel = MakeLabel(_districtLoading.transform, "ArrivalTitle", "", 40,
                FontStyle.Bold, ColCream, TextAnchor.MiddleCenter);
            StyleBody(_districtLoadingLabel, ColCream);
            SetRect(_districtLoadingLabel.rectTransform, new Vector2(.5f, .47f),
                new Vector2(.5f, .47f), Vector2.zero, new Vector2(880f, 180f));
            var indicator = MakePanel(_districtLoading.transform, "ArrivalIndicator", ColYellow,
                new Vector2(.5f, .57f), new Vector2(.5f, .57f), Vector2.zero, new Vector2(42f, 42f));
            _districtLoadingIndicator = indicator.GetComponent<RectTransform>();
            _districtLoading.SetActive(false);
        }

        public void ShowDistrictLoading(bool visible)
        {
            if (_districtLoading == null) return;
            if (visible)
            {
                _districtLoadingLabel.text = Loc.IsTR ? "Yeni semte varıyoruz…" : "Arriving in the next district…";
                _districtLoading.transform.SetAsLastSibling();
            }
            _districtLoading.SetActive(visible);
        }

        void TickDistrictLoading()
        {
            if (_districtLoadingIndicator == null || !_districtLoading.activeSelf) return;
            _districtLoadingIndicator.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 100f);
        }
    }
}
