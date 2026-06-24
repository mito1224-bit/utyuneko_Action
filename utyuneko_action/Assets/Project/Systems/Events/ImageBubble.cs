using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ImageBubble : MonoBehaviour
{
    public enum StampType
    {
        OK, Question, Surprise, Doya, Sweat, Hatena, Joy, Gift, Star, Go, Enemy, Maru, Batu, Right, Left, Bottom, Confusion, Denger, Fellow, Dokuro
    }

    [Header("スタンプ画像素材リスト")]
    [Tooltip("OK")]
    [SerializeField] private Sprite imgOK;
    [Tooltip("疑問")]
    [SerializeField] private Sprite imgQuestion;
    [Tooltip("驚き")]
    [SerializeField] private Sprite imgSurprise;
    [Tooltip("どや顔")]
    [SerializeField] private Sprite imgDoya;
    [Tooltip("悲しい")]
    [SerializeField] private Sprite imgSweat;
    [Tooltip("!?")]
    [SerializeField] private Sprite imgHatena;
    [Tooltip("喜ぶ")]
    [SerializeField] private Sprite imgJoy;
    [Tooltip("プレゼント")]
    [SerializeField] private Sprite imgGift;
    [Tooltip("星")]
    [SerializeField] private Sprite imgStar;
    [Tooltip("行こう")]
    [SerializeField] private Sprite imgGo;
    [Tooltip("敵")]
    [SerializeField] private Sprite imgEnemy;
    [Tooltip("丸")]
    [SerializeField] private Sprite imgMaru;
    [Tooltip("バツ")]
    [SerializeField] private Sprite imgBatu;
    [Tooltip("右")]
    [SerializeField] private Sprite imgRight;
    [Tooltip("左")]
    [SerializeField] private Sprite imgLeft;
    [Tooltip("下")]
    [SerializeField] private Sprite imgBottom;
    [Tooltip("混乱")]
    [SerializeField] private Sprite imgConfusion;
    [Tooltip("危険")]
    [SerializeField] private Sprite imgDenger;
    [Tooltip("協力")]
    [SerializeField] private Sprite imgFellow;
    [Tooltip("どくろ")]
    [SerializeField] private Sprite imgDokuro;

    private SpriteRenderer spriteRenderer;
    private Vector3 originalLocalPosition;
    private Quaternion originalWorldRotation;
    private Coroutine currentAnimRoutine;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalLocalPosition = transform.localPosition;
        originalWorldRotation = transform.rotation;
        spriteRenderer.enabled = false;
    }

    void LateUpdate()
    {
        if (spriteRenderer.enabled)
        {
            transform.rotation = originalWorldRotation;
        }
    }

    /// <summary>
    /// スタンプをフワッと表示して、消さずに「その場でずっとキープ」する
    /// </summary>
    public void ShowStamp(StampType type)
    {
        if (spriteRenderer == null) return;
        if (currentAnimRoutine != null) StopCoroutine(currentAnimRoutine);

        switch (type) 
        {
            case StampType.OK: spriteRenderer.sprite = imgOK; break;
            case StampType.Question: spriteRenderer.sprite = imgQuestion; break;
            case StampType.Surprise: spriteRenderer.sprite = imgSurprise; break;
            case StampType.Doya: spriteRenderer.sprite = imgDoya; break;
            case StampType.Sweat: spriteRenderer.sprite = imgSweat; break;
            case StampType.Hatena: spriteRenderer.sprite = imgHatena; break;
            case StampType.Joy: spriteRenderer.sprite = imgJoy; break;
            case StampType.Gift: spriteRenderer.sprite = imgGift; break;
            case StampType.Star: spriteRenderer.sprite = imgStar; break;
            case StampType.Go: spriteRenderer.sprite = imgGo; break;
            case StampType.Enemy: spriteRenderer.sprite = imgEnemy; break;
            case StampType.Maru: spriteRenderer.sprite = imgMaru; break;
            case StampType.Batu: spriteRenderer.sprite = imgBatu; break;
            case StampType.Right: spriteRenderer.sprite = imgRight; break;
            case StampType.Left: spriteRenderer.sprite = imgLeft; break;
            case StampType.Bottom: spriteRenderer.sprite = imgBottom; break;
            case StampType.Confusion: spriteRenderer.sprite = imgConfusion; break;
            case StampType.Denger: spriteRenderer.sprite = imgDenger; break;
            case StampType.Fellow: spriteRenderer.sprite = imgFellow; break;
            case StampType.Dokuro: spriteRenderer.sprite = imgDokuro; break;
        }

        currentAnimRoutine = StartCoroutine(AppearRoutine());

        IEventActor actor = GetComponentInParent<IEventActor>();
        if (actor != null)
        {
            // 親がプレイヤーだろうと、補佐だろうと、誰であろうと一発でリアクションが発動！
            actor.PlayReaction(type);
        }
    }

    /// <summary>
    /// イベント側でボタンが押された瞬間に呼び出され、退場フェードアウトを始める
    /// </summary>
    public void StartFadeOut()
    {
        if (currentAnimRoutine != null) StopCoroutine(currentAnimRoutine);
        currentAnimRoutine = StartCoroutine(FadeOutRoutine());
    }

    // 登場アニメ：パッと出て、少し上に持ち上がったら、そのまま表示を維持して待機
    private IEnumerator AppearRoutine()
    {
        spriteRenderer.enabled = true;
        transform.localPosition = originalLocalPosition;

        Color initColor = spriteRenderer.color;
        initColor.a = 1f;
        spriteRenderer.color = initColor;

        float elapsedTime = 0f;
        Vector3 startPos = originalLocalPosition;
        Vector3 targetPos = originalLocalPosition + Vector3.up * 0.3f;

        while (elapsedTime < 0.2f)
        {
            elapsedTime += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(startPos, targetPos, elapsedTime / 0.2f);
            yield return null;
        }
        transform.localPosition = targetPos;
        // ここで自動消滅の WaitForSeconds を入れずに終了させることで、無限にキープされます！
    }

    // 退場アニメ：少し上に消えながらフェードアウト
    private IEnumerator FadeOutRoutine()
    {
        float elapsedTime = 0f;
        Vector3 startPos = transform.localPosition;
        Vector3 targetPos = transform.localPosition + Vector3.up * 0.2f;

        while (elapsedTime < 0.2f)
        {
            elapsedTime += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(startPos, targetPos, elapsedTime / 0.2f);

            Color c = spriteRenderer.color;
            c.a = Mathf.Lerp(1f, 0f, elapsedTime / 0.2f);
            spriteRenderer.color = c;

            yield return null;
        }

        spriteRenderer.enabled = false;
        Color finalColor = spriteRenderer.color;
        finalColor.a = 1f;
        spriteRenderer.color = finalColor;
    }
}