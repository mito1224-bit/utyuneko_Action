using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class HosaController : MonoBehaviour, IEventActor
{
    private IHosaState currentState;

    public HosaState_Event StateEvent { get; private set; }
    public HosaState_Follow StateFollow { get; private set; }

    [HideInInspector] public Rigidbody2D rb2D;
    [HideInInspector] public Animator anim;
    [HideInInspector] public Transform playerTransform;

    [Header("✨ 常時浮遊設定")]
    [SerializeField] private float hoverSpeed = 2.2f;
    [SerializeField] private float hoverAmount = 0.15f;

    private Coroutine hosaReactionCoroutine;

    void Awake()
    {
        StateEvent = new HosaState_Event();
        StateFollow = new HosaState_Follow();
    }

    void Start()
    {
        rb2D = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;

        TransitionToState(StateEvent);
    }

    void Update()
    {
        currentState?.UpdateState();

        // 👑【浮遊ロジック】Reaction中でなければ、見た目(anim.transform)だけをふわふわさせる
        if (hosaReactionCoroutine == null && anim != null)
        {
            float hoverY = Mathf.Sin(Time.time * hoverSpeed) * hoverAmount;
            anim.transform.localPosition = new Vector3(0f, hoverY, 0f);
        }
    }

    void FixedUpdate() => currentState?.FixedUpdateState();

    public void TransitionToState(IHosaState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter(this);
    }

    // ===================================================================
    // 🎭 感情表現コルーチン群 (ReactionWrapper経由で実行)
    // ===================================================================
    public void PlayReaction(ImageBubble.StampType type, float duration = 2.0f)
    {
        if (hosaReactionCoroutine != null) StopCoroutine(hosaReactionCoroutine);
        if (anim != null) { anim.transform.localPosition = Vector3.zero; anim.transform.localRotation = Quaternion.identity; }
        
        hosaReactionCoroutine = StartCoroutine(ReactionWrapper(type, duration));
    }

    private IEnumerator ReactionWrapper(ImageBubble.StampType type, float duration)
    {
        // 演出ごとのコルーチンを実行
        switch (type)
        {
            case ImageBubble.StampType.OK:
            case ImageBubble.StampType.Maru:
                yield return StartCoroutine(HosaBobbingLocalRoutine(0.4f, 25f, 0.2f));
                break;

            case ImageBubble.StampType.Question:
            case ImageBubble.StampType.Hatena:
                // ローカル関数として定義していたロジックをそのまま実行
                float t = 0f;
                Transform visual = anim != null ? anim.transform : transform;
                Quaternion origRot = visual.localRotation;
                while (t < 0.3f) { t += Time.deltaTime; visual.Rotate(Vector3.forward, 60f * Time.deltaTime); yield return null; }
                yield return new WaitForSeconds(0.2f);
                t = 0f;
                while (t < 0.3f) { t += Time.deltaTime; visual.localRotation = Quaternion.Slerp(visual.localRotation, origRot, t / 0.3f); yield return null; }
                visual.localRotation = origRot;
                break;

            case ImageBubble.StampType.Surprise:
            case ImageBubble.StampType.Denger:
            case ImageBubble.StampType.Enemy:
                yield return StartCoroutine(HosaSurpriseJumpLocalRoutine(0.35f, 1.0f, 0.08f));
                break;

            case ImageBubble.StampType.Doya:
                yield return StartCoroutine(HosaBobbingLocalRoutine(0.5f, 8f, 0.05f));
                break;

            case ImageBubble.StampType.Sweat:
            case ImageBubble.StampType.Confusion:
            case ImageBubble.StampType.Dokuro:
                yield return StartCoroutine(HosaTwitchLocalRoutine(0.8f, 0.12f));
                break;

            case ImageBubble.StampType.Joy:
                yield return StartCoroutine(HosaBobbingLocalRoutine(0.8f, 20f, 0.3f));
                break;

            case ImageBubble.StampType.Star:
                yield return StartCoroutine(HosaSpinLocalRoutine(0.4f, 1080f));
                break;

            case ImageBubble.StampType.Go:
            case ImageBubble.StampType.Right:
            case ImageBubble.StampType.Left:
                float slideDir = (type == ImageBubble.StampType.Left) ? -0.8f : 0.8f;
                yield return StartCoroutine(HosaSlideLocalRoutine(slideDir, 0.3f));
                break;

            case ImageBubble.StampType.Batu:
                yield return StartCoroutine(HosaBobbingLocalRoutine(0.5f, 10f, -0.25f));
                break;

            default:
                // 何もしない場合でも念のため待機させる
                yield return new WaitForSeconds(duration);
                break;
        }

        // 👑【超重要】必ず最後にこの行を通るようにする
        hosaReactionCoroutine = null;
        yield return null;
    }

    private IEnumerator HosaBobbingLocalRoutine(float duration, float speed, float amount)
    {
        Transform visual = anim != null ? anim.transform : transform;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offsetY = Mathf.Sin(elapsed * speed) * amount;
            visual.localPosition = new Vector3(0f, offsetY, 0f);
            yield return null;
        }
        visual.localPosition = Vector3.zero;
    }

    private IEnumerator HosaSpinLocalRoutine(float duration, float speed)
    {
        Transform visual = anim != null ? anim.transform : transform;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            visual.Rotate(Vector3.up, speed * Time.deltaTime);
            yield return null;
        }
        float lerpT = 0f;
        Quaternion startRot = visual.localRotation;
        while (lerpT < 1f)
        {
            lerpT += Time.deltaTime * 5f;
            visual.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, Mathf.Clamp01(lerpT));
            yield return null;
        }
        visual.localRotation = Quaternion.identity;
    }

    private IEnumerator HosaSurpriseJumpLocalRoutine(float duration, float height, float twitchMagnitude)
    {
        Transform visual = anim != null ? anim.transform : transform;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float arcY = Mathf.Sin(t * Mathf.PI) * height;
            float twitchX = Random.Range(-twitchMagnitude, twitchMagnitude);
            float twitchY = Random.Range(-twitchMagnitude, twitchMagnitude);
            visual.localPosition = new Vector3(twitchX, arcY + twitchY, 0f);
            yield return null;
        }
        visual.localPosition = Vector3.zero;
    }

    private IEnumerator HosaTwitchLocalRoutine(float duration, float magnitude)
    {
        Transform visual = anim != null ? anim.transform : transform;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offsetX = Random.Range(-magnitude, magnitude);
            float offsetY = Random.Range(-magnitude, magnitude);
            visual.localPosition = new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }
        visual.localPosition = Vector3.zero;
    }

    private IEnumerator HosaSlideLocalRoutine(float dirX, float duration)
    {
        Transform visual = anim != null ? anim.transform : transform;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.Sin(t * Mathf.PI);
            visual.localPosition = new Vector3(dirX * smoothT, 0f, 0f);
            yield return null;
        }
        visual.localPosition = Vector3.zero;
    }
}