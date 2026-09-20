using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Second, independent touch joystick dedicated to the player's ability kit.
///
/// Derives from VariableJoystick purely to reuse its "hidden until pressed, re-centers on
/// the touch point" behaviour (Joystick Type: Floating or Dynamic) — the same visual
/// behaviour the existing Jump/Sling joystick uses. VariableJoystick's own side effects
/// (playerCtrl.jump() on press, jumpRelease()/ExecuteSling() on release) are all guarded by
/// null checks on its `playerCtrl`/`_touchManager` fields, so as long as this component's
/// instance leaves those two fields unassigned in the Inspector, none of that logic ever
/// runs — only the positioning/show-hide behaviour is inherited. Do NOT assign playerCtrl
/// or _touchManager on this component.
///
/// A quick tap (released before the equipped charge ability's chargeTimeThreshold)
/// registers a combo hit. Holding past that threshold and releasing fires the charge
/// ability, aimed at the drag direction held at release. A quick release with a drag past the
/// equipped aim ability's aimThreshold fires the aim ability instead of the combo tap.
/// </summary>
public class INPT_AbilityJoystick : VariableJoystick
{
    [SerializeField] private CTRL_PlayerAbilityController _abilityController;

    private float _pressStartTime;

    private void Update()
    {
        if (_isPressed && _abilityController != null)
            _abilityController.OnHoldTick(Time.time - _pressStartTime, Direction);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        _pressStartTime = Time.time;
        base.OnPointerDown(eventData); // re-centers + shows the joystick; jump() no-ops (playerCtrl left unassigned)
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        float heldDuration = Time.time - _pressStartTime;
        Vector2 releaseDir = Direction;

        base.OnPointerUp(eventData); // hides the joystick again, zeroes Direction — read releaseDir first

        if (_abilityController == null) return;

        // Held long enough → charge. Otherwise a quick release with a drag past the aim
        // threshold → aim ability. Otherwise (no real aim) → melee combo tap.
        if (heldDuration >= _abilityController.ChargeTimeThreshold)
            _abilityController.OnChargeReleased(heldDuration, releaseDir);
        else if (releaseDir.magnitude >= _abilityController.AimThreshold)
            _abilityController.OnAimReleased(releaseDir);
        else
            _abilityController.RegisterTap();
    }
}
