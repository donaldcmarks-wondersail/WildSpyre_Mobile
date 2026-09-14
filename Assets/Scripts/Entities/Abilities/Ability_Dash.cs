using System.Collections;
using UnityEngine;

/// <summary>
/// Launches the enemy toward the player with a burst of velocity, held for
/// dashDuration, then hard-stopped. Optionally invulnerable for that window.
/// </summary>
public class Ability_Dash : IEnemyAbility
{
    private readonly SO_AbilityDash _config;
    private float _cooldownTimer;

    public string AbilityName => _config.abilityName;
    public bool   IsReady     => _cooldownTimer <= 0f;
    public float  Range       => _config.range;

    public Ability_Dash(SO_AbilityDash config)
    {
        _config = config;
    }

    public void Execute(EnemyBlackboard board)
    {
        if (!IsReady || board.target == null || board.owner == null) return;

        _cooldownTimer = _config.cooldown;

        Vector2 dashDir = ((Vector2)board.target.position - board.rb.position).normalized;

        if (_config.dashParticlesPrefab != null)
            Object.Instantiate(_config.dashParticlesPrefab, board.rb.position, Quaternion.identity);

        board.owner.StartCoroutine(DashCO(board, dashDir));
    }

    private IEnumerator DashCO(EnemyBlackboard board, Vector2 dashDir)
    {
        if (_config.invulnerableDuringDash)
            board.health?.SetAbilityInvulnerable(true);

        board.rb.linearVelocity = dashDir * _config.dashForce;

        float startTime = Time.time;
        while (Time.time < startTime + _config.dashDuration)
            yield return null;

        board.rb.linearVelocity = Vector2.zero;

        if (_config.invulnerableDuringDash)
            board.health?.SetAbilityInvulnerable(false);
    }

    public void UpdateCooldown(float deltaTime)
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= deltaTime;
    }

    public void ResetCooldown() => _cooldownTimer = 0f;
}
