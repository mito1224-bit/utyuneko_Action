using UnityEngine;

public class EnterKeySceneTrigger : MonoBehaviour
{
    [Header("遷移設定")]
    [SerializeField] private string targetSceneName;
    [SerializeField] private TransitionType transitionType = TransitionType.DigitalRain;

    void Update()
    {
        // メインのエンターキー、またはテンキーのエンターが押された瞬間
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning("[EnterKeySceneTrigger] 遷移先のシーン名が空っぽです！");
                return;
            }

            TransitionManager.Instance.ChangeScene(targetSceneName, transitionType);
        }
    }
}