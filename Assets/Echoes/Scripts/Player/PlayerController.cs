using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador en primera persona del jugador (sin modelo visible): mirar con el ratón,
/// caminar, correr, saltar y agacharse. Usa CharacterController y el nuevo Input System
/// (teclado/ratón y mando). "Player Scale" agranda al jugador y ajusta altura,
/// velocidades y salto en proporción.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Referencias")]
    public Transform cameraPivot;
    public Camera playerCamera;

    [Header("Tamaño")]
    [Tooltip("Multiplica altura, radio, velocidades y salto. 1 = persona de 1.8 m.")]
    [Min(0.1f)] public float playerScale = 2.5f;

    [Header("Vista")]
    public float mouseSensitivity = 0.1f;
    public float gamepadLookSpeed = 160f;
    public float maxLookAngle = 85f;
    public bool lockCursor = true;
    public float walkFov = 70f;
    public float sprintFov = 78f;
    public float fovChangeSpeed = 8f;

    [Header("Movimiento (a escala 1)")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float crouchSpeed = 2f;
    public float acceleration = 12f;
    public float airControl = 0.3f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Agacharse (a escala 1)")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.2f;
    public float radius = 0.35f;
    public float crouchTransitionSpeed = 8f;
    public float eyeOffsetFromTop = 0.15f;

    [Header("Balanceo de cabeza")]
    public bool enableHeadBob = true;
    public float strideLength = 1.4f;
    public float bobAmplitude = 0.045f;

    public bool IsGrounded => controller.isGrounded;
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }
    /// <summary>Velocidad horizontal relativa a caminar (1 = caminando, ~1.75 = corriendo).</summary>
    public float SpeedFactor { get; private set; }

    CharacterController controller;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float pitch;
    float stepPhase;
    float bobBlend;
    float height; // altura actual en unidades de escala 1

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        height = standHeight;
        ApplyScale();
    }

    void OnValidate()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (height <= 0f) height = standHeight;
        ApplyScale();
    }

    void OnEnable()
    {
        if (lockCursor) SetCursorLocked(true);
    }

    void Update()
    {
        HandleCursor();
        HandleLook();
        HandleMovement();
        HandleCrouch();
        HandleCamera();
    }

    public void ApplyScale()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (height <= 0f) height = standHeight;
        float s = playerScale;
        controller.radius = radius * s;
        controller.height = height * s;
        controller.center = new Vector3(0f, height * s * 0.5f, 0f);
        controller.stepOffset = Mathf.Min(0.4f * s, controller.height * 0.5f);
        controller.skinWidth = 0.05f * s;
        if (cameraPivot != null)
            cameraPivot.localPosition = new Vector3(0f, (height - eyeOffsetFromTop) * s, 0f);
    }

    void HandleCursor()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        else if (lockCursor && mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            SetCursorLocked(true);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void HandleLook()
    {
        Vector2 delta = Vector2.zero;
        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            delta += Mouse.current.delta.ReadValue() * mouseSensitivity;
        if (Gamepad.current != null)
            delta += Gamepad.current.rightStick.ReadValue() * gamepadLookSpeed * Time.deltaTime;

        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -maxLookAngle, maxLookAngle);
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMovement()
    {
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        float s = playerScale;

        Vector2 input = Vector2.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
        }
        if (pad != null) input += pad.leftStick.ReadValue();
        input = Vector2.ClampMagnitude(input, 1f);

        bool sprintHeld = (kb != null && kb.leftShiftKey.isPressed) || (pad != null && pad.leftStickButton.isPressed);
        IsSprinting = sprintHeld && !IsCrouching && input.y > 0.1f;

        float speed = (IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed) * s;
        Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;

        float accel = controller.isGrounded ? acceleration : acceleration * airControl;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, accel * speed * Time.deltaTime);

        float g = gravity * s;
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f * s;
            bool jump = (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (jump && !IsCrouching) verticalVelocity = Mathf.Sqrt(jumpHeight * s * -2f * g);
        }
        verticalVelocity += g * Time.deltaTime;

        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        Vector3 v = controller.velocity;
        float horizontalSpeed = new Vector2(v.x, v.z).magnitude;
        SpeedFactor = horizontalSpeed / (walkSpeed * s);
        if (controller.isGrounded)
            stepPhase = Mathf.Repeat(stepPhase + horizontalSpeed / (strideLength * s) * Mathf.PI * 2f * Time.deltaTime, Mathf.PI * 2f);

        float moving = controller.isGrounded ? Mathf.Clamp01(horizontalSpeed / (walkSpeed * s)) : 0f;
        bobBlend = Mathf.MoveTowards(bobBlend, moving, Time.deltaTime * 4f);
    }

    void HandleCrouch()
    {
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        bool crouchHeld = (kb != null && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed)) || (pad != null && pad.buttonEast.isPressed);

        if (crouchHeld) IsCrouching = true;
        else if (IsCrouching && CanStandUp()) IsCrouching = false;

        height = Mathf.MoveTowards(height, IsCrouching ? crouchHeight : standHeight, crouchTransitionSpeed * Time.deltaTime);
        ApplyScale();
    }

    bool CanStandUp()
    {
        float r = controller.radius * 0.9f;
        Vector3 origin = transform.position + Vector3.up * (controller.height - r);
        float distance = (standHeight - height) * playerScale + 0.05f;
        return !Physics.SphereCast(origin, r, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
    }

    void HandleCamera()
    {
        if (cameraPivot == null) return;

        float s = playerScale;
        float bob = enableHeadBob ? -Mathf.Abs(Mathf.Sin(stepPhase)) * bobAmplitude * s * bobBlend : 0f;
        float sway = enableHeadBob ? Mathf.Cos(stepPhase) * bobAmplitude * 0.5f * s * bobBlend : 0f;
        cameraPivot.localPosition += new Vector3(sway, bob, 0f);

        if (playerCamera != null)
        {
            float targetFov = IsSprinting && controller.velocity.sqrMagnitude > walkSpeed * walkSpeed * s * s ? sprintFov : walkFov;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, fovChangeSpeed * Time.deltaTime);
        }
    }
}
