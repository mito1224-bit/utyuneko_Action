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

    private float chargeDrillAngle = 0f; // チャージ中の独自のドリル回転蓄積用

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：チャージ開始（空中スロー）");

        if (p.hoverSensor != null) p.hoverSensor.GetComponent<Collider2D>().enabled = false;

        // インスペクターの「useInertiaInCharge」を見て慣性を残すか、その場停止かを切り替える
        if (p.useInertiaInCharge)
        {
            p.rb2D.linearVelocity = p.rb2D.linearVelocity * 0.5f; // 慣性あり：速度を半分にしてスローに
        }
        else
        {
            p.rb2D.linearVelocity = Vector2.zero; // 慣性なし：その場にピタッと完全停止！
        }

        p.OnCollisionEnterEvent += OnCollisionEnter;

        p.currentChargeTimer = 0f;
        p.currentChargeLevel = 0;
        chargeDrillAngle = 0f; // 回転角度リセット

        Time.timeScale = p.aimTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

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
        p.currentChargeTimer += Time.unscaledDeltaTime;
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

        if (p.inputActions.Player.Charge.WasReleasedThisFrame())
        {
            p.TransitionToState(p.StateBurst);
        }
    }

    public void FixedUpdateState()
    {
        lastVelocity = p.rb2D.linearVelocity;

        // 慣性オフ設定のときは、毎フレーム速度を0に固定し続けてブレを防ぐ
        if (!p.useInertiaInCharge)
        {
            p.rb2D.linearVelocity = Vector2.zero;
        }

        if (p.visualManager != null && p.visualManager.playerVisual != null)
        {
            // エイム矢印のX方向（左右）だけを見て、右半分なら310度、左半分なら50度をセット
            float targetYAngle = (aimDirection.x >= 0f) ? 310f : 50f;

            // エイムの上下（aimDirection.y）に合わせて、心地よい「しなり（前のめり）」を計算
            float normalizedAimY = Mathf.Clamp(aimDirection.y, -1f, 1f);

            float directionSign = 1f;

            float targetLeanAngle = normalizedAimY * p.visualManager.leanAngle * 1.5f * directionSign;

            // ベースの目標回転（しなりX軸、2D左右向きY軸）を作成
            Quaternion targetRotation = Quaternion.Euler(targetLeanAngle, targetYAngle, 0f);

            // コントローラーのチェックボックス（useRotationInCharge）を確認
            if (p.useRotationInCharge)
            {
                // ONの場合：ベースの向きを維持しつつ、さらにドリル自転（X軸）をグルグル乗せる
                float currentDrillSpeed = p.visualManager.drillSpeed * 0.5f;
                chargeDrillAngle += currentDrillSpeed * Time.fixedDeltaTime * -1f;

                Quaternion drillRotation = Quaternion.Euler(chargeDrillAngle, 0f, 0f);
                targetRotation = targetRotation * drillRotation;
            }

            p.visualManager.playerVisual.localRotation = Quaternion.Lerp(
                p.visualManager.playerVisual.localRotation,
                targetRotation,
                Time.fixedDeltaTime * p.visualManager.chargeLeanSmoothing
            );

            // 伸縮の管理
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
        if (((1 << collision.gameObject.layer) & p.GetGroundLayerMask()) != 0)
        {
            if (!p.useInertiaInCharge) return;

            // 通常通りの反射計算
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
        p.OnCollisionEnterEvent -= OnCollisionEnter;

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        if (p.aimPivot != null)
        {
            p.aimPivot.gameObject.SetActive(false);
        }

        if (arrowMaterial != null)
        {
            Object.Destroy(arrowMaterial);
        }
    }
}