using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] private Color activatedColor = Color.yellow;
    [SerializeField] private Renderer targetRenderer;  // Inspectorで指定できるように
    private bool isTriggered = false; // ★1回踏んだら true にしてロックする

    private void Awake()
    {
        // Inspectorで未設定なら自動取得（自分→親の順に探す）
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInParent<SpriteRenderer>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other) // (3DならCollider other)
    {
        if (isTriggered) return; // すでに踏まれていたら何もしない

        if (other.CompareTag("Player"))
        {
            if (DataManager.Instance != null)
            {
                isTriggered = true; // ★ここでロック！
                FXManager.Instance.Play(FXType.ChackPoint, transform.position);
                SoundManager.Instance.PlaySE(SeType.ItemBitGet, 2.0f);
                SetColor();
                Vector3 spawnPosition = transform.position + Vector3.up * 0.5f;
                DataManager.Instance.UpdateCheckpoint(spawnPosition);
            }
        }
    }

    private void SetColor()
    {
        if (targetRenderer == null)
        {
            Debug.LogError("targetRenderer が見つかりません", this);
            return;
        }

        // SpriteRenderer なら .color が最優先（マテリアルを触らずに済む）
        if (targetRenderer is SpriteRenderer sr)
        {
            sr.color = activatedColor;
            return;
        }

        // それ以外は material（インスタンス）を書き換える
        var mat = targetRenderer.material; // sharedMaterial は使わない！
        if (mat.HasProperty("_BaseColor"))      // URP/HDRP 系
        {
            mat.SetColor("_BaseColor", activatedColor);
        }
        else if (mat.HasProperty("_Color"))     // Built-in / Sprites-Default
        {
            mat.SetColor("_Color", activatedColor);
        }
        else
        {
            Debug.LogError($"色プロパティが無いシェーダー: {mat.shader.name}", this);
        }
    }
}