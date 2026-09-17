using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Second, independent touch joystick dedicated to the player's ability kit — a wholly
/// separate GameObject/component from VariableJoystick (Jump/Sling), sharing only the
/// Joystick Pack base class for its drag/direction math. Never calls into
/// CTRL_PlayerPlatformer at all; only talks to CTRL_PlayerAbilityController.
///
/// A quick tap (released before the equipped charge ability's chargeTimeThreshold)
/// registers a combo hit. Holding past that threshold and releasing fires the charge
/// ability, aimed at the drag direction held at release.
/// </summary>
public class INPT_AbilityJoystick : Joystick
{
    [SerializeField] private CTRL_PlayerAbilityController _abilityController;

    private bool _isPressed;
    private float _pressStartTime;

    private void Update()
    {
        if (_isPressed && _abilityController != null)
            _abilityController.OnHoldTick(Time.time - _pressStartTime);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        _pressStartTime = Time.time;
        base.OnPointerDown(eventData);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        float heldDuration = Time.time - _pressStartTime;
        Vector2 releaseDir = Direction;

        _isPressed = false;
        base.OnPointerUp(eventData); // zeroes Direction/handle — read releaseDir first

        if (_abilityController == null) return;

        if (heldDuration < _abilityController.ChargeTimeThreshold)
            _abilityController.RegisterTap();
        else
            _abilityController.OnChargeReleased(heldDuration, releaseDir);
    }
}
