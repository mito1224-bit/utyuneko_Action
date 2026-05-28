using UnityEngine;

/// <summary>
/// プレイヤーのレイヤーをバースト中だけ切り替えるスクリプト。
/// プレイヤーのGameObjectにアタッチする。
///
/// 【仕組み】
///   通常時  : Player レイヤー       → EnemyPierceable と衝突する（弾かれる）
///   バースト中: PlayerBurst レイヤー → EnemyPierceable と衝突しない（貫通する）
///
///   Layer Collision Matrix で PlayerBurst × EnemyPierceable のチェックを外すだけ。
///   物理エンジンの最下層で制御されるので、スクリプトのタイミング問題が起きない。
///
/// 【Unityのセットアップ手順】
///
///   ① レイヤーを作成する
///      Edit → Project Settings → Tags and Layers → Layers
///      空いている番号に以下を追加:
///        例: Layer 6 = "Player"
///        例: Layer 7 = "PlayerBurst"
///        例: Layer 8 = "EnemyPierceable"
///
///   ② Layer Collision Matrix を設定する
///      Edit → Project Settings → Physics → Layer Collision Matrix
///        Player × EnemyPierceable        → ON（チェックあり）= 衝突する
///        PlayerBurst × EnemyPierceable   → OFF（チェック外す）= 貫通する
///        ※ それ以外はデフォルトのまま
///
///   ③ オブジェクトにレイヤーを割り当てる
///      プレイヤー    → Layer: Player
///      貫通させたい敵 → Layer: EnemyPierceable
///      通常の敵      → Layer: Default のまま（レイヤー変更不要）
///
///   ④ このスクリプトをプレイヤーにアタッチ
///      Inspector で normalLayer に "Player"、burstLayer に "PlayerBurst" を設定
///
///   ⑤ PierceZone 子オブジェクトは不要になるので削除してOK
/// </summary>
public class PlayerLayerSwitcher : MonoBehaviour
{
    [Header("レイヤー名の設定")]
    [Tooltip("通常時のレイヤー名（Unityで作成したレイヤー名と完全一致させる）")]
    public string normalLayerName = "Player";

    [Tooltip("バースト中のレイヤー名（Unityで作成したレイヤー名と完全一致させる）")]
    public string burstLayerName = "PlayerBurst";

    // レイヤー番号のキャッシュ
    private int normalLayer;
    private int burstLayer;

    // PlayerController への参照
    private PlayerController pc;

    // 前フレームのバースト状態（変化したときだけ切り替える）
    private bool wasBursting = false;

    void Start()
    {
        // レイヤー名から番号を取得
        normalLayer = LayerMask.NameToLayer(normalLayerName);
        burstLayer = LayerMask.NameToLayer(burstLayerName);

        if (normalLayer == -1)
            Debug.LogError($"レイヤー '{normalLayerName}' が見つかりません。Project Settings で作成してください。");

        if (burstLayer == -1)
            Debug.LogError($"レイヤー '{burstLayerName}' が見つかりません。Project Settings で作成してください。");

        pc = GetComponent<PlayerController>();

        if (pc == null)
            Debug.LogError("PlayerController が見つかりません。同じGameObjectにアタッチしてください。");

        // 初期レイヤーを設定
        gameObject.layer = normalLayer;
    }

    void Update()
    {
        if (pc == null) return;

        bool isBursting = pc.CurrentState == pc.StateBurst;

        // 状態が変わったときだけレイヤーを切り替える
        if (isBursting != wasBursting)
        {
            gameObject.layer = isBursting ? burstLayer : normalLayer;
            wasBursting = isBursting;

            Debug.Log($"レイヤー切替: {(isBursting ? burstLayerName : normalLayerName)}");
        }
    }
}