using UnityEngine;

/// <summary>
/// Spawns a dust particle prefab at the character's feet while the
/// character is moving faster than a threshold speed.
///
/// Put this on the same GameObject as PlayerMovementInput (the one that
/// moves). Drag your dust prefab into the "Dust Prefab" slot.
/// </summary>
public class DustTrail : MonoBehaviour
{
    [Header("Dust")]
    [Tooltip("A prefab containing a Particle System.")]
    [SerializeField] private GameObject dustPrefab;
    [Tooltip("Where the dust appears. Leave empty to use this object's position (the feet).")]
    [SerializeField] private Transform feetPoint;

    [Header("Tuning")]
    [Tooltip("Dust only spawns above this ground speed (units per second). Walk is about 1.5, run about 4.")]
    [SerializeField] private float speedThreshold = 3f;
    [Tooltip("Seconds between puffs while above the threshold.")]
    [SerializeField] private float spawnInterval = 0.2f;
    [Tooltip("Seconds before a spawned puff is cleaned up.")]
    [SerializeField] private float dustLifetime = 2f;

    private Vector3 lastPosition;
    private float timer;

    /// <summary>Current horizontal speed, measured from how far we moved this frame.</summary>
    public float Speed { get; private set; }

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        // Our movement uses transform.Translate (no Rigidbody), so there is no
        // built-in velocity. Work it out from the change in position instead.
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f; // ignore vertical movement, only ground speed matters
        Speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        lastPosition = transform.position;

        timer -= Time.deltaTime;

        if (Speed > speedThreshold && timer <= 0f && dustPrefab != null)
        {
            Vector3 spawnPos = feetPoint != null ? feetPoint.position : transform.position;
            GameObject dust = Instantiate(dustPrefab, spawnPos, Quaternion.identity);
            Destroy(dust, dustLifetime);
            timer = spawnInterval;
        }
    }
}
