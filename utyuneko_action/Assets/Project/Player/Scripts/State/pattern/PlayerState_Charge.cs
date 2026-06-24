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

        p.OnCollisionEnterEvent += OnCollisionEnter; //

        p.currentChargeTimer = 0f; //
        p.currentChargeLevel = 0; //

        // 💡 ドリル回転の引っかかりバグ対策として、蓄積角度のリセットはマネージャー側で一括管理します
        if (p.visualManager != null)
        {
            p.visualManager.ResetDrillRotation();
        }

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.StartSlowMotion(p.aimTimeScale);
        }

        if (p.aimPivot != null)
        {
            p.aimPivot.gameObject.SetActive(true); //
            p.aimPivot.rotation = Quaternion.identity; //

            arrowRenderer = p.aimPivot.GetComponentInChildren<Renderer>(); //
            if (arrowRenderer != null)
            {
                arrowMaterial = arrowRenderer.material; //
                SetArrowColor(p.chargeColors[0]); //
            }
        }
    }

    public void UpdateState()
    {
        p.currentChargeTimer += Time.unscaledDeltaTime; //
        p.currentChargeLevel = Mathf.FloorToInt(p.currentChargeTimer / p.chargeTimePerLevel); //
        p.currentChargeLevel = Mathf.Min(p.currentChargeLevel, p.chargeForceLevels.Length - 1); //

        if (arrowMaterial != null)
        {
            SetArrowColor(p.chargeColors[p.currentChargeLevel]); //
        }

        var activeControl = p.inputActions.Player.Move.activeControl; //
        bool isGamepad = activeControl != null && activeControl.device is Gamepad; //

        if (isGamepad && p.moveInput.sqrMagnitude > 0.01f) //
        {
            aimDirection = p.moveInput.normalized; //
        }
        else
        {
            Vector3 playerScreenPos = Camera.main.WorldToScreenPoint(p.transform.position); //
            Vector3 mouseScreenPos = new Vector3(p.mousePositionInput.x, p.mousePositionInput.y, 0f); //
            Vector2 directionOnScreen = (Vector2)(mouseScreenPos - playerScreenPos); //
            aimDirection = directionOnScreen.normalized; //
        }

        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg; //
        p.aimPivot.rotation = Quaternion.Euler(0, 0, angle - 90f); //

        // 🚀【鏡写し＆放物線＆カラー連動】完璧に対応した予測線のリアルタイム更新
        if (p.trajectoryLine != null && p.aimPivot != null)
        {
            Vector3 startPos = p.transform.position; //
            float launchSpeed = p.chargeForceLevels[p.currentChargeLevel]; //
            Vector2 launchVelocity = aimDirection * launchSpeed; //

            p.trajectoryLine.UpdateMovableTrajectory(startPos, launchVelocity, p.chargeColors[p.currentChargeLevel]); //
        }

        if (p.inputActions.Player.Charge.WasReleasedThisFrame()) //
        {
            p.TransitionToState(p.StateBurst); //
        }
    }

    public void FixedUpdateState()
    {
        lastVelocity = p.rb2D.linearVelocity; //

        // 慣性オフ設定のときは、毎フレーム速度を0に固定し続けてブレを防ぐ
        if (!p.useInertiaInCharge)
        {
            p.rb2D.linearVelocity = Vector2.zero; //
        }

        if (p.visualManager != null)
        {
            // 1. 向き・しなり・ドリル超高速自転をまとめて安全に更新！
            p.visualManager.UpdateChargeRotation(aimDirection, p.currentChargeLevel);

            // 2. 伸縮（モチッと演出）の管理
            if (p.useSquashInCharge)
            {
                p.visualManager.UpdateSquashAndStretch(); //
            }
            else
            {
                p.visualManager.ResetVisuals(); //
            }
        }
    }

    private void OnCollisionEnter(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & p.GetGroundLayerMask()) != 0) //
        {
            if (!p.useInertiaInCharge) return; //

            // 通常通りの反射計算
            Vector2 incomingVector = lastVelocity; //
            if (incomingVector.magnitude < 0.1f) return; //

            //
            Vector2 wallNormal = Vector2.zero;
            foreach (var contact in collision.contacts) wallNormal += contact.normal; //
            wallNormal = wallNormal.normalized; //

            Vector2 reflectedDirection = Vector3.Reflect(incomingVector.normalized, wallNormal); //
            p.rb2D.linearVelocity = reflectedDirection.normalized * (incomingVector.magnitude * p.reflectEfficiency); //

            if (p.useSquashInCharge && p.visualManager != null) //
            {
                p.visualManager.TriggerSquash(wallNormal, incomingVector); //
            }

            Debug.Log("チャージ中に壁に衝突！跳ね返り＆伸縮発生"); //
        }
    }

    private void SetArrowColor(Color color)
    {
        if (arrowMaterial.HasProperty("_BaseColor")) //
            arrowMaterial.SetColor("_BaseColor", color); //
        else
            arrowMaterial.color = color; //
    }

    public void Exit()
    {
        p.OnCollisionEnterEvent -= OnCollisionEnter; //

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.StopSlowMotion();
        }

        if (p.aimPivot != null)
        {
            p.aimPivot.gameObject.SetActive(false); //
        }

        if (p.trajectoryLine != null)
        {
            p.trajectoryLine.HideLine(); //
        }

        if (arrowMaterial != null)
        {
            Object.Destroy(arrowMaterial); //
        }
    }
}