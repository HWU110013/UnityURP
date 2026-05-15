#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UIImage = UnityEngine.UI.Image;
using GraphicRaycaster = UnityEngine.UI.GraphicRaycaster;
using CanvasScaler = UnityEngine.UI.CanvasScaler;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using CatzTools.GameFlow;

namespace CatzTools.GameFlow.Editor
{
    #region 場景流程編輯器視窗
    /// <summary>
    /// 場景流程編輯器視窗 — GraphView 容器。
    /// FlowManager 不參與節點圖，而是作為底層常駐場景，
    /// 開啟視窗時自動建立場景、掛好腳本、加入 Build Settings 第一個。
    /// </summary>
    public class SceneFlowEditorWindow : EditorWindow
    {
        #region 常數

        // 三欄佈局寬度統一由 CatzTools.Editor.CatzEditorStyles 提供
        private const float SCROLLBAR_W = CatzTools.Editor.CatzEditorStyles.SCROLLBAR_W;
        private const float LEFT_WIDTH  = CatzTools.Editor.CatzEditorStyles.LEFT_WIDTH;
        private const float RIGHT_WIDTH = CatzTools.Editor.CatzEditorStyles.RIGHT_WIDTH;

        #endregion 常數

        #region 私有變數
        private SceneFlowGraphView _graphView;
        private SceneBlueprintData _blueprintData;
        private Label _statusLabel;
        private Label _flowManagerStatusLabel;
        private Button _playButton;
        private Button _langButton;
        private VisualElement _sceneListPanel;
        private VisualElement _sceneListContent;
        private SceneFlowMiniMap _miniMap;
        private bool _sceneListVisible = true;
        private VisualElement _coverPanel;
        private IMGUIContainer _coverIMGUI;
        private bool _coverPanelVisible = false;
        private Vector2 _coverScrollPos;
        private readonly HashSet<string> _coverFoldouts = new();
        /// <summary>共用 Inline 改名機制（CatzTools 統一規範）</summary>
        private CatzTools.CatzInlineRename _rename;
        private VisualElement _inspectorPanel;
        private VisualElement _inspectorContent;
        private IMGUIContainer _inspectorIMGUI;
        private object _selectedObject;
        private Vector2 _inspectorScrollPos;
        // Hybrid 轉場 UI 狀態（v0.7.8b）
        // 0 = Enter（進場），1 = Exit（離場）
        private int _nodeTransMode;
        private int _edgeTransMode;

        // ── Service manifest cache（避免每次 IMGUI repaint 都反射掃 assembly 造成卡頓）──
        private List<(System.Type type, AutoRegisterAttribute attr)> _serviceCache;
        #endregion 私有變數

        #region Lazy Loading 屬性
        private SceneBlueprintData BlueprintData
        {
            get
            {
                if (_blueprintData == null)
                {
                    _blueprintData = LoadOrCreateBlueprintData();
                    _blueprintData?.MigrateEdgesToHybridModel();
                }
                return _blueprintData;
            }
        }
        #endregion Lazy Loading 屬性

        #region Unity 編輯器選單
        /// <summary>
        /// 開啟場景流程圖編輯器
        /// </summary>
        [MenuItem("CatzTools/[v" + GameFlowVersion.VERSION + "] Scene Flow", false, 103)]
        public static void ShowWindow()
        {
            var dockTargets = CatzTools.Editor.CatzEditorStyles.GetDockTargets<SceneFlowEditorWindow>();
            var window = GetWindow<SceneFlowEditorWindow>(SceneFlowLocale.WindowTitle, dockTargets);
            window.minSize = new Vector2(800, 500);
        }
        #endregion Unity 編輯器選單

        #region 生命週期
        private void CreateGUI()
        {
            // 共用 Inline 改名機制初始化（CatzTools 統一規範）
            _rename ??= new CatzTools.CatzInlineRename(Repaint);

            var root = rootVisualElement;

            // FlowManager 資訊列（頂部）
            CreateFlowManagerBar(root);

            // 工具列
            CreateToolbar(root);

            // 主要區域（左側屬性面板 + GraphView + 右側場景清單）
            var mainContainer = CatzTools.Editor.CatzEditorStyles.CreateMainContainer();

            // 左側：屬性面板
            CreateInspectorPanel(mainContainer);

            // 中央：GraphView
            _graphView = new SceneFlowGraphView();
            _graphView.OnGraphChanged += OnGraphDataChanged;
            _graphView.OnRequestAddScene += OnRequestAddSceneFromGraph;
            _graphView.OnRequestSetAsStart += OnSetSceneAsStart;
            _graphView.OnRequestOpenScene += EnsureAndOpenScene;
            _graphView.OnSelectionChanged += OnGraphSelectionChanged;
            mainContainer.Add(_graphView);

            // 右側面板
            CreateCoverPanel(mainContainer);
            CreateSceneListPanel(mainContainer);

            root.Add(mainContainer);

            // 狀態列
            CreateStatusBar(root);

            // 確保 FlowManager 場景存在
            EnsureFlowManagerScene();

            // 載入資料
            _graphView.LoadBlueprintData(BlueprintData);
            UpdateStatusBar();
            UpdateFlowManagerBar();
            RefreshSceneListPanel();

            // 延遲一幀後將起始場景置中
            EditorApplication.delayCall += () => _graphView.FrameStartNode();

            // 監聽 Play Mode 狀態變化（更新播放按鈕外觀）
            EditorApplication.playModeStateChanged += OnPlayModeUIRefresh;

            // 監聽專案資產變動（場景刪除/移動/重新命名）
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeUIRefresh;
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        /// <summary>
        /// 視窗取得焦點時重新驗證所有場景連結狀態
        /// </summary>
        private void OnFocus()
        {
            ValidateSceneLinks();
        }

        /// <summary>
        /// 專案資產變動時（刪除/移動/重新命名場景檔）
        /// </summary>
        private void OnProjectChanged()
        {
            ValidateSceneLinks();
        }

        /// <summary>
        /// 驗證所有場景節點的資源連結，自動修復或更新狀態
        /// </summary>
        private void ValidateSceneLinks()
        {
            if (BlueprintData == null || _graphView == null) return;

            bool changed = false;
            foreach (var node in BlueprintData.nodes)
            {
                if (node.nodeType != SceneNodeType.Scene) continue;

                // sceneAsset 被 Unity 清除（檔案已刪除/移動）
                if (node.sceneAsset == null)
                {
                    // 嘗試用名稱重新配對
                    string path = $"Assets/Scenes/{node.sceneName}.unity";
                    var found = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    if (found != null)
                    {
                        node.sceneAsset = found;
                        changed = true;
                    }
                }
            }

            if (changed)
                EditorUtility.SetDirty(BlueprintData);

            _graphView.RefreshAllNodeAppearance();
            UpdateStatusBar();
        }

        private void OnPlayModeUIRefresh(PlayModeStateChange state)
        {
            UpdatePlayButton();
        }

        /// <summary>
        /// 切換語系後重建整個 UI
        /// </summary>
        private void RebuildUI()
        {
            rootVisualElement.Clear();
            _blueprintData = null;
            titleContent = new GUIContent(SceneFlowLocale.WindowTitle);
            CreateGUI();
        }
        #endregion 生命週期

        #region FlowManager 資訊列
        /// <summary>
        /// 建立 FlowManager 資訊列（固定在頂部，不可刪除）
        /// </summary>
        private void CreateFlowManagerBar(VisualElement root)
        {
            var bar = CatzTools.Editor.CatzEditorStyles.CreateInfoBar();

            bar.Add(CatzTools.Editor.CatzEditorStyles.CreateInfoBarTitle(SceneFlowLocale.FlowManagerTitle));

            var openButton = CatzTools.Editor.CatzEditorStyles.CreateInfoBarButton(SceneFlowLocale.BtnOpen, () => OpenFlowManagerScene());
            bar.Add(openButton);

            var rebuildButton = CatzTools.Editor.CatzEditorStyles.CreateInfoBarButton(SceneFlowLocale.BtnRebuild, () =>
            {
                CreateFlowManagerScene();
                UpdateFlowManagerBar();
            });
            bar.Add(rebuildButton);

            _playButton = CatzTools.Editor.CatzEditorStyles.CreateInfoBarButton("", () => PlayFromFlowManager());
            UpdatePlayButton();
            bar.Add(_playButton);

            // 狀態
            _flowManagerStatusLabel = CatzTools.Editor.CatzEditorStyles.CreateInfoBarStatus();
            bar.Add(_flowManagerStatusLabel);

            // 從 GDS 匯入（右側，LOG 左邊） — 與 InputManager 保持一致的位置
            bar.Add(CatzTools.Editor.CatzEditorStyles.CreateInfoBarButton(
                SceneFlowLocale.InfoBarImportGDS,
                () => EditorUtility.DisplayDialog("GDS", SceneFlowLocale.DlgGdsNotImplemented, SceneFlowLocale.DlgYes)));

            // Debug Log 開關
            bar.Add(CatzTools.Editor.CatzEditorStyles.CreateInfoBarDebugToggle(
                CatzLogger.IsChannelEnabled("FlowManager"), on =>
            {
                CatzLogger.SetChannelEnabled("FlowManager", on);
            }));

            _langButton = CatzTools.Editor.CatzEditorStyles.CreateInfoBarLangButton(SceneFlowLocale.LangToggle, () =>
            {
                SceneFlowLocale.Toggle();
                RebuildUI();
            });
            bar.Add(_langButton);

            root.Add(bar);
        }

        /// <summary>
        /// 更新 FlowManager 資訊列
        /// </summary>
        private void UpdateFlowManagerBar()
        {
            if (_flowManagerStatusLabel == null) return;

            // 用實際檔案存在判斷，避免 Missing Reference 誤判
            string path = BlueprintData.flowManagerScenePath;
            bool hasScene = BlueprintData.flowManagerScene != null
                && !string.IsNullOrEmpty(path)
                && System.IO.File.Exists(path);

            string startScene = BlueprintData.startSceneName;
            string startInfo = string.IsNullOrEmpty(startScene) ? SceneFlowLocale.StatusNone : startScene;

            if (hasScene)
            {
                _flowManagerStatusLabel.text = SceneFlowLocale.StatusReady(startInfo);
                _flowManagerStatusLabel.style.color = string.IsNullOrEmpty(startScene)
                    ? new Color(0.9f, 0.7f, 0.2f)
                    : new Color(0.4f, 0.8f, 0.4f);
            }
            else
            {
                _flowManagerStatusLabel.text = SceneFlowLocale.StatusMissing;
                _flowManagerStatusLabel.style.color = new Color(0.9f, 0.7f, 0.2f);
            }
        }
        #endregion FlowManager 資訊列

        #region 工具列
        private void CreateToolbar(VisualElement root)
        {
            var toolbar = CatzTools.Editor.CatzEditorStyles.CreateToolbar();

            toolbar.Add(CatzTools.Editor.CatzEditorStyles.CreateToolbarButton(SceneFlowLocale.ToolAddScene, () => AddNewScene()));
            toolbar.Add(CatzTools.Editor.CatzEditorStyles.CreateToolbarSpacer());

            toolbar.Add(CatzTools.Editor.CatzEditorStyles.CreateToolbarButton(SceneFlowLocale.ToolGenerateAll, () => GenerateAllScenes()));
            // 同步到建置 / 從建置同步：搬到場景清單面板的「建置設定」標題列旁邊（場景清單才是建置設定的主要操作區）
            // 匯入/匯出/存讀預設：v0.7.9b 移除（實際無明確使用情境；需要時從 GDS 匯入走 InfoBar）

            toolbar.Add(CatzTools.Editor.CatzEditorStyles.CreateToolbarFlexSpacer());

            // 自動排列 / 置中 / 重新載入：搬到 MiniMap 標題列（留右側空間給面板切換）
            toolbar.Add(CatzTools.Editor.CatzEditorStyles.CreateToolbarButton(SceneFlowLocale.ToolSceneList, () => ToggleSceneListPanel()));
            toolbar.Add(CatzTools.Editor.CatzEditorStyles.CreateToolbarButton(SceneFlowLocale.ToolCover, () => ToggleCoverPanel()));

            root.Add(toolbar);
        }
        #endregion 工具列

        #region 狀態列
        private void CreateStatusBar(VisualElement root)
        {
            var bar = CatzTools.Editor.CatzEditorStyles.CreateStatusBar();

            _statusLabel = CatzTools.Editor.CatzEditorStyles.CreateStatusBarText();
            bar.Add(_statusLabel);

            var versionLabel = CatzTools.Editor.CatzEditorStyles.CreateStatusBarVersion(GameFlowVersion.DISPLAY);
            bar.Add(versionLabel);

            root.Add(bar);
        }

        private void UpdateStatusBar()
        {
            if (_statusLabel == null || BlueprintData?.nodes == null) return;

            int totalNodes = BlueprintData.nodes.Count;
            int totalEdges = BlueprintData.edges?.Count ?? 0;
            int linkedCount = BlueprintData.nodes.Count(n => n.sceneAsset != null);

            string fmStatus = BlueprintData.flowManagerScene != null ? "✓" : "⚠";

            _statusLabel.text = $"{SceneFlowLocale.StatusNodes}: {totalNodes} | " +
                $"{SceneFlowLocale.StatusEdges}: {totalEdges} | " +
                $"Linked: {linkedCount}/{totalNodes} | FlowManager: {fmStatus}";
        }

        private void OnGraphDataChanged()
        {
            UpdateStatusBar();
            SyncAllScenesToBuildSettings();
            AutoBackup();
            RefreshSceneListPanel();
        }
        #endregion 狀態列

        #region FlowManager 場景自動建立
        /// <summary>
        /// 確保 FlowManager 場景存在。不存在則自動建立。
        /// </summary>
        private void EnsureFlowManagerScene()
        {
            // 檢查現有參照是否還有效（檔案可能已被刪除）
            if (BlueprintData.flowManagerScene != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(BlueprintData.flowManagerScene);
                if (!string.IsNullOrEmpty(assetPath) && System.IO.File.Exists(assetPath))
                {
                    BlueprintData.flowManagerScenePath = assetPath;
                    return;
                }

                // 參照失效，清除
                BlueprintData.flowManagerScene = null;
            }

            // 嘗試從已知路徑載入
            string path = BlueprintData.flowManagerScenePath;
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                var existingAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (existingAsset != null)
                {
                    BlueprintData.flowManagerScene = existingAsset;
                    EditorUtility.SetDirty(BlueprintData);
                    AssetDatabase.SaveAssets();
                    return;
                }
            }

            // 搜尋專案中是否有 FlowManager 場景
            string[] guids = AssetDatabase.FindAssets("t:SceneAsset FlowManager");
            foreach (var guid in guids)
            {
                string foundPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(foundPath);
                if (asset != null && asset.name == "FlowManager")
                {
                    BlueprintData.flowManagerScene = asset;
                    BlueprintData.flowManagerScenePath = foundPath;
                    EditorUtility.SetDirty(BlueprintData);
                    AssetDatabase.SaveAssets();
                    return;
                }
            }

            // 完全不存在，自動建立
            CreateFlowManagerScene();
        }

        /// <summary>
        /// 建立 FlowManager 場景（含 GameObject + 腳本）
        /// </summary>
        private void CreateFlowManagerScene()
        {
            string path = "Assets/Scenes/FlowManager.unity";

            try
            {
            // 確保 Scenes 資料夾存在
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            // 開啟或建立 FlowManager 場景
            UnityEngine.SceneManagement.Scene fmScene;
            if (System.IO.File.Exists(path))
            {
                fmScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            else
            {
                fmScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                fmScene.name = "FlowManager";
            }

            // 清空場景內所有物件
            foreach (var go in fmScene.GetRootGameObjects())
                DestroyImmediate(go);

            // === FlowManager GameObject（管理器 + UI Camera）===
            var flowManagerObj = new GameObject("[FlowManager]");
            // Tag 讀 BlueprintData 設定（預設 Untagged 避免跟遊戲主相機衝突；
            // 純測試場景可在 Inspector 改為 MainCamera 讓 Camera.main 有值）
            var flowCamTag = BlueprintData != null && !string.IsNullOrWhiteSpace(BlueprintData.flowManagerCameraTag)
                ? BlueprintData.flowManagerCameraTag
                : "Untagged";
            try
            {
                flowManagerObj.tag = flowCamTag;
            }
            catch (UnityException)
            {
                CatzLogger.LogWarning("FlowManager",
                    $"[SceneFlow] Tag '{flowCamTag}' 不存在於 Tags & Layers 設定，FlowManager 相機維持 Untagged。");
                flowManagerObj.tag = "Untagged";
            }
            flowManagerObj.layer = LayerMask.NameToLayer("UI");
            flowManagerObj.AddComponent(typeof(FlowManager));
            var cam = flowManagerObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.depth = -1;
            cam.cullingMask = 1 << LayerMask.NameToLayer("UI"); // 只渲染 UI Layer
            flowManagerObj.AddComponent<AudioListener>();

            // === TransitionCanvas（轉場遮罩）===
            var canvasObj = new GameObject("[TransitionCanvas]");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // 最上層
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // UI 遮罩 Image（預設 Fade / Slide 用）
            var overlayObj = new GameObject("Overlay");
            overlayObj.transform.SetParent(canvasObj.transform, false);
            var overlayImage = overlayObj.AddComponent<UIImage>();
            overlayImage.color = new Color(0, 0, 0, 0);
            overlayImage.raycastTarget = false;
            var overlayRect = overlayObj.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayObj.SetActive(false);

            // Shader 遮罩 Image（CustomShader 轉場用）
            var shaderObj = new GameObject("ShaderOverlay");
            shaderObj.transform.SetParent(canvasObj.transform, false);
            var shaderImage = shaderObj.AddComponent<UIImage>();
            shaderImage.color = Color.white;
            shaderImage.raycastTarget = false;
            var shaderRect = shaderObj.GetComponent<RectTransform>();
            shaderRect.anchorMin = Vector2.zero;
            shaderRect.anchorMax = Vector2.one;
            shaderRect.offsetMin = Vector2.zero;
            shaderRect.offsetMax = Vector2.zero;
            shaderObj.SetActive(false);

            // TransitionController 掛在 Canvas 上
            var controller = canvasObj.AddComponent<TransitionController>();

            // 用 SerializedObject 設定私有欄位
            var so = new SerializedObject(controller);
            so.FindProperty("_overlay").objectReferenceValue = overlayImage;
            so.FindProperty("_shaderOverlay").objectReferenceValue = shaderImage;
            so.ApplyModifiedPropertiesWithoutUndo();

            // === CoverCanvas（Cover 共用容器，sortingOrder 在轉場遮罩之下）===
            var coverCanvasObj = new GameObject("[CoverCanvas]");
            var coverCanvas = coverCanvasObj.AddComponent<Canvas>();
            coverCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            coverCanvas.sortingOrder = 5000; // 高於遊戲 UI，低於轉場遮罩
            var coverScaler = coverCanvasObj.AddComponent<CanvasScaler>();
            coverScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            coverScaler.referenceResolution = new Vector2(1920, 1080);
            coverCanvasObj.AddComponent<GraphicRaycaster>();
            coverCanvasObj.AddComponent<CanvasGroup>();

            // === EventSystem（UI 點擊必要，掛在 FlowManager 上）===
            flowManagerObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
            flowManagerObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            flowManagerObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif

            // 用 SerializedObject 設定 FlowManager._coverCanvas
            var fmSO = new SerializedObject(flowManagerObj.GetComponent<FlowManager>());
            fmSO.FindProperty("_coverCanvas").objectReferenceValue = coverCanvasObj.transform;
            fmSO.ApplyModifiedPropertiesWithoutUndo();

            // 儲存場景
            EditorSceneManager.SaveScene(fmScene, path);

            // 更新藍圖資料
            BlueprintData.flowManagerScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            BlueprintData.flowManagerScenePath = path;
            EditorUtility.SetDirty(BlueprintData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 同步 Build Settings
            SyncAllScenesToBuildSettings();

            CatzLogger.Log("FlowManager", $"[SceneFlow] FlowManager 場景已重建：{path}");

            }
            catch (System.Exception e)
            {
                CatzLogger.LogError("FlowManager", $"[SceneFlow] FlowManager 重建失敗：{e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog(SceneFlowLocale.DlgRebuildFail, $"FlowManager rebuild failed:\n{e.Message}", SceneFlowLocale.DlgOk);
            }
        }

        /// <summary>
        /// 開啟 FlowManager 場景
        /// </summary>
        private void OpenFlowManagerScene()
        {
            if (BlueprintData.flowManagerScene == null)
            {
                EditorUtility.DisplayDialog(SceneFlowLocale.DlgError, SceneFlowLocale.DlgFmMissing, SceneFlowLocale.DlgOk);
                return;
            }

            string path = AssetDatabase.GetAssetPath(BlueprintData.flowManagerScene);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
            }
        }

        /// <summary>
        /// 從 FlowManager 場景啟動 Play Mode（退出後自動恢復原場景）
        /// </summary>
        private void PlayFromFlowManager()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            if (BlueprintData.flowManagerScene == null)
            {
                EditorUtility.DisplayDialog(SceneFlowLocale.DlgError, SceneFlowLocale.DlgFmMissing, SceneFlowLocale.DlgOk);
                return;
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            // 標記是由工具啟動的，domain reload 後仍能辨識
            SessionState.SetBool("SceneFlow_PlayFromTool", true);
            EditorSceneManager.playModeStartScene = BlueprintData.flowManagerScene;
            EditorApplication.isPlaying = true;
        }

        /// <summary>
        /// Domain reload 後自動清除 playModeStartScene，
        /// 確保原生 Play 按鈕不受影響。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RegisterPlayModeCleanup()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode &&
                    SessionState.GetBool("SceneFlow_PlayFromTool", false))
                {
                    EditorSceneManager.playModeStartScene = null;
                    SessionState.EraseBool("SceneFlow_PlayFromTool");
                }
            };
        }

        /// <summary>
        /// 根據 Play Mode 狀態切換按鈕外觀
        /// </summary>
        private void UpdatePlayButton()
        {
            if (_playButton == null) return;

            bool playing = EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode;
            var bg = playing ? new Color(0.7f, 0.15f, 0.15f) : new Color(0.2f, 0.45f, 0.2f);

            _playButton.text = playing ? SceneFlowLocale.BtnStop : SceneFlowLocale.BtnPlay;

            // 清除預設 Button 背景圖片，否則會蓋住 backgroundColor 和文字
            _playButton.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            _playButton.style.backgroundColor = bg;
            _playButton.style.color = Color.white;
            _playButton.style.borderTopWidth = 1;
            _playButton.style.borderBottomWidth = 1;
            _playButton.style.borderLeftWidth = 1;
            _playButton.style.borderRightWidth = 1;
            _playButton.style.borderTopColor = bg;
            _playButton.style.borderBottomColor = bg;
            _playButton.style.borderLeftColor = bg;
            _playButton.style.borderRightColor = bg;
        }
        #endregion FlowManager 場景自動建立

        #region 場景操作
        /// <summary>
        /// 新增場景 — 彈出命名視窗，確認後建立場景檔 + 節點 + 同步 Build Settings
        /// </summary>
        private void AddNewScene()
        {
            SceneNameInputWindow.Show((sceneName) =>
            {
                CreateSceneNodeWithCheck(sceneName, new Vector2(400, 300), SceneNodeType.Scene);
            });
        }

        /// <summary>
        /// 建立場景節點 — 只建節點（名稱+連線），不建場景檔。
        /// 場景檔在「開啟場景」或「一口氣產生全部」時才建立。
        /// </summary>
        private void CreateSceneNodeWithCheck(string sceneName, Vector2 position, SceneNodeType nodeType)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return;

            // 節點圖中已有同名節點
            if (BlueprintData.nodes.Any(n => n.sceneName == sceneName))
            {
                EditorUtility.DisplayDialog(SceneFlowLocale.DlgNameDuplicate,
                    SceneFlowLocale.DlgNameDuplicateMsg(sceneName), SceneFlowLocale.DlgOk);
                return;
            }

            // 只建節點，不建場景檔
            var node = _graphView.AddSceneNode(sceneName, position, nodeType);

            // 如果場景檔已存在，自動接入（僅 Scene 節點）
            if (nodeType == SceneNodeType.Scene)
            {
                string scenePath = $"Assets/Scenes/{sceneName}.unity";
                var existingAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (existingAsset != null)
                {
                    node.SceneData.sceneAsset = existingAsset;
                    EnsureSceneEvent(scenePath);
                    node.RefreshAppearance();
                    SyncAllScenesToBuildSettings();
                }
            }

            UpdateStatusBar();
            CatzLogger.Log("FlowManager", $"節點已建立：{sceneName}（場景檔待產生）");
        }

        /// <summary>
        /// 確保場景檔存在（不存在就建立，含 [SceneEvent]），然後開啟。
        /// </summary>
        private void EnsureAndOpenScene(SceneNode nodeData)
        {
            if (nodeData.nodeType != SceneNodeType.Scene) return;

            string scenePath = $"Assets/Scenes/{nodeData.sceneName}.unity";

            // 場景檔不存在 → 建立
            if (nodeData.sceneAsset == null)
            {
                var existingAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (existingAsset != null)
                {
                    // 檔案已存在但節點沒接 → 接入 + 確保有 SceneEvent
                    nodeData.sceneAsset = existingAsset;
                    EnsureSceneEvent(scenePath);
                }
                else
                {
                    // 建立新場景 + [SceneEvent]
                    CreateSceneFile(nodeData.sceneName, scenePath);
                    nodeData.sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                }

                EditorUtility.SetDirty(BlueprintData);
                AssetDatabase.SaveAssets();
                _graphView.RefreshAllNodeAppearance();
                UpdateStatusBar();
                SyncAllScenesToBuildSettings();
            }

            // 開啟場景
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
            }
        }

        /// <summary>
        /// 確保場景內有 [SceneEvent]，沒有就補建
        /// </summary>
        private void EnsureSceneEvent(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return;

            // 暫時以 Additive 開啟目標場景
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            if (!scene.IsValid()) return;

            // 檢查是否已有 SceneEvent
            bool hasSceneEvent = false;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.GetComponent<SceneEvent>() != null)
                { hasSceneEvent = true; break; }
            }

            SceneEvent sceneEventComp = null;
            bool modified = false;
            if (!hasSceneEvent)
            {
                var seObj = new GameObject("[SceneEvent]");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(seObj, scene);
                sceneEventComp = (SceneEvent)seObj.AddComponent(typeof(SceneEvent));
                modified = true;
                CatzLogger.Log("FlowManager", $"[SceneFlow] 已為場景補建 SceneEvent：{scenePath}");
            }
            else
            {
                foreach (var go in scene.GetRootGameObjects())
                {
                    sceneEventComp = go.GetComponent<SceneEvent>();
                    if (sceneEventComp != null) break;
                }
            }

            // 順手清掉本場景與 FlowManager 重複的共用單例（EventSystem / AudioListener / InputModule）。
            // SceneEvent 存在 = 這是 FlowManager 管理場景，這些單例由 FlowManager 提供。
            if (sceneEventComp != null)
            {
                int removed = sceneEventComp.CleanupSharedSingletons(silent: true);
                if (removed > 0)
                {
                    modified = true;
                    CatzLogger.Log("FlowManager", $"[SceneFlow] 場景 {scene.name}：清掉 {removed} 個與 FlowManager 重複的共用單例");
                }
            }

            if (modified)
                EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>
        /// 建立場景檔（含預設 Camera/Light + [SceneEvent]）
        /// </summary>
        private void CreateSceneFile(string sceneName, string scenePath)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            var newScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            // 自動建立 [SceneEvent]
            var sceneEventObj = new GameObject("[SceneEvent]");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sceneEventObj, newScene);
            sceneEventObj.AddComponent(typeof(SceneEvent));

            EditorSceneManager.SaveScene(newScene, scenePath);
            EditorSceneManager.CloseScene(newScene, true);

            AssetDatabase.Refresh();
            CatzLogger.Log("FlowManager", $"場景已建立：{scenePath}");
        }

        /// <summary>
        /// 一口氣產生全部未建立的場景檔
        /// </summary>
        private void GenerateAllScenes()
        {
            var missingScenes = BlueprintData.nodes
                .Where(n => n.nodeType == SceneNodeType.Scene && n.sceneAsset == null)
                .ToList();

            if (missingScenes.Count == 0)
            {
                EditorUtility.DisplayDialog(SceneFlowLocale.DlgAllReady, SceneFlowLocale.DlgAllReadyMsg, SceneFlowLocale.DlgOk);
                return;
            }

            if (!EditorUtility.DisplayDialog(SceneFlowLocale.DlgGenScenes,
                SceneFlowLocale.GenWillCreate(missingScenes.Count) +
                string.Join("\n", missingScenes.Select(n => $"  • {n.sceneName}")),
                SceneFlowLocale.DlgCreateAll, SceneFlowLocale.DlgCancel))
                return;

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            foreach (var node in missingScenes)
            {
                string scenePath = $"Assets/Scenes/{node.sceneName}.unity";

                // 檢查檔案是否已存在
                var existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (existing != null)
                {
                    node.sceneAsset = existing;
                    EnsureSceneEvent(scenePath);
                    continue;
                }

                CreateSceneFile(node.sceneName, scenePath);
                node.sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            }

            EditorUtility.SetDirty(BlueprintData);
            AssetDatabase.SaveAssets();
            _graphView.RefreshAllNodeAppearance();
            UpdateStatusBar();
            SyncAllScenesToBuildSettings();

            EditorUtility.DisplayDialog(SceneFlowLocale.DlgGenDone,
                SceneFlowLocale.GenCreated(missingScenes.Count), SceneFlowLocale.DlgOk);
        }

        /// <summary>
        /// 處理 GraphView 右鍵選單的新增場景請求
        /// </summary>
        private void OnRequestAddSceneFromGraph(Vector2 position, SceneNodeType nodeType)
        {
            string defaultName = nodeType switch
            {
                SceneNodeType.End => "EndPoint",
                SceneNodeType.PopCover => "NewCover",
                _ => "NewScene"
            };

            SceneNameInputWindow.Show((sceneName) =>
            {
                CreateSceneNodeWithCheck(sceneName, position, nodeType);
            }, defaultName);
        }

        /// <summary>
        /// 設定場景為起點 — 標記 isStartNode，同步 FlowManager._firstGameScene
        /// Start 只是場景節點上的標籤，不是獨立節點。
        /// </summary>
        private void OnSetSceneAsStart(SceneFlowNode targetNode)
        {
            string targetName = targetNode.SceneData.sceneName;

            // 1. 清除所有節點的 isStartNode
            foreach (var node in BlueprintData.nodes)
                node.isStartNode = false;

            // 2. 標記目標為起始場景
            targetNode.SceneData.isStartNode = true;

            // 3. 更新 startSceneName
            BlueprintData.startSceneName = targetName;

            // 4. 同步 FlowManager._firstGameScene
            SyncStartSceneToFlowManager(targetName);

            // 5. START 節點自動連線到起始場景
            _graphView.ConnectStartToScene(targetNode.SceneData);

            EditorUtility.SetDirty(BlueprintData);
            AssetDatabase.SaveAssets();
            UpdateStatusBar();
            UpdateFlowManagerBar();

            CatzLogger.Log("FlowManager", $"已設定 \"{targetName}\" 為起始場景，START 已自動連線");
        }

        /// <summary>
        /// 同步起始場景到 FlowManager 的 _firstGameScene
        /// </summary>
        private void SyncStartSceneToFlowManager(string sceneName)
        {
            if (BlueprintData.flowManagerScene == null) return;

            string fmPath = AssetDatabase.GetAssetPath(BlueprintData.flowManagerScene);
            if (string.IsNullOrEmpty(fmPath)) return;

            // 開啟 FlowManager 場景（Additive 不影響當前場景）
            var fmScene = EditorSceneManager.OpenScene(fmPath, OpenSceneMode.Additive);

            // 找到 FlowManager 物件並更新 _firstGameScene
            var rootObjects = fmScene.GetRootGameObjects();
            foreach (var obj in rootObjects)
            {
                var fm = obj.GetComponent<FlowManager>();
                if (fm != null)
                {
                    fm.FirstGameScene = sceneName;
                    EditorUtility.SetDirty(fm);
                    break;
                }
            }

            EditorSceneManager.SaveScene(fmScene);
            EditorSceneManager.CloseScene(fmScene, true);
        }

        /// <summary>
        /// 同步所有場景至 Build Settings（FlowManager 永遠第一個）。
        /// 自動觸發路徑（圖變動、加節點、產場景等）直接走，無確認對話框。
        /// </summary>
        private void SyncAllScenesToBuildSettings()
        {
            SyncToBuildSettingsCore();
            RefreshSceneListPanel();
        }

        /// <summary>
        /// 使用者主動點「同步到建置」按鈕 — 跳確認對話框後呼叫 Core。
        /// </summary>
        private void SyncToBuildSettings()
        {
            if (!EditorUtility.DisplayDialog(
                    SceneFlowLocale.DlgSyncToBuildTitle,
                    SceneFlowLocale.DlgSyncToBuildMsg,
                    SceneFlowLocale.DlgYes, SceneFlowLocale.DlgNo))
                return;

            SyncToBuildSettingsCore();
        }

        /// <summary>
        /// 實際同步邏輯（無對話框）。FlowManager 永遠第一筆，Cover 場景排尾，
        /// 其餘節點依藍圖順序。供使用者主動觸發與自動同步共用。
        /// </summary>
        private void SyncToBuildSettingsCore()
        {
            var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();

            // FlowManager 永遠第一個
            if (BlueprintData.flowManagerScene != null)
            {
                string fmPath = AssetDatabase.GetAssetPath(BlueprintData.flowManagerScene);
                buildScenes.Add(new EditorBuildSettingsScene(fmPath, true));
            }
            else
            {
                CatzLogger.LogWarning("FlowManager", "FlowManager 場景不存在，無法同步 Build Settings");
                return;
            }

            // 一般場景節點（排除 Start / End / Cover）
            foreach (var node in BlueprintData.nodes)
            {
                if (node.nodeType == SceneNodeType.Scene && node.sceneAsset != null)
                {
                    string path = AssetDatabase.GetAssetPath(node.sceneAsset);
                    if (!buildScenes.Any(s => s.path == path))
                        buildScenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            // Scene Cover（加載）排在最後
            foreach (var node in BlueprintData.nodes)
            {
                if (node.nodeType == SceneNodeType.PopCover &&
                    node.coverSourceType == CoverSourceType.Scene &&
                    node.coverSceneAsset != null)
                {
                    string coverPath = AssetDatabase.GetAssetPath(node.coverSceneAsset);
                    if (!string.IsNullOrEmpty(coverPath) && !buildScenes.Any(s => s.path == coverPath))
                        buildScenes.Add(new EditorBuildSettingsScene(coverPath, true));
                }
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        /// <summary>
        /// 從 Build Settings 載入
        /// </summary>
        private void LoadFromBuildSettings()
        {
            if (!EditorUtility.DisplayDialog(
                    SceneFlowLocale.DlgLoadFromBuildTitle,
                    SceneFlowLocale.DlgLoadFromBuildMsg,
                    SceneFlowLocale.DlgYes, SceneFlowLocale.DlgNo))
                return;

            BlueprintData.nodes.Clear();
            BlueprintData.edges.Clear();

            var buildScenes = EditorBuildSettings.scenes;
            int row = 0;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                if (string.IsNullOrEmpty(buildScenes[i].path)) continue;
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScenes[i].path);
                if (sceneAsset == null) continue;

                // FlowManager 不加入節點列表
                if (sceneAsset.name == "FlowManager")
                {
                    BlueprintData.flowManagerScene = sceneAsset;
                    BlueprintData.flowManagerScenePath = buildScenes[i].path;
                    continue;
                }

                var node = new SceneNode(sceneAsset.name);
                node.sceneAsset = sceneAsset;
                node.position = new Vector2(300, 50 + row * 120);
                BlueprintData.nodes.Add(node);
                EnsureSceneEvent(buildScenes[i].path);
                row++;
            }

            EditorUtility.SetDirty(BlueprintData);
            AssetDatabase.SaveAssets();
            _graphView.LoadBlueprintData(BlueprintData);
            UpdateStatusBar();
            UpdateFlowManagerBar();

            EditorUtility.DisplayDialog(SceneFlowLocale.DlgLoadDone,
                SceneFlowLocale.LoadBuildDone(BlueprintData.nodes.Count),
                SceneFlowLocale.DlgOk);
        }
        #endregion 場景操作

        // JSON 匯入/匯出 方法於 v0.7.9b 移除（實際無使用情境）

        #region Build Settings 場景清單側邊欄
        /// <summary>
        /// 建立場景清單側邊欄
        /// </summary>
        #region 左側屬性面板
        /// <summary>
        /// 建立左側屬性面板（選到節點/連線時顯示對應屬性）
        /// </summary>
        private void CreateInspectorPanel(VisualElement parent)
        {
            _inspectorPanel = new VisualElement();
            _inspectorPanel.style.width = 220 + 13;    // 220 有效內容寬 + 13 捲軸補償
            _inspectorPanel.style.minWidth = 220 + 13;
            _inspectorPanel.style.backgroundColor = new Color(0.18f, 0.18f, 0.2f);
            _inspectorPanel.style.borderRightWidth = 1;
            _inspectorPanel.style.borderRightColor = new Color(0.3f, 0.3f, 0.4f);

            // 標題
            var header = new VisualElement();
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.height = 26;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.Center;
            header.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f);

            var titleLabel = new Label(SceneFlowLocale.InspTitle);
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.8f, 0.8f, 0.9f);
            header.Add(titleLabel);
            _inspectorPanel.Add(header);

            // IMGUI 內容區（用 IMGUI 畫 EditorGUILayout，支援 ColorField 等）
            _inspectorIMGUI = new IMGUIContainer(DrawInspectorGUI);
            _inspectorIMGUI.style.flexGrow = 1;
            // padding 移至 IMGUI 層（BeginVertical 內）處理，避免 UIToolkit padding + ScrollView 座標錯亂
            _inspectorPanel.Add(_inspectorIMGUI);

            parent.Add(_inspectorPanel);
        }

        /// <summary>
        /// 繪製屬性面板 IMGUI 內容
        /// </summary>
        private void DrawInspectorGUI()
        {
            if (_selectedObject == null)
            {
                EditorGUILayout.LabelField(SceneFlowLocale.InspSelectHint, EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // 強制只允許垂直捲動，水平捲軸隱藏 — 內容必須在面板寬度內排版
            _inspectorScrollPos = GUILayout.BeginScrollView(
                _inspectorScrollPos, GUIStyle.none, GUI.skin.verticalScrollbar);
            // padding 補償：原 paddingLeft=8, paddingRight=20（捲軸空間）, paddingTop=8
            GUILayout.BeginVertical(GUILayout.Width(220 - SCROLLBAR_W));
            GUILayout.Space(8);

            if (_selectedObject is SceneNode node)
            {
                DrawNodeInspector(node);
            }
            else if (_selectedObject is SceneEdge edge)
            {
                DrawEdgeInspector(edge);
            }

            GUILayout.Space(8);
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 節點屬性面板
        /// </summary>
        private void DrawNodeInspector(SceneNode node)
        {
            // 標題
            EditorGUILayout.LabelField(node.nodeType == SceneNodeType.Start ? SceneFlowLocale.InspStartPoint : node.sceneName,
                EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (node.nodeType == SceneNodeType.Start)
            {
                EditorGUILayout.HelpBox(SceneFlowLocale.InspStartDesc, MessageType.Info);
                EditorGUILayout.Space(8);
                DrawFlowManagerSettings();
                EditorGUILayout.Space(8);
                DrawStartServiceManifest();
                return;
            }

            if (node.nodeType == SceneNodeType.End)
            {
                EditorGUILayout.HelpBox(SceneFlowLocale.InspEndDesc, MessageType.Info);
                return;
            }

            if (node.nodeType == SceneNodeType.PopCover)
            {
                DrawCoverInspector(node);
                return;
            }

            // 場景名稱
            EditorGUI.BeginChangeCheck();
            var newName = EditorGUILayout.TextField(SceneFlowLocale.InspSceneName, node.sceneName);
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(newName))
            {
                node.sceneName = newName;
                SaveAndRefresh();
            }

            // 場景資源
#if UNITY_EDITOR
            EditorGUI.BeginChangeCheck();
            var newAsset = (SceneAsset)EditorGUILayout.ObjectField(
                SceneFlowLocale.InspSceneAsset, node.sceneAsset, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                node.sceneAsset = newAsset;
                if (newAsset != null && string.IsNullOrEmpty(node.sceneName))
                    node.sceneName = newAsset.name;
                SaveAndRefresh();
            }
#endif

            EditorGUILayout.Space(4);

            // 起始場景標記
            bool isStart = node.isStartNode;
            EditorGUILayout.LabelField(SceneFlowLocale.InspStatus, isStart ? SceneFlowLocale.InspStartScene : SceneFlowLocale.InspNormalScene);

            EditorGUILayout.Space(4);

            // 描述
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField(SceneFlowLocale.InspDescription, EditorStyles.miniLabel);
            var newDesc = EditorGUILayout.TextArea(node.description ?? "", GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck())
            {
                node.description = newDesc;
                SaveAndRefresh();
            }

            EditorGUILayout.Space(8);

            // ── 場景預設轉場（Hybrid）──
            DrawNodeTransitionSection(node);

            EditorGUILayout.Space(4);

            // ── 自動開啟 Cover ──
            DrawAutoShowCovers(node);

            EditorGUILayout.Space(4);

            // 連線資訊
            if (BlueprintData != null)
            {
                var outEdges = BlueprintData.GetOutgoingEdges(node.id);
                var inEdges = BlueprintData.GetIncomingEdges(node.id);

                EditorGUILayout.LabelField(SceneFlowLocale.NodeEdgeCount(outEdges.Count, inEdges.Count),
                    EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space(8);

            // 操作按鈕
            EditorGUILayout.LabelField(SceneFlowLocale.InspOperations, EditorStyles.miniLabel);

#if UNITY_EDITOR
            if (node.sceneAsset == null)
            {
                // 場景不存在：顯示建立按鈕
                if (GUILayout.Button(SceneFlowLocale.InspCreateScene))
                {
                    EnsureAndOpenScene(node);
                    // 建立後不開啟，只建檔案和掛 SceneEvent
                    // EnsureAndOpenScene 會建立場景，但不會切過去如果只是要建立
                }
            }
            else
            {
                // 場景已存在：顯示開啟按鈕
                if (GUILayout.Button(SceneFlowLocale.InspOpenScene))
                {
                    string path = AssetDatabase.GetAssetPath(node.sceneAsset);
                    if (!string.IsNullOrEmpty(path))
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,
                            UnityEditor.SceneManagement.OpenSceneMode.Single);
                    }
                }
            }
#endif

            // 設為起點
            if (!node.isStartNode)
            {
                if (GUILayout.Button(SceneFlowLocale.InspSetStart))
                {
                    // 找到對應的 GraphView 節點
                    var flowNode = FindFlowNodeByData(node);
                    if (flowNode != null)
                        OnSetSceneAsStart(flowNode);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(SceneFlowLocale.InspSetStartDone, MessageType.None);
            }
        }

        /// <summary>
        /// 透過 SceneNode 找到 GraphView 上的 SceneFlowNode
        /// </summary>
        private SceneFlowNode FindFlowNodeByData(SceneNode data)
        {
            if (_graphView == null) return null;
            foreach (var element in _graphView.graphElements)
            {
                if (element is SceneFlowNode flowNode && flowNode.SceneData == data)
                    return flowNode;
            }
            return null;
        }

        /// <summary>
        /// 連線屬性面板（Hybrid 模式：預設用兩端場景的 defaultEnter/Exit；勾選「覆寫」則用 edge.transition）
        /// </summary>
        private void DrawEdgeInspector(SceneEdge edge)
        {
            var srcNode = BlueprintData?.FindNodeById(edge.source);
            var tgtNode = BlueprintData?.FindNodeById(edge.target);
            var srcName = srcNode?.sceneName ?? "?";
            var tgtName = tgtNode?.sceneName ?? "?";

            EditorGUILayout.LabelField($"{srcName} → {tgtName}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (edge.transition == null)
                edge.transition = new TransitionSettings();

            // ── 模式切換：場景預設 / 覆寫 ──
            EditorGUI.BeginChangeCheck();
            bool newOverride = DrawBinaryModeToggle(
                SceneFlowLocale.EdgeModeDefault, new Color(0.4f, 0.65f, 0.9f),
                SceneFlowLocale.EdgeModeOverride, new Color(0.9f, 0.75f, 0.2f),
                edge.useOverride);
            if (EditorGUI.EndChangeCheck())
            {
                edge.useOverride = newOverride;
                EditorUtility.SetDirty(BlueprintData);
                _graphView?.RefreshAllNodeAppearance(); // 線上標籤顯示/隱藏同步
            }

            EditorGUILayout.Space(6);

            if (!edge.useOverride)
            {
                // 預設模式：顯示唯讀預覽
                DrawEdgeDefaultPreview(srcNode, tgtNode);
            }
            else
            {
                // 覆寫模式：顯示完整轉場編輯器（黃底）
                EditorGUILayout.HelpBox(SceneFlowLocale.EdgeOverrideHint, MessageType.Warning);
                EditorGUILayout.Space(4);
                DrawTransitionEditor(edge.transition, new Color(0.9f, 0.75f, 0.2f, 0.12f));
            }
        }

        /// <summary>Edge 預設模式的唯讀預覽（顯示將實際播放的兩端設定）</summary>
        private void DrawEdgeDefaultPreview(SceneNode srcNode, SceneNode tgtNode)
        {
            EditorGUILayout.LabelField(SceneFlowLocale.EdgePreviewTitle, EditorStyles.boldLabel);
            GUILayout.Space(2);

            var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.DrawRect(rect, new Color(0.4f, 0.65f, 0.9f, 0.08f));

            string srcExit = FormatTransitionLabel(srcNode?.defaultExit);
            string tgtEnter = FormatTransitionLabel(tgtNode?.defaultEnter);
            EditorGUILayout.LabelField(SceneFlowLocale.EdgePreviewExit(srcNode?.sceneName ?? "START", srcExit), EditorStyles.miniLabel);
            EditorGUILayout.LabelField(SceneFlowLocale.EdgePreviewEnter(tgtNode?.sceneName ?? "?", tgtEnter), EditorStyles.miniLabel);
            GUILayout.Space(2);
            EditorGUILayout.LabelField(SceneFlowLocale.EdgePreviewHint, EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private static string FormatTransitionLabel(TransitionSettings t)
        {
            if (t == null) return SceneFlowLocale.TransNone;
            return t.type switch
            {
                TransitionType.None => SceneFlowLocale.TransNone,
                TransitionType.Fade => SceneFlowLocale.TransFade,
                TransitionType.SlideLeft => SceneFlowLocale.TransSlideL,
                TransitionType.SlideRight => SceneFlowLocale.TransSlideR,
                TransitionType.SlideUp => SceneFlowLocale.TransSlideU,
                TransitionType.SlideDown => SceneFlowLocale.TransSlideD,
                TransitionType.CustomShader when t.customMaterial != null =>
                    SceneFlowLocale.TransCustom(SceneFlowShaderPresets.GetDisplayName(t.customMaterial)),
                TransitionType.CustomShader => SceneFlowLocale.TransCustomNoMat,
                _ => t.type.ToString()
            };
        }

        // ──────────────────────────────────────────────────────────
        //  共用：Hybrid 轉場 UI helpers（v0.7.8b）
        // ──────────────────────────────────────────────────────────

        /// <summary>雙模式切換按鈕（回傳 true/false 代表右側被選）</summary>
        private bool DrawBinaryModeToggle(string leftLabel, Color leftColor, string rightLabel, Color rightColor, bool rightActive)
        {
            EditorGUILayout.BeginHorizontal();
            var prevBg = GUI.backgroundColor;

            GUI.backgroundColor = !rightActive ? leftColor : new Color(0.5f, 0.5f, 0.5f, 0.6f);
            var leftStyle = !rightActive ? EditorStyles.miniButtonLeft : EditorStyles.miniButtonLeft;
            if (GUILayout.Button(leftLabel, leftStyle, GUILayout.Height(22)))
                rightActive = false;

            GUI.backgroundColor = rightActive ? rightColor : new Color(0.5f, 0.5f, 0.5f, 0.6f);
            if (GUILayout.Button(rightLabel, EditorStyles.miniButtonRight, GUILayout.Height(22)))
                rightActive = true;

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
            return rightActive;
        }

        /// <summary>共用轉場編輯器（popup / duration / color / material / shader / 快捷 / 底色 tint）</summary>
        private void DrawTransitionEditor(TransitionSettings t, Color tintColor)
        {
            if (t == null) return;

            var bodyRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(bodyRect, tintColor);
            GUILayout.Space(4);

            var prevType = t.type;
            string[] transitionNames =
            {
                SceneFlowLocale.TransNone, SceneFlowLocale.TransFade,
                SceneFlowLocale.TransSlideL, SceneFlowLocale.TransSlideR,
                SceneFlowLocale.TransSlideU, SceneFlowLocale.TransSlideD,
                "Custom Shader"
            };
            int transitionIndex = (int)t.type;
            transitionIndex = EditorGUILayout.Popup(SceneFlowLocale.TransEffect, transitionIndex, transitionNames);
            t.type = (TransitionType)transitionIndex;
            t.duration = EditorGUILayout.Slider(SceneFlowLocale.TransDuration, t.duration, 0.1f, 3f);

            EditorGUILayout.Space(4);

            if (t.type != TransitionType.CustomShader)
                t.color = EditorGUILayout.ColorField(SceneFlowLocale.TransMaskColor, t.color);

            if (t.type == TransitionType.CustomShader)
            {
                var prevMat = t.customMaterial;
                t.customMaterial = (Material)EditorGUILayout.ObjectField(
                    SceneFlowLocale.TransShaderMat, t.customMaterial, typeof(Material), false);

                if (t.customMaterial == null)
                    EditorGUILayout.HelpBox(SceneFlowLocale.TransMatHint, MessageType.Info);

                if (t.customMaterial != prevMat)
                    t.shaderOverrides = null;
            }

            if (prevType == TransitionType.CustomShader && t.type != TransitionType.CustomShader)
                t.shaderOverrides = null;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(SceneFlowLocale.HelpBoxPreview(FormatTransitionLabel(t), t.duration), MessageType.None);

            if (t.type == TransitionType.CustomShader && t.customMaterial != null)
                DrawShaderOverrides(t);

            if (GUI.changed)
            {
                EditorUtility.SetDirty(BlueprintData);
                _graphView?.RefreshAllNodeAppearance(); // 轉場型別/材質變更 → 線上標籤文字要跟著變
            }

            EditorGUILayout.Space(8);
            DrawTransitionQuickPresets(t);

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        /// <summary>場景節點的預設進/出轉場區塊（頂部進/出切換 + 底色提示）</summary>
        private void DrawNodeTransitionSection(SceneNode node)
        {
            if (node.defaultEnter == null) node.defaultEnter = new TransitionSettings { type = TransitionType.Fade, duration = 1f, color = Color.black };
            if (node.defaultExit == null)  node.defaultExit  = new TransitionSettings { type = TransitionType.Fade, duration = 1f, color = Color.black };

            EditorGUILayout.LabelField(SceneFlowLocale.TransSectionDefault, EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // 模式切換（0=Enter 綠, 1=Exit 紅）
            EditorGUI.BeginChangeCheck();
            bool isExit = DrawBinaryModeToggle(
                SceneFlowLocale.TransModeEnter, new Color(0.35f, 0.75f, 0.4f),
                SceneFlowLocale.TransModeExit,  new Color(0.85f, 0.4f, 0.4f),
                _nodeTransMode == 1);
            if (EditorGUI.EndChangeCheck())
                _nodeTransMode = isExit ? 1 : 0;

            EditorGUILayout.Space(4);

            if (_nodeTransMode == 0)
            {
                EditorGUILayout.HelpBox(SceneFlowLocale.TransSceneHelpEnter + "\n" + SceneFlowLocale.TransSceneHelpHint, MessageType.None);
                EditorGUILayout.Space(2);
                DrawTransitionEditor(node.defaultEnter, new Color(0.5f, 0.9f, 0.6f, 0.12f));
            }
            else
            {
                EditorGUILayout.HelpBox(SceneFlowLocale.TransSceneHelpExit + "\n" + SceneFlowLocale.TransSceneHelpHint, MessageType.None);
                EditorGUILayout.Space(2);
                DrawTransitionEditor(node.defaultExit, new Color(0.95f, 0.5f, 0.5f, 0.12f));
            }
        }

        /// <summary>共用轉場快捷按鈕組</summary>
        private void DrawTransitionQuickPresets(TransitionSettings t)
        {
            EditorGUILayout.LabelField(SceneFlowLocale.TransUILabel, EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(SceneFlowLocale.PresetFade, EditorStyles.miniButton))
                ApplyQuickPreset(t, TransitionType.Fade, 0.5f, Color.black, null);
            if (GUILayout.Button(SceneFlowLocale.PresetWhiteFade, EditorStyles.miniButton))
                ApplyQuickPreset(t, TransitionType.Fade, 0.5f, Color.white, null);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(SceneFlowLocale.PresetSlideL, EditorStyles.miniButton))
                ApplyQuickPreset(t, TransitionType.SlideLeft, 0.5f, Color.black, null);
            if (GUILayout.Button(SceneFlowLocale.PresetSlideR, EditorStyles.miniButton))
                ApplyQuickPreset(t, TransitionType.SlideRight, 0.5f, Color.black, null);
            if (GUILayout.Button(SceneFlowLocale.PresetNone, EditorStyles.miniButton))
                ApplyQuickPreset(t, TransitionType.None, 0f, Color.black, null);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(SceneFlowLocale.TransCustomLabel, EditorStyles.miniLabel);
            var presets = SceneFlowShaderPresets.Presets;
            int cols = SceneFlowLocale.IsZH ? 3 : 2;
            for (int pi = 0; pi < presets.Length; pi++)
            {
                if (pi % cols == 0) EditorGUILayout.BeginHorizontal();

                var preset = presets[pi];
                if (GUILayout.Button(preset.DisplayName, EditorStyles.miniButton))
                {
                    var mat = SceneFlowShaderPresets.GetOrCreateMaterial(preset);
                    if (mat != null)
                        ApplyQuickPreset(t, TransitionType.CustomShader, 0.8f, Color.black, mat);
                }

                if (pi % cols == cols - 1 || pi == presets.Length - 1)
                    EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Cover 節點屬性面板
        /// </summary>
        private void DrawCoverInspector(SceneNode node)
        {
            // Cover 名稱
            EditorGUI.BeginChangeCheck();
            var newName = EditorGUILayout.TextField(SceneFlowLocale.InspSceneName, node.sceneName);
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(newName))
            {
                node.sceneName = newName;
                SaveAndRefresh();
            }

            EditorGUILayout.Space(4);

            // 來源類型
            EditorGUI.BeginChangeCheck();
            var newSourceType = (CoverSourceType)EditorGUILayout.EnumPopup(SceneFlowLocale.CoverSource, node.coverSourceType);
            if (EditorGUI.EndChangeCheck())
            {
                node.coverSourceType = newSourceType;
                SaveAndRefresh();
            }

            // Prefab 或場景參考
            if (node.coverSourceType == CoverSourceType.Prefab)
            {
                EditorGUI.BeginChangeCheck();
                var newPrefab = (GameObject)EditorGUILayout.ObjectField(
                    SceneFlowLocale.CoverPrefab, node.coverPrefab, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck())
                {
                    node.coverPrefab = newPrefab;
                    SaveAndRefresh();
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                var newAsset = (SceneAsset)EditorGUILayout.ObjectField(
                    SceneFlowLocale.CoverSceneAsset, node.coverSceneAsset, typeof(SceneAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    node.coverSceneAsset = newAsset;
                    node.coverSceneName = newAsset != null ? newAsset.name : "";
                    SaveAndRefresh();
                }
            }

            EditorGUILayout.Space(8);

            // 開啟動畫
            EditorGUI.BeginChangeCheck();
            node.coverOpenDuration = EditorGUILayout.Slider(
                SceneFlowLocale.CoverOpenAnim, node.coverOpenDuration, 0.1f, 1f);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(BlueprintData);

            // 關閉動畫
            EditorGUI.BeginChangeCheck();
            node.coverCloseDuration = EditorGUILayout.Slider(
                SceneFlowLocale.CoverCloseAnim, node.coverCloseDuration, 0.1f, 1f);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(BlueprintData);

            // 描述
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(SceneFlowLocale.InspDescription, EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            var newDesc = EditorGUILayout.TextArea(node.description ?? "", GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck())
            {
                node.description = newDesc;
                EditorUtility.SetDirty(BlueprintData);
            }

            if (GUI.changed)
                EditorUtility.SetDirty(BlueprintData);
        }

        /// <summary>
        /// 繪製場景節點的「自動開啟 Cover」清單
        /// </summary>
        private void DrawAutoShowCovers(SceneNode node)
        {
            if (node.autoShowCovers == null)
                node.autoShowCovers = new List<string>();

            EditorGUILayout.LabelField(SceneFlowLocale.InspAutoShowCovers, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(SceneFlowLocale.InspAutoShowCoversHint, MessageType.None);

            if (node.autoShowCovers.Count == 0)
            {
                EditorGUILayout.LabelField(SceneFlowLocale.InspAutoShowNone, EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < node.autoShowCovers.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"  {node.autoShowCovers[i]}", EditorStyles.miniLabel);
                    if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), GUIStyle.none,
                        GUILayout.Width(18), GUILayout.Height(18)))
                    {
                        node.autoShowCovers.RemoveAt(i);
                        EditorUtility.SetDirty(BlueprintData);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            // 新增按鈕（Dropdown 選擇可用的 Cover）
            var availableCovers = BlueprintData.nodes
                .Where(n => n.nodeType == SceneNodeType.PopCover)
                .Select(n => n.sceneName)
                .Where(n => !node.autoShowCovers.Contains(n))
                .ToList();

            if (availableCovers.Count > 0 && GUILayout.Button(SceneFlowLocale.InspAutoShowAdd, EditorStyles.miniButton))
            {
                var menu = new GenericMenu();
                foreach (var coverName in availableCovers)
                {
                    string captured = coverName;
                    menu.AddItem(new GUIContent(coverName), false, () =>
                    {
                        node.autoShowCovers.Add(captured);
                        EditorUtility.SetDirty(BlueprintData);
                    });
                }
                menu.ShowAsContext();
            }
            else if (availableCovers.Count == 0 && node.autoShowCovers.Count > 0)
            {
                EditorGUILayout.LabelField(SceneFlowLocale.InspAutoShowAllAdded, EditorStyles.centeredGreyMiniLabel);
            }
        }

        /// <summary>
        /// 繪製簡化版轉場設定（Cover 開關用）
        /// </summary>
        private void DrawSimpleTransitionSettings(TransitionSettings t)
        {
            t.type = (TransitionType)EditorGUILayout.EnumPopup(SceneFlowLocale.TransEffect, t.type);

            if (t.type != TransitionType.None)
            {
                t.duration = EditorGUILayout.Slider(SceneFlowLocale.TransDuration, t.duration, 0.1f, 3f);

                if (t.type != TransitionType.CustomShader)
                    t.color = EditorGUILayout.ColorField(SceneFlowLocale.TransMaskColor, t.color);

                if (t.type == TransitionType.CustomShader)
                {
                    t.customMaterial = (Material)EditorGUILayout.ObjectField(
                        SceneFlowLocale.TransShaderMat, t.customMaterial, typeof(Material), false);
                }
            }
        }

        /// <summary>
        /// 繪製 Shader 屬性覆蓋面板
        /// </summary>
        private void DrawShaderOverrides(TransitionSettings t)
        {
            var mat = t.customMaterial;
            if (mat == null) return;

            // 清單與 Shader 不符時自動重建（保留既有值）
            if (!IsOverridesValid(t, mat))
            {
                SyncOverridesWithShader(t, mat);
                EditorUtility.SetDirty(BlueprintData);
            }

            // 同步 displayName（Shader 改英文後舊資料可能還是中文）
            RefreshOverrideDisplayNames(t, mat);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(SceneFlowLocale.TransCustomProps(t.shaderOverrides.Count), EditorStyles.boldLabel);

            // 縮小 label 寬度，讓拉桿有更多空間
            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 80f;

            // 取得 _ShapeType 的值（用於條件顯示 _ShapeTex）
            float shapeTypeVal = -1;
            var shapeTypeOverride = t.shaderOverrides.Find(x => x.propertyName == "_ShapeType");
            if (shapeTypeOverride != null) shapeTypeVal = shapeTypeOverride.floatValue;

            foreach (var o in t.shaderOverrides)
            {
                // _ShapeTex 只在 _ShapeType == Custom(4) 時顯示
                if (o.propertyName == "_ShapeTex" && !Mathf.Approximately(shapeTypeVal, 4f))
                    continue;

                // 標籤在上面，拉桿在下面各佔一行
                // Shader 屬性原文為英文，中文查翻譯表
                string propLabel = SceneFlowLocale.IsZH
                    ? ShaderPropertyLocale.GetZH(o.displayName)
                    : o.displayName;
                EditorGUILayout.LabelField(propLabel, EditorStyles.miniLabel);
                switch (o.valueType)
                {
                    case ShaderPropertyValueType.Float:
                        var sliderRect = EditorGUILayout.GetControlRect(false, 18f);
                        o.floatValue = GUI.HorizontalSlider(
                            new Rect(sliderRect.x, sliderRect.y, sliderRect.width - 50, sliderRect.height),
                            o.floatValue, o.rangeMin, o.rangeMax);
                        // 右側數值顯示
                        float newVal = EditorGUI.FloatField(
                            new Rect(sliderRect.xMax - 46, sliderRect.y, 46, sliderRect.height),
                            o.floatValue);
                        o.floatValue = Mathf.Clamp(newVal, o.rangeMin, o.rangeMax);
                        break;
                    case ShaderPropertyValueType.Enum:
                        if (o.enumNames != null && o.enumNames.Length > 0)
                        {
                            // 找到目前 floatValue 對應的 index
                            int curIdx = 0;
                            if (o.enumValues != null)
                            {
                                for (int ei = 0; ei < o.enumValues.Length; ei++)
                                {
                                    if (Mathf.Approximately(o.floatValue, o.enumValues[ei]))
                                    { curIdx = ei; break; }
                                }
                            }
                            int newIdx = EditorGUILayout.Popup(curIdx, o.enumNames);
                            if (o.enumValues != null && newIdx < o.enumValues.Length)
                                o.floatValue = o.enumValues[newIdx];
                        }
                        break;
                    case ShaderPropertyValueType.Toggle:
                        bool togVal = o.floatValue > 0.5f;
                        togVal = EditorGUILayout.Toggle(togVal);
                        o.floatValue = togVal ? 1f : 0f;
                        break;
                    case ShaderPropertyValueType.Texture:
                        o.textureValue = (Texture)EditorGUILayout.ObjectField(
                            o.textureValue, typeof(Texture), false,
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        break;
                    case ShaderPropertyValueType.Color:
                        o.colorValue = EditorGUILayout.ColorField(o.colorValue);
                        break;
                }
                EditorGUILayout.Space(2);
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;
        }

        /// <summary>
        /// 檢查覆蓋清單是否與 Shader 屬性匹配
        /// </summary>
        private bool IsOverridesValid(TransitionSettings t, Material mat)
        {
            if (t.shaderOverrides == null || t.shaderOverrides.Count == 0)
                return false;

            var shader = mat.shader;
            int idx = 0;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string propName = shader.GetPropertyName(i);
                if (propName == "_Progress" || propName == "_MainTex" || propName == "_ScreenTex")
                    continue;
                if ((shader.GetPropertyFlags(i) & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0)
                    continue;
                var propType = shader.GetPropertyType(i);
                if (propType != UnityEngine.Rendering.ShaderPropertyType.Color &&
                    propType != UnityEngine.Rendering.ShaderPropertyType.Float &&
                    propType != UnityEngine.Rendering.ShaderPropertyType.Range &&
                    propType != UnityEngine.Rendering.ShaderPropertyType.Texture)
                    continue;

                if (idx >= t.shaderOverrides.Count) return false;
                if (t.shaderOverrides[idx].propertyName != propName) return false;
                idx++;
            }
            return idx == t.shaderOverrides.Count;
        }

        /// <summary>
        /// 確保覆蓋清單與 Shader 屬性同步（屬性增減、名稱變更都會自動修正）
        /// </summary>
        private void SyncOverridesWithShader(TransitionSettings t, Material mat)
        {
            var shader = mat.shader;
            var newList = new List<ShaderPropertyOverride>();

            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string propName = shader.GetPropertyName(i);
                var propType = shader.GetPropertyType(i);

                if (propName == "_Progress" || propName == "_MainTex" || propName == "_ScreenTex")
                    continue;
                if ((shader.GetPropertyFlags(i) & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0)
                    continue;
                if (propType != UnityEngine.Rendering.ShaderPropertyType.Color &&
                    propType != UnityEngine.Rendering.ShaderPropertyType.Float &&
                    propType != UnityEngine.Rendering.ShaderPropertyType.Range &&
                    propType != UnityEngine.Rendering.ShaderPropertyType.Texture)
                    continue;

                string display = shader.GetPropertyDescription(i);

                // 從舊覆蓋清單找既有值
                var existing = t.shaderOverrides?.Find(x => x.propertyName == propName);

                switch (propType)
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        newList.Add(new ShaderPropertyOverride
                        {
                            displayName = display,
                            propertyName = propName,
                            valueType = ShaderPropertyValueType.Texture,
                            textureValue = existing?.textureValue
                        });
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        newList.Add(new ShaderPropertyOverride
                        {
                            displayName = display,
                            propertyName = propName,
                            valueType = ShaderPropertyValueType.Color,
                            // 優先用既有值 → 否則用 Shader 預設值（不從 Material 讀，避免交叉污染）
                            colorValue = existing != null
                                ? existing.colorValue
                                : shader.GetPropertyDefaultVectorValue(i)
                        });
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                    {
                        // 偵測是否為 Enum 屬性（[Enum(...)] 標記）
                        var attrs = shader.GetPropertyAttributes(i);
                        string[] enumNames = null;
                        float[] enumVals = null;
                        bool isEnum = false;

                        if (attrs != null)
                        {
                            foreach (var attr in attrs)
                            {
                                if (!attr.StartsWith("Enum(")) continue;

                                isEnum = true;
                                // 解析 "Enum(Heart,0,Star,1,...)"
                                string inner = attr.Substring(5, attr.Length - 6);
                                string[] parts = inner.Split(',');
                                int count = parts.Length / 2;
                                enumNames = new string[count];
                                enumVals = new float[count];
                                for (int e = 0; e < count; e++)
                                {
                                    enumNames[e] = parts[e * 2].Trim();
                                    float.TryParse(parts[e * 2 + 1].Trim(),
                                        System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        out enumVals[e]);
                                }
                                break;
                            }
                        }

                        // 偵測是否為 Toggle 屬性（[Toggle] 標記）
                        bool isToggle = false;
                        if (attrs != null)
                        {
                            foreach (var attr in attrs)
                            {
                                if (attr == "Toggle" || attr.StartsWith("Toggle("))
                                { isToggle = true; break; }
                            }
                        }

                        if (isEnum)
                        {
                            newList.Add(new ShaderPropertyOverride
                            {
                                displayName = display,
                                propertyName = propName,
                                valueType = ShaderPropertyValueType.Enum,
                                floatValue = existing != null
                                    ? existing.floatValue
                                    : shader.GetPropertyDefaultFloatValue(i),
                                enumNames = enumNames,
                                enumValues = enumVals
                            });
                        }
                        else if (isToggle)
                        {
                            newList.Add(new ShaderPropertyOverride
                            {
                                displayName = display,
                                propertyName = propName,
                                valueType = ShaderPropertyValueType.Toggle,
                                floatValue = existing != null
                                    ? existing.floatValue
                                    : shader.GetPropertyDefaultFloatValue(i)
                            });
                        }
                        else
                        {
                            float min = 0, max = 1;
                            if (propType == UnityEngine.Rendering.ShaderPropertyType.Range)
                            {
                                var range = shader.GetPropertyRangeLimits(i);
                                min = range.x;
                                max = range.y;
                            }
                            newList.Add(new ShaderPropertyOverride
                            {
                                displayName = display,
                                propertyName = propName,
                                valueType = ShaderPropertyValueType.Float,
                                floatValue = existing != null
                                    ? existing.floatValue
                                    : shader.GetPropertyDefaultFloatValue(i),
                                rangeMin = min,
                                rangeMax = max
                            });
                        }
                        break;
                    }
                }
            }

            t.shaderOverrides = newList;
            CatzLogger.Log("FlowManager", $"[SceneFlow] SyncOverrides 重建完成：{newList.Count} 個屬性");
            foreach (var item in newList)
                CatzLogger.Log("FlowManager", $"  → {item.propertyName} ({item.valueType}) display=\"{item.displayName}\" range=[{item.rangeMin},{item.rangeMax}]");
        }

        /// <summary>
        /// 快速套用預設（UI 和 Shader 通用）
        /// </summary>
        /// <summary>
        /// 將序列化的 displayName 同步為 Shader 最新值（解決 Shader 改英文後舊資料殘留中文）
        /// </summary>
        private void RefreshOverrideDisplayNames(TransitionSettings t, Material mat)
        {
            if (t.shaderOverrides == null || mat == null) return;
            var shader = mat.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string propName = shader.GetPropertyName(i);
                string display = shader.GetPropertyDescription(i);
                var o = t.shaderOverrides.Find(x => x.propertyName == propName);
                if (o != null && o.displayName != display)
                {
                    o.displayName = display;
                    EditorUtility.SetDirty(BlueprintData);
                }
            }
        }

        private void ApplyQuickPreset(TransitionSettings t, TransitionType type,
            float duration, Color color, Material mat)
        {
            t.type = type;
            t.duration = duration;
            t.color = color;
            t.customMaterial = mat;
            t.shaderOverrides = null;

            if (type == TransitionType.CustomShader && mat != null)
                SyncOverridesWithShader(t, mat);

            CatzLogger.Log("FlowManager", $"[SceneFlow] ApplyQuickPreset: overrides={t.shaderOverrides?.Count ?? -1}");
            EditorUtility.SetDirty(BlueprintData);
            CatzLogger.Log("FlowManager", $"[SceneFlow] SaveAssets 前 overrides={t.shaderOverrides?.Count ?? -1}");
            AssetDatabase.SaveAssets();
            CatzLogger.Log("FlowManager", $"[SceneFlow] SaveAssets 後 overrides={t.shaderOverrides?.Count ?? -1}");
            _graphView?.RefreshAllNodeAppearance();
            _inspectorIMGUI?.MarkDirtyRepaint();
            UpdateStatusBar();
            Repaint();
        }

        /// <summary>
        /// 選取變更處理
        /// </summary>
        private void OnGraphSelectionChanged(object selected)
        {
            _selectedObject = selected;
            _inspectorIMGUI?.MarkDirtyRepaint();
        }

        /// <summary>
        /// 儲存並刷新
        /// </summary>
        private void SaveAndRefresh()
        {
            if (BlueprintData != null)
            {
                EditorUtility.SetDirty(BlueprintData);
                AssetDatabase.SaveAssets();
            }
            _graphView?.RefreshAllNodeAppearance();
            UpdateStatusBar();
        }

        #region Start 節點 — FlowManager 場景設定

        /// <summary>
        /// 繪製 FlowManager 場景設定區塊（在 Start 節點 Inspector 內）。
        /// 目前含 FlowManager 相機 Tag 的選擇器。變更後需按 Rebuild 才會套用到實際場景檔。
        /// </summary>
        private void DrawFlowManagerSettings()
        {
            if (BlueprintData == null) return;

            EditorGUILayout.LabelField(SceneFlowLocale.InspFlowManagerSection, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(SceneFlowLocale.InspFlowCameraTagHint, MessageType.None);

            EditorGUI.BeginChangeCheck();
            string currentTag = string.IsNullOrWhiteSpace(BlueprintData.flowManagerCameraTag)
                ? "Untagged"
                : BlueprintData.flowManagerCameraTag;
            string newTag = EditorGUILayout.TagField(SceneFlowLocale.InspFlowCameraTag, currentTag);
            if (EditorGUI.EndChangeCheck() && newTag != BlueprintData.flowManagerCameraTag)
            {
                Undo.RecordObject(BlueprintData, "Change FlowManager Camera Tag");
                BlueprintData.flowManagerCameraTag = newTag;
                EditorUtility.SetDirty(BlueprintData);
            }
        }

        #endregion Start 節點 — FlowManager 場景設定

        #region Start 節點 — ServiceLocator 啟動清單

        /// <summary>
        /// 繪製 Start 節點的 ServiceLocator 啟動清單編輯器。
        /// 資料儲存在 BlueprintData.startServices（含 priority override），
        /// runtime 由 ServiceLocator.Bootstrap 從 Resources/SceneBlueprintData 載入並套用。
        /// </summary>
        private void DrawStartServiceManifest()
        {
            if (BlueprintData == null) return;

            var manifest = BlueprintData.GetOrMigrateStartServices();
            // 從 cache 讀；_serviceCache == null 表示還沒掃過或被強制清掉
            if (_serviceCache == null)
                _serviceCache = ServiceLocator.EnumerateRegisterableServices();
            var discovered = _serviceCache;

            EditorGUILayout.LabelField(SceneFlowLocale.InspServicesTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(SceneFlowLocale.InspServicesHint, MessageType.None);

            // ── 操作列（拆成兩行，避免 4 顆中文按鈕最小寬度超過右欄 180px）──
            // 第一行：重新掃描 + 依 Priority 排序（長按鈕）
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(SceneFlowLocale.InspServicesScan,
                SceneFlowLocale.InspServicesScanTip)))
            {
                // 強制清 cache → 下次繪製會重新反射掃描
                _serviceCache = null;
                Repaint();
            }
            if (GUILayout.Button(new GUIContent(SceneFlowLocale.InspServicesSortByPriority,
                SceneFlowLocale.InspServicesSortByPriorityTip)))
            {
                SortManifestByPriority(discovered);
            }
            EditorGUILayout.EndHorizontal();

            // 第二行：全選 + 清空（短按鈕）
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(SceneFlowLocale.InspServicesSelectAll,
                SceneFlowLocale.InspServicesSelectAllTip)))
            {
                SelectAllServices(discovered);
            }
            if (GUILayout.Button(SceneFlowLocale.InspServicesSelectNone))
            {
                manifest.Clear();
                BlueprintData.startServiceTypeNames?.Clear();
                SaveAndRefresh();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── 顯示清單：已勾選依 manifest 順序在前；未勾選依 priority 在後 ──
            var displayItems = BuildDisplayList(manifest, discovered);

            int checkedCount = 0;
            foreach (var item in displayItems)
                if (item.isChecked) checkedCount++;

            EditorGUILayout.LabelField(
                SceneFlowLocale.InspServicesCount(checkedCount, displayItems.Count),
                EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            if (displayItems.Count == 0)
            {
                EditorGUILayout.LabelField(SceneFlowLocale.InspServicesNoneScanned, EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // ── 清單本體 ──
            // helpBox 寬度由 parent BeginVertical(Width(220 - SCROLLBAR_W)) 自然決定，
            // 不再手動指定寬度（舊的 panelWidth - 56 補償是 UIToolkit padding 時代的遺留，
            // 現在會導致 entry 比上方按鈕窄 ~30px）
            int orderInsideManifest = 0;
            int moveUp = -1, moveDown = -1;
            bool dirty = false;

            for (int i = 0; i < displayItems.Count; i++)
            {
                var item = displayItems[i];

                // 找對應的 manifest 條目（已勾選才有）
                ServiceManifestEntry entry = null;
                int manifestIdx = -1;
                if (item.isChecked)
                {
                    for (int m = 0; m < manifest.Count; m++)
                    {
                        if (manifest[m].typeName == item.fullName || manifest[m].typeName == item.rawName)
                        {
                            entry = manifest[m];
                            manifestIdx = m;
                            break;
                        }
                    }
                }

                // 整列用 helpBox vertical 包起來，寬度由 parent BeginVertical 決定
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // ── Sub-row 1：Toggle + idx + Priority 欄 + ↑↓ 按鈕（靠右）──
                EditorGUILayout.BeginHorizontal();

                bool nowChecked = EditorGUILayout.Toggle(item.isChecked, GUILayout.Width(16));
                if (nowChecked != item.isChecked)
                {
                    if (nowChecked && item.fullName != null)
                    {
                        manifest.Add(new ServiceManifestEntry(item.fullName));
                    }
                    else
                    {
                        if (item.fullName != null)
                            manifest.RemoveAll(e => e.typeName == item.fullName);
                        if (item.rawName != null)
                            manifest.RemoveAll(e => e.typeName == item.rawName);
                    }
                    dirty = true;
                }

                if (item.isChecked)
                {
                    orderInsideManifest++;
                    GUILayout.Label($"{orderInsideManifest}.", EditorStyles.miniLabel, GUILayout.Width(18));
                }
                else
                {
                    GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(18));
                }

                int effectivePrio = entry != null
                    ? entry.GetEffectivePriority(item.attrPriority)
                    : item.attrPriority;
                var prioContent = new GUIContent(string.Empty, "Priority（空 = 沿用 attribute 原值）");
                // 極窄 IntField（30px），IMGUI 會正常處理三位數以內
                int newPrio = EditorGUILayout.IntField(effectivePrio, GUILayout.Width(30));
                if (newPrio != effectivePrio && entry != null)
                {
                    if (newPrio == item.attrPriority)
                        entry.priorityOverride = string.Empty;
                    else
                        entry.priorityOverride = newPrio.ToString();
                    dirty = true;
                }

                if (entry != null && entry.HasOverride)
                {
                    GUILayout.Label("*", new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(1f, 0.85f, 0.3f) },
                        fontStyle = FontStyle.Bold,
                    }, GUILayout.Width(10));
                }

                GUILayout.FlexibleSpace();

                // ↑↓ 移動按鈕（靠右，與 Priority 同排；未勾選或只剩一條時不顯示）
                if (item.isChecked && checkedCount > 1 && manifestIdx >= 0)
                {
                    using (new EditorGUI.DisabledScope(manifestIdx <= 0))
                    {
                        if (GUILayout.Button(SceneFlowLocale.InspServicesMoveUp, EditorStyles.miniButtonLeft, GUILayout.Width(22), GUILayout.Height(16)))
                            moveUp = manifestIdx;
                    }
                    using (new EditorGUI.DisabledScope(manifestIdx >= manifest.Count - 1))
                    {
                        if (GUILayout.Button(SceneFlowLocale.InspServicesMoveDown, EditorStyles.miniButtonRight, GUILayout.Width(22), GUILayout.Height(16)))
                            moveDown = manifestIdx;
                    }
                }

                EditorGUILayout.EndHorizontal();

                // ── Sub-row 2：服務短名，整行獨佔（wordWrap 讓長名自動換行不被截斷）──
                var nameStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontStyle = FontStyle.Bold,
                };
                if (item.isMissing) nameStyle.normal.textColor = new Color(0.95f, 0.6f, 0.3f);
                else if (!item.isChecked) nameStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                EditorGUILayout.LabelField(item.shortName, nameStyle);

                // ── Sub-row 3：描述（支援 \n 換行列點，自動撐高）──
                if (!string.IsNullOrEmpty(item.description))
                {
                    var descStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        richText = false,
                        normal = { textColor = item.isChecked
                            ? new Color(0.65f, 0.65f, 0.65f)
                            : new Color(0.45f, 0.45f, 0.45f) },
                    };
                    GUILayout.Label(item.description, descStyle);
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(2);
            }

            // 套用順序變更
            if (moveUp > 0)
            {
                (manifest[moveUp - 1], manifest[moveUp]) = (manifest[moveUp], manifest[moveUp - 1]);
                dirty = true;
            }
            else if (moveDown >= 0 && moveDown < manifest.Count - 1)
            {
                (manifest[moveDown + 1], manifest[moveDown]) = (manifest[moveDown], manifest[moveDown + 1]);
                dirty = true;
            }

            if (dirty) SaveAndRefresh();
        }

        /// <summary>顯示用 row 結構</summary>
        private struct ServiceRow
        {
            public bool isChecked;
            public bool isMissing;
            /// <summary>反射到的 Type FullName（missing 時為 null）</summary>
            public string fullName;
            /// <summary>manifest 裡儲存的原始字串（可能是 short name 或 FullName）</summary>
            public string rawName;
            /// <summary>顯示用短名（不含 priority 前綴；priority 由 IntField 顯示）</summary>
            public string shortName;
            /// <summary>反射讀到的 [AutoRegister] priority 原值（給 IntField 當 fallback 與「清除 override」判定）</summary>
            public int attrPriority;
            /// <summary>簡短說明</summary>
            public string description;
        }

        /// <summary>建構顯示清單：勾選的依 manifest 順序在前，未勾選依 priority 在後</summary>
        private static List<ServiceRow> BuildDisplayList(
            List<ServiceManifestEntry> manifest,
            List<(System.Type type, AutoRegisterAttribute attr)> discovered)
        {
            var rows = new List<ServiceRow>();
            var usedFullNames = new HashSet<string>();

            // 1) 已勾選的（依 manifest 順序）
            foreach (var entry in manifest)
            {
                var name = entry?.typeName;
                if (string.IsNullOrEmpty(name)) continue;

                var found = discovered.Find(e => e.type.FullName == name || e.type.Name == name);
                if (found.type != null)
                {
                    rows.Add(new ServiceRow
                    {
                        isChecked = true,
                        isMissing = false,
                        fullName = found.type.FullName,
                        rawName = name,
                        shortName = ServiceDisplayName(found.type, found.attr),
                        attrPriority = found.attr.Priority,
                        description = found.attr.Description,
                    });
                    usedFullNames.Add(found.type.FullName);
                }
                else
                {
                    rows.Add(new ServiceRow
                    {
                        isChecked = true,
                        isMissing = true,
                        fullName = null,
                        rawName = name,
                        shortName = SceneFlowLocale.InspServicesMissing(ShortName(name)),
                        attrPriority = 0,
                        description = null,
                    });
                }
            }

            // 2) 未勾選的（依 priority 排序）
            var unchecked_ = new List<(System.Type type, AutoRegisterAttribute attr)>();
            foreach (var entry in discovered)
            {
                if (!usedFullNames.Contains(entry.type.FullName))
                    unchecked_.Add(entry);
            }
            unchecked_.Sort((a, b) => a.attr.Priority.CompareTo(b.attr.Priority));

            foreach (var (type, attr) in unchecked_)
            {
                rows.Add(new ServiceRow
                {
                    isChecked = false,
                    isMissing = false,
                    fullName = type.FullName,
                    rawName = type.FullName,
                    shortName = ServiceDisplayName(type, attr),
                    attrPriority = attr.Priority,
                    description = attr.Description,
                });
            }

            return rows;
        }

        /// <summary>全選：把所有反射到的服務按 priority 順序填入 manifest</summary>
        private void SelectAllServices(List<(System.Type type, AutoRegisterAttribute attr)> discovered)
        {
            if (BlueprintData == null) return;
            var sorted = new List<(System.Type type, AutoRegisterAttribute attr)>(discovered);
            sorted.Sort((a, b) => a.attr.Priority.CompareTo(b.attr.Priority));
            BlueprintData.startServices = sorted.ConvertAll(e => new ServiceManifestEntry(e.type.FullName));
            SaveAndRefresh();
        }

        /// <summary>把當前 manifest 內的項目按 effective priority（override 優先）重新排序</summary>
        private void SortManifestByPriority(List<(System.Type type, AutoRegisterAttribute attr)> discovered)
        {
            if (BlueprintData == null) return;
            var manifest = BlueprintData.GetOrMigrateStartServices();

            var withPriority = new List<(ServiceManifestEntry entry, int priority)>();
            foreach (var entry in manifest)
            {
                var name = entry?.typeName;
                if (string.IsNullOrEmpty(name)) continue;
                var found = discovered.Find(e => e.type.FullName == name || e.type.Name == name);
                int attrP = found.type != null ? found.attr.Priority : int.MaxValue;
                int effective = entry.GetEffectivePriority(attrP);
                withPriority.Add((entry, effective));
            }
            withPriority.Sort((a, b) => a.priority.CompareTo(b.priority));
            BlueprintData.startServices = withPriority.ConvertAll(t => t.entry);
            SaveAndRefresh();
        }

        /// <summary>從 FullName 取最後一段做顯示用</summary>
        private static string ShortName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return string.Empty;
            int dot = fullName.LastIndexOf('.');
            return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
        }

        /// <summary>取得服務顯示名稱：優先用 [AutoRegister].DisplayName，fallback type.Name</summary>
        private static string ServiceDisplayName(System.Type type, AutoRegisterAttribute attr)
        {
            if (!string.IsNullOrEmpty(attr?.DisplayName)) return attr.DisplayName;
            return type?.Name ?? string.Empty;
        }

        #endregion Start 節點 — ServiceLocator 啟動清單

        #endregion 左側屬性面板

        private void CreateSceneListPanel(VisualElement parent)
        {
            _sceneListPanel = new VisualElement();
            _sceneListPanel.style.width = 220 + 13;    // 220 有效內容寬 + 13 捲軸補償
            _sceneListPanel.style.minWidth = 180;
            _sceneListPanel.style.backgroundColor = new Color(0.18f, 0.18f, 0.2f);
            _sceneListPanel.style.borderLeftWidth = 1;
            _sceneListPanel.style.borderLeftColor = new Color(0.3f, 0.3f, 0.4f);

            // 標題
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 4;
            header.style.height = 26;
            header.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f);

            var title = new Label(SceneFlowLocale.PanelBuildSettings);
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.8f, 0.8f, 0.9f);
            header.Add(title);

            // 彈性空間：把建置設定操作按鈕推到右側
            var headerSpacer = new VisualElement();
            headerSpacer.style.flexGrow = 1;
            header.Add(headerSpacer);

            // 同步到建置 / 從建置同步（覆蓋風險高，都會詢問確認）
            var btnSyncTo = new Button(SyncToBuildSettings) { text = SceneFlowLocale.ToolSyncBuild };
            btnSyncTo.style.fontSize = 10;
            btnSyncTo.style.height = 20;
            btnSyncTo.style.marginTop = 0;
            btnSyncTo.style.marginBottom = 0;
            btnSyncTo.style.marginLeft = 0;
            btnSyncTo.style.marginRight = 2;
            btnSyncTo.style.paddingLeft = 6;
            btnSyncTo.style.paddingRight = 6;
            header.Add(btnSyncTo);

            var btnSyncFrom = new Button(LoadFromBuildSettings) { text = SceneFlowLocale.ToolLoadBuild };
            btnSyncFrom.style.fontSize = 10;
            btnSyncFrom.style.height = 20;
            btnSyncFrom.style.marginTop = 0;
            btnSyncFrom.style.marginBottom = 0;
            btnSyncFrom.style.marginLeft = 0;
            btnSyncFrom.style.marginRight = 4;
            btnSyncFrom.style.paddingLeft = 6;
            btnSyncFrom.style.paddingRight = 6;
            header.Add(btnSyncFrom);

            var refreshBtn = new Button(() => RefreshSceneListPanel()) { text = "↻" };
            refreshBtn.style.width = 24;
            refreshBtn.style.height = 20;
            refreshBtn.style.fontSize = 12;
            header.Add(refreshBtn);

            _sceneListPanel.Add(header);

            // 圖例（固定在清單上方，不跟著捲動）
            var legendBar = new VisualElement();
            legendBar.style.flexDirection = FlexDirection.Row;
            legendBar.style.flexWrap = Wrap.Wrap;
            legendBar.style.paddingLeft = 8;
            legendBar.style.paddingTop = 2;
            legendBar.style.paddingBottom = 2;
            legendBar.style.borderBottomWidth = 1;
            legendBar.style.borderBottomColor = new Color(0.25f, 0.25f, 0.3f);

            var legendItems = new[] {
                ("✓", SceneFlowLocale.LegendInGraph, new Color(0.4f, 0.8f, 0.4f)),
                ("–", SceneFlowLocale.LegendNotInGraph, new Color(0.6f, 0.6f, 0.6f)),
                ("✗", SceneFlowLocale.LegendFileMissing, new Color(0.8f, 0.3f, 0.3f))
            };
            foreach (var (sym, desc, color) in legendItems)
            {
                var symLbl = new Label(sym);
                symLbl.style.fontSize = 9;
                symLbl.style.color = color;
                symLbl.style.marginLeft = 2;
                legendBar.Add(symLbl);

                var descLbl = new Label(desc);
                descLbl.style.fontSize = 9;
                descLbl.style.color = new Color(0.5f, 0.5f, 0.5f);
                descLbl.style.marginRight = 6;
                legendBar.Add(descLbl);
            }
            _sceneListPanel.Add(legendBar);

            // 滾動內容（只有清單資料捲動）
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.flexShrink = 1;

            _sceneListContent = new VisualElement();
            _sceneListContent.style.paddingTop = 4;
            _sceneListContent.style.paddingBottom = 4;
            scrollView.Add(_sceneListContent);
            _sceneListPanel.Add(scrollView);

            // MiniMap 區塊（底部）
            CreateMiniMapSection(_sceneListPanel);

            parent.Add(_sceneListPanel);
        }

        /// <summary>
        /// 建立 MiniMap 區塊（嵌入右側面板底部）
        /// </summary>
        private void CreateMiniMapSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.style.height = 150;
            section.style.minHeight = 100;
            section.style.borderTopWidth = 1;
            section.style.borderTopColor = new Color(0.3f, 0.3f, 0.4f);

            // 標題列（左：MiniMap 標題；右：自動排列 + 置中 按鈕）
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 4;
            header.style.height = 22;
            header.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f);

            var title = new Label(SceneFlowLocale.PanelMiniMap);
            title.style.fontSize = 11;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.7f, 0.7f, 0.8f);
            header.Add(title);

            // 彈性空間把按鈕推到右側
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            // 自動排列
            var btnAutoLayout = new Button(() =>
            {
                _graphView?.AutoLayout();
                UpdateStatusBar();
            }) { text = SceneFlowLocale.ToolAutoLayout };
            btnAutoLayout.style.fontSize = 10;
            btnAutoLayout.style.height = 18;
            btnAutoLayout.style.marginTop = 0;
            btnAutoLayout.style.marginBottom = 0;
            btnAutoLayout.style.marginLeft = 0;
            btnAutoLayout.style.marginRight = 2;
            btnAutoLayout.style.paddingLeft = 6;
            btnAutoLayout.style.paddingRight = 6;
            header.Add(btnAutoLayout);

            // 置中
            var btnCenter = new Button(() => _graphView?.FrameAllNodes()) { text = SceneFlowLocale.ToolCenter };
            btnCenter.style.fontSize = 10;
            btnCenter.style.height = 18;
            btnCenter.style.marginTop = 0;
            btnCenter.style.marginBottom = 0;
            btnCenter.style.marginLeft = 0;
            btnCenter.style.marginRight = 0;
            btnCenter.style.paddingLeft = 6;
            btnCenter.style.paddingRight = 6;
            header.Add(btnCenter);

            section.Add(header);

            // MiniMap 本體
            _miniMap = new SceneFlowMiniMap(_graphView);
            _miniMap.style.flexGrow = 1;
            section.Add(_miniMap);

            parent.Add(section);
        }

        #region Cover 管理面板
        /// <summary>
        /// 建立 Cover 管理面板
        /// </summary>
        private void CreateCoverPanel(VisualElement parent)
        {
            _coverPanel = new VisualElement();
            _coverPanel.style.width = 260;
            _coverPanel.style.minWidth = 200;
            _coverPanel.style.backgroundColor = new Color(0.16f, 0.14f, 0.2f);
            _coverPanel.style.borderLeftWidth = 1;
            _coverPanel.style.borderLeftColor = new Color(0.4f, 0.3f, 0.5f);
            _coverPanel.style.display = DisplayStyle.None;

            // 標題列
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.paddingTop = 6;
            header.style.paddingBottom = 6;
            header.style.backgroundColor = new Color(0.4f, 0.2f, 0.5f);

            var title = new Label(SceneFlowLocale.PanelCoverTitle);
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            header.Add(title);

            var addBtn = new Button(() => AddNewCover()) { text = SceneFlowLocale.PanelCoverAdd };
            addBtn.style.height = 20;
            header.Add(addBtn);

            _coverPanel.Add(header);

            // IMGUI 內容
            _coverIMGUI = new IMGUIContainer(DrawCoverPanelIMGUI);
            _coverIMGUI.style.flexGrow = 1;
            _coverPanel.Add(_coverIMGUI);

            parent.Add(_coverPanel);
        }

        /// <summary>
        /// 切換 Cover 面板顯示
        /// </summary>
        private void ToggleCoverPanel()
        {
            _coverPanelVisible = !_coverPanelVisible;
            _coverPanel.style.display = _coverPanelVisible
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// 新增彈出層到 BlueprintData
        /// </summary>
        private void AddNewCover()
        {
            SceneNameInputWindow.Show((name) =>
            {
                // 預設排序流水：現有最大 sortOrder + 10（排除 9999 的 Scene 類型）
                int maxOrder = 0;
                foreach (var n in BlueprintData.nodes)
                {
                    if (n.nodeType == SceneNodeType.PopCover && n.sortOrder < 9999 && n.sortOrder > maxOrder)
                        maxOrder = n.sortOrder;
                }

                var coverNode = new SceneNode(name)
                {
                    nodeType = SceneNodeType.PopCover,
                    sortOrder = maxOrder + 10,
                    coverOpenTransition = new TransitionSettings
                        { type = TransitionType.Fade, duration = 0.3f },
                    coverCloseTransition = new TransitionSettings
                        { type = TransitionType.Fade, duration = 0.3f }
                };
                BlueprintData.nodes.Add(coverNode);
                _coverFoldouts.Add(coverNode.id); // 新建時預設展開
                EditorUtility.SetDirty(BlueprintData);
                AssetDatabase.SaveAssets();
                _coverIMGUI?.MarkDirtyRepaint();
            }, SceneFlowLocale.NewCoverDefault);
        }

        /// <summary>
        /// 彈出層面板 IMGUI 繪製
        /// </summary>
        private void DrawCoverPanelIMGUI()
        {
            if (BlueprintData == null) return;

            // 依 sortOrder 排序（值小的在上）
            var covers = BlueprintData.nodes
                .Where(n => n.nodeType == SceneNodeType.PopCover)
                .OrderBy(n => n.sortOrder)
                .ThenBy(n => n.sceneName)
                .ToList();

            if (covers.Count == 0)
            {
                EditorGUILayout.HelpBox(SceneFlowLocale.PanelCoverEmpty, MessageType.Info);
                return;
            }

            _coverScrollPos = GUILayout.BeginScrollView(_coverScrollPos, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(220 - SCROLLBAR_W));

            foreach (var cover in covers)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // ── 標題列（收折箭頭 + 名稱 + [Additive] 標記 + 改名 + 刪除）──
                bool isScene = cover.coverSourceType == CoverSourceType.Scene;

                // Inline rename 模式：整個 header 列替換成 TextField
                if (_rename.IsActive(cover.id))
                {
                    _rename.DrawField();
                    if (_rename.CheckEnd(out var newName) && !string.IsNullOrWhiteSpace(newName) && newName != cover.sceneName)
                    {
                        RenameCover(cover, newName);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                    continue;
                }

                string foldLabel = isScene
                    ? $"{cover.sceneName}  [{SceneFlowLocale.LabelAdditive}]"
                    : cover.sceneName;

                bool expanded = _coverFoldouts.Contains(cover.id);
                int coverIdx = covers.IndexOf(cover);

                EditorGUILayout.BeginHorizontal();

                // Scene 類型用橘色背景提示
                var prevBg = GUI.backgroundColor;
                if (isScene) GUI.backgroundColor = new Color(1f, 0.7f, 0.4f);
                bool newExpanded = EditorGUILayout.Foldout(expanded, foldLabel, true, EditorStyles.foldoutHeader);
                GUI.backgroundColor = prevBg;

                if (newExpanded != expanded)
                {
                    if (newExpanded) _coverFoldouts.Add(cover.id);
                    else _coverFoldouts.Remove(cover.id);
                }

                // sortOrder 小灰字（收折時也看得到）
                var sortStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleRight };
                EditorGUILayout.LabelField(cover.sortOrder.ToString(), sortStyle, GUILayout.Width(30));

                // ↑↓ 互換 sortOrder
                GUI.enabled = coverIdx > 0;
                if (GUILayout.Button("↑", EditorStyles.miniButton, GUILayout.Width(18), GUILayout.Height(18)))
                {
                    var other = covers[coverIdx - 1];
                    (cover.sortOrder, other.sortOrder) = (other.sortOrder, cover.sortOrder);
                    EditorUtility.SetDirty(BlueprintData);
                }
                GUI.enabled = coverIdx < covers.Count - 1;
                if (GUILayout.Button("↓", EditorStyles.miniButton, GUILayout.Width(18), GUILayout.Height(18)))
                {
                    var other = covers[coverIdx + 1];
                    (cover.sortOrder, other.sortOrder) = (other.sortOrder, cover.sortOrder);
                    EditorUtility.SetDirty(BlueprintData);
                }
                GUI.enabled = true;

                // 改名（連動資產檔名，走共用 Inline 改名機制）
                if (GUILayout.Button(EditorGUIUtility.IconContent("editicon.sml"), GUIStyle.none, GUILayout.Width(20), GUILayout.Height(18)))
                {
                    _rename.Begin(cover.id, cover.sceneName);
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), GUIStyle.none, GUILayout.Width(20), GUILayout.Height(18)))
                {
                    if (EditorUtility.DisplayDialog(SceneFlowLocale.DlgDeleteCover,
                        SceneFlowLocale.DlgDeleteCoverMsg(cover.sceneName), SceneFlowLocale.DlgDelete, SceneFlowLocale.DlgCancel))
                    {
                        BlueprintData.nodes.Remove(cover);
                        _coverFoldouts.Remove(cover.id);
                        EditorUtility.SetDirty(BlueprintData);
                        break;
                    }
                }
                EditorGUILayout.EndHorizontal();

                // ── 展開內容 ──
                if (!newExpanded)
                {
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                    continue;
                }

                EditorGUI.indentLevel++;

                // ── 排序值 ──
                EditorGUI.BeginChangeCheck();
                cover.sortOrder = EditorGUILayout.IntField(SceneFlowLocale.CoverSortOrder, cover.sortOrder);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(BlueprintData);

                // 來源類型
                EditorGUI.BeginChangeCheck();
                var prevSourceType = cover.coverSourceType;
                cover.coverSourceType = (CoverSourceType)EditorGUILayout.EnumPopup(SceneFlowLocale.CoverSource, cover.coverSourceType);
                if (EditorGUI.EndChangeCheck())
                {
                    // Scene（加載）預設 sortOrder = 9999（通常最高覆蓋）
                    if (cover.coverSourceType == CoverSourceType.Scene && prevSourceType != CoverSourceType.Scene)
                        cover.sortOrder = 9999;
                    EditorUtility.SetDirty(BlueprintData);
                }

                // ── 預置物 ──
                if (cover.coverSourceType == CoverSourceType.Prefab)
                {
                    EditorGUI.BeginChangeCheck();
                    cover.coverPrefab = (GameObject)EditorGUILayout.ObjectField(
                        SceneFlowLocale.CoverPrefab, cover.coverPrefab, typeof(GameObject), false);
                    if (EditorGUI.EndChangeCheck())
                        EditorUtility.SetDirty(BlueprintData);

                    EditorGUILayout.BeginHorizontal();
                    if (cover.coverPrefab == null)
                    {
                        if (GUILayout.Button(SceneFlowLocale.CoverCreatePrefab, EditorStyles.miniButton))
                        {
                            cover.coverPrefab = CreateCoverPrefab(cover.sceneName);
                            EditorUtility.SetDirty(BlueprintData);
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(SceneFlowLocale.CoverOpenEdit, EditorStyles.miniButton))
                            AssetDatabase.OpenAsset(cover.coverPrefab);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                // ── 場景 ──
                else
                {
                    EditorGUI.BeginChangeCheck();
                    cover.coverSceneAsset = (SceneAsset)EditorGUILayout.ObjectField(
                        SceneFlowLocale.CoverSceneAsset, cover.coverSceneAsset, typeof(SceneAsset), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        // 自動同步場景名稱供 Runtime 使用
                        cover.coverSceneName = cover.coverSceneAsset != null
                            ? cover.coverSceneAsset.name : "";
                        EditorUtility.SetDirty(BlueprintData);
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (cover.coverSceneAsset == null)
                    {
                        string csn = string.IsNullOrEmpty(cover.coverSceneName)
                            ? cover.sceneName : cover.coverSceneName;
                        if (GUILayout.Button(SceneFlowLocale.CoverCreateScene, EditorStyles.miniButton))
                        {
                            CreateCoverScene(cover, csn);
                            EditorUtility.SetDirty(BlueprintData);
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(SceneFlowLocale.CoverOpenScene, EditorStyles.miniButton))
                        {
                            string path = AssetDatabase.GetAssetPath(cover.coverSceneAsset);
                            EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(4);

                // ── 開啟動畫 ──
                EditorGUI.BeginChangeCheck();
                cover.coverOpenDuration = EditorGUILayout.Slider(
                    SceneFlowLocale.CoverOpenAnim, cover.coverOpenDuration, 0.1f, 1f);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(BlueprintData);

                // ── 關閉動畫 ──
                EditorGUI.BeginChangeCheck();
                cover.coverCloseDuration = EditorGUILayout.Slider(
                    SceneFlowLocale.CoverCloseAnim, cover.coverCloseDuration, 0.1f, 1f);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(BlueprintData);

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            if (GUI.changed)
                EditorUtility.SetDirty(BlueprintData);
        }
        /// <summary>
        /// 建立 Cover Prefab（含 CoverController + CanvasGroup + RectTransform 全螢幕）
        /// </summary>
        private GameObject CreateCoverPrefab(string coverName)
        {
            string dir = "Assets/Scenes/CoverPrefabs";
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Scenes", "CoverPrefabs");

            string prefabPath = $"{dir}/{coverName}.prefab";

            // 根節點（RectTransform 全螢幕，UI Layer）
            var root = new GameObject(coverName);
            root.layer = LayerMask.NameToLayer("UI");
            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // CanvasGroup（由 CoverController 控制）
            var cg = root.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            // CoverController
            root.AddComponent<CoverController>();

            // 背景遮罩（半透明黑底）
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(root.transform, false);
            var bgRT = bgObj.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);
            bgImage.raycastTarget = true;

            // 內容容器
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(root.transform, false);
            var contentRT = contentObj.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0.1f, 0.1f);
            contentRT.anchorMax = new Vector2(0.9f, 0.9f);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;

            // 存成 Prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);

            AssetDatabase.Refresh();
            CatzLogger.Log("FlowManager", $"Cover Prefab 已建立: {prefabPath}");
            return prefab;
        }

        /// <summary>
        /// 建立 Cover 場景（含基本 UI 結構 + CoverController）
        /// </summary>
        private void CreateCoverScene(SceneNode coverNode, string sceneName)
        {
            string path = $"Assets/Scenes/{sceneName}.unity";

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            newScene.name = sceneName;

            // Canvas + CanvasGroup
            var canvasObj = new GameObject($"[{sceneName}Canvas]");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();
            canvasObj.AddComponent<CanvasGroup>();
            canvasObj.AddComponent<CoverController>();

            EditorSceneManager.SaveScene(newScene, path);
            EditorSceneManager.CloseScene(newScene, true);

            AssetDatabase.Refresh();
            coverNode.coverSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            coverNode.coverSceneName = sceneName;

            // 同步到 Build Settings
            SyncAllScenesToBuildSettings();

            CatzLogger.Log("FlowManager", $"Cover 場景已建立: {path}");
        }
        /// <summary>
        /// 重新命名 Cover（連動 Prefab / Scene 資產檔名）
        /// </summary>
        private void RenameCover(SceneNode cover, string newName)
        {
            string oldName = cover.sceneName;

            // 重新命名 Prefab 資產
            if (cover.coverSourceType == CoverSourceType.Prefab && cover.coverPrefab != null)
            {
                string prefabPath = AssetDatabase.GetAssetPath(cover.coverPrefab);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    string err = AssetDatabase.RenameAsset(prefabPath, newName);
                    if (!string.IsNullOrEmpty(err))
                    {
                        CatzLogger.LogWarning("FlowManager", $"Prefab 改名失敗: {err}");
                        return;
                    }
                }
            }

            // 重新命名 Scene 資產
            if (cover.coverSourceType == CoverSourceType.Scene && cover.coverSceneAsset != null)
            {
                string scenePath = AssetDatabase.GetAssetPath(cover.coverSceneAsset);
                if (!string.IsNullOrEmpty(scenePath))
                {
                    string err = AssetDatabase.RenameAsset(scenePath, newName);
                    if (!string.IsNullOrEmpty(err))
                    {
                        CatzLogger.LogWarning("FlowManager", $"場景改名失敗: {err}");
                        return;
                    }
                    cover.coverSceneName = newName;
                }
            }

            // 更新節點名稱
            cover.sceneName = newName;

            EditorUtility.SetDirty(BlueprintData);
            AssetDatabase.SaveAssets();
            _coverIMGUI?.MarkDirtyRepaint();

            CatzLogger.Log("FlowManager", $"Cover 已改名: {oldName} → {newName}");
        }
        #endregion Cover 管理面板

        /// <summary>
        /// 切換場景清單面板顯示
        /// </summary>
        private void ToggleSceneListPanel()
        {
            _sceneListVisible = !_sceneListVisible;
            _sceneListPanel.style.display = _sceneListVisible
                ? DisplayStyle.Flex : DisplayStyle.None;

            if (_sceneListVisible)
                RefreshSceneListPanel();
        }

        /// <summary>
        /// 刷新場景清單內容
        /// </summary>
        private void RefreshSceneListPanel()
        {
            if (_sceneListContent == null) return;
            _sceneListContent.Clear();

            var buildScenes = EditorBuildSettings.scenes;

            for (int i = 0; i < buildScenes.Length; i++)
            {
                var buildScene = buildScenes[i];
                string scenePath = buildScene.path;
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                bool exists = !string.IsNullOrEmpty(scenePath) && System.IO.File.Exists(scenePath);

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingLeft = 8;
                row.style.paddingRight = 4;
                row.style.height = 24;

                // 交替背景色
                if (i % 2 == 0)
                    row.style.backgroundColor = new Color(0.2f, 0.2f, 0.22f);

                // 序號
                var indexLabel = new Label($"{i}");
                indexLabel.style.width = 18;
                indexLabel.style.fontSize = 10;
                indexLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                row.Add(indexLabel);

                // 狀態圖示
                bool isInGraph = BlueprintData.FindNodeByName(sceneName) != null
                    || sceneName == "FlowManager";
                string icon = !exists ? "✗" : isInGraph ? "✓" : "–";
                var iconLabel = new Label(icon);
                iconLabel.style.width = 16;
                iconLabel.style.fontSize = 11;
                iconLabel.style.color = !exists
                    ? new Color(0.8f, 0.3f, 0.3f)
                    : isInGraph
                        ? new Color(0.4f, 0.8f, 0.4f)
                        : new Color(0.6f, 0.6f, 0.6f);
                row.Add(iconLabel);

                // 判斷是否為 Scene Cover（Additive 加載）
                bool isCoverScene = BlueprintData.nodes.Any(n =>
                    n.nodeType == SceneNodeType.PopCover &&
                    n.coverSourceType == CoverSourceType.Scene &&
                    n.coverSceneName == sceneName);

                // 場景名稱
                string displayName = isCoverScene ? $"{sceneName} [{SceneFlowLocale.LabelAdditive}]" : sceneName;
                var nameLabel = new Label(displayName);
                nameLabel.style.flexGrow = 1;
                nameLabel.style.fontSize = 11;
                nameLabel.style.overflow = Overflow.Hidden;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                // 顏色區分
                if (sceneName == "FlowManager")
                    nameLabel.style.color = new Color(0.6f, 0.7f, 0.9f);
                else if (isCoverScene)
                    nameLabel.style.color = new Color(1f, 0.7f, 0.4f); // 橘色
                else if (buildScene.enabled)
                    nameLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
                else
                    nameLabel.style.color = new Color(0.5f, 0.5f, 0.5f);

                row.Add(nameLabel);

                // 啟用狀態
                var enabledLabel = new Label(buildScene.enabled ? SceneFlowLocale.LabelEnabled : SceneFlowLocale.LabelDisabled);
                enabledLabel.style.width = 24;
                enabledLabel.style.fontSize = 9;
                enabledLabel.style.color = buildScene.enabled
                    ? new Color(0.4f, 0.7f, 0.4f)
                    : new Color(0.5f, 0.5f, 0.5f);
                row.Add(enabledLabel);

                _sceneListContent.Add(row);
            }

            if (buildScenes.Length == 0)
            {
                var emptyLabel = new Label(SceneFlowLocale.PanelBuildEmpty);
                emptyLabel.style.paddingLeft = 8;
                emptyLabel.style.paddingTop = 8;
                emptyLabel.style.fontSize = 11;
                emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                _sceneListContent.Add(emptyLabel);
            }

            // 圖例已移至面板頂部固定區域
        }
        #endregion Build Settings 場景清單側邊欄

        #region 預設管理與自動備份
        /// <summary>
        /// 預設資料夾路徑
        /// </summary>
        private const string PresetFolder = "Assets/GAME_TOOLS/GAME_FLOW_SYS/Presets";

        /// <summary>
        /// 自動備份路徑
        /// </summary>
        private const string AutoBackupPath = "Assets/GAME_TOOLS/GAME_FLOW_SYS/Presets/_autosave.json";

        /// <summary>
        /// 確保預設資料夾存在
        /// </summary>
        private void EnsurePresetFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/GAME_TOOLS/GAME_FLOW_SYS/Presets"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/GAME_TOOLS/GAME_FLOW_SYS"))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/GAME_TOOLS"))
                        AssetDatabase.CreateFolder("Assets", "GAME_TOOLS");
                    AssetDatabase.CreateFolder("Assets/GAME_TOOLS", "GAME_FLOW_SYS");
                }
                AssetDatabase.CreateFolder("Assets/GAME_TOOLS/GAME_FLOW_SYS", "Presets");
            }
        }

        /// <summary>
        /// 自動備份（每次圖表變更時）
        /// </summary>
        private void AutoBackup()
        {
            if (BlueprintData == null || BlueprintData.nodes == null) return;
            if (BlueprintData.nodes.Count == 0) return;

            EnsurePresetFolder();

            string json = SceneFlowJson.Export(BlueprintData);
            string fullPath = System.IO.Path.GetFullPath(AutoBackupPath);
            System.IO.File.WriteAllText(fullPath, json);

            // 延遲 Refresh 讓 Unity 識別檔案（不會每次都卡頓）
            EditorApplication.delayCall += () => AssetDatabase.Refresh();
        }

        // SavePreset / LoadPreset 方法於 v0.7.9b 移除（實際無使用情境，AutoBackup 仍保留供其他場景用）
        #endregion 預設管理與自動備份

        #region 資料管理
        private SceneBlueprintData LoadOrCreateBlueprintData()
        {
            SceneBlueprintData data = Resources.Load<SceneBlueprintData>("SceneBlueprintData");

            if (data == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:SceneBlueprintData");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    data = AssetDatabase.LoadAssetAtPath<SceneBlueprintData>(path);
                }
            }

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<SceneBlueprintData>();
                data.nodes = new System.Collections.Generic.List<SceneNode>();
                data.edges = new System.Collections.Generic.List<SceneEdge>();

                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                AssetDatabase.CreateAsset(data, "Assets/Resources/SceneBlueprintData.asset");
                AssetDatabase.SaveAssets();
            }

            return data;
        }
        #endregion 資料管理
    }
    #endregion 場景流程編輯器視窗
}
#endif
