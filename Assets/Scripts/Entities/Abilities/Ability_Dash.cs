using System.Collections;
using UnityEngine;

/// <summary>
/// Optionally pulls back opposite the target's direction first (anticipation telegraph),
/// then launches the enemy toward the target, held for dashDuration and hard-stopped.
/// The target's position is sampled twice, independently: once to aim the anticipation
/// pullback, and again right before the dash fires — so if the target moves during the
/// wind-up, the dash aims at where it actually is by then, not where it was when the
/// ability was first triggered. Both phases are curve-eased per axis; optionally
/// invulnerable during the dash itself (anticipation is always vulnerable — that's the
/// point of a telegraph).
/// </summary>
public class Ability_Dash : IEnemyAbility
{
    private readonly SO_AbilityDash _config;
    private float _cooldownTimer;

    public string AbilityName => _config.abilityName;
    public bool   IsReady     => _cooldownTimer <= 0f;
    public float  Range       => _config.range;
    public SO_AbilityBase Config => _config;

    public Ability_Dash(SO_AbilityDash config)
    {
        _config = config;
    }

    public void Execute(EnemyBlackboard board)
    {
        if (!IsReady || board.target == null || board.owner == null) return;

        _cooldownTimer = _config.cooldown;

        board.owner.StartCoroutine(DashSequenceCO(board));
    }

    private IEnumerator DashSequenceCO(EnemyBlackboard board)
    {
        // Anticipation aims opposite wherever the target is right now — just the
        // telegraph's direction, not a commitment to dash that way.
        Vector2 anticTargetPos = board.target.position;
        Vector2 anticDir = ((Vector2)anticTargetPos - board.rb.position).normalized;

        if (_config.useAnticipation)
            yield return AnticipationCO(board, anticDir);

        if (board.target == null)
            yield break; // target lost/destroyed during the wind-up

        // Re-sample the target's position now, right before the dash actually fires — not
        // the position captured before the anticipation pullback ran. The target (and the
        // enemy, after pulling back) may have moved during that window, so this is where
        // the dash actually aims from/at.
        Vector2 dashTargetPos = board.target.position;
        Vector2 dashDir = ((Vector2)dashTargetPos - board.rb.position).normalized;

        if (_config.dashParticlesPrefab != null)
            Object.Instantiate(_config.dashParticlesPrefab, board.rb.position, Quaternion.identity);

        yield return DashCO(board, dashDir);
    }

    private IEnumerator AnticipationCO(EnemyBlackboard board, Vector2 telegraphDir)
    {
        if (!string.IsNullOrEmpty(_config.anticipationTrigger))
            board.anim?.SetTrigger(_config.anticipationTrigger);

        if (_config.anticipationDistance > 0f)
        {
            Vector2 antDir = -telegraphDir;
            yield return AbilityMotion.MoveCO(board, antDir, _config.anticipationDistance,
                _config.anticipationDuration, _config.anticipationCurveX, _config.anticipationCurveY);
        }
        else
        {
            yield return AbilityMotion.WaitCO(_config.anticipationDuration);
        }
    }

    private IEnumerator DashCO(EnemyBlackboard board, Vector2 dashDir)
    {
        if (_config.invulnerableDuringDash)
            board.health?.SetAbilityInvulnerable(true);

        // try/finally, not "clear it after the loop": CTRL_AbilityController.CancelActiveAbilities
        // (fired from EnemyState_TakeDamage/EnemyState_Dead) can StopAllCoroutines() this coroutine
        // mid-flight, which jumps straight to Dispose() — any cleanup sitting after the loop simply
        // never runs. That used to leave an enemy permanently invulnerable (every future TakeDamage
        // call bails on the _abilityInvulnerable check at its very first line, so hits stop
        // registering entirely — no flash, no knockback, nothing) if the dash coroutine was ever
        // stopped by anything other than running to completion. A finally block runs on Dispose()
        // too, so this is guaranteed to clear regardless of how the coroutine ends.
        try
        {
            Vector2 startPos = board.rb.position;
            float duration = Mathf.Max(_config.dashDuration, 0.0001f);
            float startTime = Time.time;

            while (true)
            {
                float t = Mathf.Clamp01((Time.time - startTime) / duration);
                float offsetX = dashDir.x * _config.dashDistance * _config.dashCurveX.Evaluate(t);
                float offsetY = dashDir.y * _config.dashDistance * _config.dashCurveY.Evaluate(t);
                // MovePosition, same as the anticipation pullback — collision-aware, and the curve
                // now directly controls how much of dashDistance has been covered, not an instantaneous speed.
                board.rb.MovePosition(startPos + new Vector2(offsetX, offsetY));

                if (t >= 1f) break;
                yield return null;
            }

            board.rb.linearVelocity = Vector2.zero;
        }
        finally
        {
            if (_config.invulnerableDuringDash)
                board.health?.SetAbilityInvulnerable(false);
        }
    }

    public void UpdateCooldown(float deltaTime)
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= deltaTime;
    }

    public void ResetCooldown() => _cooldownTimer = 0f;
}
