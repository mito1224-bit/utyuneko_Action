using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

[RequireComponent(typeof(LineRenderer))]
public class AimTrajectoryLine : MonoBehaviour
{
    [Header("予測線のシミュレーション設定")]
    [Tooltip("何秒先までの未来を予測するか（秒数）")]
    [SerializeField] private float maxSimulationTime = 2.0f;

    [Tooltip("シミュレーションの細かさ（0.02fが最適値）")]
    [SerializeField] private float timeStep = 0.02f;

    [Header("反射の設定")]
    [Tooltip("壁に何回まで反射した軌道を描くか")]
    [SerializeField] private int maxBounces = 1;

    private LineRenderer lineRenderer;
    private PlayerController playerController;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        // 親オブジェクトからPlayerControllerを自動で探して持ってくる
        playerController = GetComponentInParent<PlayerController>();

        // 最初は非表示にしておく
        lineRenderer.enabled = false;
    }

    /// <summary>
    /// 【放物線・反射対応】チャージステートから毎フレーム呼ばれるメイン関数
    /// </summary>
    public void UpdateMovableTrajectory(Vector3 startPos, Vector2 launchVelocity, Color lineColor)
    {
        if (lineRenderer == null || playerController == null) return;

        lineRenderer.enabled = true;

        lineRenderer.startColor = lineColor;

        Color endColor = lineColor;
        endColor.a = 0f; // 先端は透明
        lineRenderer.endColor = endColor;

        // 点の座標を記録するリスト
        List<Vector3> linePoints = new List<Vector3>();
        linePoints.Add(startPos); // スタート地点（dB君の中心）を登録

        // シミュレーション用の仮想的な「現在の座標」と「現在の速度」
        Vector2 currentPos = startPos;
        Vector2 currentVelocity = launchVelocity;

        // 司令塔（PlayerController）から重力スケール、反射効率、床のレイヤーを借りてくる
        float gravityScale = (playerController.rb2D != null) ? playerController.rb2D.gravityScale : 1f;
        float reflectEfficiency = playerController.reflectEfficiency; // インスペクターで設定した反射の強さ(0.8など)
        LayerMask groundLayer = playerController.GetGroundLayerMask(); // 地形のレイヤー

        int bounceCount = 0;
        float elapsedTime = 0f;

        // 未来の時間（例: 2秒先）まで、一コマずつ時間を進めて放物線を仮想計算するループ
        while (elapsedTime < maxSimulationTime)
        {
            // 1. 次の1コマ（0.02秒後）に進んだ時の仮の座標を計算
            Vector2 nextPos = currentPos + currentVelocity * timeStep;

            // 2. 重力による「速度の引っ張り（自由落下）」を速度のY軸に加える
            currentVelocity.y += Physics2D.gravity.y * gravityScale * timeStep;

            // 3. 今の座標から次の座標までの間に、壁（障害物）がないかセンサー（Linecast）を飛ばす
            RaycastHit2D hit = Physics2D.Linecast(currentPos, nextPos, groundLayer);

            if (hit.collider != null)
            {
                // 💥 壁に激突した！
                linePoints.Add(hit.point); // 衝突点を曲がり角として登録
                currentPos = hit.point;    // 仮想座標を激突地点に合わせる

                // 残りの反射回数に余裕があれば、速度ベクトルを壁の法線（hit.normal）で反射させる！
                if (bounceCount < maxBounces)
                {
                    bounceCount++;
                    // 💡 reflectEfficiency(反射効率)を掛けることで、実際のバウンドの減速まで完璧に再現！
                    currentVelocity = Vector2.Reflect(currentVelocity, hit.normal) * reflectEfficiency;

                    // 次の出発点が壁にめり込んで連続衝突バグを起こさないように、少しだけ浮かせる
                    currentPos += hit.normal * 0.01f;
                }
                else
                {
                    // 反射限界に達したらシミュレーションをここで終了する
                    break;
                }
            }
            else
            {
                // 🌌 何にも当たらなかったら、そのまま進んだ座標を新しい点として登録
                linePoints.Add(nextPos);
                currentPos = nextPos;
            }

            elapsedTime += timeStep;
        }

        // 計算したすべての点を LineRenderer に流し込んで、綺麗な1本の放物線カーブにする！
        lineRenderer.positionCount = linePoints.Count;
        lineRenderer.SetPositions(linePoints.ToArray());
    }

    /// <summary>
    /// ❌ 予測線を非表示にする関数
    /// </summary>
    public void HideLine()
    {
        if (lineRenderer != null) lineRenderer.enabled = false;
    }
}