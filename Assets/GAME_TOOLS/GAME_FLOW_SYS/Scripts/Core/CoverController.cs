using UnityEngine;
using UnityEngine.Events;
using System.Threading.Tasks;

namespace CatzTools.GameFlow
{
    #region Cover 控制器
    /// <summary>
    /// Cover 控制器 — 掛在每個 Cover Prefab 根節點上。
    /// 透過 CanvasGroup 控制顯隱與互動，提供生命週期事件。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class CoverController : MonoBehaviour
    {
        #region 事件
        /// <summary>Cover 開啟完成時觸發</summary>
        [SerializeField] private UnityEvent _onOpened;
        /// <summary>Cover 關閉開始前觸發</summary>
        [SerializeField] private UnityEvent _onClosing;

        /// <summary>
        /// Cover 顯示狀態變更（開啟=true / 關閉=false）。
        /// 用於 Dynamic 綁定內層物件（如 SettingsPanelController.SetOpen）。
        /// </summary>
        [SerializeField] private UnityEvent<bool> _onVisibilityChanged;

        public UnityEvent OnOpened => _onOpened;
        public UnityEvent OnClosing => _onClosing;
        public UnityEvent<bool> OnVisibilityChanged => _onVisibilityChanged;
        #endregion 事件

        #region Lazy Loading
        private CanvasGroup _canvasGroup;
        private CanvasGroup Group => _canvasGroup != null
            ? _canvasGroup : (_canvasGroup = GetComponent<CanvasGroup>());
        #endregion Lazy Loading

        #region 公開方法
        /// <summary>
        /// 顯示 Cover（alpha 0→1 + interactable + blocksRaycasts）
        /// </summary>
        public async Task Show(float duration = 0f)
        {
            // 動畫開始前：先開 blocksRaycasts 擋住底層點擊，interactable 等動畫結束才開
            Group.blocksRaycasts = true;

            if (duration > 0f)
                await FadeAlpha(0f, 1f, duration);
            else
                Group.alpha = 1f;

            Group.interactable = true;
            _onVisibilityChanged?.Invoke(true);
            _onOpened?.Invoke();
        }

        /// <summary>
        /// 隱藏 Cover（alpha 1→0 + 關閉 interactable + blocksRaycasts）
        /// </summary>
        public async Task Hide(float duration = 0f)
        {
            _onVisibilityChanged?.Invoke(false);
            _onClosing?.Invoke();
            Group.interactable = false;

            if (duration > 0f)
                await FadeAlpha(1f, 0f, duration);
            else
                Group.alpha = 0f;

            Group.blocksRaycasts = false;
        }

        /// <summary>
        /// 立即設定可見狀態（無動畫）
        /// </summary>
        public void SetVisible(bool visible)
        {
            Group.alpha = visible ? 1f : 0f;
            Group.interactable = visible;
            Group.blocksRaycasts = visible;
        }

        /// <summary>
        /// 請求關閉自身（供 Cover 內部 UI 按鈕呼叫）
        /// </summary>
        public void RequestClose()
        {
            if (FlowManager.Instance != null)
                _ = FlowManager.Instance.HideCover(gameObject.name.Replace("(Clone)", "").Trim());
        }
        #endregion 公開方法

        #region 內部
        private async Task FadeAlpha(float from, float to, float duration)
        {
            float elapsed = 0f;
            Group.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                Group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                await Task.Yield();
            }
            Group.alpha = to;
        }
        #endregion 內部
    }
    #endregion Cover 控制器
}
