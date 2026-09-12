using UnityEngine;

/// <summary>
/// Damage-dealing hitbox for enemy attacks (melee swings, dash contact, etc.).
///
/// Tag: "DamagePlayer" — MNGR_PlayerLife.OnTriggerEnter2D already listens for this tag,
/// so player damage flows through the existing system with no modification needed.
///
/// Usage:
///   - Attach to a child GameObject of the enemy with its own Collider2D (trigger).
///   - Disable the child GameObject by default; abilities enable/disable it during attacks.
///   - The enemy body itself (tag "Enemy") handles passive contact damage via
///     MNGR_PlayerLife.OnCollisionEnter2D — no damager needed for that.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CTRL_EnemyDamager : MonoBehaviour
{
    private void Awake()
    {
        gameObject.tag = "DamagePlayer";
        GetComponent<Collider2D>().isTrigger = true;

        // Hitbox is inactive by default; abilities activate it during attack windows
        gameObject.SetActive(false);
    }

    /// <summary>Enable or disable this hitbox. Called by ability scripts.</summary>
    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}
