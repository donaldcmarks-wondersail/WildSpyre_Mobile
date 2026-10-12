using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

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

        // This manager is DontDestroyOnLoad (via Singleton<T>), so it survives a level reload
        // ("Try Again") — but every pooled instance in _pool is a normal scene object and does NOT
        // survive. Without this, Spawn() would dequeue a reference to something Unity already
        // destroyed and throw MissingReferenceException the moment it touched .transform, silently
        // breaking every future drop for that prefab (the exception gets swallowed by Die()'s
        // onDeath try/catch, so it fails with zero visible symptom beyond "no pickups anymore").
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Every pooled instance belonged to the scene that just went away — nothing left to reuse.
        // Costs a few fresh Instantiate calls for the first drops in the new scene; that's fine,
        // pooling exists to avoid *ongoing* per-frame churn, not a handful of one-time allocations.
        _pool.Clear();
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

        Pickup instance = null;
        if (_pool.TryGetValue(prefab, out Queue<Pickup> queue))
        {
            // Defense in depth alongside OnSceneLoaded's Clear(): skip over any entry Unity has
            // already destroyed out from under us (== here is Unity's overload, which correctly
            // treats a destroyed object as null) rather than throwing on the first one found.
            while (queue.Count > 0 && instance == null)
            {
                Pickup candidate = queue.Dequeue();
                if (candidate != null)
                    instance = candidate;
            }

            if (instance != null)
            {
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.gameObject.SetActive(true);
            }
        }

        if (instance == null)
            instance = Instantiate(prefab, position, rotation);

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
