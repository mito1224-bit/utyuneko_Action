using UnityEngine;

public class StartStage : MonoBehaviour
{
    [SerializeField] private BgmType bgmType;
    [SerializeField] private float fadeInBGM = 1.0f;
    [SerializeField] private float fadeOutBGM = 1.0f;

    void Start()
    {
        SoundManager.Instance.PlayBGM(bgmType, fadeInBGM);
    }

    void OnDestroy()
    {
        SoundManager.Instance.StopBGM(fadeOutBGM);
    }
}
