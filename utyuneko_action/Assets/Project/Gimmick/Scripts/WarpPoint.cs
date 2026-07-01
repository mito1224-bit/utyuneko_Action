using System.Collections; // コルーチンを使うために必要
using UnityEngine;

public class WarpPoint : MonoBehaviour
{
    [SerializeField] private Transform warpTarget;
    [SerializeField] private Vector2 warpOffset = new Vector2(0f, 0f); // クールタイムがあるので 0,0 でもOKになります

    // ★【追加】ワープが現在クールタイム中（利用不可）かどうかを管理するフラグ
    private static bool isWarpCoolingDown = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ★ クールタイム中なら、触れても何もせずに無視する
        if (isWarpCoolingDown) return;

        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();

            if (player != null)
            {
                // ワープ処理が始まったら、即座に世界中の全ワープをロックする
                StartCoroutine(WarpRoutine(player));
            }
        }
    }

    private IEnumerator WarpRoutine(PlayerController player)
    {
        // クールタイム開始（これでお互いに行き来するのを防ぐ）
        isWarpCoolingDown = true;

        int savedFuel = player.currentBurstCount;

        // 1. 速度と回転を完全にリセットする
        player.rb2D.linearVelocity = Vector2.zero;
        player.rb2D.angularVelocity = 0f;

        // 2. ステートを遷移させて燃料を維持
        player.TransitionToState(player.StateBurst);
        player.currentBurstCount = savedFuel;

        // 3. 座標を書き換える
        Vector3 targetPosition = warpTarget.position + (Vector3)warpOffset;
        targetPosition.z = 0f;
        player.transform.position = targetPosition;

        Debug.Log($"[{gameObject.name}] ワープ成功。クールタイムを開始します。");

        // ★ 0.5秒間待つ（この間にプレイヤーはワープ先で安全に静止し、引っ張りエイムができる）
        yield return new WaitForSeconds(0.5f);

        // クールタイム終了（再びワープが使えるようになる）
        isWarpCoolingDown = false;
        Debug.Log("ワープのクールタイムが終了しました。");
    }
}