using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimEvents : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private CTRL_PlayerPlatformer playerCtrl;

    private void FinishLedgeClimb()
    {
        playerCtrl.FinishLedgeClimb();
    }
}
