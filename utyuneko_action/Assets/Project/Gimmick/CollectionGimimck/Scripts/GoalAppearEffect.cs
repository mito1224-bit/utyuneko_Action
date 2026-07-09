using System.Collections;
using UnityEngine;

public class GoalAppearEffect : MonoBehaviour
{

    [SerializeField] private float appearTime = 0.5f;

    private Vector3 targetScale;

    private void Awake()
    {
        targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    private void Start()
    {
        StartCoroutine(AppearRoutine());
    }

    private IEnumerator AppearRoutine()
    {
        float timer = 0f;

        while (timer < appearTime)
        {
            timer += Time.deltaTime;

            float t = timer / appearTime;

            // Overshoot
            float scale = Mathf.Lerp(
                0f,
                1.2f,
                Mathf.SmoothStep(0f, 1f, t)
            );

            transform.localScale = targetScale * scale;

            yield return null;
        }

        // ­‚µk‚ß‚ÄŠ®¬
        float returnTime = 0.15f;
        timer = 0f;

        Vector3 startScale = targetScale * 1.2f;

        while (timer < returnTime)
        {
            timer += Time.deltaTime;

            float t = timer / returnTime;

            transform.localScale =
                Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        transform.localScale = targetScale;
    }

}
