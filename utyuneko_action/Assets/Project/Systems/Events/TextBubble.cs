using System.Collections;
using UnityEngine;
using TMPro;

public class TextBubble : MonoBehaviour
{
    [Header("文字コンポーネントの参照")]
    [SerializeField] private TextMeshPro textMesh;
    [SerializeField] private SpriteRenderer backgroundSprite; // 吹き出しの背景枠

    [Header("テキストの表示速度")]
    [SerializeField] private float typeSpeed = 0.04f; // 1文字が出るスピード

    private Vector3 originalLocalPosition;
    private Quaternion originalWorldRotation;
    private Coroutine currentTypeRoutine;

    // 💡 現在テキストを表示中（タイピング中）かどうかのフラグ
    public bool IsTyping { get; private set; }

    void Awake()
    {
        originalLocalPosition = transform.localPosition;
        originalWorldRotation = transform.rotation;

        // 初期状態は非表示
        SetVisible(false);
    }

    void LateUpdate()
    {
        // 2.5Dビルボード処理（親が回っても文字は常にカメラを向く：今まで通り！）
        if (backgroundSprite != null && backgroundSprite.enabled)
        {
            transform.rotation = originalWorldRotation;
        }
    }

    /// <summary>
    /// SANABI風：テキストをセットしてタイピングアニメーションを開始する（外部から呼ばれる）
    /// </summary>
    public void DisplayText(string sentence)
    {
        if (currentTypeRoutine != null) StopCoroutine(currentTypeRoutine);

        SetVisible(true);
        transform.localPosition = originalLocalPosition;

        currentTypeRoutine = StartCoroutine(TypeSentenceRoutine(sentence));
    }

    private IEnumerator TypeSentenceRoutine(string sentence)
    {
        IsTyping = true;
        textMesh.text = ""; // 一旦クリア

        // 1文字ずつシュシュシュッと出す（SANABI風タイピング）
        foreach (char letter in sentence.ToCharArray())
        {
            textMesh.text += letter;
            // 🔊 ここで「ピピッ」という軽いSEを鳴らすと、さらにSANABI感が出ます！
            yield return new WaitForSeconds(typeSpeed);
        }

        IsTyping = false;
    }

    /// <summary>
    /// タイピングをスキップして一瞬で全文字出す（プレイヤーがボタンを連打した用）
    /// </summary>
    public void CompleteTextImmediately(string fullSentence)
    {
        if (currentTypeRoutine != null) StopCoroutine(currentTypeRoutine);
        textMesh.text = fullSentence;
        IsTyping = false;
    }

    /// <summary>
    /// 会話が終わった時に吹き出しを完全に消す
    /// </summary>
    public void CloseBubble()
    {
        if (currentTypeRoutine != null) StopCoroutine(currentTypeRoutine);
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (textMesh != null) textMesh.enabled = visible;
        if (backgroundSprite != null) backgroundSprite.enabled = visible;
    }
}