using UnityEngine;
using System.Collections;

// SquareStepTransition(四角い枠が段階的に迫るシェーダー)と、
// CameraFollowWithZoom.StartTrackTarget によるカメラズームを組み合わせた新規トランジション。
//
// 仕様:
//  - FadeOut (0→1): プレイヤーに向かってカメラがズームしながら、四角い枠が段階的に画面を覆っていく
//  - FadeIn  (1→0): ズーム処理は一切行わず、四角い枠が段階的に開いていくだけ
//  - ズーム量は StartTrackTarget に渡す eventTarget の「Zの値」を変えることで制御する
//    (CameraFollowWithZoom.LockCamera が eventTarget.position.z を targetZValue として使うため)
//
// TransitionManager の effectEntries には、SquareStepTransitionEffect とは別の
// TransitionType(例: SquareStepZoom)として新規登録してください。
public class SquareStepZoomTransitionEffect : MonoBehaviour, ITransitionEffect
{
    [Header("マテリアル設定")]
    [SerializeField] private Material transitionMaterial; // SquareStepTransition マテリアルをアサイン

    [Header("イージング（任意）")]
    [SerializeField] private bool useSmoothStep = false; // trueにするとWipeEffect同様のイーズがかかる

    [Header("カメラズーム設定")]
    [SerializeField] private float zoomTargetZ = -5f;         // ズームインしたいZ値(eventTarget.position.zとして渡す)
    [SerializeField] private float eventPositionSpeed = 3f;   // StartTrackTarget の位置追従の速さ
    [SerializeField] private float eventZoomSpeed = 2f;       // StartTrackTarget のズームの速さ

    private int progressID;
    private float duration;

    // StartTrackTarget に渡すための使い回し用プロキシTransform(プレイヤーのXY + 指定したZ を持たせる)
    private Transform zoomProxy;

    public void Initialize(float duration)
    {
        this.duration = duration;
        progressID = Shader.PropertyToID("_Progress");

        if (transitionMaterial != null)
            transitionMaterial.SetFloat(progressID, 0f);
    }

    // 【フェードアウト】プレイヤーにズームしながら、四角い枠が段階的に迫り画面全体を覆う (Progress: 0 → 1)
    public IEnumerator FadeOut()
    {
        if (transitionMaterial == null)
        {
            Debug.LogError("[SquareStepZoomTransitionEffect] Transition Material がセットされていません！");
            yield break;
        }

        // --- カメラズーム開始 ---
        // このエフェクトはシーンをまたいで永続する(TransitionManagerと同様にDontDestroyOnLoad)想定のため、
        // Inspectorで固定参照せず、今アクティブなシーンのカメラをそのつど動的に取得する。
        CameraFollowWithZoom cameraFollowWithZoom = FindActiveCameraFollowWithZoom();
        if (cameraFollowWithZoom != null)
        {
            Transform playerTransform = cameraFollowWithZoom.target;
            if (playerTransform != null)
            {
                EnsureZoomProxy();
                // プレイヤーのXYはそのまま、Zだけ zoomTargetZ に差し替えてズーム量を指定する
                zoomProxy.position = new Vector3(playerTransform.position.x, playerTransform.position.y, zoomTargetZ);
                cameraFollowWithZoom.StartTrackTarget(zoomProxy, eventPositionSpeed, eventZoomSpeed);
            }
            else
            {
                Debug.LogWarning("[SquareStepZoomTransitionEffect] CameraFollowWithZoom.target(プレイヤー)が見つからないため、ズームをスキップします。");
            }
        }
        else
        {
            Debug.LogWarning("[SquareStepZoomTransitionEffect] シーン内に CameraFollowWithZoom が見つからないため、ズームをスキップします。");
        }

        // --- 四角い枠の被覆(既存 SquareStepTransitionEffect と同じロジック) ---
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float raw = Mathf.Clamp01(elapsed / duration);
            float t = useSmoothStep ? Mathf.SmoothStep(0f, 1f, raw) : raw;
            transitionMaterial.SetFloat(progressID, t);
            yield return null;
        }
        // 必ず1を明示的に入れて、画面全体が完全な単色になる状態を保証する
        transitionMaterial.SetFloat(progressID, 1f);
    }

    // 【フェードイン】ズーム処理は行わず、四角い枠だけが段階的に中央から外側へ開いていく (Progress: 1 → 0)
    public IEnumerator FadeIn()
    {
        if (transitionMaterial == null) yield break;

        // ★ 仕様により、1→0 のときはズーム処理を一切行わない。

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float raw = Mathf.Clamp01(elapsed / duration);
            float t = useSmoothStep ? Mathf.SmoothStep(0f, 1f, raw) : raw;
            transitionMaterial.SetFloat(progressID, 1f - t);
            yield return null;
        }
        transitionMaterial.SetFloat(progressID, 0f);
    }

    // 現在アクティブなシーンの CameraFollowWithZoom を動的に探す。
    // まず Camera.main から辿り(高速)、見つからなければシーン全体を探索する。
    private CameraFollowWithZoom FindActiveCameraFollowWithZoom()
    {
        if (Camera.main != null)
        {
            CameraFollowWithZoom onMainCamera = Camera.main.GetComponent<CameraFollowWithZoom>();
            if (onMainCamera != null) return onMainCamera;
        }

        // Unity 6 では FindObjectOfType が非推奨のため FindFirstObjectByType を使用
        return Object.FindFirstObjectByType<CameraFollowWithZoom>();
    }

    private void EnsureZoomProxy()
    {
        if (zoomProxy != null) return;

        GameObject proxyObj = new GameObject("SquareStepZoomTransition_CameraTargetProxy");
        proxyObj.hideFlags = HideFlags.HideInHierarchy;
        zoomProxy = proxyObj.transform;
        zoomProxy.SetParent(transform, worldPositionStays: false);
    }

    private void OnDestroy()
    {
        if (transitionMaterial != null)
            transitionMaterial.SetFloat(progressID, 0f);

        if (zoomProxy != null)
            Destroy(zoomProxy.gameObject);
    }
}