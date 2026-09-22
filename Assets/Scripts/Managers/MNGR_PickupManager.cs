using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Single point of contact for the pickup system. Owns collected-currency totals and fires
/// events other systems (HUD, ability controllers, save system) subscribe to, instead of Pickup
/// mutating game state directly the way Entity/CTRL_EnemyHealth stay decoupled from combat
/// callers. Also pools pickup instances per source prefab so levels with lots of coins don't
/// Instantiate/Destroy every one of them — GC churn matters on Android/arm64.
/// </summary>
public class MNGR_PickupManager : Singleton<MNGR_PickupManager>
{
    [System.Serializable]
    public class PickupDataEvent : UnityEvent<SO_PickupData> { }

    [Header("Persistence")]
    [Tooltip("Load/save the coin total to PlayerPrefs so it survives between app sessions, not just scenes.")]
    [SerializeField] private bool _persistCoins = true;
    [SerializeField] private string _coinsPrefsKey = "WildSpyre_Coins";

    [Header("Events")]
    [Tooltip("Fired after Coins changes. Read the Coins property in the handler, same way " +
             "MNGR_PlayerLife.updatePlayerLivesHUD() is called after numLives changes.")]
    public UnityEvent onCoinsChanged = new UnityEvent();
    [Tooltip("Fired for every collected pickup, coins included. Health/PowerUp/Custom kinds have " +
             "no manager-side handling — subscribe here (e.g. from MNGR_PlayerLife or an ability " +
             "controller) and switch on data.kind to react, so this manager never needs a direct " +
             "reference to every system a pickup might affect.")]
    public PickupDataEvent onPickupCollected = new PickupDataEvent();

    private int _coins;
    private readonly Dictionary<Pickup, Queue<Pickup>> _pool = new Dictionary<Pickup, Queue<Pickup>>();

    public int Coins => _coins;

    private void Awake()
    {
        if (_persistCoins)
            _coins = PlayerPrefs.GetInt(_coinsPrefsKey, 0);
    }

    // ── Economy ──────────────────────────────────────────────────────────────

    /// <summary>Called by Pickup.Collect(). Handles Coin directly; every kind (Coin included)
    /// also goes out on onPickupCollected for anything else that cares.</summary>
    public void CollectPickup(SO_PickupData data)
    {
        if (data == null) return;

        if (data.kind == PickupKind.Coin)
        {
            _coins += Mathf.Max(0, data.amount);
            if (_persistCoins)
                PlayerPrefs.SetInt(_coinsPrefsKey, _coins);
            onCoinsChanged.Invoke();
        }

        onPickupCollected.Invoke(data);
    }

    // ── Pooling ──────────────────────────────────────────────────────────────

    /// <summary>Reuses a released instance of this exact prefab if one's free, otherwise
    /// Instantiates a new one. Prefer this over Instantiate(pickupPrefab, ...) directly wherever
    /// you spawn pickups at runtime (drops, level dressing spawned by a manager, etc.).</summary>
    public Pickup Spawn(Pickup prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        Pickup instance;
        if (_pool.TryGetValue(prefab, out Queue<Pickup> queue) && queue.Count > 0)
        {
            instance = queue.Dequeue();
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
        }
        else
        {
            instance = Instantiate(prefab, position, rotation);
        }

        instance.SetSourcePrefab(prefab);
        return instance;
    }

    /// <summary>Called by Pickup.Despawn() instead of Destroy() whenever the instance came from
    /// Spawn(). Deactivates and returns it to the queue for its source prefab.</summary>
    public void Release(Pickup prefab, Pickup instance)
    {
        if (prefab == null || instance == null) return;

        instance.gameObject.SetActive(false);

        if (!_pool.TryGetValue(prefab, out Queue<Pickup> queue))
        {
            queue = new Queue<Pickup>();
            _pool[prefab] = queue;
        }
        queue.Enqueue(instance);
    }
}
