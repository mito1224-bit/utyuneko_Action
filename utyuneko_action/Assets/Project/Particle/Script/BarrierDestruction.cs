using UnityEngine;

public class BarrierDestruction : MonoBehaviour
{


    [Header("割れた時のガラスパーティクルPrefab")]
    [SerializeField] private GameObject glassShatterPrefab;

    //[Header("割れる時のSEがあれば（任意）")]
    //[SerializeField] private AudioClip shatterSound;

    private bool isDestroyed = false;

    // ★2DのTrigger判定用（すり抜ける設定の場合）
    private void OnTriggerEnter2D(Collider2D other)
    {
        // プレイヤーに当たった、かつ、まだ壊れていない場合
        if (other.CompareTag("Player") && !isDestroyed)
        {
            Shatter();
        }
    }

    // ★2DのCollision判定用（物理的にぶつかって止まる設定ならこちらを使う）
    /*
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && !isDestroyed)
        {
            Shatter();
        }
    }
    */

    private Transform target; // 追いかける敵のTarget

    // 敵からターゲットを設定してもらうための関数
    public void SetupTarget(Transform Transform)
    {
        target = Transform;
    }

    private void LateUpdate()
    {
        // 敵が存在していれば、位置だけを毎フレーム同期する（回転は無視される）
        if (target != null)
        {
            transform.position = target.position;
        }
    }

    private void Shatter()
    {
        isDestroyed = true;

        // 1. ガラス割れパーティクルをバリアと同じ位置・回転で生成
        if (glassShatterPrefab != null)
        {
            Instantiate(glassShatterPrefab, transform.position, transform.rotation);
        }

        //// 2. 音を鳴らす
        //if (shatterSound != null)
        //{
        //    AudioSource.PlayClipAtPoint(shatterSound, transform.position);
        //}

        // 3. バリア自身を即座に消去
        Destroy(gameObject);
    }

    //[Header("割れた時のガラスパーティクルPrefab")]
    //[SerializeField] private GameObject glassShatterPrefab;

    //[Header("割れる時のSEがあれば（任意）")]
    //[SerializeField] private AudioClip shatterSound;

    //private bool isDestroyed = false;

    //private void OnTriggerEnter(Collider other)
    //{
    //    // プレイヤーに当たった、かつ、まだ壊れていない場合
    //    if (other.CompareTag("Player") && !isDestroyed)
    //    {
    //        Shatter();
    //    }
    //}

    //// もしバリアがTrigger（すり抜ける設定）ではなく、物理的にぶつかる設定ならこちらを使う
    ///*
    //private void OnCollisionEnter(Collision collision)
    //{
    //    if (collision.gameObject.CompareTag("Player") && !isDestroyed)
    //    {
    //        Shatter();
    //    }
    //}
    //*/

    //private void Shatter()
    //{
    //    isDestroyed = true;

    //    // 1. ガラス割れパーティクルをバリアと同じ位置・回転で生成
    //    if (glassShatterPrefab != null)
    //    {
    //        Instantiate(glassShatterPrefab, transform.position, transform.rotation);
    //    }

    //    // 2. 音を鳴らす（AudioSourceがカメラ等にあればそこで再生、簡易的には以下）
    //    if (shatterSound != null)
    //    {
    //        AudioSource.PlayClipAtPoint(shatterSound, transform.position);
    //    }

    //    // 3. バリア自身を即座に消去
    //    Destroy(gameObject);
    //}
}
