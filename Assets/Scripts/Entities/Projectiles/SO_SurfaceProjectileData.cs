using UnityEngine;

/// <summary>What CTRL_SurfaceProjectile does once nothing supports it anymore — it ran off the end
/// of an enabled range, or the next surface probe found nothing at all within reach.</summary>
public enum SurfaceDetachBehavior
{
    /// <summary>Destroyed immediately — simplest, good for a crawler that should never be loose.</summary>
    Destroy,
    /// <summary>Gravity turns back on and it keeps going as a free projectile along its last
    /// crawl direction — e.g. walking off a ledge with no wall to catch it.</summary>
    FreeFall,
    /// <summary>Stops dead where it left off. Rare, but useful for a projectile that should just
    /// sit as a stationary hazard once it can't find anywhere valid left to go.</summary>
    Stop
}

/// <summary>
/// One accepted band of surface angles, measured consistently as degrees between the surface
/// normal and world Vector2.up: 0 = flat floor, 90 = a perfectly vertical wall, 180 = a flat
/// ceiling. Independent Enabled flags per surface type (see SO_SurfaceProjectileData) are what let
/// you support, say, ground and ceiling but not walls, or only near-vertical walls rather than the
/// whole transitional slope in between.
/// </summary>
[System.Serializable]
public struct SurfaceSupportRange
{
    public bool enabled;
    public float minAngle;
    public float maxAngle;

    public bool Contains(float angleFromUp) => enabled && angleFromUp >= minAngle && angleFromUp <= maxAngle;
}

/// <summary>
/// Tuning for CTRL_SurfaceProjectile: what angles of ground/wall/ceiling it's allowed to stick to
/// and crawl along, how abrupt a corner it can round without falling off, and what happens once it
/// runs out of supported surface.
/// </summary>
[CreateAssetMenu(fileName = "NewSurfaceProjectileData", menuName = "WildSpyre/Enemy/Projectile/Surface Data")]
public class SO_SurfaceProjectileData : ScriptableObject
{
    [Header("Flight")]
    [Tooltip("Speed it travels along a surface once attached. Initial free-flight speed is passed " +
             "in separately by whatever calls Launch() — typically the throwing ability's own config.")]
    public float speed = 6f;
    public int damage = 1;
    [Tooltip("Projectile Lifetime — how long until it self-destructs, AND the span Velocity Curve " +
             "and Sprite Curve below are normalized against (their X axis is 0-1 progress through " +
             "this, not raw seconds — same convention as a Particle System's Size/Color over " +
             "Lifetime). One number driving both, so the curves can never silently fall out of " +
             "sync with how long the thing actually lives.")]
    public float lifetimeSeconds = 6f;
    [Tooltip("What counts as a surface it can stick to and crawl on.")]
    public LayerMask groundMask;
    [Tooltip("Real-time seconds right after spawning during which an overlap with something that " +
             "would normally destroy it (anything not ground-mask, DamagePlayer, or Enemy) is " +
             "ignored instead. Whatever spawned this thing — a hand, a burst point, an impact point " +
             "— is often itself something this would otherwise 'hit' and die against on its very " +
             "first physics step, before its launch velocity has had a chance to carry it clear. " +
             "0 = no grace, the old behavior.")]
    public float spawnGracePeriod = 0.08f;

    [Header("Surface Support")]
    [Tooltip("Flat/gently-sloped floors.")]
    public SurfaceSupportRange groundSupport = new SurfaceSupportRange { enabled = true, minAngle = 0f, maxAngle = 45f };
    [Tooltip("Vertical-ish surfaces. Narrow this (e.g. 70-110) if you only want near-perfectly-" +
             "vertical walls supported, not the transitional slope between ground and wall.")]
    public SurfaceSupportRange wallSupport = new SurfaceSupportRange { enabled = true, minAngle = 45f, maxAngle = 135f };
    [Tooltip("Overhangs/ceilings. Off by default — most projectiles shouldn't crawl upside-down " +
             "unless you specifically want that.")]
    public SurfaceSupportRange ceilingSupport = new SurfaceSupportRange { enabled = false, minAngle = 135f, maxAngle = 180f };

    [Header("Surface Tracking")]
    [Tooltip("How far ahead along the current travel direction the next-surface probe starts from " +
             "each tick — roughly how far ahead it's looking. Too small and it can clip through a " +
             "sharp convex corner before the probe catches the new face; too large and it visibly " +
             "cuts corners short.")]
    public float surfaceProbeAheadDistance = 0.15f;
    [Tooltip("How far the probe searches perpendicular into the surface for the next contact point.")]
    public float surfaceProbeDistance = 0.6f;
    [Tooltip("How far off the surface (along its normal) the projectile actually rests, so its " +
             "collider/sprite doesn't sit embedded in the ground.")]
    public float surfaceSkin = 0.05f;
    [Tooltip("Max degrees the surface normal is allowed to change between one probe and the next " +
             "before it counts as leaving the surface rather than going around a corner. A sharp " +
             "right-angle corner (ground to wall, or wall to ceiling) is about a 90-degree jump in " +
             "one sample — set this a bit above 90 (100-120 is a good start) so real corners clear " +
             "comfortably while a near-180 reversal (the back face of a thin wall or spike) still " +
             "gets rejected instead of wrapping onto it.")]
    public float maxTransitionAngleDelta = 110f;
    [Tooltip("Degrees/second the VISUAL rotation eases toward the tracked surface normal. Separate " +
             "from Max Transition Angle Delta above, which governs whether a corner is accepted at " +
             "all — this just keeps the sprite from visibly snapping once it has been.")]
    public float rotationSmoothSpeed = 540f;
    [Tooltip("Added to the computed facing angle before it's applied. 0 if your sprite is drawn " +
             "facing +X (right) by default — matches CTRL_EnemyProjectile's convention. Use -90 if " +
             "it's drawn facing +Y (up) by default instead, which reads more naturally for a crawler " +
             "whose 'top' should point away from the surface it's stuck to.")]
    public float orientationOffsetDegrees = 0f;

    [Header("On Leaving Supported Surface")]
    public SurfaceDetachBehavior detachBehavior = SurfaceDetachBehavior.FreeFall;

    [Header("Lifetime-Driven Curves")]
    [Tooltip("Multiplies Speed above while crawling (not during free flight) over the course of the " +
             "projectile's life — X axis is 0-1 progress through Lifetime Seconds (0 = just spawned, " +
             "1 = about to self-destruct), Y is the multiplier (1 = full configured Speed, 0 = " +
             "stopped, >1 = faster than configured). Flat at 1 for the old constant-speed behavior.")]
    public AnimationCurve velocityCurve = AnimationCurve.Constant(0f, 1f, 1f);
    [Tooltip("Picks which entry of Sprite Frames below is shown, over the course of the projectile's " +
             "whole life (not just while crawling). X axis is 0-1 progress through Lifetime Seconds; " +
             "Y is read directly as a frame index — e.g. a key at (0, 0) and (0.5, 3) steps/eases " +
             "from frame 0 to frame 3 over the first half of its life. The raw output is rounded to " +
             "the nearest whole number and clamped into a valid index, so you can key it at exact " +
             "integers for a hard cut between frames or let it ease through fractional values for " +
             "however that reads once rounded. No effect if Sprite Frames is empty.")]
    public AnimationCurve spriteCurve = AnimationCurve.Constant(0f, 1f, 0f);
    [Tooltip("Frames Sprite Curve above swaps between, by rounded index. Needs a SpriteRenderer on " +
             "the prefab (CTRL_SurfaceProjectile auto-finds one on its own GameObject if not wired " +
             "up explicitly). Leave empty to skip sprite-swapping entirely.")]
    public Sprite[] spriteFrames;

    [Header("FX")]
    public GameObject hitParticlesPrefab;
}
