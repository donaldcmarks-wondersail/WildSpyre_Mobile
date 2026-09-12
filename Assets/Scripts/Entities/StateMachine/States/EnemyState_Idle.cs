/// <summary>
/// Enemy stands idle. Transitions to Patrol if waypoints are assigned,
/// or to Chase as soon as a target is detected.
/// </summary>
public class EnemyState_Idle : IEnemyState
{
    public void Enter(EnemyBlackboard board)
    {
        board.mover?.Stop();
        board.anim?.SetBool("isMoving", false);
        board.hasAggroed = false;   // disengaged — next engage plays the aggro alert again
    }

    public void Update(EnemyBlackboard board) { }

    public void FixedUpdate(EnemyBlackboard board) { }

    public void Exit(EnemyBlackboard board) { }

    public string CheckTransitions(EnemyBlackboard board)
    {
        if (board.hasTarget)
            return "Chase";

        if (board.HasPatrolRoute && board.profile.behavior.patrolType != PatrolType.Static)
            return "Patrol";

        return null;
    }
}
