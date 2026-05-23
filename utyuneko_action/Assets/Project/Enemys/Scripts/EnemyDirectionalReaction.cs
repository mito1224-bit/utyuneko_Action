using System.Collections;
using UnityEngine;

public class EnemyDirectionalReaction : MonoBehaviour
{

    [System.Serializable]
    public struct AngleRange
    {
        public float min;
        public float max;

        public bool Contains(float angleDeg)
        {
            float a = Normalize(angleDeg);
            float mn = Normalize(min);
            float mx = Normalize(max);

            if (mn <= mx) return a >= mn && a <= mx;
            return a >= mn || a <= mx;
        }

        private static float Normalize(float a)
        {
            return Mathf.Repeat(a + 180f, 360f) - 180f;
        }
    }

    [Header("判定対象")]
    public string playerTag = "Player";

    [Header("貫通を許可する角度帯（複数可）")]
    [Tooltip("進入方向をXZ平面に投影した角度（度）。右(+X)=0、前(+Z)=90、左=180/-180、後(-Z)=-90")]
    public AngleRange[] pierceRanges;

    [Header("すり抜け設定")]
    public float pierceIgnoreTime = 0.12f;

    [Header("反射（ノックバック）設定")]
    public float knockbackForce = 15f;

    private Collider myCol;

    void Awake()
    {
        myCol = GetComponent<Collider>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(playerTag)) return;
        if (myCol == null) return;

        Collider playerCol = collision.collider;
        Rigidbody playerRb = collision.rigidbody;

        Vector3 approach = collision.relativeVelocity;
        if (approach.sqrMagnitude < 0.0001f)
            approach = (transform.position - collision.transform.position);

        // XZ平面に投影して角度計算
        Vector3 approachXZ = new Vector3(approach.x, 0f, approach.z);
        if (approachXZ.sqrMagnitude < 0.0001f)
            approachXZ = new Vector3(approach.x, 0f, approach.z);

        approachXZ = approachXZ.normalized;

        float angle = Mathf.Atan2(approachXZ.z, approachXZ.x) * Mathf.Rad2Deg; // -180..180

        if (IsAngleInPierceRanges(angle))
        {
            if (playerCol != null)
                StartCoroutine(TemporaryIgnoreCollision3D(playerCol, pierceIgnoreTime));
            return;
        }

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            Vector3 knockDir = (collision.transform.position - transform.position);
            knockDir.y = 0f; // 2.5DならY方向に弾かない
            knockDir = knockDir.normalized;

            playerRb.AddForce(knockDir * knockbackForce, ForceMode.Impulse);
        }
    }

    private bool IsAngleInPierceRanges(float angleDeg)
    {
        if (pierceRanges == null || pierceRanges.Length == 0) return false;
        for (int i = 0; i < pierceRanges.Length; i++)
            if (pierceRanges[i].Contains(angleDeg)) return true;
        return false;
    }

    private IEnumerator TemporaryIgnoreCollision3D(Collider playerCol, float seconds)
    {
        Physics.IgnoreCollision(myCol, playerCol, true);
        yield return new WaitForSeconds(seconds);
        if (myCol != null && playerCol != null)
            Physics.IgnoreCollision(myCol, playerCol, false);
    }
}