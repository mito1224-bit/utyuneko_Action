using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class LaserVisualController : MonoBehaviour
{
    private ParticleSystem ps;
    private ParticleSystem.MainModule main;

    private float baseStartSizeY;
    private float baseStartSizeZ;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        main = ps.main;

        baseStartSizeY = main.startSizeY.constant;
        baseStartSizeZ = main.startSizeZ.constant;
    }

    /// <summary>
    /// レーザーの長さ（ワールド単位）と太さ（倍率、1=プレハブそのまま）を設定してから再生する。
    /// Play On Awake はプレハブ側でOFFにしておくこと（このメソッドの前に自動再生されるのを防ぐため）。
    /// </summary>
    public void Configure(float worldLength, float widthMultiplier = 1f)
    {
        // 念のため再生前提でクリア
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 長さ = Speed × Lifetime なので、Lifetimeは固定してSpeedを逆算する
        float lifetime = main.startLifetime.constant;
        if (lifetime > 0f)
        {
            var speed = main.startSpeed;
            speed.constant = worldLength / lifetime;
            main.startSpeed = speed;
        }

        // 太さ = Start Size の Y・Z（Xは伸び計算用なので触らない）
        var sizeY = main.startSizeY;
        sizeY.constant = baseStartSizeY * widthMultiplier;
        main.startSizeY = sizeY;

        var sizeZ = main.startSizeZ;
        sizeZ.constant = baseStartSizeZ * widthMultiplier;
        main.startSizeZ = sizeZ;

        ps.Play();
    }
}
