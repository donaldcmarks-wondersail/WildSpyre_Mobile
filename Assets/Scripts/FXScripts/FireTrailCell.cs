using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One pooled "stamp" of the player's ground fire trail. Purely a gameplay hitbox —
/// FireTrailController owns the single LineRenderer that renders the whole trail, so
/// this component has no visual of its own.
///
/// Lifetime and pooling are owned by FireTrailController: it calls Activate() when a
/// stamp is placed and Deactivate() when the stamp burns out or is recycled early.
/// Damage itself isn't applied here either — see FireTrailController's overlap
/// tracker for why (avoids double-dipping when stamps overlap).
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class FireTrailCell : MonoBehaviour
{
    [Header("Visual (Optional)")]
    [Tooltip("Optional 2D Point Light that dims as this stamp burns out, driven by FireTrailController's Light Intensity Curve. Safe to leave unassigned.")]
    [SerializeField] private Light2D _light;

    private CircleCollider2D _collider;
    private FireTrailController _owner;
    private readonly HashSet<IFireDamageable> _overlapping = new HashSet<IFireDamageable>();
    private float _baseLightIntensity;   // the intensity authored on _light in the prefab

    /// <summary>Time.time this stamp was placed — drives both burn-out and the visual fade.</summary>
    public float SpawnTime { get; private set; }

    /// <summary>Surface normal at the point this stamp was placed, for the ribbon's visual offset.</summary>
    public Vector2 SurfaceNormal { get; private set; }

    private void Awake()
    {
        _collider = GetComponent<CircleCollider2D>();
        _collider.isTrigger = true;
        _collider.enabled = false;   // stays off until Activate() places a real stamp

        // Captured once, before anything else can touch it, so it reflects exactly
        // what's authored on the prefab regardless of how many times this pooled
        // instance gets reused.
        if (_light != null) _baseLightIntensity = _light.intensity;
    }

    /// <summary>Places and (re)activates this stamp. Called by FireTrailController from its pool.</summary>
    public void Activate(Vector2 position, Vector2 surfaceNormal, float radius, FireTrailController owner)
    {
        // Activate the GameObject FIRST: if this cell was instantiated inactive (e.g.
        // the prefab's root is saved inactive), Awake() — and the _collider it
        // assigns — doesn't run until SetActive(true) does. Touching _collider before
        // this line would null-ref on a cell that's never been active before.
        gameObject.SetActive(true);

        _owner = owner;
        transform.position = position;
        _collider.radius = radius;
        _collider.enabled = true;
        SurfaceNormal = surfaceNormal;
        SpawnTime = Time.time;
        _overlapping.Clear();
    }

    /// <summary>
    /// Ends this stamp — releases any targets it's still tracking (so a stamp that
    /// burns out, or is recycled early, while something is standing in it can't leave
    /// a phantom damage-tick registration behind) — then deactivates the GameObject.
    /// </summary>
    public void Deactivate()
    {
        foreach (IFireDamageable target in _overlapping)
            _owner.UnregisterOverlap(target);
        _overlapping.Clear();
        _collider.enabled = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Drives this stamp's point light: intensity is a 0–1 multiplier against the
    /// light's own authored starting intensity (captured in Awake), color is used
    /// directly. Called every frame by FireTrailController with its Light Intensity
    /// Curve and Start/End color lerp evaluated at this cell's normalized age. Safe to
    /// call on cells with no light assigned.
    /// </summary>
    public void UpdateLight(float intensityMultiplier, Color color)
    {
        if (_light == null) return;
        _light.intensity = _baseLightIntensity * intensityMultiplier;
        _light.color = color;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IFireDamageable target = other.GetComponentInParent<IFireDamageable>();
        if (target == null || !_overlapping.Add(target)) return;

        _owner.RegisterOverlap(target);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        IFireDamageable target = other.GetComponentInParent<IFireDamageable>();
        if (target == null || !_overlapping.Remove(target)) return;

        _owner.UnregisterOverlap(target);
    }
}
