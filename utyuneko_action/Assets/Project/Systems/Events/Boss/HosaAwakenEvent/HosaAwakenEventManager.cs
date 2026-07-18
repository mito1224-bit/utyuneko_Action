using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 👑 補佐戦・開幕カットシーンイベントマネージャー（完全シークエンス演出版）
/// プレイヤーと補佐のスタンプによる完璧な掛け合いドラマからボスの叫び、戦闘開始までを完全支配！
/// </summary>
public class HosaAwakenEventManager : BaseEventManager
{
    [Header("👥 1. 道中用補佐コントローラー（動かす対象）")]
    [SerializeField] private HosaController hosa;

    [Header("😈 2. 戦闘用・バトル用のボス補佐オブジェクト（出す方）")]
    [SerializeField] private GlitchHosaController bossController;

    [Header("📊 3. UI・環境設定")]
    [SerializeField] private GameObject hpBarObject;
    [SerializeField] private GameObject bossWallObject;

    [Header("💬 4. 掛け合い用スタンプバブル設定")]
    [Tooltip("プレイヤーのオブジェクト直下にあるImageBubbleをセットしてね")]
    [SerializeField] private ImageBubble playerBubble;
    [Tooltip("道中用補佐NPC（hosa）の直下にあるImageBubbleをセットしてね")]
    [SerializeField] private ImageBubble bossBubble;

    [Header("🎥 5. カメラズーム設定")]
    [Tooltip("ズームイン時のカメラのサイズ（数値を小さくするほどドアップになります。例: 3.2）")]
    [SerializeField] private float cameraZoomSize = 3.2f;
    [SerializeField] private CameraBoundsTrigger stageCamera;

    [Header("🎬 6. 演出用タイムパラメータ")]
    [SerializeField] private float struggleDuration = 2.0f;       // 混乱して苦しむ時間
    [SerializeField] private float whiteFlashInDuration = 0.25f;   // 白飛びする速度
    [SerializeField] private float whiteFlashHoldDuration = 0.4f;  // 真っ白のまま維持する時間

    private CameraFollowWithZoom cameraFollow;
    private bool isStarted = false;
    private bool isLookingEachOther = false;

    // 見つめ合い用の立体角度パラメータ（ソース14完全準拠）
    private float maxLookAngle = 30f;
    private float lookSmoothing = 12.0f;
    private float hosaRightYAngle = 310f;
    private float hosaLeftYAngle = 50f;

    protected override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        cameraFollow = FindFirstObjectByType<CameraFollowWithZoom>();
    }

    /// <summary>
    /// EventTriggerArea2D（汎用トリガー）から呼び出される共通エントリポイント
    /// </summary>
    public override void OnAreaEntered()
    {
        if (isStarted) return;
        isStarted = true;

        // 親クラスのベース機能（UI生成やプレイヤーの入力ブロックなど）を発動
        StartEvent();

        // 道中BGMを1秒かけてフェードアウトストップ
        SoundManager.Instance.StopBGM(1.0f);

        // 覚醒カットシーンのメインタイムラインコルーチンをキック
        activeTimelineCoroutine = StartCoroutine(HosaAwakenTimelineRoutine());
    }

    void LateUpdate()
    {
        if (isLookingEachOther)
        {
            KeepLookingAtEachOther();
        }
    }

    /// <summary>
    /// 補佐とプレイヤーがお互いを見つめ合う美麗3D軸補正（ソース14完全移植）
    /// </summary>
    private void KeepLookingAtEachOther()
    {
        if (hosa == null || playerTransform == null || playerController == null) return;

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

    /// <summary>
    /// 🎬 ユーザー要望のタイムラインを100%完全再現したメインコルーチン
    /// </summary>
    private IEnumerator HosaAwakenTimelineRoutine()
    {
        if (hosa == null || bossController == null)
        {
            Debug.LogError("イベントマネージャーに hosa(NPC) または bossController がセットされていません！");
            EndEvent();
            yield break;
        }

        // ===================================================================
        // 🎬 1. プレイヤーが補佐を見つけて、不思議がる
        // ===================================================================
        isLookingEachOther = true;
        hosa.TransitionToState(hosa.StateEvent);

        if (stageCamera) stageCamera.gameObject.SetActive(false); // カメラ境界一時解除

        if (cameraFollow != null)
        {
            cameraFollow.StartTrackTarget(hosa.transform, cameraZoomSize, 1.0f); // 補佐へズームイン
        }
        yield return new WaitForSecondsRealtime(0.3f);

        // プレイヤーが頭上に「はてな（Hatena）」を出して不思議がる
        if (playerBubble != null)
        {
            playerBubble.ShowStamp(ImageBubble.StampType.Question);
        }
        yield return new WaitForSecondsRealtime(1.0f);
        if (playerBubble != null) playerBubble.StartFadeOut();
        yield return new WaitForSecondsRealtime(0.2f);

        // ===================================================================
        // 💀 2. 補佐が危険マークやどくろマークを出して警告する
        // ===================================================================
        if (bossBubble != null) bossBubble.ShowStamp(ImageBubble.StampType.Denger); // 危険！
        yield return new WaitForSecondsRealtime(0.8f);

        if (bossBubble != null) bossBubble.ShowStamp(ImageBubble.StampType.Dokuro); // どくろ！
        yield return new WaitForSecondsRealtime(0.8f);

        // ===================================================================
        // ⚡ 3. 補佐が混乱してくる ＆ 4. プレイヤーが驚く
        // ===================================================================
        if (bossBubble != null) bossBubble.ShowStamp(ImageBubble.StampType.Confusion); // 混乱！
        hosa.PlayReaction(ImageBubble.StampType.Confusion, struggleDuration);

        float elapsed = 0f;
        Vector3 npcOrigPos = hosa.transform.position;
        Vector3 npcOrigScale = hosa.transform.localScale;
        bool isPlayerShocked = false;

        while (elapsed < struggleDuration)
        {
            elapsed += Time.deltaTime;

            // マネージャー側からも重ねて、道中NPCを激しくエラーパニック振動させる
            float shakeX = Random.Range(-0.35f, 0.35f);
            float shakeY = Random.Range(-0.15f, 0.15f);
            hosa.transform.position = npcOrigPos + new Vector3(shakeX, shakeY, 0f);

            float noiseScaleX = Random.Range(0.8f, 1.2f);
            float noiseScaleY = Random.Range(0.8f, 1.2f);
            hosa.transform.localScale = new Vector3(npcOrigScale.x * noiseScaleX, npcOrigScale.y * noiseScaleY, npcOrigScale.z);

            // 👑【時間差連動】補佐がバグり始めて0.5秒後、それを見たプレイヤーが驚く（Surprise）！
            if (elapsed >= 0.5f && !isPlayerShocked)
            {
                isPlayerShocked = true;
                if (playerBubble != null)
                {
                    playerBubble.ShowStamp(ImageBubble.StampType.Surprise);
                }
            }
            yield return null;
        }

        // 座標・スケール復旧
        if (hosa != null) { hosa.transform.position = npcOrigPos; hosa.transform.localScale = npcOrigScale; }
        if (bossBubble != null) bossBubble.StartFadeOut();
        if (playerBubble != null) playerBubble.StartFadeOut();
        yield return new WaitForSecondsRealtime(0.2f);

        // ===================================================================
        // ⚡ 5. 白飛びしてボスが出現
        // ===================================================================
        SoundManager.Instance.PlayBGM(BgmType.BossBattle, 1.0f);

        if (skipFadeCanvasGroup != null)
        {
            Image fadeImage = skipFadeCanvasGroup.GetComponentInChildren<Image>();
            if (fadeImage != null) fadeImage.color = Color.white;
            skipFadeCanvasGroup.blocksRaycasts = true;
        }

        float flashTimer = 0f;
        while (flashTimer < whiteFlashInDuration)
        {
            flashTimer += Time.deltaTime;
            if (skipFadeCanvasGroup != null) skipFadeCanvasGroup.alpha = Mathf.Clamp01(flashTimer / whiteFlashInDuration);
            yield return null;
        }
        if (skipFadeCanvasGroup != null) skipFadeCanvasGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(whiteFlashHoldDuration);

        // --- 🤍 白飛びの目隠し中の裏方バトンタッチ処理 ---
        isLookingEachOther = false;
        if (hosa != null) hosa.gameObject.SetActive(false); // 道中NPCを完全消去

        if (bossWallObject != null) bossWallObject.SetActive(true);
        if (hpBarObject != null) hpBarObject.SetActive(true);

        // 戦闘用ボスの座標を空中センターへジャスト配置
        float centerX = (bossController.stageMinX + bossController.stageMaxX) / 2f;
        float centerY = Mathf.Lerp(bossController.stageMinY, bossController.stageMaxY, 0.58f);
        bossController.transform.position = new Vector3(centerX, centerY, 0f);

        bossController.gameObject.SetActive(true);
        bossController.TransitionToState(bossController.StateAppear); // 出現ステートへロック

        if (cameraFollow != null)
        {
            cameraFollow.StartTrackTarget(bossController.transform, cameraZoomSize, 0.1f); // 補佐へズームイン
        }

        // 👑 ボスの出現スクワッシュ弾性アニメーションをここで完全同期再生！
        Vector3 bossOrigScale = bossController.originalVisualLocalScale;
        bossController.targetScale = new Vector3(0.01f, bossOrigScale.y * 2.6f, bossOrigScale.z); // 極細スタート

        yield return new WaitForSecondsRealtime(0.1f);
        SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
        ShakeTarget.Instance.Shake(0.5f, 4.0f);

        // HPバーUIを「グググォォン！」と満タンチャージ起動
        var bossHealth = bossController.GetComponent<GlitchHosaHealth>();
        if (bossHealth != null && bossHealth.bossHpBar != null)
        {
            bossHealth.bossHpBar.StartAppearAnimation(bossHealth.maxHP, bossHealth.maxHP);
        }

        // 白フェードアウト（晴れ渡る）と同時にボスがビヨヨンと弾性展開！
        float t = 0f;
        float animDuration = 0.4f;
        while (t < animDuration)
        {
            t += Time.deltaTime;
            float ratio = t / animDuration;
            float smoothRatio = Mathf.Sin(ratio * Mathf.PI * 1.5f) * Mathf.Exp(-ratio * 3f);

            float bounceX = Mathf.Lerp(bossOrigScale.x, bossOrigScale.x * 1.9f, smoothRatio);
            float bounceY = Mathf.Lerp(bossOrigScale.y, bossOrigScale.y * 0.3f, smoothRatio);
            bossController.targetScale = new Vector3(bounceX, bounceY, bossOrigScale.z);

            if (skipFadeCanvasGroup != null)
            {
                skipFadeCanvasGroup.alpha = Mathf.Clamp01(1f - ratio); // 白飛びを晴れさせる
            }
            yield return null;
        }
        bossController.targetScale = bossOrigScale;
        bossController.targetZRotation = 0f;
        if (skipFadeCanvasGroup != null) { skipFadeCanvasGroup.alpha = 0f; skipFadeCanvasGroup.blocksRaycasts = false; }

        yield return new WaitForSecondsRealtime(0.2f);

        // ===================================================================
        // 😲 6. プレイヤーが驚く
        // ===================================================================
        if (playerBubble != null)
        {
            playerBubble.ShowStamp(ImageBubble.StampType.Surprise); // 目の前の巨大ボスに再び驚愕！
        }
        playerController.PlayReaction(ImageBubble.StampType.Surprise); // プレイヤーがビクッと跳ねる！
        yield return new WaitForSecondsRealtime(0.7f);

        // ===================================================================
        // 👿 7. ボスが叫ぶ
        // ===================================================================
        if (bossController != null)
        {
            bossController.ShowBossStamp(ImageBubble.StampType.Enemy); // ボスが敵意剥き出しの叫びアイコン！
            SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);     // バースト咆哮音！
            ShakeTarget.Instance.Shake(0.4f, 3.0f);                  // 画面大揺れ！
        }
        yield return new WaitForSecondsRealtime(1.2f);

        // 吹き出しを一斉にスッとフェードアウト退場
        if (playerBubble != null) playerBubble.StartFadeOut();
        if (bossController != null) bossController.HideBossStamp();
        yield return new WaitForSecondsRealtime(0.2f);

        // ===================================================================
        // ⚔️ 8. 戦闘開始
        // ===================================================================
        if (cameraFollow != null)
        {
            cameraFollow.ReturnToPlayerFromEvent(0.5f); // カメラを等倍の通常戦闘用へ戻す
        }
        yield return new WaitForSecondsRealtime(0.4f);

        if (stageCamera) stageCamera.gameObject.SetActive(true); // カメラ境界線をONに安全復旧

        // ボスのすべての戦闘ロック・当たり判定を完全解放！通常AIモードへ！
        bossController.SetAllCollidersEnabled(true);
        bossController.SetAllDamageSourcesEnabled(true);
        bossController.TransitionToState(bossController.StateIdle);

        EndEvent();
    }

    protected override void OnSkipWarp()
    {
        if (cameraFollow != null) cameraFollow.ForceStopEventCameraWork();
        if (bossBubble != null) bossBubble.StartFadeOut();
        if (playerBubble != null) playerBubble.StartFadeOut();

        if (hosa != null) hosa.gameObject.SetActive(false);
        if (bossWallObject != null) bossWallObject.SetActive(true);
        if (hpBarObject != null) hpBarObject.SetActive(true);

        if (stageCamera) stageCamera.gameObject.SetActive(true);

        float centerX = (bossController.stageMinX + bossController.stageMaxX) / 2f;
        float centerY = Mathf.Lerp(bossController.stageMinY, bossController.stageMaxY, 0.58f);
        bossController.transform.position = new Vector3(centerX, centerY, 0f);

        bossController.gameObject.SetActive(true);
        bossController.SetAllCollidersEnabled(true);
        bossController.SetAllDamageSourcesEnabled(true);
        bossController.TransitionToState(bossController.StateIdle);

        var health = bossController.GetComponent<GlitchHosaHealth>();
        if (health != null && health.bossHpBar != null)
        {
            health.bossHpBar.StartAppearAnimation(health.maxHP, health.maxHP);
        }
    }
}