#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatzTools.Editor
{
    #region CatzEditorStyles 共用 Editor 視窗樣式

    /// <summary>
    /// CatzTools Editor 視窗共用樣式 — 確保所有工具的資訊列、工具列、狀態列外觀完全一致。
    /// 所有 CatzTools EditorWindow 必須使用此類別建立骨架元素，禁止自行設定 padding/margin。
    /// </summary>
    public static class CatzEditorStyles
    {
        #region 三欄佈局寬度常數（唯一真實來源，禁止在各 Window 重新定義）

        // 設計原則：以「有效內容寬」對齊，不以 panel 外框寬對齊。
        // 有捲軸欄位外框 = 內容寬 + 13（捲軸從 panel 內部切走 13px，預補償）
        // 無捲軸欄位外框 = 內容寬
        // 兩者內容控件的 x 座標、右邊界完全一致；僅 panel 外框右邊界差 SCROLLBAR_W
        // 詳見 .claude/rules/editor-layout.md 與 ToolsReadMe「三欄佈局」章節

        /// <summary>IMGUI 垂直捲軸寬度（捲軸從 panel 內部切走此寬度）</summary>
        public const float SCROLLBAR_W = 13f;

        /// <summary>左欄 panel 外框寬（有捲軸）= 193f</summary>
        public const float LEFT_WIDTH = 180f + SCROLLBAR_W;

        /// <summary>右欄 panel 外框寬（有捲軸）= 233f</summary>
        public const float RIGHT_WIDTH = 220f + SCROLLBAR_W;

        /// <summary>左欄內控件寬（按鈕/Popup/LabelField 硬約束用）</summary>
        public const float LEFT_CONTENT_W = 176f;

        /// <summary>右欄內控件寬（按鈕/Popup/LabelField 硬約束用）</summary>
        public const float RIGHT_CONTENT_W = 216f;

        /// <summary>分類側欄 panel 外框寬（無捲軸，直接 180）</summary>
        public const float CAT_PANEL_W = 180f;

        /// <summary>分類側欄按鈕寬（與 LEFT_CONTENT_W 一致，確保跨 tab 按鈕視覺對齊）</summary>
        public const float CAT_CONTENT_W = 176f;

        #endregion 三欄佈局寬度常數

        #region 色彩常數

        public static readonly Color InfoBarBg = new(0.18f, 0.18f, 0.22f);
        public static readonly Color InfoBarBorder = new(0.3f, 0.3f, 0.4f);
        public static readonly Color ToolbarBg = new(0.1f, 0.1f, 0.1f);
        public static readonly Color StatusBarBg = new(0.18f, 0.18f, 0.18f);
        public static readonly Color TitleColor = new(0.8f, 0.8f, 0.9f);
        public static readonly Color StatusReady = new(0.4f, 0.8f, 0.4f);
        public static readonly Color StatusWarning = new(0.9f, 0.7f, 0.2f);
        public static readonly Color StatusText = new(0.7f, 0.7f, 0.7f);
        public static readonly Color VersionText = new(0.5f, 0.5f, 0.5f);
        public static readonly Color PanelBorder = new(0.1f, 0.1f, 0.1f);

        #endregion 色彩常數

        #region 資訊列（32px）

        /// <summary>
        /// 建立標準資訊列容器（32px，垂直置中，底線）
        /// </summary>
        public static VisualElement CreateInfoBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.height = 32;
            bar.style.minHeight = 32;
            bar.style.flexShrink = 0;
            bar.style.backgroundColor = InfoBarBg;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 12;
            bar.style.paddingRight = 12;
            bar.style.paddingTop = 0;
            bar.style.paddingBottom = 0;
            bar.style.borderBottomWidth = 1;
            bar.style.borderBottomColor = InfoBarBorder;
            return bar;
        }

        /// <summary>資訊列標題 Label（鎖死行高，防止 emoji 撐高）</summary>
        public static Label CreateInfoBarTitle(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 13;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.color = TitleColor;
            label.style.marginRight = 12;
            label.style.height = 22;
            label.style.overflow = Overflow.Hidden;
            ResetSpacing(label);
            return label;
        }

        /// <summary>資訊列狀態 Label（彈性填滿，鎖死高度）</summary>
        public static Label CreateInfoBarStatus()
        {
            var label = new Label();
            label.style.fontSize = 11;
            label.style.flexGrow = 1;
            label.style.marginLeft = 8;
            label.style.height = 22;
            label.style.overflow = Overflow.Hidden;
            ResetSpacing(label);
            return label;
        }

        /// <summary>資訊列標準按鈕</summary>
        public static Button CreateInfoBarButton(string text, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 22;
            btn.style.paddingTop = 2;
            btn.style.paddingBottom = 2;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.marginTop = 0;
            btn.style.marginBottom = 0;
            btn.style.marginLeft = 2;
            btn.style.marginRight = 2;
            return btn;
        }

        /// <summary>
        /// <summary>
        /// Inspector 用 — 繪製 CatzLogger 頻道狀態文字（灰色提示，非互動）。
        /// ON = 綠字，OFF = 灰字 + 提示從工具列控制。
        /// </summary>
        public static void DrawLogChannelStatus(string channel)
        {
            bool isOn = CatzLogger.IsChannelEnabled(channel);
            var rect = EditorGUILayout.GetControlRect(false, 16);
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = isOn ? StatusReady : new Color(0.5f, 0.5f, 0.5f) },
                fontStyle = isOn ? FontStyle.Bold : FontStyle.Normal
            };
            var text = isOn
                ? $"LOG ON \u2014 [{channel}]"
                : $"LOG OFF \u2014 (\u5f9e\u7de8\u8f2f\u5668\u5de5\u5177\u5217\u958b\u555f)";
            GUI.Label(rect, text, style);
        }

        /// <summary>
        /// 資訊列 Debug Log 開關按鈕 — ON/OFF 文字 + 背景色切換。
        /// 點擊時切換狀態並呼叫 onToggle(bool)。
        /// </summary>
        public static Button CreateInfoBarDebugToggle(bool initialState, System.Action<bool> onToggle)
        {
            bool state = initialState;
            var btn = new Button();
            btn.style.height = 20;
            btn.style.width = 42;
            btn.style.fontSize = 9;
            btn.style.paddingTop = 1;
            btn.style.paddingBottom = 1;
            btn.style.paddingLeft = 4;
            btn.style.paddingRight = 4;
            btn.style.marginTop = 0;
            btn.style.marginBottom = 0;
            btn.style.marginLeft = 4;
            btn.style.marginRight = 0;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;

            void UpdateVisual()
            {
                btn.text = state ? "LOG ON" : "LOG";
                btn.style.backgroundColor = state
                    ? new Color(0.2f, 0.5f, 0.2f)
                    : new Color(0.25f, 0.25f, 0.25f);
                btn.style.color = state
                    ? new Color(0.8f, 1f, 0.8f)
                    : new Color(0.5f, 0.5f, 0.5f);
            }

            btn.clicked += () =>
            {
                state = !state;
                UpdateVisual();
                onToggle?.Invoke(state);
            };

            UpdateVisual();
            return btn;
        }

        /// <summary>資訊列語系切換按鈕（最右側，小按鈕）</summary>
        public static Button CreateInfoBarLangButton(string text, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 20;
            btn.style.width = 28;
            btn.style.fontSize = 10;
            btn.style.paddingTop = 1;
            btn.style.paddingBottom = 1;
            btn.style.paddingLeft = 2;
            btn.style.paddingRight = 2;
            btn.style.marginTop = 0;
            btn.style.marginBottom = 0;
            btn.style.marginLeft = 4;
            btn.style.marginRight = 0;
            return btn;
        }

        /// <summary>設定狀態 Label 為就緒（✓ + 綠色）</summary>
        public static void SetStatusReady(Label label, string text)
        {
            label.text = "\u2713 " + text;
            label.style.color = StatusReady;
        }

        /// <summary>設定狀態 Label 為警告（黃色）</summary>
        public static void SetStatusWarning(Label label, string text)
        {
            label.text = text;
            label.style.color = StatusWarning;
        }

        #endregion 資訊列

        #region 工具列（30px）

        public static readonly Color ToolbarBorder = new(0.12f, 0.12f, 0.15f);

        /// <summary>
        /// 建立標準工具列容器（30px，垂直置中，底線）
        /// </summary>
        public static VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 30;
            toolbar.style.minHeight = 30;
            toolbar.style.flexShrink = 0;
            toolbar.style.backgroundColor = ToolbarBg;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.paddingTop = 0;
            toolbar.style.paddingBottom = 0;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = ToolbarBorder;
            return toolbar;
        }

        /// <summary>工具列標準按鈕</summary>
        public static Button CreateToolbarButton(string text, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 22;
            btn.style.fontSize = 11;
            btn.style.paddingTop = 2;
            btn.style.paddingBottom = 2;
            btn.style.paddingLeft = 6;
            btn.style.paddingRight = 6;
            btn.style.marginTop = 0;
            btn.style.marginBottom = 0;
            btn.style.marginLeft = 2;
            btn.style.marginRight = 2;
            return btn;
        }

        /// <summary>工具列下拉選單</summary>
        public static PopupField<string> CreateToolbarPopup(List<string> choices, int defaultIndex = 0, int width = 140)
        {
            var popup = new PopupField<string>(choices, defaultIndex);
            popup.style.width = width;
            popup.style.height = 20;
            popup.style.fontSize = 11;
            popup.style.marginTop = 0;
            popup.style.marginBottom = 0;
            popup.style.marginLeft = 4;
            popup.style.marginRight = 4;
            // 隱藏 label 部分（只顯示值）
            popup.labelElement.style.display = DisplayStyle.None;
            return popup;
        }

        /// <summary>工具列搜尋框（內建放大鏡 + 清除按鈕）</summary>
        public static ToolbarSearchField CreateToolbarSearchField(int width = 150)
        {
            var field = new ToolbarSearchField();
            field.style.width = width;
            field.style.height = 20;
            field.style.fontSize = 11;
            field.style.marginTop = 0;
            field.style.marginBottom = 0;
            field.style.marginLeft = 4;
            field.style.marginRight = 4;
            field.style.flexShrink = 0;
            return field;
        }

        /// <summary>工具列文字標籤</summary>
        public static Label CreateToolbarLabel(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = StatusText;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            ResetSpacing(label);
            label.style.marginLeft = 6;
            label.style.marginRight = 2;
            return label;
        }

        /// <summary>工具列分隔（固定 8px 寬）</summary>
        public static VisualElement CreateToolbarSpacer()
        {
            var spacer = new VisualElement();
            spacer.style.width = 8;
            return spacer;
        }

        /// <summary>工具列彈性空間（推後面的按鈕靠右）</summary>
        public static VisualElement CreateToolbarFlexSpacer()
        {
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            return spacer;
        }

        #endregion 工具列

        #region 狀態列（24px）

        /// <summary>
        /// 建立標準狀態列容器（24px，垂直置中）
        /// </summary>
        public static VisualElement CreateStatusBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.height = 24;
            bar.style.minHeight = 24;
            bar.style.flexShrink = 0;
            bar.style.backgroundColor = StatusBarBg;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 8;
            bar.style.paddingRight = 8;
            bar.style.paddingTop = 0;
            bar.style.paddingBottom = 0;
            return bar;
        }

        /// <summary>狀態列文字 Label（左側，彈性填滿）</summary>
        public static Label CreateStatusBarText()
        {
            var label = new Label();
            label.style.fontSize = 11;
            label.style.color = StatusText;
            label.style.flexGrow = 1;
            ResetSpacing(label);
            return label;
        }

        /// <summary>狀態列版本號 Label（右側）</summary>
        public static Label CreateStatusBarVersion(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = VersionText;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            ResetSpacing(label);
            return label;
        }

        #endregion 狀態列

        #region 三欄佈局

        /// <summary>建立三欄主容器（水平排列，flexGrow）</summary>
        public static VisualElement CreateMainContainer()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexGrow = 1;
            return container;
        }

        /// <summary>建立固定寬度的側欄（左欄或右欄）</summary>
        public static VisualElement CreateSidePanel(float width, bool borderRight = true)
        {
            var panel = new VisualElement();
            panel.style.width = width;
            if (borderRight)
            {
                panel.style.borderRightWidth = 1;
                panel.style.borderRightColor = PanelBorder;
            }
            panel.style.overflow = Overflow.Hidden;
            return panel;
        }

        /// <summary>建立彈性寬度的中欄</summary>
        public static VisualElement CreateMiddlePanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderRightColor = PanelBorder;
            panel.style.overflow = Overflow.Hidden;
            return panel;
        }

        #endregion 三欄佈局

        #region 色塊標籤（IMGUI）

        private const float TAG_HEIGHT = 16f;

        /// <summary>
        /// 繪製色塊標籤 — 底色背景 + 左側色條 + 居中文字。
        /// 自動垂直居中於給定 Rect 內（固定高度 16px）。
        /// 用於列表項目前的分類標籤（如 Action Type、Sound Type、Combo Type）。
        /// </summary>
        public static void DrawTag(Rect rect, string label, Color color)
        {
            // 垂直居中
            var tagRect = new Rect(rect.x, rect.y + (rect.height - TAG_HEIGHT) * 0.5f, rect.width, TAG_HEIGHT);

            // 底色（25% 透明度）
            EditorGUI.DrawRect(tagRect, new Color(color.r, color.g, color.b, 0.25f));

            // 左側色條
            EditorGUI.DrawRect(new Rect(tagRect.x, tagRect.y, 3f, tagRect.height), color);

            // 文字
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = color }
            };
            GUI.Label(tagRect, label, style);
        }

        #endregion 色塊標籤

        #region Inspector 資訊面板（IMGUI）

        /// <summary>
        /// 繪製 CatzTools 系統 Inspector 資訊面板 — 背景色與 InfoBar 一致。
        /// 顯示工具名稱 + 版本 + 狀態文字。
        /// </summary>
        public static void DrawInspectorInfoPanel(string toolName, string version, string statusText, bool isReady)
        {
            var rect = EditorGUILayout.GetControlRect(false, 36);

            // 背景色（跟 InfoBar 一致）
            EditorGUI.DrawRect(rect, InfoBarBg);

            // 底線
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), InfoBarBorder);

            // 工具名稱 + 版本
            var titleRect = new Rect(rect.x + 8, rect.y + 2, rect.width - 16, 16);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = TitleColor }
            };
            GUI.Label(titleRect, $"{toolName}  <size=10>v{version}</size>", new GUIStyle(titleStyle) { richText = true });

            // 狀態文字
            var statusRect = new Rect(rect.x + 8, rect.y + 18, rect.width - 16, 14);
            var statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = isReady ? StatusReady : StatusWarning }
            };
            var prefix = isReady ? "\u2713 " : "";
            GUI.Label(statusRect, $"{prefix}{statusText}", statusStyle);
        }

        #endregion Inspector 資訊面板

        #region EditorWindow 停靠

        /// <summary>所有 CatzTools EditorWindow 的全名（反射查找，缺的模組自動跳過）</summary>
        private static readonly string[] s_CatzWindowTypeNames =
        {
            "CatzTools.AudioManager.Editor.AudioManagerEditorWindow",
            "CatzTools.InputSys.Editor.InputManagerEditorWindow",
            "CatzTools.GameFlow.Editor.SceneFlowEditorWindow",
            "CatzTools.CameraSys.Editor.CameraManagerEditorWindow",
            "CatzTools.DataSys.Editor.DataManagerEditorWindow",
            "CatzTools.SettingSys.Editor.SettingManagerEditorWindow",
        };

        /// <summary>
        /// 取得所有其他 CatzTools EditorWindow 的 Type 陣列（排除自己）。
        /// 用反射查找，缺的模組自動跳過，不產生硬引用。
        /// </summary>
        public static System.Type[] GetDockTargets<TExclude>() where TExclude : EditorWindow
        {
            var excludeName = typeof(TExclude).FullName;
            var result = new System.Collections.Generic.List<System.Type>();

            foreach (var name in s_CatzWindowTypeNames)
            {
                if (name == excludeName) continue;
                var type = System.Type.GetType(name + ", Assembly-CSharp-Editor");
                type ??= System.Type.GetType(name + ", Assembly-CSharp");
                if (type != null) result.Add(type);
            }

            return result.ToArray();
        }

        #endregion EditorWindow 停靠

        #region 內部工具

        /// <summary>清零 VisualElement 的上下 padding 和 margin</summary>
        private static void ResetSpacing(VisualElement element)
        {
            element.style.paddingTop = 0;
            element.style.paddingBottom = 0;
            element.style.marginTop = 0;
            element.style.marginBottom = 0;
        }

        #endregion 內部工具
    }

    #endregion CatzEditorStyles 共用 Editor 視窗樣式
}
#endif
