using UnityEngine;

/// <summary>
/// Terminal state. Stops all movement, triggers death animation, spawns death particles.
/// Destruction of the GameObject is handled by CTRL_EnemyHealth after a short delay.
/// </summary>
public class EnemyState_Dead : IEnemyState
{
    public void Enter(EnemyBlackboard board)
    {
        board.mover?.Stop();
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
