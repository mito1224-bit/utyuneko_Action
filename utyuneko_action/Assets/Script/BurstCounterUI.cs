using UnityEngine;
using UnityEngine.UI;

public class BurstCounterUI : MonoBehaviour
{
    [Header("メモリの画像(3つ)をここに登録")]
    [SerializeField] private Image[] segments;

    [Header("色の設定")]
    [SerializeField] private Color activeColor = Color.cyan; // 使える時
    [SerializeField] private Color inactiveColor = new Color(0.2f, 0.2f, 0.2f, 0.5f); // 使用済み

    // ゲーム開始時の表示
    void Start()
    {
        UpdateDisplay(0, 3);
    }

    // 表示を更新する命令（PlayerMovementから呼ばれる）
    public void UpdateDisplay(int currentCount, int maxCount)
    {
        // 残り回数を計算（最大3 - 使った数）
        int remaining = maxCount - currentCount;

        for (int i = 0; i < segments.Length; i++)
        {
            if (i < remaining)
            {
                segments[i].color = activeColor; // 残弾がある分は光らせる
            }
            else
            {
                segments[i].color = inactiveColor; // 使った分は暗くする
            }
        }
    }
}