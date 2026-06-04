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
    private PlayerInputActions _inputActions;
    private bool _hasInputPressed = false;

    private void Awake()
    {
        // 大砲自身の入力システムを用意する（プレイヤーの状態に依存しなくなる）
        _inputActions = new PlayerInputActions();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerController player = collision.GetComponent<PlayerController>();
            if (player != null)
            {
                if (_activeLaunchCoroutine != null)
                {
                    _currentActiveCannon.StopCoroutine(_activeLaunchCoroutine);
                    _currentActiveCannon.DisableInputMonitoring(); // 古い大砲の監視を終了
                }

                _currentActiveCannon = this;
                _activeLaunchCoroutine = StartCoroutine(LaunchPlayerRoutine(player));
            }
        }
    }

    private IEnumerator LaunchPlayerRoutine(PlayerController player)
    {
        Debug.Log($"[{gameObject.name}] プレイヤーが大砲に入りました。Eキーまたはボタンを待っています...");

        // 1. 大砲待機中（完全ホールド）
        player.enabled = false;
        player.transform.position = transform.position + -transform.right * 0.3f;
        player.rb2D.linearVelocity = Vector2.zero;

        float originalGravity = player.rb2D.gravityScale;
        player.rb2D.gravityScale = 0f;
        player.rb2D.bodyType = RigidbodyType2D.Kinematic;

        // ★ 大砲独自のルートで入力を監視開始
        _hasInputPressed = false;
        EnableInputMonitoring();

        // ★ ボタンが押されるまでじっと待機
        while (!_hasInputPressed)
        {
            yield return null;
        }

        // ★ ボタンが押されたので監視を終了
        DisableInputMonitoring();

        // 2. 発射（重力オン、Burst状態へ）
        Debug.Log($"[{gameObject.name}] ボタン入力を検知！発射します！");
        player.rb2D.bodyType = RigidbodyType2D.Dynamic;
        player.rb2D.gravityScale = originalGravity;
        player.TransitionToState(player.StateBurst);

        Vector2 firingDirection = -transform.right;
        player.rb2D.linearVelocity = firingDirection * firingSpeed;

        // 大砲から抜け出すための猶予
        yield return new WaitForFixedUpdate();

        // 3. 飛行・衝突監視ループ
        while (true)
        {
            if (player.rb2D.IsTouchingLayers(obstacleLayers)) break;
            if (player.rb2D.linearVelocity.magnitude < 0.1f) break;
            yield return null;
        }

        // 4. 解放処理
        player.enabled = true;

        if (_currentActiveCannon == this)
        {
            _activeLaunchCoroutine = null;
            _currentActiveCannon = null;
        }

        Debug.Log($"[{gameObject.name}] プレイヤーを解放しました。");
    }

    // ─── 大砲独自の入力監視処理（自動生成クラスを使用） ───

    private void EnableInputMonitoring()
    {
        if (_inputActions != null)
        {
            // 共有してもらった自動生成クラスの中の「Gimmick」マップの「Firing」にメソッドを登録
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