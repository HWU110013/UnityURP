#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using CatzTools.GameFlow;

namespace CatzTools.GameFlow.Editor
{
    #region SceneEvent 自訂 Inspector
    /// <summary>
    /// SceneEvent 自訂 Inspector — 場景名稱唯讀、顯示可關聯場景清單、提供轉場測試與觸發產生按鈕
    /// </summary>
    [CustomEditor(typeof(SceneEvent))]
    public class SceneEventEditor : UnityEditor.Editor
    {
        #region 私有變數
        private SceneEvent _target;
        private SceneBlueprintData _blueprintData;
        private List<string> _connectedScenes;
        private List<string> _availableCovers;
        private string _selfSceneName;
        private bool _foldoutConnections = true;
        private bool _foldoutCovers = true;
        #endregion 私有變數

        #region Lazy Loading
        private SceneBlueprintData BlueprintData
        {
            get
            {
                if (_blueprintData == null)
                    _blueprintData = Resources.Load<SceneBlueprintData>("SceneBlueprintData");
                return _blueprintData;
            }
        }
        #endregion Lazy Loading

        #region Unity Editor
        private void OnEnable()
        {
            _target = (SceneEvent)target;
            RefreshConnectedScenes();
        }

        public override void OnInspectorGUI()
        {
            // NotEditable 會鎖死 Inspector，手動解鎖讓元件可編輯
            GUI.enabled = true;
            serializedObject.Update();

            // ── 場景名稱（唯讀）──
            EditorGUILayout.Space(4);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(SceneFlowLocale.SeSceneName, _target.SceneName);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(2);

            // ── Log 狀態 ──
            CatzTools.Editor.CatzEditorStyles.DrawLogChannelStatus("FlowManager");

            EditorGUILayout.Space(8);

            // ── 可關聯場景清單 ──
            DrawConnectedScenes();

            // ── 可用 Cover 清單 ──
            DrawAvailableCovers();

            EditorGUILayout.Space(4);

            // ── 刷新按鈕 ──
            if (GUILayout.Button("🔃 刷新場景清單"))
            {
                _blueprintData = null;
                RefreshConnectedScenes();
            }

            // ── 清理重複共用單例 ──
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            if (GUILayout.Button("🧹 清理重複共用單例（EventSystem / AudioListener / InputModule）"))
            {
                int removed = _target.CleanupSharedSingletons();
                if (removed > 0)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_target.gameObject.scene);
                    EditorUtility.DisplayDialog("Scene 清理",
                        $"已從場景 '{_target.gameObject.scene.name}' 移除 {removed} 個重複組件。\n建議儲存場景。",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Scene 清理",
                        $"場景 '{_target.gameObject.scene.name}' 沒有重複組件，無需清理。",
                        "OK");
                }
            }
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }
        #endregion Unity Editor

        #region 可關聯場景清單
        /// <summary>
        /// 繪製可關聯場景清單 + 轉場測試按鈕 + 產生觸發按鈕
        /// </summary>
        private void DrawConnectedScenes()
        {
            _foldoutConnections = EditorGUILayout.Foldout(_foldoutConnections,
                SceneFlowLocale.SeConnectedScenes(_connectedScenes?.Count ?? 0), true);

            if (!_foldoutConnections) return;

            EditorGUI.indentLevel++;

            if (_connectedScenes == null || _connectedScenes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    SceneFlowLocale.SeNoConnected,
                    MessageType.Info);
                EditorGUI.indentLevel--;
                return;
            }

            foreach (var sceneName in _connectedScenes)
            {
                bool isSelf = sceneName == _selfSceneName;

                EditorGUILayout.BeginHorizontal();

                // 場景名稱（唯讀），self 行加前綴標示
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(isSelf ? SceneFlowLocale.SeSelfLabel(sceneName) : sceneName);
                EditorGUI.EndDisabledGroup();

                // 測試按鈕（僅 Play Mode）— self 呼叫 RequestReload，其他呼叫 RequestTransition
                EditorGUI.BeginDisabledGroup(!Application.isPlaying);
                string btnLabel = isSelf ? SceneFlowLocale.SeBtnReload : SceneFlowLocale.SeBtnTest;
                if (GUILayout.Button(btnLabel, GUILayout.Width(60)))
                {
                    if (isSelf) _target.RequestReload();
                    else _target.RequestTransition(sceneName);
                }
                EditorGUI.EndDisabledGroup();

                // 產生觸發按鈕（僅 Edit Mode）
                EditorGUI.BeginDisabledGroup(Application.isPlaying);
                if (GUILayout.Button(SceneFlowLocale.SeBtnAddTrigger, GUILayout.Width(70)))
                {
                    ShowTriggerMenu(sceneName);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;

            // 提示
            if (!Application.isPlaying && _connectedScenes.Count > 0)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(
                    SceneFlowLocale.SeHint,
                    MessageType.None);
            }
        }
        #endregion 可關聯場景清單

        #region 可用 Cover 清單
        /// <summary>
        /// 繪製可用 Cover 清單 + 測試按鈕 + 觸發產生
        /// </summary>
        private void DrawAvailableCovers()
        {
            _foldoutCovers = EditorGUILayout.Foldout(_foldoutCovers,
                SceneFlowLocale.SeCovers(_availableCovers?.Count ?? 0), true);

            if (!_foldoutCovers) return;

            EditorGUI.indentLevel++;

            if (_availableCovers == null || _availableCovers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    SceneFlowLocale.SeNoCover,
                    MessageType.Info);
                EditorGUI.indentLevel--;
                return;
            }

            foreach (var coverName in _availableCovers)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField($"◻ {coverName}");
                EditorGUI.EndDisabledGroup();

                // 測試開啟（僅 Play Mode）
                EditorGUI.BeginDisabledGroup(!Application.isPlaying);
                if (GUILayout.Button(SceneFlowLocale.SeBtnOpen, GUILayout.Width(60)))
                {
                    _target.RequestShowCover(coverName);
                }
                EditorGUI.EndDisabledGroup();

                // 產生觸發按鈕
                EditorGUI.BeginDisabledGroup(Application.isPlaying);
                if (GUILayout.Button(SceneFlowLocale.SeBtnAddTrigger, GUILayout.Width(70)))
                {
                    ShowCoverTriggerMenu(coverName);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 顯示 Cover 觸發器類型選單
        /// </summary>
        private void ShowCoverTriggerMenu(string coverName)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(SceneFlowLocale.TrigCoverOpen), false, () => CreateCoverButtonTrigger(coverName, CoverAction.Show));
            menu.AddItem(new GUIContent(SceneFlowLocale.TrigCoverClose), false, () => CreateCoverButtonTrigger(coverName, CoverAction.Hide));
            menu.AddItem(new GUIContent(SceneFlowLocale.TrigCoverToggle), false, () => CreateCoverButtonTrigger(coverName, CoverAction.Toggle));
            menu.ShowAsContext();
        }

        /// <summary>
        /// 產生 Cover 按鈕觸發器
        /// </summary>
        private void CreateCoverButtonTrigger(string coverName, CoverAction action)
        {
            string actionLabel = action switch
            {
                CoverAction.Show => "Open",
                CoverAction.Hide => "Close",
                CoverAction.Toggle => "Toggle",
                _ => "Cover"
            };

            // 尋找場景中的 Canvas
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("[SceneFlow] CoverCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasObj, "建立 Canvas");
            }

            var btnObj = new GameObject($"[SceneFlow] {actionLabel}_{coverName}");
            btnObj.transform.SetParent(canvas.transform, false);

            var rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 40);

            var image = btnObj.AddComponent<UnityEngine.UI.Image>();
            image.color = action == CoverAction.Show
                ? new Color(0.5f, 0.2f, 0.6f, 0.9f)
                : new Color(0.4f, 0.4f, 0.4f, 0.9f);

            btnObj.AddComponent<UnityEngine.UI.Button>();
            var trigger = btnObj.AddComponent<ButtonCoverTrigger>();

            // 設定 Cover 名稱和動作
            var so = new SerializedObject(trigger);
            so.FindProperty("_coverName").stringValue = coverName;
            so.FindProperty("_action").enumValueIndex = (int)action;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 文字
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<UnityEngine.UI.Text>();
            text.text = $"{actionLabel} {coverName}";
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 14;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Undo.RegisterCreatedObjectUndo(btnObj, $"建立 Cover 觸發器 → {coverName}");
            Selection.activeGameObject = btnObj;

            CatzLogger.Log("FlowManager", $"[SceneEventEditor] 已建立 Cover {actionLabel} 觸發器 → {coverName}");
        }
        #endregion 可用 Cover 清單

        #region 觸發產生選單
        /// <summary>
        /// 顯示觸發器類型選單
        /// </summary>
        private void ShowTriggerMenu(string targetScene)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(SceneFlowLocale.TrigAuto), false, () => CreateAutoTrigger(targetScene));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(SceneFlowLocale.TrigButton), false, () => CreateButtonTrigger(targetScene));
            menu.AddItem(new GUIContent(SceneFlowLocale.TrigCollider3D), false, () => CreateColliderTrigger(targetScene, false));
            menu.AddItem(new GUIContent(SceneFlowLocale.TrigCollider2D), false, () => CreateColliderTrigger(targetScene, true));
            // 互動鍵觸發器選項已移除（v0.7.7b 跨工具零源碼耦合修正）。
            // 改用：場景上掛 InputSys.InputActionTrigger，UnityEvent 拉到 SceneTransitionTrigger.Transition()。
            menu.ShowAsContext();
        }
        #endregion 觸發產生選單

        #region 產生觸發物件
        /// <summary>
        /// 產生自動轉場觸發器
        /// </summary>
        private void CreateAutoTrigger(string targetScene)
        {
            var obj = new GameObject($"[SceneFlow] AutoTransition_GoTo_{targetScene}");
            var trigger = obj.AddComponent<AutoTransitionTrigger>();
            SetTargetScene(trigger, targetScene);

            Undo.RegisterCreatedObjectUndo(obj, $"建立自動轉場 → {targetScene}");
            Selection.activeGameObject = obj;

            CatzLogger.Log("FlowManager", $"[SceneEventEditor] 已建立自動轉場觸發器 → {targetScene}");
        }

        /// <summary>
        /// 產生 UI 按鈕轉場觸發器
        /// </summary>
        private void CreateButtonTrigger(string targetScene)
        {
            // 尋找場景中的 Canvas，沒有就建立
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("[SceneFlow] TransitionCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasObj, "建立 TransitionCanvas");
            }

            // 建立 Button
            var btnObj = new GameObject($"[SceneFlow] Btn_GoTo_{targetScene}");
            btnObj.transform.SetParent(canvas.transform, false);

            var rectTransform = btnObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 60);

            var image = btnObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            btnObj.AddComponent<Button>();

            var trigger = btnObj.AddComponent<ButtonTransitionTrigger>();
            SetTargetScene(trigger, targetScene);

            // 建立文字子物件
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"→ {targetScene}";
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            Undo.RegisterCreatedObjectUndo(btnObj, $"建立按鈕觸發器 → {targetScene}");
            Selection.activeGameObject = btnObj;

            CatzLogger.Log("FlowManager", $"[SceneEventEditor] 已建立 UI 按鈕觸發器 → {targetScene}");
        }

        /// <summary>
        /// 產生碰撞轉場觸發器
        /// </summary>
        private void CreateColliderTrigger(string targetScene, bool is2D)
        {
            var obj = new GameObject($"[SceneFlow] ColliderTrigger_GoTo_{targetScene}");

            if (is2D)
            {
                var col = obj.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(2f, 2f);
            }
            else
            {
                var col = obj.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(2f, 2f, 2f);
            }

            var trigger = obj.AddComponent<ColliderTransitionTrigger>();
            SetTargetScene(trigger, targetScene);

            Undo.RegisterCreatedObjectUndo(obj, $"建立碰撞觸發器 → {targetScene}");
            Selection.activeGameObject = obj;

            CatzLogger.Log("FlowManager", $"[SceneEventEditor] 已建立碰撞觸發器{(is2D ? " (2D)" : " (3D)")} → {targetScene}");
        }

        // CreateInteractionTrigger 已於 v0.7.7b 移除 — 違反跨工具零源碼耦合鐵律。
        // 互動觸發改在 Scene 用 InputSys.InputActionTrigger + UnityEvent → SceneTransitionTrigger.Transition()。

        /// <summary>
        /// 透過 SerializedObject 設定目標場景（支援 Undo）
        /// </summary>
        private void SetTargetScene(SceneTransitionTrigger trigger, string targetScene)
        {
            var so = new SerializedObject(trigger);
            so.FindProperty("_targetScene").stringValue = targetScene;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion 產生觸發物件

        #region 資料查詢
        /// <summary>
        /// 從藍圖資料查詢此場景可連接的目標場景。
        /// 第一筆為自己（重載入口），後續為從 edge 查詢到的轉場目標。
        /// </summary>
        private void RefreshConnectedScenes()
        {
            _connectedScenes = new List<string>();
            _availableCovers = new List<string>();
            _selfSceneName = null;

            if (BlueprintData == null) return;

            string currentName = _target.SceneName;
            if (string.IsNullOrEmpty(currentName))
            {
                var scene = _target.gameObject.scene;
                if (scene.IsValid())
                    currentName = scene.name;
            }

            if (string.IsNullOrEmpty(currentName)) return;

            // 自己永遠列第一（作為重載入口），即使在藍圖中找不到節點也能觸發 reload
            _selfSceneName = currentName;
            _connectedScenes.Add(currentName);

            var node = BlueprintData.FindNodeByName(currentName);
            if (node == null) return;

            // 可轉場的場景（從 edge 查詢）— 排除自身避免重複
            var reachableIds = BlueprintData.GetReachableTargets(node.id);
            foreach (var targetId in reachableIds)
            {
                var targetNode = BlueprintData.FindNodeById(targetId);
                if (targetNode == null || targetNode.nodeType != SceneNodeType.Scene) continue;
                if (targetNode.sceneName == currentName) continue; // 已作為 self 加入
                _connectedScenes.Add(targetNode.sceneName);
            }

            // 可用 Cover（從綁定清單查詢）
            var covers = BlueprintData.GetAvailableCovers(currentName);
            foreach (var cover in covers)
                _availableCovers.Add(cover.sceneName);
        }
        #endregion 資料查詢
    }
    #endregion SceneEvent 自訂 Inspector
}
#endif
