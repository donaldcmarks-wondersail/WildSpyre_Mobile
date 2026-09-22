using UnityEngine;

/// <summary>Never moves — a coin sitting in place.</summary>
public class PickupMotion_Static : IPickupMotion
{
    public void Init(Transform pickupTransform, SO_PickupData data) { }
    public void Tick(float deltaTime) { }
}

/// <summary>Drifts straight up at Movement Speed, easing in over Movement Damping seconds —
/// e.g. a pickup popping out of a broken box and floating upward.</summary>
public class PickupMotion_Move : IPickupMotion
{
    private Transform _t;
    private SO_PickupData _data;
    private float _age;

    public void Init(Transform pickupTransform, SO_PickupData data)
    {
        _t = pickupTransform;
        _data = data;
        _age = 0f;
    }

    public void Tick(float deltaTime)
    {
        _age += deltaTime;
        float ease = _data.movementDamping > 0f ? Mathf.Clamp01(_age / _data.movementDamping) : 1f;
        _t.position += Vector3.up * (_data.movementSpeed * ease * deltaTime);
    }
}

/// <summary>Idle vertical bob around its spawn point via a sine wave — a classic hovering coin.</summary>
public class PickupMotion_Bounce : IPickupMotion
{
    private Transform _t;
    private SO_PickupData _data;
    private Vector3 _origin;
    private float _phase;

    public void Init(Transform pickupTransform, SO_PickupData data)
    {
        _t = pickupTransform;
        _data = data;
        _origin = pickupTransform.position;
        _phase = 0f;
    }

    public void Tick(float deltaTime)
    {
        _phase += deltaTime;
        float offset = Mathf.Sin(_phase * Mathf.Max(0.01f, _data.movementSpeed)) * _data.bounceAmount;
        _t.position = _origin + Vector3.up * offset;
    }
}

/// <summary>Move's upward drift with Bounce's wobble layered on top.</summary>
public class PickupMotion_MoveAndBounce : IPickupMotion
{
    private readonly PickupMotion_Move _move = new PickupMotion_Move();
    private readonly PickupMotion_Bounce _bounce = new PickupMotion_Bounce();

    public void Init(Transform pickupTransform, SO_PickupData data)
    {
        _move.Init(pickupTransform, data);
        _bounce.Init(pickupTransform, data);
    }

    public void Tick(float deltaTime)
    {
        _move.Tick(deltaTime);
        _bounce.Tick(deltaTime);
    }
}

/// <summary>Lazy circular drift around its spawn point (Bounce Amount = orbit radius) — fireflies,
/// floating orbs, anything that should feel alive without chasing or fleeing the player.</summary>
public class PickupMotion_Fly : IPickupMotion
{
    private Transform _t;
    private SO_PickupData _data;
    private Vector3 _origin;
    private float _angle;

    public void Init(Transform pickupTransform, SO_PickupData data)
    {
        _t = pickupTransform;
        _data = data;
        _origin = pickupTransform.position;
        _angle = Random.value * Mathf.PI * 2f;
    }

    public void Tick(float deltaTime)
    {
        _angle += _data.movementSpeed * deltaTime;
        float radius = Mathf.Max(0.01f, _data.bounceAmount);
        _t.position = _origin + new Vector3(Mathf.Cos(_angle), Mathf.Sin(_angle)) * radius;
    }
}

/// <summary>Shared base for motions that steer relative to the player, found once on Init (same
/// FindWithTag("Player") pattern CTRL_Enemy.cs already uses).</summary>
public abstract class PickupMotion_PlayerRelative : IPickupMotion
{
    protected Transform T;
    protected SO_PickupData Data;
    protected Transform Player;

    public virtual void Init(Transform pickupTransform, SO_PickupData data)
    {
        T = pickupTransform;
        Data = data;

        GameObject playerObj = GameObject.FindWithTag("Player");
        Player = playerObj != null ? playerObj.transform : null;
    }

    public abstract void Tick(float deltaTime);
}

/// <summary>Skitters directly away from the player while they're inside Evade Radius — a pickup
/// that has to be cornered or hit rather than casually walked over.</summary>
public class PickupMotion_Evade : PickupMotion_PlayerRelative
{
    public override void Tick(float deltaTime)
    {
        if (Player == null) return;

        Vector2 toSelf = (Vector2)T.position - (Vector2)Player.position;
        if (toSelf.sqrMagnitude > Data.evadeRadius * Data.evadeRadius) return;

        Vector2 away = toSelf.sqrMagnitude > 0.0001f ? toSelf.normalized : Random.insideUnitCircle.normalized;
        T.position += (Vector3)(away * Data.movementSpeed * deltaTime);
    }
}

/// <summary>Homes straight toward the player at all times — unlike Magnetize (see
/// PickupMagnetize.cs), there's no activation radius; it's always closing distance.</summary>
public class PickupMotion_Chase : PickupMotion_PlayerRelative
{
    public override void Tick(float deltaTime)
    {
        if (Player == null) return;
        T.position = Vector3.MoveTowards(T.position, Player.position, Data.movementSpeed * deltaTime);
    }
}
