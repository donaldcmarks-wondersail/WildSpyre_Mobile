using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class INPT_TouchManager : MonoBehaviour
{
    [Header("PlayerVariables")]
    public GameObject player;
    private CTRL_PlayerPlatformer playerCtrl;

    [Header("Game Objects/Components")]
    public Camera cam;
    public Material LRMaterial;
    public GameObject swipeArea;
    public LineRenderer aimLine;
    public Image swipeAreaImage;
    public Util_Trajectory trajectory;
    public RectTransform swipeRectCenter;
    [SerializeField] private VariableJoystick _joystick;
    
    [Header("Swipe Variables")]
    public Vector2 swipeAreaSize;
    private Vector2 swipeAreaSizeCalc;
    public float swipeThreshold;
    public float swipeMax;
    private float swipeValCalc;
    public Color swipeColor;
    private Touch curSwipeTouch;
    private Vector2 startPos;
    [HideInInspector] public Vector2 direction;
    public bool isTouchedZone = false;
    public bool isSwipe = false;
    public float aimMaxMagnitude;
    private float slingForceCalc;

    [Header("Aim Visualization Variables")]
    public GameObject aimReticle;
    private Quaternion aimReticleRot;
    private Quaternion aimReticleRotNegative;
    private SpriteRenderer aimReticleSpriteRend;
    private Vector3 aimReticleScale;
    public float aimReticleScaleMax;
    public Color aimReticleColorStart;
    public Color aimReticleColorFaded;
    public float trajectoryMagnitudeMultiplier;

    [Header("Swipe Area Calculations")]
    private Vector3 swipeLimitPos_botLeft;
    private Vector3 swipeLimitPos_botRight;
    private Vector3 swipeLimitPos_topLeft;
    private Vector3 swipeLimitPos_topRight;

    private Vector3 swipeLimit_BotLeft;
    private Vector3 swipeLimit_BotRight;
    private Vector3 swipeLimit_TopLeft;
    private Vector3 swipeLimit_TopRight;

    [Header("Debug Line Renderer Assets")]
    public LineRenderer LR01;
    public LineRenderer LR02;
    public LineRenderer LR03;
    public LineRenderer LR04;
    
    private Vector3[] LR01pos = new Vector3[2];
    private Vector3[] LR02pos = new Vector3[2];
    private Vector3[] LR03pos = new Vector3[2];
    private Vector3[] LR04pos = new Vector3[2];

    private Vector3[] aimLinePos = new Vector3[2];

    [Header("MultiTouch Variables")]
    public GameObject circle;
    public List<touchLocation> touches = new List<touchLocation>();
    public int swipeFingerID = -1;
    
    // Start is called before the first frame update
    void Start()
    {
        playerCtrl = player.GetComponent<CTRL_PlayerPlatformer>();
        LRMaterial.color = Color.yellow;
        aimLinePos[0] = aimLine.gameObject.transform.position;
        swipeAreaImage.color = Color.yellow;
        swipeAreaSizeCalc = swipeAreaSize;
        initializeSwipeArea();
        cam.farClipPlane = 2001.0f;
        aimReticleSpriteRend = aimReticle.GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        initializeSwipeArea();
        if (playerCtrl.PlayerState != CTRL_PlayerPlatformer.playerControlState.Dead && playerCtrl.PlayerState != CTRL_PlayerPlatformer.playerControlState.LevelComplete)
        {
            //evaluateSwipeArea();
            evaluateJoystick();
        }

        this.transform.position = cam.ScreenToWorldPoint(swipeRectCenter.position);
        if (isSwipe)
        {
            drawAimLine();
        }
    }

    private void parseSwipeAreaBegin(Touch touch)
    {
        if (touch.position.x > swipeLimit_BotLeft.x && touch.position.x < swipeLimit_BotRight.x && touch.position.y < swipeLimit_TopLeft.y && touch.position.y > swipeLimit_BotRight.y)
        {
            curSwipeTouch = touch;
            Debug.Log("curSwipeTouch fingerID = " + curSwipeTouch.fingerId);
            isTouchedZone = true;
            LRMaterial.color = Color.red;
            swipeAreaImage.color = Color.red;
            swipeFingerID = touch.fingerId;
            startPos = touch.position;
            
            switch (playerCtrl.PlayerState)
            {
                case CTRL_PlayerPlatformer.playerControlState.Platformer:
                    playerCtrl.jump();
                    break;
                case CTRL_PlayerPlatformer.playerControlState.Sling:
                    //playerCtrl.setStatePlatform();
                    break;
            }
        }
    }

    private void evaluateJoystick()
    {
        if (_joystick!= null )
        {
            if (_joystick._isPressed)
            {
                direction = _joystick.Direction;
                CheckForSling(direction);
            }
        }
    }
    
    // Checks if a touch exists within the defined Swipe Area
    private void evaluateSwipeArea()
    {
        int j = 0;
        while (j < Input.touchCount)
        {
            Touch t = Input.GetTouch(j);
            if (t.phase == TouchPhase.Began)
            {
                touches.Add(new touchLocation(t.fingerId, createCircle(t)));
                parseSwipeAreaBegin(t);
            }
            else if (t.phase == TouchPhase.Ended)
            {
                touchLocation thisTouch = touches.Find(touchLocation => touchLocation.touchId == t.fingerId);

                if (t.fingerId == swipeFingerID)
                {
                    Debug.Log("Swiper FingerID Lifted");
                    swipeFingerID = -1;
                    LRMaterial.color = Color.yellow;
                    swipeAreaImage.color = Color.yellow;
                    aimLine.material.color = Color.yellow;

                    ExecuteSling(direction);
                }
                
                Destroy(thisTouch.circle);

                touches.RemoveAt(touches.IndexOf(thisTouch));

            }
            else if (t.phase == TouchPhase.Moved)
            {
                if (t.fingerId == swipeFingerID)
                {
                    direction = t.position - startPos;
                    
                    //Debug.Log("Swipe Direction Magnitude = " + direction.magnitude);

                    CheckForSling(direction);

                }

                touchLocation thisTouch = touches.Find(touchLocation => touchLocation.touchId == t.fingerId);
                thisTouch.circle.transform.position = getTouchPosition(t.position);
            }
            ++j;
        }

        drawSwipeArea();
    }

    private void CheckForSling(Vector2 aimDirection)
    {
        if (aimDirection.magnitude > swipeThreshold)
        {
            swipeAreaImage.color = swipeColor;
            aimLine.material.color = swipeColor;

            if (playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.Platformer || playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.Sling)
            {
                if (playerCtrl.slingVars.canSling)
                {
                    if (playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.Platformer)
                    {
                        playerCtrl.setStateSling();
                    }

                    if (!playerCtrl._isSlinging)
                    {
                        playerCtrl.StartCoroutine("startSlingSlowMoCO");
                    }
                
                    aimReticle.SetActive(true);
                    isSwipe = true;
                }
            }
        }
        else
        {
            aimLine.material.color = Color.yellow;
        }
    }

    public void ExecuteSling(Vector3 slingDirection)
    {
        aimReticle.SetActive(false);

        swipeValCalc = 0.0f;
        playerCtrl.jumpRelease();
        
        if (playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.Sling)
        {
            if (slingDirection.magnitude > swipeThreshold)
            {
                if (playerCtrl.slingVars.canSling)
                {
                    playerCtrl.StopCoroutine("startSlingSlowMoCO");
                    Instantiate(playerCtrl.particleFXVars.slingFireParticles, playerCtrl.transform.position, aimReticleRotNegative);
                    playerCtrl.resetPlayerPhysics();
                    playerCtrl.resetTimescale();
                    playerCtrl.rb.AddForce(playerCtrl.slingVars.slingForce * (-slingDirection.normalized));
                    playerCtrl.slingVars.canSling = false;
                }
            }
            else
            {
                playerCtrl.StopCoroutine("startSlingSlowMoCO");
                playerCtrl.resetTimescale();
                playerCtrl.setStatePlatform();
            }
        }

        isSwipe = false;

        aimLine.enabled = false;

        trajectory.resetLineRendPositions();

        trajectory.Calculate_Trajectory(this.transform.position);
        trajectory.drawTrajectory();

        trajectory.lineRendere.enabled = false;

        direction = Vector2.zero;
    }

    private void processSlingForce()
    {
        swipeValCalc = direction.magnitude / swipeMax;

        slingForceCalc = Mathf.Lerp(playerCtrl.slingVars.slingForceMin, playerCtrl.slingVars.slingForceMax, swipeValCalc);

        if (swipeValCalc > 1.0f)
        {
            swipeValCalc = 1.0f;
        }

        Debug.Log("swipeValCalc = " + swipeValCalc);

        playerCtrl.slingVars.slingForce = Mathf.Lerp(playerCtrl.slingVars.slingForceMin, playerCtrl.slingVars.slingForceMax, swipeValCalc);
    }

    private void drawAimLine()
    {
        aimLinePos[0] = player.transform.position;

        Vector3 aimVector = aimLinePos[0] + new Vector3(direction.x, direction.y, 0.0f) * (direction.magnitude / 10000.0f);
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float angleNegative = Mathf.Atan2(-direction.y, -direction.x) * Mathf.Rad2Deg;
        aimReticleRot = Quaternion.AngleAxis(angle, Vector3.forward);
        aimReticleRotNegative = Quaternion.AngleAxis(angleNegative, Vector3.forward);
        aimReticle.transform.rotation = Quaternion.Slerp(transform.rotation, aimReticleRot, 10.0f);

        //Quaternion aimReticleRotation = Quaternion.LookRotation(aimReticle.transform.right, aimVector.normalized);
        //aimReticle.transform.rotation = aimReticleRotation;

        aimReticle.transform.localScale = Vector3.Lerp(new Vector3(0.2f, aimReticleScaleMax, aimReticleScaleMax), new Vector3(aimReticleScaleMax, aimReticleScaleMax, aimReticleScaleMax), swipeValCalc);

        aimLinePos[1] = aimVector;

        aimLine.SetPositions(aimLinePos);

        if (direction.magnitude > swipeThreshold)
        {
            aimReticleSpriteRend.color = aimReticleColorStart;
        }
        else
        {
            aimReticleSpriteRend.color = aimReticleColorFaded;
        }

        processSlingForce();

        calculateTrajectory();
    }

    //Defines Swipe Area
    private void initializeSwipeArea()
    {
        swipeLimitPos_botLeft = swipeArea.transform.position + new Vector3(-swipeAreaSize.x, -swipeAreaSize.y, 0.0f);
        swipeLimitPos_botRight = swipeArea.transform.position + new Vector3(swipeAreaSize.x, -swipeAreaSize.y, 0.0f);
        swipeLimitPos_topLeft = swipeArea.transform.position + new Vector3(-swipeAreaSize.x, swipeAreaSize.y, 0.0f);
        swipeLimitPos_topRight = swipeArea.transform.position + new Vector3(swipeAreaSize.x, swipeAreaSize.y, 0.0f);

        swipeLimit_BotLeft = cam.WorldToScreenPoint(swipeLimitPos_botLeft);
        swipeLimit_BotRight = cam.WorldToScreenPoint(swipeLimitPos_botRight);
        swipeLimit_TopLeft = cam.WorldToScreenPoint(swipeLimitPos_topLeft);
        swipeLimit_TopRight = cam.WorldToScreenPoint(swipeLimitPos_topRight);

    }
    
    // Draws Swipe Area using Line Renderers
    private void drawSwipeArea()
    {
        LR01pos[0] = cam.ScreenToWorldPoint(swipeLimit_BotLeft);
        LR01pos[1] = cam.ScreenToWorldPoint(swipeLimit_BotRight);

        LR02pos[0] = cam.ScreenToWorldPoint(swipeLimit_BotRight);
        LR02pos[1] = cam.ScreenToWorldPoint(swipeLimit_TopRight);

        LR03pos[0] = cam.ScreenToWorldPoint(swipeLimit_TopRight);
        LR03pos[1] = cam.ScreenToWorldPoint(swipeLimit_TopLeft);

        LR04pos[0] = cam.ScreenToWorldPoint(swipeLimit_TopLeft);
        LR04pos[1] = cam.ScreenToWorldPoint(swipeLimit_BotLeft);
        
        LR01.SetPositions(LR01pos);
        LR02.SetPositions(LR02pos);
        LR03.SetPositions(LR03pos);
        LR04.SetPositions(LR04pos);
    }

    private void drawAimGizmo(Vector2 startPos, Vector2 direction)
    {
        Debug.DrawLine(new Vector3(startPos.x, startPos.y, 0.0f), new Vector3(startPos.x, startPos.y, 0.0f) + new Vector3(direction.x, direction.y, 0.0f), Color.yellow);
    }

    // Gizmo Stuff
    private void OnDrawGizmosSelected()
    {
        initializeSwipeArea();
        drawSwipeArea();
    }
    
    Vector2 getTouchPosition(Vector2 touchPosition)
    {
        return cam.ScreenToWorldPoint(new Vector3(touchPosition.x, touchPosition.y, transform.position.z));
    }

    GameObject createCircle(Touch t)
    {
        GameObject c = Instantiate(circle) as GameObject;
        c.name = "Touch" + t.fingerId;
        c.transform.position = getTouchPosition(t.position);
        return c;
    }

    public void calculateTrajectory()
    {
        trajectory.velocity = ((-direction.normalized * playerCtrl.slingVars.slingForce * trajectoryMagnitudeMultiplier));
        trajectory.Calculate_Trajectory(this.transform.position);
        trajectory.drawTrajectory();


        trajectory.lineRendere.enabled = true;
    }

}
