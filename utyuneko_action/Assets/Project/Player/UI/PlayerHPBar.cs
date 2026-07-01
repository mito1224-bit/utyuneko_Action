using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHPBar : MonoBehaviour
{
    [Header("自動生成の設定")]
    [Tooltip("生成するHPのセル1個分のプレハブ(Imageがついたもの)を登録する")]
    [SerializeField] private GameObject hpCellPrefab;

    [Header("カラー設定")]
    [SerializeField] private Color colorFull = new Color(0f, 1f, 0.5f);
    [SerializeField] private Color colorWarning = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color colorDanger = new Color(1f, 0.2f, 0.2f);

    [Header("背景の表示設定")]
    [Tooltip("HPが減ったときに、後ろに灰色の背景（枠）を残すかどうか")]
    [SerializeField] private bool showBackground = true;

    [Header("非戦闘時のスマート縮小設定")]
    [Tooltip("ダメージ等の変化を受けてから、フルサイズを維持する時間（秒）")]
    [SerializeField] private float displayDuration = 3.0f;

    [Tooltip("小さくなった時のサイズ倍率（X:0.5, Y:0.5 にすれば半分。全部0にすれば完全に消せます！）")]
    [SerializeField] private Vector3 shrunkScale = new Vector3(0.5f, 0.5f, 1f);

    [Tooltip("サイズが大きくなったり縮んだりする時のアニメーション速度")]
    [SerializeField] private float scaleSpeed = 6.0f;

    private PlayerHealth playerHealth;
    private List<Image> hpCellsList = new List<Image>();

    private Coroutine[] blinkRoutines;
    private float glitchTimer = 0f;

    private float displayTimer = 0f;
    private Vector3 normalScale;

    void Start()
    {
        normalScale = transform.localScale;
        displayTimer = displayDuration;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            GenerateHPCells(playerHealth.MaxHealth);
            playerHealth.OnHealthChanged += RefreshHP;
            RefreshHP();
        }
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= RefreshHP;
        }
    }

    private void GenerateHPCells(int maxHealth)
    {
        if (hpCellPrefab == null)
        {
            Debug.LogError("[PlayerHPBar] HP Cell Prefab がセットされていません！");
            return;
        }

        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        hpCellsList.Clear();

        blinkRoutines = new Coroutine[maxHealth];

        for (int i = 0; i < maxHealth; i++)
        {
            GameObject newCell = Instantiate(hpCellPrefab, transform);
            newCell.name = $"HP_Cell_{i}";

            Transform bgTransform = newCell.transform.Find("Background");
            if (bgTransform != null)
            {
                bgTransform.gameObject.SetActive(showBackground);
            }

            // 新しく作ったプレハブの子オブジェクト "Fill" からImageを探す
            Transform fillTransform = newCell.transform.Find("Fill");
            Image cellImage = (fillTransform != null) ? fillTransform.GetComponent<Image>() : newCell.GetComponent<Image>();

            if (cellImage != null)
            {
                hpCellsList.Add(cellImage);
            }
        }
    }

    private void RefreshHP()
    {
        if (playerHealth == null || hpCellsList.Count == 0) return;

        displayTimer = displayDuration;

        int currentHealth = playerHealth.CurrentHealth;

        Color targetColor = colorFull;
        if (currentHealth == 2) targetColor = colorWarning;
        if (currentHealth <= 1) targetColor = colorDanger;

        for (int i = 0; i < hpCellsList.Count; i++)
        {
            if (hpCellsList[i] == null) continue;

            if (i >= hpCellsList.Count - currentHealth)
            {
                if (blinkRoutines[i] != null)
                {
                    StopCoroutine(blinkRoutines[i]);
                    blinkRoutines[i] = null;
                }

                hpCellsList[i].enabled = true;

                Color c = targetColor;
                c.a = 1f;
                hpCellsList[i].color = c;
            }
            else
            {
                if (blinkRoutines[i] == null && hpCellsList[i].enabled)
                {
                    blinkRoutines[i] = StartCoroutine(BlinkAndHideRoutine(i));
                }
            }
        }
    }

    private IEnumerator BlinkAndHideRoutine(int index)
    {
        Image cell = hpCellsList[index];

        float duration = 0.5f;
        float blinkInterval = 0.1f;
        float elapsedTime = 0f;
        float blinkTimer = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            blinkTimer += Time.deltaTime;

            if (blinkTimer >= blinkInterval)
            {
                blinkTimer = 0f;
                cell.enabled = !cell.enabled;
            }
            yield return null;
        }

        cell.enabled = false;
        blinkRoutines[index] = null;
    }

    void Update()
    {
        if (displayTimer > 0f)
        {
            displayTimer -= Time.deltaTime;
        }

        Vector3 targetScale = (displayTimer > 0f) ? normalScale : shrunkScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);

        if (playerHealth != null && playerHealth.CurrentHealth == 1 && hpCellsList.Count > 0)
        {
            int lastIndex = hpCellsList.Count - 1;

            if (hpCellsList[lastIndex].enabled && blinkRoutines[lastIndex] == null)
            {
                glitchTimer += Time.deltaTime * 25f;
                float alpha = Mathf.Sin(glitchTimer) > 0f ? 1.0f : 0.3f;

                Color c = hpCellsList[lastIndex].color;
                c.a = alpha;
                hpCellsList[lastIndex].color = c;
            }
        }
    }
}