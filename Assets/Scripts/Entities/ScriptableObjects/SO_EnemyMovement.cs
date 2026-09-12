using System;
using UnityEngine;

[Flags]
public enum MovementCapability
{
    None         = 0,
    Walk         = 1,
    Jump         = 2,
    Fly          = 4,
    ClimbWalls   = 8,
    ClimbCeiling = 16
}

[CreateAssetMenu(fileName = "NewEnemyMovement", menuName = "WildSpyre/Enemy/Movement")]
public class SO_EnemyMovement : ScriptableObject
{
    [Header("Capability Flags")]
    public MovementCapability capabilities = MovementCapability.Walk | MovementCapability.Jump;

    [Header("Walk / Run")]
    public float walkSpeed = 2f;
    public float chaseSpeed = 4f;
    public float acceleration = 10f;

    [Header("Jump")]
    public float jumpForce = 8f;
    public float jumpHeightThreshold = 0.5f;

    [Header("Fly")]
    public float flySpeed = 4f;

    [Header("Climb")]
    public float climbSpeed = 3f;

    [Header("Smoothing")]
    [Tooltip("Seconds to ease velocity toward its target during fly / climb movement, " +
             "while patrolling / wandering. Higher = floatier and rounds corners; " +
             "0 = instant. Ground walking uses Acceleration instead.")]
    public float velocitySmoothTime = 0.12f;

    [Tooltip("Same, but while chasing a target. Usually lower than the patrol value so " +
             "pursuit feels sharper and more urgent than idle drifting.")]
    public float chaseVelocitySmoothTime = 0.06f;

    [Header("Pathfinding")]
    public float pathUpdateInterval = 0.5f;
    public float waypointReachedDistance = 0.25f;

    [Header("Ground Detection")]
    public LayerMask groundMask;
    public float groundCheckDistance = 0.15f;
}
