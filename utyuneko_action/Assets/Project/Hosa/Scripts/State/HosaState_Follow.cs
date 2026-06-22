using UnityEngine;

public class HosaState_Follow : IHosaState
{
    private HosaController h;
    private PlayerController playerCtrl;

    // 🚶【追従パラメータ】
    private float stopDistanceX = 1.2f;    // プレイヤーの背後にズレる距離
    private float stopDistanceY = 1.0f;    // プレイヤーの頭上の高さ

    // 🛑 ガタつきを完全に揉み消すための内部変数
    private float currentOffsetX = 0f;

    public void Enter(HosaController hosa)
    {
        h = hosa;
        if (h.playerTransform != null)
        {
            playerCtrl = h.playerTransform.GetComponent<PlayerController>();

            // 開始時に、現在のプレイヤーの向きに合わせて初期位置をカチッと合わせておく
            if (playerCtrl != null)
            {
                currentOffsetX = -playerCtrl.GetFacingDirection() * stopDistanceX;
            }
        }
        Debug.Log("補佐ステート：追従状態（Follow）- 滑らか回り込みモード始動");
    }

    // ===================================================================
    // 🌟【お引越し】
    // 画面の描き換えタイミング（Update）で動かすことで、
    // プレイヤーの Interpolate（補間機能）と完全に同期させ、ブレを100%消し去ります！
    // ===================================================================
    public void UpdateState()
    {
        if (h.playerTransform == null || playerCtrl == null) return;

        // 1. プレイヤーの現在の向きを取得（右なら1、左なら-1）
        float playerFacingDir = playerCtrl.GetFacingDirection();
        float targetOffsetX = -playerFacingDir * stopDistanceX;

        // 2. オフセットのLerp補間（全体の速度と回り込みの速度を最適化）
        float followEasing;
        float offsetXSpeed;

        if (playerCtrl.CurrentState is PlayerState_Burst)
        {
            // 🚀 バースト中
            followEasing = 12.0f;
            offsetXSpeed = 4.0f;
        }
        else
        {
            // 🚶 通常時（歩き・待機）
            followEasing = 5.0f;
            offsetXSpeed = 10.0f;
        }

        // 💡【修正】Time.fixedDeltaTime ➔ Time.deltaTime に変更
        currentOffsetX = Mathf.Lerp(currentOffsetX, targetOffsetX, Time.deltaTime * offsetXSpeed);

        // 最終的な目標位置を計算
        Vector3 targetPos = playerCtrl.transform.position + new Vector3(currentOffsetX, stopDistanceY, 0f);

        // 現在地から目標座標までの距離を計測
        float currentDistance = Vector3.Distance(h.transform.position, targetPos);

        // 3. 補佐本体を目標位置に向けて Lerp 移動
        // 💡【修正】Time.fixedDeltaTime ➔ Time.deltaTime に変更
        h.transform.position = Vector3.Lerp(
            h.transform.position,
            targetPos,
            Time.deltaTime * followEasing
        );

        // 4. 補佐のドット絵の向き（常にプレイヤーがいる側を見つめる）
        float directionToPlayer = (playerCtrl.transform.position.x - h.transform.position.x) > 0 ? 1f : -1f;

        // 右にプレイヤーがいれば310度、左にいれば50度を目標にする
        float hosaTargetY = (directionToPlayer > 0f) ? 310f : 50f;

        // 上下の傾き（X軸）は通常プレイ時は0（水平）に戻す
        Quaternion targetHosaRot = Quaternion.Euler(0f, hosaTargetY, 0f);

        // 💡【修正】Time.fixedDeltaTime ➔ Time.deltaTime に変更
        h.transform.localRotation = Quaternion.Lerp(
            h.transform.localRotation,
            targetHosaRot,
            Time.deltaTime * 10.0f
        );

        // アニメーションの切り替え
        if (currentDistance > 0.1f)
        {
        }
        else
        {
        }
    }

    // 💡 Update側にお引越ししたため、こちらは空っぽでOKです
    public void FixedUpdateState()
    {
    }

    public void Exit()
    {
    }
}