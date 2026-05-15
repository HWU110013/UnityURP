namespace CatzTools
{
    /// <summary>
    /// 設定 Binder 共用介面。
    /// 所有綁定 PlayerPrefs 設定的 UI 元件（SETTING_SYS、AUDIO_SYS 等）必須實作此介面，
    /// SettingsPanelController 透過此介面統一操作 Refresh / Commit / ResetToDefault。
    /// 放在 _UTILITIES 以避免跨系統的套件相依。
    /// </summary>
    public interface ISettingBinder
    {
        /// <summary>從 PlayerPrefs 讀值 → 更新 UI 與暫存值</summary>
        void Refresh();

        /// <summary>將暫存值寫入 PlayerPrefs</summary>
        void Commit();

        /// <summary>將暫存值還原為預設值並更新 UI（不自動 Commit）</summary>
        void ResetToDefault();
    }
}
