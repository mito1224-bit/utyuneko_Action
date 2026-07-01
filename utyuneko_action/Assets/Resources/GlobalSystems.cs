using UnityEngine;

public class GlobalSystems : MonoBehaviour
{
    private static bool isInitialized = false;

    // ゲーム起動時（シーンが読み込まれる一瞬前）に、世界で1回だけ実行される
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeApplication()
    {
        if (isInitialized) return;

        // Resources/GlobalSystems プレハブを読み込む
        GameObject prefab = Resources.Load<GameObject>("GlobalSystems");
        if (prefab != null)
        {
            GameObject systemsObj = Instantiate(prefab);
            systemsObj.name = "[GlobalSystems]";

            // シーンを跨いでも絶対に消えないようにロック
            DontDestroyOnLoad(systemsObj);

            isInitialized = true;
            Debug.Log("[GlobalSystems] 全ての常駐マネージャーが一括自動生成されました！");
        }
        else
        {
            Debug.LogError("Resourcesフォルダの中に 'GlobalSystems' プレハブが見つかりません！");
        }
    }
}