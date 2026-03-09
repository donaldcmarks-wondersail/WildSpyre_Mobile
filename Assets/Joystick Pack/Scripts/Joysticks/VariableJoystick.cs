using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class VariableJoystick : Joystick
{
    public float MoveThreshold { get { return moveThreshold; } set { moveThreshold = Mathf.Abs(value); } }

    [SerializeField] private float moveThreshold = 1;
    [SerializeField] private JoystickType joystickType = JoystickType.Fixed;
    [HideInInspector] public bool _isPressed;
    public CTRL_PlayerPlatformer playerCtrl;
    [SerializeField] private INPT_TouchManager _touchManager;

    private Vector2 fixedPosition = Vector2.zero;

    public void SetMode(JoystickType joystickType)
    {
        this.joystickType = joystickType;
        if(joystickType == JoystickType.Fixed)
        {
            background.anchoredPosition = fixedPosition;
            background.gameObject.SetActive(true);
        }
        else
            background.gameObject.SetActive(false);
    }

    protected override void Start()
    {
        base.Start();
        fixedPosition = background.anchoredPosition;
        SetMode(joystickType);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        if(joystickType != JoystickType.Fixed)
        {
            background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position);
            background.gameObject.SetActive(true);
        }
        if (playerCtrl != null)
        {
            playerCtrl.jump();
        }

        base.OnPointerDown(eventData);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        if(joystickType != JoystickType.Fixed)
            background.gameObject.SetActive(false);

        if (playerCtrl != null)
        {
            switch (playerCtrl.PlayerState)
            {
                case CTRL_PlayerPlatformer.playerControlState.Platformer:
                    playerCtrl.jumpRelease();
                    break;
                case CTRL_PlayerPlatformer.playerControlState.Sling:
                    _touchManager.ExecuteSling(Direction);
                    break;
                case CTRL_PlayerPlatformer.playerControlState.TakeDamage:
                    break;
                case CTRL_PlayerPlatformer.playerControlState.Dead:
                    break;
                case CTRL_PlayerPlatformer.playerControlState.LevelComplete:
                    break;
            }
        }

        base.OnPointerUp(eventData);
    }

    protected override void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        if (joystickType == JoystickType.Dynamic && magnitude > moveThreshold)
        {
            Vector2 difference = normalised * (magnitude - moveThreshold) * radius;
            background.anchoredPosition += difference;
        }
        base.HandleInput(magnitude, normalised, radius, cam);
    }
}

public enum JoystickType { Fixed, Floating, Dynamic }