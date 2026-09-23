using UnityEngine;

/// <summary>
/// Spawns a burst of pickups from this enemy's death position, biased toward the direction of
/// the killing blow — shoot an enemy left-to-right and its drops pop out to the right, via
/// CTRL_EnemyHealth.LastHitDirection. Subscribes to CTRL_EnemyHealth.onDeath.
///
/// Every entry in Drops is rolled independently (not a single weighted pick), so one enemy can
/// mix "always drop 1-3 coins" (Drop Chance 1) with "10% chance of a gem" (Drop Chance 0.1) in
/// the same list.
/// </summary>
[RequireComponent(typeof(CTRL_EnemyHealth))]
public class CTRL_EnemyPickupDrop : MonoBehaviour
{
    [System.Serializable]
    public class DropEntry
    {
        public Pickup prefab;
        [Tooltip("Chance this entry drops at all, rolled once per death.")]
        [Range(0f, 1f)] public float dropChance = 1f;
        public int minCount = 1;
        public int maxCount = 1;
    }

    [SerializeField] private DropEntry[] _drops;

    private CTRL_EnemyHealth _health;

    private void Awake()
    {
        _health = GetComponent<CTRL_EnemyHealth>();
        _health.onDeath.AddListener(SpawnDrops);
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.onDeath.RemoveListener(SpawnDrops);
    }

    private void SpawnDrops()
    {
        if (_drops == null || _drops.Length == 0) return;
        if (MNGR_PickupManager.Instance == null) return;

        Vector2 hitDirection = _health.LastHitDirection;
        Vector3 spawnPos = transform.position;

        foreach (DropEntry entry in _drops)
        {
            if (entry.prefab == null) continue;
            if (Random.value > entry.dropChance) continue;

            int count = Random.Range(entry.minCount, Mathf.Max(entry.minCount, entry.maxCount) + 1);
            for (int i = 0; i < count; i++)
            {
                Pickup instance = MNGR_PickupManager.Instance.Spawn(entry.prefab, spawnPos, Quaternion.identity);
                if (instance != null)
                    instance.BeginBurst(hitDirection);
            }
        }
    }
}
