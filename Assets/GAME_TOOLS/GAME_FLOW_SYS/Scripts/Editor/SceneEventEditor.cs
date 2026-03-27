#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace CatzTools
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
        private bool _foldoutConnections = true;
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
            EditorGUILayout.TextField("場景名稱", _target.SceneName);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(2);

            // ── Debug Log 開關 ──
            var debugProp = serializedObject.FindProperty("_showDebugLogs");
            EditorGUILayout.PropertyField(debugProp, new GUIContent("顯示 Debug Log"));

            EditorGUILayout.Space(8);

            // ── 可關聯場景清單 ──
            DrawConnectedScenes();

            EditorGUILayout.Space(4);

            // ── 刷新按鈕 ──
            if (GUILayout.Button("🔃 刷新場景清單"))
            {
                _blueprintData = null;
                RefreshConnectedScenes();
            }

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
                $"可關聯場景（{_connectedScenes?.Count ?? 0}）", true);

            if (!_foldoutConnections) return;

            EditorGUI.indentLevel++;

            if (_connectedScenes == null || _connectedScenes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "尚無關聯場景。請在「場景流程圖」中建立連線。",
                    MessageType.Info);
                EditorGUI.indentLevel--;
                return;
            }

            foreach (var sceneName in _connectedScenes)
            {
                EditorGUILayout.BeginHorizontal();

                // 場景名稱（唯讀）
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(sceneName);
                EditorGUI.EndDisabledGroup();

                // 轉場測試按鈕（僅 Play Mode）
                EditorGUI.BeginDisabledGroup(!Application.isPlaying);
                if (GUILayout.Button("▶ 測試", GUILayout.Width(60)))
                {
                    _target.RequestTransition(sceneName);
                }
                EditorGUI.EndDisabledGroup();

                // 產生觸發按鈕（僅 Edit Mode）
                EditorGUI.BeginDisabledGroup(Application.isPlaying);
                if (GUILayout.Button("＋ 觸發 ▾", GUILayout.Width(70)))
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
                    "「▶ 測試」需在 Play Mode。「＋ 觸發」可在 Edit Mode 產生觸發物件。",
                    MessageType.None);
            }
        }
        #endregion 可關聯場景清單

        #region 觸發產生選單
        /// <summary>
        /// 顯示觸發器類型選單
        /// </summary>
        private void ShowTriggerMenu(string targetScene)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("UI 按鈕"), false, () => CreateButtonTrigger(targetScene));
            menu.AddItem(new GUIContent("碰撞觸發器 (3D)"), false, () => CreateColliderTrigger(targetScene, false));
            menu.AddItem(new GUIContent("碰撞觸發器 (2D)"), false, () => CreateColliderTrigger(targetScene, true));
            menu.AddItem(new GUIContent("互動鍵觸發器 (3D)"), false, () => CreateInteractionTrigger(targetScene, false));
            menu.AddItem(new GUIContent("互動鍵觸發器 (2D)"), false, () => CreateInteractionTrigger(targetScene, true));
            menu.ShowAsContext();
        }
        #endregion 觸發產生選單

        #region 產生觸發物件
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

            Debug.Log($"[SceneEventEditor] 已建立 UI 按鈕觸發器 → {targetScene}");
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

            Debug.Log($"[SceneEventEditor] 已建立碰撞觸發器{(is2D ? " (2D)" : " (3D)")} → {targetScene}");
        }

        /// <summary>
        /// 產生互動鍵轉場觸發器
        /// </summary>
        private void CreateInteractionTrigger(string targetScene, bool is2D)
        {
            var obj = new GameObject($"[SceneFlow] InteractionTrigger_GoTo_{targetScene}");

            if (is2D)
            {
                var col = obj.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(3f, 3f);
            }
            else
            {
                var col = obj.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(3f, 3f, 3f);
            }

            var trigger = obj.AddComponent<InteractionTransitionTrigger>();
            SetTargetScene(trigger, targetScene);

            Undo.RegisterCreatedObjectUndo(obj, $"建立互動鍵觸發器 → {targetScene}");
            Selection.activeGameObject = obj;

            Debug.Log($"[SceneEventEditor] 已建立互動鍵觸發器{(is2D ? " (2D)" : " (3D)")} → {targetScene}");
        }

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
        /// 從藍圖資料查詢此場景可連接的目標場景
        /// </summary>
        private void RefreshConnectedScenes()
        {
            _connectedScenes = new List<string>();

            if (BlueprintData == null) return;

            string currentName = _target.SceneName;
            if (string.IsNullOrEmpty(currentName))
            {
                // 嘗試從場景取得
                var scene = _target.gameObject.scene;
                if (scene.IsValid())
                    currentName = scene.name;
            }

            if (string.IsNullOrEmpty(currentName)) return;

            // 找到此場景的節點
            var node = BlueprintData.FindNodeByName(currentName);
            if (node == null) return;

            // 依方向取得可轉場的目標場景
            var reachableIds = BlueprintData.GetReachableTargets(node.id);
            foreach (var targetId in reachableIds)
            {
                var targetNode = BlueprintData.FindNodeById(targetId);
                if (targetNode != null && targetNode.nodeType != SceneNodeType.End)
                {
                    _connectedScenes.Add(targetNode.sceneName);
                }
            }
        }
        #endregion 資料查詢
    }
    #endregion SceneEvent 自訂 Inspector
}
#endif
