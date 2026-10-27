using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OddsCab
{
    /// <summary>Reusable unscaled entrance motion. Activation never builds a tween or captures a delegate.</summary>
    public sealed class UiPopIn : MonoBehaviour
    {
        public float FromScale = 0.94f;
        public float Duration = 0.28f;
        CanvasGroup _group;
        Vector3 _rest;
        float _elapsed;
        bool _ready, _animating;

        void Awake()
        {
            if (_ready) return;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _rest = transform.localScale;
            _ready = true;
        }

        void OnEnable()
        {
            Awake();
            _elapsed = 0f;
            _animating = true;
            transform.localScale = _rest * FromScale;
            _group.alpha = 0f;
        }

        void Update()
        {
            if (!_animating) return;
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, Duration));
            float u = t - 1f;
            float spring = 1f + 2.4f * u * u * u + 1.4f * u * u;
            transform.localScale = _rest * Mathf.LerpUnclamped(FromScale, 1f, spring);
            _group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.8f));
            if (t >= 1f) { _animating = false; transform.localScale = _rest; _group.alpha = 1f; }
        }

        void OnDisable()
        {
            if (!_ready) return;
            _animating = false;
            transform.localScale = _rest;
            _group.alpha = 1f;
        }
    }

    /// <summary>Touch/keyboard feedback with an allocation-free damped spring and separate tactile audio.</summary>
    public sealed class UiPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        Vector3 _rest;
        UnityEngine.UI.Selectable _selectable;
        float _value = 1f, _velocity, _target = 1f;
        bool _down, _hover, _ready;

        void Awake()
        {
            _rest = transform.localScale;
            _selectable = GetComponent<UnityEngine.UI.Selectable>();
            _ready = true;
        }

        bool CanInteract => _selectable == null || _selectable.IsInteractable();

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !CanInteract) return;
            _down = true;
            _target = 0.945f;
            SynthAudio.Instance?.PlayUiPress();
        }
        public void OnPointerUp(PointerEventData e) { if (_down) Release(); }
        public void OnPointerEnter(PointerEventData e) => Hover();
        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            if (_down) Release();
            else _target = 1f;
        }
        public void OnSelect(BaseEventData e) => Hover();
        public void OnDeselect(BaseEventData e) { _hover = false; _target = 1f; }

        void Hover()
        {
            if (!CanInteract) return;
            if (!_hover) SynthAudio.Instance?.PlayUiHover();
            _hover = true;
            if (!_down) _target = 1.018f;
        }
        void Release()
        {
            _down = false;
            _target = _hover && CanInteract ? 1.018f : 1f;
            _velocity = Mathf.Max(_velocity, 0.9f);
        }
        void Update()
        {
            if (Mathf.Abs(_value - _target) < 0.0001f && Mathf.Abs(_velocity) < 0.001f) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            int count = Mathf.Max(1, Mathf.CeilToInt(dt * 120f));
            float step = dt / count;
            for (int i = 0; i < count; i++)
            {
                _velocity += ((_target - _value) * 360f - _velocity * 24f) * step;
                _value += _velocity * step;
            }
            transform.localScale = _rest * _value;
        }
        void OnDisable()
        {
            if (!_ready) return;
            transform.localScale = _rest;
            _value = _target = 1f;
            _velocity = 0f;
            _down = _hover = false;
        }
    }

    public partial class UiDirector
    {
        RectTransform _hudCoinBadge;
        RectTransform _rewardCoinsIcon;
        Sprite _flyCoinSprite;
        int _coinShown = -1;
        const int RewardFlyerCapacity = 10;
        const float CoinFlightDuration = 0.62f;
        struct RewardFlyer
        {
            public RectTransform rect;
            public Vector3 from, control;
            public float delay;
            public bool arrived;
        }
        readonly RewardFlyer[] _rewardFlyers = new RewardFlyer[RewardFlyerCapacity];
        int _rewardFlyerCount, _rewardFrom, _rewardTotal;
        float _rewardStarted, _coinBadgePulse;
        bool _rewardAnimating;
        Vector3 _coinBadgeRestScale = Vector3.one;

        void SetupUiTweens()
        {
            DOTween.Init(recycleAllByDefault: false, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(240, 48);
            foreach (var panel in new[] { _puzzlePanel, _resultBox, _stationArrivalCard, _pipReactionCard, _toastBox })
                if (panel != null && panel.GetComponent<UiPopIn>() == null)
                {
                    var pop = panel.AddComponent<UiPopIn>();
                    if (panel == _toastBox || panel == _pipReactionCard) { pop.FromScale = 0.92f; pop.Duration = 0.22f; }
                }
            _flyCoinSprite = CreateFlyCoinSprite();
            for (int i = 0; i < RewardFlyerCapacity; i++)
            {
                var go = new GameObject("RewardCoinPool_" + i, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(_portraitRoot, false);
                var image = go.GetComponent<UnityEngine.UI.Image>();
                image.sprite = _flyCoinSprite;
                image.raycastTarget = false;
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(56f, 56f);
                _rewardFlyers[i].rect = rect;
                go.SetActive(false);
            }
            if (_hudCoinBadge != null) _coinBadgeRestScale = _hudCoinBadge.localScale;
        }

        void PlayRewardTweens(int coinsEarned)
        {
            StopRewardJuice();
            if (coinsEarned <= 0 || _hudCoinBadge == null || _rewardCoinsIcon == null) return;
            _rewardTotal = SaveSystem.Data.coins;
            _rewardFrom = Mathf.Max(0, _rewardTotal - coinsEarned);
            _coinShown = _rewardFrom;
            _rewardFlyerCount = Mathf.Clamp(coinsEarned / 2, 5, RewardFlyerCapacity);
            _rewardStarted = Time.unscaledTime;
            _rewardAnimating = true;
            for (int i = 0; i < _rewardFlyerCount; i++)
            {
                ref RewardFlyer flyer = ref _rewardFlyers[i];
                flyer.from = _rewardCoinsIcon.position;
                flyer.from.x += Random.Range(-24f, 24f) * _portraitRoot.lossyScale.x;
                flyer.control = Vector3.Lerp(flyer.from, _hudCoinBadge.position, 0.5f);
                flyer.control.x += Random.Range(-170f, 170f) * _portraitRoot.lossyScale.x;
                flyer.control.y += Random.Range(60f, 180f) * _portraitRoot.lossyScale.y;
                flyer.delay = 0.28f + i * 0.055f;
                flyer.arrived = false;
                flyer.rect.position = flyer.from;
                flyer.rect.localScale = Vector3.zero;
                flyer.rect.gameObject.SetActive(true);
                flyer.rect.SetAsLastSibling();
            }
        }

        void StopRewardJuice()
        {
            _rewardAnimating = false;
            _coinShown = -1;
            for (int i = 0; i < _rewardFlyerCount; i++)
                if (_rewardFlyers[i].rect != null) _rewardFlyers[i].rect.gameObject.SetActive(false);
            _rewardFlyerCount = 0;
            if (_hudCoinBadge != null) _hudCoinBadge.localScale = _coinBadgeRestScale;
            _coinBadgePulse = 0f;
        }

        void UpdateRewardJuice()
        {
            if (!_rewardAnimating) return;
            if (_gm.State != GameState.PuzzleResult) { StopRewardJuice(); return; }
            float elapsed = Time.unscaledTime - _rewardStarted;
            bool pending = false;
            for (int i = 0; i < _rewardFlyerCount; i++)
            {
                ref RewardFlyer flyer = ref _rewardFlyers[i];
                if (flyer.arrived) continue;
                float t = (elapsed - flyer.delay) / CoinFlightDuration;
                if (t < 0f) { pending = true; continue; }
                if (t >= 1f)
                {
                    flyer.arrived = true;
                    flyer.rect.gameObject.SetActive(false);
                    _coinBadgePulse = 1f;
                    SynthAudio.Instance?.PlayCoinCollect(i);
                    continue;
                }
                pending = true;
                float eased = t * t;
                float u = 1f - eased;
                flyer.rect.position = u * u * flyer.from + 2f * u * eased * flyer.control
                    + eased * eased * _hudCoinBadge.position;
                flyer.rect.localScale = Vector3.one * (Mathf.Lerp(1.05f, 0.52f, t) * Mathf.Clamp01(t * 10f));
                flyer.rect.localRotation = Quaternion.Euler(0f, 0f, t * 330f + i * 27f);
            }
            float duration = 0.28f + (_rewardFlyerCount - 1) * 0.055f + CoinFlightDuration;
            float counter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _coinShown = Mathf.RoundToInt(Mathf.Lerp(_rewardFrom, _rewardTotal, counter));
            _coinBadgePulse = Mathf.Max(0f, _coinBadgePulse - Time.unscaledDeltaTime * 5f);
            _hudCoinBadge.localScale = _coinBadgeRestScale * (1f + Mathf.Sin(_coinBadgePulse * Mathf.PI) * 0.18f);
            if (!pending && _coinBadgePulse <= 0f) StopRewardJuice();
        }
        static Sprite CreateFlyCoinSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FlyCoin" };
            var gold = new Color(1f, 0.80f, 0.20f); var rim = new Color(0.80f, 0.52f, 0.06f); var hi = new Color(1f, 0.95f, 0.70f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - s * 0.5f, dy = y + 0.5f - s * 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) / (s * 0.5f);
                    float a = Mathf.Clamp01((1f - r) * s * 0.5f);
                    Color c = r > 0.80f ? rim : (r > 0.62f && r < 0.70f ? rim : gold);
                    float h = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(-9f, 10f)) / 14f);
                    c = Color.Lerp(c, hi, h * 0.8f);
                    c.a = a;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
