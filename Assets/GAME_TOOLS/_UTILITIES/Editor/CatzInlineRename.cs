using System;
using UnityEditor;
using UnityEngine;

namespace CatzTools
{
    /// <summary>
    /// CatzTools 共用 Inline 改名機制（所有 EditorWindow 的筆 ✏ 圖示改名都走這個）。
    /// </summary>
    /// <remarks>
    /// 解決 IMGUI 直接改名的常見坑：
    /// <list type="bullet">
    /// <item>FocusTextInControl 首幀失效 → <c>Begin()</c> 設 <c>_frameDelay = 3</c>，但只在**第一幀**呼叫 FocusTextInControl（每幀呼叫會重置 IME composition state，中文輸入法會斷字）</item>
    /// <item>控制項 ID 快取 → 每次 Begin 遞增 <c>_sessionId</c>，控制項名稱 <c>CatzRename_{sessionId}</c></item>
    /// <item>無法判斷改名結束 → 追蹤 <c>EditorGUIUtility.editingTextField</c> 從 true→false 的 transition</item>
    /// <item>點另一筆會帶過文字 → <c>Begin()</c> 內建 <c>CancelAll()</c>，強制互斥</item>
    /// <item>跨清單共用 → 用 object key 識別（int index / string id / 物件參考都行）</item>
    /// <item>Enter 確認 / ESC 取消 → 在 TextField 繪製前攔截 KeyDown 事件（Event.Use 消費掉避免 TextField 看到）</item>
    /// </list>
    /// 使用方式：
    /// <code>
    /// private CatzInlineRename _rename;
    /// private void OnEnable() { _rename = new CatzInlineRename(Repaint); }
    ///
    /// // 繪製清單項目時：
    /// if (_rename.IsActive(i))
    /// {
    ///     _rename.DrawField();
    ///     if (_rename.CheckEnd(out var newName) &amp;&amp; !string.IsNullOrWhiteSpace(newName))
    ///         RenameItem(i, newName);
    /// }
    /// else
    /// {
    ///     // 正常繪製 + 筆按鈕
    ///     if (GUI.Button(editRect, editIcon, GUIStyle.none))
    ///         _rename.Begin(i, item.name);
    /// }
    /// </code>
    /// </remarks>
    public sealed class CatzInlineRename
    {
        private object _activeKey;
        private string _buffer = string.Empty;
        private int _frameDelay;
        private int _sessionId;
        private bool _wasEditing;
        private bool _pendingCommit; // Enter 按下待 commit
        private bool _pendingCancel; // ESC 按下待 cancel
        private readonly Action _repaint;

        /// <summary>建立實例，傳入 EditorWindow 的 Repaint 方法（或任何 GUI 刷新 callback）</summary>
        public CatzInlineRename(Action repaint)
        {
            _repaint = repaint;
        }

        /// <summary>是否有任何改名正在進行</summary>
        public bool IsRenaming => _activeKey != null;

        /// <summary>指定 key 是否正在改名中</summary>
        public bool IsActive(object key)
        {
            if (_activeKey == null || key == null) return false;
            return _activeKey.Equals(key);
        }

        /// <summary>目前編輯中的文字（唯讀，外部不該直接塞值，用 Begin() 開啟會話）</summary>
        public string Buffer => _buffer;

        /// <summary>
        /// 開始改名會話。會自動取消任何現存的改名狀態（全域互斥 → 阻止兩筆同時進行）。
        /// </summary>
        /// <param name="key">識別 key，後續用 <see cref="IsActive(object)"/> 判斷</param>
        /// <param name="initialText">初始文字（通常是物件目前的名稱）</param>
        public void Begin(object key, string initialText)
        {
            CancelAll();
            _activeKey    = key;
            _buffer       = initialText ?? string.Empty;
            _sessionId++;
            _frameDelay   = 3;
            _wasEditing   = false;
            _pendingCommit = false;
            _pendingCancel = false;
        }

        /// <summary>
        /// 取消所有改名狀態（不套用變更）。切換分頁、關閉視窗、ESC 等情境應呼叫此方法。
        /// </summary>
        public void CancelAll()
        {
            _activeKey     = null;
            _buffer        = string.Empty;
            _wasEditing    = false;
            _frameDelay    = 0;
            _pendingCommit = false;
            _pendingCancel = false;
            EditorGUIUtility.editingTextField = false;
            GUIUtility.keyboardControl        = 0;
        }

        /// <summary>
        /// 用 GUILayout 繪製改名 TextField。必須在 <c>IsActive(key) == true</c> 時才呼叫。
        /// </summary>
        public void DrawField(float height = 20f)
        {
            HandleKeyboardShortcuts();

            var ctrlName = $"CatzRename_{_sessionId}";
            GUI.SetNextControlName(ctrlName);
            _buffer = EditorGUILayout.TextField(_buffer, GUILayout.Height(height));

            ApplyInitialFocus(ctrlName);
        }

        /// <summary>
        /// 用絕對 Rect 繪製改名 TextField（給非 GUILayout 情境，例如手動 Rect 佈局）。
        /// 必須在 <c>IsActive(key) == true</c> 時才呼叫。
        /// </summary>
        public void DrawField(Rect rect)
        {
            HandleKeyboardShortcuts();

            var ctrlName = $"CatzRename_{_sessionId}";
            GUI.SetNextControlName(ctrlName);
            _buffer = GUI.TextField(rect, _buffer);

            ApplyInitialFocus(ctrlName);
        }

        /// <summary>
        /// 檢查改名是否結束（Enter 按下 / ESC 取消 / 點擊外部 / 失焦）。
        /// 結束時回傳 true 並透過 out 參數給出 Trim 後的結果，同時清掉會話狀態。
        /// ESC 情境回傳 false（不套用變更），會話仍會被清掉。
        /// </summary>
        /// <param name="finalText">最終文字（已 Trim）</param>
        /// <returns>true = 本幀剛結束且要套用變更；false = 尚未結束、已結束過、或使用者取消</returns>
        public bool CheckEnd(out string finalText)
        {
            finalText = _buffer;

            // Enter = 確認
            if (_pendingCommit)
            {
                finalText   = _buffer.Trim();
                ClearSession();
                return true;
            }

            // ESC = 取消（不套用變更）
            if (_pendingCancel)
            {
                ClearSession();
                return false;
            }

            if (_frameDelay > 0) return false;

            // 外部失焦偵測：editingTextField 從 true → false
            bool editing = EditorGUIUtility.editingTextField;
            if (_wasEditing && !editing)
            {
                finalText   = _buffer.Trim();
                ClearSession();
                return true;
            }
            _wasEditing = editing;
            return false;
        }

        /// <summary>
        /// 在 TextField 繪製前處理 Enter/ESC KeyDown 事件，避免 TextField 自己吃掉。
        /// </summary>
        private void HandleKeyboardShortcuts()
        {
            if (Event.current.type != EventType.KeyDown) return;

            if (Event.current.keyCode == KeyCode.Escape)
            {
                _pendingCancel = true;
                Event.current.Use();
                _repaint?.Invoke();
            }
            else if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            {
                _pendingCommit = true;
                Event.current.Use();
                _repaint?.Invoke();
            }
        }

        /// <summary>
        /// 只在第一幀呼叫 FocusTextInControl；後續幾幀只 Repaint 讓 focus 有時間生效。
        /// 避免每幀呼叫 FocusTextInControl 會重置 IME 輸入法的 composition state，造成中文輸入斷字變英文。
        /// </summary>
        private void ApplyInitialFocus(string ctrlName)
        {
            if (_frameDelay <= 0) return;

            // 關鍵：只在起始幀搶 focus 一次，不要每幀都搶
            if (_frameDelay == 3)
            {
                EditorGUI.FocusTextInControl(ctrlName);
            }
            _frameDelay--;
            _repaint?.Invoke();
        }

        /// <summary>清掉會話狀態（commit / cancel / 失焦結束時共用）</summary>
        private void ClearSession()
        {
            _activeKey     = null;
            _buffer        = string.Empty;
            _wasEditing    = false;
            _pendingCommit = false;
            _pendingCancel = false;
            _frameDelay    = 0;
        }
    }
}
