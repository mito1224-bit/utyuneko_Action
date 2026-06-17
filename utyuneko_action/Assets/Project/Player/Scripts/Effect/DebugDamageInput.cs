using UnityEngine;
using System.Collections;

public class DebugDamageInput : MonoBehaviour
{
    private PlayerDamageEffect _damageEffect;

    [Header("トランジション設定")]
    [SerializeField] private string targetSceneName = "SampleScene";

    [Tooltip("ダメージエフェクト開始からトランジション起動までの待機時間（秒）")]
    [SerializeField] private float transitionDelay = 0.6f;

    private void Awake()
    {
        _damageEffect = GetComponent<PlayerDamageEffect>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            // ドローンがダメージを吸収→トランジションなし
            _damageEffect.PlayDamageEffect(DamageType.Drone);
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            // プレイヤーが直接ダメージ→少し待ってからフェード
            _damageEffect.PlayDamageEffect(DamageType.Player);
            StartCoroutine(TriggerTransitionAfterDelay());
        }
    }

    private IEnumerator TriggerTransitionAfterDelay()
    {
        yield return new WaitForSeconds(transitionDelay);
        TransitionManager.Instance.ChangeScene(targetSceneName, TransitionType.Fade);
    }
}