using UnityEngine;
using System.Collections;

public class AfterImageEffect : MonoBehaviour
{
    [Header("残像のプレハブ")]
    public GameObject afterImagePrefab;

    [Header("残像を出す間隔（秒）")]
    public float spawnInterval = 0.1f;

    [Header("消える速度（値が大きいほど早く消える）")]
    public float fadeSpeed = 3.0f;

    [Header("アルファの初期値")]
    public float alpha = 0.6f;

    private float timer;

    void Update()
    {
        // 常に時間をカウント
        timer += Time.deltaTime;

        // 一定時間ごとに残像を生成
        if (timer >= spawnInterval)
        {
            SpawnGhost();
            timer = 0f;
        }
    }

    void SpawnGhost()
    {
        // プレイヤーの現在位置・角度で残像を生成
        GameObject ghost = Instantiate(afterImagePrefab, transform.position, transform.rotation);

        // サイズはプレイヤーよりほんの少し小さくする
        ghost.transform.localScale = transform.localScale * 0.95f;

        // フェードアウトして消滅させるコルーチンを開始
        StartCoroutine(FadeOutAndDestroy(ghost));
    }

    IEnumerator FadeOutAndDestroy(GameObject ghost)
    {
        Renderer ghostRenderer = ghost.GetComponent<Renderer>();
        if (ghostRenderer != null)
        {
            // 残像専用のマテリアルインスタンスを取得
            Material mat = ghostRenderer.material;

            // URPの標準的なカラープロパティ名（_BaseColor）を取得
            Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;

            //アルファ初期値
            color.a = alpha;

            // アルファ値（透明度）が0になるまでループ
            while (color.a > 0)
            {
                color.a -= fadeSpeed * Time.deltaTime;

                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                else
                    mat.color = color;

                yield return null;
            }
        }

        // 透明になったらメモリ解放のために削除
        Destroy(ghost);
    }
}