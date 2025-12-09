using System.Collections;
using UnityEngine;

using UnityEngine.Animations.Rigging;

public class EnemyVisuals : MonoBehaviour
{
    
    private Animator anim;
    private AudioSource audioSource;
    private EnemyMotor motor;

    
    
    

    [Header("--- CONFIGURACION DE AUDIO ---")]
    [Tooltip("Sonido de Rugido")]
    public AudioClip roarClip;
    [Tooltip("Sonido de Ataque (Impacto)")]
    public AudioClip attackClip;
    [Tooltip("Sonido Secundario de Ataque (Esfuerzo/Grito)")]
    public AudioClip secondaryAttackClip;
    [Tooltip("Sonido al romper la pared")]
    public AudioClip wallBreakSound;
    [Tooltip("Sonido de comer (Loop)")]
    public AudioClip eatingSound;

    [Header("--- SISTEMA DE PISADAS ---")]
    public AudioClip crawlFootstepClip;
    public AudioClip walkFootstepClip;
    [Tooltip("Tiempo entre pasos al gatear")]
    public float crawlFootstepInterval = 0.5f;
    [Tooltip("Tiempo entre pasos al caminar")]
    public float walkFootstepInterval = 0.35f;
    [Tooltip("Variacion aleatoria del tono (Pitch) para realismo")]
    public float pitchVariance = 0.1f;

    [Header("--- HITBOXES (COMBATE) ---")]
    [Tooltip("Collider de la mano derecha (Weapon/Claw)")]
    public GameObject rightHandCollider;
    [Tooltip("Collider de la mano izquierda")]
    public GameObject leftHandCollider;

    [Header("--- EFECTOS VISUALES (VFX) ---")]
    [Tooltip("Material con el shader de distorsion para el rugido")]
    public Material roarMaterial;
    public float maxRoarDistortion = 0.03f;

    [Header("--- ANIMATOR ---")]
    [Tooltip("Velocidad de interpolacion para los Blend Trees (Suavizado)")]
    public float animationDampTime = 5f;

    [Header("--- ANIMATION RIGGING (SISTEMA DE MIRADA) ---")]
    [Tooltip("Arrastra aqui el objeto 'IK_Rig' que tiene el componente Rig")]
    public Rig headAimRig;
    [Tooltip("Arrastra aqui el objeto 'LookTarget' que la cabeza sigue")]
    public Transform lookTarget;
    [Tooltip("Que tan lejos se mueve el target de izquierda a derecha")]
    public float scanWidth = 1.5f;
    [Tooltip("Velocidad del escaneo (movimiento de cabeza)")]
    public float scanSpeed = 2.0f;

    
    
    

    
    public bool AnimImpactReceived { get; private set; }
    public bool AnimFinishedReceived { get; private set; }

    
    private Coroutine footstepCoroutine;
    private AudioClip currentStepClip;
    private float currentStepInterval;

    
    private int _roarIntensityID;
    private int _isActiveID;
    private Coroutine roarVisualCoroutine;

    
    private bool isInvestigating = false;
    private Vector3 defaultTargetLocalPos;

    
    
    

    void Awake()
    {
        
        anim = GetComponent<Animator>();
        motor = GetComponent<EnemyMotor>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        
        DisableAllHitboxes();

        
        if (roarMaterial != null)
        {
            _roarIntensityID = Shader.PropertyToID("_RoarIntensity");
            _isActiveID = Shader.PropertyToID("_IsActive");
            roarMaterial.SetFloat(_isActiveID, 0f);
        }

        
        if (lookTarget != null)
        {
            
            defaultTargetLocalPos = lookTarget.localPosition;
        }

        
        if (headAimRig != null)
        {
            headAimRig.weight = 0f;
        }
    }

    void Update()
    {
        HandleHeadScanningLogic();
    }

    
    
    

    private void HandleHeadScanningLogic()
    {
        if (lookTarget == null || headAimRig == null) return;

        
        
        float targetRigWeight = isInvestigating ? 1f : 0f;
        headAimRig.weight = Mathf.MoveTowards(headAimRig.weight, targetRigWeight, Time.deltaTime * 2f);

        
        
        if (headAimRig.weight > 0.01f)
        {
            
            float sway = Mathf.Sin(Time.time * scanSpeed) * scanWidth;

            
            Vector3 newPos = defaultTargetLocalPos;

            
            newPos.x += sway;

            lookTarget.localPosition = newPos;
        }
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

        
        float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.deltaTime * animationDampTime);
        anim.SetFloat("Speed", newSpeed);

        
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

    public void StopAttack()
    {
        DisableAllHitboxes();
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

    
    
    

    public void ResetSyncFlags()
    {
        AnimImpactReceived = false;
        AnimFinishedReceived = false;
    }

    
    public void AE_ActionImpact()
    {
        AnimImpactReceived = true;
    }

    
    public void AE_ActionFinish()
    {
        AnimFinishedReceived = true;
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