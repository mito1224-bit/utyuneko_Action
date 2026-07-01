using System.Collections;
using UnityEngine;

public class HitStoppable : MonoBehaviour
{
    public bool IsHitStopping { get; private set; } = false;

    private Rigidbody2D rb2D;
    private Animator anim;

    private Vector2 savedVelocity;
    private float savedGravityScale;
    private float savedAnimSpeed;

    void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    /// <summary>
    /// 自分自身のヒットストップ
    /// </summary>
    public void TriggerHitStop(float duration)
    {
        if (IsHitStopping) return;
        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        IsHitStopping = true;

        if (rb2D != null)
        {
            savedVelocity = rb2D.linearVelocity;
            rb2D.linearVelocity = Vector2.zero;
            savedGravityScale = rb2D.gravityScale;
            rb2D.gravityScale = 0f;
        }

        if (anim != null)
        {
            savedAnimSpeed = anim.speed;
            anim.speed = 0f;
        }

        yield return new WaitForSecondsRealtime(duration);

        if (anim != null) anim.speed = savedAnimSpeed;
        if (rb2D != null)
        {
            rb2D.linearVelocity = savedVelocity;
            rb2D.gravityScale = savedGravityScale;
        }

        IsHitStopping = false;
    }
}