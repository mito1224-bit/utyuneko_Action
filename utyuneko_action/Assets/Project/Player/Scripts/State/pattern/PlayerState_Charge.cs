using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerState_Charge : IPlayerState
{
    private PlayerController p;
    private Vector2 aimDirection = Vector2.right;
    private Vector2 lastVelocity;

    private Renderer arrowRenderer;
    private Material arrowMaterial;

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：チャージ開始（空中スロー）");

        SoundManager.Instance.PlayLoopSE(p.gameObject, SeType.PlayerCharging);
        SoundManager.Instance.FadeBGMVolume(0.5f, 0.5f);

        if (p.anim != null)
        {
            p.anim.SetBool("isBurst", true);
        }

        if (p.hoverSensor != null) p.hoverSensor.GetComponent<Collider2D>().enabled = false;

        if (p.useInertiaInCharge)
        {
            p.rb2D.linearVelocity = p.rb2D.linearVelocity * 0.5f;
        }
        else
        {
            p.rb2D.linearVelocity = Vector2.zero;
        }

        p.OnCollisionEnterEvent += OnCollisionEnter;

        p.currentChargeTimer = 0f;
        p.currentChargeLevel = 0;

        if (p.visualManager != null)
        {
            p.visualManager.ResetDrillRotation();
        }

        // 👑 【能力強奪対応：チャージ中スロー無効化】
        // ステージ内にボスが存在し、かつスロー能力をハッキングされている場合は、世界のスローモーション（TimeManager）を起動させない！
        var boss = Object.FindFirstObjectByType<GlitchHosaController>();
        if (boss != null && boss.isSlowStolen)
        {
            Debug.Log("<color=red>⚠️ ERROR: スロー能力がハッキングされています！世界はスローになりません！</color>");
        }
        else
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.StartSlowMotion(p.aimTimeScale);
            }
        }

        if (p.aimPivot != null)
        {
            p.aimPivot.gameObject.SetActive(true);
            p.aimPivot.rotation = Quaternion.identity;

            arrowRenderer = p.aimPivot.GetComponentInChildren<Renderer>();
            if (arrowRenderer != null)
            {
                arrowMaterial = arrowRenderer.material;
                SetArrowColor(p.chargeColors[0]);
            }
        }
    }

    public void UpdateState()
    {
        // 👑【今回の重要修正ポイント】
        float dt = Time.unscaledDeltaTime;

        // 👑 ボスにスロー能力を奪われているかチェック
        var boss = Object.FindFirstObjectByType<GlitchHosaController>();
        if (boss != null && boss.isSlowStolen)
        {
            // 世界が遅くならない分、本来スロー中に得られるはずだった「体感の加速倍率」をチャージ進行速度に直接上乗せブースト！
            // 例：p.aimTimeScale が 0.2f（5倍遅い世界）なら、チャージの進む速度を 5倍 にして、等倍世界でも一瞬でチャージが完了するようにします。
            float bonusSpeedMultiplier = (p.aimTimeScale > 0.001f) ? (1f / p.aimTimeScale) : 5f;
            dt *= bonusSpeedMultiplier;
        }

        p.currentChargeTimer += dt;
        p.currentChargeLevel = Mathf.FloorToInt(p.currentChargeTimer / p.chargeTimePerLevel);
        p.currentChargeLevel = Mathf.Min(p.currentChargeLevel, p.chargeForceLevels.Length - 1);

        if (arrowMaterial != null)
        {
            SetArrowColor(p.chargeColors[p.currentChargeLevel]);
        }

        var activeControl = p.inputActions.Player.Move.activeControl;
        bool isGamepad = activeControl != null && activeControl.device is Gamepad;

        if (isGamepad && p.moveInput.sqrMagnitude > 0.01f)
        {
            aimDirection = p.moveInput.normalized;
        }
        else
        {
            Vector3 playerScreenPos = Camera.main.WorldToScreenPoint(p.transform.position);
            Vector3 mouseScreenPos = new Vector3(p.mousePositionInput.x, p.mousePositionInput.y, 0f);
            Vector2 directionOnScreen = (Vector2)(mouseScreenPos - playerScreenPos);
            aimDirection = directionOnScreen.normalized;
        }

        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        p.aimPivot.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // 🚀【鏡写し＆放物線＆カラー連動】予測線のリアルタイム更新
        // 👑 【能力強奪対応：予測線非表示】ボスに予測線表示フラグを奪われている場合は、予測線を完全にハッキングハイド（非表示）にする！
        if (boss != null && boss.isLineStolen)
        {
            if (p.trajectoryLine != null)
            {
                p.trajectoryLine.HideLine();
            }
        }
        else
        {
            if (p.trajectoryLine != null && p.aimPivot != null)
            {
                Vector3 startPos = p.transform.position;
                float launchSpeed = p.chargeForceLevels[p.currentChargeLevel];
                Vector2 launchVelocity = aimDirection * launchSpeed;

                p.trajectoryLine.UpdateMovableTrajectory(startPos, launchVelocity, p.chargeColors[p.currentChargeLevel]);
            }
        }

        if (p.inputActions.Player.Charge.WasReleasedThisFrame())
        {
            p.TransitionToState(p.StateBurst);
        }
    }

    public void FixedUpdateState()
    {
        lastVelocity = p.rb2D.linearVelocity;

        if (!p.useInertiaInCharge)
        {
            p.rb2D.linearVelocity = Vector2.zero;
        }

        if (p.visualManager != null)
        {
            p.visualManager.UpdateChargeRotation(aimDirection, p.currentChargeLevel);

            if (p.useSquashInCharge)
            {
                p.visualManager.UpdateSquashAndStretch();
            }
            else
            {
                p.visualManager.ResetVisuals();
            }
        }
    }

    private void OnCollisionEnter(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & p.GetReflectionLayerMask()) != 0)
        {
            if (!p.useInertiaInCharge) return;

            Vector2 incomingVector = lastVelocity;
            if (incomingVector.magnitude < 0.1f) return;

            Vector2 wallNormal = Vector2.zero;
            foreach (var contact in collision.contacts) wallNormal += contact.normal;
            wallNormal = wallNormal.normalized;

            Vector2 reflectedDirection = Vector3.Reflect(incomingVector.normalized, wallNormal);
            p.rb2D.linearVelocity = reflectedDirection.normalized * (incomingVector.magnitude * p.reflectEfficiency);

            if (p.useSquashInCharge && p.visualManager != null)
            {
                p.visualManager.TriggerSquash(wallNormal, incomingVector);
            }

            Debug.Log("チャージ中に壁に衝突！跳ね返り＆伸縮発生");
        }
    }

    private void SetArrowColor(Color color)
    {
        if (arrowMaterial.HasProperty("_BaseColor"))
            arrowMaterial.SetColor("_BaseColor", color);
        else
            arrowMaterial.color = color;
    }

    public void Exit()
    {
        SoundManager.Instance.StopLoopSE(p.gameObject);
        SoundManager.Instance.FadeBGMVolume(1.0f, 0.5f);

        p.OnCollisionEnterEvent -= OnCollisionEnter;

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.StopSlowMotion();
        }

        if (p.aimPivot != null)
        {
            p.aimPivot.gameObject.SetActive(false);
        }

        if (p.trajectoryLine != null)
        {
            p.trajectoryLine.HideLine();
        }

        if (arrowMaterial != null)
        {
            Object.Destroy(arrowMaterial);
        }
    }
}