using UnityEngine;

/// <summary>
/// Billboard：讓掛載此元件的物件永遠面向指定相機。
/// </summary>
/// <remarks>
/// 常用於 World Space 的 UI（互動提示、血條、名牌）、3D 場景中的粒子貼片等。
/// 採 LateUpdate 確保所有相機移動邏輯已完成後再更新朝向。
/// </remarks>
public class Billboard : MonoBehaviour
{
    #region 基本設定
    [Header("基本設定")]
    [SerializeField, Tooltip("面向的相機，未指定時自動抓 Camera.main")]
    private Camera targetCamera;

    [SerializeField, Tooltip("鎖定 Y 軸：只繞 Y 旋轉。適合角色頭頂 UI 不隨俯視角翻轉")]
    private bool lockYAxis = false;

    [SerializeField, Tooltip("反轉朝向：有些 UI Canvas 需要反向才會正面朝向相機")]
    private bool flip = true;
    #endregion 基本設定

    #region 內部快取
    private Camera cachedCamera;
    #endregion 內部快取

    #region 快取存取（Lazy Loading）
    /// <summary>
    /// 取得當前使用的相機。優先用 Inspector 指定，否則自動抓 Camera.main。
    /// 使用 UnityEngine.Object 的 == null 判斷，避免 destroy 後偽 null 問題。
    /// </summary>
    private Camera ActiveCamera
    {
        get
        {
            if (cachedCamera == null)
                cachedCamera = (targetCamera != null) ? targetCamera : Camera.main;
            return cachedCamera;
        }
    }
    #endregion 快取存取

    #region Unity 生命週期
    private void LateUpdate()
    {
        Camera cam = ActiveCamera;
        if (cam == null) return;

        Vector3 forward = transform.position - cam.transform.position;
        if (lockYAxis) forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return;

        if (flip) forward = -forward;
        transform.rotation = Quaternion.LookRotation(-forward);
    }
    #endregion Unity 生命週期

    #region 公開 API
    /// <summary>
    /// 強制重設相機快取。切換主相機（例如進入 Boss 戰專用視角）後呼叫。
    /// </summary>
    public void RefreshCamera()
    {
        cachedCamera = null;
    }
    #endregion 公開 API
}
