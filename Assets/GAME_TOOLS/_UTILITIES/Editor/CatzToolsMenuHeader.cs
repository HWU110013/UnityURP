using UnityEditor;

namespace CatzTools.Editor
{
    /// <summary>
    /// CatzTools 選單最上方的套件 header label。
    /// </summary>
    /// <remarks>
    /// 透過 disabled MenuItem（validator 永遠回 false）做成「純文字顯示」的效果，
    /// 用來在選單最上方秀整個 CatzGameTools 套件的總覽版本號。
    ///
    /// Priority 設計：
    /// <list type="bullet">
    /// <item>Header priority = <c>0</c></item>
    /// <item>各工具 priority = <c>20-29</c>（Camera/Audio/Input/SceneFlow/Data）</item>
    /// <item>公用工具 priority = <c>1000+</c></item>
    /// </list>
    /// Header 與第一個工具的 priority 差 = 20，遠大於 10，Unity 會自動插入分隔線。
    /// （實測 gap = 11 不觸發分隔線，拉到 20 確保穩定。）
    /// </remarks>
    internal static class CatzToolsMenuHeader
    {
        private const string MENU_PATH =
            "CatzTools/[v" + CatzToolsVersion.VERSION + "] " + CatzToolsVersion.NAME;

        /// <summary>
        /// 純文字 label，點擊無動作（被 validator 擋下來永遠 disabled）。
        /// </summary>
        [MenuItem(MENU_PATH, false, 0)]
        private static void Header()
        {
            // 空方法，永遠不會被呼叫（validator 回 false）
        }

        /// <summary>
        /// Validator 永遠回 false → MenuItem 呈現為灰色不可點擊。
        /// </summary>
        [MenuItem(MENU_PATH, true, 0)]
        private static bool HeaderValidator() => false;
    }
}
