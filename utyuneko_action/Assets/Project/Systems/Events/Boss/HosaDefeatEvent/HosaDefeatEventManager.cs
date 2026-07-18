using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 👑 補佐戦・撃破後カットシーンイベントマネージャー（TransitionManager連携版）
/// 補佐とプレイヤーが互いに向き合い、視線を完全に同期させた状態で感動の掛け合いを行います！
/// </summary>
public class HosaDefeatEventManager : BaseEventManager
{
    [Header("😈 ボスの参照（消す対象）")]
    [SerializeField] private GlitchHosaController bossController;

    [Header("👥 補佐(NPC)の参照（出す方）")]
    [SerializeField] private HosaController npcHosa;

    [Header("💬 UI・吹き出し設定")]
    [SerializeField] private ImageBubble playerBubble;
    [SerializeField] private ImageBubble hosaBubble;

    [Header("🎬 タイトル表示設定")]
    [Tooltip("最後に中央に表示させたいタイトル（ロゴ）のCanvasGroup")]
    [SerializeField] private CanvasGroup titleTextGroup;
    [Tooltip("タイトルが完全に表示されてからキープする時間（秒）")]
    [SerializeField] private float titleDisplayDuration = 2.5f;

    [Header("🏎️ クレジットシーン遷移設定")]
    [SerializeField] private string creditSceneName = "CreditScene";

    // 👑【新設】見つめ合い制御用のフラグとパラメータ（開幕マネージャー完全準拠）
    private bool isLookingEachOther = false;
    private float maxLookAngle = 30f;
    private float lookSmoothing = 12.0f;
    private float hosaRightYAngle = 310f;
    private float hosaLeftYAngle = 50f;

    /// <summary>
    /// ボスが撃破され、着地した瞬間に外部（GlitchHosaController等）からキックされるエントリーポイント
    /// </summary>
    public override void OnAreaEntered()
    {
        SoundManager.Instance.PlayBGM(BgmType.Rast, 3.0f);

        // 親クラスの入力ブロック＆UI生成機能を発動
        StartEvent();
        activeTimelineCoroutine = StartCoroutine(DefeatTimelineRoutine());
    }

    // 👑【新設】Update/LateUpdateループで視線追従を滑らかに実行
    private void LateUpdate()
    {
        if (isLookingEachOther)
        {
            KeepLookingAtEachOther();
        }
    }

    /// <summary>
    /// 👑 補佐とプレイヤーがお互いを見つめ合う美麗3D軸補正（開幕イベントから完全移植）
    /// </summary>
    private void KeepLookingAtEachOther()
    {
        if (npcHosa == null || playerTransform == null || playerController == null) return;

        // 補佐 ➔ プレイヤーへの視線計算
        Vector3 dirToPlayer = playerTransform.position - npcHosa.transform.position;
        float targetHosaYAngle = (dirToPlayer.x > 0f) ? hosaRightYAngle : hosaLeftYAngle;

        float hosaAbsX = Mathf.Abs(dirToPlayer.x);
        float hosaAngleX = 0f;
        if (hosaAbsX > 0.01f)
        {
            hosaAngleX = Mathf.Atan2(dirToPlayer.y, hosaAbsX) * Mathf.Rad2Deg;
            hosaAngleX = Mathf.Clamp(hosaAngleX, -maxLookAngle, maxLookAngle);
        }

        Quaternion targetHosaRot = Quaternion.Euler(hosaAngleX, targetHosaYAngle, 0f);
        npcHosa.transform.localRotation = Quaternion.Lerp(npcHosa.transform.localRotation, targetHosaRot, Time.deltaTime * lookSmoothing);

        // プレイヤー ➔ 補佐への視線計算
        if (playerController.visualManager != null && playerController.visualManager.playerVisual != null)
        {
            Vector3 dirToHosa = npcHosa.transform.position - playerTransform.position;
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

    private IEnumerator DefeatTimelineRoutine()
    {
        if (bossController == null || npcHosa == null)
        {
            Debug.LogError("[HosaDefeatEventManager] ボスまたは補佐NPCの参照がアサインされていません！");
            EndEvent();
            yield break;
        }

        // ===================================================================
        // ✨ 1. 画面を真っ白に白飛び（フラッシュイン）
        // ===================================================================
        if (skipFadeCanvasGroup != null)
        {
            Image fadeImage = skipFadeCanvasGroup.GetComponentInChildren<Image>();
            if (fadeImage != null) fadeImage.color = Color.white;
            skipFadeCanvasGroup.blocksRaycasts = true;

            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                skipFadeCanvasGroup.alpha = Mathf.Clamp01(t / 0.5f);
                yield return null;
            }
        }

        // 👑【ユーザー要望①】白飛びしきった状態でちょっと待つ（0.4秒のタメ）
        yield return new WaitForSecondsRealtime(0.4f);

        // --- 白飛びの目隠し中に裏方のバトンタッチ処理 ---
        bossController.gameObject.SetActive(false);
        npcHosa.gameObject.SetActive(true);

        // 👑【ユーザー要望②】補佐のY座標をプレイヤーの高さと完全に同期！
        float targetY = playerTransform != null ? playerTransform.position.y : bossController.transform.position.y;
        npcHosa.transform.position = new Vector3(bossController.transform.position.x, targetY, bossController.transform.position.z);

        npcHosa.TransitionToState(npcHosa.StateEvent); // 直立不動のイベント待ち状態へ

        // 👑 補佐が実体化したので、白画面が晴れる前に見つめ合いシステムを即座に起動！
        isLookingEachOther = true;

        // ===================================================================
        // ✨ 2. 白飛びを徐々に晴れさせる（フラッシュアウト）
        // ===================================================================
        if (skipFadeCanvasGroup != null)
        {
            float t = 0.5f;
            while (t > 0f)
            {
                t -= Time.deltaTime;
                skipFadeCanvasGroup.alpha = Mathf.Clamp01(t / 0.5f);
                yield return null;
            }
            skipFadeCanvasGroup.alpha = 0f;
            skipFadeCanvasGroup.blocksRaycasts = false;
        }

        // ===================================================================
        // ✨ 3. 補佐とプレイヤーの最後の感動スタンプ掛け合い
        // ===================================================================
        yield return new WaitForSecondsRealtime(0.5f);

        // 補佐がぴょんぴょん飛び跳ねて大喜び（Joy）！
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Joy, 1.5f));

        // プレイヤーが「フッ、楽勝だったな」と胸を張る（Doya）！
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Doya, 1.5f));

        // お互いに「お疲れ！」「これからもよろしく！」のハイタッチ協力スタンプ（OK）！
        hosaBubble.ShowStamp(ImageBubble.StampType.OK);
        playerBubble.ShowStamp(ImageBubble.StampType.OK);
        yield return new WaitForSecondsRealtime(2.0f);

        // 吹き出しをスッと綺麗に退場させる
        hosaBubble.StartFadeOut();
        playerBubble.StartFadeOut();
        yield return new WaitForSecondsRealtime(0.3f);

        // タイトル表示フェーズに入るタイミングで、視線追従をOFFにして正面に固定
        isLookingEachOther = false;

        // ===================================================================
        // ✨ 4. タイトルのフェードイン ＆ 一定時間の表示キープ
        // ===================================================================
        if (titleTextGroup != null)
        {
            titleTextGroup.alpha = 0f;
            titleTextGroup.gameObject.SetActive(true);

            float t = 0f;
            float titleFadeInDuration = 0.8f;
            while (t < titleFadeInDuration)
            {
                t += Time.deltaTime;
                titleTextGroup.alpha = Mathf.Clamp01(t / titleFadeInDuration);
                yield return null;
            }
            titleTextGroup.alpha = 1f;
        }

        // 👑 タイトル画面を指定された一定時間そのままじっくり見せる
        yield return new WaitForSecondsRealtime(titleDisplayDuration);

        // ===================================================================
        // 👑 5. TransitionManagerを介して、美しいFadeでクレジットシーンへ移行！
        // ===================================================================
        if (TransitionManager.Instance != null)
        {
            Debug.Log($"<color=green>🎬 補佐戦完結：TransitionManagerを呼び出し、{creditSceneName} へFade遷移します。</color>");
            TransitionManager.Instance.ChangeScene(creditSceneName, TransitionType.Fade);
        }
        else
        {
            Debug.LogWarning("[HosaDefeatEventManager] シーン内に TransitionManager が見つかりません。通常のロードを実行します。");
            UnityEngine.SceneManagement.SceneManager.LoadScene(creditSceneName);
        }

        // イベント制御用のキャンバス群を破棄して正常終了
        EndEvent();
    }

    /// <summary>
    /// イベントが長押しでスキップされた場合のワープ処理
    /// </summary>
    protected override void OnSkipWarp()
    {
        isLookingEachOther = false;
        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.ChangeScene(creditSceneName, TransitionType.Fade);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(creditSceneName);
        }
    }
}