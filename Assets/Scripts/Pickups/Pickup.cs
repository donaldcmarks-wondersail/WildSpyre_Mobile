using UnityEngine;

/// <summary>
/// Generic pickup: collision, lifetime, and FX only. All tuning (kind/amount, behavior,
/// magnetize, FX) lives on an SO_PickupData asset — see SO_PickupData.cs — so new pickup
/// variants (a bigger coin, a health orb, a buff) are authored as data rather than new C#
/// subclasses the way PickupCoin used to be. Movement is delegated to an IPickupMotion picked
/// from data.behavior (PickupMotions.cs); magnetize is delegated to the optional
/// PickupMagnetize component so it can be toggled independently at runtime.
///
/// Reports collection to MNGR_PickupManager rather than mutating game state itself, and spawns/
/// despawns through MNGR_PickupManager's pool when possible, so levels with lots of pickups
/// don't churn Instantiate/Destroy on mobile.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Pickup : MonoBehaviour
{
    [Tooltip("Collider2D on this object must have Is Trigger on — collection is detected in OnTriggerEnter2D.")]
    [SerializeField] private SO_PickupData _data;

    private IPickupMotion _motion;
    private PickupMagnetize _magnetize;
    private PickupBurst _burst;
    private float _lifetimeTimer;

    /// <summary>Set by MNGR_PickupManager.Spawn() right after Instantiate. Null for a pickup
    /// placed directly in a scene, in which case Despawn() just Destroys it instead of pooling.</summary>
    private Pickup _sourcePrefab;

    public SO_PickupData Data => _data;

    private void Awake()
    {
        _magnetize = GetComponent<PickupMagnetize>();
        _burst = GetComponent<PickupBurst>();
        _motion = CreateMotion(_data != null ? _data.behavior : PickupBehaviorType.Static);
    }

    private void OnEnable()
    {
        _lifetimeTimer = 0f;
        _motion.Init(transform, _data);
        if (_magnetize != null)
            _magnetize.Init(_data);

        if (_data != null && _data.spawnFXPrefab != null)
            Instantiate(_data.spawnFXPrefab, transform.position, Quaternion.identity);
    }

    private void Update()
    {
        if (_data == null) return;

        // Priority: a burst in progress owns the transform outright (PickupMagnetize defers to it
        // too — see its own Update()); otherwise magnetize owns it while actively pulling; only
        // once neither applies does the idle IPickupMotion get to touch position. Never more than
        // one of these three writes transform.position in the same frame.
        bool bursting = _burst != null && _burst.IsBursting;
        bool magnetizing = !bursting && _magnetize != null && _magnetize.IsMagnetizing;

        if (!bursting && !magnetizing)
            _motion.Tick(Time.deltaTime);

        if (_data.lifetime > 0f)
        {
            _lifetimeTimer += Time.deltaTime;
            if (_lifetimeTimer >= _data.lifetime)
                Despawn();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        Collect();
    }

    private void Collect()
    {
        if (_data != null)
        {
            if (_data.pickupFXPrefab != null)
                Instantiate(_data.pickupFXPrefab, transform.position, Quaternion.identity);
            if (_data.pickupSFX != null)
                AudioSource.PlayClipAtPoint(_data.pickupSFX, transform.position);

            if (MNGR_PickupManager.Instance != null)
                MNGR_PickupManager.Instance.CollectPickup(_data);
        }

        Despawn();
    }

    private void Despawn()
    {
        if (_sourcePrefab != null && MNGR_PickupManager.Instance != null)
            MNGR_PickupManager.Instance.Release(_sourcePrefab, this);
        else
            Destroy(gameObject);
    }

    /// <summary>Called by MNGR_PickupManager.Spawn() — see its doc comment.</summary>
    public void SetSourcePrefab(Pickup prefab)
    {
        _sourcePrefab = prefab;
    }

    /// <summary>Called by whoever spawns this pickup (e.g. CTRL_EnemyPickupDrop) right after
    /// MNGR_PickupManager.Spawn(), to play the burst-and-land intro instead of settling straight
    /// into idle motion. No-op if this prefab has no PickupBurst component.</summary>
    public void BeginBurst(Vector2 hitDirection)
    {
        _burst?.Begin(hitDirection);
    }

    private static IPickupMotion CreateMotion(PickupBehaviorType type)
    {
        switch (type)
        {
            case PickupBehaviorType.Move: return new PickupMotion_Move();
            case PickupBehaviorType.Bounce: return new PickupMotion_Bounce();
            case PickupBehaviorType.MoveAndBounce: return new PickupMotion_MoveAndBounce();
            case PickupBehaviorType.Fly: return new PickupMotion_Fly();
            case PickupBehaviorType.Evade: return new PickupMotion_Evade();
            case PickupBehaviorType.Chase: return new PickupMotion_Chase();
            default: return new PickupMotion_Static();
        }
    }
}
