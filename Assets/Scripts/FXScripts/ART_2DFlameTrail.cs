using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ART_2DFlameTrail : MonoBehaviour
{
    [Header("Game Objects/Components")]
    public CTRL_PlayerPlatformer playerCtrl;
    public Animator playerAnimator;

    [Header("Animation Curves")]
    public float fireFlowInfluence;
    public float fireFlowEvalSpeed;
    public float fireWeightEvalSpeed;
    public AnimationCurve[] fireFlowCurves = new AnimationCurve[0];
    public AnimationCurve[] fireWeightCurves = new AnimationCurve[0];
    public AnimationCurve lagTimeCurve;
    public float fireUpwardsInfluence;
    public AnimationCurve fireUpwardsCurve;
    private float fireFlowOutput;
    private float fireWeightOutput;
    private float fireWeightCalculation;
    private float fireFlowCurveTimer = 0.0f;
    private int curveIndexA = 0;
    private int curveIndexB = 0;
    private bool isCurveA = true;
    private float[] lineIndexFloats;

    [Header("Core Mechanics")]
    public int trailResolution;
    public float offset;
    public Vector3 facingDirection;
    public Vector3 animOverrideDirection;
    [Range(0.0f, 1.0f)]
    public float _animOverrideValue;
    [SerializeField] private float _maxVelocity;
    private float _startingLag;
    [SerializeField] private float _maxVelocityLag;
    private float _startEvalSpeed;
    [SerializeField] private float _maxVelocityEvalSpeed;
    private float _startingInfluence;
    [SerializeField] private float _maxVelocityInfluence;
    private Vector3 facingDirectionStart;
    public float lagTime;
    private Vector3[] lineSegmentPositions;
    private Vector3[] lineSegmentVelocities;
    private LineRenderer lineRenderer;
    private Vector3 positionRandomCalc;

    [Header("Debug Stuff")]
    private Vector3 facingPerpendicularDir;
    public float perpLineLength;

    // Use this for initialization
    void Start()
    {
        fireFlowCurveTimer = 1.0f;
        facingDirectionStart = facingDirection;
        curveIndexA = Random.Range(0, fireFlowCurves.Length - 1);
        curveIndexB = Random.Range(0, fireFlowCurves.Length - 1);
        _startingLag = lagTime;
        _startEvalSpeed = fireFlowEvalSpeed;
        _startingInfluence = fireFlowInfluence;
        //for(int f = 0; f < trailResolution; f++)
        //{
        //    lineIndexFloats[f] = (float)f / (float)trailResolution; 
        //}
        


        initializeLineRenderer();

        initializePositions();
    }

    // Update is called once per frame
    void Update()
    {
        ProcessVelocity(playerCtrl);
        evaluateCurveTimers();
        updatePositions();
    }

//========================================================================================//
//   Functions
//========================================================================================//

    private void evaluatePlayerFlip()
    {
        //if (playerCtrl.isFlipped)
        //{
        //    facingDirection.x = -facingDirectionStart.x;
        //}
        //else
        //{
        //    facingDirection.x = facingDirectionStart.x;
        //}
    }

    private void evaluateCurveTimers()
    {

        if (fireFlowCurveTimer <= 0.0f)
        {
            fireFlowCurveTimer = 1.0f;
            if (isCurveA)
            {
                curveIndexB = Random.Range(0, fireFlowCurves.Length - 1);
                isCurveA = false;
            }
            else
            {
                curveIndexA = Random.Range(0, fireFlowCurves.Length - 1);
                isCurveA = true;
            }

        }

        fireFlowCurveTimer -= Time.deltaTime * fireFlowEvalSpeed;



    }

    // Initializes Line Render Variables/Properties
    private void initializeLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.SetVertexCount(trailResolution);

        lineSegmentPositions = new Vector3[trailResolution];
        lineSegmentVelocities = new Vector3[trailResolution];
    }

    // Initializes Line Renderer Positions
    private void initializePositions()
    {
        evaluatePlayerFlip();

        for (int i = 0; i < lineSegmentPositions.Length; i++)
        {
            lineSegmentPositions[i] = new Vector3();
            lineSegmentVelocities[i] = new Vector3();

            if (i == 0)
            {
                // Set the first position to be at the base of the transform
                lineSegmentPositions[i] = transform.position;
            }
            else
            {
                positionRandomCalc = lineSegmentPositions[i - 1] + (facingDirection.normalized * (offset* this.transform.localScale.y));

                // All others will follow the original with the offset that you set up
                lineSegmentPositions[i] = Vector3.SmoothDamp(lineSegmentPositions[i], positionRandomCalc, ref lineSegmentVelocities[i], lagTime);
            }
        }
    }

    // Calculates Line Renderer Positions
    private void updatePositions()
    {
        evaluatePlayerFlip();

        facingPerpendicularDir.x = facingDirection.y;
        facingPerpendicularDir.y = -facingDirection.x;

        for (int i = 0; i < lineSegmentPositions.Length; i++)
        {
            if (i == 0)
            {
                // We always want the first position to be exactly at the original position
                lineSegmentPositions[i] = transform.position;
            }
            else
            {
                // Basically drawing a ray from the last position to the desired point position.
                positionRandomCalc = lineSegmentPositions[i - 1] + (facingDirection.normalized * (offset* this.transform.localScale.y));

                fireWeightCalculation = ((float)i / (float)trailResolution) + fireFlowCurveTimer;

                if (fireWeightCalculation > 1.0f)
                {
                    fireFlowOutput = fireFlowCurves[curveIndexA].Evaluate(fireWeightCalculation-1.0f);
                }
                else
                {
                    fireFlowOutput = fireFlowCurves[curveIndexA].Evaluate(fireWeightCalculation);
                }

                fireFlowOutput = fireFlowOutput * fireWeightCurves[0].Evaluate((float)i / (float)trailResolution);

                if (fireFlowOutput >= 0.0f)
                {
                    positionRandomCalc = Vector3.Lerp(positionRandomCalc, (positionRandomCalc + (facingPerpendicularDir.normalized * fireFlowInfluence)), fireFlowOutput);
                }
                else
                {
                    positionRandomCalc = Vector3.Lerp(positionRandomCalc, (positionRandomCalc - (facingPerpendicularDir.normalized * fireFlowInfluence)), Mathf.Abs(fireFlowOutput));
                }

                //if (isCurveA)
                //{
                //    fireFlowOutput = fireFlowCurves[curveIndexA].Evaluate((1.0f - fireFlowCurveTimer));
                //}
                //else
                //{
                //    fireFlowOutput = fireFlowCurves[curveIndexB].Evaluate((1.0f - fireFlowCurveTimer));
                //}


                // All others will follow the original with the offset that you set up
                lineSegmentPositions[i] = Vector3.SmoothDamp(lineSegmentPositions[i], positionRandomCalc, ref lineSegmentVelocities[i], lagTime * lagTimeCurve.Evaluate((float)i / (float)trailResolution));
            }
            // Once we're done calculating where our position should be, set the line segment to be in its proper place
            lineRenderer.SetPosition(i, lineSegmentPositions[i]);
        }
    }

    public void ProcessVelocity(CTRL_PlayerPlatformer playerCtrl)
    {
        float curVelocity = playerCtrl.rb.linearVelocity.magnitude;
        if (curVelocity > _maxVelocity)
        {
            curVelocity = _maxVelocity;
        }

        Vector3 velocityNormalized = playerCtrl.rb.linearVelocity.normalized;
        Vector3 curFacingDir = Vector3.zero;
        // Direction only — NOT transform.position + ... — since facingDirection is
        // always consumed as a direction. Adding the world position made the trail
        // skew with the character's distance from world origin.
        Vector3 animOverrideDirTarget = (facingDirectionStart + animOverrideDirection).normalized * (offset * (float)trailResolution);
        if (!playerCtrl.Flipped)
        {
            curFacingDir = facingDirectionStart;            
        }
        else
        {
            curFacingDir = new Vector3(-facingDirectionStart.x, facingDirectionStart.y, facingDirectionStart.z);
            animOverrideDirTarget.x = -animOverrideDirTarget.x;
        }
        
        Vector3 velocityOverrideDirTarget = -velocityNormalized * (offset * (float)trailResolution) * this.transform.localScale.y;
        Vector3 facingDirCalc = Vector3.Lerp(curFacingDir, velocityOverrideDirTarget,
            playerCtrl.rb.linearVelocity.magnitude / _maxVelocity);

        fireFlowInfluence = Mathf.Lerp(_startingInfluence, _maxVelocityInfluence, playerCtrl.rb.linearVelocity.magnitude / _maxVelocity);
        fireFlowEvalSpeed = Mathf.Lerp(_startEvalSpeed, _maxVelocityEvalSpeed, playerCtrl.rb.linearVelocity.magnitude / _maxVelocity);
        lagTime = Mathf.Lerp(_startingLag, _maxVelocityLag, playerCtrl.rb.linearVelocity.magnitude / _maxVelocity);
        facingDirection = Vector3.Lerp(facingDirCalc, animOverrideDirTarget, _animOverrideValue);
    }
    
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, ((transform.position+(facingDirection.normalized * (offset * (float)trailResolution)* this.transform.localScale.y))));

        if (animOverrideDirection != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, ((transform.position+((facingDirection+animOverrideDirection).normalized * (offset * (float)trailResolution)* this.transform.localScale.y))));
        }
        
        for (int n = 0; n < trailResolution; n++)
        {
            float lineResolutionFraction = ((float)n) / ((float)trailResolution);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(((transform.position + ((facingDirection.normalized * this.transform.localScale.y) * (lineResolutionFraction* (offset * (float)trailResolution)))) + (facingPerpendicularDir.normalized * (perpLineLength * 0.5f))), ((transform.position + ((facingDirection.normalized * this.transform.localScale.y) * (lineResolutionFraction * (offset * (float)trailResolution)))) - (facingPerpendicularDir.normalized * (perpLineLength * 0.5f))));
        }
    }
}
