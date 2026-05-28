using UnityEngine;

// 罠や敵の弾など「ダメージを与える側」に貼るスクリプト
public class DamageSource : MonoBehaviour
{
    [Header("与えるダメージ量")]
    // インスペクターからオブジェクトごとに自由に変えられる！
    public int damageAmount = 1;
}