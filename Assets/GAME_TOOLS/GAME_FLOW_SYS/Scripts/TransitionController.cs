using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace CatzTools
{
    #region 轉場控制器
    /// <summary>
    /// 轉場控制器 — 掛在 FlowManager 場景的 TransitionCanvas 上。
    /// 提供 UI 遮罩預設轉場（Fade / Slide）和自訂 Shader 轉場。
    /// FlowManager 建立時由 Editor 自動組裝，Runtime 不需手動設定。
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    public class TransitionController : MonoBehaviour
    {
        #region 序列化欄位
        [Header("遮罩元件")]
        /// <summary>全螢幕遮罩 Image（UI 轉場用）</summary>
        [SerializeField] private Image _overlay;

        [Header("Shader 轉場")]
        /// <summary>全螢幕遮罩 Image（Shader 轉場用）</summary>
        [SerializeField] private Image _shaderOverlay;

        [Header("預設設定")]
        /// <summary>預設轉場時長</summary>
        [SerializeField] private float _defaultDuration = 0.5f;

        /// <summary>預設遮罩顏色</summary>
        [SerializeField] private Color _defaultColor = Color.black;
        #endregion 序列化欄位

        #region 私有變數
        private RectTransform _overlayRect;
        private RectTransform _shaderOverlayRect;
        private bool _isPlaying;
        private Texture2D _capturedScreen;
        #endregion 私有變數

        #region Lazy Loading
        private RectTransform OverlayRect =>
            _overlayRect != null ? _overlayRect : (_overlayRect = _overlay.GetComponent<RectTransform>());

        private RectTransform ShaderOverlayRect =>
            _shaderOverlayRect != null ? _shaderOverlayRect : (_shaderOverlayRect = _shaderOverlay.GetComponent<RectTransform>());
        #endregion Lazy Loading

        #region 公開屬性
        /// <summary>是否正在播放轉場</summary>
        public bool IsPlaying => _isPlaying;
        #endregion 公開屬性

        #region Unity 生命週期
        private void Awake()
        {
            // 遮罩初始為全黑覆蓋（FlowManager 開場就是黑幕）
            if (_overlay != null)
            {
                _overlay.color = new Color(_defaultColor.r, _defaultColor.g, _defaultColor.b, 1f);
                _overlay.raycastTarget = true;
                _overlay.gameObject.SetActive(true);
                OverlayRect.anchoredPosition = Vector2.zero;
            }

            if (_shaderOverlay != null)
            {
                _shaderOverlay.gameObject.SetActive(false);
            }
        }
        #endregion Unity 生命週期

        #region 轉場播放
        /// <summary>
        /// 播放轉場效果（淡出 → 等待場景切換 → 淡入）
        /// </summary>
        public async Task PlayTransitionOut(TransitionSettings settings)
        {
            if (_isPlaying) return;
            _isPlaying = true;

            var s = settings ?? new TransitionSettings();
            if (s.type == TransitionType.None)
            {
                _isPlaying = false;
                return;
            }

            float duration = s.duration > 0 ? s.duration : _defaultDuration;
            var type = s.type;

            if (type == TransitionType.CustomShader && s.customMaterial != null)
            {
                ApplyShaderOverrides(s.customMaterial, s);
                await PlayShaderOut(s.customMaterial, duration);
            }
            else
            {
                await PlayUIOut(type, s.color, duration);
            }
        }

        /// <summary>
        /// 播放轉場淡入（場景已載入後呼叫）
        /// </summary>
        public async Task PlayTransitionIn(TransitionSettings settings)
        {
            var s = settings ?? new TransitionSettings();
            if (s.type == TransitionType.None) { _isPlaying = false; return; }

            float duration = s.duration > 0 ? s.duration : _defaultDuration;
            var type = s.type;

            if (type == TransitionType.CustomShader && s.customMaterial != null)
            {
                ApplyShaderOverrides(s.customMaterial, s);
                await PlayShaderIn(s.customMaterial, duration);
            }
            else
            {
                await PlayUIIn(type, s.color, duration);
            }

            _isPlaying = false;
        }
        #endregion 轉場播放

        #region UI 轉場（Fade / Slide）
        /// <summary>
        /// 取得 Overlay 在 Canvas 座標系下的完整尺寸
        /// </summary>
        private Vector2 GetOverlaySize()
        {
            var rect = OverlayRect.rect;
            return new Vector2(rect.width, rect.height);
        }

        /// <summary>
        /// UI 淡出（螢幕被遮住）
        /// </summary>
        private async Task PlayUIOut(TransitionType type, Color color, float duration)
        {
            if (_overlay == null) return;

            _overlay.gameObject.SetActive(true);
            _overlay.raycastTarget = true;

            var size = GetOverlaySize();

            switch (type)
            {
                case TransitionType.Fade:
                    OverlayRect.anchoredPosition = Vector2.zero;
                    await AnimateFade(0f, 1f, color, duration);
                    break;
                case TransitionType.SlideLeft:
                    // 從右邊滑入覆蓋：起點在右側外 → 終點覆蓋
                    await AnimateSlide(new Vector2(size.x, 0), Vector2.zero, color, duration);
                    break;
                case TransitionType.SlideRight:
                    // 從左邊滑入覆蓋：起點在左側外 → 終點覆蓋
                    await AnimateSlide(new Vector2(-size.x, 0), Vector2.zero, color, duration);
                    break;
                case TransitionType.SlideUp:
                    // 從下方滑入覆蓋：起點在下方外 → 終點覆蓋
                    await AnimateSlide(new Vector2(0, -size.y), Vector2.zero, color, duration);
                    break;
                case TransitionType.SlideDown:
                    // 從上方滑入覆蓋：起點在上方外 → 終點覆蓋
                    await AnimateSlide(new Vector2(0, size.y), Vector2.zero, color, duration);
                    break;
                default:
                    OverlayRect.anchoredPosition = Vector2.zero;
                    await AnimateFade(0f, 1f, color, duration);
                    break;
            }
        }

        /// <summary>
        /// UI 淡入（遮罩消失）
        /// </summary>
        private async Task PlayUIIn(TransitionType type, Color color, float duration)
        {
            if (_overlay == null) return;

            var size = GetOverlaySize();

            switch (type)
            {
                case TransitionType.Fade:
                    await AnimateFade(1f, 0f, color, duration);
                    break;
                case TransitionType.SlideLeft:
                    // 繼續往左滑出：從覆蓋 → 左側外
                    await AnimateSlide(Vector2.zero, new Vector2(-size.x, 0), color, duration);
                    break;
                case TransitionType.SlideRight:
                    // 繼續往右滑出：從覆蓋 → 右側外
                    await AnimateSlide(Vector2.zero, new Vector2(size.x, 0), color, duration);
                    break;
                case TransitionType.SlideUp:
                    // 繼續往上滑出：從覆蓋 → 上方外
                    await AnimateSlide(Vector2.zero, new Vector2(0, size.y), color, duration);
                    break;
                case TransitionType.SlideDown:
                    // 繼續往下滑出：從覆蓋 → 下方外
                    await AnimateSlide(Vector2.zero, new Vector2(0, -size.y), color, duration);
                    break;
                default:
                    await AnimateFade(1f, 0f, color, duration);
                    break;
            }

            OverlayRect.anchoredPosition = Vector2.zero;
            _overlay.raycastTarget = false;
            _overlay.gameObject.SetActive(false);
        }

        /// <summary>
        /// 透明度動畫
        /// </summary>
        private async Task AnimateFade(float from, float to, Color color, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                float alpha = Mathf.Lerp(from, to, smoothT);
                _overlay.color = new Color(color.r, color.g, color.b, alpha);
                await Task.Yield();
            }
            _overlay.color = new Color(color.r, color.g, color.b, to);
        }

        /// <summary>
        /// 滑動動畫
        /// </summary>
        private async Task AnimateSlide(Vector2 from, Vector2 to, Color color, float duration)
        {
            _overlay.color = new Color(color.r, color.g, color.b, 1f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                OverlayRect.anchoredPosition = Vector2.Lerp(from, to, smoothT);
                await Task.Yield();
            }
            OverlayRect.anchoredPosition = to;
        }
        #endregion UI 轉場（Fade / Slide）

        #region Shader 轉場
        /// <summary>
        /// Shader 淡出（_Progress 從 0 到 1）
        /// </summary>
        private async Task PlayShaderOut(Material material, float duration)
        {
            if (_shaderOverlay == null) return;

            // 隱藏 UI 遮罩，改由 Shader 遮罩接管
            HideUIOverlay();

            // 需要畫面擷取的 Shader（如 Pixelate、Blur）
            if (material.HasProperty("_ScreenTex"))
            {
                await CaptureScreenAsync();
                material.SetTexture("_ScreenTex", _capturedScreen);
            }

            _shaderOverlay.material = material;
            _shaderOverlay.raycastTarget = true;
            _shaderOverlay.gameObject.SetActive(true);
            material.SetFloat("_Progress", 0f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                material.SetFloat("_Progress", t);
                await Task.Yield();
            }
            material.SetFloat("_Progress", 1f);
        }

        /// <summary>
        /// Shader 淡入（_Progress 從 1 到 0）
        /// </summary>
        private async Task PlayShaderIn(Material material, float duration)
        {
            if (_shaderOverlay == null) return;

            // 確保 UI 遮罩已隱藏（首次載入時 Awake 會設為全黑）
            HideUIOverlay();

            // 檢查是否支援反向模式（如老電視開機）
            bool hasReverse = material.HasProperty("_Reverse");
            if (hasReverse) material.SetFloat("_Reverse", 1f);

            // 入場時擷取新場景畫面
            if (material.HasProperty("_ScreenTex"))
            {
                // 先讓 Shader 遮罩暫時隱藏，擷取乾淨的新場景
                _shaderOverlay.gameObject.SetActive(false);
                await CaptureScreenAsync();
                material.SetTexture("_ScreenTex", _capturedScreen);
            }

            _shaderOverlay.material = material;
            _shaderOverlay.raycastTarget = true;
            _shaderOverlay.gameObject.SetActive(true);

            float elapsed = 0f;
            if (hasReverse)
            {
                // 有反向模式：Progress 0→1（Shader 內部處理反向邏輯）
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    material.SetFloat("_Progress", t);
                    await Task.Yield();
                }
                material.SetFloat("_Progress", 1f);
                material.SetFloat("_Reverse", 0f);
            }
            else
            {
                // 無反向模式：Progress 1→0（原本行為）
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    material.SetFloat("_Progress", 1f - t);
                    await Task.Yield();
                }
                material.SetFloat("_Progress", 0f);
            }

            _shaderOverlay.raycastTarget = false;
            _shaderOverlay.gameObject.SetActive(false);
            ReleaseCapturedScreen();
        }

        /// <summary>
        /// 等待幀結束後擷取畫面（async 包裝 Coroutine）
        /// </summary>
        private Task CaptureScreenAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(CaptureScreenCoroutine(tcs));
            return tcs.Task;
        }

        /// <summary>
        /// 擷取畫面 Coroutine — WaitForEndOfFrame 後才能呼叫 CaptureScreenshotAsTexture
        /// </summary>
        private IEnumerator CaptureScreenCoroutine(TaskCompletionSource<bool> tcs)
        {
            yield return new WaitForEndOfFrame();
            ReleaseCapturedScreen();
            _capturedScreen = ScreenCapture.CaptureScreenshotAsTexture();
            tcs.SetResult(true);
        }

        /// <summary>
        /// 釋放擷取的畫面資源
        /// </summary>
        private void ReleaseCapturedScreen()
        {
            if (_capturedScreen != null)
            {
                Destroy(_capturedScreen);
                _capturedScreen = null;
            }
        }

        /// <summary>
        /// 將連線的 Shader 屬性覆蓋值套用到 Material（Runtime 臨時套用）
        /// </summary>
        private void ApplyShaderOverrides(Material material, TransitionSettings settings)
        {
            if (settings.shaderOverrides == null) return;

            foreach (var o in settings.shaderOverrides)
            {
                if (string.IsNullOrEmpty(o.propertyName)) continue;
                if (!material.HasProperty(o.propertyName)) continue;

                switch (o.valueType)
                {
                    case ShaderPropertyValueType.Float:
                    case ShaderPropertyValueType.Enum:
                        material.SetFloat(o.propertyName, o.floatValue);
                        break;
                    case ShaderPropertyValueType.Color:
                        material.SetColor(o.propertyName, o.colorValue);
                        break;
                    case ShaderPropertyValueType.Texture:
                        if (o.textureValue != null)
                            material.SetTexture(o.propertyName, o.textureValue);
                        break;
                }
            }
        }

        /// <summary>
        /// 隱藏 UI 遮罩（Shader 轉場時需要讓出畫面）
        /// </summary>
        private void HideUIOverlay()
        {
            if (_overlay == null) return;
            _overlay.color = new Color(_overlay.color.r, _overlay.color.g, _overlay.color.b, 0f);
            _overlay.raycastTarget = false;
            _overlay.gameObject.SetActive(false);
        }
        #endregion Shader 轉場
    }
    #endregion 轉場控制器
}
