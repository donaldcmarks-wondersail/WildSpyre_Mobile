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
    private float _lifetimeTimer;

    /// <summary>Set by MNGR_PickupManager.Spawn() right after Instantiate. Null for a pickup
    /// placed directly in a scene, in which case Despawn() just Destroys it instead of pooling.</summary>
    private Pickup _sourcePrefab;

    public SO_PickupData Data => _data;

    private void Awake()
    {
        _magnetize = GetComponent<PickupMagnetize>();
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

        // Magnetize takes over the transform while it's pulling the pickup in; don't fight it
        // with the idle motion the same frame.
        if (_magnetize == null || !_magnetize.IsMagnetizing)
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
