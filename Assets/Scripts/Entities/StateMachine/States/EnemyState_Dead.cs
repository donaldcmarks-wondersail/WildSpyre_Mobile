using UnityEngine;

/// <summary>
/// Terminal state. Stops all movement, triggers death animation, spawns death particles,
/// and shuts off everything that could still hurt the player. The root collider is
/// deliberately left on so a dying enemy still collides with walls/ground instead of
/// clipping through them. Destruction of the GameObject is handled by CTRL_EnemyHealth
/// after SO_EnemyStats.deathDestroyDelay.
/// </summary>
public class EnemyState_Dead : IEnemyState
{
    public void Enter(EnemyBlackboard board)
    {
        board.mover?.Stop();

        if (board.owner != null)
        {
            // Every ability (dash, melee, projectile, ability movement) runs its coroutines on
            // board.owner, so this cancels any in-flight one. CTRL_EnemyHealth's own
            // coroutine lives on a separate component and is unaffected.
            board.owner.StopAllCoroutines();

            // Body-contact hitbox and any melee hitbox (both CTRL_EnemyDamager) — the things
            // that damage the player. A stopped melee coroutine would otherwise leave its
            // hitbox stuck enabled.
            foreach (CTRL_EnemyDamager damager in board.owner.GetComponentsInChildren<CTRL_EnemyDamager>(true))
                damager.SetActive(false);
        }

        // A stopped dash can't clear its own invulnerability flag.
        board.health?.SetAbilityInvulnerable(false);

        board.anim?.SetTrigger("Death");

        if (board.profile.stats.deathParticlesPrefab != null)
            Object.Instantiate(board.profile.stats.deathParticlesPrefab, board.rb.position, Quaternion.identity);
    }

    public void Update(EnemyBlackboard board) { }

    public void FixedUpdate(EnemyBlackboard board) { }

    public void Exit(EnemyBlackboard board) { }

    public string CheckTransitions(EnemyBlackboard board)
    {
        // No transitions out of Dead — CTRL_EnemyHealth calls Destroy
        return null;
    }
}
