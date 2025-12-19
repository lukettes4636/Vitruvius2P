using System.Collections;
using UnityEngine;

public class EnemyVisuals : MonoBehaviour
{
    
    private Animator anim;
    private AudioSource audioSource;
    private EnemyMotor motor;

    
    
    

    [Header("--- AUDIO ---")]
    public AudioClip roarClip;
    public AudioClip attackClip;
    public AudioClip secondaryAttackClip;
    public AudioClip wallBreakSound;
    public AudioClip eatingSound;

    [Header("--- SISTEMA DE PISADAS ---")]
    public AudioClip crawlFootstepClip;
    public AudioClip walkFootstepClip;
    public float crawlFootstepInterval = 0.5f;
    public float walkFootstepInterval = 0.35f;
    [Tooltip("Variacion aleatoria del tono del audio")]
    public float pitchVariance = 0.1f;

    [Header("--- HITBOXES (COMBATE) ---")]
    public GameObject rightHandCollider;
    public GameObject leftHandCollider;

    [Header("--- VFX (SHADER RUGIDO) ---")]
    public Material roarMaterial;
    public float maxRoarDistortion = 0.03f;

    [Header("--- CONFIGURACION ANIMATOR ---")]
    [Tooltip("Suavizado de la transicion de caminar (Blend Tree)")]
    public float animationDampTime = 5f;

    
    public float investigateRigBlendSpeed = 2f;
    private bool overrideScan = false;
    private float overrideT = 0f;
    public bool IsScanning { get; private set; }

    
    
    

    
    public bool AnimImpactReceived { get; private set; }
    public bool AnimFinishedReceived { get; private set; }

    
    private Coroutine footstepCoroutine;
    private AudioClip currentStepClip;
    private float currentStepInterval;

    
    private int _roarIntensityID;
    private int _isActiveID;
    private Coroutine roarVisualCoroutine;

    
    private bool isInvestigating = false;
    
    
    
    

    void Awake()
    {
        anim = GetComponent<Animator>();
        motor = GetComponent<EnemyMotor>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        
        DisableAllHitboxes();
        EnsureHitboxSetup(rightHandCollider);
        EnsureHitboxSetup(leftHandCollider);

        
        if (roarMaterial != null)
        {
            _roarIntensityID = Shader.PropertyToID("_RoarIntensity");
            _isActiveID = Shader.PropertyToID("_IsActive");
            roarMaterial.SetFloat(_isActiveID, 0f);
        }
    }

    void Update()
    {
        
    }

    
    public void SetInvestigatingMode(bool state)
    {
        isInvestigating = state;
    }

    
    
    

    public void UpdateAnimationState(bool isCrawling)
    {
        
        anim.SetBool("isCrawling", isCrawling);

        
        float targetSpeed = motor.IsMoving ? 1f : 0f;
        float currentSpeed = anim.GetFloat("Speed");

        
        if (!motor.IsMoving)
        {
            anim.SetFloat("Speed", 0f);
        }
        else
        {
            float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.deltaTime * animationDampTime);
            anim.SetFloat("Speed", newSpeed);
        }

        
        if (motor.IsMoving)
        {
            AudioClip targetClip = isCrawling ? crawlFootstepClip : walkFootstepClip;
            float targetInterval = isCrawling ? crawlFootstepInterval : walkFootstepInterval;
            UpdateFootsteps(targetClip, targetInterval);
        }
        else
        {
            StopFootsteps();
        }
    }

    
    public void SetPassiveState(int stateIndex)
    {
        anim.SetFloat("PassiveType", (float)stateIndex);

        
        if (stateIndex == 1 && eatingSound != null)
        {
            if (!audioSource.isPlaying || audioSource.clip != eatingSound)
            {
                audioSource.clip = eatingSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else if (audioSource.clip == eatingSound)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
    }

    
    public void SetSleep(bool state) { if (state) SetPassiveState(0); }
    public void SetEat(bool state) { if (state) SetPassiveState(1); }

    
    
    

    public void TriggerAttack(int attackIndex)
    {
        ResetSyncFlags();
        anim.ResetTrigger("Attack");
        anim.SetInteger("AttackIndex", attackIndex);
        anim.SetTrigger("Attack");
    }

    public void TriggerRoar()
    {
        ResetSyncFlags();
        anim.SetTrigger("Roar");
    }

    public void TriggerGetUp()
    {
        ResetSyncFlags();
        anim.ResetTrigger("ToCrawl");
        anim.SetTrigger("GetUp");
    }

    public void TriggerToCrawl()
    {
        ResetSyncFlags();
        anim.ResetTrigger("GetUp");
        anim.SetTrigger("ToCrawl");
    }

    public void StopAttack() => DisableAllHitboxes();

    

    public void ResetSyncFlags()
    {
        AnimImpactReceived = false;
        AnimFinishedReceived = false;
    }

    
    public void AE_ActionImpact()
    {
        AnimImpactReceived = true;
        EnableRightHand();
        EnableLeftHand();
    }

    
    public void AE_ActionFinish()
    {
        AnimFinishedReceived = true;
        DisableAllHitboxes();
    }

    
    
    

    public void PlayRoarSound() => PlayOneShot(roarClip);

    public void PlayAttackSound()
    {
        PlayOneShot(attackClip);
        PlayOneShot(secondaryAttackClip);
    }

    public void PlayWallBreakSound() => PlayOneShot(wallBreakSound);

    private void PlayOneShot(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(clip);
        }
    }

    
    public void EnableRightHand() { if (rightHandCollider) rightHandCollider.SetActive(true); }
    public void DisableRightHand() { if (rightHandCollider) rightHandCollider.SetActive(false); }
    public void EnableLeftHand() { if (leftHandCollider) leftHandCollider.SetActive(true); }
    public void DisableLeftHand() { if (leftHandCollider) leftHandCollider.SetActive(false); }

    private void DisableAllHitboxes() { DisableRightHand(); DisableLeftHand(); }

    
    
    

    private void EnsureHitboxSetup(GameObject go)
    {
        if (!go) return;
        var col = go.GetComponent<Collider>();
        if (col) col.isTrigger = true;
        var rb = go.GetComponent<Rigidbody>();
        if (!rb)
        {
            rb = go.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    public void AE_StartRoarEffect()
    {
        if (roarMaterial)
        {
            roarMaterial.SetFloat(_isActiveID, 1f);
            if (roarVisualCoroutine != null) StopCoroutine(roarVisualCoroutine);
            roarVisualCoroutine = StartCoroutine(RoarRoutine());
        }
    }

    public void AE_StopRoarEffect()
    {
        if (roarMaterial)
        {
            roarMaterial.SetFloat(_isActiveID, 0f);
            if (roarVisualCoroutine != null) StopCoroutine(roarVisualCoroutine);
        }
    }

    private IEnumerator RoarRoutine()
    {
        float t = 0;
        while (true)
        {
            t += Time.deltaTime;
            float pulse = Mathf.Abs(Mathf.Sin(t * 10f)) * maxRoarDistortion;
            roarMaterial.SetFloat(_roarIntensityID, pulse);
            yield return null;
        }
    }

    
    
    

    private void UpdateFootsteps(AudioClip clip, float interval)
    {
        if (footstepCoroutine != null && currentStepClip == clip && currentStepInterval == interval) return;

        StopFootsteps();
        currentStepClip = clip;
        currentStepInterval = interval;
        footstepCoroutine = StartCoroutine(FootstepRoutine(clip, interval));
    }

    private void StopFootsteps()
    {
        if (footstepCoroutine != null) StopCoroutine(footstepCoroutine);
        footstepCoroutine = null;
        currentStepClip = null;
    }

    private IEnumerator FootstepRoutine(AudioClip clip, float interval)
    {
        while (true)
        {
            if (clip != null)
            {
                audioSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
                audioSource.PlayOneShot(clip);
            }
            yield return new WaitForSeconds(interval);
        }
    }
}
