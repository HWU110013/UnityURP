using UnityEngine;

namespace CatzTools
{
    /// <summary>
    /// 設定 Binder 通用抽象基底。
    /// 放在 _UTILITIES，讓任何子系統（SETTING_SYS、AUDIO_SYS 等）
    /// 都能繼承並實作 <see cref="ISettingBinder"/>。
    /// 不含任何特定系統依賴（如 GameSetting、AudioSetting）。
    /// </summary>
    public abstract class SettingBinderBase : MonoBehaviour, ISettingBinder
    {
        #region 序列化欄位

        [Header("Binder 設定")]
        [Tooltip("是否在 OnEnable 時自動 Refresh UI")]
        [SerializeField] private bool _refreshOnEnable = true;

        #endregion 序列化欄位

        #region 抽象成員

        /// <inheritdoc/>
        public abstract void Refresh();

        /// <inheritdoc/>
        public abstract void Commit();

        /// <inheritdoc/>
        public abstract void ResetToDefault();

        #endregion 抽象成員

        #region Unity 生命週期

        protected virtual void OnEnable()
        {
            if (_refreshOnEnable) Refresh();
        }

        protected virtual void OnDisable() { }

        #endregion Unity 生命週期
    }
}
