using UnityEngine;

/// <summary>
/// Footsteps, jump and landing sounds for the player.
///
/// Footsteps have two modes:
///  - LOOPED: assign one recording that contains several steps to "Footstep Loop".
///    It loops while the player moves on the ground, fades out when they stop,
///    and plays faster (higher pitch) the faster they move.
///  - STEPPED: leave "Footstep Loop" empty and fill the "Footsteps" list with
///    single-step clips. One is played every time the player travels one stride.
///
/// Jump and landing sounds react to the PlayerController's Jumped and Landed
/// events; landings get louder the harder the impact.
///
/// Put this on the same object as PlayerController. Every field is exposed in
/// the Inspector so it can be tuned without touching code.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MovementSettings settings;
    [Tooltip("Optional. One is created automatically if left empty.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Clips")]
    [Tooltip("A footstep recording with SEVERAL steps in it. Loops while moving.")]
    [SerializeField] private AudioClip footstepLoop;
    [Tooltip("Single-step clips. Only used if Footstep Loop is empty.")]
    [SerializeField] private AudioClip[] footsteps;
    [SerializeField] private AudioClip[] jumpSounds;
    [SerializeField] private AudioClip[] landSounds;

    [Header("Looped footsteps")]
    [SerializeField, Range(0f, 1f)] private float loopVolume = 0.7f;
    [Tooltip("Playback pitch (and speed) while walking.")]
    [SerializeField] private float walkPitch = 0.9f;
    [Tooltip("Playback pitch (and speed) while running.")]
    [SerializeField] private float runPitch = 1.3f;
    [Tooltip("Playback pitch (and speed) at full sprint.")]
    [SerializeField] private float sprintPitch = 1.5f;
    [Tooltip("How quickly the loop fades in and out. Higher = quicker.")]
    [SerializeField] private float fadeSpeed = 10f;

    [Header("Stepped footsteps (single-step clips)")]
    [Tooltip("Metres travelled between steps while walking.")]
    [SerializeField] private float walkStepDistance = 0.8f;
    [Tooltip("Metres travelled between steps while running.")]
    [SerializeField] private float runStepDistance = 1.4f;
    [Tooltip("Metres travelled between steps at full sprint.")]
    [SerializeField] private float sprintStepDistance = 1.6f;
    [SerializeField] private Vector2 footstepVolume = new Vector2(0.5f, 0.8f);
    [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);

    [Header("Jump and Landing")]
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 0.6f;
    [Tooltip("Impact speed at or below this plays the landing quietly.")]
    [SerializeField] private float softLandSpeed = 3f;
    [Tooltip("Impact speed at or above this plays the landing at full volume.")]
    [SerializeField] private float hardLandSpeed = 12f;
    [SerializeField] private Vector2 landVolume = new Vector2(0.4f, 1f);

    private PlayerController player;
    private AudioSource loopSource;
    private float stepAccumulator;

    private void Awake()
    {
        player = GetComponent<PlayerController>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        // A separate source for the looping footsteps, so changing its pitch
        // never affects the jump and landing sounds.
        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.spatialBlend = 0f;
        loopSource.loop = true;
        loopSource.volume = 0f;
        loopSource.clip = footstepLoop;
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
        if (loopSource != null) loopSource.Stop();
    }

    private void Update()
    {
        if (settings == null) return;

        if (footstepLoop != null) UpdateLoopedFootsteps();
        else UpdateSteppedFootsteps();
    }

    private void UpdateLoopedFootsteps()
    {
        bool moving = player.IsGrounded && player.Speed > 0.5f;
        float targetVolume = moving ? loopVolume : 0f;

        loopSource.volume = Mathf.MoveTowards(loopSource.volume, targetVolume, fadeSpeed * Time.deltaTime);

        if (moving)
        {
            float s = player.Speed;
            loopSource.pitch = s <= settings.runSpeed
                ? Mathf.Lerp(walkPitch, runPitch, Mathf.InverseLerp(settings.walkSpeed, settings.runSpeed, s))
                : Mathf.Lerp(runPitch, sprintPitch, Mathf.InverseLerp(settings.runSpeed, settings.sprintSpeed, s));
        }

        if (loopSource.volume > 0.001f && !loopSource.isPlaying) loopSource.Play();
        else if (loopSource.volume <= 0.001f && loopSource.isPlaying) loopSource.Stop();
    }

    private void UpdateSteppedFootsteps()
    {
        if (player.IsGrounded && player.Speed > 0.5f)
        {
            stepAccumulator += player.Speed * Time.deltaTime;

            float speed = player.Speed;
            float stride = speed <= settings.runSpeed
                ? Mathf.Lerp(walkStepDistance, runStepDistance,
                    Mathf.InverseLerp(settings.walkSpeed, settings.runSpeed, speed))
                : Mathf.Lerp(runStepDistance, sprintStepDistance,
                    Mathf.InverseLerp(settings.runSpeed, settings.sprintSpeed, speed));

            if (stepAccumulator >= stride)
            {
                stepAccumulator = 0f;

                // Quieter when walking, full volume when sprinting.
                float loudness = Mathf.Lerp(0.6f, 1f,
                    Mathf.InverseLerp(settings.walkSpeed, settings.sprintSpeed, speed));
                PlayRandom(footsteps, Random.Range(footstepVolume.x, footstepVolume.y) * loudness);
            }
        }
        else if (player.IsGrounded)
        {
            // Standing still: get ready so the first step sounds quickly.
            stepAccumulator = walkStepDistance * 0.5f;
        }
    }

    private void OnJumped()
    {
        PlayRandom(jumpSounds, jumpVolume);
    }

    private void OnLanded(float impactSpeed)
    {
        float t = Mathf.InverseLerp(softLandSpeed, hardLandSpeed, impactSpeed);
        PlayRandom(landSounds, Mathf.Lerp(landVolume.x, landVolume.y, t));
    }

    private void PlayRandom(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, volume);
    }
}