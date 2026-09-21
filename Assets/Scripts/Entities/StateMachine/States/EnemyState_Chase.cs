using UnityEngine;

/// <summary>
/// Enemy actively pathfinds toward the player.
/// Transitions to Attack when close enough, or back to Idle/Patrol if target is lost.
///
/// Aggro alert (SO_EnemyBehavior.useAggroAlert): on the first engage of a fresh
/// pursuit, the enemy fires an animation trigger and holds still — turned to face
/// the player — for aggroAlertDelay seconds before the chase movement begins.
/// </summary>
public class EnemyState_Chase : IEnemyState
{
    private bool _alerting;
    private float _alertTimer;

    public void Enter(EnemyBlackboard board)
    {
        board.loseTargetTimer = 0f;
        board.mover?.SetSpeed(board.profile.movement.chaseSpeed);
        board.mover?.SetSmoothTime(board.profile.movement.chaseVelocitySmoothTime);

        SO_EnemyBehavior behavior = board.profile.behavior;

        _alerting = behavior.useAggroAlert
                 && !board.hasAggroed
                 && board.hasTarget && board.target != null;
        board.hasAggroed = true;         // engaged now — don't alert again until a full disengage
        board.aggroAlerting = _alerting; // CTRL_Enemy faces the player while this is set

        if (_alerting)
        {
            _alertTimer = behavior.aggroAlertDelay;
            board.mover?.Stop();
            board.anim?.SetBool("isMoving", false);
            if (!string.IsNullOrEmpty(behavior.aggroAlertTrigger))
            {
                board.anim?.SetTrigger(behavior.aggroAlertTrigger);
#if UNITY_EDITOR
                LogAggroDiagnostics(board, behavior.aggroAlertTrigger);
#endif
            }
            return;
        }

        board.anim?.SetBool("isMoving", true);
        if (board.hasTarget && board.target != null)
            board.mover?.MoveTo(board.target.position);
    }

    public void Update(EnemyBlackboard board)
    {
        if (_alerting)
        {
            _alertTimer -= Time.deltaTime;
            if (_alertTimer > 0f)
                return;

            _alerting = false;
            board.aggroAlerting = false;   // hold over — face travel direction again
            board.anim?.SetBool("isMoving", true);
        }

        if (board.hasTarget && board.target != null)
        {
            board.lastKnownTargetPos = board.target.position;
            board.loseTargetTimer = 0f;
            board.mover?.MoveTo(board.target.position);
        }
        else
        {
            board.loseTargetTimer += Time.deltaTime;

            // Keep moving toward last known position
            board.mover?.MoveTo(board.lastKnownTargetPos);
        }
    }

    public void FixedUpdate(EnemyBlackboard board) { }

    public void Exit(EnemyBlackboard board)
    {
        _alerting = false;
        board.aggroAlerting = false;
        board.mover?.SetSpeed(board.profile.movement.walkSpeed);
        board.mover?.SetSmoothTime(board.profile.movement.velocitySmoothTime);
    }

    public string CheckTransitions(EnemyBlackboard board)
    {
        if (_alerting) return null;   // focusing on the player — commit to the wind-up

        if (board.hasTarget && board.target != null)
        {
            float dist = Vector2.Distance(board.rb.position, board.target.position);
            // Not while interrupted — Attack would fire, fail the lockout, and burn its cooldown.
            if (dist <= board.AttackRange && board.attackCooldownTimer <= 0f && !board.AbilitiesLocked)
                return "Attack";
        }

        if (!board.hasTarget && board.loseTargetTimer >= board.profile.behavior.loseTargetTime)
        {
            return board.HasPatrolRoute ? "Patrol" : "Idle";
        }

        return null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only. Dumps everything needed to work out why an aggro SetTrigger
    /// isn't producing a state change: which Animator/controller was hit, whether
    /// the parameter actually exists on it, whether it's enabled and on-screen,
    /// and what state layer 0 is currently in.
    /// </summary>
    private static void LogAggroDiagnostics(EnemyBlackboard board, string triggerName)
    {
        Animator a = board.anim;
        string name = triggerName.ToUpperInvariant();

        if (a == null)
        {
            Debug.Log("<color=red><b>>>>>> AGGRO TRIGGER \"" + name +
                      "\" - board.anim is NULL. Assign the Anim field on CTRL_Enemy.</b></color>", board.owner);
            return;
        }

        bool paramExists = false;
        foreach (AnimatorControllerParameter p in a.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
            {
                paramExists = true;
                break;
            }
        }

        string controllerName = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "NONE";
        string paramMsg = paramExists
            ? "EXISTS (trigger)"
            : "MISSING  <-- wrong controller / wrong Animator reference";
        bool isMoving = a.GetBool("isMoving");
        int stateHash = a.GetCurrentAnimatorStateInfo(0).shortNameHash;

        string msg =
            "<color=red><b>>>>>> AGGRO TRIGGER \"" + name + "\" SET <<<<<\n" +
            "   animator GameObject : '" + a.gameObject.name + "'  (what CTRL_Enemy.Anim points at)\n" +
            "   controller          : '" + controllerName + "'\n" +
            "   trigger param on it : " + paramMsg + "\n" +
            "   animator.enabled    : " + a.enabled + "\n" +
            "   GO activeInHierarchy: " + a.gameObject.activeInHierarchy + "\n" +
            "   cullingMode         : " + a.cullingMode + "  (use AlwaysAnimate to rule out culling)\n" +
            "   layer0 stateHash    : " + stateHash + "   isMoving=" + isMoving + "\n" +
            "   If param EXISTS but no transition: check AnyState->Aggro isn't Muted, nothing is Solo'd, culling = AlwaysAnimate.</b></color>";

        Debug.Log(msg, a);
    }
#endif
}
