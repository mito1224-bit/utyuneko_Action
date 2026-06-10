using UnityEngine;

public class EnterKeySceneTrigger : MonoBehaviour
{
    [Header("遷移先のシーン名")]
    [SerializeField] private string targetSceneName;

    void Update()
    {
        // メインのエンターキー、またはテンキーのエンターが押された瞬間
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                // 分割した演出実行用のスクリプト（Instance）を呼び出す！
                DigitalRainTransition.Instance.FadeToScene(targetSceneName);
            }
            else
            {
                Debug.LogWarning("遷移先のシーン名（Target Scene Name）が空っぽです！");
            }
        }
    }
}