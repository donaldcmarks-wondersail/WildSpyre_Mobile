using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class CTRL_PlayerPlatformer : MonoBehaviour
{
    [Header("GameObjects/Components")]
    public Rigidbody2D rb;
    public Animator animatorChar;
    public CapsuleCollider2D playerCollider;
    private MNGR_PlayerLife playerLifeManager;
    public SpriteRenderer playerSpriteRend;
    public Animator animatorUI;
    private MNGR_LevelManager levelManager;
    [HideInInspector] public bool _isSlinging = false;
    private Coroutine _damagerReactionCoroutine;
    
    [Header("Global Class Variables")]
    public bool flipped = false;

    [System.Serializable]
    public class SpriteProtoVisuals
    {
        public Sprite platformerSprite;
        public Sprite slingSprite;
    }

    [System.Serializable]
    public class TrailVariables
    {
        public TrailRenderer slingTrailRenderer;
        public float slingTrailLength = 0.15f;
        public LineRenderer flameHeadLineRend;
    }

    [System.Serializable]
    public class InputVariables
    {
        public bool getKeyboardInput = false;
        public bool lockInput = false;
    }

    [System.Serializable]
    public class ParticleFXVariables
    {
        public ParticleSystem switchToPlatParticles;
        public ParticleSystem slingFireParticles;
        public ParticleSystem skidParticles;
        public ParticleSystem skidJumpParticles;
        [HideInInspector] public ParticleSystem skidParticlesRef;
    }

    [System.Serializable]
    public class MovementVariables
    {
        public float moveSpeedGrounded;
        public float moveSpeedInAir;
        [Range(0.0f, 1.0f)]
        public float moveDampingGroundedBasic;
        [Range(0.0f, 1.0f)]
        public float moveDampingGroundedStop;
        [Range(0.0f, 1.0f)]
        public float moveDampingGroundedTurning;
        [Range(0.0f, 1.0f)]
        public float moveDampingInAirBasic;
        public bool isMoveRight;
        public bool isMoveLeft;
        public Vector3 playerMoveVector;
        [HideInInspector]
        public Vector3 calcVelocity;
        [HideInInspector]
        public Vector3 previousPos;
    }

    [System.Serializable]
    public class JumpVariables
    {
        public float jumpForce;
        public float jumpBuffer;
        public float jumpBufferTimer;
        public float coyoteTime;
        public float coyoteTimer;
        public bool jumpPressed = false;
        public bool isOnGround;
        public float minJumpTime = 0.15f;
        public float jumpPressedTime;
        public float wallJumpedTime;
        public float wallJumpNoMoveTime = 0.5f;
        public float negativeYVelTime;
        public bool downwardForceReset;
        public float maxGravityScale = 4.0f;
        public float maxNegativeYTime = 0.5f;
        public float maxFallVelocity;
        public float maxFallWallHangVelocity;
        [HideInInspector] public float startingGravityScale;
    }

    [System.Serializable]
    public class SkidVariables
    {
        public float minRunSkidTime = 0.5f;
        public float runResetTime;
        public float skidTime = 0.5f;
        public AnimationCurve skidCurve;
        public Coroutine skidCoroutine;
        public bool isSkidding = false;
    }
    
    [System.Serializable]
    public class GroundCheck
    {
        public LayerMask groundMask;
        public float groundCheckLength;
        public bool isGrounded;
        public float maxGroundedSlopeDot = 0.5f;
        public Vector2 wallCheckOffset;
        public bool isTouchingWall;
        public Vector2 ledgeCheckOffset;
        public float wallDetectDistance = 0.25f;
        public bool canClimbLedge = false;
        public bool isTouchingLedge;
        public bool ledgeDetected;
        public Vector2 ledgePos1;
        public Vector2 ledgePos2;
        public Vector2 ledgeClimbOffset1;
        public Vector2 ledgeClimbOffset2;
        public Vector2 ledgePosBot;
        public bool isWallHanging;
    }

    [System.Serializable]
    public class SlopeCheck
    {
        public float maxSlopeAngle;
        public float slopeCheckDistance;
        public float slopeDownAngle;
        public float slopeDownAngleOld;
        public Vector2 slopeNormalPerp;
        public Vector2 slopeCheckPos;
        public bool isOnSlope;
        public float slopeSideAngle;
    }

    [System.Serializable]
    public class SlingVariables
    {
        public float slingForce;
        public float slingForceMax;
        public float slingForceMin;
        public float slingSlowFactor;
        public float slingSlowTimer;
        public bool canSling;
        public Coroutine StartSlingCoroutine;
    }

    [System.Serializable]
    public class StateVariables
    {
        public Vector2 capColSize_Platform;
        public Vector2 capColSize_Sling;
        public Vector3 spriteScale_Platform;
        public Vector3 spriteScale_Sling;
    }

    [System.Serializable]
    public class PhysicsMaterials
    {
        public PhysicsMaterial2D PMAT_playerStatic;
        public PhysicsMaterial2D PMAT_playerMoving;
        public PhysicsMaterial2D PMAT_playerSling;
        public PhysicsMaterial2D PMAT_playerWallHang;
    }

    [Header("Sprite Prototype Visuals")]
    public SpriteProtoVisuals spriteProtoVis;

    [Header("Trail Prototype Visuals")]
    public TrailVariables trailVars;

    [Header("Player State Variables")]
    public playerControlState playerState = playerControlState.Platformer;

    [Header("Input Variables")]
    public InputVariables inputVars;

    [Header("Particle FX Variables")]
    public ParticleFXVariables particleFXVars;

    [Header("Movement Variables")]
    public MovementVariables moveVars;
    
    [Header("Jump Variables")]
    public JumpVariables jumpVars;

    [Header("Skid Variables")] 
    public SkidVariables skidVars;
    
    [Header("Ground Check")]
    public GroundCheck groundCheck;
    
    [Header("Slope Check")]
    public SlopeCheck slopeCheck;
    
    [Header("Sling Variables")]
    public SlingVariables slingVars;

    [Header("Sling Variables")]
    public StateVariables stateVars;

    [Header("Physics Materials")]
    public PhysicsMaterials physMats;

    // Start is called before the first frame update
    void Start()
    {
        jumpVars.jumpBufferTimer = jumpVars.jumpBuffer;
        jumpVars.coyoteTimer = jumpVars.coyoteTime;
        jumpVars.startingGravityScale = rb.gravityScale;
        stateVars.spriteScale_Platform = playerSpriteRend.gameObject.transform.localScale;
        stateVars.capColSize_Platform = playerCollider.size;
        playerLifeManager = this.GetComponent<MNGR_PlayerLife>();
        levelManager = GameObject.FindGameObjectWithTag("LevelManager").GetComponent<MNGR_LevelManager>();
    }

    // Update is called once per frame
    void Update()
    {
        evaluateJumpTimers();
        evaluateAnimator();
    }

    private void FixedUpdate()
    {
        switch (PlayerState)
        {
            case playerControlState.Platformer:
                CheckSurroundings();
                CheckLedgeClimb();
                checkSlope();
                if (inputVars.getKeyboardInput)
                {
                    getKeyboardInput();
                }
                getInput();
                evaluateVerticalVelocityTimer();
                applyMovement();      
                break;
            case playerControlState.Sling:
                evaluateFlipDuringSling();
                break;
            case playerControlState.TakeDamage:
                evaluateCOVelocity();
                break;
            case playerControlState.Dead:
                break;
            case playerControlState.LevelComplete:
                break;
            case playerControlState.OnLedge:
                CheckLedgeClimb();
                getInput();
                break;
        }
    }

    private void evaluateCOVelocity()
    {
        moveVars.calcVelocity = ((transform.position - moveVars.previousPos)) / Time.deltaTime;
        moveVars.previousPos = transform.position;
    }

    private void checkSlope()
    {
        slopeCheck.slopeCheckPos = transform.position - new Vector3(0.0f, (playerCollider.size.y/2.0f)); ;
        slopeCheckHorizontal();
        slopeCheckVertical();
    }

    private void slopeCheckHorizontal()
    {
        RaycastHit2D slopeHitFront = Physics2D.Raycast(slopeCheck.slopeCheckPos, transform.right, slopeCheck.slopeCheckDistance, groundCheck.groundMask);
        RaycastHit2D slopeHitBack = Physics2D.Raycast(slopeCheck.slopeCheckPos, -transform.right, slopeCheck.slopeCheckDistance, groundCheck.groundMask);

        Debug.DrawLine(slopeCheck.slopeCheckPos, slopeCheck.slopeCheckPos + Vector2.right * slopeCheck.slopeCheckDistance, Color.yellow);
        Debug.DrawLine(slopeCheck.slopeCheckPos, slopeCheck.slopeCheckPos + Vector2.left * slopeCheck.slopeCheckDistance, Color.yellow);

        if (slopeHitFront)
        {
            slopeCheck.slopeSideAngle = Vector2.Angle(slopeHitFront.normal, Vector2.up);

        }else if (slopeHitBack)
        {
            slopeCheck.slopeSideAngle = Vector2.Angle(slopeHitBack.normal, Vector2.up);
        }
        else
        {
            slopeCheck.slopeSideAngle = 0.0f;
        }

        if (Mathf.Abs(slopeCheck.slopeSideAngle) > 0.0f && Mathf.Abs(slopeCheck.slopeSideAngle) < 45.0f)
        {
            slopeCheck.isOnSlope = true;
        }
        else
        {
            slopeCheck.isOnSlope = false;
        }
        
    }

    private void slopeCheckVertical()
    {
        RaycastHit2D hit = Physics2D.Raycast(slopeCheck.slopeCheckPos, Vector2.down, slopeCheck.slopeCheckDistance, groundCheck.groundMask);
        Debug.DrawLine(slopeCheck.slopeCheckPos, slopeCheck.slopeCheckPos + Vector2.down * slopeCheck.slopeCheckDistance, Color.yellow);

        if (hit)
        {
            slopeCheck.slopeNormalPerp = Vector2.Perpendicular(hit.normal).normalized;
            slopeCheck.slopeDownAngle = Vector2.Angle(hit.normal, Vector2.up);

            if (slopeCheck.slopeDownAngle > 0.0f && slopeCheck.slopeDownAngle < slopeCheck.maxSlopeAngle)
            {
                slopeCheck.isOnSlope = true;
            }
            
            slopeCheck.slopeDownAngleOld = slopeCheck.slopeDownAngle;
            Debug.DrawRay(hit.point, hit.normal, Color.green);
            Debug.DrawRay(hit.point, slopeCheck.slopeNormalPerp, Color.red);
        }
    }

    private void evaluateVerticalVelocityTimer()
    {
        if (groundCheck.isGrounded || groundCheck.isWallHanging)
        {
            jumpVars.downwardForceReset = false;            
        }
        else
        {
            if (!jumpVars.downwardForceReset && rb.linearVelocity.y < 0.0f)
            {
                jumpVars.downwardForceReset = true;
                jumpVars.negativeYVelTime = Time.time;            
            }
        }
    }
    
    private void applyMovement()
    {
        float horizontalVelocity = rb.linearVelocity.x;

        if (jumpVars.downwardForceReset)
        {
            rb.gravityScale = Mathf.Lerp(jumpVars.startingGravityScale, jumpVars.maxGravityScale, (Time.time-jumpVars.negativeYVelTime)/ jumpVars.maxNegativeYTime);            
        }
        else
        {
            rb.gravityScale = jumpVars.startingGravityScale; 
        }

        
        if (Time.time >= jumpVars.wallJumpedTime + jumpVars.wallJumpNoMoveTime)
        {
            if (groundCheck.isGrounded && !slopeCheck.isOnSlope)
            {
                horizontalVelocity += moveVars.playerMoveVector.x;
                if (Mathf.Abs(moveVars.playerMoveVector.x) < 0.1f)
                {
                    horizontalVelocity *= Mathf.Pow(1f - moveVars.moveDampingGroundedStop, Time.deltaTime * 10f);
                }
                else
                {
                    if (Mathf.Sign(moveVars.playerMoveVector.x) != Mathf.Sign(horizontalVelocity))
                    {
                        horizontalVelocity *= Mathf.Pow(1f - moveVars.moveDampingGroundedTurning, Time.deltaTime * 10f);
                    }else{
                        horizontalVelocity *= Mathf.Pow(1f - moveVars.moveDampingGroundedBasic, Time.deltaTime * 10f);
                    }
                }

                rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);

            }
            else if(groundCheck.isGrounded && slopeCheck.isOnSlope && slopeCheck.slopeDownAngle <= slopeCheck.maxSlopeAngle)
            {
               float verticalVelocity = rb.linearVelocity.y;

               horizontalVelocity += -moveVars.playerMoveVector.x * slopeCheck.slopeNormalPerp.x;
               verticalVelocity += -moveVars.playerMoveVector.x * slopeCheck.slopeNormalPerp.y;
                
                if (Mathf.Abs(moveVars.playerMoveVector.x) < 0.1f)
                {
                    horizontalVelocity *= Mathf.Pow(1f - moveVars.moveDampingGroundedStop, Time.deltaTime * 10f);
                    verticalVelocity *= Mathf.Pow(1f - moveVars.moveDampingGroundedStop, Time.deltaTime * 10f);
                }
                else
                {
                    if (Mathf.Sign(moveVars.playerMoveVector.x) != Mathf.Sign(horizontalVelocity))
                    {
                        horizontalVelocity *= Mathf.Pow(1f - moveVars.moveDampingGroundedTurning, Time.deltaTime * 10f);
                        verticalVelocity *= Mathf.Pow(1f - moveVars.moveDampingGroundedTurning, Time.deltaTime * 10f);
                    }
                    else
                    {
                        horizontalVelocity *= Mathf.Pow(1f - moveVars.moveDampingGroundedBasic, Time.deltaTime * 10f);
                        verticalVelocity *= Mathf.Pow(1f - moveVars.moveDampingGroundedBasic, Time.deltaTime * 10f);
                    }
                }
                
                rb.linearVelocity = new Vector2(horizontalVelocity, verticalVelocity);

            }
            else if(groundCheck.isGrounded == false)
            {
                horizontalVelocity += moveVars.playerMoveVector.x;
                horizontalVelocity *= Mathf.Pow(1f - moveVars.moveDampingInAirBasic, Time.deltaTime * 10f);

                float verticalVelocity = rb.linearVelocity.y;

                if (groundCheck.isWallHanging)
                {
                    if (rb.linearVelocity.y < jumpVars.maxFallWallHangVelocity)
                    {
                        verticalVelocity = jumpVars.maxFallWallHangVelocity;
                    }
                }
                else
                {
                    if (rb.linearVelocity.y < jumpVars.maxFallVelocity)
                    {
                        verticalVelocity = jumpVars.maxFallVelocity;
                    }
                }

                rb.linearVelocity = new Vector2(horizontalVelocity, verticalVelocity);
                
            }            
        }

        if (!skidVars.isSkidding)
        {
            if (horizontalVelocity > 1.0f)
            {
                Flipped = false;
            }
            else
            {
                if (horizontalVelocity < -1.0f)
                {
                    Flipped = true;
                }
            }
        }
        
        if (jumpVars.jumpBufferTimer > 0.0f) 
        {
            if (groundCheck.isWallHanging || jumpVars.coyoteTimer > 0.0f)
            {
                if (Time.time > (jumpVars.jumpPressedTime + jumpVars.minJumpTime))
                {
                    if (jumpVars.jumpPressed)
                    {
                        if (groundCheck.isWallHanging)
                        {
                            executeJump(-playerSpriteRend.transform.localScale.x * jumpVars.jumpForce*0.45f,jumpVars.jumpForce*0.9f, groundCheck.isWallHanging);
                        }
                        else
                        {
                            executeJump(horizontalVelocity,jumpVars.jumpForce, groundCheck.isWallHanging);                        
                        }
                    }
                    else
                    {
                        if (groundCheck.isWallHanging)
                        {
                            executeJump(-playerSpriteRend.transform.localScale.x * jumpVars.jumpForce*0.45f,jumpVars.jumpForce *0.3f, groundCheck.isWallHanging);
                        }
                        else
                        {
                            executeJump(horizontalVelocity,jumpVars.jumpForce *0.4f, groundCheck.isWallHanging);
                        }
                    }
                }
            }
        }
    }

    private void executeJump(float horVel,float jumpVelocity, bool isWallJump)
    {
        jumpVars.jumpBufferTimer = 0.0f;
        jumpVars.coyoteTimer = 0.0f;
        jumpVars.jumpPressedTime = Time.time;
        if (skidVars.isSkidding)
        {
            stopSkid();
            if (particleFXVars.skidParticlesRef != null)
            {
                Destroy(particleFXVars.skidParticlesRef.gameObject);
            }
            Instantiate(particleFXVars.skidJumpParticles, playerSpriteRend.transform.position, Quaternion.identity);
            rb.linearVelocity = new Vector2(-horVel, jumpVelocity*1.275f);     
            animatorChar.SetTrigger("SkidJump");
        }
        else
        {
            if (isWallJump)
            {
                jumpVars.wallJumpedTime = Time.time;
                groundCheck.isWallHanging = false;
                animatorChar.SetTrigger("WallJump");
            }
            else
            {
                animatorChar.SetTrigger("Jump");
            }
            rb.linearVelocity = new Vector2(horVel, jumpVelocity);
        }


        if (PlayerState == playerControlState.Platformer)
        {
            animatorChar.SetTrigger("Jump");
        }
    }
    
    private void evaluateFlipDuringSling()
    {
        if (!skidVars.isSkidding)
        {
            if (rb.linearVelocity.x >= 0.0f)
            {
                Flipped = false;
            }
            else
            {
                Flipped = true;
            }
        }
    }
    
    public void evaluateJumpTimers()
    {
        jumpVars.jumpBufferTimer -= Time.deltaTime;
        jumpVars.coyoteTimer -= Time.deltaTime;
        jumpVars.isOnGround = groundCheck.isGrounded;

        if (jumpVars.isOnGround)
        {
            jumpVars.coyoteTimer = jumpVars.coyoteTime;
        }
    }

    public void getInput()
    {
        if (!inputVars.lockInput || !skidVars.isSkidding)
        {
            switch(PlayerState){
                case playerControlState.Platformer:
                    getPlatformInput();
                    break;
                case playerControlState.Sling:
                    break;
                case playerControlState.OnLedge:
                    getPlatformInput();
                    break;
            }
        }
    }

    public void getPlatformInput()
    {
        if (groundCheck.isGrounded)
        {
            if (moveVars.isMoveRight)
            {
                moveVars.playerMoveVector = Vector3.right * moveVars.moveSpeedGrounded;
                playerCollider.sharedMaterial = physMats.PMAT_playerMoving;
            }
            else
            {
                if (moveVars.isMoveLeft)
                {
                    moveVars.playerMoveVector = Vector3.left * moveVars.moveSpeedGrounded;
                    playerCollider.sharedMaterial = physMats.PMAT_playerMoving;
                }
                else
                {
                    skidVars.runResetTime = Time.time;
                    moveVars.playerMoveVector = Vector2.zero;
                    if (slopeCheck.isOnSlope)
                    {
                        playerCollider.sharedMaterial = physMats.PMAT_playerStatic;
                    }
                }
            }
            evaluateSkid();
        }
        else
        {
            if (moveVars.isMoveRight)
            {
                moveVars.playerMoveVector = Vector3.right * moveVars.moveSpeedInAir;
                playerCollider.sharedMaterial = physMats.PMAT_playerMoving;
            }
            else
            {
                if (moveVars.isMoveLeft)
                {
                    moveVars.playerMoveVector = Vector3.left * moveVars.moveSpeedInAir;
                    playerCollider.sharedMaterial = physMats.PMAT_playerMoving;
                }
                else
                {
                    moveVars.playerMoveVector = Vector2.zero;
                    if (slopeCheck.isOnSlope)
                    {
                        playerCollider.sharedMaterial = physMats.PMAT_playerStatic;
                    }
                }
            }
        }
    }

    private void evaluateSkid()
    {
        animatorChar.SetFloat("RunReset", Time.time - skidVars.runResetTime);
        // Debug.Log("Move vs RB Vel Dot = " + Vector3.Dot(rb.velocity.normalized, moveVars.playerMoveVector.normalized));
        if (Vector3.Dot(rb.linearVelocity.normalized, moveVars.playerMoveVector.normalized) < 0.0f)
        {
            if (Time.time > (skidVars.runResetTime+skidVars.minRunSkidTime) && !skidVars.isSkidding)
            {
                particleFXVars.skidParticlesRef = Instantiate(particleFXVars.skidParticles, playerSpriteRend.transform.position, Quaternion.identity);
                particleFXVars.skidParticlesRef.transform.parent = playerSpriteRend.transform;
                //newSkidParticles.transform.localScale = playerSpriteRend.transform.localScale;
                skidVars.skidCoroutine = StartCoroutine(ExecuteSkidCO());
            }
            else
            {
                skidVars.runResetTime = Time.time;
            }
        }
    }

    private IEnumerator ExecuteSkidCO()
    {
        skidVars.isSkidding = true;
        float skidStartTime = Time.time;
        animatorChar.SetBool("isSkidding", true);
        float startXVel = rb.linearVelocity.x;
        while (Time.time < (skidStartTime + skidVars.skidTime))
        {
            rb.linearVelocity = new Vector2(Mathf.Lerp(startXVel, 0.0f, skidVars.skidCurve.Evaluate((Time.time - skidStartTime) / skidVars.skidTime)), rb.linearVelocity.y);
            yield return null;
        }
        rb.linearVelocity = new Vector2(0.0f, rb.linearVelocity.y);
        animatorChar.SetBool("isSkidding", false);
        skidVars.isSkidding = false;
        skidVars.runResetTime = Time.time;
        yield return null;
    }
    
    public void jump()
    {
        jumpVars.jumpBufferTimer = jumpVars.jumpBuffer;
        jumpVars.jumpPressed = true;
    }

    public void jumpRelease()
    {
        _isSlinging = false;
        if (rb.linearVelocity.y > 0.0f)
        {
            Vector2 newVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y*0.4f);
            rb.linearVelocity = newVelocity;
        }
        jumpVars.jumpPressed = false;
    }

    public void setMoveRightBoolTrue()
    {
        if (PlayerState == playerControlState.Platformer || PlayerState == playerControlState.OnLedge)
        { 
            moveVars.isMoveRight = true;
        }
        else
        {
            moveVars.isMoveRight = false;
        }
    }

    public void setMoveLeftBoolTrue()
    {
        if (PlayerState == playerControlState.Platformer || PlayerState == playerControlState.OnLedge)
        {
            moveVars.isMoveLeft = true;
        }
        else
        {
            moveVars.isMoveLeft = false;
        }
    }

    public void setMoveRightBoolFalse()
    {
        moveVars.isMoveRight = false;
    }

    public void setMoveLeftBoolFalse()
    {
        moveVars.isMoveLeft = false;
    }
    
    public IEnumerator startSlingSlowMoCO()
    {
        _isSlinging = true;
        float t = 0.0f;

        while (t < slingVars.slingSlowTimer)
        {
            t += Time.deltaTime;

            Time.timeScale = Mathf.Lerp(1.0f, slingVars.slingSlowFactor, t/slingVars.slingSlowTimer);

            yield return null;
        }

        Time.timeScale = slingVars.slingSlowFactor;

        yield return null;
    }

    public void resetTimescale()
    {
        Time.timeScale = 1.0f;
    }

    public void resetPlayerPhysics()
    {
        moveVars.playerMoveVector = Vector2.zero;
        jumpVars.isOnGround = false;
        rb.linearVelocity = Vector2.zero;
    }

    private void CheckSurroundings()
    {
        if ((Time.time) >= (jumpVars.jumpPressedTime+jumpVars.minJumpTime))
        {
            RaycastHit2D groundHit = Physics2D.BoxCast(playerCollider.bounds.center, new Vector2(playerCollider.bounds.size.x*0.65f, playerCollider.bounds.size.y), 0.0f, Vector2.down, groundCheck.groundCheckLength, groundCheck.groundMask);

            // Debug.Log("groundhit.normal = "+groundHit.normal);
            // Debug.Log("Ground Dot Product = " + Vector3.Dot(groundHit.normal, Vector3.up));
            Debug.DrawRay(this.transform.position, groundHit.normal);
            Debug.DrawRay(this.transform.position, Vector3.up);
            DrawGroundedBox(groundHit);   
            
            if (Vector3.Dot(groundHit.normal, Vector3.up) > groundCheck.maxGroundedSlopeDot)
            {
                if (!groundCheck.isGrounded)
                {
                    skidVars.runResetTime = Time.time;
                }
                groundCheck.isGrounded = groundHit;
            }
            else
            {
                groundCheck.isGrounded = false;
                if (skidVars.isSkidding)
                {
                    stopSkid();                    
                }

            }
        }
        else
        {
            groundCheck.isGrounded = false;
            if (skidVars.isSkidding)
            {
                stopSkid();                    
            }
        }

        Vector2 wallRayPosA = (transform.position + (Vector3)groundCheck.wallCheckOffset);
        Vector2 ledgeRayPosA = (transform.position + (Vector3)groundCheck.ledgeCheckOffset);

        DetectWallsLedges(wallRayPosA, ledgeRayPosA);
    }

    private void DetectWallsLedges(Vector2 wallPosA, Vector2 ledgePosA)
    {
        Vector2 wallDir;
        Vector2 wallPosB;
        Vector2 ledgePosB;
        
        if (!Flipped)
        {
            wallDir = Vector2.right;
        }
        else
        {
            wallDir = Vector2.left;
        }

        wallPosB = new Vector2(wallDir.x * groundCheck.wallDetectDistance, 0.0f);
        ledgePosB = new Vector2(wallDir.x * groundCheck.wallDetectDistance, 0.0f);
        RaycastHit2D wallHit = Physics2D.Raycast(wallPosA, wallDir, groundCheck.wallDetectDistance,groundCheck.groundMask);
        RaycastHit2D ledgeHit = Physics2D.Raycast(ledgePosA, wallDir, groundCheck.wallDetectDistance,groundCheck.groundMask);
        
        // Debug.Log("wallhit.Normal = " + wallHit.normal);
        // Debug.Log("wallhit.DotProduct = " + Vector2.Dot(wallHit.normal, wallDir));
        
        if (Vector2.Dot(wallHit.normal, wallDir) != -1.0f)
        {
            groundCheck.isTouchingWall = false;
        }
        else
        {
            groundCheck.isTouchingWall = wallHit;
        }
        
        groundCheck.isTouchingLedge = ledgeHit;
        DrawRay(wallPosA, wallPosB, wallHit);
        DrawRay(ledgePosA, ledgePosB, ledgeHit);

        if (groundCheck.isTouchingWall && groundCheck.isTouchingLedge && Time.time >= jumpVars.wallJumpedTime+jumpVars.minJumpTime && !groundCheck.isGrounded)
        {
            ExecuteWallHang();
        }
        else
        {
            animatorChar.SetBool("pressingWall", false);
            groundCheck.isWallHanging = false;
        }
        
        if (groundCheck.isTouchingWall && !groundCheck.isTouchingLedge && !groundCheck.ledgeDetected)
        {
            groundCheck.ledgeDetected = true;
            groundCheck.ledgePosBot = wallPosA;
        }
    }

    private void ExecuteWallHang()
    {
        groundCheck.isWallHanging = true;
        animatorChar.SetBool("pressingWall", true);
        if (rb.linearVelocity.y > 0.0f)
        {
            rb.linearVelocity = new Vector2(0.0f, rb.linearVelocity.y * 0.9f);            
        }
        playerCollider.sharedMaterial = physMats.PMAT_playerWallHang;
    }
    
    private void CheckLedgeClimb()
    {
        if (groundCheck.ledgeDetected && !groundCheck.canClimbLedge)
        {
            groundCheck.canClimbLedge = true;

            if (!Flipped)
            {
                groundCheck.ledgePos1 = new Vector2(Mathf.Floor(groundCheck.ledgePosBot.x + groundCheck.wallDetectDistance)-groundCheck.ledgeClimbOffset1.x, Mathf.Floor(groundCheck.ledgePosBot.y) + groundCheck.ledgeClimbOffset1.y);
                groundCheck.ledgePos2 = new Vector2(Mathf.Floor(groundCheck.ledgePosBot.x + groundCheck.wallDetectDistance)+groundCheck.ledgeClimbOffset2.x, Mathf.Floor(groundCheck.ledgePosBot.y) + groundCheck.ledgeClimbOffset2.y);                   
            }
            else
            {
                groundCheck.ledgePos1 = new Vector2(Mathf.Ceil(groundCheck.ledgePosBot.x - groundCheck.wallDetectDistance)+groundCheck.ledgeClimbOffset1.x, Mathf.Floor(groundCheck.ledgePosBot.y) + groundCheck.ledgeClimbOffset1.y);
                groundCheck.ledgePos2 = new Vector2(Mathf.Ceil(groundCheck.ledgePosBot.x - groundCheck.wallDetectDistance)-groundCheck.ledgeClimbOffset2.x, Mathf.Floor(groundCheck.ledgePosBot.y) + groundCheck.ledgeClimbOffset2.y);   
            }

            setStateOnLedge();
            
            animatorChar.SetBool("canClimbLedge", groundCheck.canClimbLedge);
        }

        if (groundCheck.canClimbLedge)
        {
            transform.position = groundCheck.ledgePos1; 
        }
    }
    
    public void FinishLedgeClimb()
    {
        groundCheck.canClimbLedge = false;
        transform.position = groundCheck.ledgePos2;
        setStatePlatform();
        groundCheck.ledgeDetected = false;
        skidVars.runResetTime = Time.time;
        animatorChar.SetBool("canClimbLedge", groundCheck.canClimbLedge);
    }
    
    private void DrawRay(Vector2 posA, Vector2 dir ,bool hit)
    {
        Color rayColor;
        if (hit)
        {
            rayColor = Color.green;

        }
        else
        {
            rayColor = Color.red;
        }
        
        Debug.DrawRay(posA, dir, rayColor);
    }
    
    private void DrawGroundedBox(bool hit)
    {
        Color rayColor;

        if (hit)
        {
            rayColor = Color.green;

        }
        else
        {
            rayColor = Color.red;
        }

        Debug.DrawRay(playerCollider.bounds.center + new Vector3(playerCollider.bounds.extents.x, 0.0f), Vector2.down *(playerCollider.bounds.extents.y + groundCheck.groundCheckLength), rayColor);
        Debug.DrawRay(playerCollider.bounds.center - new Vector3(playerCollider.bounds.extents.x, 0.0f), Vector2.down * (playerCollider.bounds.extents.y + groundCheck.groundCheckLength), rayColor);
        Debug.DrawRay(playerCollider.bounds.center - new Vector3(playerCollider.bounds.extents.x, playerCollider.bounds.extents.y+ groundCheck.groundCheckLength), Vector2.right * (playerCollider.bounds.extents.x*2), rayColor);
    }
    
    public void getKeyboardInput()
    {
        if (Input.GetAxis("Horizontal") == 0.0f)
        {
            moveVars.isMoveRight = false;
            moveVars.isMoveLeft = false;
        }
        else
        {
            if (Input.GetAxis("Horizontal") > 0.0f)
            {
                moveVars.isMoveRight = true;
            }

            if (Input.GetAxis("Horizontal") < 0.0f)
            {
                moveVars.isMoveLeft = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jump();
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            jumpRelease();
        }

    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isInLayerMask(collision.gameObject, groundCheck.groundMask))
        {
            slingVars.canSling = true;
        }
    }

    private void OnDrawGizmos()
    {
        
    }

    public void setStatePlatform()
    {
        rb.isKinematic = false;
        rb.gravityScale = 2.0f;
        if (PlayerState == playerControlState.Sling)
        {
            Instantiate(particleFXVars.switchToPlatParticles, this.gameObject.transform);            
        }
        PlayerState = playerControlState.Platformer;
        playerSpriteRend.sprite = spriteProtoVis.platformerSprite;
        if (!Flipped)
        {
            playerSpriteRend.gameObject.transform.localScale = stateVars.spriteScale_Platform;            
        }
        else
        {
            playerSpriteRend.gameObject.transform.localScale = new Vector3(-stateVars.spriteScale_Platform.x, stateVars.spriteScale_Platform.y, 1.0f);      
        }

        playerCollider.size = stateVars.capColSize_Platform;
        playerCollider.sharedMaterial = physMats.PMAT_playerStatic;
        resetPlayerPhysics();
        trailVars.slingTrailRenderer.enabled = false;
    }

    public void setStateSling()
    {
        stopSkid();
        rb.isKinematic = false;
        jumpVars.downwardForceReset = false;
        rb.gravityScale = 2.0f;
        animatorChar.SetBool("pressingWall", false);
        PlayerState = playerControlState.Sling;
        slingVars.StartSlingCoroutine = StartCoroutine("startSlingSlowMoCO");
        playerSpriteRend.sprite = spriteProtoVis.slingSprite;
        playerSpriteRend.gameObject.transform.localScale = stateVars.spriteScale_Sling;
        playerCollider.size = stateVars.capColSize_Sling;
        playerCollider.sharedMaterial = physMats.PMAT_playerSling;
        trailVars.slingTrailRenderer.enabled = true;
    }

    public void setStateOnLedge()
    {
        PlayerState = playerControlState.OnLedge;
        rb.gravityScale = 0.0f;
        rb.isKinematic = true;
    }
    
    public void setStateTakeDamage(Vector2 hitSourcePosition)
    {
        if (_damagerReactionCoroutine != null)
        {
            StopCoroutine(_damagerReactionCoroutine);
        }
        rb.gravityScale = 2.0f;
        Instantiate(particleFXVars.switchToPlatParticles, this.gameObject.transform);
        playerSpriteRend.sprite = spriteProtoVis.platformerSprite;
        playerSpriteRend.gameObject.transform.localScale = stateVars.spriteScale_Platform;
        playerCollider.size = stateVars.capColSize_Platform;
        playerCollider.sharedMaterial = physMats.PMAT_playerStatic;
        resetPlayerPhysics();
        PlayerState = playerControlState.TakeDamage;
        trailVars.slingTrailRenderer.enabled = false;
        if (skidVars.isSkidding)
        {
            stopSkid();
        }

        // Which side the hit came from — knockback and the hit-reaction facing both key off
        // this, not off whichever way the player happened to be facing when they got hit.
        bool attackerOnLeft = hitSourcePosition.x < ((Vector2)transform.position).x;

        StartCoroutine(TakeDamageCO(playerLifeManager.playerDmgVariables.damageTimer, playerLifeManager.playerDmgVariables.damageKnockback, attackerOnLeft));
    }

    private void stopSkid()
    {
        animatorChar.SetBool("isSkidding", false);
        skidVars.isSkidding = false;
        if (skidVars.skidCoroutine != null)
        {
            StopCoroutine(skidVars.skidCoroutine);            
        }
    }
    
    public void setStateDead()
    {
        resetPlayerPhysics();
        rb.isKinematic = true;
        rb.gravityScale = 2.0f;
        playerCollider.enabled = false;
        Instantiate(particleFXVars.switchToPlatParticles, this.gameObject.transform);
        trailVars.flameHeadLineRend.gameObject.SetActive(false);
        playerSpriteRend.enabled = false;
        trailVars.slingTrailRenderer.enabled = false;
        PlayerState = playerControlState.Dead;
        animatorUI.SetTrigger("Death");
    }

    public void setStateLevelComplete()
    {
        resetPlayerPhysics();
        rb.isKinematic = true;
        rb.gravityScale = 2.0f;
        playerCollider.enabled = false;
        trailVars.slingTrailRenderer.enabled = false;
        PlayerState = playerControlState.LevelComplete;
        levelManager.timerVars.timersActive = false;
        animatorUI.SetTrigger("LevelCompleted");
    }

    public void evaluateAnimator()
    {
        if (PlayerState == playerControlState.Platformer)
        {
            animatorChar.SetBool("isGrounded", jumpVars.isOnGround);   
        }
        animatorChar.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
    }

    public void GiveDamageReaction(Vector2 direction, float blockInputTime, float reactForce)
    {
        _damagerReactionCoroutine = StartCoroutine(GiveDamageReactionCO(direction, blockInputTime, reactForce));
    }
    
    public IEnumerator GiveDamageReactionCO(Vector2 direction, float reactionTime, float reactForce)
    {
        resetPlayerPhysics();
        float startTime = Time.time;
        rb.isKinematic = false;
        inputVars.lockInput = true;
        
        rb.AddForce(direction.normalized*reactForce, ForceMode2D.Impulse );
        
        while (Time.time < (startTime + reactionTime))
        {
            yield return null;
        }
        
        inputVars.lockInput = false;
        yield return null;
    }
    
    public IEnumerator GiveDamageReactionCOB(Vector2 direction, float reactionTime)
    {
        inputVars.lockInput = true;
        float t = 0f;
        Vector2 calcPos = Vector2.zero;
        
        Vector2 startingPos = this.transform.position;
        Vector2 targetPos = this.transform.position + (Vector3)direction;
        
        while (t < reactionTime)
        {
            this.transform.position = Vector3.Lerp(startingPos, targetPos, t / reactionTime);
            
            t += Time.fixedDeltaTime;
            yield return null;
        }

        this.transform.position = targetPos;
        inputVars.lockInput = false;
        yield return null;
    }
    
    public IEnumerator TakeDamageCO(float damageTimer, Vector2 damageKnockback, bool attackerOnLeft)
    {
        StartCoroutine(TakeDmgInvulnerableTimerCO());
        Vector2 calcPos = Vector2.zero;

        // Face toward whichever side the attack came from — holds until the player moves
        // again and normal movement-driven facing takes back over.
        Flipped = attackerOnLeft;

        if (attackerOnLeft)
        {
            damageKnockback.x = -damageKnockback.x;
        }

        Vector2 startingPos = this.transform.position;
        Vector2 targetPos = this.transform.position + (Vector3)damageKnockback;
        float duration = Mathf.Max(damageTimer, 0.0001f);
        float startTime = Time.time;

        while (true)
        {
            // Elapsed-time based, not an accumulated per-frame increment — frame-rate
            // independent (no drift, no assumption about how often this loop iterates)
            // instead of the old t += Time.fixedDeltaTime inside an Update-cadence loop,
            // which silently ran in slow motion on any device below ~50fps.
            float t = Mathf.Clamp01((Time.time - startTime) / duration);

            calcPos.x = Mathf.Lerp(startingPos.x, targetPos.x, t);
            calcPos.y = Mathf.Lerp(startingPos.y, targetPos.y, t) + playerLifeManager.playerDmgVariables.knockbackOffsetCurve.Evaluate(t);
            rb.MovePosition(calcPos);

            // Re-assert every frame so the flip holds for the whole reaction regardless of
            // what else runs that frame.
            playerSpriteRend.transform.localScale = attackerOnLeft ? new Vector3(-1f, 1f, 1f) : Vector3.one;

            if (t >= 1f) break;
            yield return null;
        }

        rb.MovePosition(calcPos);
        //resetPlayerPhysics();
        //rb.AddForce(moveVars.calcVelocity);

        PlayerState = playerControlState.Platformer;

        skidVars.runResetTime = Time.time;
        yield return null;
    }

    public IEnumerator TakeDmgInvulnerableTimerCO()
    {
        playerLifeManager.playerDmgVariables.invulnerable = true;
        yield return new WaitForSeconds(playerLifeManager.playerDmgVariables.invulnerableTimer);
        playerLifeManager.playerDmgVariables.invulnerable = false;
        yield return null;
    }
    
    public bool isInLayerMask(GameObject obj, LayerMask layerMask)
    {
        return ((layerMask.value & (1 << obj.layer)) > 0);
    }

    public enum playerControlState
    {
        Platformer,
        Sling,
        TakeDamage,
        Dead,
        LevelComplete,
        OnLedge,
        OnWall,
    }

    public bool Flipped
    {
        get
        {
            return flipped;
        }

        set
        {
            flipped = value;

            if (PlayerState == playerControlState.OnLedge)
            {

            }else{
                if (flipped)
                {
                    playerSpriteRend.transform.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
                }
                else
                {
                    playerSpriteRend.transform.localScale = Vector3.one;
                }
            }
        }
    }

    public playerControlState PlayerState
    {
        get
        {
            return playerState;
        }

        set
        {
            playerState = value;
            switch (playerState)
            {
                case playerControlState.Platformer:
                    animatorChar.SetBool("isBall", false);
                    break;
                case playerControlState.Sling:
                    animatorChar.SetBool("isBall", true);
                    break;
                case playerControlState.TakeDamage:
                    animatorChar.SetBool("isBall", false);
                    animatorChar.SetTrigger("TakeDamage");
                    break;
                case playerControlState.Dead:
                    break;
                case playerControlState.LevelComplete:
                    break;
            }
        }
    }
}
