using System.Collections;
using UnityEngine;

public class BarrierManager : MonoBehaviour
{
    // �ǂ�����ł��A�N�Z�X�ł���悤�ɂ��邽�߂̍����t�i�V���O���g���j
    public static BarrierManager Instance { get; private set; }

    [Header("�o���A�̃v���n�u")]
    [SerializeField] private GameObject barrierPrefab;

    [Header("���[�U�[�̃v���n�u")]
    [SerializeField] private GameObject laserPrefab;

    private void Awake()
    {
        // �V�[������1�������݂���悤�ɐݒ�
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// �o���A�𐶐����鋤�ʊ֐�
    /// </summary>
    /// <param name="spawnPosition">��������ʒu</param>
    /// <param name="spawnRotation">���������]</param>
    /// <param name="parent">�Ǐ]���������e�I�u�W�F�N�g�i�C�ӁA�w�肵�Ȃ���ΓƗ����Đ����j</param>
    // �� �ύX1�F�߂�l�� void ����uBarrierDestruction�v�ɕύX
    public BarrierDestruction SpawnBarrier(Vector3 spawnPosition, Quaternion spawnRotation, Transform parent = null)
    {
        if (barrierPrefab == null)
        {
            Debug.LogWarning("BarrierManager�Ƀo���A�̃v���n�u���ݒ肳��Ă��܂���I");
            return null; // �� �ύX2�Fvoid�ł͂Ȃ��̂� null ��Ԃ�
        }

        GameObject barrier = Instantiate(barrierPrefab, spawnPosition, spawnRotation);

        // �e�I�u�W�F�N�g���w�肳��Ă�����A���̎q�ɂ���i�Ǐ]���[�h�j
        if (parent != null)
        {
            barrier.transform.SetParent(parent);
        }

        // �� �ύX3�F���������o���A�ɂ������Ă���X�N���v�g���擾���āA�Ăяo�����ɕԂ��Ă�����
        return barrier.GetComponent<BarrierDestruction>();
    }

    public GameObject SpawnLaser(Vector3 muzzlePosition, Vector3 targetPosition, float duration, Transform followParent = null)
    {
        if (laserPrefab == null)
        {
            Debug.LogWarning("BarrierManager�Ƀ��[�U�[�̃v���n�u���ݒ肳��Ă��܂���I");
            return null;
        }

        // 1. ���ˌ�����^�[�Q�b�g�ւ̕������v�Z����
        Vector3 direction = targetPosition - muzzlePosition;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward; // �[���x�N�g���΍�

        // 2. ���̕������������߂̉�]�iRotation�j���쐬����
        Quaternion lookRotation = Quaternion.LookRotation(direction);

        // 3. ���ˌ��̈ʒu�E�v�Z������]�Ń��[�U�[�𐶐�����
        //    �� ��]���������܂Ȃ��悤�A�e�ɂ͐ݒ肵�Ȃ��i�ʒu�����Ǐ]���������ꍇ�� followParent ���g���j
        GameObject laser = Instantiate(laserPrefab, muzzlePosition, lookRotation);

        // �G�̈ړ��Ɉʒu�����Ǐ]���������ꍇ�i��]�͒Ǐ]�����Ȃ��j
        if (followParent != null)
        {
            laser.transform.SetParent(followParent, true); // ���[���h���W�ێ�
                                                           // ����: SetParent����Ɖ�]���e�ɒǏ]���܂��B
                                                           // �ʒu�����Ǐ]�E��]�͌Œ�ɂ������ꍇ�͉��L�̂悤�ȒǏ]��p�X�N���v�g���K�v�ł��B
        }

        ParticleSystem ps = laser.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Collider2D playerCollider2D = player.GetComponent<Collider2D>();
                if (playerCollider2D != null)
                {
                    var triggerModule = ps.trigger;
                    triggerModule.SetCollider(0, playerCollider2D);
                }
            }
            else
            {
                Debug.LogWarning("�V�[������ 'Player' �^�O�̂����I�u�W�F�N�g��������܂���I");
            }
        }

        StartCoroutine(DestroyLaserAfterTime(laser, duration));

        return laser;
    }

    // �������ŗp�̃R���[�`��
    private IEnumerator DestroyLaserAfterTime(GameObject laserObj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (laserObj != null)
        {
            Destroy(laserObj);
        }
    }
}
