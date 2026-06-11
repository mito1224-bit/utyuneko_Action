using UnityEngine;

public class DebugDamageInput : MonoBehaviour
{
    private PlayerDamageEffect _damageEffect;

    private void Awake()
    {
        _damageEffect = GetComponent<PlayerDamageEffect>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            _damageEffect.PlayDamageEffect();
        }
    }
}