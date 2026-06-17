using UnityEngine;

public class HosaController : MonoBehaviour
{
    private IHosaState currentState;

    //  プレイヤーと同じく、各ステートのインスタンスをキャッシュしておく
    public HosaState_Event StateEvent { get; private set; }
    public HosaState_Follow StateFollow { get; private set; }

    // 各種コンポーネントの隠しプロパティ
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
}