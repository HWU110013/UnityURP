using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace CatzTools
{
    #region 自動註冊標記

    /// <summary>
    /// 標記為自動註冊服務。ServiceLocator 啟動時反射掃描並按 priority 排序初始化。
    /// Priority 數字越小越先初始化。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoRegisterAttribute : Attribute
    {
        /// <summary>初始化優先順序（越小越先）</summary>
        public int Priority { get; }

        /// <summary>簡短說明（顯示在 SceneFlow Start 節點服務清單中）</summary>
        public string Description { get; set; }

        /// <summary>顯示名稱（服務清單 UI 用，不填則 fallback type.Name）</summary>
        public string DisplayName { get; set; }

        public AutoRegisterAttribute(int priority = 100) => Priority = priority;
    }

    #endregion 自動註冊標記

    #region 服務介面

    /// <summary>
    /// CatzTools 服務介面 — 所有受 ServiceLocator 管理的系統必須實作。
    /// </summary>
    public interface ICatzService
    {
        /// <summary>異步初始化（ServiceLocator 按 priority 順序呼叫）</summary>
        Task InitializeAsync();

        /// <summary>異步關閉（應用程式結束時呼叫）</summary>
        Task ShutdownAsync();
    }

    #endregion 服務介面

    #region 服務狀態

    /// <summary>服務狀態（供 Debug 顯示）</summary>
    public enum ServiceState { Pending, Initializing, Ready, Failed }

    /// <summary>服務狀態紀錄</summary>
    public struct ServiceStatus
    {
        public string name;
        public int priority;
        public ServiceState state;
        public Type type;
    }

    #endregion 服務狀態

    #region ServiceLocator 核心

    /// <summary>
    /// 服務定位器 — 統一管理所有 CatzTools 系統的啟動、存取、關閉。
    /// 唯一的 [RuntimeInitializeOnLoadMethod]，取代各系統各自啟動。
    /// </summary>
    public static class ServiceLocator
    {
        #region 私有欄位

        private static readonly Dictionary<Type, ICatzService> _services = new();
        private static readonly List<ServiceStatus> _statusList = new();
        private static bool _isReady;

        #endregion 私有欄位

        #region 事件

        /// <summary>所有服務初始化完成（只觸發一次）</summary>
        public static event Action OnAllServicesReady;

        #endregion 事件

        #region 公開屬性

        /// <summary>是否所有服務都已就緒</summary>
        public static bool IsReady => _isReady;

        #endregion 公開屬性

        #region 啟動

        /// <summary>唯一啟動點 — 反射掃描 [AutoRegister] → 按 priority 排序 → 依序初始化</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static async void Bootstrap()
        {
            _services.Clear();
            _statusList.Clear();
            _isReady = false;

            CatzLogger.Log("[ServiceLocator] Bootstrap 開始");

            // 反射掃描所有標記 [AutoRegister] + ICatzService 的類別
            var entries = EnumerateRegisterableServices();

            // 嘗試讀取 Resources/SceneBlueprintData 的 startServiceTypeNames 作為清單覆寫
            var manifest = LoadServiceManifest();
            if (manifest != null && manifest.Count > 0)
            {
                CatzLogger.Log($"[ServiceLocator] 套用 manifest（{manifest.Count} 個服務）");
                entries = ApplyManifest(entries, manifest);
            }
            else
            {
                // 沒有 manifest → 按 priority 排序（原行為）
                entries.Sort((a, b) => a.attr.Priority.CompareTo(b.attr.Priority));
            }

            // 建立狀態列表
            foreach (var (type, attr) in entries)
            {
                _statusList.Add(new ServiceStatus
                {
                    name = type.Name,
                    priority = attr.Priority,
                    state = ServiceState.Pending,
                    type = type
                });
            }

            // 依序初始化
            for (int i = 0; i < entries.Count; i++)
            {
                var (type, attr) = entries[i];
                UpdateState(i, ServiceState.Initializing);
                CatzLogger.Log($"[ServiceLocator] [{attr.Priority}] {type.Name} 初始化中...");

                try
                {
                    var service = CreateService(type);
                    await service.InitializeAsync();
                    _services[type] = service;
                    UpdateState(i, ServiceState.Ready);
                    CatzLogger.Log($"[ServiceLocator] [{attr.Priority}] {type.Name} 就緒 ({i + 1}/{entries.Count})");
                }
                catch (Exception ex)
                {
                    UpdateState(i, ServiceState.Failed);
                    CatzLogger.LogError($"[ServiceLocator] {type.Name} 初始化失敗: {ex.Message}");
                }
            }

            _isReady = true;
            CatzLogger.Log($"[ServiceLocator] 全部就緒 ({_services.Count} 服務)");
            OnAllServicesReady?.Invoke();
        }

        #endregion 啟動

        #region 服務發現（Editor + Runtime 共用）

        /// <summary>
        /// 反射掃描所有標記 [AutoRegister] 且實作 ICatzService 的類別。
        /// 不建立實例，純查詢；Editor 端可用來列出可選服務。
        /// </summary>
        public static List<(Type type, AutoRegisterAttribute attr)> EnumerateRegisterableServices()
        {
            var result = new List<(Type type, AutoRegisterAttribute attr)>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!typeof(ICatzService).IsAssignableFrom(type)) continue;
                        if (type.IsInterface || type.IsAbstract) continue;

                        var attr = type.GetCustomAttribute<AutoRegisterAttribute>();
                        if (attr == null) continue;

                        result.Add((type, attr));
                    }
                }
                catch { /* 忽略無法載入的程式集 */ }
            }
            return result;
        }

        /// <summary>單筆 manifest 條目（type 名稱 + 可選的 priority 覆寫）</summary>
        private struct ManifestEntry
        {
            public string typeName;
            public int? priorityOverride;
        }

        /// <summary>
        /// 從 Resources/SceneBlueprintData 載入服務啟動清單。
        /// 優先讀新版 startServices（含 priority 覆寫），fallback 到 legacy startServiceTypeNames。
        /// 找不到資源或清單為空時回傳 null。
        /// </summary>
        private static List<ManifestEntry> LoadServiceManifest()
        {
            // 用 reflection 取得 SceneBlueprintData 型別與欄位，避免 _UTILITIES 硬引用 GAME_FLOW_SYS
            var bpType = Type.GetType("CatzTools.GameFlow.SceneBlueprintData, Assembly-CSharp")
                      ?? Type.GetType("CatzTools.GameFlow.SceneBlueprintData");
            if (bpType == null) return null;

            var asset = Resources.Load("SceneBlueprintData", bpType);
            if (asset == null) return null;

            // 1) 優先讀新版 startServices（List<ServiceManifestEntry>）
            var newField = bpType.GetField("startServices", BindingFlags.Public | BindingFlags.Instance);
            if (newField != null && newField.GetValue(asset) is System.Collections.IList newList && newList.Count > 0)
            {
                var entries = new List<ManifestEntry>(newList.Count);
                foreach (var item in newList)
                {
                    if (item == null) continue;
                    var itemType = item.GetType();
                    var nameField = itemType.GetField("typeName", BindingFlags.Public | BindingFlags.Instance);
                    var prioField = itemType.GetField("priorityOverride", BindingFlags.Public | BindingFlags.Instance);
                    if (nameField == null) continue;

                    var name = nameField.GetValue(item) as string;
                    if (string.IsNullOrEmpty(name)) continue;

                    int? prio = null;
                    if (prioField != null && prioField.GetValue(item) is string prioStr
                        && !string.IsNullOrEmpty(prioStr) && int.TryParse(prioStr, out var prioVal))
                    {
                        prio = prioVal;
                    }
                    entries.Add(new ManifestEntry { typeName = name, priorityOverride = prio });
                }
                if (entries.Count > 0) return entries;
            }

            // 2) Fallback：讀 legacy startServiceTypeNames（List<string>）
            var legacyField = bpType.GetField("startServiceTypeNames", BindingFlags.Public | BindingFlags.Instance);
            if (legacyField == null) return null;
            if (legacyField.GetValue(asset) is System.Collections.IList legacyList && legacyList.Count > 0)
            {
                var entries = new List<ManifestEntry>(legacyList.Count);
                foreach (var o in legacyList)
                {
                    if (o is string s && !string.IsNullOrEmpty(s))
                        entries.Add(new ManifestEntry { typeName = s, priorityOverride = null });
                }
                return entries.Count > 0 ? entries : null;
            }
            return null;
        }

        /// <summary>
        /// 用 manifest 過濾並重排服務清單。
        /// - 只保留 manifest 內列出的型別（FullName 或 Name 比對皆可）
        /// - 順序依 manifest 為準（手動 ↑↓ 排好的就用那個順序）
        /// - 若 manifest 條目有 priorityOverride，會用它取代 attribute 原值（影響 log + Inspector 顯示，不影響執行順序）
        /// - manifest 列了但反射找不到的型別會被略過並 log warning
        /// </summary>
        private static List<(Type type, AutoRegisterAttribute attr)> ApplyManifest(
            List<(Type type, AutoRegisterAttribute attr)> discovered, List<ManifestEntry> manifest)
        {
            var ordered = new List<(Type type, AutoRegisterAttribute attr)>();
            foreach (var entry in manifest)
            {
                var match = discovered.Find(e =>
                    e.type.FullName == entry.typeName || e.type.Name == entry.typeName);
                if (match.type == null)
                {
                    CatzLogger.LogWarning($"[ServiceLocator] manifest 列了 '{entry.typeName}' 但找不到對應的 ICatzService");
                    continue;
                }

                // 若有 priority override，建立新的 attribute 副本帶上 override 值（log / inspector 一致用此值）
                if (entry.priorityOverride.HasValue)
                {
                    var overriddenAttr = new AutoRegisterAttribute(entry.priorityOverride.Value)
                    {
                        Description = match.attr.Description,
                    };
                    ordered.Add((match.type, overriddenAttr));
                }
                else
                {
                    ordered.Add(match);
                }
            }
            return ordered;
        }

        #endregion 服務發現（Editor + Runtime 共用）

        #region 服務建立

        /// <summary>建立服務實例（MonoBehaviour 用 MonoSingleton.Instance，一般類別用 Activator）</summary>
        private static ICatzService CreateService(Type type)
        {
            // MonoBehaviour 子類別 → 透過 MonoSingleton 的 Instance 屬性取得
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                var instanceProp = type.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                if (instanceProp != null)
                {
                    var instance = instanceProp.GetValue(null);
                    if (instance is ICatzService service)
                        return service;
                }

                throw new InvalidOperationException(
                    $"{type.Name} 是 MonoBehaviour 但沒有 static Instance 屬性，無法自動建立");
            }

            // 一般類別 → new
            return Activator.CreateInstance(type) as ICatzService
                ?? throw new InvalidOperationException($"無法建立 {type.Name}");
        }

        #endregion 服務建立

        #region 服務存取

        /// <summary>同步取得已初始化的服務</summary>
        public static T Get<T>() where T : class, ICatzService
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return service as T;

            throw new InvalidOperationException($"服務未初始化: {typeof(T).Name}");
        }

        /// <summary>嘗試取得服務（不拋例外）</summary>
        public static bool TryGet<T>(out T service) where T : class, ICatzService
        {
            if (_services.TryGetValue(typeof(T), out var s))
            {
                service = s as T;
                return service != null;
            }
            service = null;
            return false;
        }

        /// <summary>檢查特定服務是否已初始化</summary>
        public static bool IsInitialized<T>() where T : class, ICatzService
            => _services.ContainsKey(typeof(T));

        #endregion 服務存取

        #region 狀態查詢

        /// <summary>取得所有服務狀態（供 Debug 顯示）</summary>
        public static IReadOnlyList<ServiceStatus> GetAllStatus() => _statusList;

        private static void UpdateState(int index, ServiceState state)
        {
            var s = _statusList[index];
            s.state = state;
            _statusList[index] = s;
        }

        #endregion 狀態查詢

        #region 關閉

        /// <summary>關閉所有服務（Application.quitting 時呼叫）</summary>
        public static async Task ShutdownAllAsync()
        {
            // 反序關閉（後啟動的先關）
            var services = _services.Values.Reverse().ToList();
            foreach (var service in services)
            {
                try { await service.ShutdownAsync(); }
                catch (Exception ex) { CatzLogger.LogWarning($"[ServiceLocator] 關閉異常: {ex.Message}"); }
            }
            _services.Clear();
            _statusList.Clear();
            _isReady = false;
        }

        #endregion 關閉
    }

    #endregion ServiceLocator 核心
}
