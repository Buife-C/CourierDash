using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Camera feedback for the player controller:
///  - Field of view widens as the player goes from running to sprinting.
///  - A small camera shake (Cinemachine Impulse) on jump, and a bigger one
///    on landing that scales with how hard the impact was.
///
/// Put this on the same object as PlayerController. The FreeLook camera
/// needs a "Cinemachine Impulse Listener" extension for the shake to show.
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CameraFeel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private MovementSettings settings;

    [Header("Sprint FOV")]
    [Tooltip("Degrees added to the field of view at full sprint.")]
    [SerializeField] private float sprintFovBoost = 10f;
    [Tooltip("How quickly the FOV changes. Higher = snappier.")]
    [SerializeField] private float fovSharpness = 6f;

    [Header("Impulse (camera shake)")]
    [SerializeField] private float jumpForce = 0.05f;
    [Tooltip("Impact speed at or below this gives the weakest landing shake.")]
    [SerializeField] private float softLandSpeed = 3f;
    [Tooltip("Impact speed at or above this gives the strongest landing shake.")]
    [SerializeField] private float hardLandSpeed = 12f;
    [SerializeField] private float minLandForce = 0.15f;
    [SerializeField] private float maxLandForce = 1f;

    private PlayerController player;
    private CinemachineImpulseSource impulseSource;
    private float baseFov;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        impulseSource = GetComponent<CinemachineImpulseSource>();

        if (cinemachineCamera == null)
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();

        if (cinemachineCamera != null)
            baseFov = cinemachineCamera.Lens.FieldOfView;
        else
            Debug.LogWarning($"{name}: No CinemachineCamera found, sprint FOV disabled.");
    }

    private void OnEnable()
    {
        player.Jumped += OnJumped;
        player.Landed += OnLanded;
    }

    private void OnDisable()
    {
        player.Jumped -= OnJumped;
        player.Landed -= OnLanded;
    }

    private void Update()
    {
        if (cinemachineCamera == null || settings == null) return;

        float t = Mathf.InverseLerp(settings.runSpeed, settings.sprintSpeed, player.Speed);
        float targetFov = baseFov + sprintFovBoost * t;

        var lens = cinemachineCamera.Lens;
        lens.FieldOfView = Mathf.Lerp(
            lens.FieldOfView, targetFov, 1f - Mathf.Exp(-fovSharpness * Time.deltaTime));
        cinemachineCamera.Lens = lens;
    }

    private void OnJumped()
    {
        impulseSource.GenerateImpulseWithForce(jumpForce);
    }

    private void OnLanded(float impactSpeed)
    {
        float t = Mathf.InverseLerp(softLandSpeed, hardLandSpeed, impactSpeed);
        impulseSource.GenerateImpulseWithForce(Mathf.Lerp(minLandForce, maxLandForce, t));
    }
}
