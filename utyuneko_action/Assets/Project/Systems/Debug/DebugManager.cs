using UnityEngine;

public class DebugManager : MonoBehaviour
{
    void Update()
    {
        // --- 1. ゲーム速度の変更 (数字の 1 ～ 3 キー) ---
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            Debug.Log("通常速度 (1.0x)");
        }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            Time.timeScale = 0.2f; // スロー
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            Debug.Log("スローモーション (0.2x)");
        }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            Time.timeScale = 3.0f; // 倍速
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            Debug.Log("倍速モード (3.0x)");
        }

        // --- 2. 当たり判定設定画面を開く (数字の 5 キー) ---
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
        {
#if UNITY_EDITOR
            // 3D用のデバッグウィンドウを開く
            UnityEditor.EditorApplication.ExecuteMenuItem("Window/Analysis/Physics Debugger");

            // 2D用のデバッグウィンドウを開く (Unity 6対応)
            UnityEditor.EditorApplication.ExecuteMenuItem("Window/Analysis/Physics 2D Debugger");

            Debug.Log("2Dと3Dの物理デバッガー画面を開きました");
#endif
        }
    }
}