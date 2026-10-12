using UnityEngine;

/// <summary>
/// Tuning for a ground pound: fires when the player taps to exit Sling (the existing small-
/// release-magnitude branch in INPT_TouchManager.ExecuteSling) shortly after slinging nearly
/// straight down into qualifying ground. All the numbers live here, not on CTRL_PlayerGroundPound,
/// so different ground pounds (bigger shockwave, different projectile, looser angle tolerance...)
/// can be authored as separate assets later without touching code — same split as
/// SO_PlayerAbility_Combo/Charge/Aim already use for the player's other abilities.
/// </summary>
[CreateAssetMenu(fileName = "NewGroundPoundAbility", menuName = "WildSpyre/Player/Ability/Ground Pound")]
public class SO_PlayerAbility_GroundPound : ScriptableObject
{
    [Header("Ground Check")]
    [Tooltip("Layer(s) checked for ground pound eligibility — the surface slung into.")]
    public LayerMask groundMask;
    [Tooltip("Max distance below the player a ground surface can be found and still qualify — used " +
             "both for the immediate check at tap time and to re-validate a buffered earlier contact.")]
    public float maxDistanceFromGround = 0.5f;
    [Tooltip("Max allowed difference between the ground's surface normal and world-up (1 - dot " +
             "product of the two normalized vectors, same style of check CTRL_PlayerPlatformer's own " +
             "ground detection already uses via groundCheck.maxGroundedSlopeDot). 0 = normals " +
             "identical (dead-flat ground, ideal). 1 = a wall (normal perpendicular to up). 2 = a " +
             "ceiling (normal pointing straight down). Keep this small (0.05-0.15) to stay restricted " +
             "to near-flat ground — 0.1 is roughly equivalent to the old 25-ish degree tolerance.")]
    public float maxNormalDifference = 0.1f;

    [Header("Timing")]
    [Tooltip("How long after a qualifying ground contact the player can still tap to trigger ground " +
             "pound, even if they've already bounced back off the ground by the time they tap — same " +
             "idea as a jump buffer.")]
    public float groundPoundBuffer = 0.15f;
    [Tooltip("Seconds player input stays locked out while the ground pound plays out, before control " +
             "returns to normal Platformer movement.")]
    public float groundPoundPauseTime = 0.3f;

    [Header("Animation")]
    public string animTrigger = "GroundPound";

    [Header("Impact Feedback")]
    [Tooltip("Real-time seconds the whole game freezes on impact. 0 = none.")]
    public float hitStopDuration = 0.08f;
    public float shakeStrength = 0.3f;
    public float shakeDuration = 0.2f;
    public GameObject impactParticlesPrefab;

    [Header("Ground Projectiles")]
    [Tooltip("Must have an IThrownProjectile component — CTRL_SurfaceProjectile is the intended fit, " +
             "so each one sticks to and crawls along the ground away from the impact point. Spawned " +
             "twice: once launched right, once launched left.")]
    public GameObject surfaceProjectilePrefab;
    public float projectileLaunchSpeed = 6f;
    [Tooltip("How far above the registered impact point each projectile spawns, so it doesn't spawn " +
             "embedded in the ground collider it's about to attach to.")]
    public float projectileSpawnClearance = 0.1f;
    [Tooltip("How far sideways — in each projectile's own launch direction — it spawns from the " +
             "impact point. Needs to clear the player's own collider: spawning exactly at the impact " +
             "point (which is right where the player's body is) makes the projectile's first physics " +
             "step register an overlap with the player and self-destruct immediately, since the " +
             "player isn't tagged DamagePlayer/Enemy or on the projectile's ground mask.")]
    public float projectileSpawnOffset = 0.4f;
}
