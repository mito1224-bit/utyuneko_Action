using UnityEngine;

public class EventCoreCube : MonoBehaviour
{
    private bool isCollected = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // プレイヤーが触れて、かつまだ未回収の場合
        if (other.CompareTag("Player") && !isCollected)
        {
            isCollected = true;

            // アイテム取得SEを鳴らす
            SoundManager.Instance.PlaySE(SeType.ItemCoreGet);

            // 2. 司令塔に「コアキューブ取られたよ！」と合図を送る
            if (CoreCubeEventManager.Instance != null)
            {
                CoreCubeEventManager.Instance.OnCoreCollected();
            }

            GameManager.Instance.AdvanceStoryPhase(); // ストーリー進行を進める

            // 3. コアキューブ本体を画面から消去（Destroy）
            Destroy(gameObject);
        }
    }
}