using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatzTools
{
    #region CatzLogger 日誌系統

    /// <summary>
    /// CatzTools 專用日誌系統 — 分級 + 分頻道過濾。
    /// 全域等級控制所有輸出，頻道開關獨立控制各系統。
    /// </summary>
    public static class CatzLogger
    {
        #region 日誌等級定義

        /// <summary>日誌等級</summary>
        public enum LogLevel
        {
            None = 0,
            Error = 1,
            Warning = 2,
            Info = 3,
            Debug = 4,
            Verbose = 5
        }

        #endregion 日誌等級定義

        #region 私有欄位

        private static LogLevel _currentLogLevel = LogLevel.Info;
        private const string LOG_PREFIX = "[CatzTools]";
        private static bool _enableTimestamp;
        private static bool _enableStackTrace;

        /// <summary>頻道開關（預設全部關閉，需明確開啟）</summary>
        private static readonly Dictionary<string, bool> _channelEnabled = new();

        /// <summary>頻道等級覆蓋（未設定時用全域等級）</summary>
        private static readonly Dictionary<string, LogLevel> _channelLevel = new();

        #endregion 私有欄位

        #region 全域設定

        /// <summary>設定全域日誌等級</summary>
        public static void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = level;
        }

        /// <summary>取得當前全域日誌等級</summary>
        public static LogLevel CurrentLogLevel => _currentLogLevel;

        /// <summary>設定時間戳記</summary>
        public static void SetTimestamp(bool enabled) => _enableTimestamp = enabled;

        /// <summary>設定堆疊追蹤</summary>
        public static void SetStackTrace(bool enabled) => _enableStackTrace = enabled;

        #endregion 全域設定

        #region 頻道管理

        private const string PREFS_PREFIX = "CatzLogger_CH_";

        /// <summary>啟用/停用頻道（持久化到 EditorPrefs / PlayerPrefs）</summary>
        public static void SetChannelEnabled(string channel, bool enabled)
        {
            _channelEnabled[channel] = enabled;
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool(PREFS_PREFIX + channel, enabled);
#endif
        }

        /// <summary>頻道是否啟用（優先讀快取，fallback 到持久化存儲）</summary>
        public static bool IsChannelEnabled(string channel)
        {
            if (_channelEnabled.TryGetValue(channel, out var cached))
                return cached;

            // 從持久化存儲讀取（預設 false）
#if UNITY_EDITOR
            bool stored = UnityEditor.EditorPrefs.GetBool(PREFS_PREFIX + channel, false);
#else
            bool stored = false;
#endif
            _channelEnabled[channel] = stored;
            return stored;
        }

        /// <summary>設定頻道專屬等級（覆蓋全域）</summary>
        public static void SetChannelLevel(string channel, LogLevel level)
        {
            _channelLevel[channel] = level;
        }

        /// <summary>取得頻道的有效等級（有覆蓋用覆蓋，沒有用全域）</summary>
        public static LogLevel GetEffectiveLevel(string channel)
        {
            return _channelLevel.TryGetValue(channel, out var level) ? level : _currentLogLevel;
        }

        /// <summary>取得所有已註冊頻道狀態（Debug 用）</summary>
        public static IReadOnlyDictionary<string, bool> GetAllChannels() => _channelEnabled;

        #endregion 頻道管理

        #region 帶頻道的日誌方法

        /// <summary>記錄頻道資訊日誌</summary>
        public static void Log(string channel, string message, UnityEngine.Object context = null)
        {
            if (!ShouldLog(channel, LogLevel.Info)) return;
            Debug.Log(FormatMessage(channel, message, "INFO"), context);
        }

        /// <summary>記錄頻道調試日誌</summary>
        public static void LogDebug(string channel, string message, UnityEngine.Object context = null)
        {
            if (!ShouldLog(channel, LogLevel.Debug)) return;
            Debug.Log(FormatMessage(channel, message, "DEBUG"), context);
        }

        /// <summary>記錄頻道詳細日誌</summary>
        public static void LogVerbose(string channel, string message, UnityEngine.Object context = null)
        {
            if (!ShouldLog(channel, LogLevel.Verbose)) return;
            Debug.Log(FormatMessage(channel, message, "VERBOSE"), context);
        }

        /// <summary>記錄頻道警告日誌（永遠輸出，不受頻道開關影響）</summary>
        public static void LogWarning(string channel, string message, UnityEngine.Object context = null)
        {
            if (_currentLogLevel < LogLevel.Warning) return;
            Debug.LogWarning(FormatMessage(channel, message, "WARNING"), context);
        }

        /// <summary>記錄頻道錯誤日誌（永遠輸出，不受頻道開關影響）</summary>
        public static void LogError(string channel, string message, UnityEngine.Object context = null)
        {
            if (_currentLogLevel < LogLevel.Error) return;
            Debug.LogError(FormatMessage(channel, message, "ERROR"), context);
            if (_enableStackTrace)
                Debug.LogError("堆疊追蹤:\n" + Environment.StackTrace);
        }

        #endregion 帶頻道的日誌方法

        #region 無頻道的日誌方法（向後相容）

        /// <summary>記錄一般資訊日誌（無頻道過濾，只看全域等級）</summary>
        public static void Log(string message, UnityEngine.Object context = null)
        {
            if (_currentLogLevel < LogLevel.Info) return;
            Debug.Log(FormatMessage(null, message, "INFO"), context);
        }

        /// <summary>記錄調試日誌</summary>
        public static void LogDebug(string message, UnityEngine.Object context = null)
        {
            if (_currentLogLevel < LogLevel.Debug) return;
            Debug.Log(FormatMessage(null, message, "DEBUG"), context);
        }

        /// <summary>記錄詳細日誌</summary>
        public static void LogVerbose(string message, UnityEngine.Object context = null)
        {
            if (_currentLogLevel < LogLevel.Verbose) return;
            Debug.Log(FormatMessage(null, message, "VERBOSE"), context);
        }

        /// <summary>記錄警告日誌</summary>
        public static void LogWarning(string message, UnityEngine.Object context = null)
        {
            if (_currentLogLevel < LogLevel.Warning) return;
            Debug.LogWarning(FormatMessage(null, message, "WARNING"), context);
        }

        /// <summary>記錄錯誤日誌</summary>
        public static void LogError(string message, UnityEngine.Object context = null)
        {
            if (_currentLogLevel < LogLevel.Error) return;
            Debug.LogError(FormatMessage(null, message, "ERROR"), context);
            if (_enableStackTrace)
                Debug.LogError("堆疊追蹤:\n" + Environment.StackTrace);
        }

        /// <summary>記錄例外日誌</summary>
        public static void LogException(Exception exception, UnityEngine.Object context = null)
        {
            if (_currentLogLevel < LogLevel.Error) return;
            Debug.LogError(FormatMessage(null, $"例外發生: {exception.Message}", "EXCEPTION"), context);
            Debug.LogException(exception, context);
        }

        #endregion 無頻道的日誌方法

        #region 條件編譯方法

        /// <summary>僅在除錯模式下記錄日誌</summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogDebugOnly(string message, UnityEngine.Object context = null)
            => LogDebug(message, context);

        /// <summary>僅在編輯器中記錄日誌</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogEditorOnly(string message, UnityEngine.Object context = null)
            => Log(message, context);

        /// <summary>僅在開發版本中記錄日誌</summary>
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogDevelopmentOnly(string message, UnityEngine.Object context = null)
            => Log(message, context);

        #endregion 條件編譯方法

        #region 內部方法

        /// <summary>判斷是否應該輸出（頻道開關 + 等級）</summary>
        private static bool ShouldLog(string channel, LogLevel required)
        {
            // 頻道關閉 → 不輸出
            if (!IsChannelEnabled(channel)) return false;
            // 頻道等級檢查
            return GetEffectiveLevel(channel) >= required;
        }

        /// <summary>格式化訊息</summary>
        private static string FormatMessage(string channel, string message, string level)
        {
            var prefix = channel != null ? $"[{channel}]" : LOG_PREFIX;
            var formatted = $"{prefix} [{level}] {message}";
            if (_enableTimestamp)
                formatted = $"[{DateTime.Now:HH:mm:ss.fff}] {formatted}";
            return formatted;
        }

        #endregion 內部方法
    }

    #endregion CatzLogger 日誌系統
}
