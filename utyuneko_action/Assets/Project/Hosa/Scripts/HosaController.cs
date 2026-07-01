using UnityEngine;

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

    /// <summary>
    /// 外部（イベントマネージャー）から呼ばれるリアクション窓口
    /// </summary>
    public void PlayReaction(ImageBubble.StampType type, float duration = 2.0f)
    {
        // 種類に応じて「体（アニメーションや物理）」のリアクションだけを自分が担当する
        switch (type)
        {
            case ImageBubble.StampType.OK:
                break;
            case ImageBubble.StampType.Question:
                break;
            case ImageBubble.StampType.Surprise:
                break;
            case ImageBubble.StampType.Doya:
                break;
            case ImageBubble.StampType.Sweat:
                break;
            case ImageBubble.StampType.Hatena:
                break;
            case ImageBubble.StampType.Joy:
                break;
            case ImageBubble.StampType.Gift:
                break;
            case ImageBubble.StampType.Star:
                break;
            case ImageBubble.StampType.Go:
                break;
        }
    }
}