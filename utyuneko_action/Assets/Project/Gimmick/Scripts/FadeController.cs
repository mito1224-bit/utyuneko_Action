using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeController : MonoBehaviour
{
    public Image fadeImage;
    public float fadeSpeed = 1.5f;

    void Start()
    {
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        Color color = fadeImage.color;

        // ê^Ç¡çï Å® ìßñæ
        while (color.a > 0)
        {
            color.a -= Time.deltaTime * fadeSpeed;
            fadeImage.color = color;

            yield return null;
        }

        color.a = 0;
        fadeImage.color = color;
    }

    public IEnumerator FadeOut()
    {
        Color color = fadeImage.color;

        // ìßñæ Å® ê^Ç¡çï
        while (color.a < 1)
        {
            color.a += Time.deltaTime * fadeSpeed;
            fadeImage.color = color;

            yield return null;
        }

        color.a = 1;
        fadeImage.color = color;
    }
}