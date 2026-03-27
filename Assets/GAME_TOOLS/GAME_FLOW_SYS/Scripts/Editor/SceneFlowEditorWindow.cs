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

namespace CatzTools
{
    #region 場景流程編輯器視窗
    /// <summary>
    /// 場景流程編輯器視窗 — GraphView 容器。
    /// FlowManager 不參與節點圖，而是作為底層常駐場景，
    /// 開啟視窗時自動建立場景、掛好腳本、加入 Build Settings 第一個。
    /// </summary>
    public class SceneFlowEditorWindow : EditorWindow
    {
        #region 私有變數
        private SceneFlowGraphView _graphView;
        private SceneBlueprintData _blueprintData;
        private Label _statusLabel;
        private Label _flowManagerStatusLabel;
        private VisualElement _sceneListPanel;
        private VisualElement _sceneListContent;
        private SceneFlowMiniMap _miniMap;
        private bool _sceneListVisible = true;
        private VisualElement _inspectorPanel;
        private VisualElement _inspectorContent;
        private IMGUIContainer _inspectorIMGUI;
        private object _selectedObject;
        private Vector2 _inspectorScrollPos;
        #endregion 私有變數

        #region Lazy Loading 屬性
        private SceneBlueprintData BlueprintData
        {
            get
            {
                if (_blueprintData == null)
                    _blueprintData = LoadOrCreateBlueprintData();
                return _blueprintData;
            }
        }
        #endregion Lazy Loading 屬性

        #region Unity 編輯器選單
        /// <summary>
        /// 開啟場景流程圖編輯器
        /// </summary>
        [MenuItem("CatzTools/場景流程圖")]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneFlowEditorWindow>("場景流程圖");
            window.minSize = new Vector2(800, 600);
        }
        #endregion Unity 編輯器選單

        #region 生命週期
        private void CreateGUI()
        {
            var root = rootVisualElement;

            // FlowManager 資訊列（頂部）
            CreateFlowManagerBar(root);

            // 工具列
            CreateToolbar(root);

            // 主要區域（左側屬性面板 + GraphView + 右側場景清單）
            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Row;
            mainContainer.style.flexGrow = 1;

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

            // 右側：Build Settings 場景清單
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
        }
        #endregion 生命週期

        #region FlowManager 資訊列
        /// <summary>
        /// 建立 FlowManager 資訊列（固定在頂部，不可刪除）
        /// </summary>
        private void CreateFlowManagerBar(VisualElement root)
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.backgroundColor = new Color(0.18f, 0.18f, 0.22f);
            bar.style.paddingLeft = 12;
            bar.style.paddingRight = 12;
            bar.style.height = 32;
            bar.style.alignItems = Align.Center;
            bar.style.borderBottomWidth = 1;
            bar.style.borderBottomColor = new Color(0.3f, 0.3f, 0.4f);

            // 圖示 + 標題
            var titleLabel = new Label("⚙ FlowManager");
            titleLabel.style.fontSize = 13;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.8f, 0.8f, 0.9f);
            titleLabel.style.marginRight = 12;
            bar.Add(titleLabel);

            // 狀態
            _flowManagerStatusLabel = new Label();
            _flowManagerStatusLabel.style.fontSize = 11;
            _flowManagerStatusLabel.style.flexGrow = 1;
            bar.Add(_flowManagerStatusLabel);

            // 開啟場景按鈕
            var openButton = new Button(() => OpenFlowManagerScene()) { text = "開啟場景" };
            openButton.style.height = 22;
            bar.Add(openButton);

            // 重建場景按鈕
            var rebuildButton = new Button(() =>
            {
                if (EditorUtility.DisplayDialog("重建 FlowManager",
                    "這將重新建立 FlowManager 場景，覆蓋現有場景。是否繼續？", "繼續", "取消"))
                {
                    CreateFlowManagerScene();
                    UpdateFlowManagerBar();
                }
            }) { text = "重建" };
            rebuildButton.style.height = 22;
            bar.Add(rebuildButton);

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
            string startInfo = string.IsNullOrEmpty(startScene) ? "未設定" : startScene;

            if (hasScene)
            {
                _flowManagerStatusLabel.text = $"✓ 就緒 — 起始場景: {startInfo}";
                _flowManagerStatusLabel.style.color = string.IsNullOrEmpty(startScene)
                    ? new Color(0.9f, 0.7f, 0.2f)
                    : new Color(0.4f, 0.8f, 0.4f);
            }
            else
            {
                _flowManagerStatusLabel.text = "⚠ 場景不存在，點擊「重建」建立";
                _flowManagerStatusLabel.style.color = new Color(0.9f, 0.7f, 0.2f);
            }
        }
        #endregion FlowManager 資訊列

        #region 工具列
        private void CreateToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();

            toolbar.Add(new ToolbarButton(() => AddNewScene()) { text = "➕ 新增場景" });
            toolbar.Add(new ToolbarSpacer());

            toolbar.Add(new ToolbarButton(() => GenerateAllScenes()) { text = "⚡ 產生全部場景" });
            toolbar.Add(new ToolbarButton(() => SyncToBuildSettings()) { text = "🔄 同步 Build Settings" });
            toolbar.Add(new ToolbarButton(() => LoadFromBuildSettings()) { text = "📥 從 Build Settings 載入" });
            toolbar.Add(new ToolbarSpacer());

            toolbar.Add(new ToolbarButton(() => ImportJson()) { text = "📂 匯入" });
            toolbar.Add(new ToolbarButton(() => ExportJson()) { text = "💾 匯出" });
            toolbar.Add(new ToolbarSpacer());

            toolbar.Add(new ToolbarButton(() => SavePreset()) { text = "💾 存預設" });
            toolbar.Add(new ToolbarButton(() => LoadPreset()) { text = "📂 讀預設" });

            toolbar.Add(new ToolbarSpacer { style = { flexGrow = 1 } });

            toolbar.Add(new ToolbarButton(() =>
            {
                _graphView.AutoLayout();
                UpdateStatusBar();
            }) { text = "🔧 自動排列" });

            toolbar.Add(new ToolbarButton(() => _graphView.FrameAllNodes())
                { text = "📍 置中" });

            toolbar.Add(new ToolbarButton(() =>
            {
                _blueprintData = null;
                _graphView.LoadBlueprintData(BlueprintData);
                UpdateStatusBar();
                UpdateFlowManagerBar();
            }) { text = "🔃 重新載入" });

            toolbar.Add(new ToolbarButton(() => ToggleSceneListPanel())
                { text = "📋 場景清單" });

            root.Add(toolbar);
        }
        #endregion 工具列

        #region 狀態列
        private void CreateStatusBar(VisualElement root)
        {
            var statusBar = new VisualElement();
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            statusBar.style.paddingLeft = 8;
            statusBar.style.paddingRight = 8;
            statusBar.style.height = 22;
            statusBar.style.alignItems = Align.Center;

            _statusLabel = new Label();
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            statusBar.Add(_statusLabel);

            root.Add(statusBar);
        }

        private void UpdateStatusBar()
        {
            if (_statusLabel == null || BlueprintData?.nodes == null) return;

            int totalNodes = BlueprintData.nodes.Count;
            int totalEdges = BlueprintData.edges?.Count ?? 0;
            int linkedCount = BlueprintData.nodes.Count(n => n.sceneAsset != null);

            string fmStatus = BlueprintData.flowManagerScene != null ? "✓" : "⚠";

            _statusLabel.text = $"場景節點: {totalNodes} | 連線: {totalEdges} | " +
                $"已連結: {linkedCount}/{totalNodes} | FlowManager: {fmStatus}";
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

            // 確保 Scenes 資料夾存在
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            // 儲存當前場景
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            // 建立新場景
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            newScene.name = "FlowManager";

            // === FlowManager GameObject（管理器 + Camera）===
            var flowManagerObj = new GameObject("[FlowManager]");
            flowManagerObj.tag = "MainCamera";
            flowManagerObj.AddComponent(typeof(FlowManager));
            var cam = flowManagerObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.depth = -1; // 最底層，遊戲場景 Camera 疊在上面

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

            // 儲存場景
            EditorSceneManager.SaveScene(newScene, path);
            EditorSceneManager.CloseScene(newScene, true);

            // 更新藍圖資料
            BlueprintData.flowManagerScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            BlueprintData.flowManagerScenePath = path;
            EditorUtility.SetDirty(BlueprintData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 同步 Build Settings
            SyncAllScenesToBuildSettings();

            Debug.Log($"FlowManager 場景已建立（含 TransitionCanvas）：{path}");
        }

        /// <summary>
        /// 開啟 FlowManager 場景
        /// </summary>
        private void OpenFlowManagerScene()
        {
            if (BlueprintData.flowManagerScene == null)
            {
                EditorUtility.DisplayDialog("錯誤", "FlowManager 場景不存在，請先重建。", "確定");
                return;
            }

            string path = AssetDatabase.GetAssetPath(BlueprintData.flowManagerScene);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
            }
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
                EditorUtility.DisplayDialog("名稱重複",
                    $"流程圖中已有 \"{sceneName}\" 節點！請換一個名稱。", "確定");
                return;
            }

            // 只建節點，不建場景檔
            var node = _graphView.AddSceneNode(sceneName, position, nodeType);

            // 如果場景檔已存在，自動接入
            if (nodeType != SceneNodeType.End)
            {
                string scenePath = $"Assets/Scenes/{sceneName}.unity";
                var existingAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (existingAsset != null)
                {
                    node.SceneData.sceneAsset = existingAsset;
                    node.RefreshAppearance();
                }
            }

            UpdateStatusBar();
            Debug.Log($"節點已建立：{sceneName}（場景檔待產生）");
        }

        /// <summary>
        /// 確保場景檔存在（不存在就建立，含 [SceneEvent]），然後開啟。
        /// </summary>
        private void EnsureAndOpenScene(SceneNode nodeData)
        {
            if (nodeData.nodeType == SceneNodeType.End) return;

            string scenePath = $"Assets/Scenes/{nodeData.sceneName}.unity";

            // 場景檔不存在 → 建立
            if (nodeData.sceneAsset == null)
            {
                var existingAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (existingAsset != null)
                {
                    // 檔案已存在但節點沒接 → 接入
                    nodeData.sceneAsset = existingAsset;
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
            Debug.Log($"場景已建立：{scenePath}");
        }

        /// <summary>
        /// 一口氣產生全部未建立的場景檔
        /// </summary>
        private void GenerateAllScenes()
        {
            var missingScenes = BlueprintData.nodes
                .Where(n => n.nodeType != SceneNodeType.End && n.sceneAsset == null)
                .ToList();

            if (missingScenes.Count == 0)
            {
                EditorUtility.DisplayDialog("全部就緒", "所有場景檔皆已建立。", "確定");
                return;
            }

            if (!EditorUtility.DisplayDialog("產生場景",
                $"將為以下 {missingScenes.Count} 個節點建立場景檔：\n" +
                string.Join("\n", missingScenes.Select(n => $"  • {n.sceneName}")),
                "建立全部", "取消"))
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

            EditorUtility.DisplayDialog("完成",
                $"已建立 {missingScenes.Count} 個場景檔。", "確定");
        }

        /// <summary>
        /// 處理 GraphView 右鍵選單的新增場景請求
        /// </summary>
        private void OnRequestAddSceneFromGraph(Vector2 position, SceneNodeType nodeType)
        {
            string defaultName = nodeType switch
            {
                SceneNodeType.End => "EndPoint",
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

            Debug.Log($"已設定 \"{targetName}\" 為起始場景，START 已自動連線");
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
        /// 同步所有場景至 Build Settings（FlowManager 永遠第一個）
        /// </summary>
        private void SyncAllScenesToBuildSettings()
        {
            SyncToBuildSettings();
        }

        /// <summary>
        /// 同步至 Build Settings
        /// </summary>
        private void SyncToBuildSettings()
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
                Debug.LogWarning("FlowManager 場景不存在，無法同步 Build Settings");
                return;
            }

            // 其他場景節點
            foreach (var node in BlueprintData.nodes)
            {
                if (node.sceneAsset != null)
                {
                    string path = AssetDatabase.GetAssetPath(node.sceneAsset);
                    if (!buildScenes.Any(s => s.path == path))
                    {
                        buildScenes.Add(new EditorBuildSettingsScene(path, true));
                    }
                }
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        /// <summary>
        /// 從 Build Settings 載入
        /// </summary>
        private void LoadFromBuildSettings()
        {
            if (BlueprintData.nodes.Count > 0)
            {
                if (!EditorUtility.DisplayDialog("載入場景",
                    "這將清除現有場景節點並從 Build Settings 載入，是否繼續？",
                    "繼續", "取消"))
                    return;
            }

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
                row++;
            }

            EditorUtility.SetDirty(BlueprintData);
            AssetDatabase.SaveAssets();
            _graphView.LoadBlueprintData(BlueprintData);
            UpdateStatusBar();
            UpdateFlowManagerBar();

            EditorUtility.DisplayDialog("載入完成",
                $"已從 Build Settings 載入 {BlueprintData.nodes.Count} 個遊戲場景",
                "確定");
        }
        #endregion 場景操作

        #region JSON 匯入匯出
        private void ImportJson()
        {
            string path = EditorUtility.OpenFilePanel("匯入場景流程 JSON", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            string json = System.IO.File.ReadAllText(path);
            SceneFlowJson.Import(json, BlueprintData);

            EditorUtility.SetDirty(BlueprintData);
            AssetDatabase.SaveAssets();
            _graphView.LoadBlueprintData(BlueprintData);
            UpdateStatusBar();
            UpdateFlowManagerBar();

            EditorUtility.DisplayDialog("匯入完成",
                $"已匯入 {BlueprintData.nodes.Count} 個節點和 {BlueprintData.edges.Count} 條連線",
                "確定");
        }

        private void ExportJson()
        {
            string path = EditorUtility.SaveFilePanel("匯出場景流程 JSON", "", "scene-flow", "json");
            if (string.IsNullOrEmpty(path)) return;

            string json = SceneFlowJson.Export(BlueprintData);
            System.IO.File.WriteAllText(path, json);

            EditorUtility.DisplayDialog("匯出完成", $"已匯出至 {path}", "確定");
        }
        #endregion JSON 匯入匯出

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
            _inspectorPanel.style.width = 240;
            _inspectorPanel.style.minWidth = 200;
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

            var titleLabel = new Label("屬性");
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.8f, 0.8f, 0.9f);
            header.Add(titleLabel);
            _inspectorPanel.Add(header);

            // IMGUI 內容區（用 IMGUI 畫 EditorGUILayout，支援 ColorField 等）
            _inspectorIMGUI = new IMGUIContainer(DrawInspectorGUI);
            _inspectorIMGUI.style.flexGrow = 1;
            _inspectorIMGUI.style.paddingLeft = 8;
            _inspectorIMGUI.style.paddingRight = 8;
            _inspectorIMGUI.style.paddingTop = 8;
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
                EditorGUILayout.LabelField("選取節點或連線", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _inspectorScrollPos = EditorGUILayout.BeginScrollView(_inspectorScrollPos);

            if (_selectedObject is SceneNode node)
            {
                DrawNodeInspector(node);
            }
            else if (_selectedObject is SceneEdge edge)
            {
                DrawEdgeInspector(edge);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 節點屬性面板
        /// </summary>
        private void DrawNodeInspector(SceneNode node)
        {
            // 標題
            EditorGUILayout.LabelField(node.nodeType == SceneNodeType.Start ? "▶ START" : node.sceneName,
                EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (node.nodeType == SceneNodeType.Start)
            {
                EditorGUILayout.HelpBox("遊戲啟動入口\n連線到起始場景，在連線上設定開場轉場效果", MessageType.Info);
                return;
            }

            if (node.nodeType == SceneNodeType.End)
            {
                EditorGUILayout.HelpBox("流程終點", MessageType.Info);
                return;
            }

            // 場景名稱
            EditorGUI.BeginChangeCheck();
            var newName = EditorGUILayout.TextField("場景名稱", node.sceneName);
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(newName))
            {
                node.sceneName = newName;
                SaveAndRefresh();
            }

            // 場景資源
#if UNITY_EDITOR
            EditorGUI.BeginChangeCheck();
            var newAsset = (SceneAsset)EditorGUILayout.ObjectField(
                "場景資源", node.sceneAsset, typeof(SceneAsset), false);
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
            EditorGUILayout.LabelField("狀態", isStart ? "★ 起始場景" : "一般場景");

            EditorGUILayout.Space(4);

            // 描述
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("描述", EditorStyles.miniLabel);
            var newDesc = EditorGUILayout.TextArea(node.description ?? "", GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck())
            {
                node.description = newDesc;
                SaveAndRefresh();
            }

            EditorGUILayout.Space(4);

            // 連線資訊
            if (BlueprintData != null)
            {
                var outEdges = BlueprintData.GetOutgoingEdges(node.id);
                var inEdges = BlueprintData.GetIncomingEdges(node.id);

                EditorGUILayout.LabelField($"出: {outEdges.Count} 條  |  入: {inEdges.Count} 條",
                    EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space(8);

            // 操作按鈕
            EditorGUILayout.LabelField("操作", EditorStyles.miniLabel);

#if UNITY_EDITOR
            if (node.sceneAsset == null)
            {
                // 場景不存在：顯示建立按鈕
                if (GUILayout.Button("建立場景"))
                {
                    EnsureAndOpenScene(node);
                    // 建立後不開啟，只建檔案和掛 SceneEvent
                    // EnsureAndOpenScene 會建立場景，但不會切過去如果只是要建立
                }
            }
            else
            {
                // 場景已存在：顯示開啟按鈕
                if (GUILayout.Button("開啟場景"))
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
                if (GUILayout.Button("設為起點"))
                {
                    // 找到對應的 GraphView 節點
                    var flowNode = FindFlowNodeByData(node);
                    if (flowNode != null)
                        OnSetSceneAsStart(flowNode);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("已設為起始場景", MessageType.None);
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
        /// 連線屬性面板（轉場設定）
        /// </summary>
        private void DrawEdgeInspector(SceneEdge edge)
        {
            var srcName = BlueprintData?.FindNodeById(edge.source)?.sceneName ?? "?";
            var tgtName = BlueprintData?.FindNodeById(edge.target)?.sceneName ?? "?";

            EditorGUILayout.LabelField($"{srcName} → {tgtName}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (edge.transition == null)
                edge.transition = new TransitionSettings();

            var t = edge.transition;

            // ── 轉場基本設定 ──
            var prevType = t.type;
            t.type = (TransitionType)EditorGUILayout.EnumPopup("轉場效果", t.type);
            t.duration = EditorGUILayout.Slider("時長 (秒)", t.duration, 0.1f, 3f);

            EditorGUILayout.Space(4);

            if (t.type != TransitionType.CustomShader)
            {
                t.color = EditorGUILayout.ColorField("遮罩顏色", t.color);
            }

            if (t.type == TransitionType.CustomShader)
            {
                var prevMat = t.customMaterial;
                t.customMaterial = (Material)EditorGUILayout.ObjectField(
                    "Shader Material", t.customMaterial, typeof(Material), false);

                if (t.customMaterial == null)
                    EditorGUILayout.HelpBox("需要含 _Progress (0~1) 屬性的 Material", MessageType.Info);

                // Material 變了 → 重新初始化覆蓋值
                if (t.customMaterial != prevMat)
                    t.shaderOverrides = null;
            }

            // 類型從 Shader 切走 → 清掉覆蓋
            if (prevType == TransitionType.CustomShader && t.type != TransitionType.CustomShader)
                t.shaderOverrides = null;

            // ── 目前套用的轉場標籤 ──
            EditorGUILayout.Space(4);
            string currentLabel = t.type switch
            {
                TransitionType.None => "無轉場",
                TransitionType.Fade => "淡入淡出",
                TransitionType.SlideLeft => "左滑",
                TransitionType.SlideRight => "右滑",
                TransitionType.SlideUp => "上滑",
                TransitionType.SlideDown => "下滑",
                TransitionType.CustomShader when t.customMaterial != null =>
                    $"Shader：{t.customMaterial.name}",
                TransitionType.CustomShader => "Shader：未指定 Material",
                _ => t.type.ToString()
            };
            EditorGUILayout.HelpBox($"▶ {currentLabel}　時長 {t.duration:F1}s", MessageType.None);

            // ── Shader 屬性面板（每條連線獨立）──
            if (t.type == TransitionType.CustomShader && t.customMaterial != null)
                DrawShaderOverrides(t);

            // ── 統一存檔（任何欄位變動都在這裡處理）──
            if (GUI.changed)
                EditorUtility.SetDirty(BlueprintData);

            EditorGUILayout.Space(8);

            // ── UI 轉場快捷 ──
            EditorGUILayout.LabelField("UI 轉場", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("淡入淡出", EditorStyles.miniButton))
                ApplyQuickPreset(t, TransitionType.Fade, 0.5f, Color.black, null);
            if (GUILayout.Button("白色淡入", EditorStyles.miniButton))
                ApplyQuickPreset(t, TransitionType.Fade, 0.5f, Color.white, null);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("左滑", EditorStyles.miniButton))
                ApplyQuickPreset(t, TransitionType.SlideLeft, 0.5f, Color.black, null);
            if (GUILayout.Button("右滑", EditorStyles.miniButton))
                ApplyQuickPreset(t, TransitionType.SlideRight, 0.5f, Color.black, null);
            if (GUILayout.Button("無轉場", EditorStyles.miniButton))
                ApplyQuickPreset(t, TransitionType.None, 0f, Color.black, null);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Shader 轉場快捷 ──
            EditorGUILayout.LabelField("Shader 轉場", EditorStyles.miniLabel);
            var presets = SceneFlowShaderPresets.Presets;
            // 每行 3 個按鈕
            for (int pi = 0; pi < presets.Length; pi++)
            {
                if (pi % 3 == 0) EditorGUILayout.BeginHorizontal();

                var preset = presets[pi];
                if (GUILayout.Button(preset.DisplayName, EditorStyles.miniButton))
                {
                    var mat = SceneFlowShaderPresets.GetOrCreateMaterial(preset);
                    if (mat != null)
                        ApplyQuickPreset(t, TransitionType.CustomShader, 0.8f, Color.black, mat);
                }

                if (pi % 3 == 2 || pi == presets.Length - 1)
                    EditorGUILayout.EndHorizontal();
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

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Shader 屬性（此連線）— {t.shaderOverrides.Count} 項", EditorStyles.boldLabel);

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
                EditorGUILayout.LabelField(o.displayName, EditorStyles.miniLabel);
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
            Debug.Log($"[SceneFlow] SyncOverrides 重建完成：{newList.Count} 個屬性");
            foreach (var item in newList)
                Debug.Log($"  → {item.propertyName} ({item.valueType}) display=\"{item.displayName}\" range=[{item.rangeMin},{item.rangeMax}]");
        }

        /// <summary>
        /// 快速套用預設（UI 和 Shader 通用）
        /// </summary>
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

            Debug.Log($"[SceneFlow] ApplyQuickPreset: overrides={t.shaderOverrides?.Count ?? -1}");
            EditorUtility.SetDirty(BlueprintData);
            Debug.Log($"[SceneFlow] SaveAssets 前 overrides={t.shaderOverrides?.Count ?? -1}");
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneFlow] SaveAssets 後 overrides={t.shaderOverrides?.Count ?? -1}");
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
        #endregion 左側屬性面板

        private void CreateSceneListPanel(VisualElement parent)
        {
            _sceneListPanel = new VisualElement();
            _sceneListPanel.style.width = 220;
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

            var title = new Label("Build Settings");
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.8f, 0.8f, 0.9f);
            title.style.flexGrow = 1;
            header.Add(title);

            var refreshBtn = new Button(() => RefreshSceneListPanel()) { text = "↻" };
            refreshBtn.style.width = 24;
            refreshBtn.style.height = 20;
            refreshBtn.style.fontSize = 12;
            header.Add(refreshBtn);

            _sceneListPanel.Add(header);

            // 滾動內容
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;

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

            // 標題列
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 4;
            header.style.height = 22;
            header.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f);

            var title = new Label("Mini Map");
            title.style.fontSize = 11;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.7f, 0.7f, 0.8f);
            header.Add(title);

            section.Add(header);

            // MiniMap 本體
            _miniMap = new SceneFlowMiniMap(_graphView);
            _miniMap.style.flexGrow = 1;
            section.Add(_miniMap);

            parent.Add(section);
        }

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

                // 場景名稱
                var nameLabel = new Label(sceneName);
                nameLabel.style.flexGrow = 1;
                nameLabel.style.fontSize = 11;
                nameLabel.style.color = buildScene.enabled
                    ? new Color(0.85f, 0.85f, 0.85f)
                    : new Color(0.5f, 0.5f, 0.5f);
                nameLabel.style.overflow = Overflow.Hidden;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                // FlowManager 特殊標記
                if (sceneName == "FlowManager")
                    nameLabel.style.color = new Color(0.6f, 0.7f, 0.9f);

                row.Add(nameLabel);

                // 啟用狀態
                var enabledLabel = new Label(buildScene.enabled ? "ON" : "OFF");
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
                var emptyLabel = new Label("Build Settings 無場景");
                emptyLabel.style.paddingLeft = 8;
                emptyLabel.style.paddingTop = 8;
                emptyLabel.style.fontSize = 11;
                emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                _sceneListContent.Add(emptyLabel);
            }

            // 圖例
            var legend = new VisualElement();
            legend.style.paddingLeft = 8;
            legend.style.paddingTop = 8;
            legend.style.borderTopWidth = 1;
            legend.style.borderTopColor = new Color(0.25f, 0.25f, 0.3f);

            var legendItems = new[] {
                ("✓", "在流程圖中", new Color(0.4f, 0.8f, 0.4f)),
                ("–", "不在流程圖中", new Color(0.6f, 0.6f, 0.6f)),
                ("✗", "檔案不存在", new Color(0.8f, 0.3f, 0.3f))
            };

            foreach (var (sym, desc, color) in legendItems)
            {
                var legendRow = new VisualElement();
                legendRow.style.flexDirection = FlexDirection.Row;
                legendRow.style.height = 16;
                legendRow.style.alignItems = Align.Center;

                var symLabel = new Label(sym);
                symLabel.style.width = 16;
                symLabel.style.fontSize = 10;
                symLabel.style.color = color;
                legendRow.Add(symLabel);

                var descLabel = new Label(desc);
                descLabel.style.fontSize = 10;
                descLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                legendRow.Add(descLabel);

                legend.Add(legendRow);
            }

            _sceneListContent.Add(legend);
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

        /// <summary>
        /// 儲存預設 — 輸入名稱存為 JSON
        /// </summary>
        private void SavePreset()
        {
            EnsurePresetFolder();

            string fullFolder = System.IO.Path.GetFullPath(PresetFolder);
            string path = EditorUtility.SaveFilePanel(
                "儲存預設", fullFolder, "preset", "json");
            if (string.IsNullOrEmpty(path)) return;

            string json = SceneFlowJson.Export(BlueprintData);
            System.IO.File.WriteAllText(path, json);

            AssetDatabase.Refresh();
            Debug.Log($"預設已儲存：{path}");
        }

        /// <summary>
        /// 讀取預設 — 顯示選單選擇（含自動備份）
        /// </summary>
        private void LoadPreset()
        {
            EnsurePresetFolder();

            // 掃描預設檔案
            string fullPath = System.IO.Path.GetFullPath(PresetFolder);
            if (!System.IO.Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("無預設", "尚無任何預設檔案。", "確定");
                return;
            }

            var files = System.IO.Directory.GetFiles(fullPath, "*.json");
            if (files.Length == 0)
            {
                EditorUtility.DisplayDialog("無預設", "尚無任何預設檔案。", "確定");
                return;
            }

            // 建立選單
            var menu = new GenericMenu();

            foreach (var file in files)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                string filePath = file;

                // 自動備份特殊標記
                string label = fileName == "_autosave"
                    ? "⟲ 自動備份（上次狀態）"
                    : fileName;

                menu.AddItem(new GUIContent(label), false, () =>
                {
                    if (!EditorUtility.DisplayDialog("讀取預設",
                        $"確定要載入「{fileName}」嗎？\n目前的配置將被覆蓋（已自動備份）。",
                        "載入", "取消"))
                        return;

                    // 載入前先備份目前狀態
                    AutoBackup();

                    string json = System.IO.File.ReadAllText(filePath);
                    SceneFlowJson.Import(json, BlueprintData);

                    EditorUtility.SetDirty(BlueprintData);
                    AssetDatabase.SaveAssets();
                    _graphView.LoadBlueprintData(BlueprintData);
                    UpdateStatusBar();
                    UpdateFlowManagerBar();

                    Debug.Log($"已載入預設：{fileName}");
                });
            }

            menu.ShowAsContext();
        }
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
