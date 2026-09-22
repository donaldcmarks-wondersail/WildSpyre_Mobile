using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sprite-sheet flipbook animator for a SpriteRenderer — the world-space equivalent of
/// UI/UISpriteAnimation.cs, which does the same thing for a UI Image. Drop this on anything that
/// just needs to cycle through an array of frames at a fixed interval (a gem shimmer, a torch
/// flicker, etc.) without an Animator Controller or a Legacy Animation clip.
///
/// Update()-driven rather than coroutine-driven (coroutines never run outside Play Mode at all)
/// so Editor Preview below actually works: with it on, an editor-only EditorApplication.update
/// hook ticks the animation and repaints the Scene view even while the Editor is stopped.
/// </summary>
[ExecuteAlways]
public class SpriteAnimation : MonoBehaviour
{
    [Tooltip("Auto-filled from this GameObject if left empty.")]
    public SpriteRenderer m_SpriteRenderer;

    public Sprite[] m_SpriteArray;
    [Tooltip("Seconds each frame is held before advancing to the next.")]
    public float m_Speed = .02f;
    public bool m_Loop = true;
    public bool m_PlayOnStart = true;

    [Header("Editor")]
    [Tooltip("Plays the animation in the Scene view while NOT in Play Mode, so you can preview it without pressing Play. Editor-only — has no effect in a build.")]
    public bool m_EditorPreview = false;

    private int m_IndexSprite;
    private float m_Timer;
    private bool m_IsDone;
    private bool m_IsPlaying;

    private void Awake()
    {
        if (m_SpriteRenderer == null)
            m_SpriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.update += EditorTick;
#endif
        if (Application.isPlaying && m_PlayOnStart)
            Func_PlayAnim();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
        m_IsPlaying = false;
    }

    public void Func_PlayAnim()
    {
        m_IsDone = false;
        m_IsPlaying = true;
        m_IndexSprite = 0;
        m_Timer = 0f;
    }

    public void Func_StopAnim()
    {
        m_IsDone = true;
        m_IsPlaying = false;
    }

    private void Update()
    {
        // In Play Mode this is the only driver — the editor hook below explicitly skips
        // ticking while playing, so the two never double-advance the same frame.
        if (Application.isPlaying)
            Tick(Time.deltaTime);
    }

#if UNITY_EDITOR
    private double m_LastEditorTime;

    /// <summary>Editor-only: keeps the animation running (and the Scene view repainting) while
    /// m_EditorPreview is on and the Editor isn't in Play Mode.</summary>
    private void EditorTick()
    {
        if (this == null || Application.isPlaying)
            return;

        double now = EditorApplication.timeSinceStartup;
        float delta = m_LastEditorTime > 0d ? (float)(now - m_LastEditorTime) : 0f;
        m_LastEditorTime = now;

        if (!m_EditorPreview)
        {
            m_IsPlaying = false;
            return;
        }

        if (!m_IsPlaying)
        {
            m_IsDone = false;
            m_IsPlaying = true;
            m_IndexSprite = 0;
            m_Timer = 0f;
        }

        Tick(delta);
        SceneView.RepaintAll();
    }
#endif

    private void Tick(float deltaTime)
    {
        if (!m_IsPlaying || m_IsDone) return;
        if (m_SpriteRenderer == null || m_SpriteArray == null || m_SpriteArray.Length == 0) return;

        m_Timer += deltaTime;
        if (m_Timer < m_Speed) return;
        m_Timer -= m_Speed;

        if (m_IndexSprite >= m_SpriteArray.Length)
        {
            if (!m_Loop)
            {
                m_IsDone = true;
                m_IsPlaying = false;
                return;
            }
            m_IndexSprite = 0;
        }

        m_SpriteRenderer.sprite = m_SpriteArray[m_IndexSprite];
        m_IndexSprite += 1;
    }
}
