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

        // バースト開始時にホバーセンサーを無効化
        if (p.hoverSensor != null) p.hoverSensor.GetComponent<Collider2D>().enabled = false;

        p.rb2D.linearVelocity = p.rb2D.linearVelocity * 0.5f;

        p.OnCollisionEnterEvent += OnCollisionEnter;

        p.currentChargeTimer = 0f;
        p.currentChargeLevel = 0;

        Time.timeScale = 0.2f;
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
    }

    private void OnCollisionEnter(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & p.GetGroundLayerMask()) != 0)
        {
            Vector2 incomingVector = lastVelocity;
            if (incomingVector.magnitude < 0.1f) return;

            Vector2 wallNormal = Vector2.zero;
            foreach (var contact in collision.contacts)
            {
                wallNormal += contact.normal;
            }
            wallNormal = wallNormal.normalized;

            // 反射角を計算
            Vector2 reflectedDirection = Vector3.Reflect(incomingVector.normalized, wallNormal);

            // チャージ中なので、通常の反射効率（reflectEfficiency）でポンと跳ね返す
            p.rb2D.linearVelocity = reflectedDirection.normalized * (incomingVector.magnitude * p.reflectEfficiency);

            Debug.Log("チャージ中に壁に衝突！跳ね返りました");
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