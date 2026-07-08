using UnityEngine;



/// <summary>

/// ボススナイパー戦を開始するためのステージトリガー。

///

/// 役割:

/// - プレイヤーが範囲に入ったら一度だけ発動

/// - 現在のBGMを止める

/// - ボス本体を有効化する

/// - ボスHPバーUIを有効化する

/// - ボス部屋の壁を有効化する

/// - 発動後、このトリガーを削除する

///

/// 注意:

/// 以前の別ボス用コードにあった StageSecondBossHealth / StartAppearAnimation は使わない。

/// ボススナイパー側のHP管理は BossSniperHealth、HPバー表示は BossSniperHPBar が担当する。

/// </summary>

public class StageBossSniperTrigger : MonoBehaviour

{

    [Header("起動するオブジェクト")]

    [Tooltip("ボススナイパー本体。BossSniper / BossSniperHealth / BossSniperBeamUnit が付いているオブジェクト")]

    public GameObject bossObject;



    [Tooltip("ボスHPバーUIの親オブジェクト。BossSniperHPBar が付いている、またはその親Canvas")]

    public GameObject hpBarObject;



    [Tooltip("ボス戦開始時に閉じる壁・封鎖オブジェクト")]

    public GameObject bossWallObject;



    [Tooltip("ボス戦開始時に有効になるカメラポジション")]

    public GameObject cameraBoundsObject;



    [Header("BGM")]

    [Tooltip("ボス戦開始時に現在のBGMを止める")]

    public bool stopBGMOnTrigger = true;



    [Tooltip("BGM停止のフェード時間")]

    public float bgmFadeTime = 0.5f;



    [Header("カメラ演出")]

    [Tooltip("出現カットインを再生するカメラ演出係。未設定なら演出なしで従来どおり開始する")]

    public BossSniperCameraDirector cameraDirector;



    [Header("発動条件")]

    [Tooltip("プレイヤー判定に使うタグ")]

    public string playerTag = "Player";



    [Tooltip("発動後にこのトリガーオブジェクトをDestroyする")]

    public bool destroyAfterTriggered = true;



    private bool isTriggered = false;



    private void OnTriggerEnter2D(Collider2D other)

    {

        if (isTriggered) return;

        if (!other.CompareTag(playerTag)) return;



        isTriggered = true;



        // 二重発動を防ぐため、先にColliderを切っておく

        Collider2D triggerCollider = GetComponent<Collider2D>();

        if (triggerCollider != null)

        {

            triggerCollider.enabled = false;

        }



        StartBossBattle();



        // 出現演出（カメラがボスへ寄る・地鳴り・衝撃シェイク）。

        // このトリガーは直後に自壊するので、演出係は別オブジェクトであること

        if (cameraDirector != null)

        {

            cameraDirector.PlayIntro();

        }



        if (destroyAfterTriggered)

        {

            Destroy(gameObject);

        }

    }



    private void StartBossBattle()

    {

        // BGM停止

        if (stopBGMOnTrigger)

        {

            if (SoundManager.Instance != null)

            {

                SoundManager.Instance.StopBGM(bgmFadeTime);

            }

            else

            {

                Debug.LogWarning("[StageBossSniperTrigger] SoundManager.Instance が見つかりません。BGM停止はスキップします。");

            }

        }



        // 先にボス本体を有効化する。

        // BossSniperHealth / BossSniper の Awake / OnEnable / Start を動かすため。

        if (bossObject != null)

        {

            bossObject.SetActive(true);

        }

        else

        {

            Debug.LogWarning("[StageBossSniperTrigger] bossObject が設定されていません。");

        }



        // ステージカメラを有効にする

        if (cameraBoundsObject != null)

        {

            cameraBoundsObject.SetActive(true);

        }

        else

        {

            Debug.LogWarning("[StageBossSniperTrigger] bossObject が設定されていません。");

        }



        // HPバーUIを有効化する。

        // BossSniperHPBar は BossSniperHealth のイベントを購読して表示する構成。

        if (hpBarObject != null)

        {

            hpBarObject.SetActive(true);



            // 親（Canvas）がONになった直後の安全なタイミングで出現アニメを起動する。

            // BossSniperHPBar を親・子から探して、満タンから始まる登場演出を叩く。

            BossSniperHPBar hpBar = hpBarObject.GetComponent<BossSniperHPBar>();

            if (hpBar == null) hpBar = hpBarObject.GetComponentInChildren<BossSniperHPBar>(true);

            if (hpBar != null)

            {

                float maxHP = 1f;

                if (bossObject != null)

                {

                    BossSniperHealth health = bossObject.GetComponent<BossSniperHealth>();

                    if (health != null && health.MaxHP > 0f) maxHP = health.MaxHP;

                }

                hpBar.StartAppearAnimation(maxHP, maxHP); // 満タンから始まる登場演出

            }

        }

        else

        {

            Debug.LogWarning("[StageBossSniperTrigger] hpBarObject が設定されていません。");

        }



        // ボス部屋の壁を有効化

        if (bossWallObject != null)

        {

            bossWallObject.SetActive(true);

        }

    }

}