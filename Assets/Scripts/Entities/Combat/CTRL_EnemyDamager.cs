using UnityEngine;

/// <summary>
/// Damage-dealing hitbox for enemy attacks (melee swings, dash contact, etc.).
///
/// Tag: "DamagePlayer" — MNGR_PlayerLife.OnTriggerEnter2D already listens for this tag,
/// so player damage flows through the existing system with no modification needed.
///
/// Usage:
///   - Attach to a child GameObject of the enemy with its own Collider2D (trigger).
///   - Ability-gated hitbox (melee swing, dash contact): leave Start Active off —
///     the child starts disabled and abilities enable/disable it during attack windows.
///   - Passive body-contact damage: turn Start Active on — the hitbox (sized to the
///     enemy's body) stays on permanently. PlayerCol/EnemyCol no longer physically
///     collide (see the Physics 2D layer matrix), so this is how "walking into an
///     enemy hurts you" is detected now instead of a solid-collision contact.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CTRL_EnemyDamager : MonoBehaviour
{
    [SerializeField] private bool _startActive = false;

    /// <summary>
    /// True for the permanent body-contact hitbox, false for ability-gated ones (melee swings).
    /// Lets an interrupt switch off only the ability hitboxes and leave body contact alone.
    /// </summary>
    public bool StartsActive => _startActive;

    private void Awake()
    {
        gameObject.tag = "DamagePlayer";
        GetComponent<Collider2D>().isTrigger = true;

        gameObject.SetActive(_startActive);
    }

    /// <summary>Enable or disable this hitbox. Called by ability scripts.</summary>
    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}
