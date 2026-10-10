using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// LAB 3 - GOAL 2 & GOAL 3
///
/// Reads the Move action (WASD / left stick), feeds it to the Animator's
/// 2D Freeform Directional blend tree, turns the character toward the
/// direction of travel, and moves it. Walks by default, runs while the
/// Sprint action (Left Shift / gamepad stick press) is held.
/// Put this on your character next to its Animator component and drag
/// your Input Actions asset into the "Input Actions" slot.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerMovementInput : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Drag your Input Actions asset here.")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string sprintActionName = "Sprint";

    [Header("Animator Parameters")]
    [SerializeField] private string moveXParam = "MoveX";
    [SerializeField] private string moveYParam = "MoveY";

    [Header("Smoothing")]
    [SerializeField] private float dampTime = 0.1f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 4f;
    [SerializeField] private float turnSharpness = 12f;

    // These match the Pos Y values of Walk_N and Run_N in the blend tree.
    private const float WalkBlend = 0.5f;
    private const float RunBlend = 1f;

    private InputAction moveAction;
    private InputAction sprintAction;
    private Animator animator;

    /// <summary>The current input direction, updated every frame.</summary>
    public Vector2 MoveInput { get; private set; }

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Starter Assets controller: Grounded must be true and MotionSpeed
        // must be 1, otherwise the walk/run animation is blocked or frozen.
        animator.SetBool("Grounded", true);
        animator.SetFloat("MotionSpeed", 1f);

        if (inputActions == null)
        {
            Debug.LogError($"{name}: No InputActionAsset assigned to PlayerMovementInput. " +
                            "Drag your Input Actions asset into the Input Actions field.");
            return;
        }

        var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
        if (map == null)
        {
            Debug.LogError($"{name}: Action map \"{actionMapName}\" not found in {inputActions.name}.");
            return;
        }

        moveAction = map.FindAction(moveActionName, throwIfNotFound: false);
        if (moveAction == null)
        {
            Debug.LogError($"{name}: Action \"{moveActionName}\" not found in map \"{actionMapName}\".");
        }

        sprintAction = map.FindAction(sprintActionName, throwIfNotFound: false);
        if (sprintAction == null)
        {
            Debug.LogWarning($"{name}: Action \"{sprintActionName}\" not found, so the character will always walk.");
        }
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        sprintAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        sprintAction?.Disable();
    }

    private void Update()
    {
        MoveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        bool running = sprintAction != null && sprintAction.IsPressed();

        // Forward-only animations: X stays 0, Y is the blend position
        // (0 = idle, 0.5 = walk, 1 = run). This is the ONLY place
        // MoveX / MoveY are set.
        float inputAmount = Mathf.Clamp01(MoveInput.magnitude);
        float blendTarget = inputAmount * (running ? RunBlend : WalkBlend);
        animator.SetFloat(moveXParam, 0f, dampTime, Time.deltaTime);
        animator.SetFloat(moveYParam, blendTarget, dampTime, Time.deltaTime);

        Transform cam = Camera.main.transform;
        Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        Vector3 move = camRight * MoveInput.x + camForward * MoveInput.y;

        if (move.sqrMagnitude > 0.01f)
        {
            // Turn to face the direction of travel...
            Quaternion target = Quaternion.LookRotation(move.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, target, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));

            // ...and move that way, at a speed that matches the animation.
            float speed = running ? runSpeed : walkSpeed;
            transform.Translate(move.normalized * speed * Time.deltaTime, Space.World);
        }
    }
}