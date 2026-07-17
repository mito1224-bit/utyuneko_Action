using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【パターン2】ステージ上空を埋め尽くすように分身と自分を配置（合計5体）し、
/// 偶数番（0, 2, 4）と奇数番（1, 3）のグリッドで交互に真下にビームを降らせるスナイパー技！
/// 👑 ユーザー仕様完全復旧 ＆ 専用ピボット統合 ＆ 独立型出現・消滅アニメーション完全対応版
/// </summary>
public class HosaP1_ClonesState : GlitchHosaBaseState
{
    private class SniperNode
    {
        public int index;
        public Vector3 position;
        public GameObject cloneObject;
        public GameObject warningLine;
        public GameObject laserPivot;
    }

    private List<SniperNode> sniperNodes = new List<SniperNode>();
    private const int totalPositions = 5;

    public HosaP1_ClonesState(GlitchHosaController boss) : base(boss) { }

    public override void Enter()
    {
        sniperNodes.Clear();
        boss.StartCoroutine(ExecuteCloneSniperSequence());
    }

    private IEnumerator ExecuteCloneSniperSequence()
    {
        float startX = boss.stageMinX + 2.0f;
        float endX = boss.stageMaxX - 2.0f;
        float topY = boss.stageMaxY - 1.5f;

        Debug.Log("<color=orange>👥 補佐：ホログラム分身を展開。上空スナイパー陣形を構築します！</color>");

        Transform baseVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        Vector3 origScale = boss.originalVisualLocalScale;

        for (int i = 0; i < totalPositions; i++)
        {
            float ratio = (float)i / (totalPositions - 1);
            float targetX = Mathf.Lerp(startX, endX, ratio);
            Vector3 nodePos = new Vector3(targetX, topY, boss.transform.position.z);

            SniperNode node = new SniperNode { index = i, position = nodePos };

            if (i == 2)
            {
                // 👑【修正】ボス本体（真ん中）がグリッドへ移動するワープ演出時間を0.2秒に完全統一！
                yield return boss.StartCoroutine(boss.TeleportWithSquashRoutine(nodePos, 0.2f));
            }
            else
            {
                GameObject clone = Object.Instantiate(baseVisual.gameObject, nodePos, Quaternion.identity);
                clone.name = $"Hosa_HologramClone_{i}";

                if (clone.TryGetComponent<GlitchHosaController>(out var c)) Object.Destroy(c);
                if (clone.TryGetComponent<Rigidbody2D>(out var rb)) Object.Destroy(rb);
                if (clone.TryGetComponent<Collider2D>(out var col)) Object.Destroy(col);
                if (clone.TryGetComponent<Animator>(out var anim)) Object.Destroy(anim);
                foreach (var childCol in clone.GetComponentsInChildren<Collider2D>()) Object.Destroy(childCol);

                Transform cloneRotation = FindCloneRotationTarget(clone);
                if (cloneRotation != null)
                {
                    cloneRotation.localRotation = Quaternion.Euler(boss.defaultXRotation, boss.defaultYRotation, 0f);
                }

                Transform cloneSquash = FindCloneSquashTarget(clone);
                var helper = clone.AddComponent<GlitchHosaCloneHelper>();
                helper.Initialize(cloneSquash, origScale);
                helper.StartUnsquash(0.12f);

                node.cloneObject = clone;
            }

            sniperNodes.Add(node);
        }

        yield return new WaitForSeconds(0.2f);

        float chargeDuration = 1.0f / boss.attackSpeedMultiplier;
        float fireDuration = 1.3f / boss.attackSpeedMultiplier;

        // ===================================================================
        // ② 偶数ターン（0, 2, 4番目）のチャージ ➔ 発射！
        // ===================================================================
        foreach (var node in sniperNodes)
        {
            if (node.index % 2 == 0) SetupSniperCharge(node, chargeDuration + fireDuration);
        }

        SoundManager.Instance.PlaySE(SeType.EnemyCharge);

        float timer = 0f;
        while (timer < chargeDuration)
        {
            timer += Time.deltaTime;
            AnimateWarningLines(timer);
            yield return null;
        }

        SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
        if (ShakeTarget.Instance != null) ShakeTarget.Instance.Shake(fireDuration, 1.0f);
        FireSniperLasers(true);

        yield return new WaitForSeconds(fireDuration);
        ClearLasersByGroup(true);

        // ===================================================================
        // ③ 奇数ターン（1, 3番目）のチャージ ➔ 発射！
        // ===================================================================
        foreach (var node in sniperNodes)
        {
            if (node.index % 2 != 0) SetupSniperCharge(node, chargeDuration + fireDuration);
        }

        SoundManager.Instance.PlaySE(SeType.EnemyCharge);

        timer = 0f;
        while (timer < chargeDuration)
        {
            timer += Time.deltaTime;
            AnimateWarningLines(timer);
            yield return null;
        }

        SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
        if (ShakeTarget.Instance != null) ShakeTarget.Instance.Shake(fireDuration, 1.0f);
        FireSniperLasers(false);

        yield return new WaitForSeconds(fireDuration);
        ClearLasersByGroup(false);

        // ===================================================================
        // ④ 撤収フェーズ（大量の分身を一斉に美しく縮小消去）
        // ===================================================================
        foreach (var node in sniperNodes)
        {
            if (node.cloneObject != null)
            {
                var helper = node.cloneObject.GetComponent<GlitchHosaCloneHelper>();
                if (helper != null)
                {
                    helper.StartSquashAndDestroy(0.12f);
                }
                else
                {
                    Object.Destroy(node.cloneObject);
                }
                node.cloneObject = null;
            }
        }
        SoundManager.Instance.PlaySE(SeType.EnemyCharge);
        yield return new WaitForSeconds(0.12f);

        Debug.Log("<color=cyan>✨ 補佐：分身陣形を解除。通常Idleへ戻ります。</color>");
        boss.TransitionToState(boss.StateIdle);
    }

    private void SetupSniperCharge(SniperNode node, float laserLifeTime)
    {
        float lineLength = 30f;
        node.warningLine = new GameObject($"HosaSniperWarningLine_{node.index}");
        node.warningLine.transform.position = node.position + Vector3.down * (lineLength * 0.5f);
        node.warningLine.transform.rotation = Quaternion.Euler(0f, 0f, -90f);

        if (boss.dashWarningSprite != null)
        {
            SpriteRenderer sr = node.warningLine.AddComponent<SpriteRenderer>();
            sr.sprite = boss.dashWarningSprite;
            sr.color = boss.dashWarningColor;
            sr.sortingOrder = -1;
            if (boss.dashWarningMaterial != null) sr.material = boss.dashWarningMaterial;

            float w = sr.sprite.bounds.size.x;
            float h = sr.sprite.bounds.size.y;
            if (w > 0.0001f && h > 0.0001f)
            {
                node.warningLine.transform.localScale = new Vector3(lineLength / w, (0.8f * boss.cloneSniperThickness) / h, 1f);
            }
        }

        node.laserPivot = new GameObject($"HosaLaserPivot_{node.index}");
        node.laserPivot.transform.position = node.position;

        Vector3 rawTarget = node.position + Vector3.down * 20f;
        Vector3 safeTarget = new Vector3(rawTarget.x + 0.001f, rawTarget.y + 0.001f, rawTarget.z + 0.001f);

        BarrierManager.Instance.SpawnLaser(node.position, safeTarget, laserLifeTime, node.laserPivot.transform);

        foreach (Transform child in node.laserPivot.transform)
        {
            Vector3 localScale = child.localScale;
            localScale.x *= boss.cloneSniperThickness;
            localScale.y *= boss.cloneSniperThickness;
            child.localScale = localScale;

            LineRenderer[] lrs = child.GetComponentsInChildren<LineRenderer>(true);
            foreach (var lr in lrs)
            {
                if (lr != null) lr.widthMultiplier *= boss.cloneSniperThickness;
            }

            ParticleSystem[] pss = child.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in pss)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    main.startSizeMultiplier *= boss.cloneSniperThickness;
                }
            }
        }
    }

    private void FireSniperLasers(bool isEvenGroup)
    {
        foreach (var node in sniperNodes)
        {
            bool isTarget = isEvenGroup ? (node.index % 2 == 0) : (node.index % 2 != 0);
            if (isTarget)
            {
                if (node.warningLine != null)
                {
                    Object.Destroy(node.warningLine);
                    node.warningLine = null;
                }

                if (node.laserPivot != null)
                {
                    ParticleSystem[] pss = node.laserPivot.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in pss)
                    {
                        if (ps != null) ps.Play();
                    }
                }
            }
        }
    }

    private void AnimateWarningLines(float elapsed)
    {
        float alpha = boss.dashWarningColor.a * (0.6f + Mathf.Sin(Time.time * 30f) * 0.4f);
        foreach (var node in sniperNodes)
        {
            if (node.warningLine != null)
            {
                SpriteRenderer sr = node.warningLine.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(boss.dashWarningColor.r, boss.dashWarningColor.g, boss.dashWarningColor.b, alpha);
            }
        }
    }

    private void ClearLasersByGroup(bool isEvenGroup)
    {
        foreach (var node in sniperNodes)
        {
            bool isTarget = isEvenGroup ? (node.index % 2 == 0) : (node.index % 2 != 0);
            if (isTarget && node.laserPivot != null)
            {
                Object.Destroy(node.laserPivot);
                node.laserPivot = null;
            }
        }
    }

    private Transform FindCloneSquashTarget(GameObject cloneObj)
    {
        string targetName = boss.GetSquashTarget().name;
        if (cloneObj.name == targetName) return cloneObj.transform;
        Transform found = FindDeepChild(cloneObj.transform, targetName);
        return found != null ? found : cloneObj.transform;
    }

    private Transform FindCloneRotationTarget(GameObject cloneObj)
    {
        string targetName = boss.GetRotationTarget().name;
        if (cloneObj.name == targetName) return cloneObj.transform;
        Transform found = FindDeepChild(cloneObj.transform, targetName);
        return found != null ? found : cloneObj.transform;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void CleanUpAllSessionObjects()
    {
        foreach (var node in sniperNodes)
        {
            if (node.warningLine != null) Object.Destroy(node.warningLine);
            if (node.laserPivot != null) Object.Destroy(node.laserPivot);
            if (node.cloneObject != null)
            {
                var helper = node.cloneObject.GetComponent<GlitchHosaCloneHelper>();
                if (helper != null)
                {
                    helper.StartSquashAndDestroy(0.1f);
                }
                else
                {
                    Object.Destroy(node.cloneObject);
                }
            }
        }
        sniperNodes.Clear();
    }

    public override void Exit()
    {
        CleanUpAllSessionObjects();
        boss.StopAllCoroutines();
    }
}