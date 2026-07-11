using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class HosaController : MonoBehaviour, IEventActor
{
    private IHosaState currentState;

    //  プレイヤーと同じく、各ステートのインスタンスをキャッシュしておく
    public HosaState_Event StateEvent { get; private set; }
    public HosaState_Follow StateFollow { get; private set; }

    // 各種コンポーネントの隠しプロパティ
    [HideInInspector] public Rigidbody2D rb2D;
    [HideInInspector] public Animator anim;
    [HideInInspector] public Transform playerTransform;

    void Awake()
    {
        // ステートの生成
        StateEvent = new HosaState_Event();
        StateFollow = new HosaState_Follow();
    }

    void Start()
    {
        rb2D = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        // 最初のステート（イベント状態）へ遷移
        TransitionToState(StateEvent);
    }

    void Update()
    {
        // 現在のステートの Update を実行
        currentState?.UpdateState();
    }

    void FixedUpdate()
    {
        // 現在のステートの FixedUpdate を実行
        currentState?.FixedUpdateState();
    }

    /// <summary>
    /// 状態を切り替えるメイン関数（PlayerController の TransitionToState と同じ仕様）
    /// </summary>
    public void TransitionToState(IHosaState newState)
    {
        if (currentState != null)
        {
            currentState.Exit();
        }

        currentState = newState;
        currentState.Enter(this);
    }

    // ===================================================================
    // 🎭【新設】アニメーション不要！空中浮遊を活かした補佐の感情表現コルーチン
    // ===================================================================
    private Coroutine hosaReactionCoroutine;

    // 💡 見た目だけをサイン波で心地よく上下に揺らす（ローカル座標）
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
        visual.localPosition = Vector3.zero; // 終わったら必ず真芯にリセット
    }

    // 💡【復活＆超強化！】見た目の子オブジェクトを高速スピンさせ、最後はヌルッと滑らかに正面に戻すコルーチン！
    private IEnumerator HosaSpinLocalRoutine(float duration, float speed)
    {
        Transform visual = anim != null ? anim.transform : transform;
        float elapsed = 0f;

        // 1. 🌀 まずは指定時間の間、猛スピードでくるくる回転させる！
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            visual.Rotate(Vector3.up, speed * Time.deltaTime);
            yield return null;
        }

        // 2. ⏳【不自然さ解消】一瞬でパチッと戻るのではなく、0.2秒かけて滑らかに正面（Quaternion.identity）へ戻す！
        Quaternion startRot = visual.localRotation;
        float lerpT = 0f;
        while (lerpT < 1f)
        {
            lerpT += Time.deltaTime * 5f; // 0.2秒でスッと戻る速度
            visual.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, Mathf.Clamp01(lerpT));
            yield return null;
        }
        visual.localRotation = Quaternion.identity;
    }

    // 💡 見た目の子オブジェクトだけを「上にピョコンと跳ねさせつつ、ガタガタ震わせる」ブレンドコルーチン
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

    // 💡 見た目だけをその場で激しくブルブル震わせる
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

    // 💡 進む方向（左右）に向かってスッと残像風にスライドして戻る（ローカル座標）
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

    /// <summary>
    /// 💡【コンパイルエラー修正完了版】すべてのコルーチンが揃った鉄壁の感情表現窓口
    /// </summary>
    public void PlayReaction(ImageBubble.StampType type, float duration = 2.0f)
    {
        if (hosaReactionCoroutine != null)
        {
            StopCoroutine(hosaReactionCoroutine);
            hosaReactionCoroutine = null;
        }

        if (rb2D != null) rb2D.linearVelocity = Vector2.zero;

        // 割り込まれた瞬間に見た目の座標・回転を一瞬で綺麗に初期化
        if (anim != null)
        {
            anim.transform.localPosition = Vector3.zero;
            anim.transform.localRotation = Quaternion.identity;
        }

        switch (type)
        {
            // OK / 丸：空中で「ぴょこっぴょこっ！」と縦に2回バウンド
            case ImageBubble.StampType.OK:
            case ImageBubble.StampType.Maru:
                hosaReactionCoroutine = StartCoroutine(HosaBobbingLocalRoutine(0.4f, 25f, 0.2f));
                break;

            // 疑問 / はてな：不思議そうに空中を「ゆらーーり」と首をかしげる
            case ImageBubble.StampType.Question:
            case ImageBubble.StampType.Hatena:
                hosaReactionCoroutine = StartCoroutine(FuncHosaTilt());
                IEnumerator FuncHosaTilt()
                {
                    float t = 0f;
                    Transform visual = anim != null ? anim.transform : transform;
                    Quaternion origRot = visual.localRotation;
                    while (t < 0.3f) { t += Time.deltaTime; visual.Rotate(Vector3.forward, 60f * Time.deltaTime); yield return null; }
                    yield return new WaitForSeconds(0.2f);
                    t = 0f;
                    while (t < 0.3f) { t += Time.deltaTime; visual.localRotation = Quaternion.Slerp(visual.localRotation, origRot, t / 0.3f); yield return null; }
                    visual.localRotation = origRot;
                }
                break;

            // 驚き / 危険 / 敵：見た目だけを「ビクッ！」と上に跳ね上げつつ激しく身震い
            case ImageBubble.StampType.Surprise:
            case ImageBubble.StampType.Denger:
            case ImageBubble.StampType.Enemy:
                hosaReactionCoroutine = StartCoroutine(HosaSurpriseJumpLocalRoutine(0.35f, 1.0f, 0.08f));
                break;

            // どや顔：フフン、と空中でピタッと静止して微小ホバー
            case ImageBubble.StampType.Doya:
                hosaReactionCoroutine = StartCoroutine(HosaBobbingLocalRoutine(0.5f, 8f, 0.05f));
                break;

            // 悲しい / 混乱 / どくろ：その場で「ガガガガガッ！」と激しくホバーパニック振動
            case ImageBubble.StampType.Sweat:
            case ImageBubble.StampType.Confusion:
            case ImageBubble.StampType.Dokuro:
                hosaReactionCoroutine = StartCoroutine(HosaTwitchLocalRoutine(0.8f, 0.12f));
                break;

            // 喜ぶ：空中大はしゃぎ縦ピョンピョン
            case ImageBubble.StampType.Joy:
                hosaReactionCoroutine = StartCoroutine(HosaBobbingLocalRoutine(0.8f, 20f, 0.3f));
                break;

            // ⭕️【エラー解消！】新設したローカル座標＆滑らかリセット版のスピンを呼び出す！
            case ImageBubble.StampType.Star:
                hosaReactionCoroutine = StartCoroutine(HosaSpinLocalRoutine(0.4f, 1080f));
                break;

            // 行こう / 右 / 左：進む方向へスッとスマートにスライドして戻る
            case ImageBubble.StampType.Go:
            case ImageBubble.StampType.Right:
            case ImageBubble.StampType.Left:
                float slideDir = (type == ImageBubble.StampType.Left) ? -0.8f : 0.8f;
                hosaReactionCoroutine = StartCoroutine(HosaSlideLocalRoutine(slideDir, 0.3f));
                break;

            // バツ：「ふにゃん…」と力なく沈み込む
            case ImageBubble.StampType.Batu:
                hosaReactionCoroutine = StartCoroutine(HosaBobbingLocalRoutine(0.5f, 10f, -0.25f));
                break;
        }
    }
}