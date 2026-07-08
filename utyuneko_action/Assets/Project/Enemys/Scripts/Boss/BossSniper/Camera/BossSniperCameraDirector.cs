using System.Collections;
using UnityEngine;

/// <summary>
/// �{�X�X�i�C�p�[��̃J�������o�W�i�o���E�����J�b�g�C���j�B
///
/// �ʃ{�X�� StageSecondBossPhaseTransitionState�i���퓮�삵�Ă�������j�Ɠ����菇�𓥂�:
///   1. �����̃J�����Œ�I�u�W�F�N�g�iCameraBoundsTrigger ��t�����I�u�W�F�N�g�j�� SetActive(false) �ɂ���
///   2. ����Ɍ����Ȃ��ꎞ�^�[�Q�b�g�����AcameraFollow.StartTrackTarget �ł��������b�N�I��
///      �i�ꎞ�^�[�Q�b�g�� z �����̂܂܃J�����̊�苗���ɂȂ�j
///   3. �V�F�C�N��X�P�[�����̉��o������
///   4. ForceStopEventCameraWork �ŃC�x���g��Ԃ���݁A�����̌Œ�_�փ��b�N������
///   5. �ꎞ�^�[�Q�b�g��j�����A�Œ�I�u�W�F�N�g�� SetActive(true) �ɖ߂�
///
/// �v���C���[�̑��샍�b�N�� StartTrackTarget �̓����iSetPlayerActiveState(false)�j���s���B
/// �{�X�Ƃ������g�̍s����~�� bossSniper.SetEventPaused(true/false) ���s���B
///
/// ���j�C�x���g�̃J������ BossSniperAbsorbEventManager �����S�����A
/// �Œ�I�u�W�F�N�g�� ON/OFF �͂��̃N���X�̐ÓI SuspendBoundsLock/ResumeBoundsLock ���g���B
///
/// �Z�b�g�A�b�v:
///   - ��I�u�W�F�N�g�ɂ��̃R���|�[�l���g��t���AbossSniper �� boundsTriggerObject �����蓖�Ă�B
///     �iboundsTriggerObject = �{�X������ CameraBoundsTrigger ��t�����I�u�W�F�N�g�j
///   - cameraFollow �͖��ݒ�Ȃ� MainCamera �^�O�̐e���玩���擾�B
///   - StageBossSniperTrigger �� cameraDirector �ɂ��̃I�u�W�F�N�g�����蓖�Ă�B
/// </summary>
public class BossSniperCameraDirector : MonoBehaviour
{
    // Absorb�C�x���g������Œ�I�u�W�F�N�g�𑀍삷�邽�߂̐ÓI����
    private static BossSniperCameraDirector instance;

    /// <summary>�����̃J�����Œ�I�u�W�F�N�g���ꎞ��~�iSetActive(false)�j�B�C�x���g�J�n���ɌĂԁB</summary>
    public static void SuspendBoundsLock()
    {
        if (instance != null) instance.SetBoundsObjectActive(false);
    }

    /// <summary>�����̃J�����Œ�I�u�W�F�N�g���ĊJ�iSetActive(true)�j�B�C�x���g�I�����ɌĂԁB</summary>
    public static void ResumeBoundsLock()
    {
        if (instance != null) instance.SetBoundsObjectActive(true);
    }

    /// <summary>
    /// �����̌Œ�_�iCameraPoint / targetZOffset�j�փJ�����̃��b�N�𒼐ڊ|�������ÓI�����B
    /// �g���K�[�̍Ĕ��΁iEnter/Stay�j�Ɉˑ������m���ɕ����̍\�}�֖߂���B
    /// ���j�C�x���g�iBossSniperAbsorbEventManager�j�̏I������������ĂԁB
    /// </summary>
    public static void RelockRoomCamera()
    {
        if (instance != null) instance.RelockToBoundsPoint();
    }

    [Header("�Q��")]
    [Tooltip("�{�X�X�i�C�p�[�{�́BonEnraged / onDefeated ���R�[�h�w�ǂ���")]
    [SerializeField] private BossSniper bossSniper;

    [Tooltip("�J��������B���ݒ�Ȃ� MainCamera �^�O�̐e���玩���擾����")]
    [SerializeField] private CameraFollowWithZoom cameraFollow;

    [Tooltip("�{�X�����̃J�����Œ�I�u�W�F�N�g�iCameraBoundsTrigger ��t�����I�u�W�F�N�g�j�B" +
             "�C�x���g���͂���� SetActive(false) �ɂ��āA�J�����̊��^�Y�[�����ז����Ȃ��悤�ɂ���")]
    [SerializeField] private GameObject boundsTriggerObject;

    [Header("�o�����o")]
    [Tooltip("������Ƃ��̃J���������iz���W�B�ʏ� -10�A�߂��ق� 0 �ɋ߂Â���j")]
    [SerializeField] private float introCameraZ = -7f;
    [Tooltip("�J��������鑬���i�ʒu�j�B�傫���قǑ���")]
    [SerializeField] private float introPositionSpeed = 3f;
    [Tooltip("�J��������鑬���i�Y�[���j�B�傫���قǑ���")]
    [SerializeField] private float introZoomSpeed = 2f;
    [Tooltip("���n�߂Ă���Ռ��V�F�C�N���o���܂ł̑҂����ԁi�b�j")]
    [SerializeField] private float introApproachWait = 0.7f;
    [Tooltip("�������ԂŌ����鎞�ԁi�b�j")]
    [SerializeField] private float introHold = 1.2f;
    [Tooltip("�v���C���[�֖߂鎞�ԁi�b�j")]
    [SerializeField] private float introReturnTime = 1.0f;
    [Tooltip("����Ă����Ԃ̒n��i�c�̂ݔ��U���j�̕b���E����")]
    [SerializeField] private float introRumbleDuration = 0.9f;
    [SerializeField] private float introRumbleMagnitude = 0.15f;
    [Tooltip("�o���̏Ռ��i�J�����V�F�C�N�j�̕b���E����")]
    [SerializeField] private float introImpactDuration = 0.35f;
    [SerializeField] private float introImpactMagnitude = 0.7f;
    [Tooltip("�o���̏Ռ��i�{�X���f���̗h��j�̋����E�b��")]
    [SerializeField] private float introBossShakeStrength = 0.15f;
    [SerializeField] private float introBossShakeDuration = 0.25f;

    [Header("�����J�b�g�C���i�o�����T���߁j")]
    [Tooltip("������Ƃ��̃J���������iz���W�j�B�o�����T���߂Ɂi-8 �ȂǁA-7 �������C���j")]
    [SerializeField] private float enrageCameraZ = -8f;
    [SerializeField] private float enragePositionSpeed = 3.5f;
    [SerializeField] private float enrageZoomSpeed = 2.5f;
    [Tooltip("���n�߂Ă���{�X�����ʂ������n�߂�܂ł̑҂����ԁi�b�j")]
    [SerializeField] private float enrageApproachWait = 0.5f;
    [Tooltip("�{�X�����ʂ������̂ɂ����鎞�ԁi�b�j")]
    [SerializeField] private float enrageFaceFrontTime = 0.3f;
    [Tooltip("���f�������ʁi�J�������j�������Ƃ��� Y ��]�p�B���f���̍��ɂ�� 90 / -90 / 0 �Ȃǂɒ���")]
    [SerializeField] private float enrageFrontYAngle = 90f;
    [Tooltip("���ʂ������Ă���̃^�����ԁi�b�j�B���̊ԂɃV�F�C�N������")]
    [SerializeField] private float enrageHoldTime = 0.45f;
    [Tooltip("���̌����֖߂����ԁi�b�j")]
    [SerializeField] private float enrageFaceBackTime = 0.25f;
    [Tooltip("�v���C���[�֖߂鎞�ԁi�b�j")]
    [SerializeField] private float enrageReturnTime = 0.7f;
    [Tooltip("�o���̏u�Ԃ̃J�����V�F�C�N�i�b���E�����j")]
    [SerializeField] private float enrageShakeDuration = 0.35f;
    [SerializeField] private float enrageShakeMagnitude = 0.6f;
    [Tooltip("�o���̏u�Ԃ̃{�X���f���̗h��i�����E�b���j")]
    [SerializeField] private float enrageBossShakeStrength = 0.25f;
    [SerializeField] private float enrageBossShakeDuration = 0.4f;

    private BossSniperShake bossShake;
    private Coroutine playingRoutine;
    private GameObject tempCameraTarget;

    void Awake()
    {
        instance = this;

        if (cameraFollow == null)
        {
            GameObject mainCam = GameObject.FindWithTag("MainCamera");
            if (mainCam != null) cameraFollow = mainCam.GetComponentInParent<CameraFollowWithZoom>();
            if (cameraFollow == null) cameraFollow = Object.FindFirstObjectByType<CameraFollowWithZoom>();
        }

        if (bossSniper != null) bossShake = bossSniper.GetComponent<BossSniperShake>();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void OnEnable()
    {
        if (bossSniper != null)
        {
            bossSniper.onEnraged.AddListener(PlayEnrageCutin);
            bossSniper.onDefeated.AddListener(CancelCutscenes);
        }
        else
        {
            Debug.LogWarning("[CameraDirector] bossSniper �������蓖�Ăł��B�C���X�y�N�^�[�Ń{�X�{�̂����蓖�ĂĂ��������B");
        }
    }

    void OnDisable()
    {
        if (bossSniper != null)
        {
            bossSniper.onEnraged.RemoveListener(PlayEnrageCutin);
            bossSniper.onDefeated.RemoveListener(CancelCutscenes);
        }
    }

    private void SetBoundsObjectActive(bool active)
    {
        if (boundsTriggerObject != null) boundsTriggerObject.SetActive(active);
    }

    // �C�x���g��ɕ����̌Œ�_�� Z ���Ɗm���ɖ߂��B
    // CameraBoundsTrigger �̍ă��b�N�� OnTriggerStay2D ���݂����A�v���C���[���Î~����
    // Rigidbody2D ���X���[�v���Ă���Ɣ��΂����A�񂹂� Z(-7/-8)���c���Ă��܂��B
    // ������ CameraBoundsTrigger �Ɠ����Œ�_�EtargetZOffset ��ǂ�ŁA�����ň�x����
    // LockCamera �𒼐ڌĂсAZ �𕔉��̒l(-15��)�֖߂��B
    private void RelockToBoundsPoint()
    {
        if (cameraFollow == null || boundsTriggerObject == null) return;

        // �Œ�_�F�q "CameraPoint" ������΂��̈ʒu�A������΃R���C�_�[���S�A�����������Ζ{�̈ʒu
        Vector3 lockPos;
        Transform camPoint = boundsTriggerObject.transform.Find("CameraPoint");
        if (camPoint != null)
        {
            lockPos = camPoint.position;
        }
        else if (boundsTriggerObject.TryGetComponent<Collider2D>(out var col))
        {
            lockPos = col.bounds.center;
        }
        else
        {
            lockPos = boundsTriggerObject.transform.position;
        }

        // targetZOffset�i�����ʁj�� CameraBoundsTrigger ����ǂ�
        float z = -40f;
        if (boundsTriggerObject.TryGetComponent<CameraBoundsTrigger>(out var bounds))
        {
            z = bounds.targetZOffset;
        }

        // ���b�N�ʒu�ɕ��ꍞ�� z�iCameraBoundsTrigger �I�u�W�F�N�g���g�� z�j�͎g��Ȃ��B
        // ���s���i�����ʁj�͕K�� z �ɓ��ꂷ��
        lockPos.z = z;
        cameraFollow.LockCamera(lockPos, z);
        Debug.Log($"<color=lime>[Relock] hasBounds={boundsTriggerObject.TryGetComponent<CameraBoundsTrigger>(out _)} lockPos=({lockPos.x:F1},{lockPos.y:F1},{lockPos.z:F1}) z={z:F1}</color>");
    }

    // ����̈ꎞ�^�[�Q�b�g�����iz ����苗���ɂȂ�j
    private void CreateTempTarget(Vector3 worldPos, float cameraZ)
    {
        DestroyTempTarget();
        tempCameraTarget = new GameObject("TempCameraEventTarget");
        tempCameraTarget.transform.position = new Vector3(worldPos.x, worldPos.y, cameraZ);
    }

    private void DestroyTempTarget()
    {
        if (tempCameraTarget != null)
        {
            Object.Destroy(tempCameraTarget);
            tempCameraTarget = null;
        }
    }

    // ������ �o�����o ����������������������������������������������������������

    /// <summary>�o�����o���Đ�����iStageBossSniperTrigger ����{�X�L�����̒���ɌĂԁj�B</summary>
    public void PlayIntro()
    {
        Debug.Log("[CameraDirector] PlayIntro �J�n");
        if (playingRoutine != null) return;
        if (bossSniper == null || cameraFollow == null) return;
        playingRoutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        Debug.Log("[CameraDirector] IntroRoutine �J�n");

        SetBoundsObjectActive(false);
        // �U���E�e���|�[�g�͎~�߂�i�o���A�j���̃X�P�[���͕ʂœ���������j
        bossSniper.SetEventPaused(true);

        // �@ �����̌Œ�J�������I�t �� �A ����^�[�Q�b�g�쐬 �� StartTrackTarget �Ŋ��

        CreateTempTarget(bossSniper.transform.position, introCameraZ);
        cameraFollow.StartTrackTarget(tempCameraTarget.transform, introPositionSpeed, introZoomSpeed);

        // ����Ă����Ԃ̒n��i�c�̂ݔ��U���j
        if (ShakeTarget.Instance != null)
            ShakeTarget.Instance.Shake(introRumbleDuration, introRumbleMagnitude, 25f, false);

        yield return new WaitForSeconds(introApproachWait);

        // �o���̏Ռ�
        if (ShakeTarget.Instance != null)
            ShakeTarget.Instance.Shake(introImpactDuration, introImpactMagnitude, 15f);
        if (bossShake != null)
            bossShake.ShakeRandom(introBossShakeStrength, introBossShakeDuration);

        // �������ԂŌ�����
        yield return new WaitForSeconds(introHold);

        // ���� �I���i�A�����b�N����؋��܂Ȃ��ŏ��\���j����
        // �@ �܂������g���K�[�𕜊��i������ Enter/Stay ���b�N�͖�������Ă��\��Ȃ��B����ł͇B�j
        SetBoundsObjectActive(true);

        // �A �C�x���g��Ԃ�������ށi�v���C���[����E���x�����EisEventWorking=false�j�B
        //    ForceStop �̓A�����b�N���v���C���[�ʒu�ւ̃��[�v�𔺂����A
        //    "�����t���[������" ����ɇB�̃��b�N�֍����ւ���̂ŒǏ]�v�Z�� 1 �t���[��������Ȃ�
        cameraFollow.ForceStopEventCameraWork();

        // �B �����̌Œ�_�iXY�j�{�����ʁiz�j�փ��b�N�������B����� z ���m���ɖ߂�
        RelockToBoundsPoint();

        DestroyTempTarget();
        bossSniper.SetEventPaused(false);
        playingRoutine = null;
    }

    // ������ �����J�b�g�C�� ������������������������������������������������

    /// <summary>�����J�b�g�C�����Đ�����ionEnraged ���玩���ŌĂ΂��j�B</summary>
    public void PlayEnrageCutin()
    {
        if (playingRoutine != null) return;
        if (bossSniper == null || cameraFollow == null) return;
        playingRoutine = StartCoroutine(EnrageRoutine());
    }

    private IEnumerator EnrageRoutine()
    {
        SetBoundsObjectActive(false);
        // �X�^���ꌂ�Ȃǂ̑S�̃X���[�itimeScale�ቺ�j�������Ă���n�߂�
        yield return new WaitUntil(() => Time.timeScale >= 0.99f);

        // �U���E�e���|�[�g���~�߂�i�������ː���������j
        bossSniper.SetEventPaused(true);

        // �@ �����̌Œ�J�������I�t �� �A ����^�[�Q�b�g �� ���

        CreateTempTarget(bossSniper.transform.position, enrageCameraZ);
        cameraFollow.StartTrackTarget(tempCameraTarget.transform, enragePositionSpeed, enrageZoomSpeed);

        yield return new WaitForSeconds(enrageApproachWait);

        // �{�X�����ʂ�����
        Transform visual = bossSniper.SelfUnit != null ? bossSniper.SelfUnit.visualTransform : null;
        Quaternion originalRot = visual != null ? visual.localRotation : Quaternion.identity;
        Quaternion frontRot = Quaternion.Euler(0f, enrageFrontYAngle, 0f);
        yield return RotateVisual(visual, originalRot, frontRot, enrageFaceFrontTime);

        // �o���̏u�ԁF�{�X�̗h��{�J�����V�F�C�N
        if (bossShake != null)
            bossShake.ShakeRandom(enrageBossShakeStrength, enrageBossShakeDuration);
        if (ShakeTarget.Instance != null)
            ShakeTarget.Instance.Shake(enrageShakeDuration, enrageShakeMagnitude, 12f);

        yield return new WaitForSeconds(enrageHoldTime);

        // ���������Ă���A��
        yield return RotateVisual(visual, frontRot, originalRot, enrageFaceBackTime);

        // ���� �I���i�A�����b�N����؋��܂Ȃ��ŏ��\���j����
        SetBoundsObjectActive(true);
        cameraFollow.ForceStopEventCameraWork();
        RelockToBoundsPoint();

        DestroyTempTarget();
        bossSniper.SetEventPaused(false);
        playingRoutine = null;
    }

    // �����ڂ� from �� to �֊��炩�ɉ�
    private IEnumerator RotateVisual(Transform visual, Quaternion from, Quaternion to, float duration)
    {
        if (visual == null || duration <= 0f)
        {
            if (visual != null) visual.localRotation = to;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            visual.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        visual.localRotation = to;
    }

    // ������ �ł��؂� ����������������������������������������������������������

    /// <summary>
    /// ���s���̃J�b�g�C����ł��؂��Č�n������i���j���� onDefeated �o�R�Ŏ����ŌĂ΂��j�B
    /// �J�����{�̂̌�n���iReturnToPlayerFromEvent / ForceStop�j�͌��j�C�x���g�����s���̂ŁA
    /// �����ł͈ꎞ�^�[�Q�b�g�̔j���E�Œ�J�����̕����E�{�X�̍ĊJ�������m���ɍς܂���B
    /// �i���j�C�x���g���͒���ɉ��߂� SuspendBoundsLock ����̂œ�d�ł����Ȃ��j
    /// </summary>
    public void CancelCutscenes()
    {
        if (playingRoutine != null)
        {
            StopCoroutine(playingRoutine);
            playingRoutine = null;
        }

        DestroyTempTarget();

        if (bossSniper != null) bossSniper.SetEventPaused(false);
        SetBoundsObjectActive(true);
    }
}