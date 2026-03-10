using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MNGR_PlayerLife : MonoBehaviour
{

    [System.Serializable]
    public class ComponentReferences
    {
        public CTRL_PlayerPlatformer playerCtrl;
    }

    [System.Serializable]
    public class PlayerLifeVariables
    {
        public int maxLives;
        //[HideInInspector]
        public int numLives;
        public List<Image> lifeImages = new List<Image>();
    }

    [System.Serializable]
    public class PlayerRespawnVariables
    {
        public float respawnPreDelayTimer;
        public float respawnPostDelayTimer;
        public float respawnTweenTimer;
        public AnimationCurve respawnTweenCurve;
    }

    [System.Serializable]
    public class PlayerTakeDamageVariables
    {
        public float damageTimer;
        public float invulnerableTimer;
        public float knockBackForce;
        public Vector2 damageKnockback;
        public AnimationCurve knockbackOffsetCurve;
        public bool invulnerable = false;
    }

    [Header("Player Life Variables")]
    public ComponentReferences componentRefs;

    [Header("Player Life Variables")]
    public PlayerLifeVariables playerLifeVars;

    [Header("Player Take Damage Variables")]
    public PlayerTakeDamageVariables playerDmgVariables;

    [Header("Player Respawn Variables")]
    public PlayerRespawnVariables playerRespawnVars;


    // Start is called before the first frame update
    void Start()
    {
        resetPlayerLives();
        updatePlayerLivesHUD();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void resetPlayerLives()
    {
        playerLifeVars.numLives = playerLifeVars.maxLives;
    }

    public void updatePlayerLivesHUD()
    {
        for (int i = 0; i < playerLifeVars.lifeImages.Count; i++)
        {
            if ((i+1) <= playerLifeVars.numLives)
            {
                playerLifeVars.lifeImages[i].enabled = true;
            }
            else
            {
                playerLifeVars.lifeImages[i].enabled = false;
            }
        }
    }

    public void playerLoseLife()
    {
        playerLifeVars.numLives -= 1;

        if (playerLifeVars.numLives < 1)
        {
            //resetPlayerLives();
            componentRefs.playerCtrl.setStateDead();
        }
        else
        {
            StartCoroutine(setInvul_TakeDamageCO());
            componentRefs.playerCtrl.setStateTakeDamage();
        }

        updatePlayerLivesHUD();
    }

    private IEnumerator playerRespawnCO(Vector3 targetPosition)
    {
        float t = 0.0f;

        Vector3 startingPosition = this.transform.position;

        while (t < playerRespawnVars.respawnTweenTimer)
        {
            t += Time.deltaTime;

            this.transform.position = Vector3.Lerp(startingPosition, targetPosition, playerRespawnVars.respawnTweenCurve.Evaluate(t/playerRespawnVars.respawnTweenTimer));

            yield return null;
        }

        this.transform.position = targetPosition;

        yield return null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Enemy" && !playerDmgVariables.invulnerable && componentRefs.playerCtrl.playerState != CTRL_PlayerPlatformer.playerControlState.Dead)
        {
            playerLoseLife();
        }

        if (collision.gameObject.tag == "DamagePlayer" && !playerDmgVariables.invulnerable && componentRefs.playerCtrl.playerState != CTRL_PlayerPlatformer.playerControlState.Dead)
        {
            playerLoseLife();
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "DamagePlayer" && !playerDmgVariables.invulnerable && componentRefs.playerCtrl.playerState != CTRL_PlayerPlatformer.playerControlState.Dead)
        {
            playerLoseLife();
        }
        
        if (collision.gameObject.tag == "LevelComplete")
        {
            componentRefs.playerCtrl.setStateLevelComplete();
        }
    }

    private IEnumerator setInvul_TakeDamageCO()
    {
        playerDmgVariables.invulnerable = true;

        float t = 0;
        while (t < playerDmgVariables.invulnerableTimer)
        {
            t += Time.deltaTime;
            
            yield return null;
        }
        playerDmgVariables.invulnerable = false;
        yield return null;
    }

    // Gizmo Stuff
    private void OnDrawGizmosSelected()
    {
        Debug.DrawLine(this.transform.position, this.transform.position+(Vector3)playerDmgVariables.damageKnockback);
    }

}
