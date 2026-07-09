using UnityEngine;

public class RotateObject : MonoBehaviour
{

    [Header("1•bŠÔ‚É‰ñ“]‚·‚éŠp“x")]
    [SerializeField] private Vector3 rotationSpeed;

    private void Update()
    {

        transform.Rotate(
            rotationSpeed.x * Time.deltaTime,
            rotationSpeed.y * Time.deltaTime,
            rotationSpeed.z * Time.deltaTime,
            Space.World
        );

    }

}
