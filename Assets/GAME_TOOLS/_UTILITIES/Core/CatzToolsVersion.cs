namespace CatzTools
{
    /// <summary>
    /// CatzGameTools 套件總覽版本（不是個別工具版本）。
    /// </summary>
    /// <remarks>
    /// 這個版本號代表整個 <c>Assets/GAME_TOOLS/</c> 套件的集合版本，
    /// 用於 CatzTools 選單最上方的 header label 顯示。
    /// 個別工具各有自己的 <c>*Version.cs</c>（Camera/Audio/Input/SceneFlow/Data）。
    ///
    /// 版本遞增時機：
    /// <list type="bullet">
    /// <item>有新工具加入（例：INVENTORY_SYS / SETTING_SYS 實作完成時）</item>
    /// <item>共用機制大改版（例：<c>CatzInlineRename</c>、<c>CatzEditorStyles</c>、<c>CatzLogger</c> 等）</item>
    /// <item>Asset Store 發布新版</item>
    /// </list>
    /// 個別工具的 bug fix / 小修正**不影響**此版本號。
    /// </remarks>
    public static class CatzToolsVersion
    {
        /// <summary>完整版本字串</summary>
        public const string VERSION = "0.1.3";

        /// <summary>套件顯示名稱</summary>
        public const string NAME = "CatzToolsUtilities";
    }
}
