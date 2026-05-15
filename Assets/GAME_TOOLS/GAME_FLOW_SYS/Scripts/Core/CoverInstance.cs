using UnityEngine;

namespace CatzTools.GameFlow
{
    #region Cover 實例
    /// <summary>
    /// 執行中的 Cover 實例 — 記錄 Cover 的 runtime 狀態
    /// </summary>
    public class CoverInstance
    {
        /// <summary>Cover 節點資料</summary>
        public SceneNode CoverData;

        /// <summary>實例化的 GameObject（Prefab 用）</summary>
        public GameObject Instance;

        /// <summary>載入的場景名稱（Scene 用）</summary>
        public string LoadedSceneName;

        /// <summary>Cover 來源類型</summary>
        public CoverSourceType SourceType;
    }
    #endregion Cover 實例
}
