using System.Threading;
using UnityEngine;

namespace CatzTools
{
    /// <summary>
    /// Unity MonoBehaviour 單例基類
    /// 確保只有一個實例存在，重複實例會自動銷毀
    /// </summary>
    /// <typeparam name="T">繼承的子類型</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : Component
    {
        #region 基本參數

        public new static string name => $"[{typeof(T).Name}] [Singleton]";

        /// <summary>
        /// 執行緒鎖
        /// </summary>
        private static Mutex mutex;

        /// <summary>
        /// 是否跨場景保留
        /// </summary>
        public bool dontDestroy;

        #endregion 基本參數

        #region 單例實例

        private static T _instance;

        /// <summary>
        /// 單例實例
        /// </summary>
        public static T Instance
        {
            get
            {
                // 檢查現有實例是否有效（Unity 物件可能被銷毀但不是 C# null）
                if (_instance == null)
                {
                    // 搜尋場景中現有的實例
                    _instance = FindAnyObjectByType<T>();

                    // 沒有找到，建立新的
                    if (_instance == null)
                    {
                        var go = new GameObject(name);
                        _instance = go.AddComponent<T>();
                    }
                }
                return _instance;
            }
        }

        #endregion 單例實例

        #region 生命週期

        protected virtual void Awake()
        {
            // 如果已有實例且不是自己，銷毀自己
            if (_instance != null && _instance != this)
            {
                // 靜默銷毀，不輸出警告（避免噪音）
                DestroyImmediate(gameObject);
                return;
            }

            // 設定為主實例
            _instance = this as T;
        }

        private void OnEnable()
        {
            // 確認是主實例才初始化
            if (_instance == this)
            {
                Initial();
            }
        }

        private void OnDestroy()
        {
            // 只有主實例被銷毀時才清理
            if (_instance == this)
            {
                DisInitial();
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            if (mutex != null)
            {
                mutex.ReleaseMutex();
                mutex.Close();
                mutex = null;
            }
        }

        #endregion 生命週期

        #region 虛擬方法

        /// <summary>
        /// 初始化（子類覆寫）
        /// </summary>
        protected virtual void Initial()
        {
            CatzLogger.Log($"{name} online.");
            if (dontDestroy) DontDestroyOnLoad(gameObject);
            gameObject.hideFlags = HideFlags.NotEditable;
        }

        /// <summary>
        /// 反初始化（子類覆寫）
        /// </summary>
        protected virtual void DisInitial()
        {
            CatzLogger.Log($"{name} offline.");
        }

        #endregion 虛擬方法
    }
}