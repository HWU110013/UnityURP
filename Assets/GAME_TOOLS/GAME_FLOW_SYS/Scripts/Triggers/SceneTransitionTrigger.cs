using UnityEngine;

namespace CatzTools
{
    #region 場景轉場觸發基底
    /// <summary>
    /// 場景轉場觸發器抽象基底。
    /// 子類實作具體觸發方式（按鈕、碰撞、互動鍵等），觸發時呼叫 ExecuteTransition。
    /// </summary>
    public abstract class SceneTransitionTrigger : MonoBehaviour
    {
        #region 序列化欄位
        [Header("轉場設定")]
        /// <summary>目標場景名稱</summary>
        [SerializeField] protected string _targetScene = "";

        /// <summary>是否允許重複觸發（防連點）</summary>
        [SerializeField] private bool _allowRetrigger = false;

        [Header("調試")]
        /// <summary>是否顯示 Debug Log</summary>
        [SerializeField] private bool _showDebugLogs = true;
        #endregion 序列化欄位

        #region 私有變數
        private SceneEvent _sceneEvent;
        private bool _hasTriggered;
        #endregion 私有變數

        #region Lazy Loading
        /// <summary>自動尋找場景中的 SceneEvent</summary>
        protected SceneEvent SceneEvent
            => _sceneEvent != null ? _sceneEvent : (_sceneEvent = FindFirstObjectByType<SceneEvent>());
        #endregion Lazy Loading

        #region 公開屬性
        /// <summary>目標場景名稱</summary>
        public string TargetScene => _targetScene;
        #endregion 公開屬性

        #region 觸發執行
        /// <summary>
        /// 執行轉場。子類在觸發條件滿足時呼叫此方法。
        /// </summary>
        protected void ExecuteTransition()
        {
            if (!CanTrigger()) return;

            if (!_allowRetrigger && _hasTriggered) return;
            _hasTriggered = true;

            if (string.IsNullOrEmpty(_targetScene))
            {
                Log("目標場景未設定", true);
                return;
            }

            if (SceneEvent == null)
            {
                Log("場景中找不到 SceneEvent", true);
                return;
            }

            Log($"觸發轉場 → {_targetScene}");
            SceneEvent.RequestTransition(_targetScene);
        }

        /// <summary>
        /// 子類可覆寫的前置條件檢查。回傳 false 時不觸發轉場。
        /// </summary>
        protected virtual bool CanTrigger() => true;
        #endregion 觸發執行

        #region 工具
        protected void Log(string message, bool isWarning = false)
        {
            if (!_showDebugLogs) return;

            string prefix = $"[{GetType().Name}:{_targetScene}]";
            if (isWarning)
                Debug.LogWarning($"{prefix} {message}");
            else
                Debug.Log($"{prefix} {message}");
        }
        #endregion 工具
    }
    #endregion 場景轉場觸發基底
}
