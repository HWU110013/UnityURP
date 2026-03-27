using System;
using UnityEngine;

namespace CatzTools
{
    #region 場景事件控制器
    /// <summary>
    /// 場景事件控制器 — 每個遊戲場景各一個。
    /// 場景載入時自動向 FlowManager 註冊，提供轉場請求介面。
    /// 場景內的觸發元件（按鈕、Trigger、影片結束等）透過此腳本請求轉場。
    /// </summary>
    public class SceneEvent : MonoBehaviour
    {
        #region 序列化欄位
        [Header("場景資訊")]
        /// <summary>本場景名稱（自動偵測）</summary>
        [SerializeField] private string _sceneName = "";

        [Header("調試")]
        /// <summary>是否顯示 Debug Log</summary>
        [SerializeField] private bool _showDebugLogs = true;
        #endregion 序列化欄位

        #region 公開屬性
        /// <summary>本場景名稱</summary>
        public string SceneName => _sceneName;
        #endregion 公開屬性

        #region 事件
        /// <summary>場景已載入（SceneEvent 就緒後觸發）</summary>
        public event Action OnSceneReady;

        /// <summary>場景即將離開（FlowManager 通知轉場前觸發）</summary>
        public event Action OnSceneWillLeave;

        /// <summary>轉場請求已發送（參數: 目標場景名稱）</summary>
        public event Action<string> OnTransitionRequested;
        #endregion 事件

        #region Unity 生命週期
        private void Awake()
        {
            // 自動偵測場景名稱
            if (string.IsNullOrEmpty(_sceneName))
                _sceneName = gameObject.scene.name;

            // 向 FlowManager 註冊
            if (FlowManager.Instance != null)
                FlowManager.Instance.RegisterSceneEvent(this);

            Log($"SceneEvent 就緒: {_sceneName}");
        }

        private void Start()
        {
            OnSceneReady?.Invoke();
        }

        private void OnDestroy()
        {
            // 停止播放時 FlowManager 可能已先被銷毀，不需要報錯
            if (!FlowManager.HasInstance) return;
            FlowManager.Instance.UnregisterSceneEvent(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_sceneName) && gameObject.scene.IsValid())
                _sceneName = gameObject.scene.name;
        }
#endif
        #endregion Unity 生命週期

        #region 轉場請求
        /// <summary>
        /// 請求轉場到指定場景。
        /// 場景內的觸發元件（按鈕、Trigger 等）呼叫此方法。
        /// </summary>
        public async void RequestTransition(string targetScene)
        {
            if (string.IsNullOrEmpty(targetScene))
            {
                Log("目標場景名稱為空", true);
                return;
            }

            Log($"請求轉場: {_sceneName} → {targetScene}");
            OnTransitionRequested?.Invoke(targetScene);

            if (FlowManager.Instance != null)
                await FlowManager.Instance.TransitionTo(targetScene);
            else
                Log("FlowManager 不存在，無法轉場", true);
        }

        /// <summary>
        /// 請求返回上一個場景
        /// </summary>
        public async void RequestGoBack()
        {
            Log("請求返回上一場景");

            if (FlowManager.Instance != null)
                await FlowManager.Instance.GoBack();
            else
                Log("FlowManager 不存在，無法返回", true);
        }

        /// <summary>
        /// 請求重新載入當前場景
        /// </summary>
        public async void RequestReload()
        {
            Log("請求重新載入場景");

            if (FlowManager.Instance != null)
                await FlowManager.Instance.ReloadCurrent();
            else
                Log("FlowManager 不存在，無法重載", true);
        }
        #endregion 轉場請求

        #region FlowManager 回調
        /// <summary>
        /// FlowManager 通知即將離開此場景（內部使用）
        /// </summary>
        public void NotifyWillLeave()
        {
            Log($"即將離開: {_sceneName}");
            OnSceneWillLeave?.Invoke();
        }
        #endregion FlowManager 回調

        #region 工具
        private void Log(string message, bool isWarning = false)
        {
            if (!_showDebugLogs) return;

            if (isWarning)
                Debug.LogWarning($"[SceneEvent:{_sceneName}] {message}");
            else
                Debug.Log($"[SceneEvent:{_sceneName}] {message}");
        }
        #endregion 工具
    }
    #endregion 場景事件控制器
}
