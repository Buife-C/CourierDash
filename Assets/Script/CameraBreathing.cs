using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Makes the camera "breathe": a slow, gentle sway while standing still that
/// becomes faster and stronger as the character speeds up, like heavier
/// breathing while sprinting.
///
/// Needs a Noise component (Basic Multi Channel Perlin) with a Noise Profile
/// on the FreeLook camera. Put this script on the player, next to
/// PlayerController.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CameraBreathing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private MovementSettings settings;

    [Header("Resting (standing still)")]
    [Tooltip("How strong the sway is at rest. Keep it subtle.")]
    [SerializeField] private float restAmplitude = 0.3f;
    [Tooltip("How fast the sway is at rest. Low = slow, calm breathing.")]
    [SerializeField] private float restFrequency = 0.4f;

    [Header("Sprinting (out of breath)")]
    [SerializeField] private float sprintAmplitude = 1.0f;
    [SerializeField] private float sprintFrequency = 1.2f;

    [Header("Smoothing")]
    [Tooltip("How quickly the breathing changes when you speed up or slow down.")]
    [SerializeField] private float sharpness = 3f;

    private PlayerController player;
    private CinemachineBasicMultiChannelPerlin noise;

    private void Awake()
    {
        player = GetComponent<PlayerController>();

        if (cinemachineCamera == null)
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();

        if (cinemachineCamera != null)
            noise = cinemachineCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();

        if (noise == null)
            Debug.LogWarning($"{name}: No Noise (Basic Multi Channel Perlin) found on the camera, so there is no breathing.");
    }

    private void Update()
    {
        if (noise == null || settings == null) return;

        // 0 when standing still, 1 at full sprint.
        float t = Mathf.InverseLerp(0f, settings.sprintSpeed, player.Speed);
        float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);

        noise.AmplitudeGain = Mathf.Lerp(
            noise.AmplitudeGain, Mathf.Lerp(restAmplitude, sprintAmplitude, t), k);
        noise.FrequencyGain = Mathf.Lerp(
            noise.FrequencyGain, Mathf.Lerp(restFrequency, sprintFrequency, t), k);
    }
}
