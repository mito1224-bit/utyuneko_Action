using System.Collections;

// すべてのエフェクトが共通して持つべき「窓口」を定義するインターフェース
// 新しいエフェクトを追加するときは、このインターフェースを実装するだけでOK
public interface ITransitionEffect
{
    // TransitionManager から duration を受け取って初期化する
    void Initialize(float duration);

    // フェードアウト処理（画面を覆っていく）
    IEnumerator FadeOut();

    // フェードイン処理（画面を元に戻す）
    IEnumerator FadeIn();
}