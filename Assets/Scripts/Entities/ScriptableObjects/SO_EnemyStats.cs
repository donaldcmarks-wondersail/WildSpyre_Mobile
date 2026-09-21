using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "WildSpyre/Enemy/Stats")]
public class SO_EnemyStats : ScriptableObject
{
    [Header("Health")]
    public int maxHP = 3;
    public float invulnerabilityDuration = 0.5f;

    [Header("Knockback (Weight)")]
    [Tooltip("This enemy is never knocked back by any hit.")]
    public bool ignoreKnockback = false;
    [Tooltip("Multiplier on every knockback this enemy receives. 1.0 = standard; above 1 flies further " +
             "(lighter), below 1 barely moves (heavier). No effect if Ignore Knockback is on.")]
    public float knockbackMagnitude = 1f;

    [Header("Damage To Player")]
    public int damageDealtToPlayer = 1;
    public float knockbackForce = 5f;
    public Vector2 knockbackDirection = new Vector2(1f, 0.5f);

    [Header("Death")]
    [Tooltip("Seconds between dying and the enemy GameObject being destroyed — set this to " +
             "match the length of this enemy's death animation.")]
    public float deathDestroyDelay = 1.5f;

    [Header("FX")]
    public GameObject deathParticlesPrefab;
    public GameObject hitParticlesPrefab;
}
