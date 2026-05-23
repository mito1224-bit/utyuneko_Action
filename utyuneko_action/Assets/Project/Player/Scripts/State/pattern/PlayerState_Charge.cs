using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerState_Charge : IPlayerState
{
    private PlayerController p;
    private Vector3 aimDirection = Vector3.right; // 狙っている方向を記憶するベクトル

    // ★【追加】矢印のレンダラーとマテリアルを保持する変数
    private Renderer arrowRenderer;
    private Material arrowMaterial;

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：チャージ開始（空中スロー）");

        p.currentChargeTimer = 0f;
        p.currentChargeLevel = 0;

        // チャージ中は空中静止、重力オフ
        //p.rb.linearVelocity = Vector3.zero;
        //p.rb.useGravity = false;

        // スローモーション
        Time.timeScale = 0.2f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // 矢印オブジェクトを表示
        if (p.aimPivot != null)
        {
            p.aimPivot.gameObject.SetActive(true);
            p.aimPivot.rotation = Quaternion.identity;

            // ★【追加】矢印オブジェクト（またはその子）からRendererを取得してマテリアルを確保
            // 提示コードの FadeOut と同じく、URPマテリアル（_BaseColor）に対応させます。
            arrowRenderer = p.aimPivot.GetComponentInChildren<Renderer>();
            if (arrowRenderer != null)
            {
                // .material にアクセスすると、このオブジェクト専用のマテリアルインスタンスが作られます
                arrowMaterial = arrowRenderer.material;
                // 初期の色（レベル0の色）を設定
                SetArrowColor(p.chargeColors[0]);
            }
        }
    }

    public void UpdateState()
    {
        // 🔥【既存】3段階のチャージ計算
        p.currentChargeTimer += Time.unscaledDeltaTime;
        p.currentChargeLevel = Mathf.FloorToInt(p.currentChargeTimer / p.chargeTimePerLevel);
        p.currentChargeLevel = Mathf.Min(p.currentChargeLevel, p.chargeForceLevels.Length - 1);

        // 🌈【新ドッキング】チャージレベルに合わせて矢印の色を変更
        if (arrowMaterial != null)
        {
            SetArrowColor(p.chargeColors[p.currentChargeLevel]);
        }

        // ★WASDの誤爆を防ぐハイブリッド・エイム計算（元からの完璧なコード）
        var activeControl = p.inputActions.Player.Move.activeControl;
        bool isGamepad = activeControl != null && activeControl.device is Gamepad;

        if (isGamepad && p.moveInput.sqrMagnitude > 0.01f)
        {
            aimDirection = new Vector3(p.moveInput.x, p.moveInput.y, 0f).normalized;
        }
        else
        {
            Vector3 playerScreenPos = Camera.main.WorldToScreenPoint(p.transform.position);
            Vector3 mouseScreenPos = new Vector3(p.mousePositionInput.x, p.mousePositionInput.y, 0f);
            Vector3 directionOnScreen = mouseScreenPos - playerScreenPos;
            aimDirection = new Vector3(directionOnScreen.x, directionOnScreen.y, 0f).normalized;
        }

        // 矢印の角度を計算して回す
        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        p.aimPivot.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // ボタンを離したらバースト！
        if (p.inputActions.Player.Charge.WasReleasedThisFrame())
        {
            p.TransitionToState(p.StateBurst);
        }
    }

    // ★【追加】色を設定するための専用メソッド（以前のFadeOutの知恵を活用）
    private void SetArrowColor(Color color)
    {
        if (arrowMaterial.HasProperty("_BaseColor"))
            arrowMaterial.SetColor("_BaseColor", color); // URPマテリアル用
        else
            arrowMaterial.color = color; // 標準マテリアル用
    }

    public void FixedUpdateState() { }

    public void Exit()
    {
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        //p.rb.useGravity = true;

        // 矢印を非表示にして隠す
        if (p.aimPivot != null)
        {
            p.aimPivot.gameObject.SetActive(false);
        }

        // ★【追加】 Exit時にマテリアルインスタンスを破棄してメモリリークを防ぐ（丁寧な処理）
        if (arrowMaterial != null)
        {
            Object.Destroy(arrowMaterial);
        }
    }
}