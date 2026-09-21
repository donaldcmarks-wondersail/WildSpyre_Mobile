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

        // Cancel any in-flight ability and switch off everything that hurts the player, body-contact
        // hitbox included (a dead enemy shouldn't damage on contact).
        board.abilities?.CancelActiveAbilities(board, includeBodyHitbox: true);

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
