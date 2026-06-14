using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // 新しい入力システムに必要

public class Fiting : MonoBehaviour
{
    [Header("── 砲台設定 ──────────────────")]
    [SerializeField] private float firingSpeed = 20f;

    [Header("── 衝突判定設定 ────────────────")]
    [SerializeField] private LayerMask obstacleLayers = ~0;

    private static Coroutine _activeLaunchCoroutine;
    private static Fiting _currentActiveCannon;

    // 自動生成された入力クラスのインスタンスを大砲が自分で持つ
    private GameInputActions _inputActions;
    private bool _hasInputPressed = false;

    private bool _isHolding = false;

    private void Awake()
    {
        // 大砲自身の入力システムを用意する（プレイヤーの状態に依存しなくなる）
        _inputActions = new GameInputActions();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // すでに何かをホールド中なら多重判定を防ぐために無視
            if (_isHolding) return;

            PlayerController player = collision.GetComponent<PlayerController>();
            if (player != null)
            {
                if (_activeLaunchCoroutine != null)
                {
                    _currentActiveCannon.StopCoroutine(_activeLaunchCoroutine);
                    _currentActiveCannon.DisableInputMonitoring(); // 古い大砲の監視を終了

                    // 古い大砲が持っていたホールドフラグも解除してあげる
                    if (_currentActiveCannon != null) _currentActiveCannon._isHolding = false;
                }

                _currentActiveCannon = this;
                _isHolding = true; // ホールド開始
                _activeLaunchCoroutine = StartCoroutine(LaunchPlayerRoutine(player));
            }
        }
    }

    private IEnumerator LaunchPlayerRoutine(PlayerController player)
    {
        Debug.Log($"[{gameObject.name}] プレイヤーが大砲に入りました。Eキーまたはボタンを待っています...");

        // ★【最重要】大砲に入った瞬間の、純粋なプレイヤーの燃料状態を記憶する
        int saveFuelCount = player.currentBurstCount;

        // 1. 大砲待機中（完全ホールド）
        player.enabled = false;
        player.rb2D.linearVelocity = Vector2.zero;

        // ? 【削除】player.currentBurstCount = 0; ← これが勝手に回復させていた原因です

        float originalGravity = player.rb2D.gravityScale;
        player.rb2D.gravityScale = 0f;

        // 物理的に完全に静止させ、移動判定を呼ばせないようにする
        player.rb2D.bodyType = RigidbodyType2D.Kinematic;
        player.rb2D.constraints = RigidbodyConstraints2D.FreezeAll; // 位置も角度も物理的に完全ロック

        _hasInputPressed = false;
        EnableInputMonitoring();

        while (!_hasInputPressed)
        {
            // 待機中も、外部の割り込み等で燃料が変わらないように突入時の値を維持
            player.currentBurstCount = saveFuelCount;

            // 毎フレーム位置を固定（微小な移動によるカウント消費を防ぐ）
            player.transform.position = transform.position + -transform.right * 0.3f;
            yield return null;
        }

        DisableInputMonitoring();

        // 2. 発射（重力オン、Burst状態へ）
        Debug.Log($"[{gameObject.name}] ボタン入力を検知！発射します！");

        // 発射するので物理ロックを解除
        player.rb2D.constraints = RigidbodyConstraints2D.FreezeRotation; // 回転だけロックに戻す

        player.rb2D.bodyType = RigidbodyType2D.Dynamic;
        player.rb2D.gravityScale = originalGravity;

        // ここでBurst状態に遷移（ステート側で勝手にカウントが消費される可能性がある）
        player.TransitionToState(player.StateBurst);

        // ★【最重要】遷移した直後のフレームで、すぐに記憶していた燃料で上書き（消費・回復を打ち消す）
        player.currentBurstCount = saveFuelCount;

        Vector2 firingDirection = -transform.right;
        player.rb2D.linearVelocity = firingDirection * firingSpeed;

        yield return new WaitForFixedUpdate();

        // 3. 飛行・衝突監視ループ
        while (true)
        {
            // 飛んでいる間も、記憶した突入時の燃料を毎フレーム強制維持
            player.currentBurstCount = saveFuelCount;

            if (player.rb2D.IsTouchingLayers(obstacleLayers)) break;
            if (player.rb2D.linearVelocity.magnitude < 0.1f) break;
            yield return null;
        }

        // 4. 解放処理
        player.enabled = true;

        // 完全に大砲の処理から解放される瞬間も、突入時の燃料状態を維持
        player.currentBurstCount = saveFuelCount;

        // 大砲から完全に離れたので、次回の侵入を受け付けるためにフラグを下ろす
        _isHolding = false;

        if (_currentActiveCannon == this)
        {
            _activeLaunchCoroutine = null;
            _currentActiveCannon = null;
        }
    }

    // ─── 大砲独自の入力監視処理（自動生成クラスを使用） ───

    private void EnableInputMonitoring()
    {
        if (_inputActions != null)
        {
            _inputActions.Gimmick.Firing.started += OnLaunchButtonPressed;
            _inputActions.Gimmick.Enable(); // Gimmickのキー受付を強制開始！
        }
    }

    private void DisableInputMonitoring()
    {
        if (_inputActions != null)
        {
            _inputActions.Gimmick.Firing.started -= OnLaunchButtonPressed;
            _inputActions.Gimmick.Disable(); // キー受付を終了
        }
    }

    private void OnLaunchButtonPressed(InputAction.CallbackContext context)
    {
        _hasInputPressed = true;
    }

    private void OnDestroy()
    {
        if (_currentActiveCannon == this)
        {
            _activeLaunchCoroutine = null;
            _currentActiveCannon = null;
        }

        // メモリリーク対策
        if (_inputActions != null)
        {
            DisableInputMonitoring();
            _inputActions.Dispose();
        }
    }
}