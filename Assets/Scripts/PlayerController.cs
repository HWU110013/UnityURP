using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DEMO 用簡易玩家控制器：CharacterController 移動、重力、跳躍、可選 Animator 參數更新。
/// </summary>
/// <remarks>
/// 讀取 InputActionAsset 裡的 Action Map（預設名稱 Player，含 Move/Jump）自行 Enable/Disable，
/// 不依賴 PlayerInput 元件或 InputMapManager。Animator 為可選，未指定時所有動畫呼叫都會被短路。
///
/// Animator 參數約定（有 Animator 時請在 Controller 自行建立同名參數）：
///   - float   "Speed"    ：水平移動速度
///   - bool    "Grounded" ：是否著地
///   - trigger "Jump"     ：跳躍起跳瞬間
/// </remarks>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    #region 移動參數
    [Header("移動參數")]
    [SerializeField, Tooltip("水平移動速度（公尺/秒）")]
    private float moveSpeed = 5f;

    [SerializeField, Tooltip("跳躍高度（公尺）")]
    private float jumpHeight = 1.5f;

    [SerializeField, Tooltip("重力加速度（負值代表向下）")]
    private float gravity = -20f;

    [SerializeField, Tooltip("以此相機前方為「前」方向。未指定時使用世界座標 Z 軸")]
    private Transform cameraTransform;
    #endregion 移動參數

    #region 輸入設定
    [Header("輸入設定")]
    [SerializeField, Tooltip("Input Actions Asset，內含玩家 Action Map")]
    private InputActionAsset inputAsset;

    [SerializeField, Tooltip("玩家專用 Action Map 名稱")]
    private string actionMapName = "Player";

    [SerializeField, Tooltip("移動 Action 名稱（Vector2）")]
    private string moveActionName = "Move";

    [SerializeField, Tooltip("跳躍 Action 名稱（Button）")]
    private string jumpActionName = "Jump";
    #endregion 輸入設定

    #region 動畫設定
    [Header("動畫設定（可選）")]
    [SerializeField, Tooltip("有指定時才會自動更新動畫參數；未指定不影響移動邏輯")]
    private Animator animator;
    #endregion 動畫設定

    #region 內部快取
    private CharacterController controller;
    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction jumpAction;

    private Vector2 moveInput;
    private Vector3 velocity;
    private bool jumpQueued;
    #endregion 內部快取

    #region Lazy Loading Component
    /// <summary>
    /// Lazy Loading 取得 CharacterController，避免 Awake/Start 提前呼叫 GetComponent。
    /// </summary>
    private CharacterController Controller
    {
        get
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            return controller;
        }
    }
    #endregion Lazy Loading Component

    #region 動畫參數 Hash
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int JumpTriggerHash = Animator.StringToHash("Jump");
    #endregion 動畫參數 Hash

    #region Unity 生命週期
    private void OnEnable()
    {
        ResolveInputActions();
        EnableInputActions();
    }

    private void OnDisable()
    {
        DisableInputActions();
    }

    private void Update()
    {
        ApplyGravity();
        ApplyMovement();
        UpdateAnimator();
    }
    #endregion Unity 生命週期

    #region 輸入處理
    /// <summary>
    /// 從 Asset 取得 Action Map 與各 Action，並訂閱跳躍 callback。
    /// </summary>
    private void ResolveInputActions()
    {
        if (inputAsset == null) return;

        playerMap = inputAsset.FindActionMap(actionMapName, throwIfNotFound: false);
        if (playerMap == null) return;

        moveAction = playerMap.FindAction(moveActionName);
        jumpAction = playerMap.FindAction(jumpActionName);

        if (jumpAction != null) jumpAction.performed += HandleJumpPerformed;
    }

    private void EnableInputActions()
    {
        if (playerMap != null) playerMap.Enable();
    }

    private void DisableInputActions()
    {
        if (jumpAction != null) jumpAction.performed -= HandleJumpPerformed;
        if (playerMap != null) playerMap.Disable();
    }

    /// <summary>
    /// 跳躍按鍵觸發時只登記意圖，實際起跳速度由 Update 統一處理。
    /// </summary>
    private void HandleJumpPerformed(InputAction.CallbackContext context)
    {
        if (Controller.isGrounded) jumpQueued = true;
    }
    #endregion 輸入處理

    #region 移動邏輯
    private void ApplyGravity()
    {
        // 著地時保留小負值，確保 isGrounded 判定穩定
        if (Controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
    }

    private void ApplyMovement()
    {
        moveInput = (moveAction != null) ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        if (jumpQueued)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            TriggerJumpAnim();
            jumpQueued = false;
        }

        Vector3 planar = ComputePlanarDirection(moveInput) * moveSpeed;
        Vector3 finalMove = planar + Vector3.up * velocity.y;
        Controller.Move(finalMove * Time.deltaTime);
    }

    /// <summary>
    /// 將 2D 輸入轉為世界空間方向。有相機時以相機水平朝向為基準。
    /// </summary>
    private Vector3 ComputePlanarDirection(Vector2 input)
    {
        if (cameraTransform == null)
            return new Vector3(input.x, 0f, input.y);

        Vector3 forward = cameraTransform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = cameraTransform.right;     right.y = 0f; right.Normalize();
        return (forward * input.y) + (right * input.x);
    }
    #endregion 移動邏輯

    #region 動畫更新
    /// <summary>
    /// 每 frame 更新 Animator 的 Speed 與 Grounded 參數。未指定 Animator 時直接返回。
    /// </summary>
    private void UpdateAnimator()
    {
        if (animator == null) return;

        float planarSpeed = moveInput.magnitude * moveSpeed;
        animator.SetFloat(SpeedHash, planarSpeed);
        animator.SetBool(GroundedHash, Controller.isGrounded);
    }

    private void TriggerJumpAnim()
    {
        if (animator == null) return;
        animator.SetTrigger(JumpTriggerHash);
    }
    #endregion 動畫更新
}
