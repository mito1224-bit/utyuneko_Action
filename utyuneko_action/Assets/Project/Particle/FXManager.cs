using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX; // VFX Graphを使うために必要

public class FXManager : MonoBehaviour
{
    // どこからでも FXManager.Instance.Play(...) で呼べるようにする（シングルトン）
    public static FXManager Instance { get; private set; }

    // インスペクターで「名前」と「プレハブ」をペアで登録するための構造体
    [Serializable]
    public struct FXData
    {
        public FXType type;
        public GameObject prefab;
        [Tooltip("VFX Graph用：自動消滅するまでの秒数（ParticleSystemは自動消滅するので0でOK）")]
        public float vfxDestroyDelay;
    }

    [Header("エフェクトの登録リスト")]
    [SerializeField] private List<FXData> fxList = new List<FXData>();

    // 検索を高速化するための辞書（内部用）
    private Dictionary<FXType, FXData> fxDictionary = new Dictionary<FXType, FXData>();

    private void Awake()
    {
        // シングルトンの初期化
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // シーン遷移しても消さない
            InitDictionary();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // リストを検索しやすいように辞書に変換する
    private void InitDictionary()
    {
        foreach (var data in fxList)
        {
            if (!fxDictionary.ContainsKey(data.type))
            {
                fxDictionary.Add(data.type, data);
            }
        }
    }

    /// <summary>
    /// 指定したエフェクトを指定した位置に生成して再生する
    /// </summary>
    public void Play(FXType type, Vector3 position, Quaternion rotation = default)
    {
        if (!fxDictionary.ContainsKey(type))
        {
            Debug.LogWarning($"エフェクト {type} が FXManager に登録されていません！");
            return;
        }

        FXData data = fxDictionary[type];
        if (data.prefab == null) return;

        // エフェクトを生成
        GameObject fxObj = Instantiate(data.prefab, position, rotation);

        // --- 1. Particle System (Shuriken) の場合 ---
        if (fxObj.TryGetComponent<ParticleSystem>(out var ps))
        {
            ps.Play();
            // インスペクター側で Stop Action: Destroy になっていれば、自動で消えるためDestroyコードは不要
        }
        // --- 2. VFX Graph (Visual Effect) の場合 ---
        else if (fxObj.TryGetComponent<VisualEffect>(out var vfx))
        {
            // VFX Graphのデフォルトイベント "OnPlay" を実行（Play on Awakeがあれば勝手に動きますが念のため）
            vfx.SendEvent("OnPlay");

            // VFXは自動消滅しないので、指定秒数後に削除する
            float delay = data.vfxDestroyDelay > 0 ? data.vfxDestroyDelay : 3f; // 未設定なら3秒
            Destroy(fxObj, delay);
        }
    }
}