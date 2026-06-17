using UnityEngine;

public class HosaState_Follow : IHosaState
{
    private HosaController h;
    private PlayerController playerCtrl;

    // 🚶【追従パラメータ】
    private float stopDistanceX = 1.2f;    // プレイヤーの背後にズレる距離
    private float stopDistanceY = 1.0f;    // プレイヤーの頭上の高さ

    // 🛑【新設】ガタつきを完全に揉み消すための内部変数
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

    public void UpdateState()
    {
    }

    public void FixedUpdateState()
    {
        if (h.playerTransform == null || playerCtrl == null) return;

        // 1. プレイヤーの現在の向きを取得（右なら1、左なら-1）
        float playerFacingDir = playerCtrl.GetFacingDirection();
        float targetOffsetX = -playerFacingDir * stopDistanceX;

        // 2. 🧠【ガタつき防止のコアロジック：オフセットのLerp補間】
        // 通常時とバースト時で、全体の追従速度（followEasing）と、回り込みの速度（offsetXSpeed）を最適化します。
        float followEasing;
        float offsetXSpeed;

        if (playerCtrl.CurrentState is PlayerState_Burst)
        {
            // 🚀 バースト中：
            // 超スピード（時速25〜40）に置いていかれないように、追従強度（followEasing）は強めの「12」に。
            // ただし、左右の回り込み速度（offsetXSpeed）はあえて「4」とマイルドに落とします！
            followEasing = 12.0f;
            offsetXSpeed = 4.0f;
        }
        else
        {
            // 🚶 通常時（歩き・待機）：
            // いつもの心地よくフワフワついてくるテンポに設定
            followEasing = 5.0f;
            offsetXSpeed = 10.0f;
        }

        // 🛑【ここが魔法の1行】
        // プレイヤーが反転した瞬間、目標位置を右から左へ「一瞬でワープ」させるのを禁止します！
        // オフセット値自体を Lerp でじわじわ変化させることで、補佐はガタガタ往復することなく、
        // dB君の頭上を「円を描くように美しく、滑らかに回り込む」ようになります。
        currentOffsetX = Mathf.Lerp(currentOffsetX, targetOffsetX, Time.fixedDeltaTime * offsetXSpeed);

        // 最終的な目標位置を計算（バースト中も常に斜め上をターゲットにし続けます）
        Vector3 targetPos = playerCtrl.transform.position + new Vector3(currentOffsetX, stopDistanceY, 0f);

        // 現在地から目標座標までの距離を計測
        float currentDistance = Vector3.Distance(h.transform.position, targetPos);


        // 3. 補佐本体を目標位置に向けて Lerp 移動
        h.transform.position = Vector3.Lerp(
            h.transform.position,
            targetPos,
            Time.fixedDeltaTime * followEasing
        );

        // 4. 補佐のドット絵の向き（常にプレイヤーがいる側を見つめる）
        float directionToPlayer = (playerCtrl.transform.position.x - h.transform.position.x) > 0 ? 1f : -1f;

        // 右にプレイヤーがいれば310度、左にいれば50度を目標にする
        float hosaTargetY = (directionToPlayer > 0f) ? 310f : 50f;

        // 上下の傾き（X軸）は通常プレイ時は0（水平）に戻す
        Quaternion targetHosaRot = Quaternion.Euler(0f, hosaTargetY, 0f);

        // 補佐自身も、プレイヤーと全く同じスムージング速度でクルッと滑らかに向き直る！
        h.transform.localRotation = Quaternion.Lerp(
            h.transform.localRotation,
            targetHosaRot,
            Time.fixedDeltaTime * 10.0f // ここの数値を大きくすると振り向きが早くなります
        );

        // アニメーションの切り替え
        if (currentDistance > 0.1f)
        {
            if (h.anim != null) h.anim.SetBool("isWalk", true);
        }
        else
        {
            if (h.anim != null) h.anim.SetBool("isWalk", false);
        }
    }

    public void Exit()
    {
        if (h.anim != null) h.anim.SetBool("isWalk", false);
    }
}