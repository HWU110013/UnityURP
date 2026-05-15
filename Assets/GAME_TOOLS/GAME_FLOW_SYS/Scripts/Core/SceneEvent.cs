using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CatzTools.GameFlow
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

        [Header("場景擁有權")]
        /// <summary>FlowManager 載入後自動移除本場景重複的 EventSystem / AudioListener / InputModule。</summary>
        [Tooltip("FlowManager 場景持久化模型下，這些單例由 FlowManager 提供。本場景重複會造成每幀警告與輸入衝突。")]
        [SerializeField] private bool _autoCleanupSharedSingletons = true;

        [Header("調試")]
        /// <summary>是否顯示 Debug Log</summary>
        #endregion 序列化欄位

        #region 公開屬性
        /// <summary>本場景名稱</summary>
        public string SceneName => _sceneName;

        /// <summary>是否啟用「重複共用單例」自動清理（Editor 守衛 + Runtime 都尊重此設定）</summary>
        public bool AutoCleanupSharedSingletons => _autoCleanupSharedSingletons;
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
        private async void Awake()
        {

            // 自動偵測場景名稱
            if (string.IsNullOrEmpty(_sceneName))
                _sceneName = gameObject.scene.name;

            // FlowManager 不存在時自動載入（方便從任意場景開始測試）
            if (FlowManager.Instance == null)
            {
                Log("FlowManager 未找到，嘗試自動載入...");
                await BootstrapFlowManager();
            }

            // FlowManager 已就緒 → 清掉本場景重複的共用單例（避免「兩個 EventSystem / AudioListener」警告）
            if (_autoCleanupSharedSingletons && FlowManager.Instance != null)
                CleanupSharedSingletons();

            // 向 FlowManager 註冊
            if (FlowManager.Instance != null)
                FlowManager.Instance.RegisterSceneEvent(this);
            else
                Log("FlowManager 仍不可用，SceneEvent 功能受限", true);

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

        #region Bootstrap
        /// <summary>
        /// 自動載入 FlowManager 場景（Additive），等待 FlowManager 實例就緒
        /// </summary>
        private async Task BootstrapFlowManager()
        {
            var op = SceneManager.LoadSceneAsync("FlowManager", LoadSceneMode.Additive);
            if (op == null)
            {
                Log("無法載入 FlowManager 場景，請確認已加入 Build Settings", true);
                return;
            }

            while (!op.isDone)
                await Task.Yield();

            // 等待 FlowManager 實例初始化（最多 100 幀）
            int maxWait = 100;
            while (FlowManager.Instance == null && maxWait-- > 0)
                await Task.Yield();

            if (FlowManager.Instance != null)
                Log("FlowManager 已自動載入（Bootstrap 模式）");
            else
                Log("FlowManager 場景載入後仍找不到實例", true);
        }
        #endregion Bootstrap

        #region 場景擁有權清理

        /// <summary>
        /// 清掉本場景所有 EventSystem / AudioListener / BaseInputModule，
        /// 因為 FlowManager 場景持久化已經提供這些單例。
        /// Editor 模式下，被清完的 GameObject 若只剩 Transform 且無子物件，整個 GO 也會被刪除。
        /// 回傳「移除的組件數 + 移除的空殼 GO 數」總和。
        /// </summary>
        /// <param name="silent">true 不輸出 Log（給批次清理用）</param>
        public int CleanupSharedSingletons(bool silent = false)
        {
            var myScene = gameObject.scene;
            if (!myScene.IsValid()) return 0;

            int removedComps = 0;
            var touchedGOs = new HashSet<GameObject>();

            foreach (var root in myScene.GetRootGameObjects())
            {
                foreach (var al in root.GetComponentsInChildren<AudioListener>(true))
                {
                    touchedGOs.Add(al.gameObject);
                    SafeDestroy(al);
                    removedComps++;
                }
                foreach (var im in root.GetComponentsInChildren<BaseInputModule>(true))
                {
                    touchedGOs.Add(im.gameObject);
                    SafeDestroy(im);
                    removedComps++;
                }
                foreach (var es in root.GetComponentsInChildren<EventSystem>(true))
                {
                    touchedGOs.Add(es.gameObject);
                    SafeDestroy(es);
                    removedComps++;
                }
            }

            // 空殼 GO 清理只在 Editor 模式做：runtime 的 Destroy 是延遲執行，
            // 此時 GetComponents 還會回傳剛 Destroy 的組件，無法準確判斷是否真的空。
            int removedGOs = 0;
            if (!Application.isPlaying)
                removedGOs = CleanupOrphanGameObjects(touchedGOs);

            if ((removedComps > 0 || removedGOs > 0) && !silent)
            {
                var msg = $"清理場景 '{myScene.name}'：移除 {removedComps} 個重複共用組件";
                if (removedGOs > 0) msg += $"，順手刪掉 {removedGOs} 個空殼 GameObject";
                Log(msg);
            }

            return removedComps + removedGOs;
        }

        /// <summary>
        /// 把被觸碰過的 GameObject 中只剩 Transform / RectTransform 且無子物件的整顆刪掉。
        /// 僅 Editor 模式呼叫（runtime 的 Destroy 是延遲，無法準確判斷）。
        /// </summary>
        private static int CleanupOrphanGameObjects(HashSet<GameObject> touched)
        {
            int removed = 0;
            foreach (var go in touched)
            {
                if (go == null) continue;
                if (go.transform.childCount > 0) continue;

                var comps = go.GetComponents<Component>();
                bool onlyTransform = true;
                for (int i = 0; i < comps.Length; i++)
                {
                    if (comps[i] == null) continue;        // 已被銷毀的組件參照
                    if (comps[i] is Transform) continue;   // 含 RectTransform（繼承自 Transform）
                    onlyTransform = false;
                    break;
                }
                if (onlyTransform)
                {
                    SafeDestroy(go);
                    removed++;
                }
            }
            return removed;
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        #endregion 場景擁有權清理

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

        #region Cover 請求
        /// <summary>Cover 開啟請求事件</summary>
        public event Action<string> OnCoverShowRequested;

        /// <summary>Cover 關閉請求事件</summary>
        public event Action<string> OnCoverHideRequested;

        /// <summary>
        /// 請求開啟 Cover
        /// </summary>
        public async void RequestShowCover(string coverName)
        {
            if (string.IsNullOrEmpty(coverName))
            {
                Log("Cover 名稱為空", true);
                return;
            }

            Log($"請求開啟 Cover: {coverName}");
            OnCoverShowRequested?.Invoke(coverName);

            if (FlowManager.Instance != null)
                await FlowManager.Instance.ShowCover(coverName);
            else
                Log("FlowManager 不存在，無法開啟 Cover", true);
        }

        /// <summary>
        /// 請求關閉最上層 Cover
        /// </summary>
        public async void RequestHideCover()
        {
            Log("請求關閉最上層 Cover");

            if (FlowManager.Instance != null)
                await FlowManager.Instance.HideCover();
            else
                Log("FlowManager 不存在，無法關閉 Cover", true);
        }

        /// <summary>
        /// 請求關閉指定 Cover
        /// </summary>
        public async void RequestHideCover(string coverName)
        {
            Log($"請求關閉 Cover: {coverName}");
            OnCoverHideRequested?.Invoke(coverName);

            if (FlowManager.Instance != null)
                await FlowManager.Instance.HideCover(coverName);
            else
                Log("FlowManager 不存在，無法關閉 Cover", true);
        }
        #endregion Cover 請求

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
        private const string LOG_CH = "FlowManager";

        private void Log(string message, bool isWarning = false)
        {
            string prefix = $"[SceneEvent:{_sceneName}]";
            if (isWarning)
                CatzLogger.LogWarning(LOG_CH, $"{prefix} {message}");
            else
                CatzLogger.Log(LOG_CH, $"{prefix} {message}");
        }
        #endregion 工具
    }
    #endregion 場景事件控制器
}
