using UnityEngine;

/// <summary>
/// Lightweight damage-dealing hitbox for player abilities (combo hits, the charge
/// projectile) — deals damage to enemies with no side effects on the player, unlike
/// Damager (used for the stomp), whose DamagerReaction() always gives the player an
/// upward bounce. Read directly by CTRL_EnemyHealth.OnTriggerEnter2D via layer + this
/// component type, independent of the Damager pathway.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerAbilityDamager : MonoBehaviour
{
    public int damage = 1;
    [SerializeField] private bool _startActive = false;

    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("PlayerDmg");
        GetComponent<Collider2D>().isTrigger = true;
        gameObject.SetActive(_startActive);
    }

    /// <summary>Enable or disable this hitbox. Called by CTRL_PlayerAbilityController.</summary>
    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}
