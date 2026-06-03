using UnityEngine;

// 回復アイテムや回復エリアに貼るスクリプト
public class HealSource : MonoBehaviour
{
    [Header("回復の設定")]
    [Tooltip("チェックを入れると、回復量に関わらず一撃で全回復します")]
    public bool isFullHeal = false;

    [Tooltip("isFullHealにチェックがない場合のみ、この回復量が適用されます")]
    public int healAmount = 1;

    [Header("破壊の設定")]
    [Tooltip("チェックを入れると、取得後破壊される")]
    public bool isDestroy = true;
}