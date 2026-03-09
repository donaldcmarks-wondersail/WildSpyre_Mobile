using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_Waypoint : MonoBehaviour
{
    [System.Serializable]
    public class WaypointVisualVariables
    {
        public SpriteRenderer waypointBaseSpriteRend;
        public SpriteRenderer waypointTipSpriteRend;
        public ParticleSystem waypointParticles;
        public Color waypointBaseColorInactive;
        public Color waypointBaseColorActive;
        public Color waypointTipColorInactive;
        public Color waypointTipColorActive;
    }

    [System.Serializable]
    public class WaypointSystemVariables
    {
        public MNGR_LevelManager levelManager;
        public int thisWPIndex;
        public bool isActive;
    }
    
    [Header("Waypoint Vis Variables")]
    public WaypointVisualVariables waypointVisVars;

    [Header("Waypoint Vis Variables")]
    public WaypointSystemVariables waypointSystemVars;

    // Start is called before the first frame update
    void Start()
    {
        waypointSystemVars.levelManager = GameObject.Find("MNGR_LevelManager").GetComponent<MNGR_LevelManager>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void setWPActive()
    {
        if (!waypointSystemVars.isActive)
        {
            waypointVisVars.waypointParticles.gameObject.SetActive(true);
            waypointVisVars.waypointBaseSpriteRend.color = waypointVisVars.waypointBaseColorActive;
            waypointVisVars.waypointTipSpriteRend.color = waypointVisVars.waypointTipColorActive;
            waypointSystemVars.isActive = true;
        }
    }
    
    public void setWPInactive()
    {
        if (waypointSystemVars.isActive)
        {
            waypointVisVars.waypointParticles.gameObject.SetActive(false);
            waypointVisVars.waypointBaseSpriteRend.color = waypointVisVars.waypointBaseColorInactive;
            waypointVisVars.waypointTipSpriteRend.color = waypointVisVars.waypointTipColorInactive;
            waypointSystemVars.isActive = false;
        }
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {

        }
    }

}
