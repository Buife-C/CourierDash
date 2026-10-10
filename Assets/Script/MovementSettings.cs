using UnityEngine;

/// <summary>
/// All the tuning numbers for the player controller, stored in an asset so
/// they can be tweaked in the Inspector (even during Play mode) without
/// touching any code.
/// Create one via: Assets > Create > Courier > Movement Settings
/// </summary>
[CreateAssetMenu(fileName = "CourierMovementSettings", menuName = "Courier/Movement Settings")]
public class MovementSettings : ScriptableObject
{
    [Header("Speeds (units per second)")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 4f;
    public float sprintSpeed = 6.5f;

    [Header("Responsiveness")]
    [Tooltip("How fast speed ramps UP. Higher = snappier start.")]
    public float acceleration = 14f;
    [Tooltip("How fast speed ramps DOWN when you let go. Higher = stops quicker.")]
    public float deceleration = 20f;
    [Tooltip("How quickly the character turns to face the move direction.")]
    public float turnSharpness = 14f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.3f;
    [Tooltip("Negative number. More negative = heavier.")]
    public float gravity = -22f;
    [Tooltip("Extra gravity while falling. Above 1 removes the floaty feeling.")]
    public float fallGravityMultiplier = 1.6f;
    [Tooltip("Extra gravity if you release Jump early while rising. Gives variable jump height.")]
    public float lowJumpGravityMultiplier = 2.5f;
    [Tooltip("Negative number. Fastest the character can fall.")]
    public float terminalVelocity = -30f;

    [Header("Forgiveness")]
    [Tooltip("Seconds after walking off a ledge that you can still jump.")]
    public float coyoteTime = 0.12f;
    [Tooltip("Seconds before landing that a jump press is remembered.")]
    public float jumpBufferTime = 0.12f;
    [Range(0f, 1f)]
    [Tooltip("How much control you keep in the air. 0 = none, 1 = full.")]
    public float airControl = 0.5f;

    [Header("Crouch")]
    [Tooltip("Movement speed while crouching.")]
    public float crouchSpeed = 1.2f;
    [Tooltip("Height of the collision capsule while crouching (standing is about 1.8).")]
    public float crouchHeight = 1.1f;
    [Tooltip("How fast the capsule shrinks and grows, in units per second.")]
    public float crouchResizeSpeed = 6f;

    [Header("Animation")]
    [Tooltip("Animation playback speed while sprinting (1 = normal run).")]
    public float sprintAnimSpeed = 1.25f;
}