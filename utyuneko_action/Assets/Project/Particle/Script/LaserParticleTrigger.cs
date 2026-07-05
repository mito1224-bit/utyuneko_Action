using System.Collections.Generic;
using UnityEngine;

public class LaserParticleTrigger : MonoBehaviour
{
    private ParticleSystem laserParticle;
    private List<ParticleSystem.Particle> enterParticles = new List<ParticleSystem.Particle>();

    [Header("レーザーの攻撃力")]
    [SerializeField] private int laserDamage = 1;

    void Start()
    {
        laserParticle = GetComponent<ParticleSystem>();
    }

    void OnParticleTrigger()
    {
        // 1. トリガー内（あるいは進入）のパーティクルを取得
        int numEnter = laserParticle.GetTriggerParticles(ParticleSystemTriggerEventType.Enter, enterParticles);

        if (numEnter == 0)
        {
            numEnter = laserParticle.GetTriggerParticles(ParticleSystemTriggerEventType.Inside, enterParticles);
        }

        if (numEnter > 0)
        {
            // 2. Triggersモジュールに登録されているコライダー（Player）を取得
            Component colliderComponent = laserParticle.trigger.GetCollider(0);

            if (colliderComponent != null)
            {
                GameObject player = colliderComponent.gameObject;

                // 3. 【重要】PlayerHealthを取得して、直接ダメージを与える！
                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    // Player側のTakeDamageメソッドを直接実行
                    playerHealth.TakeDamage(laserDamage);
                    Debug.Log($"レーザーがPlayerに{laserDamage}ダメージを与えた！");
                }
            }
        }
    }
}