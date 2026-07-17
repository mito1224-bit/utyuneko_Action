using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【第2形態専用コンボ①】👑 5連クローン交互ビーム × 空間遮断バースト突進
/// 3回目のダッシュ時、壁に激突した瞬間にすべてのビームを吹き飛ばして即座にスタンへ移行するテンポ改善版！
/// </summary>
public class HosaP2_CloneDashState : GlitchHosaBaseState
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
    private GameObject activeDashWarningLine;
    private int dashCount = 0;
    private const int maxDashes = 3;
    private const int totalPositions = 5;

    public HosaP2_CloneDashState(GlitchHosaController boss) : base(boss) { }

    public override void Enter()
    {
        dashCount = 0;
        sniperNodes.Clear();
        activeDashWarningLine = null;
        boss.StartCoroutine(ExecuteCloneDashSequence());
    }

    private IEnumerator ExecuteCloneDashSequence()
    {
        Transform baseVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        Vector3 origScale = boss.originalVisualLocalScale;

        // ===================================================================
        // ① 5連空中陣形の構築
        // ===================================================================
        float startX = boss.stageMinX + 2.0f;
        float endX = boss.stageMaxX - 2.0f;
        float topY = boss.stageMaxY - 1.5f;

        Debug.Log("<color=orange>👥 補佐P2：ハッキング・クローンアレイ展開！スナイパー陣形を同期します。</color>");

        for (int i = 0; i < totalPositions; i++)
        {
            float ratio = (float)i / (totalPositions - 1);
            float targetX = Mathf.Lerp(startX, endX, ratio);
            Vector3 nodePos = new Vector3(targetX, topY, boss.transform.position.z);

            SniperNode node = new SniperNode { index = i, position = nodePos };

            GameObject clone = Object.Instantiate(baseVisual.gameObject, nodePos, Quaternion.identity);
            clone.name = $"HosaP2_ComboSniperClone_{i}";

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
            sniperNodes.Add(node);
        }

        yield return new WaitForSeconds(0.15f);

        float chargeDuration = 1.0f / boss.attackSpeedMultiplier;
        float fireDuration = 1.2f / boss.attackSpeedMultiplier;

        // ===================================================================
        // 🔄 突進 ＆ 交互ビーム連動ループ（全3回）
        // ===================================================================
        while (dashCount < maxDashes)
        {
            dashCount++;

            // 1. 本体の突進開始位置（右端）をプレイヤーの高さに合わせてロック
            float playerY = boss.GetPlayerTransform() != null ? boss.GetPlayerTransform().position.y : boss.transform.position.y;
            playerY = Mathf.Clamp(playerY, boss.stageMinY + 1.0f, boss.stageMaxY - 1.0f);
            Vector3 startPos = new Vector3(boss.stageMaxX - 1.5f, playerY, boss.transform.position.z);

            // 本体をワープインで右端へ登場させる
            boss.transform.position = startPos;
            yield return boss.StartCoroutine(boss.TeleportInRoutine(startPos, 0.15f));
            boss.SetBarrierActive(true, force: true);

            // 突進目標地点（左端）
            float targetLeftX = boss.stageMinX + 1.5f;
            Vector3 targetLeftPos = new Vector3(targetLeftX, startPos.y, boss.transform.position.z);

            // 2. グリッドレーザーのチャージをトリガー
            if (dashCount == 1)
            {
                foreach (var node in sniperNodes) if (node.index % 2 == 0) SetupSniperCharge(node, chargeDuration + fireDuration);
            }
            else if (dashCount == 2)
            {
                foreach (var node in sniperNodes) if (node.index % 2 != 0) SetupSniperCharge(node, chargeDuration + fireDuration);
            }
            else
            {
                foreach (var node in sniperNodes) SetupSniperCharge(node, chargeDuration + fireDuration);
            }

            // 本体の突進予兆も同時に表示
            SetupDashWarning(startPos, targetLeftPos);
            SoundManager.Instance.PlaySE(SeType.EnemyCharge);

            // 3. 同期チャージ
            float timer = 0f;
            float dirX = targetLeftPos.x - startPos.x;
            while (timer < chargeDuration)
            {
                timer += Time.deltaTime;
                boss.SetFacingDirection(dirX);

                AnimateDashWarning();
                AnimateLaserWarnings();
                yield return null;
            }

            // 発射！
            ClearDashWarning();
            if (dashCount == 1) FireSniperLasers(true);
            else if (dashCount == 2) FireSniperLasers(false);
            else FireAllSniperLasers();

            SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
            SoundManager.Instance.PlaySE(SeType.PlayerBurstBegin);
            if (ShakeTarget.Instance != null) ShakeTarget.Instance.Shake(fireDuration, 1.2f);

            // 4. マッハ突進
            yield return boss.StartCoroutine(boss.HoverMoveRoutine(targetLeftPos, 0.2f / boss.attackSpeedMultiplier));

            // ===================================================================
            // 💥【ここが修正のキモ！】3回目の突進が完了した瞬間（壁に当たった瞬間）
            // ===================================================================
            if (dashCount == maxDashes)
            {
                // ビームの終わりを待たずに、その場で即座にバリア大破エフェクトを発生させる
                var health = boss.GetComponent<GlitchHosaHealth>();
                if (health != null && health.hasBarrier)
                {
                    health.hasBarrier = false;
                    health.UpdateBarrierVisual();
                    if (health.barrierBreakEffect != null)
                    {
                        GameObject fx = Object.Instantiate(health.barrierBreakEffect, boss.transform.position, Quaternion.identity);
                        fx.transform.localScale = Vector3.one * health.barrierBreakEffectScale;
                    }
                }

                boss.SetBarrierActive(false);
                SoundManager.Instance.PlaySE(SeType.PlayerWallHit); // 激突音！
                ShakeTarget.Instance.Shake(0.35f, 2.8f);         // 画面激震！

                // 衝撃波（ショックウェーブ）の発生
                GameObject runnerObj = new GameObject("HosaShockwaveRunner");
                var runnerBehavior = runnerObj.AddComponent<ShockwaveRunnerHelper>();
                runnerBehavior.SetupAndStart(boss, targetLeftPos);

                // 👑【テンポ改善】出ているレーザーも分動的にその瞬間にすべて強制消去！
                ClearAllLasers();

                // すべての分身を一斉に縮小消滅
                foreach (var node in sniperNodes)
                {
                    if (node.cloneObject != null)
                    {
                        var helper = node.cloneObject.GetComponent<GlitchHosaCloneHelper>();
                        if (helper != null) helper.StartSquashAndDestroy(0.12f);
                        else Object.Destroy(node.cloneObject);
                        node.cloneObject = null;
                    }
                }

                SoundManager.Instance.PlaySE(SeType.EnemyCharge);
                yield return new WaitForSeconds(0.12f); // 激突の最高に気持ちいい一瞬の硬直

                CleanUpAllSessionObjects();

                // 即座にスタン状態へ！これで「ガツンと当たって気絶した感」が完璧に出ます
                boss.TransitionToState(boss.StateStun);
                yield break;
            }

            // ===================================================================
            // ※1回目、2回目の突進の時だけ、これまで通りビームの残存時間を見届ける
            // ===================================================================
            yield return new WaitForSeconds(fireDuration - 0.2f);

            if (dashCount == 1) ClearLasersByGroup(true);
            else if (dashCount == 2) ClearLasersByGroup(false);

            // 次のダッシュへの繋ぎ
            yield return boss.StartCoroutine(boss.TeleportOutRoutine(0.15f));
            boss.SetBarrierActive(false);
            yield return new WaitForSeconds(0.15f);
        }

        CleanUpAllSessionObjects();
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
            foreach (var lr in lrs) if (lr != null) lr.widthMultiplier *= boss.cloneSniperThickness;

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
                if (node.warningLine != null) { Object.Destroy(node.warningLine); node.warningLine = null; }
                if (node.laserPivot != null)
                {
                    ParticleSystem[] pss = node.laserPivot.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in pss) if (ps != null) ps.Play();
                }
            }
        }
    }

    private void FireAllSniperLasers()
    {
        foreach (var node in sniperNodes)
        {
            if (node.warningLine != null) { Object.Destroy(node.warningLine); node.warningLine = null; }
            if (node.laserPivot != null)
            {
                ParticleSystem[] pss = node.laserPivot.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in pss) if (ps != null) ps.Play();
            }
        }
    }

    private void AnimateLaserWarnings()
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
            if (isTarget && node.laserPivot != null) { Object.Destroy(node.laserPivot); node.laserPivot = null; }
        }
    }

    private void ClearAllLasers()
    {
        foreach (var node in sniperNodes)
        {
            if (node.laserPivot != null) { Object.Destroy(node.laserPivot); node.laserPivot = null; }
        }
    }

    private void SetupDashWarning(Vector3 start, Vector3 end)
    {
        Vector3 dir = end - start;
        float dist = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        if (boss.dashWarningSprite != null)
        {
            activeDashWarningLine = new GameObject("HosaP2DashWarningLine");
            activeDashWarningLine.transform.position = start + dir * 0.5f;
            activeDashWarningLine.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            SpriteRenderer sr = activeDashWarningLine.AddComponent<SpriteRenderer>();
            sr.sprite = boss.dashWarningSprite;
            sr.color = boss.dashWarningColor;
            sr.sortingOrder = -1;
            if (boss.dashWarningMaterial != null) sr.material = boss.dashWarningMaterial;

            float w = sr.sprite.bounds.size.x;
            float h = sr.sprite.bounds.size.y;
            if (w > 0.0001f && h > 0.0001f)
            {
                activeDashWarningLine.transform.localScale = new Vector3(dist / w, 1.5f / h, 1f);
            }
        }
    }

    private void AnimateDashWarning()
    {
        if (activeDashWarningLine != null)
        {
            SpriteRenderer sr = activeDashWarningLine.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float alpha = boss.dashWarningColor.a * (0.6f + Mathf.Sin(Time.time * 30f) * 0.4f);
                sr.color = new Color(boss.dashWarningColor.r, boss.dashWarningColor.g, boss.dashWarningColor.b, alpha);
            }
        }
    }

    private void ClearDashWarning()
    {
        if (activeDashWarningLine != null) { Object.Destroy(activeDashWarningLine); activeDashWarningLine = null; }
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
        ClearDashWarning();
        ClearAllLasers();
        foreach (var node in sniperNodes)
        {
            if (node.warningLine != null) Object.Destroy(node.warningLine);
            if (node.cloneObject != null)
            {
                var helper = node.cloneObject.GetComponent<GlitchHosaCloneHelper>();
                if (helper != null) helper.StartSquashAndDestroy(0.1f);
                else Object.Destroy(node.cloneObject);
            }
        }
        sniperNodes.Clear();
    }

    public override void Exit()
    {
        CleanUpAllSessionObjects();
        if (boss != null)
        {
            boss.SetBarrierActive(false);
            boss.targetXRotation = boss.defaultXRotation;
            boss.targetYRotation = boss.defaultYRotation;
            boss.targetZRotation = 0f;
        }
        boss.StopAllCoroutines();
    }
}