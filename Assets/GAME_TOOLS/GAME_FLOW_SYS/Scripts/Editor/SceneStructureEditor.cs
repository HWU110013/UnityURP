#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CatzTools
{
    #region 場景結構編輯器
    /// <summary>
    /// 場景結構編輯器 - 簡潔的清單式場景管理
    /// </summary>
    public class SceneStructureEditor : EditorWindow
    {
        #region 私有變數
        private SceneBlueprintData _blueprintData;
        private Vector2 _scrollPosition;
        private SceneNode _selectedNode;
        private bool _isEditingConnections = false;
        private string _searchFilter = "";
        private int _reorderFromIndex = -1;
        private int _reorderToIndex = -1;
        #endregion 私有變數

        #region Lazy Loading 屬性
        /// <summary>
        /// 藍圖資料 (Lazy Loading)
        /// </summary>
        private SceneBlueprintData BlueprintData
        {
            get
            {
                if (_blueprintData == null)
                {
                    _blueprintData = LoadOrCreateBlueprintData();
                }
                return _blueprintData;
            }
        }
        #endregion Lazy Loading 屬性

        #region Unity 編輯器選單
        /// <summary>
        /// 開啟場景結構編輯器
        /// </summary>
        [MenuItem("CatzTools/場景結構編輯器")]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneStructureEditor>("場景結構");
            window.minSize = new Vector2(400, 500);
        }
        #endregion Unity 編輯器選單

        #region 生命週期
        /// <summary>
        /// 視窗啟用時初始化
        /// </summary>
        private void OnEnable()
        {
            EnsureStartNode();
            CleanupInvalidConnections();
        }

        /// <summary>
        /// 清理無效的連接（移除 FlowManager 的連接）
        /// </summary>
        private void CleanupInvalidConnections()
        {
            if (BlueprintData.nodes == null) return;

            bool hasChanges = false;
            var flowManagerNode = BlueprintData.nodes.FirstOrDefault(n => n.sceneName == "FlowManager");

            if (flowManagerNode != null)
            {
                foreach (var node in BlueprintData.nodes)
                {
                    if (node.connectedNodeIds != null && node.connectedNodeIds.Contains(flowManagerNode.id))
                    {
                        node.connectedNodeIds.Remove(flowManagerNode.id);
                        hasChanges = true;
                        Debug.Log($"已從 {node.sceneName} 移除對 FlowManager 的連接");
                    }
                }
            }

            if (hasChanges)
            {
                SaveBlueprintData();
            }
        }

        /// <summary>
        /// 繪製GUI
        /// </summary>
        private void OnGUI()
        {
            DrawToolbar();
            DrawMainContent();
        }
        #endregion 生命週期

        #region 繪製方法
        /// <summary>
        /// 繪製工具列
        /// </summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 新增場景按鈕
            if (GUILayout.Button(new GUIContent("➕ 新增場景", "新增一個遊戲場景"), EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                AddNewScene();
            }

            // 同步按鈕
            if (GUILayout.Button(new GUIContent("🔄 同步", "同步至Build Settings"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                SyncToBuildSettings();
            }

            // 載入按鈕
            if (GUILayout.Button(new GUIContent("📥 載入", "從Build Settings載入"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                LoadFromBuildSettings();
            }

            GUILayout.FlexibleSpace();

            // FlowManager 狀態指示
            var flowManagerNode = BlueprintData.nodes?.FirstOrDefault(n => n.sceneName == "FlowManager");
            if (flowManagerNode != null)
            {
                bool hasAsset = flowManagerNode.sceneAsset != null;
                GUI.color = hasAsset ? Color.green : Color.yellow;
                GUILayout.Label(hasAsset ? "✓ FlowManager 就緒" : "⚠ FlowManager 未建立", EditorStyles.toolbarButton);
                GUI.color = Color.white;
            }

            GUILayout.Space(10);

            // 搜尋欄位
            GUILayout.Label("🔍", GUILayout.Width(20));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));

            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                _searchFilter = "";
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 繪製主要內容
        /// </summary>
        private void DrawMainContent()
        {
            EditorGUILayout.BeginHorizontal();

            // 左側：場景清單
            DrawSceneList();

            // 右側：場景詳細資訊
            DrawSceneDetails();

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 繪製場景清單
        /// </summary>
        private void DrawSceneList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f));

            // 標題
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 14;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField("📋 場景清單", headerStyle, GUILayout.Height(25));

            EditorGUILayout.Space(5);

            // 場景清單捲動區域
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUI.skin.box);

            if (BlueprintData.nodes != null && BlueprintData.nodes.Count > 0)
            {
                // 過濾場景
                var filteredNodes = string.IsNullOrEmpty(_searchFilter)
                    ? BlueprintData.nodes
                    : BlueprintData.nodes.Where(n => n.sceneName.ToLower().Contains(_searchFilter.ToLower())).ToList();

                for (int i = 0; i < filteredNodes.Count; i++)
                {
                    DrawSceneListItem(filteredNodes[i], i);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("尚無場景資料，請新增場景或從Build Settings載入", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();

            // 統計資訊
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUIStyle statsStyle = new GUIStyle(EditorStyles.miniLabel);
            statsStyle.alignment = TextAnchor.MiddleLeft;
            EditorGUILayout.LabelField($"總計: {BlueprintData.nodes?.Count ?? 0} 個場景", statsStyle);

            int linkedCount = BlueprintData.nodes?.Count(n => n.sceneAsset != null) ?? 0;
            EditorGUILayout.LabelField($"已連結: {linkedCount} 個", statsStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 繪製場景清單項目
        /// </summary>
        private void DrawSceneListItem(SceneNode node, int index)
        {
            bool isFlowManager = node.sceneName == "FlowManager";

            EditorGUILayout.BeginHorizontal();

            // 拖曳手柄（FlowManager 不能拖曳）
            if (isFlowManager)
            {
                GUI.color = Color.gray;
                GUILayout.Label("⚙", GUILayout.Width(20));
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label("≡", GUILayout.Width(20));
            }

            // 起始場景標記
            if (node.isStartNode)
            {
                GUI.color = isFlowManager ? Color.cyan : Color.green;
                GUILayout.Label(isFlowManager ? "◆" : "▶", GUILayout.Width(20));
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Space(20);
            }

            // 場景狀態圖示
            bool hasAsset = node.sceneAsset != null;
            GUI.color = hasAsset ? Color.green : (isFlowManager ? Color.yellow : Color.red);
            GUILayout.Label(hasAsset ? "✓" : (isFlowManager ? "!" : "✗"), GUILayout.Width(20));
            GUI.color = Color.white;

            // 場景名稱按鈕
            bool isSelected = _selectedNode == node;

            if (isFlowManager)
            {
                GUI.backgroundColor = isSelected ? new Color(0.3f, 0.6f, 0.9f) : new Color(0.4f, 0.4f, 0.5f);
            }
            else
            {
                GUI.backgroundColor = isSelected ? new Color(0.3f, 0.5f, 0.8f) : Color.white;
            }

            string displayName = isFlowManager ? "FlowManager [系統]" : node.sceneName;

            if (GUILayout.Button(displayName, EditorStyles.toolbarButton))
            {
                _selectedNode = node;
                _isEditingConnections = false;
            }

            GUI.backgroundColor = Color.white;

            // 連線數量標記（FlowManager 不顯示）
            if (!isFlowManager)
            {
                int connectionCount = node.connectedNodeIds?.Count ?? 0;
                if (connectionCount > 0)
                {
                    GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel);
                    badgeStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.4f, 0.6f, 1f));
                    badgeStyle.normal.textColor = Color.white;
                    badgeStyle.alignment = TextAnchor.MiddleCenter;
                    badgeStyle.padding = new RectOffset(4, 4, 1, 1);

                    GUILayout.Label($"{connectionCount}", badgeStyle, GUILayout.Width(25));
                }
            }

            EditorGUILayout.EndHorizontal();

            // 拖曳重新排序（FlowManager 不能重新排序）
            if (!isFlowManager && Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                _reorderFromIndex = BlueprintData.nodes.IndexOf(node);
            }
            else if (Event.current.type == EventType.MouseUp && _reorderFromIndex >= 0)
            {
                _reorderToIndex = BlueprintData.nodes.IndexOf(node);
                if (_reorderFromIndex != _reorderToIndex && _reorderToIndex >= 0)
                {
                    // 防止拖曳到 FlowManager 位置
                    if (BlueprintData.nodes[_reorderToIndex].sceneName != "FlowManager")
                    {
                        ReorderNodes(_reorderFromIndex, _reorderToIndex);
                    }
                }
                _reorderFromIndex = -1;
                _reorderToIndex = -1;
            }
        }

        /// <summary>
        /// 繪製場景詳細資訊
        /// </summary>
        private void DrawSceneDetails()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 標題
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 14;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField("📝 場景詳細資訊", headerStyle, GUILayout.Height(25));

            EditorGUILayout.Space(10);

            if (_selectedNode != null)
            {
                // 檢查是否為 FlowManager 場景
                bool isFlowManager = _selectedNode.sceneName == "FlowManager";

                // 場景名稱
                EditorGUILayout.LabelField("場景名稱", EditorStyles.boldLabel);

                if (isFlowManager)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField("FlowManager (系統場景)");
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    _selectedNode.sceneName = EditorGUILayout.TextField(_selectedNode.sceneName);
                }

                EditorGUILayout.Space(5);

                // 場景資源
                EditorGUILayout.LabelField("場景資源", EditorStyles.boldLabel);
                _selectedNode.sceneAsset = EditorGUILayout.ObjectField(_selectedNode.sceneAsset, typeof(SceneAsset), false) as SceneAsset;

                if (_selectedNode.sceneAsset == null)
                {
                    if (isFlowManager)
                    {
                        EditorGUILayout.HelpBox("FlowManager 場景尚未建立", MessageType.Error);

                        if (GUILayout.Button("建立 FlowManager 場景", GUILayout.Height(30)))
                        {
                            CreateFlowManagerScene(_selectedNode);
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("場景尚未連結資源檔案", MessageType.Warning);

                        if (GUILayout.Button("建立場景檔案", GUILayout.Height(25)))
                        {
                            CreateSceneFile(_selectedNode);
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button("開啟場景", GUILayout.Height(25)))
                    {
                        OpenScene(_selectedNode);
                    }
                }

                EditorGUILayout.Space(10);

                // 場景描述
                EditorGUILayout.LabelField("場景描述", EditorStyles.boldLabel);

                if (isFlowManager)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextArea("流程管理場景 - 管理整個遊戲流程，永遠保持載入", GUILayout.Height(50));
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    _selectedNode.description = EditorGUILayout.TextArea(_selectedNode.description, GUILayout.Height(50));
                }

                EditorGUILayout.Space(10);

                // 起始場景設定（FlowManager 永遠是起始場景）
                if (isFlowManager)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.Toggle("起始場景", true);
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.HelpBox("FlowManager 永遠是起始場景", MessageType.Info);
                }
                else
                {
                    // 其他場景不能設為起始場景
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.Toggle("起始場景", false);
                    EditorGUI.EndDisabledGroup();
                }

                EditorGUILayout.Space(10);

                // 場景連接設定（FlowManager 不顯示連接設定）
                if (isFlowManager)
                {
                    EditorGUILayout.LabelField("FlowManager 設定", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "FlowManager 管理所有場景載入：\n" +
                        "• 使用 Additive 方式載入遊戲場景\n" +
                        "• 自動管理場景切換和卸載\n" +
                        "• 保存全域資料和遊戲狀態\n\n" +
                        "請在 FlowManager 組件中設定第一個要載入的遊戲場景。",
                        MessageType.Info);
                }
                else
                {
                    DrawConnectionsSection();
                }

                EditorGUILayout.Space(10);

                // 操作按鈕
                EditorGUILayout.BeginHorizontal();

                // 刪除按鈕（FlowManager 不能刪除）
                EditorGUI.BeginDisabledGroup(isFlowManager);
                GUI.backgroundColor = isFlowManager ? Color.gray : new Color(0.8f, 0.3f, 0.3f);
                if (GUILayout.Button(isFlowManager ? "系統場景" : "刪除場景", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("刪除確認",
                        $"確定要刪除場景 '{_selectedNode.sceneName}' 嗎？",
                        "刪除", "取消"))
                    {
                        DeleteNode(_selectedNode);
                    }
                }
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup();

                // 複製按鈕（FlowManager 不能複製）
                EditorGUI.BeginDisabledGroup(isFlowManager);
                if (GUILayout.Button("複製場景", GUILayout.Height(30)))
                {
                    DuplicateNode(_selectedNode);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();

                // 儲存變更
                if (GUI.changed && !isFlowManager)
                {
                    SaveBlueprintData();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("請從左側清單選擇一個場景來查看詳細資訊", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 繪製連接設定區塊
        /// </summary>
        private void DrawConnectionsSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("可跳轉場景", EditorStyles.boldLabel);

            if (GUILayout.Button(_isEditingConnections ? "完成" : "編輯", GUILayout.Width(60)))
            {
                _isEditingConnections = !_isEditingConnections;
            }
            EditorGUILayout.EndHorizontal();

            if (_isEditingConnections)
            {
                // 編輯模式：顯示所有場景的勾選框（排除 FlowManager 和自己）
                EditorGUILayout.Space(5);

                foreach (var node in BlueprintData.nodes)
                {
                    // 排除自己和 FlowManager
                    if (node == _selectedNode || node.sceneName == "FlowManager") continue;

                    bool isConnected = _selectedNode.connectedNodeIds?.Contains(node.id) ?? false;
                    bool newValue = EditorGUILayout.Toggle(node.sceneName, isConnected);

                    if (newValue != isConnected)
                    {
                        if (_selectedNode.connectedNodeIds == null)
                            _selectedNode.connectedNodeIds = new List<string>();

                        if (newValue)
                        {
                            if (!_selectedNode.connectedNodeIds.Contains(node.id))
                                _selectedNode.connectedNodeIds.Add(node.id);
                        }
                        else
                        {
                            _selectedNode.connectedNodeIds.Remove(node.id);
                        }

                        SaveBlueprintData();
                    }
                }

                // 如果沒有可連接的場景
                if (BlueprintData.nodes.Count <= 2) // 只有 FlowManager 和當前場景
                {
                    EditorGUILayout.LabelField("沒有其他場景可連接", EditorStyles.centeredGreyMiniLabel);
                }
            }
            else
            {
                // 檢視模式：顯示已連接的場景
                if (_selectedNode.connectedNodeIds != null && _selectedNode.connectedNodeIds.Count > 0)
                {
                    // 過濾掉 FlowManager
                    var validConnections = new List<SceneNode>();
                    foreach (var nodeId in _selectedNode.connectedNodeIds)
                    {
                        var connectedNode = BlueprintData.nodes.FirstOrDefault(n => n.id == nodeId);
                        if (connectedNode != null && connectedNode.sceneName != "FlowManager")
                        {
                            validConnections.Add(connectedNode);
                        }
                    }

                    if (validConnections.Count > 0)
                    {
                        foreach (var connectedNode in validConnections)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("→", GUILayout.Width(20));

                            if (GUILayout.Button(connectedNode.sceneName, EditorStyles.linkLabel))
                            {
                                _selectedNode = connectedNode;
                            }

                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("無連接場景", EditorStyles.centeredGreyMiniLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("無連接場景", EditorStyles.centeredGreyMiniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }
        #endregion 繪製方法

        #region 場景操作
        /// <summary>
        /// 新增場景
        /// </summary>
        private void AddNewScene()
        {
            // 計算新場景名稱（排除 FlowManager）
            int gameSceneCount = BlueprintData.nodes.Count(n => n.sceneName != "FlowManager");
            SceneNode newNode = new SceneNode($"GameScene_{gameSceneCount + 1}");

            BlueprintData.nodes.Add(newNode);
            _selectedNode = newNode;
            SaveBlueprintData();
        }

        /// <summary>
        /// 建立 FlowManager 場景
        /// </summary>
        private void CreateFlowManagerScene(SceneNode node)
        {
            string path = "Assets/Scenes/FlowManager.unity";

            // 確保 Scenes 資料夾存在
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            // 建立新場景
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            newScene.name = "FlowManager";

            // 添加 FlowManager GameObject
            GameObject flowManagerObj = new GameObject("[FlowManager]");

            // 使用反射添加 FlowManager 組件
            var flowManagerType = System.Type.GetType("CatzTools.FlowManager, Assembly-CSharp");
            if (flowManagerType != null)
            {
                flowManagerObj.AddComponent(flowManagerType);

                // 添加 SceneEvents 作為子物件
                GameObject sceneEventsObj = new GameObject("[SceneEvents]");
                var sceneEventsType = System.Type.GetType("CatzTools.SceneEvents, Assembly-CSharp");
                if (sceneEventsType != null)
                {
                    sceneEventsObj.AddComponent(sceneEventsType);
                    sceneEventsObj.transform.SetParent(flowManagerObj.transform);
                }

                Debug.Log("已建立 FlowManager 場景並添加必要組件");
            }

            // 添加基本燈光（讓場景不會全黑）
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 0.5f;
            lightObj.transform.rotation = Quaternion.Euler(30, -30, 0);

            // 儲存場景
            EditorSceneManager.SaveScene(newScene, path);

            // 更新節點資源
            node.sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            AssetDatabase.Refresh();
            SaveBlueprintData();

            // 選中 FlowManager
            UnityEditor.Selection.activeGameObject = flowManagerObj;

            EditorUtility.DisplayDialog("FlowManager 場景已建立",
                "FlowManager 場景已建立完成。\n\n請在 FlowManager 組件中設定第一個要載入的遊戲場景。",
                "確定");
        }

        /// <summary>
        /// 刪除節點
        /// </summary>
        private void DeleteNode(SceneNode node)
        {
            if (node.isStartNode)
            {
                EditorUtility.DisplayDialog("無法刪除", "無法刪除起始場景！", "確定");
                return;
            }

            // 移除所有指向此節點的連接
            foreach (var otherNode in BlueprintData.nodes)
            {
                otherNode.connectedNodeIds?.Remove(node.id);
            }

            BlueprintData.nodes.Remove(node);

            if (_selectedNode == node)
                _selectedNode = null;

            SaveBlueprintData();
        }

        /// <summary>
        /// 複製節點
        /// </summary>
        private void DuplicateNode(SceneNode node)
        {
            // 不能複製 FlowManager
            if (node.sceneName == "FlowManager")
            {
                EditorUtility.DisplayDialog("無法複製", "FlowManager 場景不能被複製！", "確定");
                return;
            }

            SceneNode newNode = new SceneNode(node.sceneName + "_Copy");
            newNode.sceneAsset = null;  // 複製的場景需要新的場景檔案
            newNode.description = node.description;
            newNode.isStartNode = false;  // 複製的場景永遠不是起始場景

            BlueprintData.nodes.Add(newNode);
            _selectedNode = newNode;
            SaveBlueprintData();
        }

        /// <summary>
        /// 重新排序節點
        /// </summary>
        private void ReorderNodes(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= BlueprintData.nodes.Count ||
                toIndex < 0 || toIndex >= BlueprintData.nodes.Count)
                return;

            // 防止移動 FlowManager
            if (BlueprintData.nodes[fromIndex].sceneName == "FlowManager")
                return;

            // 防止移動到 FlowManager 的位置（位置 0）
            if (toIndex == 0 && BlueprintData.nodes[0].sceneName == "FlowManager")
            {
                toIndex = 1;  // 改為移動到第二個位置
            }

            var node = BlueprintData.nodes[fromIndex];
            BlueprintData.nodes.RemoveAt(fromIndex);
            BlueprintData.nodes.Insert(toIndex, node);
            SaveBlueprintData();
        }

        /// <summary>
        /// 建立場景檔案
        /// </summary>
        private void CreateSceneFile(SceneNode node)
        {
            string path = EditorUtility.SaveFilePanel("儲存場景", "Assets/Scenes", node.sceneName, "unity");

            if (!string.IsNullOrEmpty(path))
            {
                path = FileUtil.GetProjectRelativePath(path);

                var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

                EditorSceneManager.SaveScene(newScene, path);

                node.sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                AssetDatabase.Refresh();
                SaveBlueprintData();
            }
        }

        /// <summary>
        /// 開啟場景
        /// </summary>
        private void OpenScene(SceneNode node)
        {
            if (node.sceneAsset != null)
            {
                string scenePath = AssetDatabase.GetAssetPath(node.sceneAsset);

                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);

                    // 如果是 FlowManager 場景，確保有 FlowManager 組件
                    if (node.sceneName == "FlowManager")
                    {
                        EnsureFlowManagerInScene();
                    }
                }
            }
        }

        /// <summary>
        /// 確保場景中有 FlowManager
        /// </summary>
        private void EnsureFlowManagerInScene()
        {
            // 尋找場景中的 FlowManager
            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            bool hasFlowManager = false;
            GameObject existingFlowManager = null;

            foreach (var obj in allObjects)
            {
                if (obj.name == "[FlowManager]")
                {
                    hasFlowManager = true;
                    existingFlowManager = obj;
                    break;
                }
            }

            if (!hasFlowManager)
            {
                GameObject flowManagerObj = new GameObject("[FlowManager]");

                // 使用反射添加 FlowManager 組件
                var flowManagerType = System.Type.GetType("CatzTools.FlowManager, Assembly-CSharp");
                if (flowManagerType != null)
                {
                    var flowManagerComp = flowManagerObj.AddComponent(flowManagerType);

                    // 添加 SceneEvents 作為子物件
                    GameObject sceneEventsObj = new GameObject("[SceneEvents]");
                    var sceneEventsType = System.Type.GetType("CatzTools.SceneEvents, Assembly-CSharp");
                    if (sceneEventsType != null)
                    {
                        sceneEventsObj.AddComponent(sceneEventsType);
                        sceneEventsObj.transform.SetParent(flowManagerObj.transform);
                    }

                    // 標記為修改
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                        UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

                    Debug.Log("已添加 FlowManager 和 SceneEvents 到 FlowManager 場景");

                    // 選中物件以便編輯
                    UnityEditor.Selection.activeGameObject = flowManagerObj;
                }
                else
                {
                    Debug.LogWarning("找不到 FlowManager 類型，請確保腳本已編譯");
                    GameObject.DestroyImmediate(flowManagerObj);
                }
            }
            else
            {
                Debug.Log("FlowManager 場景中已存在 FlowManager");

                // 確保有 SceneEvents 子物件
                Transform sceneEventsTransform = existingFlowManager.transform.Find("[SceneEvents]");
                if (sceneEventsTransform == null)
                {
                    GameObject sceneEventsObj = new GameObject("[SceneEvents]");
                    var sceneEventsType = System.Type.GetType("CatzTools.SceneEvents, Assembly-CSharp");
                    if (sceneEventsType != null)
                    {
                        sceneEventsObj.AddComponent(sceneEventsType);
                        sceneEventsObj.transform.SetParent(existingFlowManager.transform);

                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

                        Debug.Log("已添加 SceneEvents 到 FlowManager");
                    }
                }

                // 選中 FlowManager
                UnityEditor.Selection.activeGameObject = existingFlowManager;
            }
        }

        /// <summary>
        /// 確保有起始節點
        /// </summary>
        private void EnsureStartNode()
        {
            if (BlueprintData.nodes == null || BlueprintData.nodes.Count == 0)
            {
                SceneNode startNode = new SceneNode("StartScene");
                startNode.isStartNode = true;
                BlueprintData.nodes = new List<SceneNode> { startNode };
                SaveBlueprintData();
            }
            else if (!BlueprintData.nodes.Any(n => n.isStartNode))
            {
                BlueprintData.nodes[0].isStartNode = true;
                SaveBlueprintData();
            }
        }

        /// <summary>
        /// 同步至Build Settings
        /// </summary>
        private void SyncToBuildSettings()
        {
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();

            // 首先確保 FlowManager 場景存在且為第一個
            var flowManagerNode = BlueprintData.nodes.FirstOrDefault(n => n.sceneName == "FlowManager");
            if (flowManagerNode?.sceneAsset != null)
            {
                string path = AssetDatabase.GetAssetPath(flowManagerNode.sceneAsset);
                buildScenes.Add(new EditorBuildSettingsScene(path, true));
            }
            else
            {
                EditorUtility.DisplayDialog("警告",
                    "FlowManager 場景尚未建立！\n請先建立 FlowManager 場景。",
                    "確定");
                return;
            }

            // 加入其他場景
            foreach (var node in BlueprintData.nodes.Where(n => n.sceneName != "FlowManager"))
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

            EditorUtility.DisplayDialog("同步完成",
                $"已將 {buildScenes.Count} 個場景同步至 Build Settings\n" +
                $"起始場景: FlowManager\n" +
                $"遊戲場景: {buildScenes.Count - 1} 個",
                "確定");
        }

        /// <summary>
        /// 從Build Settings載入
        /// </summary>
        private void LoadFromBuildSettings()
        {
            if (BlueprintData.nodes.Count > 1)  // 保留 FlowManager 節點
            {
                if (!EditorUtility.DisplayDialog("載入場景",
                    "這將清除現有資料（FlowManager 除外）並從Build Settings載入場景，是否繼續？",
                    "繼續", "取消"))
                {
                    return;
                }
            }

            // 保留 FlowManager 節點
            var flowManagerNode = BlueprintData.nodes.FirstOrDefault(n => n.sceneName == "FlowManager");

            BlueprintData.nodes.Clear();

            // 重新加入 FlowManager
            if (flowManagerNode != null)
            {
                BlueprintData.nodes.Add(flowManagerNode);
            }
            else
            {
                // 如果沒有 FlowManager，建立一個
                EnsureStartNode();
            }

            var buildScenes = EditorBuildSettings.scenes;

            for (int i = 0; i < buildScenes.Length; i++)
            {
                if (!string.IsNullOrEmpty(buildScenes[i].path))
                {
                    SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScenes[i].path);

                    if (sceneAsset != null && sceneAsset.name != "FlowManager")
                    {
                        SceneNode node = new SceneNode(sceneAsset.name);
                        node.sceneAsset = sceneAsset;
                        node.isStartNode = false;  // 只有 FlowManager 是起始場景
                        BlueprintData.nodes.Add(node);
                    }
                }
            }

            SaveBlueprintData();

            EditorUtility.DisplayDialog("載入完成",
                $"已從Build Settings載入 {BlueprintData.nodes.Count - 1} 個遊戲場景（加上 FlowManager）",
                "確定");
        }
        #endregion 場景操作

        #region 工具方法
        /// <summary>
        /// 建立純色材質
        /// </summary>
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }
        #endregion 工具方法

        #region 資料管理
        /// <summary>
        /// 載入或建立藍圖資料
        /// </summary>
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
                data.nodes = new List<SceneNode>();

                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }

                string dataPath = "Assets/Resources/SceneBlueprintData.asset";
                AssetDatabase.CreateAsset(data, dataPath);
                AssetDatabase.SaveAssets();

                Debug.Log($"建立新的場景結構資料: {dataPath}");
            }

            return data;
        }

        /// <summary>
        /// 儲存藍圖資料
        /// </summary>
        private void SaveBlueprintData()
        {
            if (BlueprintData != null)
            {
                EditorUtility.SetDirty(BlueprintData);
                AssetDatabase.SaveAssets();
            }
        }
        #endregion 資料管理
    }
    #endregion 場景結構編輯器
}
#endif