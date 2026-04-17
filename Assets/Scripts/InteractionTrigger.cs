using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 互動觸發區域（獨立 InputAction 版）：玩家進入 Trigger 範圍時顯示提示 UI，
/// 按下綁定的互動鍵時觸發 UnityEvent。
/// </summary>
/// <remarks>
/// 使用獨立 InputAction，不依賴 Input Actions Asset，Inspector 可直接綁定 Binding。
/// 注意：此版本自行管理 Enable/Disable，不會受 InputMapManager 整合控制。
/// 若需要跟 Player/UI map 切換同步，請改用 InputActionReference 版本。
/// </remarks>
[RequireComponent(typeof(Collider))]
public class InteractionTrigger : MonoBehaviour
{
    #region 基本設定
    [Header("基本設定")]
    [SerializeField, Tooltip("可觸發互動的物件 Tag")]
    private string playerTag = "Player";

    [SerializeField, Tooltip("互動按鍵（Inspector 直接綁定 Binding，例如 <Keyboard>/e）")]
    private InputAction interactAction;

    [SerializeField, Tooltip("提示文字，例如：按 E 互動")]
    private string promptText = "按 E 互動";
    #endregion 基本設定

    #region 提示 UI
    [Header("提示 UI")]
    [SerializeField, Tooltip("提示 UI 根物件（進入時啟用，離開時關閉）")]
    private GameObject promptRoot;

    [SerializeField, Tooltip("顯示提示文字的 Label，可選")]
    private Text promptLabel;
    #endregion 提示 UI

    #region 互動事件
    [Header("互動事件")]
    [SerializeField, Tooltip("玩家在範圍內按下互動鍵時觸發")]
    private UnityEvent onInteract;
    #endregion 互動事件

    #region 內部狀態
    private bool isPlayerInside;
    #endregion 內部狀態

    #region Unity 生命週期
    private void OnEnable()
    {
        SubscribeAndEnableInput();
        ApplyPromptText();
        SetPromptVisible(false);
    }

    private void OnDisable()
    {
        DisableAndUnsubscribeInput();
        isPlayerInside = false;
        SetPromptVisible(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        isPlayerInside = true;
        SetPromptVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        isPlayerInside = false;
        SetPromptVisible(false);
    }
    #endregion Unity 生命週期

    #region 互動邏輯
    /// <summary>
    /// Input System 的 performed callback，只在玩家仍在範圍內時觸發 UnityEvent。
    /// </summary>
    private void HandleInteractPerformed(InputAction.CallbackContext context)
    {
        if (!isPlayerInside) return;
        onInteract?.Invoke();
    }

    private void SubscribeAndEnableInput()
    {
        if (interactAction == null) return;
        interactAction.performed += HandleInteractPerformed;
        interactAction.Enable();
    }

    private void DisableAndUnsubscribeInput()
    {
        if (interactAction == null) return;
        interactAction.Disable();
        interactAction.performed -= HandleInteractPerformed;
    }
    #endregion 互動邏輯

    #region UI 控制
    private void SetPromptVisible(bool visible)
    {
        if (promptRoot != null) promptRoot.SetActive(visible);
    }

    private void ApplyPromptText()
    {
        if (promptLabel != null) promptLabel.text = promptText;
    }
    #endregion UI 控制
}
