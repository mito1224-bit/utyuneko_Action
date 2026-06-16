using UnityEngine;
public class TransitionDebugger : MonoBehaviour
{
    [Header("遷移先シーン名")]
    [SerializeField] private string targetSceneName = "SampleScene";

    [Header("キー設定")]
    [SerializeField] private KeyCode wipeKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode digitalRainKey = KeyCode.Alpha2;

    void Update()
    {
        if (Input.GetKeyDown(wipeKey))
        {
            Debug.Log($"[TransitionDebugger] Wipe 実行 → {targetSceneName}");
            TransitionManager.Instance.ChangeScene(targetSceneName, TransitionType.Wipe);
        }

        if (Input.GetKeyDown(digitalRainKey))
        {
            Debug.Log($"[TransitionDebugger] DigitalRain 実行 → {targetSceneName}");
            TransitionManager.Instance.ChangeScene(targetSceneName, TransitionType.DigitalRain);
        }
    }
}