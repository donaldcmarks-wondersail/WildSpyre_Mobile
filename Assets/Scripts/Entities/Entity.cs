using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Entity : MonoBehaviour
{
    [Header("Entity States Variables")]
    [SerializeField] private EntityState _defaultState;
    private EntityState _currentState;
    
    [Header("Line of Sight Variables")]
    [SerializeField] private bool _lineOfSight = true;
    [SerializeField] private float _LOSRange = 1.0f;
    [SerializeField] private float _LOSRangeHunting = 1.4f;
    [SerializeField] private LayerMask _LOSMask;
    private bool _LOSCheck = false;
    
    [Header("Movement Variables")]
    [SerializeField] private MovementType _movementType;
    [SerializeField] private float _movementSpeed = 1.0f;
    [SerializeField] private float _movementSpeedHunting = 1.0f;
    [SerializeField][Range(0.0f,1.0f)] private float _movementDamping = 0.0f;
    [SerializeField][Range(0.0f,1.0f)] private float _directionDamping = 0.0f;
    [SerializeField][Range(0.0f,1.0f)] private float _movementDampingHunting = 0.0f;
    [SerializeField][Range(0.0f,1.0f)] private float _directionDampingHunting = 0.0f;
    [SerializeField] private bool _flipWithMotion = true;
    [SerializeField] private bool _rotateTowardsTarget = false;
    [SerializeField][Range(0.0f,1.0f)] private float _rotationDamping = 0.0f;
    private Vector3 _previousMoveDir;
    private Vector3 _currentVelocity;
    
    [Header("Patrol Variables")] 
    [SerializeField] private PatrolType _patrolType;
    [SerializeField] private bool _useWaypointObjects;
    [SerializeField] private float _pauseAtWaypointTime = 0.0f;
    [SerializeField] private List<Transform> _waypointObjects = new List<Transform>();
    [SerializeField] private List<Vector3> _waypointVectors = new List<Vector3>();
    private List<Vector3> WaypointPositions = new List<Vector3>();
    private int _currentWaypointIndex = 0;
    private bool _patrolPingPongReversed = false;
    [SerializeField] private bool _patrolLoopReversed = false;
    [SerializeField] private bool _pauseOnWaypoints;
    [SerializeField] private bool _pauseOnEndPoints;
    [SerializeField] private AnimationCurve _waypointEaseInCurve;
    private float _distanceBetweenWaypoints;
    
    [Header("Hunt Variables")] 
    [SerializeField] private float _pauseTimeAfterTargetLost = 1.0f;
    private Coroutine _pauseCoroutine;
    
    [Header("Attacking Variables")]
    [SerializeField] private AttackType _attackType;
    [SerializeField] private float _attackCooldown;
    private float _lastAttackTime;
    
    [Header("Art Variables")] 
    [SerializeField] private Transform _artParent;
    [SerializeField] private Animator _animator;

    [Header("Damage Player Variables")]
    [SerializeField] private GameObject _damagePlayer;
    
    [Header("Take Damage Variables")]
    [SerializeField] private AnimationCurve _takeDamageCurve;
    
    [Header("Miscellaneous Variables")] 
    private CTRL_PlayerPlatformer _player;
    private Rigidbody2D _rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitializeWaypointPositions();
        _rb = this.GetComponent<Rigidbody2D>();
        _player = GameObject.FindWithTag("Player").GetComponent<CTRL_PlayerPlatformer>();
        CurrentState = _defaultState;;
    }

    private void InitializeWaypointPositions()
    {
        for (int i = 0; i < _waypointVectors.Count; i++)
        {
            WaypointPositions.Add(this.transform.position + _waypointVectors[i]);
        }
    }

    // Update is called once per frame
    void Update()
    {
        ProcessMovement();
        
        if (_lineOfSight)
        {
            CheckLOS();
        }
    }

    private void ProcessMovement()
    {
        Vector3 lastPos = this.transform.position;
        
        switch (CurrentState)
        {
            case EntityState.Patrol:
                ProcessPatrolMovement();
                break;
            case EntityState.Hunting:
                ProcessHuntingMovement();
                break;
        }
        _previousMoveDir = (lastPos - transform.position).normalized;
    }

    private void ProcessHuntingMovement()
    {
        switch (_movementType)
        {
            case MovementType.Air:
                // Calculate the direction to the target waypoint
                Vector3 targetDirection = (_player.transform.position - this.transform.position).normalized;

                // Dampen the direction change using Lerp to simulate momentum
                Vector3 dampenedDirection = Vector3.Lerp(_currentVelocity.normalized, targetDirection, _directionDampingHunting);

                _rb.velocity += (Vector2)(dampenedDirection * _movementSpeedHunting - (Vector3)_rb.velocity) * _directionDampingHunting;
                
                // Apply the dampened direction into the velocity
                //_currentVelocity = dampenedDirection * _movementSpeedHunting;

                // Update the position using the computed velocity
                //_rb.velocity = _currentVelocity;
                //this.transform.position += _currentVelocity * Time.fixedDeltaTime; // Fixed time step
                break;
            case MovementType.Ground:
                break;
        }
    }

    private void ProcessPatrolMovement()
    {
        // Get the current waypoint position
        Vector3 targetWaypoint = WaypointPositions[_currentWaypointIndex];
        switch (_movementType)
        {
            case MovementType.Air:
                // Calculate the direction to the target waypoint
                Vector3 targetDirection = (targetWaypoint - this.transform.position).normalized;

                // Dampen the direction change using Lerp to simulate momentum
                Vector3 dampenedDirection = Vector3.Lerp(_currentVelocity.normalized, targetDirection, _directionDamping);

                float velocityMultiplier = 1.0f;  
                
                float distanceToWaypoint = Vector3.Distance(this.transform.position, targetWaypoint);

                //velocityMultiplier = _waypointEaseInCurve.Evaluate();
                
                _rb.velocity += (Vector2)(dampenedDirection * _movementSpeed - (Vector3)_rb.velocity) * _directionDamping;
                
                
                
                // Apply the dampened direction into the velocity
                //_currentVelocity = dampenedDirection * _movementSpeed;

                // Update the position using the computed velocity
                //_rb.velocity = _currentVelocity;
                //this.transform.position += _currentVelocity * Time.fixedDeltaTime; // Fixed time step

                // Check if the entity has reached the waypoint (using a small threshold)
                if (distanceToWaypoint < 0.1f)
                {
                    NewWaypointIndex();
                }
                break;

            case MovementType.Ground:
                // Calculate the direction along the ground to the current waypoint
                Vector3 movementDirection = (targetWaypoint - this.transform.position).normalized;
                transform.position += new Vector3(movementDirection.x, 0, 0) * _movementSpeed * Time.deltaTime;

                // Optional: Flip the entity based on movement direction
                if (_flipWithMotion)
                {
                    //FlipEntity(movementDirection.x);
                }

                // Check if the entity has reached the waypoint (using a small threshold)
                if (Vector2.Distance(this.transform.position, targetWaypoint) < 0.1f)
                {
                    // Move to the next waypoint
                    _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypointVectors.Count;
                }
                break;
        }
    }

    private int NewWaypointIndex()
    {
        switch (_patrolType)
        {
            int oldWaypointIndex = _currentWaypointIndex;
            int newWaypointIndex = _currentWaypointIndex;
                
            case PatrolType.Loop:
                // Move to the next waypoint, cycling back to 0 if at the end of the list
                if (_patrolLoopReversed)
                {
                    newWaypointIndex = (_currentWaypointIndex - 1 + WaypointPositions.Count) % WaypointPositions.Count
                    _currentWaypointIndex = newWaypointIndex;
                }
                else
                {
                    newWaypointIndex = (_currentWaypointIndex + 1) % WaypointPositions.Count;
                    _currentWaypointIndex = newWaypointIndex;
                }
                
                break;
            case PatrolType.PingPong:
                switch (_patrolPingPongReversed)
                {
                    case true:
                        if (_currentWaypointIndex == 0)
                        {
                            _patrolPingPongReversed = false;
                            newWaypointIndex++;
                            _currentWaypointIndex = newWaypointIndex;
                        }
                        else
                        {
                            newWaypointIndex--;
                            _currentWaypointIndex = newWaypointIndex;
                        }
                        break;
                    case false:
                        if (_currentWaypointIndex == WaypointPositions.Count-1)
                        {
                            _patrolPingPongReversed = true;
                            newWaypointIndex--;
                            _currentWaypointIndex = newWaypointIndex;
                        }
                        else
                        {
                            newWaypointIndex++;
                            _currentWaypointIndex = newWaypointIndex;
                        }
                        break;
                }
                break;
                
            _distanceBetweenWaypoints = Vector3.Distance(WaypointPositions[oldWaypointIndex], WaypointPositions[newWaypointIndex]);
        }
    }
    
    private void CheckLOS()
    {
        // Calculate the direction to the player
        Vector2 directionToPlayer = (_player.transform.position - transform.position).normalized;
        float detectionRange = _LOSRange;

        if (CurrentState == EntityState.Hunting)
        {
            detectionRange = _LOSRangeHunting;
        }

        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToPlayer, detectionRange, _LOSMask);

        // Check if the raycast hits the player
        if (hit.collider != null && hit.transform.tag == "Player")
        {
            if (CurrentState != EntityState.TakingDamage && CurrentState != EntityState.Stunned && CurrentState != EntityState.Spawning)
            {
                _LOSCheck = true;
                CurrentState = EntityState.Hunting;    
                Debug.Log("Setting State to HUNTING");
                if (_pauseCoroutine != null)
                {
                    StopCoroutine(_pauseCoroutine);
                }
            }
        }
        else
        {
            _LOSCheck = false;
            if (_pauseCoroutine == null)
            {
                Debug.Log("Setting State to DEFAULT");
                CurrentState = _defaultState;                
            }
        }
    }
    
    private enum MovementType
    {
        Ground,
        Air
    }
    
    private enum AttackType
    {
        Melee,
        Projectile
    }
    
    public enum EntityState
    {
        Spawning,
        Idle,
        Patrol,
        Hunting,
        Emoting,
        Attacking,
        TakingDamage,
        Stunned
    }

    public enum PatrolType
    {
        PingPong,
        Loop
    }
    
    public EntityState CurrentState
    {
        get { return _currentState; }
        set
        {
            if (value != _currentState)
            {
                EntityState oldState = _currentState;
                switch (oldState)
                {
                    case EntityState.Spawning:
                        _currentState = value;
                        break;
                    case EntityState.Idle:
                        _currentState = value;
                        break;
                    case EntityState.Patrol:
                        _currentState = value;
                        break; 
                    case EntityState.Hunting:
                        //_pauseCoroutine = StartCoroutine(PauseAfterHuntingCO());
                        _currentState = value;
                        break;
                    case EntityState.Attacking:
                        _currentState = value;
                        break;
                    case EntityState.Emoting:    
                        _currentState = value;
                        break;
                    case EntityState.Stunned:
                        _currentState = value;
                        break;
                    case EntityState.TakingDamage:
                        _currentState = value;
                        break;
                }                
            }
        }
    }

    public IEnumerator TakeDamageCO(float timeToPause, Vector3 targetVector)
    {
        _damagePlayer.SetActive(false);
        Debug.Log("PauseTake Damage Coroutine being Called!!! WORKING????");
        EntityState oldEntityState = CurrentState;
        CurrentState = EntityState.TakingDamage;
        if (_rb != null)
        {
            // Linearly interpolate (lerp) the position towards the targetVector over the given time
            float elapsedTime = 0f; // Tracks the time for the duration of the pause
            Vector3 initialPosition = transform.position; // Starting position of the object

            while (elapsedTime < timeToPause)
            {
                // Gradually update the position
                Vector3 interpolatedPosition = Vector3.Lerp(initialPosition, targetVector, _takeDamageCurve.Evaluate(elapsedTime / timeToPause));
                _rb.MovePosition(interpolatedPosition); // Smoothly move the rigidbody's position

                elapsedTime += Time.fixedDeltaTime; // Increment elapsed time
                yield return null; // Wait for the next frame
            }

            // Ensure the position is set to the final target at the end
            _rb.MovePosition(targetVector);
        }
        _damagePlayer.SetActive(true);
        CurrentState = oldEntityState;
        yield return null;
    }
    
    private IEnumerator PauseAfterHuntingCO()
    {
        CurrentState = EntityState.Idle;
        yield return new WaitForSeconds(_pauseTimeAfterTargetLost);
        CurrentState = _defaultState;
        yield return null;
    }
    
    void OnDrawGizmos()
    {

        if (Application.isPlaying)
        {
            DrawWaypointGizmosPlaying();
        }
        else
        {
            DrawWaypointGizmosEditor();
        }
        
        // Debugging: Draw the ray in the Scene view
        Vector2 directionToPlayer = Vector2.zero;
        if (_player != null)
        {
            directionToPlayer = (_player.transform.position - transform.position).normalized;            
        }

        if (_LOSCheck)
        {
            Gizmos.color = Color.green;  
            Gizmos.DrawWireSphere(transform.position, _LOSRange);
            if (CurrentState == EntityState.Hunting)
            {
                Gizmos.color = Color.yellow;  
                Gizmos.DrawWireSphere(transform.position, _LOSRangeHunting);
            }
            Debug.DrawRay(transform.position, directionToPlayer * _LOSRange, Color.green, 0.1f);            
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _LOSRange);
            Debug.DrawRay(transform.position, directionToPlayer * _LOSRange, Color.red, 0.1f);
        }
        
        Debug.DrawLine(transform.position, this.transform.position + _previousMoveDir*4.0f, Color.orange, 0.1f);
    }

    private void DrawWaypointGizmosPlaying()
    {
        //Draw Patrol Gizmos
        Gizmos.color = Color.cyan;
        if (_useWaypointObjects)
        {
            for (int i = 0; i < _waypointObjects.Count; i++)
            {
                Gizmos.DrawSphere(_waypointObjects[i].position, 0.125f);
                if (i > 0)
                {
                    Gizmos.DrawLine(_waypointObjects[i].position, _waypointObjects[i-1].position);    
                }

                if (i == _waypointObjects.Count-1 && _patrolType == PatrolType.Loop)
                {
                    Gizmos.DrawLine(_waypointObjects[i].position, _waypointObjects[0].position);    
                }
            }
        }
        else
        {
            for (int i = 0; i < WaypointPositions.Count; i++)
            {
                Gizmos.DrawSphere(WaypointPositions[i], 0.25f);
                if (i > 0)
                {
                    Gizmos.DrawLine(WaypointPositions[i], WaypointPositions[i-1]);    
                }

                if (i == _waypointVectors.Count-1 && _patrolType == PatrolType.Loop)
                {
                    Gizmos.DrawLine(WaypointPositions[i], WaypointPositions[0]);    
                }
            }
        }
    }

    private void DrawWaypointGizmosEditor()
    {
        //Draw Patrol Gizmos
        Gizmos.color = Color.cyan;
        if (_useWaypointObjects)
        {
            for (int i = 0; i < _waypointObjects.Count; i++)
            {
                Gizmos.DrawSphere(_waypointObjects[i].position, 0.125f);
                if (i > 0)
                {
                    Gizmos.DrawLine(_waypointObjects[i].position, _waypointObjects[i-1].position);    
                }

                if (i == _waypointObjects.Count-1 && _patrolType == PatrolType.Loop)
                {
                    Gizmos.DrawLine(_waypointObjects[i].position, _waypointObjects[0].position);    
                }
            }
        }
        else
        {
            for (int i = 0; i < _waypointVectors.Count; i++)
            {
                Gizmos.DrawSphere(this.transform.position + _waypointVectors[i], 0.25f);
                if (i > 0)
                {
                    Gizmos.DrawLine(this.transform.position + _waypointVectors[i],this.transform.position + _waypointVectors[i-1]);    
                }

                if (i == _waypointVectors.Count-1 && _patrolType == PatrolType.Loop)
                {
                    Gizmos.DrawLine(this.transform.position +_waypointVectors[i], this.transform.position +_waypointVectors[0]);    
                }
            }
        }
    }
}
