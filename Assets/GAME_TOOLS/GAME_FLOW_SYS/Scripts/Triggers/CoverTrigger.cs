using UnityEngine;

namespace CatzTools.GameFlow
{
    #region Cover 動作類型
    /// <summary>
    /// Cover 觸發動作
    /// </summary>
    public enum CoverAction
    {
        /// <summary>開啟 Cover</summary>
        [InspectorName("開啟")]
        Show,
        /// <summary>關閉 Cover</summary>
        [InspectorName("關閉")]
        Hide,
        /// <summary>切換 Cover（開→關 / 關→開）</summary>
        [InspectorName("切換")]
        Toggle
    }
    #endregion Cover 動作類型

    #region Cover 觸發器基底
    /// <summary>
    /// Cover 觸發器抽象基底。
    /// 子類實作觸發方式，觸發時開啟或關閉 Cover。
    /// </summary>
    public abstract class CoverTrigger : MonoBehaviour
    {
        #region 序列化欄位
        [Header("Cover 設定")]
        /// <summary>目標 Cover 名稱</summary>
        [SerializeField] protected string _coverName = "";

        /// <summary>觸發動作</summary>
        [SerializeField] protected CoverAction _action = CoverAction.Show;

        [Header("調試")]
        /// <summary>是否顯示 Debug Log</summary>
        #endregion 序列化欄位

        #region Lazy Loading
        private SceneEvent _sceneEvent;

        /// <summary>自動尋找場景中的 SceneEvent</summary>
        protected SceneEvent SceneEvent
            => _sceneEvent != null ? _sceneEvent : (_sceneEvent = FindFirstObjectByType<SceneEvent>());
        #endregion Lazy Loading

        #region 公開屬性
        /// <summary>目標 Cover 名稱</summary>
        public string CoverName => _coverName;
        #endregion 公開屬性

        #region 觸發執行
        /// <summary>
        /// 執行 Cover 動作。子類在觸發條件滿足時呼叫此方法。
        /// </summary>
        protected void ExecuteCover()
        {
            if (string.IsNullOrEmpty(_coverName))
            {
                Log("Cover 名稱未設定", true);
                return;
            }

            if (SceneEvent == null)
            {
                Log("場景中找不到 SceneEvent", true);
                return;
            }

            switch (_action)
            {
                case CoverAction.Show:
                    Log($"開啟 Cover → {_coverName}");
                    SceneEvent.RequestShowCover(_coverName);
                    break;
                case CoverAction.Hide:
                    Log($"關閉 Cover → {_coverName}");
                    SceneEvent.RequestHideCover(_coverName);
                    break;
                case CoverAction.Toggle:
                    if (FlowManager.Instance != null && FlowManager.Instance.HasActiveCover)
                    {
                        Log($"切換關閉 Cover → {_coverName}");
                        SceneEvent.RequestHideCover(_coverName);
                    }
                    else
                    {
                        Log($"切換開啟 Cover → {_coverName}");
                        SceneEvent.RequestShowCover(_coverName);
                    }
                    break;
            }
        }
        #endregion 觸發執行

        #region 工具
        private const string LOG_CH = "FlowManager";

        protected void Log(string message, bool isWarning = false)
        {
            string prefix = $"[{GetType().Name}:{_coverName}]";
            if (isWarning)
                CatzLogger.LogWarning(LOG_CH, $"{prefix} {message}");
            else
                CatzLogger.Log(LOG_CH, $"{prefix} {message}");
        }
        #endregion 工具
    }
    #endregion Cover 觸發器基底
}
