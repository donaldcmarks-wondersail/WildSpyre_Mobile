using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager that tracks all active enemies in the current scene.
/// Enemies self-register on Start and unregister on Destroy via CTRL_Enemy.
/// </summary>
public class MNGR_EnemyManager : MonoBehaviour
{
    private static MNGR_EnemyManager _instance;
    public static MNGR_EnemyManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MNGR_EnemyManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("MNGR_EnemyManager");
                    _instance = go.AddComponent<MNGR_EnemyManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
    [Header("Enemy Tracking (Read Only)")]
    [SerializeField] private List<CTRL_Enemy> _activeEnemies = new List<CTRL_Enemy>();

    public IReadOnlyList<CTRL_Enemy> ActiveEnemies => _activeEnemies;
    public int EnemyCount => _activeEnemies.Count;

    // ── Registration ─────────────────────────────────────────────────────────
    public void RegisterEnemy(CTRL_Enemy enemy)
    {
        if (!_activeEnemies.Contains(enemy))
            _activeEnemies.Add(enemy);
    }

    public void UnregisterEnemy(CTRL_Enemy enemy)
    {
        _activeEnemies.Remove(enemy);
    }

    // ── Queries ───────────────────────────────────────────────────────────────
    /// <summary>Returns the nearest living enemy to a world position, or null if none exist.</summary>
    public CTRL_Enemy GetNearestEnemy(Vector2 position)
    {
        CTRL_Enemy nearest  = null;
        float      bestDist = float.MaxValue;

        foreach (CTRL_Enemy enemy in _activeEnemies)
        {
            if (enemy == null) continue;
            float dist = Vector2.Distance(position, enemy.rb.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest  = enemy;
            }
        }

        return nearest;
    }
}
