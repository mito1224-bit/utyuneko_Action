using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerState_Charge : IPlayerState
{
    private PlayerController p;
    private Vector2 aimDirection = Vector2.right; // Vector2に変更

    private Renderer arrowRenderer;
    private Material arrowMaterial;

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：チャージ開始（空中スロー）");

        p.currentChargeTimer = 0f;
        p.currentChargeLevel = 0;

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

    private void SetArrowColor(Color color)
    {
        if (arrowMaterial.HasProperty("_BaseColor"))
            arrowMaterial.SetColor("_BaseColor", color);
        else
            arrowMaterial.color = color;
    }

    public void FixedUpdateState() { }

    public void Exit()
    {
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