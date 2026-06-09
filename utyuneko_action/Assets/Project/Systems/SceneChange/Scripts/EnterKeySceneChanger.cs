using UnityEngine;

public class EnterKeySceneChanger : MonoBehaviour
{
    // インスペクターから遷移先のシーン名を自由に設定できるようにします
    [SerializeField] private string targetSceneName;

    void Update()
    {
        // 通常のエンターキー、またはテンキーのエンターが押された瞬間
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            // 登録したシーン名に向かってトランジションを開始！
            TransitionManager.Instance.ChangeScene(targetSceneName);
        }
    }
}