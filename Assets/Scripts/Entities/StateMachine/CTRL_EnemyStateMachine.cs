using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the active enemy state, handles registering states and transitions.
/// Called each frame by CTRL_Enemy.
/// </summary>
public class CTRL_EnemyStateMachine
{
    private Dictionary<string, IEnemyState> _states = new Dictionary<string, IEnemyState>();
    private IEnemyState _activeState;
    private EnemyBlackboard _board;

    public string ActiveStateName { get; private set; }

    public CTRL_EnemyStateMachine(EnemyBlackboard board)
    {
        _board = board;
    }

    public void RegisterState(string name, IEnemyState state)
    {
        _states[name] = state;
    }

    public void SetInitialState(string name)
    {
        if (!_states.ContainsKey(name))
        {
            Debug.LogWarning($"[EnemyStateMachine] Initial state '{name}' not registered.");
            return;
        }

        ActiveStateName = name;
        _activeState = _states[name];
        _activeState.Enter(_board);
    }

    public void Update()
    {
        if (_activeState == null) return;

        _activeState.Update(_board);

        string next = _activeState.CheckTransitions(_board);
        if (next != null && next != ActiveStateName && _states.ContainsKey(next))
        {
            TransitionTo(next);
        }
    }

    public void FixedUpdate()
    {
        _activeState?.FixedUpdate(_board);
    }

    public void TransitionTo(string name)
    {
        if (!_states.ContainsKey(name))
        {
            Debug.LogWarning($"[EnemyStateMachine] Cannot transition to unknown state '{name}'.");
            return;
        }

        _activeState?.Exit(_board);
        ActiveStateName = name;
        _activeState = _states[name];
        _activeState.Enter(_board);
    }
}
