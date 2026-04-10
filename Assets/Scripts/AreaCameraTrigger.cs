using UnityEngine;
// 注意：如果是舊版 Cinemachine (2.x)，請改為 using Cinemachine; 並且將 CinemachineCamera 改為 CinemachineVirtualCamera
using Unity.Cinemachine;

/// <summary>
/// 區域攝影機觸發器，負責處理玩家進出區域時的攝影機優先級 (Priority) 切換。
/// </summary>
[RequireComponent(typeof(Collider))]
public class AreaCameraTrigger : MonoBehaviour
{
    #region 靜態常量
    // 依據你的需求建立常數，避免 Magic String 和 Magic Number
    private const string PLAYER_TAG = "Player";
    private const int ACTIVE_PRIORITY = 100;
    private const int INACTIVE_PRIORITY = 10;
    #endregion 靜態常量

    #region 基本參數
    [Tooltip("目標攝影機，若不填寫將嘗試在同一個 GameObject 上尋找")]
    [SerializeField] private CinemachineCamera targetCamera;
    #endregion 基本參數

    #region 屬性 (Lazy Loading)
    /// <summary>
    /// 使用 Lazy Loading 取得目標攝影機，避免在 Awake/Start 浪費效能或找不到參考
    /// </summary>
    private CinemachineCamera TargetCamera
    {
        get
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<CinemachineCamera>();
            }
            return targetCamera;
        }
    }
    #endregion 屬性 (Lazy Loading)

    #region Unity 事件
    private void Awake()
    {
        // 防呆：確保掛載此腳本的 Collider 已經勾選 isTrigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[{gameObject.name}] 的 Collider 沒有勾選 isTrigger，系統已自動修正。");
            col.isTrigger = true;
        }

        // 確保初始狀態優先級在低檔
        if (TargetCamera != null)
        {
            TargetCamera.Priority = INACTIVE_PRIORITY;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(PLAYER_TAG))
        {
            SetCameraActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(PLAYER_TAG))
        {
            SetCameraActive(false);
        }
    }
    #endregion Unity 事件

    #region 核心邏輯
    /// <summary>
    /// 設定攝影機狀態 (透過變更 Priority，由 CinemachineBrain 自動處理混和運鏡)
    /// </summary>
    private void SetCameraActive(bool isActive)
    {
        if (TargetCamera == null)
        {
            Debug.LogError($"[{gameObject.name}] 找不到對應的 CinemachineCamera，無法切換運鏡。");
            return;
        }

        TargetCamera.Priority = isActive ? ACTIVE_PRIORITY : INACTIVE_PRIORITY;
    }
    #endregion 核心邏輯
}