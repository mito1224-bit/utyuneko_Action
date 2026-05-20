using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AfterImage : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color color;

    private float alpha;
    private float alphaDecay; // 消えていく速度
    private float alphaInitial; // 最初の透明度

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    // 残像を初期化する関数
    public void Init(Sprite sprite, Vector3 position, Quaternion rotation, Vector3 scale, Color color, float initialAlpha, float decay)
    {
        sr.sprite = sprite;
        transform.position = position;
        transform.rotation = rotation;
        transform.localScale = scale; // プレイヤーの縮尺に合わせる

        // 色と透明度の設定
        this.color = color;
        this.alphaInitial = initialAlpha;
        this.alphaDecay = decay;
        this.alpha = alphaInitial;
    }

    void Update()
    {
        // 毎フレーム透明にしていく
        alpha -= alphaDecay * Time.deltaTime;

        if (alpha <= 0)
        {
            // 完全に透明になったら自分自身を削除（プール化してもいいですがプロトなので簡易的に）
            Destroy(gameObject);
        }
        else
        {
            // SpriteRendererに色を適用
            color.a = alpha;
            sr.color = color;
        }
    }
}