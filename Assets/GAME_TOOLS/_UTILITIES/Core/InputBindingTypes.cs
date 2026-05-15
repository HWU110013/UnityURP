using System;

namespace CatzTools
{
    /// <summary>輸入裝置類型</summary>
    public enum InputDeviceType
    {
        Keyboard,
        Mouse,
        Gamepad,
        Touch,
        Other,
    }

    /// <summary>
    /// 單一按鍵綁定資訊。
    /// 由 <see cref="IInputRebindProvider"/> 提供，供 SETTING_SYS 等消費端顯示。
    /// </summary>
    [Serializable]
    public class InputBindingInfo
    {
        /// <summary>所屬 Action Map 名稱（如 "Gameplay"、"UI"）</summary>
        public string mapName;

        /// <summary>動作名稱（如 "Jump"、"Fire"）</summary>
        public string actionName;

        /// <summary>綁定路徑（如 "&lt;Keyboard&gt;/space"）</summary>
        public string bindingPath;

        /// <summary>人類可讀的顯示名稱（如 "Space"、"Left Click"）</summary>
        public string displayName;

        /// <summary>裝置類型</summary>
        public InputDeviceType device;

        /// <summary>是否已被使用者覆蓋（非原始綁定）</summary>
        public bool isOverridden;
    }

    /// <summary>
    /// 輸入重新綁定提供者介面。
    /// 由 INPUT_SYS 的 InputManager 實作，SETTING_SYS 透過此介面查詢按鍵綁定狀態。
    /// 放在 _UTILITIES 確保兩個系統不互相依賴。
    /// </summary>
    public interface IInputRebindProvider
    {
        /// <summary>取得所有 Action Map 名稱</summary>
        string[] GetMapNames();

        /// <summary>取得指定 Map 下所有動作的綁定資訊</summary>
        InputBindingInfo[] GetBindingsForMap(string mapName);

        /// <summary>取得所有動作的綁定資訊</summary>
        InputBindingInfo[] GetAllBindings();

        /// <summary>取得指定動作的綁定資訊</summary>
        InputBindingInfo[] GetBindingsForAction(string actionName);

        /// <summary>取得所有已註冊的動作名稱</summary>
        string[] GetActionNames();

        /// <summary>匯出目前的覆蓋層資料（JSON）</summary>
        string ExportRebindData();

        /// <summary>匯入覆蓋層資料（JSON）</summary>
        void ImportRebindData(string json);

        /// <summary>重置指定動作的綁定至原始設定</summary>
        void ResetBinding(string actionName);

        /// <summary>重置所有綁定至原始設定</summary>
        void ResetAllBindings();
    }
}
