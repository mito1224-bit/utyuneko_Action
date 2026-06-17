using System.Collections;
using UnityEngine;

public class RescueEventManager : MonoBehaviour
{
    public enum RescueState { InDanger, Thanking, Absorbing, Talking, Finished }

    [Header("現在のイベント状態（確認用）")]
    [SerializeField] private RescueState currentState = RescueState.InDanger;
    public RescueState CurrentState => currentState;

    [Header("登場オブジェクトの設定")]
    [SerializeField] private HosaController hosa;

    // お互いの頭上につけた TextBubble の参照をセットしてください
    [SerializeField] private TextBubble hosaBubble;
    [SerializeField] private TextBubble playerBubble;

    [Header("補佐の移動スピード")]
    [SerializeField] private float hosaMoveSpeed = 5f;

    private float maxLookAngle = 30f;
    private float lookSmoothing = 12.0f;

    private float hosaInDangerYAngle = 180f;
    private float hosaRightYAngle = 310f;
    private float hosaLeftYAngle = 50f;

    private EventEnemy targetEnemy;
    private Transform playerTransform;
    private PlayerController playerController;
    private Vector3 hosaFloorPosition;

    void Start()
    {
        currentState = RescueState.InDanger;

        if (hosa != null)
        {
            hosaFloorPosition = hosa.transform.position;
            hosa.transform.localRotation = Quaternion.Euler(0f, hosaInDangerYAngle, 0f);
            hosa.TransitionToState(hosa.StateEvent);
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    void LateUpdate()
    {
        if (currentState != RescueState.InDanger && currentState != RescueState.Finished)
        {
            KeepLookingAtEachOther();
        }
    }

    /// <summary>
    /// 毎フレーム、イベントの状態に応じてお互いの位置を滑らかにロックオンし続ける関数
    /// </summary>
    private void KeepLookingAtEachOther()
    {
        if (hosa == null || playerTransform == null || playerController == null) return;

        // ==========================================
        // 👁️ 1. 補佐（Hosa）の視線・向き制御
        // ==========================================

        // 💡【ここが今回の最大のポイント！】
        // 現在の状態が「吸い込み移動中〜吸引完了まで」かつ敵が存在するなら、ターゲットを敵に切り替える！
        if (currentState == RescueState.Absorbing && targetEnemy != null)
        {
            // 🎯 敵への方向ベクトルを計算
            Vector3 dirToEnemy = targetEnemy.transform.position - hosa.transform.position;

            // 敵が右にいれば右向き(310度)、左にいれば左向き(50度)を目標にする
            float targetHosaYAngle = (dirToEnemy.x > 0f) ? hosaRightYAngle : hosaLeftYAngle;

            // 🛑【上下は加味しない】ので、上下の目標X角度は「常に 0f（水平）」に固定！
            float hosaAngleX = 0f;

            // 敵へ向かう目標回転を作成
            Quaternion targetHosaRot = Quaternion.Euler(hosaAngleX, targetHosaYAngle, 0f);

            // 敵の方へクルッと滑らかに回転させる
            hosa.transform.localRotation = Quaternion.Lerp(
                hosa.transform.localRotation,
                targetHosaRot,
                Time.deltaTime * lookSmoothing
            );
        }
        else
        {
            // 🚶【それ以外のフェーズ（お礼や会話など）】は今まで通りプレイヤーを上下左右ロックオン！
            Vector3 dirToPlayer = playerTransform.position - hosa.transform.position;
            float targetHosaYAngle = (dirToPlayer.x > 0f) ? hosaRightYAngle : hosaLeftYAngle;

            float hosaAbsX = Mathf.Abs(dirToPlayer.x);
            float hosaAngleX = 0f;
            if (hosaAbsX > 0.01f)
            {
                hosaAngleX = Mathf.Atan2(dirToPlayer.y, hosaAbsX) * Mathf.Rad2Deg;
                hosaAngleX = Mathf.Clamp(hosaAngleX, -maxLookAngle, maxLookAngle);
            }

            Quaternion targetHosaRot = Quaternion.Euler(hosaAngleX, targetHosaYAngle, 0f);
            hosa.transform.localRotation = Quaternion.Lerp(hosa.transform.localRotation, targetHosaRot, Time.deltaTime * lookSmoothing);
        }

        // ==========================================
        // 👁️ 2. プレイヤー（dB君）から補佐への滑らかなロックオン
        // ==========================================
        if (playerController.visualManager != null && playerController.visualManager.playerVisual != null)
        {
            Vector3 dirToHosa = hosa.transform.position - playerTransform.position;
            float playerDirX = dirToHosa.x > 0 ? 1f : -1f;
            float targetPlayerYAngle = (playerDirX > 0f) ? 310f : 50f;

            float playerAbsX = Mathf.Abs(dirToHosa.x);
            float playerAngleX = 0f;
            if (playerAbsX > 0.01f)
            {
                playerAngleX = Mathf.Atan2(dirToHosa.y, playerAbsX) * Mathf.Rad2Deg;
                playerAngleX = Mathf.Clamp(playerAngleX, -maxLookAngle, maxLookAngle);
            }

            Quaternion targetPlayerRot = Quaternion.Euler(playerAngleX, targetPlayerYAngle, 0f);
            playerController.visualManager.playerVisual.localRotation = Quaternion.Lerp(
                playerController.visualManager.playerVisual.localRotation,
                targetPlayerRot,
                Time.deltaTime * lookSmoothing
            );
        }
    }


    public void OnEnemyDefeated(EventEnemy enemy)
    {
        if (currentState == RescueState.InDanger)
        {
            targetEnemy = enemy;
            currentState = RescueState.Thanking;

            if (playerController != null && playerController.inputActions != null)
            {
                playerController.inputActions.Player.Disable();
                playerController.TransitionToState(playerController.StateNormal);
            }

            StartCoroutine(RescueEventTimelineRoutine());
        }
    }

    /// <summary>
    /// 💡【新設】指定した吹き出しにセリフを表示し、プレイヤーがボタンを押して次に進むのを待つ便利な関数
    /// </summary>
    private IEnumerator Speak(TextBubble bubble, string text)
    {
        bubble.DisplayText(text);

        // 1フレーム待って、前のフレームのボタン入力をリセット
        yield return null;

        // プレイヤーが「スペースキー」または「マウス左クリック」を押すまでループして待機
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                // もし文字がまだタイピング途中なら、一瞬で全文字表示してあげる（親切設計）
                if (bubble.IsTyping)
                {
                    bubble.CompleteTextImmediately(text);
                    yield return null; // 決定の連打で次のセリフに暴発するのを防ぐために1コマ待つ
                }
                else
                {
                    // 文字が出きった状態でボタンが押されたら、このセリフを終了して次へ！
                    break;
                }
            }
            yield return null;
        }

        bubble.CloseBubble(); // 吹き出しを閉じる
    }

    /// <summary>
    /// ✨ SANABI風に生まれ変わったドラマチック・タイムライン！
    /// </summary>
    private IEnumerator RescueEventTimelineRoutine()
    {
        // ==========================================
        // 🎬 1. 補佐救出して補佐がお礼を言う
        // ==========================================
        Debug.Log("補佐：お礼");
        yield return StartCoroutine(Speak(hosaBubble, "ひゃああっ！ ……あ、助けていただき、ありがとうございます！"));

        // ==========================================
        // 🎬 2. 補佐が敵に近づいていくことを主人公は疑問に思う
        // ==========================================
        // 移動フラグを立てる（LateUpdate が自動で敵ロックオンに切り替えてくれます！）
        currentState = RescueState.Absorbing;

        // 主人公が「？」を出す（補佐は移動開始）
        Coroutine playerQuestion = StartCoroutine(Speak(playerBubble, "……？"));

        if (targetEnemy != null && hosa != null)
        {
            Vector3 targetPosition = targetEnemy.transform.position + Vector3.up * 1.5f;
            while (Vector3.Distance(hosa.transform.position, targetPosition) > 0.05f)
            {
                hosa.transform.position = Vector3.MoveTowards(hosa.transform.position, targetPosition, hosaMoveSpeed * Time.deltaTime);
                yield return null;
            }
            hosa.transform.position = targetPosition;
        }

        // 主人公がボタンを押して「？」を閉じるのを待つ
        yield return playerQuestion;

        // ==========================================
        // 🎬 3. 敵が補佐に吸い込まれて行って主人公はびっくりする
        // ==========================================
        float shrinkTime = 1.0f;
        if (targetEnemy != null && hosa != null) targetEnemy.StartAbsorb(hosa.transform, shrinkTime);

        if (playerController != null)
        {
            playerController.PlayReaction(PlayerVisualManager.ReactionType.Surprise);
        }

        // 吸い込みアニメ中に主人公が「！！」と驚く
        yield return StartCoroutine(Speak(playerBubble, "！！"));
        yield return new WaitForSeconds(0.5f); // 吸い込み完了の余韻

        // ==========================================
        // 🎬 4. 補佐が降りてきて自慢げにする
        // ==========================================
        if (hosa != null)
        {
            Vector3 targetDropPosition = new Vector3(hosa.transform.position.x, hosaFloorPosition.y, hosa.transform.position.z);
            while (Vector3.Distance(hosa.transform.position, targetDropPosition) > 0.05f)
            {
                hosa.transform.position = Vector3.MoveTowards(hosa.transform.position, targetDropPosition, hosaMoveSpeed * Time.deltaTime);
                yield return null;
            }
            hosa.transform.position = targetDropPosition;
        }

        // 状態を会話モードに戻す（LateUpdate がお互いロックオンに戻してくれます！）
        currentState = RescueState.Talking;
        yield return StartCoroutine(Speak(hosaBubble, "ふふん、どうです？ 私だって、ただ守られてるだけじゃないんですよ！"));

        // ==========================================
        // 🎬 5. 補佐が今データ世界はバグに支配されてることを説明する
        // ==========================================
        // Rich Textタグを使って、バグの文字を赤くガタガタ震わせる（TextMeshProの神機能！）
        yield return StartCoroutine(Speak(hosaBubble, "……冗談はさておき。いま、このデータ世界は恐ろしい <color=red><shake>【バグ】</shake></color> に支配されかけています。"));

        // ==========================================
        // 🎬 6. 主人公が協力を申し出る
        // ==========================================
        yield return StartCoroutine(Speak(playerBubble, "（……静かに拳を握り、補佐を見つめる）"));

        // ==========================================
        // 🎬 7. 補佐が喜ぶ
        // ==========================================
        yield return StartCoroutine(Speak(hosaBubble, "えっ……？ 一緒に戦ってくれるんですか……！？ やったあぁ！"));

        // ==========================================
        // 🎬 8. 補佐がプレゼントをくれる ➔ 9. 主人公の疑問
        // ==========================================
        yield return StartCoroutine(Speak(hosaBubble, "それなら、あなたにこれを受け取ってほしいです！"));
        yield return StartCoroutine(Speak(playerBubble, "（データ容量拡張ドライブを手に入れた！ ……これなんだろう？）"));

        // ==========================================
        // 🎬 10. もらって喜ぶ ➔ 11. よし行こうと合図
        // ==========================================
        yield return StartCoroutine(Speak(hosaBubble, "これを使えば、あなたのバーストの威力がさらに上がります！相棒、よろしく頼みます！"));
        yield return StartCoroutine(Speak(playerBubble, "（……心強い相棒ができた！ よし、行こう！）"));

        // ==========================================
        // 🎬 12. 補佐も行こうと返してイベント終了
        // ==========================================
        yield return StartCoroutine(Speak(hosaBubble, "はい！ 私のナビゲート、期待してくださいね！"));

        CompleteEvent();
    }

    private void CompleteEvent()
    {
        currentState = RescueState.Finished;
        Debug.Log("イベント完了！");

        if (playerController != null && playerController.inputActions != null)
        {
            playerController.inputActions.Player.Enable();
        }

        if (hosa != null)
        {
            hosa.transform.localRotation = Quaternion.identity;
            hosa.TransitionToState(hosa.StateFollow); // 🔓 吸い取った場所からフワッとプレイヤーを追尾！
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvanceStoryPhase();
        }
    }
}