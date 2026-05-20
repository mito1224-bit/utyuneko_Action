using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerState_Charge : IPlayerState
{
    private PlayerController p;
    private Vector3 aimDirection = Vector3.right; // 狙っている方向を記憶するベクトル（初期値は右向き）

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：チャージ開始（空中スロー）");

        //p.rb.linearVelocity = Vector3.zero;
        //p.rb.useGravity = false;

        Time.timeScale = 0.2f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // 矢印オブジェクトを表示
        if (p.aimPivot != null)
        {
            p.aimPivot.gameObject.SetActive(true);
            // 最初は現在のプレイヤーの向き（簡易的に右向きなど）にしておく
            p.aimPivot.rotation = Quaternion.identity;
        }
    }

    public void UpdateState()
    {
        // ★【新・設計】WASDの誤爆を防ぐハイブリッド・エイム計算

        // 今の入力が「ゲームパッド（コントローラー）のスティック」によるものかどうかを判定する
        // activeControl がスティック（Sticky等）の時だけ、スティックエイムとして扱う
        var activeControl = p.inputActions.Player.Move.activeControl;
        bool isGamepad = activeControl != null && activeControl.device is Gamepad;

        // 1. 本当にコントローラーのスティックが傾いている時だけ、スティックの方向を使う
        if (isGamepad && p.moveInput.sqrMagnitude > 0.01f)
        {
            aimDirection = new Vector3(p.moveInput.x, p.moveInput.y, 0f).normalized;
        }
        // 2. キーボード（WASD）を押している時や、コントローラーに触れていない時は、すべてマウスの方向を狙う！
        else
        {
            // プレイヤーの3D世界座標を画面の2Dピクセル座標に変換
            Vector3 playerScreenPos = Camera.main.WorldToScreenPoint(p.transform.position);

            // マウスの画面座標からプレイヤーの画面座標を引き算
            Vector3 mouseScreenPos = new Vector3(p.mousePositionInput.x, p.mousePositionInput.y, 0f);
            Vector3 directionOnScreen = mouseScreenPos - playerScreenPos;

            // 2.5Dの平面ベクトルとして正規化
            aimDirection = new Vector3(directionOnScreen.x, directionOnScreen.y, 0f).normalized;
        }

        // 3. 矢印の角度を計算して回す
        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        p.aimPivot.rotation = Quaternion.Euler(0, 0, angle - 90f);


        // ボタンを離したらバースト！
        if (p.inputActions.Player.Charge.WasReleasedThisFrame())
        {
            p.TransitionToState(p.StateBurst);
        }
    }

    public void FixedUpdateState()
    {
        // 「ゆっくり落下させたい」仕様にする場合は、後でここに少しだけY軸の速度を足すコードを書きます
    }

    public void Exit()
    {
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        p.rb.useGravity = true;

        // ★【追加】チャージが終わったので、矢印を非表示にして隠す！
        if (p.aimPivot != null)
        {
            p.aimPivot.gameObject.SetActive(false);
        }
    }
}