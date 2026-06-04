using System.Collections.Generic;
using UnityEngine;

public class PlayerBitUI : MonoBehaviour
{
    public enum EasingType
    {
        Immediate,
        LinearLerp,
        SmoothDamp,
        EaseOutCubic,
        SpringBack    // 一度通り過ぎて戻ってくる
    }

    [Header("参照設定")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject bitPrefab;

    [Header("配置設定")]
    [SerializeField] private float offsetY = 1.0f;
    [SerializeField] private float offsetZ = -1.0f;
    [SerializeField] private float spacing = 0.4f;

    [Header("イージング（追従）設定")]
    [SerializeField] private EasingType easingType = EasingType.SpringBack; // 初期値をこれに！
    [SerializeField][Range(1f, 30f)] private float lerpSpeed = 12f;
    [SerializeField][Range(0.01f, 0.5f)] private float smoothTime = 0.1f;

    [Header("SpringBack（通り過ぎて戻る）用設定")]
    [Tooltip("バネの強さ。大きくするほど、通り過ぎてから戻る勢いが強くなる")]
    [SerializeField][Range(100f, 500f)] private float springStiffness = 180f;
    [Tooltip("ブレーキの強さ。小さすぎるといつまでもプルプル揺れ、大きすぎると通り過ぎなる")]
    [SerializeField][Range(10f, 50f)] private float springDamping = 12f;

    [Header("浮遊エフェクト")]
    [Tooltip("不規則な揺らぎの幅")]
    [SerializeField] private float floatAmplitude = 0.12f;
    [Tooltip("揺らぎのうごめくスピード")]
    [SerializeField] private float floatSpeed = 2.5f;

    private List<GameObject> spawnedBits = new List<GameObject>();
    private List<Vector3> bitVelocityList = new List<Vector3>();

    void Start()
    {
        if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        playerHealth.OnHealthChanged += RefreshBitVisibility;
        InitializeBits();
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= RefreshBitVisibility;
    }

    private void InitializeBits()
    {
        for (int i = 0; i < playerHealth.MaxHealth; i++)
        {
            GameObject bit = Instantiate(bitPrefab, playerHealth.transform.position, Quaternion.identity, transform);
            spawnedBits.Add(bit);
            bitVelocityList.Add(Vector3.zero);
        }
        RefreshBitVisibility();
    }

    private void RefreshBitVisibility()
    {
        for (int i = 0; i < spawnedBits.Count; i++)
        {
            if (spawnedBits[i] == null) continue;
            bool shouldBeActive = i < playerHealth.CurrentHealth;
            spawnedBits[i].SetActive(shouldBeActive);
        }
    }

    void Update()
    {
        int totalBits = spawnedBits.Count;
        int aliveCount = playerHealth.CurrentHealth; //現在生きている（表示する）ビットの総数
        int aliveIndex = 0;

        for (int i = 0; i < totalBits; i++)
        {
            GameObject bit = spawnedBits[i];
            if (bit == null) continue;

            // 💡【修正】もし非表示（ダメージで消えた分）のビットなら
            // 計算をスキップして、プレイヤーの真上に即座に待機させておく（次回回復時のバグ防止）
            if (i >= aliveCount)
            {
                bit.transform.position = playerHealth.transform.position + new Vector3(0, offsetY, offsetZ);
                continue;
            }

            // ────────────────────────────────────────────────────────
            // ① 【大改造】生存しているビットの数（aliveCount）をベースに中央揃えを計算
            // ────────────────────────────────────────────────────────
            // aliveIndex（生存内の連番）を使うことで、残り2個なら「左・右」、残り1個なら「真上」に自動整列します
            float offsetX = (aliveIndex - (aliveCount - 1) / 2f) * spacing;
            Vector3 targetPos = playerHealth.transform.position + new Vector3(offsetX, offsetY, offsetZ);

            // ────────────────────────────────────────────────────────
            // ② パーリンノイズによる「不規則なデジタル浮遊」
            // 💡ノイズのシード（第2引数）には「i」を使うことで、被弾時に揺れ方がガクッと変わるのを防ぎます
            // ────────────────────────────────────────────────────────
            float noiseX = (Mathf.PerlinNoise(Time.time * floatSpeed, i * 20f) - 0.5f) * 2f * floatAmplitude;
            float noiseY = (Mathf.PerlinNoise(i * 20f, Time.time * floatSpeed) - 0.5f) * 2f * floatAmplitude;
            targetPos += new Vector3(noiseX, noiseY, 0);

            float dist = Vector3.Distance(bit.transform.position, targetPos);

            // ────────────────────────────────────────────────────────
            // ③ イージング処理（aliveIndex を基準にして時間差をつける）
            // ────────────────────────────────────────────────────────
            switch (easingType)
            {
                case EasingType.Immediate:
                    bit.transform.position = targetPos;
                    break;

                case EasingType.LinearLerp:
                    float individualLerpSpeed = lerpSpeed * (1f - aliveIndex * 0.12f);
                    bit.transform.position = Vector3.Lerp(bit.transform.position, targetPos, individualLerpSpeed * Time.deltaTime);
                    break;

                case EasingType.SmoothDamp:
                    float individualSmoothTime = smoothTime + (aliveIndex * 0.04f);
                    Vector3 currentVelocitySD = bitVelocityList[i];
                    bit.transform.position = Vector3.SmoothDamp(bit.transform.position, targetPos, ref currentVelocitySD, individualSmoothTime);
                    bitVelocityList[i] = currentVelocitySD;
                    break;

                case EasingType.EaseOutCubic:
                    if (dist > 0.001f)
                    {
                        float individualCubicSpeed = lerpSpeed * (1f - aliveIndex * 0.1f);
                        float speedFactor = Mathf.Pow(dist, 2f) * individualCubicSpeed;
                        bit.transform.position = Vector3.MoveTowards(bit.transform.position, targetPos, (speedFactor + 2f) * Time.deltaTime);
                    }
                    else
                    {
                        bit.transform.position = targetPos;
                    }
                    break;

                case EasingType.SpringBack:
                    Vector3 displacement = targetPos - bit.transform.position;

                    // 生存している並び順（aliveIndex）でバネの強さをズラす
                    float individualStiffness = springStiffness * (1f - aliveIndex * 0.15f);

                    Vector3 springForce = displacement * individualStiffness;
                    bitVelocityList[i] += springForce * Time.deltaTime;

                    float individualDamping = springDamping * (1f - aliveIndex * 0.05f);
                    bitVelocityList[i] *= (1f - individualDamping * Time.deltaTime);

                    bit.transform.position += bitVelocityList[i] * Time.deltaTime;
                    break;
            }

            // 💡【重要】処理が1つ終わったら、生存インデックスを次に進める
            aliveIndex++;
        }
    }
}