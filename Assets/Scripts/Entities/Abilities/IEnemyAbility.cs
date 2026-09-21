/// <summary>
/// Interface for all enemy abilities (dash, melee, projectile, etc.).
/// Implementations are plain C# classes — not MonoBehaviours.
/// Coroutines are started via board.owner (the CTRL_Enemy MonoBehaviour).
/// </summary>
public interface IEnemyAbility
{
    string AbilityName { get; }
    bool   IsReady     { get; }

    /// <summary>Max target distance at which this ability may be used (SO_AbilityBase.range).</summary>
    float Range { get; }

    /// <summary>The ability's authored data — read by CTRL_AbilityController for shared behaviour like armor.</summary>
    SO_AbilityBase Config { get; }

    void Execute(EnemyBlackboard board);
    void UpdateCooldown(float deltaTime);
    void ResetCooldown();
}
