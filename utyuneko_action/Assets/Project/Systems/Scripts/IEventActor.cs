/// <summary>
/// イベント中にスタンプと連動して「体」を動かせるキャラクターの共通資格
/// </summary>
public interface IEventActor
{
    // この資格を持つキャラは、絶対にこの関数を実装しなければならないというルール
    void PlayReaction(ImageBubble.StampType type, float duration = 2.0f);
}