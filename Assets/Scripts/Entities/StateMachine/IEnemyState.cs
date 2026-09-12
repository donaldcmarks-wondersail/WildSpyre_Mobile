/// <summary>
/// Interface implemented by every enemy state.
/// States communicate through the shared EnemyBlackboard.
/// </summary>
public interface IEnemyState
{
    void Enter(EnemyBlackboard board);
    void Update(EnemyBlackboard board);
    void FixedUpdate(EnemyBlackboard board);
    void Exit(EnemyBlackboard board);

    /// <summary>
    /// Returns the name of the next state to transition to, or null to stay in this state.
    /// </summary>
    string CheckTransitions(EnemyBlackboard board);
}
