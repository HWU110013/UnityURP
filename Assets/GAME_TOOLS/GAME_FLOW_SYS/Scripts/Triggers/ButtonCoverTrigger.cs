using UnityEngine;
using UnityEngine.UI;

namespace CatzTools.GameFlow
{
    #region UI 按鈕 Cover 觸發器
    /// <summary>
    /// UI 按鈕 Cover 觸發器 — 掛在帶 Button 的 GameObject 上，點擊即觸發 Cover 開啟/關閉。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonCoverTrigger : CoverTrigger
    {
        #region Lazy Loading
        private Button _button;
        private Button Btn => _button != null ? _button : (_button = GetComponent<Button>());
        #endregion Lazy Loading

        #region Unity 生命週期
        private void Awake()
        {
            Btn.onClick.AddListener(OnButtonClick);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClick);
        }
        #endregion Unity 生命週期

        #region 事件處理
        private void OnButtonClick()
        {
            ExecuteCover();
        }
        #endregion 事件處理
    }
    #endregion UI 按鈕 Cover 觸發器
}
