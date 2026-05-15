#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using CatzTools.GameFlow;

namespace CatzTools.GameFlow.Editor
{
    #region CoverController 自訂 Inspector
    /// <summary>
    /// CoverController 自訂 Inspector — 提供快速建立 UI 元件與播放模式測試按鈕
    /// </summary>
    [CustomEditor(typeof(CoverController))]
    public class CoverControllerEditor : UnityEditor.Editor
    {
        private CoverController _target;

        private void OnEnable()
        {
            _target = (CoverController)target;
        }

        public override void OnInspectorGUI()
        {
            // CatzTools 標準工具列
            if (CatzTools.Editor.CatzInspectorHeader.Draw(target, serializedObject)) return;

            // NotEditable 會鎖死 Inspector，手動解鎖讓元件可編輯
            if ((_target.gameObject.hideFlags & HideFlags.NotEditable) != 0)
                _target.gameObject.hideFlags &= ~HideFlags.NotEditable;

            serializedObject.Update();

            // ── 標題 ──
            EditorGUILayout.LabelField(SceneFlowLocale.CcTitle, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── 事件 ──
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_onOpened"),
                new GUIContent(SceneFlowLocale.CcOnOpenEvent));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_onClosing"),
                new GUIContent(SceneFlowLocale.CcOnCloseEvent));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_onVisibilityChanged"),
                new GUIContent(SceneFlowLocale.CcOnVisibility));
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);

            // ── 快速建立（編輯模式）──
            if (!Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(SceneFlowLocale.CcQuickCreate, EditorStyles.boldLabel);

                // ── 區塊 A：本 Cover ──
                EditorGUILayout.LabelField(SceneFlowLocale.CcSectionSelf, EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(SceneFlowLocale.CcBtnClose, EditorStyles.miniButton))
                    CreateCloseButton();
                if (GUILayout.Button(SceneFlowLocale.CcBtnConfirm, EditorStyles.miniButton))
                    CreateActionButton("確認", new Color(0.2f, 0.5f, 0.2f));
                if (GUILayout.Button(SceneFlowLocale.CcBtnCancel, EditorStyles.miniButton))
                    CreateActionButton("取消", new Color(0.5f, 0.2f, 0.2f));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // ── 區塊 B：呼叫其他 Cover ──
                EditorGUILayout.LabelField(SceneFlowLocale.CcSectionOther, EditorStyles.miniLabel);
                if (GUILayout.Button(SceneFlowLocale.CcOtherCoverBtn, EditorStyles.miniButton))
                    ShowCoverActionMenu();

                EditorGUILayout.Space(4);

                // ── 區塊 C：UI 元素 ──
                EditorGUILayout.LabelField(SceneFlowLocale.CcSectionUI, EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(SceneFlowLocale.CcTitleText, EditorStyles.miniButton))
                    ShowTextMenu("標題", 32, TMPro.TextAlignmentOptions.Center,
                        new Vector2(0.2f, 0.85f), new Vector2(0.8f, 0.95f));
                if (GUILayout.Button(SceneFlowLocale.CcContentText, EditorStyles.miniButton))
                    ShowTextMenu("內容", 18, TMPro.TextAlignmentOptions.TopLeft,
                        new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            // ── 播放模式測試 ──
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(SceneFlowLocale.CcTestSection, EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                var cg = _target.GetComponent<CanvasGroup>();
                bool isVisible = cg != null && cg.alpha > 0.5f;

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUI.enabled = !isVisible;
                if (GUILayout.Button("▶", GUILayout.Width(50), GUILayout.Height(50)))
                    _ = _target.Show();

                GUILayout.Space(8);

                GUI.enabled = isVisible;
                if (GUILayout.Button("■", GUILayout.Width(50), GUILayout.Height(50)))
                    _ = _target.Hide();

                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // 狀態
                EditorGUILayout.Space(4);
                var color = isVisible ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
                var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    { normal = { textColor = color } };
                EditorGUILayout.LabelField(isVisible ? SceneFlowLocale.CcShowing : SceneFlowLocale.CcHidden, style);

                EditorGUILayout.EndVertical();
                Repaint();
            }
        }

        #region Lazy Loading
        private SceneBlueprintData _blueprintData;
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

        #region 快速建立方法

        /// <summary>
        /// 顯示「呼叫其他 Cover」選單：Cover 名稱 → 動作（開啟/關閉/切換）
        /// </summary>
        private void ShowCoverActionMenu()
        {
            var covers = GetAvailableCoverNames();
            if (covers.Count == 0)
            {
                EditorUtility.DisplayDialog(SceneFlowLocale.CcTitle, SceneFlowLocale.CcNoCover, SceneFlowLocale.DlgOk);
                return;
            }

            var menu = new GenericMenu();
            foreach (var coverName in covers)
            {
                string cn = coverName;
                menu.AddItem(new GUIContent($"{cn}/{SceneFlowLocale.CcActionShow}"), false,
                    () => CreateCoverTriggerButton(cn, CoverAction.Show));
                menu.AddItem(new GUIContent($"{cn}/{SceneFlowLocale.CcActionHide}"), false,
                    () => CreateCoverTriggerButton(cn, CoverAction.Hide));
                menu.AddItem(new GUIContent($"{cn}/{SceneFlowLocale.CcActionToggle}"), false,
                    () => CreateCoverTriggerButton(cn, CoverAction.Toggle));
            }
            menu.ShowAsContext();
        }

        /// <summary>
        /// 取得可選 Cover 名稱（排除自身）
        /// </summary>
        private List<string> GetAvailableCoverNames()
        {
            if (BlueprintData == null) return new List<string>();
            string selfName = _target.gameObject.name.Replace("(Clone)", "").Trim();
            return BlueprintData.nodes
                .Where(n => n.nodeType == SceneNodeType.PopCover && n.sceneName != selfName)
                .Select(n => n.sceneName)
                .ToList();
        }

        /// <summary>
        /// 建立帶 ButtonCoverTrigger 的按鈕（自動填入 coverName + action）
        /// </summary>
        private void CreateCoverTriggerButton(string coverName, CoverAction action)
        {
            var content = FindOrCreateContent();

            string actionLabel = action switch
            {
                CoverAction.Show => SceneFlowLocale.CcActionShow,
                CoverAction.Hide => SceneFlowLocale.CcActionHide,
                CoverAction.Toggle => SceneFlowLocale.CcActionToggle,
                _ => action.ToString()
            };

            string label = $"[{actionLabel}] {coverName}";
            var btnObj = new GameObject($"[BTN] {label}");
            btnObj.transform.SetParent(content, false);
            Undo.RegisterCreatedObjectUndo(btnObj, $"建立 Cover 按鈕: {label}");

            var rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.05f);
            rt.anchorMax = new Vector2(0.8f, 0.12f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.25f, 0.35f, 0.5f, 0.9f);

            btnObj.AddComponent<Button>();

            // 掛 ButtonCoverTrigger 並設定欄位
            var trigger = btnObj.AddComponent<ButtonCoverTrigger>();
            var so = new SerializedObject(trigger);
            so.FindProperty("_coverName").stringValue = coverName;
            so.FindProperty("_action").enumValueIndex = (int)action;
            so.ApplyModifiedProperties();

            Selection.activeGameObject = btnObj;
            CatzLogger.Log("FlowManager", $"[CoverController] 已建立 Cover 按鈕: {label}");
        }

        /// <summary>
        /// 建立關閉按鈕（右上角 ✕，自動綁定 RequestClose）
        /// </summary>
        private void CreateCloseButton()
        {
            var content = FindOrCreateContent();

            var btnObj = new GameObject("[BTN] 關閉");
            btnObj.transform.SetParent(content, false);
            Undo.RegisterCreatedObjectUndo(btnObj, "建立關閉按鈕");

            // RectTransform — 右上角 50x50
            var rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(50, 50);
            rt.anchoredPosition = new Vector2(-10, -10);

            // 背景
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

            // Button + 綁定 RequestClose（純按鈕，不含文字元件）
            var btn = btnObj.AddComponent<Button>();
            var clickEvent = new Button.ButtonClickedEvent();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                clickEvent, _target.RequestClose);
            btn.onClick = clickEvent;

            Selection.activeGameObject = btnObj;
            CatzLogger.Log("FlowManager", "[CoverController] 已建立關閉按鈕（已綁定 RequestClose）");
        }

        /// <summary>
        /// 建立通用按鈕（底部居中）
        /// </summary>
        private void CreateActionButton(string label, Color bgColor)
        {
            var content = FindOrCreateContent();

            var btnObj = new GameObject($"[BTN] {label}");
            btnObj.transform.SetParent(content, false);
            Undo.RegisterCreatedObjectUndo(btnObj, $"建立{label}按鈕");

            var rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.3f, 0.05f);
            rt.anchorMax = new Vector2(0.7f, 0.12f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            // 純按鈕，不含文字元件
            btnObj.AddComponent<Button>();

            Selection.activeGameObject = btnObj;
            CatzLogger.Log("FlowManager", $"[CoverController] 已建立{label}按鈕");
        }

        /// <summary>
        /// 顯示 TMP / Text 選擇選單
        /// </summary>
        private void ShowTextMenu(string label, int fontSize,
            TMPro.TextAlignmentOptions tmpAlign, Vector2 anchorMin, Vector2 anchorMax)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("TextMeshPro"), false, () =>
                CreateTMPText(label, fontSize, tmpAlign, anchorMin, anchorMax));
            menu.AddItem(new GUIContent("Legacy Text"), false, () =>
                CreateLegacyText(label, fontSize, anchorMin, anchorMax));
            menu.ShowAsContext();
        }

        /// <summary>
        /// 建立 TextMeshPro 文字
        /// </summary>
        private void CreateTMPText(string label, int fontSize,
            TMPro.TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax)
        {
            var content = FindOrCreateContent();

            var textObj = new GameObject($"[TMP] {label}");
            textObj.transform.SetParent(content, false);
            Undo.RegisterCreatedObjectUndo(textObj, $"建立{label}文字 (TMP)");

            var rt = textObj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.overflowMode = TMPro.TextOverflowModes.Overflow;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

            Selection.activeGameObject = textObj;
            CatzLogger.Log("FlowManager", $"[CoverController] 已建立{label}文字 (TMP)");
        }

        /// <summary>
        /// 建立 Legacy Text 文字
        /// </summary>
        private void CreateLegacyText(string label, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var content = FindOrCreateContent();

            var textObj = new GameObject($"[TEXT] {label}");
            textObj.transform.SetParent(content, false);
            Undo.RegisterCreatedObjectUndo(textObj, $"建立{label}文字 (Text)");

            var rt = textObj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<Text>();
            text.text = label;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            Selection.activeGameObject = textObj;
            CatzLogger.Log("FlowManager", $"[CoverController] 已建立{label}文字 (Text)");
        }

        /// <summary>
        /// 找到 Content 子物件，沒有就建立
        /// </summary>
        private Transform FindOrCreateContent()
        {
            var content = _target.transform.Find("Content");
            if (content != null) return content;

            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(_target.transform, false);
            Undo.RegisterCreatedObjectUndo(contentObj, "建立 Content");

            var rt = contentObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return contentObj.transform;
        }
        #endregion 快速建立方法
    }
    #endregion CoverController 自訂 Inspector
}
#endif
