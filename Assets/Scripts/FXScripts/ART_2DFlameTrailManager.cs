using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ART_2DFlameTrailManager : MonoBehaviour
{
    [Header("GameObjects and Components")] 
    [SerializeField] private List<ART_2DFlameTrail> _flameTrails = new List<ART_2DFlameTrail>();
    [SerializeField] private CTRL_PlayerPlatformer _playerCtrl;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
