using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Courier player controller: camera-relative movement with acceleration,
/// smooth turning, jump (coyote time, jump buffering, variable height),
/// crouch, falling and landing. Drives the Starter Assets animator.
///
/// Setup: put this on the character (it needs a CharacterController and an
/// Animator). Assign a MovementSettings asset and your Input Actions asset.
/// Remove the old PlayerMovementInput script from the character.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private MovementSettings settings;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string sprintActionName = "Sprint";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string crouchActionName = "Crouch";

    [Header("Animation")]
    [SerializeField] private float blendDampTime = 0.08f;

    // Events other scripts (audio, VFX, camera shake) can subscribe to.
    public event Action Jumped;
    public event Action<float> Landed;   // passes the impact speed

    public Vector2 MoveInput { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsSprinting { get; private set; }
    /// <summary>True while crouched. AI scripts can read this for stealth.</summary>
    public bool IsCrouching { get; private set; }
    public float Speed => horizontalVelocity.magnitude;
    public float VerticalVelocity => verticalVelocity;

    /// <summary>Multiplies top speed. Used by the package (heavier = slower).</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    private CharacterController controller;
    private Animator animator;
    private Transform cameraTransform;
    private InputAction moveAction, sprintAction, jumpAction, crouchAction;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float airTime;
    private bool isJumping;

    private float standingHeight;
    private float standingCenterY;
    private readonly Collider[] overlapBuffer = new Collider[8];

    private const float GroundedStick = -2f;
    private const float FreeFallDelay = 0.15f;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    private static readonly int CrouchingHash = Animator.StringToHash("Crouching");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        standingHeight = controller.height;
        standingCenterY = controller.center.y;

        if (Camera.main != null) cameraTransform = Camera.main.transform;
        else Debug.LogError($"{name}: No camera tagged MainCamera found.");

        if (settings == null)
            Debug.LogError($"{name}: No MovementSettings assigned to PlayerController.");

        if (inputActions == null)
        {
            Debug.LogError($"{name}: No InputActionAsset assigned to PlayerController.");
            return;
        }

        var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
        if (map == null)
        {
            Debug.LogError($"{name}: Action map \"{actionMapName}\" not found in {inputActions.name}.");
            return;
        }

        moveAction = map.FindAction(moveActionName, throwIfNotFound: false);
        sprintAction = map.FindAction(sprintActionName, throwIfNotFound: false);
        jumpAction = map.FindAction(jumpActionName, throwIfNotFound: false);
        crouchAction = map.FindAction(crouchActionName, throwIfNotFound: false);

        if (moveAction == null) Debug.LogError($"{name}: Action \"{moveActionName}\" not found.");
        if (sprintAction == null) Debug.LogWarning($"{name}: Action \"{sprintActionName}\" not found, no sprint.");
        if (jumpAction == null) Debug.LogWarning($"{name}: Action \"{jumpActionName}\" not found, no jump.");
        if (crouchAction == null) Debug.LogWarning($"{name}: Action \"{crouchActionName}\" not found, no crouch.");
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        sprintAction?.Enable();
        jumpAction?.Enable();
        crouchAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        sprintAction?.Disable();
        jumpAction?.Disable();
        crouchAction?.Disable();
    }

    private void Update()
    {
        if (settings == null) return;

        float dt = Time.deltaTime;

        ReadInput();
        UpdateGroundState(dt);
        UpdateCrouch(dt);
        UpdateHorizontal(dt);
        UpdateJumpAndGravity(dt);

        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * dt);

        UpdateAnimator(dt);
    }

    private void ReadInput()
    {
        MoveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
    }

    private void UpdateGroundState(float dt)
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = controller.isGrounded;

        if (!IsGrounded)
        {
            airTime += dt;
        }
        else if (!wasGrounded)
        {
            // Just landed this frame.
            float impact = Mathf.Max(0f, -verticalVelocity);
            if (airTime > FreeFallDelay) Landed?.Invoke(impact);
            isJumping = false;
            airTime = 0f;
            verticalVelocity = GroundedStick;
        }
        else
        {
            airTime = 0f;
        }
    }

    private void UpdateCrouch(float dt)
    {
        bool crouchHeld = crouchAction != null && crouchAction.IsPressed();

        if (crouchHeld && IsGrounded)
            IsCrouching = true;
        else if (!crouchHeld && IsCrouching && CanStandUp())
            IsCrouching = false;   // stays crouched while something is overhead

        // Shrink / grow the collision capsule, keeping the feet in place.
        float targetHeight = IsCrouching ? settings.crouchHeight : standingHeight;
        float newHeight = Mathf.MoveTowards(controller.height, targetHeight, settings.crouchResizeSpeed * dt);
        controller.height = newHeight;
        controller.center = new Vector3(
            controller.center.x,
            standingCenterY - (standingHeight - newHeight) * 0.5f,
            controller.center.z);
    }

    private bool CanStandUp()
    {
        // Is there room for the full standing capsule above the feet?
        Vector3 feet = transform.position + controller.center - Vector3.up * (controller.height * 0.5f);
        float r = controller.radius * 0.95f;
        Vector3 bottom = feet + Vector3.up * (r + 0.05f);
        Vector3 top = feet + Vector3.up * (standingHeight - r);

        int count = Physics.OverlapCapsuleNonAlloc(
            bottom, top, r, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (overlapBuffer[i] != controller) return false;
        }
        return true;
    }

    private void UpdateHorizontal(float dt)
    {
        Vector3 desired = CameraRelative(MoveInput);
        bool hasInput = desired.sqrMagnitude > 0.01f;

        bool walkHeld = Keyboard.current != null && Keyboard.current.leftAltKey.isPressed;
        bool sprintHeld = sprintAction != null && sprintAction.IsPressed();

        IsSprinting = sprintHeld && hasInput && !IsCrouching;

        // Gentle gamepad stick tilt or holding Left Alt = walk.
        bool walking = walkHeld || MoveInput.magnitude < 0.5f;

        float topSpeed = IsCrouching ? settings.crouchSpeed
                       : IsSprinting ? settings.sprintSpeed
                       : walking ? settings.walkSpeed
                       : settings.runSpeed;
        topSpeed *= SpeedMultiplier;

        Vector3 targetVelocity = hasInput ? desired.normalized * topSpeed : Vector3.zero;

        float rate = hasInput ? settings.acceleration : settings.deceleration;
        if (!IsGrounded) rate *= settings.airControl;

        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * dt);

        // Turn to face the input direction.
        if (hasInput)
        {
            Quaternion targetRot = Quaternion.LookRotation(desired.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, 1f - Mathf.Exp(-settings.turnSharpness * dt));
        }
    }

    private void UpdateJumpAndGravity(float dt)
    {
        // Coyote time: still allowed to jump shortly after leaving the ground.
        if (IsGrounded) coyoteTimer = settings.coyoteTime;
        else coyoteTimer -= dt;

        // Jump buffer: remember a jump press for a moment before landing.
        if (jumpAction != null && jumpAction.WasPressedThisFrame())
            jumpBufferTimer = settings.jumpBufferTime;
        else
            jumpBufferTimer -= dt;

        // No jumping while crouched.
        if (jumpBufferTimer > 0f && coyoteTimer > 0f && !IsCrouching)
        {
            verticalVelocity = Mathf.Sqrt(settings.jumpHeight * -2f * settings.gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            isJumping = true;
            Jumped?.Invoke();
        }

        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = GroundedStick;
            return;
        }

        float g = settings.gravity;
        if (verticalVelocity < 0f)
            g *= settings.fallGravityMultiplier;
        else if (isJumping && jumpAction != null && !jumpAction.IsPressed())
            g *= settings.lowJumpGravityMultiplier;

        verticalVelocity = Mathf.Max(verticalVelocity + g * dt, settings.terminalVelocity);
    }

    private void UpdateAnimator(float dt)
    {
        float speed = Speed;
        float walk = settings.walkSpeed;
        float run = settings.runSpeed;

        // Map real speed onto the blend tree: Idle 0, Walk_N 0.5, Run_N 1.
        float blend;
        if (speed <= walk)
            blend = walk > 0f ? (speed / walk) * 0.5f : 0f;
        else
            blend = 0.5f + Mathf.Clamp01((speed - walk) / (run - walk)) * 0.5f;

        animator.SetFloat(MoveXHash, 0f);
        animator.SetFloat(MoveYHash, blend, blendDampTime, dt);
        animator.SetFloat(SpeedHash, speed);
        animator.SetFloat(MotionSpeedHash, IsSprinting && IsGrounded ? settings.sprintAnimSpeed : 1f);

        animator.SetBool(GroundedHash, IsGrounded);
        animator.SetBool(JumpHash, isJumping);
        animator.SetBool(FreeFallHash, !IsGrounded && !isJumping && airTime > FreeFallDelay);
        animator.SetBool(CrouchingHash, IsCrouching);
    }

    private Vector3 CameraRelative(Vector2 input)
    {
        if (cameraTransform == null) return new Vector3(input.x, 0f, input.y);

        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        return right * input.x + forward * input.y;
    }
}