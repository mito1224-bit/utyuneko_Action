using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 👑 崩壊した補佐：２段階連鎖衝撃波コントロールヘルパー（完全独立ファイル版）
/// ボスのコルーチン消滅命令から完全に独立し、壁沿い ➔ 天井・床沿いの
/// ２段階の枝分かれ伝播衝撃波を完璧に完走させます。
/// </summary>
public class ShockwaveRunnerHelper : MonoBehaviour
{
    private GlitchHosaController controller;

    public void SetupAndStart(GlitchHosaController boss, Vector3 impactPos)
    {
        this.controller = boss;

        // 3.5秒後に、このヘルパー自体を安全に自動消滅させてメモリをクリーンにします
        Destroy(gameObject, 3.5f);

        // 上方向と下方向の垂直衝撃波コルーチンを、それぞれ完全に独立して並列スタート！
        StartCoroutine(VerticalUpRoutine(impactPos));
        StartCoroutine(VerticalDownRoutine(impactPos));
    }

    private IEnumerator VerticalUpRoutine(Vector3 colorImpactPos)
    {
        float stepInterval = 1.3f; // 配置間隔
        float timeDelay = 0.04f;   // 伝播スピード

        float currentY = colorImpactPos.y;
        while (currentY < controller.stageMaxY)
        {
            currentY += stepInterval;

            // 天井端を超えそうなら、天井端ジャストの位置に補正
            if (currentY >= controller.stageMaxY)
            {
                currentY = controller.stageMaxY;
            }

            Vector3 targetPos = new Vector3(colorImpactPos.x, currentY, 0f);
            SpawnShockwaveExplosionDirectly(controller, targetPos);

            if (currentY >= controller.stageMaxY) break;

            yield return new WaitForSeconds(timeDelay);
        }

        // 天井端に到達した瞬間に、天井に沿って左右へ広がる衝撃波コルーチンを即座にキック！
        StartCoroutine(HorizontalSpreadRoutine(controller.stageMaxY, colorImpactPos.x));
    }

    private IEnumerator VerticalDownRoutine(Vector3 colorImpactPos)
    {
        float stepInterval = 1.3f;
        float timeDelay = 0.04f;

        float currentY = colorImpactPos.y;
        while (currentY > controller.stageMinY)
        {
            currentY -= stepInterval;

            // 床端を超えそうなら、床端ジャストの位置に補正
            if (currentY <= controller.stageMinY)
            {
                currentY = controller.stageMinY;
            }

            Vector3 targetPos = new Vector3(colorImpactPos.x, currentY, 0f);
            SpawnShockwaveExplosionDirectly(controller, targetPos);

            if (currentY <= controller.stageMinY) break;

            yield return new WaitForSeconds(timeDelay);
        }

        // 床端に到達した瞬間に、床に沿って左右へ広がる衝撃波コルーチンを即座にキック！
        StartCoroutine(HorizontalSpreadRoutine(controller.stageMinY, colorImpactPos.x));
    }

    private IEnumerator HorizontalSpreadRoutine(float fixedY, float startX)
    {
        float stepInterval = 1.3f; // 爆発の配置間隔
        float timeDelay = 0.04f;   // 左右に伝わるスピード

        int leftSteps = Mathf.CeilToInt((startX - controller.stageMinX) / stepInterval) + 1;
        int rightSteps = Mathf.CeilToInt((controller.stageMaxX - startX) / stepInterval) + 1;
        int totalSteps = Mathf.Max(leftSteps, rightSteps);

        for (int i = 1; i <= totalSteps; i++)
        {
            bool hasSpawnedAny = false;

            // 👈 左方向への衝撃波配置
            float currentXLeft = startX - (i * stepInterval);
            if (currentXLeft >= controller.stageMinX - 1.5f)
            {
                if (currentXLeft < controller.stageMinX) currentXLeft = controller.stageMinX;

                Vector3 targetPos = new Vector3(currentXLeft, fixedY, 0f);
                SpawnShockwaveExplosionDirectly(controller, targetPos);
                hasSpawnedAny = true;
            }

            // 👉 右方向への衝撃波配置
            float currentXRight = startX + (i * stepInterval);
            if (currentXRight <= controller.stageMaxX + 1.5f)
            {
                if (currentXRight > controller.stageMaxX) currentXRight = controller.stageMaxX;

                Vector3 targetPos = new Vector3(currentXRight, fixedY, 0f);
                SpawnShockwaveExplosionDirectly(controller, targetPos);
                hasSpawnedAny = true;
            }

            if (!hasSpawnedAny) break;

            yield return new WaitForSeconds(timeDelay);
        }
    }

    private void SpawnShockwaveExplosionDirectly(GlitchHosaController boss, Vector3 spawnTargetPos)
    {
        if (boss.shockwavePrefab != null)
        {
            Object.Instantiate(boss.shockwavePrefab, spawnTargetPos, Quaternion.identity);
            return;
        }

        if (boss.timedBombPrefab == null) return;

        GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, spawnTargetPos, Quaternion.identity);
        foreach (var renderer in bombObj.GetComponentsInChildren<Renderer>(true)) if (renderer != null) renderer.enabled = false;
        foreach (var spriteRenderer in bombObj.GetComponentsInChildren<SpriteRenderer>(true)) if (spriteRenderer != null) spriteRenderer.enabled = false;

        Transform indicator = bombObj.transform.Find("Indicator");
        if (indicator != null) indicator.gameObject.SetActive(false);
        Transform indicatorRoot = bombObj.transform.Find("IndicatorRoot");
        if (indicatorRoot != null) indicatorRoot.gameObject.SetActive(false);

        var bomb = bombObj.GetComponent<GlitchHosaTimedBomb>();
        if (bomb != null)
        {
            bomb.explodeOnLand = true;
            bomb.InitializeToss(spawnTargetPos, spawnTargetPos, 0.01f, 0f);
        }
    }
}