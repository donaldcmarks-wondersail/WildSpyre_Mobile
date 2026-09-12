using UnityEngine;

/// <summary>
/// Brief hit-stun state. Enemy halts movement and plays a damage reaction.
/// Returns to Chase or Patrol once the reaction window expires.
/// </summary>
public class EnemyState_TakeDamage : IEnemyState
{
    private float _reactionTimer;
    private const float ReactionDuration = 0.35f;

    public void Enter(EnemyBlackboard board)
    {
        _reactionTimer = ReactionDuration;
        board.mover?.Stop();
        board.anim?.SetTrigger("TakeDamage");
    }

    public void Update(EnemyBlackboard board)
    {
        _reactionTimer -= Time.deltaTime;
    }

    public void FixedUpdate(EnemyBlackboard board) { }

    public void Exit(EnemyBlackboard board) { }

    public string CheckTransitions(EnemyBlackboard board)
    {
        if (_reactionTimer > 0f) return null;

        if (board.hasTarget) return "Chase";
        return board.HasPatrolRoute ? "Patrol" : "Idle";
    }
}
