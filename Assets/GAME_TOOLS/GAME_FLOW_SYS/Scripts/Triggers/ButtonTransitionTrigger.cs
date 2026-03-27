using UnityEngine;
using UnityEngine.UI;

namespace CatzTools
{
    #region UI 按鈕轉場觸發器
    /// <summary>
    /// UI 按鈕轉場觸發器 — 掛在帶 Button 的 GameObject 上，點擊即觸發轉場。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonTransitionTrigger : SceneTransitionTrigger
    {
        #region Lazy Loading
        private Button _button;
        private Button Btn => _button != null ? _button : (_button = GetComponent<Button>());
        #endregion Lazy Loading

        #region Unity 生命週期
        private void Awake()
        {
            Btn.onClick.AddListener(OnButtonClicked);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClicked);
        }
        #endregion Unity 生命週期

        #region 事件處理
        private void OnButtonClicked()
        {
            ExecuteTransition();
        }
        #endregion 事件處理
    }
    #endregion UI 按鈕轉場觸發器
}
