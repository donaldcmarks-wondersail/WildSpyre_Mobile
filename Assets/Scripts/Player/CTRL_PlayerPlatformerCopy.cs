using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_PlayerPlatformerCopy : MonoBehaviour
{
    [Header("GameObjects/Components")]
    public Rigidbody2D rb;
    public CapsuleCollider2D playerCollider;

    [Header("Movement Variables")]
    public float moveSpeedGrounded;
    public float moveSpeedInAir;
    [Range(0.0f,1.0f)]
    public float moveDampingGroundedBasic;
    [Range(0.0f, 1.0f)]
    public float moveDampingGroundedStop;
    [Range(0.0f, 1.0f)]
    public float moveDampingGroundedTurning;
    [Range(0.0f, 1.0f)]
    public float moveDampingInAirBasic;
    private bool isMoveRight;
    private bool isMoveLeft;
    private Vector3 playerMoveVector;
    
    [Header("Jump Variables")]
    public float jumpForce;
    public float jumpBuffer;
    public float jumpBufferTimer;
    public float coyoteTime;
    public float coyoteTimer;
    public bool jumpPressed = false;
    
    [Header("Ground Check")]
    public LayerMask groundMask;
    public float groundCheckLength;

    [Header("Slope Check")]
    public float maxSlopeAngle;
    [SerializeField]
    private float slopeCheckDistance;
    private float slopeDownAngle;
    private Vector2 slopeNormalPerp;
    private Vector2 slopeCheckPos;
    [SerializeField]
    private bool isOnSlope;

    // Start is called before the first frame update
    void Start()
    {
        jumpBufferTimer = jumpBuffer;
        coyoteTimer = coyoteTime;
    }

    // Update is called once per frame
    void Update()
    {
        evaluateJumpTimers();
    }

    private void FixedUpdate()
    {
        slopeCheck();
        getInput();
        applyMovement();
    }

    private void slopeCheck()
    {
        slopeCheckPos = transform.position - new Vector3(0.0f, (playerCollider.size.y/2.0f)); ;
        slopeCheckHorizontal();
        slopeCheckVertical();
    }

    private void slopeCheckHorizontal()
    {

    }

    private void slopeCheckVertical()
    {
        RaycastHit2D hit = Physics2D.Raycast(slopeCheckPos, Vector2.down, slopeCheckDistance, groundMask);

        if (hit)
        {
            slopeNormalPerp = Vector2.Perpendicular(hit.normal).normalized;
            slopeDownAngle = Vector2.Angle(hit.normal, Vector2.up);

            if (slopeDownAngle > 0.0f && slopeDownAngle < maxSlopeAngle)
            {
                isOnSlope = true;
            }
            else
            {
                isOnSlope = false;
            }

            Debug.DrawRay(hit.point, hit.normal, Color.green);
            Debug.DrawRay(hit.point, slopeNormalPerp, Color.red);
        }
    }

    private void applyMovement()
    {

        float horizontalVelocity = rb.linearVelocity.x;
        
        if (isGrounded() && !isOnSlope)
        {
            horizontalVelocity += playerMoveVector.x;
            if (Mathf.Abs(playerMoveVector.x) < 0.1f)
            {
                horizontalVelocity *= Mathf.Pow(1f - moveDampingGroundedStop, Time.deltaTime * 10f);
            }
            else
            {
                if (Mathf.Sign(playerMoveVector.x) != Mathf.Sign(horizontalVelocity))
                {
                    horizontalVelocity *= Mathf.Pow(1f - moveDampingGroundedTurning, Time.deltaTime * 10f);
                }else{
                    horizontalVelocity *= Mathf.Pow(1f - moveDampingGroundedBasic, Time.deltaTime * 10f);
                }
            }

            rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);

        }
        else if(isGrounded() && isOnSlope)
        {

            float verticalVelocity = rb.linearVelocity.y;

            if (Mathf.Abs(playerMoveVector.x) > 0.0f)
            {
                horizontalVelocity += -playerMoveVector.x * slopeNormalPerp.x;
                verticalVelocity += -playerMoveVector.x * slopeNormalPerp.y;
            }
            else
            {
                horizontalVelocity = 0;
                verticalVelocity = 0;
            }
            
            Debug.Log("Grounded Slope horizontalVelocity = " + horizontalVelocity + " and Vertical Velocity = " + verticalVelocity);
            Debug.Log("RB Velocity = " + rb.linearVelocity);

            if (Mathf.Abs(playerMoveVector.x) < 0.1f)
            {

                horizontalVelocity *= Mathf.Pow(1f - moveDampingGroundedStop, Time.deltaTime * 10f);
                verticalVelocity *= Mathf.Pow(1f - moveDampingGroundedStop, Time.deltaTime * 10f);

            }
            else
            {
                if (Mathf.Sign(playerMoveVector.x) != Mathf.Sign(horizontalVelocity))
                {
                    
                    horizontalVelocity *= Mathf.Pow(1f - moveDampingGroundedTurning, Time.deltaTime * 10f);
                    verticalVelocity *= Mathf.Pow(1f - moveDampingGroundedTurning, Time.deltaTime * 10f);
                }
                else
                {
                    horizontalVelocity *= Mathf.Pow(1f - moveDampingGroundedBasic, Time.deltaTime * 10f);
                    verticalVelocity *= Mathf.Pow(1f - moveDampingGroundedBasic, Time.deltaTime * 10f);
                }
            }

            rb.linearVelocity = new Vector2(horizontalVelocity, verticalVelocity);

        }
        else if(isGrounded() == false)
        {
            horizontalVelocity += playerMoveVector.x;
            horizontalVelocity *= Mathf.Pow(1f - moveDampingInAirBasic, Time.deltaTime * 10f);

            rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);
        }




        if (jumpBufferTimer > 0.0f && coyoteTimer > 0.0f)
        {
            if (jumpPressed)
            {
                jumpBufferTimer = 0.0f;
                coyoteTimer = 0.0f;
                rb.linearVelocity = new Vector2(horizontalVelocity, jumpForce);
            }
            else
            {
                jumpBufferTimer = 0.0f;
                coyoteTimer = 0.0f;
                rb.linearVelocity = new Vector2(horizontalVelocity, jumpForce*0.4f);
            }

        }

    }

    public void evaluateJumpTimers()
    {
        jumpBufferTimer -= Time.deltaTime;
        coyoteTimer -= Time.deltaTime;

        if (isGrounded())
        {
            coyoteTimer = coyoteTime;
        }
    }

    public void getInput()
    {

        if (isGrounded())
        {
            if (isMoveRight)
            {
                playerMoveVector = Vector3.right * moveSpeedGrounded;
            }
            else
            {
                if (isMoveLeft)
                {
                    playerMoveVector = Vector3.left * moveSpeedGrounded;
                }
                else
                {
                    playerMoveVector.x = 0.0f;
                }
            }
        }
        else
        {
            if (isMoveRight)
            {
                playerMoveVector = Vector3.right * moveSpeedInAir;
            }
            else
            {
                if (isMoveLeft)
                {
                    playerMoveVector = Vector3.left * moveSpeedInAir;
                }
                else
                {
                    playerMoveVector.x = 0.0f;
                }
            }
        }   
    }

    public void jump()
    {
        jumpBufferTimer = jumpBuffer;
        jumpPressed = true;
    }

    public void jumpRelease()
    {
        if (rb.linearVelocity.y > 0.0f)
        {
            Vector2 newVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y*0.4f);
            rb.linearVelocity = newVelocity;
        }
        jumpPressed = false;
    }

    public void setMoveRightBoolTrue()
    {
        isMoveRight = true;
    }

    public void setMoveLeftBoolTrue()
    {
        isMoveLeft = true;
    }

    public void setMoveRightBoolFalse()
    {
        isMoveRight = false;
    }

    public void setMoveLeftBoolFalse()
    {
        isMoveLeft = false;
    }
    
    private bool isGrounded()
    {
        RaycastHit2D raycastHit = Physics2D.BoxCast(playerCollider.bounds.center, playerCollider.bounds.size, 0.0f, Vector2.down, groundCheckLength, groundMask);

        Color rayColor;

        if (raycastHit.collider != null)
        {
            rayColor = Color.green;
        }
        else
        {
            rayColor = Color.red;
        }

        Debug.DrawRay(playerCollider.bounds.center + new Vector3(playerCollider.bounds.extents.x, 0.0f), Vector2.down *(playerCollider.bounds.extents.y + groundCheckLength), rayColor);
        Debug.DrawRay(playerCollider.bounds.center - new Vector3(playerCollider.bounds.extents.x, 0.0f), Vector2.down * (playerCollider.bounds.extents.y + groundCheckLength), rayColor);
        Debug.DrawRay(playerCollider.bounds.center - new Vector3(playerCollider.bounds.extents.x, playerCollider.bounds.extents.y+ groundCheckLength), Vector2.right * (playerCollider.bounds.extents.x*2), rayColor);

        return raycastHit.collider != null;
    }

    private void OnDrawGizmos()
    {
        isGrounded();
    }

}
