using UnityEngine;

/// <summary>
/// Enemy follows assigned waypoints according to the patrol type set in SO_EnemyBehavior.
/// Transitions to Chase on target detection.
/// </summary>
public class EnemyState_Patrol : IEnemyState
{
    public void Enter(EnemyBlackboard board)
    {
        board.anim?.SetBool("isMoving", true);
        board.mover?.SetSpeed(board.profile.movement.walkSpeed);
        board.mover?.SetSmoothTime(board.profile.movement.velocitySmoothTime);
        board.hasAggroed = false;   // disengaged — next engage plays the aggro alert again

        if (!board.HasPatrolRoute) return;

        board.currentWaypointIndex =
            Mathf.Clamp(board.currentWaypointIndex, 0, board.patrolPoints.Length - 1);
        board.mover?.MoveTo(board.patrolPoints[board.currentWaypointIndex]);
    }

    public void Update(EnemyBlackboard board)
    {
        if (board.mover == null) return;
        if (!board.HasPatrolRoute) return;

        if (board.mover.IsAtTarget)
        {
            AdvanceWaypoint(board);
            board.mover.MoveTo(board.patrolPoints[board.currentWaypointIndex]);
        }
    }

    public void FixedUpdate(EnemyBlackboard board) { }

    public void Exit(EnemyBlackboard board) { }

    public string CheckTransitions(EnemyBlackboard board)
    {
        if (board.hasTarget) return "Chase";
        return null;
    }

    private void AdvanceWaypoint(EnemyBlackboard board)
    {
        int count = board.patrolPoints.Length;
        if (count < 2) { board.currentWaypointIndex = 0; return; }

        switch (board.profile.behavior.patrolType)
        {
            case PatrolType.LoopWaypoints:
                board.currentWaypointIndex = (board.currentWaypointIndex + 1) % count;
                break;

            case PatrolType.PingPong:
                if (board.patrolForward)
                {
                    board.currentWaypointIndex++;
                    if (board.currentWaypointIndex >= count)
                    {
                        board.currentWaypointIndex = count - 2;
                        board.patrolForward = false;
                    }
                }
                else
                {
                    board.currentWaypointIndex--;
                    if (board.currentWaypointIndex < 0)
                    {
                        board.currentWaypointIndex = 1;
                        board.patrolForward = true;
                    }
                }
                break;

            case PatrolType.RandomWaypoints:
                board.currentWaypointIndex = Random.Range(0, count);
                break;
        }
    }
}
