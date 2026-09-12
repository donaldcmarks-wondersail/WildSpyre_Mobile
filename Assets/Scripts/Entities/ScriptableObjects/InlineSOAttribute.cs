using UnityEngine;

/// <summary>
/// Put on a ScriptableObject reference field to draw that asset's own fields
/// inline (expandable) beneath the object slot, so a referenced sub-asset can be
/// edited without selecting it in the Project window.
///
/// Works on a single reference (e.g. <c>SO_EnemyBehavior behavior;</c>) and on a
/// list of references (each element becomes expandable).
/// </summary>
public class InlineSOAttribute : PropertyAttribute { }
