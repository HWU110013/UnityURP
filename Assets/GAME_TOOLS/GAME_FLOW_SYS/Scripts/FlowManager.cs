using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatzTools
{
    #region 流程管理器
    /// <summary>
    /// 流程管理器 — DontDestroyOnLoad 單例。
    /// 負責場景的載入、卸載、轉場。
    /// 每個遊戲場景透過 SceneEvent 與本管理器溝通。
    /// </summary>
    public class FlowManager : MonoBehaviour
    {
        #region 單例
        private static FlowManager _instance;

        /// <summary>單例實例</summary>
        public static FlowManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<FlowManager>();
                    if (_instance == null)
                        Debug.LogError("FlowManager 不存在！請確保 FlowManager 場景已載入。");
                }
                return _instance;
            }
        }

        /// <summary>單例是否存在（不觸發搜尋，用於銷毀階段安全檢查）</summary>
        public static bool HasInstance => _instance != null;
        #endregion 單例

        #region 序列化欄位
        [Header("起始設定")]
        /// <summary>第一個載入的遊戲場景</summary>
        [SerializeField] private string _firstGameScene = "";

        [Header("調試")]
        /// <summary>是否顯示 Debug Log</summary>
        [SerializeField] private bool _showDebugLogs = true;
        #endregion 序列化欄位

        #region 私有變數
        private string _currentSceneName = "";
        private bool _isTransitioning;
        private bool _isFirstLoad = true;
        private readonly List<string> _sceneHistory = new();
        private SceneEvent _currentSceneEvent;
        private SceneBlueprintData _blueprintData;
        private TransitionController _transitionController;
        #endregion 私有變數

        #region Lazy Loading
        /// <summary>藍圖資料</summary>
        private SceneBlueprintData BlueprintData
        {
            get
            {
                if (_blueprintData == null)
                    _blueprintData = Resources.Load<SceneBlueprintData>("SceneBlueprintData");
                return _blueprintData;
            }
        }

        /// <summary>轉場控制器</summary>
        private TransitionController Transition
        {
            get
            {
                if (_transitionController == null)
                    _transitionController = FindObjectOfType<TransitionController>();
                return _transitionController;
            }
        }
        #endregion Lazy Loading

        #region 公開屬性
        /// <summary>當前遊戲場景名稱</summary>
        public string CurrentSceneName => _currentSceneName;

        /// <summary>是否正在轉場</summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>場景歷史</summary>
        public IReadOnlyList<string> SceneHistory => _sceneHistory;

        /// <summary>當前場景的 SceneEvent</summary>
        public SceneEvent CurrentSceneEvent => _currentSceneEvent;

        /// <summary>第一個遊戲場景名稱</summary>
        public string FirstGameScene
        {
            get => _firstGameScene;
            set => _firstGameScene = value;
        }
        #endregion 公開屬性

        #region 事件
        /// <summary>場景即將切換（參數: 目標場景名稱）</summary>
        public static event Action<string> OnSceneWillChange;

        /// <summary>場景切換完成（參數: 新場景名稱）</summary>
        public static event Action<string> OnSceneChanged;

        /// <summary>場景載入進度（參數: 0~1）</summary>
        public static event Action<float> OnSceneLoadProgress;

        /// <summary>場景載入錯誤（參數: 錯誤訊息）</summary>
        public static event Action<string> OnSceneLoadError;
        #endregion 事件

        #region Unity 生命週期
        private async void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // 在 DontDestroyOnLoad 之前抓住 FlowManager 場景參考
            var bootScene = gameObject.scene;

            DontDestroyOnLoad(gameObject);

            // TransitionCanvas 也設為不銷毀
            var transitionCanvas = FindObjectOfType<TransitionController>();
            if (transitionCanvas != null && transitionCanvas.gameObject != gameObject)
                DontDestroyOnLoad(transitionCanvas.gameObject);

            Log("FlowManager 初始化");

            // 從藍圖資料讀取起始場景（優先於序列化欄位）
            if (BlueprintData != null && !string.IsNullOrEmpty(BlueprintData.startSceneName))
                _firstGameScene = BlueprintData.startSceneName;

            // 自動載入起始場景
            if (!string.IsNullOrEmpty(_firstGameScene))
            {
                Log($"載入起始場景: {_firstGameScene}");
                await Task.Delay(100);

                await TransitionTo(_firstGameScene);

                // 等待一幀確保新場景完全就緒
                await Task.Yield();

                // 第一場景載入完成，卸載 FlowManager 場景（物件已在 DontDestroyOnLoad）
                if (bootScene.isLoaded && bootScene.name == "FlowManager")
                {
                    await WaitForAsyncOp(SceneManager.UnloadSceneAsync(bootScene));
                    Log("FlowManager 場景已卸載（物件保留在 DontDestroyOnLoad）");
                }
            }
            else
            {
                Log("未設定起始場景！請在場景流程圖中設定。", true);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
        #endregion Unity 生命週期

        #region 場景轉場
        /// <summary>
        /// 最短覆蓋停留時間（秒）— 即使場景瞬間載完也至少停留這麼久
        /// </summary>
        private const float MinCoverDuration = 1f;

        /// <summary>
        /// 兩段式轉場：
        /// 1. 轉場出場動畫 + 同時預載目標場景 → 覆蓋畫面後靜止等待預載完成（最少 1 秒）
        /// 2. 在覆蓋下卸載舊場景、啟用新場景 → 播放轉場入場動畫
        /// </summary>
        public async Task TransitionTo(string targetScene)
        {
            if (_isTransitioning)
            {
                Log("轉場進行中，忽略重複請求", true);
                return;
            }

            if (string.IsNullOrEmpty(targetScene))
            {
                Log("目標場景名稱為空", true);
                return;
            }

            if (targetScene == "FlowManager")
            {
                Log("不能轉場到 FlowManager", true);
                return;
            }

            _isTransitioning = true;
            OnSceneWillChange?.Invoke(targetScene);

            // 詳細狀態 log
            Log($"開始轉場: [{_currentSceneName}] → [{targetScene}]");
            Log($"  _isFirstLoad={_isFirstLoad}, 載入場景數={SceneManager.sceneCount}");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                Log($"  場景[{i}]: {s.name} (loaded={s.isLoaded}, valid={s.IsValid()})");
            }

            TransitionSettings transitionSettings = FindTransitionSettings(_currentSceneName, targetScene);
            bool hasTransition = transitionSettings != null && transitionSettings.type != TransitionType.None;

            try
            {
                // 通知當前 SceneEvent 即將離開
                if (_currentSceneEvent != null)
                    _currentSceneEvent.NotifyWillLeave();

                float coverStartTime = Time.unscaledTime;

                // ===== 第一段：出場動畫（遮住畫面）=====
                // 第一次載入時畫面已經是全黑的，跳過出場動畫
                if (!_isFirstLoad)
                {
                    if (hasTransition && Transition != null)
                        await Transition.PlayTransitionOut(transitionSettings);
                }

                // ===== 畫面已被覆蓋：先載入新場景，再卸載舊場景 =====
                // Unity 不允許卸載最後一個場景，所以必須先載入再卸載

                string oldSceneName = _currentSceneName;

                // 載入目標場景
                var load = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
                if (load == null)
                    throw new Exception($"無法載入場景 {targetScene}，請確認已加入 Build Settings");

                await WaitForAsyncOp(load, progress => OnSceneLoadProgress?.Invoke(progress));

                // 設定為活動場景
                var newScene = SceneManager.GetSceneByName(targetScene);
                if (newScene.isLoaded)
                    SceneManager.SetActiveScene(newScene);

                // 卸載舊場景（新場景已載入，不會是最後一個）
                if (!string.IsNullOrEmpty(oldSceneName))
                {
                    var oldScene = SceneManager.GetSceneByName(oldSceneName);
                    if (oldScene.IsValid() && oldScene.isLoaded)
                    {
                        Log($"卸載場景: {oldSceneName}");
                        var unloadOp = SceneManager.UnloadSceneAsync(oldScene);
                        if (unloadOp != null)
                        {
                            await WaitForAsyncOp(unloadOp);
                            Log($"場景已卸載: {oldSceneName}");
                        }
                        else
                        {
                            Log($"卸載失敗: {oldSceneName}", true);
                        }
                    }
                }

                // 更新狀態
                _currentSceneName = targetScene;
                _sceneHistory.Add(targetScene);

                // 確保覆蓋至少停留 MinCoverDuration
                float elapsed = Time.unscaledTime - coverStartTime;
                if (elapsed < MinCoverDuration)
                    await Task.Delay(Mathf.CeilToInt((MinCoverDuration - elapsed) * 1000));

                // ===== 第二段：入場動畫（新場景已就緒，黑幕退場）=====
                if (Transition != null)
                {
                    if (_isFirstLoad)
                    {
                        // 第一次：用 START→起始場景 edge 的轉場設定退場
                        // 如果沒設定轉場，預設用淡入
                        var inSettings = transitionSettings ?? new TransitionSettings
                        {
                            type = TransitionType.Fade,
                            duration = 1f,
                            color = Color.black
                        };
                        await Transition.PlayTransitionIn(inSettings);
                    }
                    else if (hasTransition)
                    {
                        await Transition.PlayTransitionIn(transitionSettings);
                    }
                }

                _isFirstLoad = false;
                OnSceneChanged?.Invoke(targetScene);
                Log($"轉場完成: {targetScene}");
            }
            catch (Exception e)
            {
                Log($"轉場失敗: {e.Message}", true);
                OnSceneLoadError?.Invoke(e.Message);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 查找兩個場景之間的轉場設定
        /// </summary>
        private TransitionSettings FindTransitionSettings(string fromScene, string toScene)
        {
            if (BlueprintData?.edges == null) return null;

            SceneNode sourceNode;
            SceneNode targetNode = BlueprintData.FindNodeByName(toScene);

            // 第一次啟動時 fromScene 為空，從 START 節點找
            if (string.IsNullOrEmpty(fromScene))
                sourceNode = BlueprintData.nodes?.Find(n => n.nodeType == SceneNodeType.Start);
            else
                sourceNode = BlueprintData.FindNodeByName(fromScene);

            if (sourceNode == null || targetNode == null) return null;

            var edge = BlueprintData.edges.Find(e =>
                e.source == sourceNode.id && e.target == targetNode.id);

            return edge?.transition;
        }

        /// <summary>
        /// 返回上一個場景
        /// </summary>
        public async Task GoBack()
        {
            if (_sceneHistory.Count < 2)
            {
                Log("沒有可返回的場景", true);
                return;
            }

            // 移除當前
            _sceneHistory.RemoveAt(_sceneHistory.Count - 1);
            // 取得上一個
            string previous = _sceneHistory[^1];
            _sceneHistory.RemoveAt(_sceneHistory.Count - 1); // TransitionTo 會再加入

            await TransitionTo(previous);
        }

        /// <summary>
        /// 重新載入當前場景
        /// </summary>
        public async Task ReloadCurrent()
        {
            if (string.IsNullOrEmpty(_currentSceneName)) return;

            string target = _currentSceneName;
            _sceneHistory.RemoveAt(_sceneHistory.Count - 1); // TransitionTo 會再加入
            await TransitionTo(target);
        }
        #endregion 場景轉場

        #region SceneEvent 註冊
        /// <summary>
        /// 由 SceneEvent.Awake 呼叫，註冊為當前場景的事件控制器
        /// </summary>
        public void RegisterSceneEvent(SceneEvent sceneEvent)
        {
            _currentSceneEvent = sceneEvent;
            Log($"SceneEvent 已註冊: {sceneEvent.gameObject.scene.name}");
        }

        /// <summary>
        /// 由 SceneEvent.OnDestroy 呼叫，取消註冊
        /// </summary>
        public void UnregisterSceneEvent(SceneEvent sceneEvent)
        {
            if (_currentSceneEvent == sceneEvent)
                _currentSceneEvent = null;
        }
        #endregion SceneEvent 註冊

        #region 工具
        /// <summary>
        /// 等待 AsyncOperation 完成
        /// </summary>
        private async Task WaitForAsyncOp(AsyncOperation op, Action<float> onProgress = null)
        {
            if (op == null) return;
            while (!op.isDone)
            {
                onProgress?.Invoke(op.progress);
                await Task.Delay(16);
            }
            onProgress?.Invoke(1f);
        }

        private void Log(string message, bool isWarning = false)
        {
            if (!_showDebugLogs) return;

            if (isWarning)
                Debug.LogWarning($"[FlowManager] {message}");
            else
                Debug.Log($"[FlowManager] {message}");
        }
        #endregion 工具
    }
    #endregion 流程管理器
}
