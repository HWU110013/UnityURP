using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatzTools.GameFlow
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

        /// <summary>
        /// 單例實例。找不到時靜默回傳 null（不 log）— 讓 caller 自行決定是否視為錯誤。
        /// SceneEvent 等使用方有 null-check + bootstrap 流程，吐 error log 反而誤判正常啟動。
        /// 若使用方拿到 null 但確實需要 FlowManager，請自己 log。
        /// </summary>
        public static FlowManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<FlowManager>();
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

        [Header("Cover")]
        /// <summary>Cover 共用容器（CoverCanvas 的 Transform）</summary>
        [SerializeField] private Transform _coverCanvas;

        #endregion 序列化欄位

        #region 私有變數
        private string _currentSceneName = "";
        private bool _isTransitioning;
        private bool _isFirstLoad = true;
        private readonly List<string> _sceneHistory = new();
        private SceneEvent _currentSceneEvent;
        private SceneBlueprintData _blueprintData;
        private TransitionController _transitionController;
        private readonly Stack<CoverInstance> _coverStack = new();
        private readonly Dictionary<string, GameObject> _coverPool = new();
        private bool _isCoverTransitioning;
        #endregion 私有變數

        #region Lazy Loading
        /// <summary>藍圖資料</summary>
        private SceneBlueprintData BlueprintData
        {
            get
            {
                if (_blueprintData == null)
                {
                    _blueprintData = Resources.Load<SceneBlueprintData>("SceneBlueprintData");
                    _blueprintData?.MigrateEdgesToHybridModel();
                }
                return _blueprintData;
            }
        }

        /// <summary>轉場控制器</summary>
        private TransitionController Transition
        {
            get
            {
                if (_transitionController == null)
                    _transitionController = FindAnyObjectByType<TransitionController>();
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
        /// <summary>是否有 Cover 開啟中</summary>
        public bool HasActiveCover => _coverStack.Count > 0;

        /// <summary>目前開啟的 Cover 數量</summary>
        public int ActiveCoverCount => _coverStack.Count;
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

        /// <summary>Cover 開啟完成（參數: cover 名稱）</summary>
        public static event Action<string> OnCoverOpened;

        /// <summary>Cover 關閉完成（參數: cover 名稱）</summary>
        public static event Action<string> OnCoverClosed;
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
            gameObject.name = "[FlowManager] [Singleton]";
            gameObject.hideFlags = HideFlags.NotEditable;

            // 在 DontDestroyOnLoad 之前抓住 FlowManager 場景參考
            var bootScene = gameObject.scene;

            DontDestroyOnLoad(gameObject);

            // TransitionCanvas 也設為不銷毀
            var transitionCanvas = FindAnyObjectByType<TransitionController>();
            if (transitionCanvas != null && transitionCanvas.gameObject != gameObject)
                DontDestroyOnLoad(transitionCanvas.gameObject);

            Log("FlowManager 初始化");

            // ===== Bootstrap 偵測：是否從其他場景啟動？ =====
            string bootstrapScene = null;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name != "FlowManager" && s.isLoaded)
                {
                    bootstrapScene = s.name;
                    break;
                }
            }

            if (bootstrapScene != null)
            {
                // Bootstrap 模式：採用已載入的場景，跳過起始場景載入
                Log($"Bootstrap 模式: 採用已載入的場景 [{bootstrapScene}]");
                _currentSceneName = bootstrapScene;
                _sceneHistory.Add(bootstrapScene);
                _isFirstLoad = false;

                // 等待一幀確保 TransitionController.Awake 已執行（黑幕已就位）
                await Task.Yield();

                // 卸載 FlowManager 場景（物件已在 DontDestroyOnLoad）
                if (bootScene.isLoaded && bootScene.name == "FlowManager")
                {
                    await WaitForAsyncOp(SceneManager.UnloadSceneAsync(bootScene));
                    Log("FlowManager 場景已卸載（Bootstrap 模式）");
                }

                // 通知場景變更
                OnSceneChanged?.Invoke(bootstrapScene);

                // 自動開啟該場景設定的 Cover
                await AutoShowCoversForScene(bootstrapScene);

                // ===== 播放 IN 轉場（黑幕退場，露出 bootstrap 場景）=====
                // Hybrid 模型：用 bootstrap 場景節點自己的 defaultEnter（若 START → 該場景 edge useOverride 則用 edge.transition）
                // 皆無設定時 fallback 淡入
                if (Transition != null)
                {
                    var inSettings = FindEnterTransition(null, bootstrapScene)
                                  ?? new TransitionSettings
                                  {
                                      type = TransitionType.Fade,
                                      duration = 1f,
                                      color = Color.black
                                  };
                    Log($"Bootstrap IN 轉場: type={inSettings.type}, duration={inSettings.duration}");
                    await Transition.PlayTransitionIn(inSettings);
                }
                return;
            }

            // ===== 正常啟動流程 =====
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

            // 新模型：出場用「來源節點的 defaultExit」，入場用「目標節點的 defaultEnter」
            // edge 若勾選 useOverride，兩段都改用 edge.transition（回歸舊行為）
            TransitionSettings outSettings = FindExitTransition(_currentSceneName, targetScene);
            TransitionSettings inSettings = FindEnterTransition(_currentSceneName, targetScene);
            bool hasOutTransition = outSettings != null && outSettings.type != TransitionType.None;
            bool hasInTransition = inSettings != null && inSettings.type != TransitionType.None;

            try
            {
                // 通知當前 SceneEvent 即將離開
                if (_currentSceneEvent != null)
                    _currentSceneEvent.NotifyWillLeave();

                // 轉場前關閉所有 Cover，防止殘留
                if (_coverStack.Count > 0)
                    await HideAllCovers();

                float coverStartTime = Time.unscaledTime;

                // ===== 第一段：出場動畫（遮住畫面）=====
                // 第一次載入時畫面已經是全黑的，跳過出場動畫
                if (!_isFirstLoad)
                {
                    if (hasOutTransition && Transition != null)
                        await Transition.PlayTransitionOut(outSettings);
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
                        // 第一次：用目標場景的 defaultEnter（或 edge override 的 transition）；皆無設定 fallback 到淡入
                        var firstInSettings = inSettings ?? new TransitionSettings
                        {
                            type = TransitionType.Fade,
                            duration = 1f,
                            color = Color.black
                        };
                        await Transition.PlayTransitionIn(firstInSettings);
                    }
                    else if (hasInTransition)
                    {
                        await Transition.PlayTransitionIn(inSettings);
                    }
                }

                _isFirstLoad = false;
                OnSceneChanged?.Invoke(targetScene);
                Log($"轉場完成: {targetScene}");

                // 自動開啟該場景設定的 Cover
                await AutoShowCoversForScene(targetScene);
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
        /// <summary>查找對應 edge（新模型需查 edge 以得知 useOverride）</summary>
        private SceneEdge FindEdge(string fromScene, string toScene)
        {
            if (BlueprintData?.edges == null) return null;

            SceneNode targetNode = BlueprintData.FindNodeByName(toScene);
            if (targetNode == null) return null;

            SceneNode sourceNode = string.IsNullOrEmpty(fromScene)
                ? BlueprintData.nodes?.Find(n => n.nodeType == SceneNodeType.Start)
                : BlueprintData.FindNodeByName(fromScene);

            if (sourceNode == null) return null;

            return BlueprintData.edges.Find(e =>
                e.source == sourceNode.id && e.target == targetNode.id);
        }

        /// <summary>
        /// 取得「離場」轉場設定（Hybrid 模型）。
        /// edge.useOverride = true → edge.transition；否則 → 來源節點的 defaultExit。
        /// fromScene 為空時（bootstrap / 第一次載入）無出場動畫，回 null。
        /// </summary>
        private TransitionSettings FindExitTransition(string fromScene, string toScene)
        {
            var edge = FindEdge(fromScene, toScene);
            if (edge != null && edge.useOverride) return edge.transition;

            if (string.IsNullOrEmpty(fromScene)) return null;
            return BlueprintData.FindNodeByName(fromScene)?.defaultExit;
        }

        /// <summary>
        /// 取得「入場」轉場設定（Hybrid 模型）。
        /// edge.useOverride = true → edge.transition；否則 → 目標節點的 defaultEnter。
        /// </summary>
        private TransitionSettings FindEnterTransition(string fromScene, string toScene)
        {
            var edge = FindEdge(fromScene, toScene);
            if (edge != null && edge.useOverride) return edge.transition;
            return BlueprintData.FindNodeByName(toScene)?.defaultEnter;
        }

        /// <summary>
        /// 舊版 API 相容 — 回傳 edge.transition（僅當 useOverride=true 時有意義）。
        /// 新程式碼請改用 <see cref="FindEnterTransition"/> / <see cref="FindExitTransition"/>。
        /// </summary>
        [System.Obsolete("改用 FindEnterTransition / FindExitTransition")]
        private TransitionSettings FindTransitionSettings(string fromScene, string toScene)
            => FindEnterTransition(fromScene, toScene);

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

        /// <summary>
        /// 播放獨立轉場效果（不切換場景）。
        /// 畫面遮住 → 執行 onCovered 回調 → 退場。
        /// 適用於：刷新畫面、重置關卡、切換遊戲階段等不需要換場景的情境。
        /// </summary>
        /// <param name="settings">轉場設定</param>
        /// <param name="onCovered">畫面完全遮住時執行的回調（可 null，純播放效果）</param>
        public async Task PlayEffect(TransitionSettings settings, System.Func<Task> onCovered = null)
        {
            if (_isTransitioning) return;
            if (Transition == null) return;

            _isTransitioning = true;
            try
            {
                await Transition.PlayEffect(settings, onCovered);
            }
            finally
            {
                _isTransitioning = false;
            }
        }
        #endregion 場景轉場

        #region Cover 管理
        /// <summary>
        /// 依場景節點的 autoShowCovers 自動開啟 Cover
        /// </summary>
        private async Task AutoShowCoversForScene(string sceneName)
        {
            var sceneNode = BlueprintData?.FindNodeByName(sceneName);
            if (sceneNode == null || sceneNode.autoShowCovers == null) return;

            foreach (var coverName in sceneNode.autoShowCovers)
            {
                if (string.IsNullOrEmpty(coverName)) continue;
                await ShowCover(coverName);
            }
        }

        /// <summary>
        /// 開啟指定 Cover（依名稱查找藍圖中的 PopCover 節點）。
        /// Prefab Cover 會快取在池中，下次開啟直接重用不重建。
        /// </summary>
        public async Task ShowCover(string coverName)
        {
            if (_isCoverTransitioning) return;

            var coverNode = BlueprintData?.FindCoverByName(coverName);
            if (coverNode == null)
            {
                Log($"找不到 Cover: {coverName}", true);
                return;
            }

            _isCoverTransitioning = true;
            try
            {
                var instance = new CoverInstance
                {
                    CoverData = coverNode,
                    SourceType = coverNode.coverSourceType
                };

                if (coverNode.coverSourceType == CoverSourceType.Prefab)
                {
                    instance.Instance = GetOrCreateCoverInstance(coverNode);
                }
                else
                {
                    await LoadCoverScene(coverNode);
                    instance.LoadedSceneName = coverNode.coverSceneName;
                }

                _coverStack.Push(instance);

                // CoverController 自身的 CanvasGroup 淡入
                if (instance.Instance != null)
                {
                    var ctrl = instance.Instance.GetComponent<CoverController>();
                    if (ctrl != null) await ctrl.Show(coverNode.coverOpenDuration);
                }

                OnCoverOpened?.Invoke(coverName);
                Log($"Cover 已開啟: {coverName}");
            }
            finally
            {
                _isCoverTransitioning = false;
            }
        }

        /// <summary>
        /// 關閉最上層 Cover（隱藏不銷毀，保留快取）
        /// </summary>
        public async Task HideCover()
        {
            if (_isCoverTransitioning || _coverStack.Count == 0) return;

            _isCoverTransitioning = true;
            try
            {
                var instance = _coverStack.Pop();
                await HideCoverInstance(instance);

                OnCoverClosed?.Invoke(instance.CoverData.sceneName);
                Log($"Cover 已關閉: {instance.CoverData.sceneName}");
            }
            finally
            {
                _isCoverTransitioning = false;
            }
        }

        /// <summary>
        /// 依名稱關閉特定 Cover
        /// </summary>
        public async Task HideCover(string coverName)
        {
            if (_coverStack.Count == 0) return;

            if (_coverStack.Peek().CoverData.sceneName == coverName)
            {
                await HideCover();
                return;
            }

            // 非最上層：靜默隱藏
            var temp = new Stack<CoverInstance>();
            CoverInstance target = null;
            while (_coverStack.Count > 0)
            {
                var top = _coverStack.Pop();
                if (top.CoverData.sceneName == coverName)
                { target = top; break; }
                temp.Push(top);
            }
            while (temp.Count > 0) _coverStack.Push(temp.Pop());

            if (target != null)
            {
                await HideCoverInstance(target);
                OnCoverClosed?.Invoke(coverName);
                Log($"Cover 已關閉（靜默）: {coverName}");
            }
        }

        /// <summary>
        /// 隱藏所有 Cover（場景切換前自動呼叫，快取保留）
        /// </summary>
        public async Task HideAllCovers()
        {
            while (_coverStack.Count > 0)
            {
                var instance = _coverStack.Pop();
                await HideCoverInstance(instance);
                OnCoverClosed?.Invoke(instance.CoverData.sceneName);
            }
        }

        /// <summary>
        /// 銷毀所有快取的 Cover（僅在需要釋放記憶體時呼叫）
        /// </summary>
        public void DestroyCoverPool()
        {
            foreach (var go in _coverPool.Values)
            {
                if (go != null) Destroy(go);
            }
            _coverPool.Clear();
            Log("Cover 快取池已清空");
        }

        /// <summary>
        /// 確保 CoverCanvas 存在（舊場景可能缺少，自動補建）
        /// </summary>
        private void EnsureCoverCanvas()
        {
            if (_coverCanvas != null) return;

            var canvasObj = new GameObject("[CoverCanvas]");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            canvasObj.AddComponent<CanvasGroup>();
            DontDestroyOnLoad(canvasObj);
            _coverCanvas = canvasObj.transform;

            // 確保 EventSystem 存在（UI 點擊必要）
            EnsureEventSystem();

            Log("CoverCanvas 自動建立（建議重建 FlowManager 場景）");
        }

        /// <summary>
        /// 確保場景中有 EventSystem（沒有就掛在 FlowManager 上）
        /// </summary>
        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            gameObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
            gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            Log("EventSystem 自動掛載到 FlowManager");
        }

        /// <summary>
        /// 從池中取或首次實例化 Cover
        /// </summary>
        private GameObject GetOrCreateCoverInstance(SceneNode coverNode)
        {
            if (coverNode.coverPrefab == null) return null;

            string key = coverNode.sceneName;

            // 池中已有 → 直接重用
            if (_coverPool.TryGetValue(key, out var cached) && cached != null)
                return cached;

            EnsureCoverCanvas();

            var coverGO = Instantiate(coverNode.coverPrefab, _coverCanvas);
            coverGO.name = key; // 去掉 (Clone) 方便辨識

            // 確保有 CoverController + CanvasGroup
            if (coverGO.GetComponent<CoverController>() == null)
                coverGO.AddComponent<CoverController>();

            // 依 sortOrder 排序 Sibling Index（值大的在上面）
            ApplyCoverSortOrder(coverGO, coverNode.sortOrder);

            // 初始隱藏
            var ctrl = coverGO.GetComponent<CoverController>();
            ctrl.SetVisible(false);

            _coverPool[key] = coverGO;
            return coverGO;
        }

        /// <summary>
        /// 依 sortOrder 設定 Cover 的 Sibling Index。
        /// sortOrder 值大的設定較高的 index，渲染在上面。
        /// </summary>
        private void ApplyCoverSortOrder(GameObject coverGO, int sortOrder)
        {
            if (_coverCanvas == null || coverGO == null) return;

            int childCount = _coverCanvas.childCount;
            int targetIndex = 0;

            for (int i = 0; i < childCount; i++)
            {
                var child = _coverCanvas.GetChild(i);
                if (child.gameObject == coverGO) continue;

                // 找同為 Cover 的物件，比較 sortOrder
                var key = child.gameObject.name;
                var node = BlueprintData?.FindCoverByName(key);
                if (node != null && node.sortOrder <= sortOrder)
                    targetIndex = i + 1;
            }

            coverGO.transform.SetSiblingIndex(Mathf.Min(targetIndex, childCount - 1));
        }

        /// <summary>
        /// 隱藏 Cover 實例（不銷毀，保留在池中供重用）
        /// </summary>
        private async Task HideCoverInstance(CoverInstance instance)
        {
            if (instance.SourceType == CoverSourceType.Prefab)
            {
                if (instance.Instance != null)
                {
                    var ctrl = instance.Instance.GetComponent<CoverController>();
                    if (ctrl != null)
                        await ctrl.Hide(instance.CoverData.coverCloseDuration);
                }
                // 不 Destroy — 留在池中
            }
            else
            {
                var sceneName = instance.LoadedSceneName;
                if (!string.IsNullOrEmpty(sceneName))
                {
                    var scene = SceneManager.GetSceneByName(sceneName);
                    if (scene.isLoaded)
                        _ = SceneManager.UnloadSceneAsync(scene);
                }
            }
        }

        private async Task LoadCoverScene(SceneNode coverNode)
        {
            var sceneName = coverNode.coverSceneName;
            if (string.IsNullOrEmpty(sceneName)) return;

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null) return;
            await WaitForAsyncOp(op);
        }
        #endregion Cover 管理

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

        private const string LOG_CH = "FlowManager";

        private void Log(string message, bool isWarning = false)
        {
            if (isWarning)
                CatzLogger.LogWarning(LOG_CH, message);
            else
                CatzLogger.Log(LOG_CH, message);
        }
        #endregion 工具
    }
    #endregion 流程管理器
}
