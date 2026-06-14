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
        if (Input.GetKeyDown(KeyCode.O))
        {
             _damageEffect.PlayDamageEffect(DamageType.Drone);
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            _damageEffect.PlayDamageEffect(DamageType.Player);
        }
    }
}