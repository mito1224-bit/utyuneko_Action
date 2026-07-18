using UnityEngine;
using System.Collections;

/// <summary>
/// 👑 崩壊した補佐：出現演出（アピア）ステート
/// イベントマネージャー側の一括タイムライン制御と100%安全に完全同期するクリーン版！
/// </summary>
public class GlitchHosaAppearState : GlitchHosaBaseState
{
    public GlitchHosaAppearState(GlitchHosaController boss) : base(boss) { }

    public override void Enter()
    {
        // 出現演出中はコライダーや攻撃の当たり判定をすべてOFFにし、予期せぬ被弾バグを鉄壁ガード！
        boss.SetAllCollidersEnabled(false);
        boss.SetAllDamageSourcesEnabled(false);

        // 通常の浮遊hoverアニメーションを一時的にフリーズ
        boss.targetVisualOffset = Vector3.zero;

        // 出現時のポーズアングル（綺麗な正面向きにロック初期化）
        boss.targetXRotation = boss.defaultXRotation;
        boss.targetYRotation = boss.defaultYRotation;
        boss.targetZRotation = 0f;

        // 👑【最適化】勝手に自律行動しないようコルーチンは走らせず、マネージャー側のタイムラインの命令を静かに待ちます
    }

    public override void Update() { }
    public override void FixedUpdate() { }

    public override void Exit()
    {
        // 通常戦闘へ移行する際の最終トランスフォーム安全保証リセット
        boss.targetScale = boss.originalVisualLocalScale;
        boss.targetZRotation = 0f;
        boss.SetAllCollidersEnabled(true);
        boss.SetAllDamageSourcesEnabled(true);
    }
}