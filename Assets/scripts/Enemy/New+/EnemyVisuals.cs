using System.Collections;
using UnityEngine;

public class EnemyVisuals : MonoBehaviour
{
    private Animator anim;
    private AudioSource audioSource;
    private EnemyMotor motor;

    [Header("Sonidos")]
    public AudioClip roarClip;
    public AudioClip attackClip;
    public AudioClip secondaryAttackClip;
    public AudioClip wallBreakSound;
    public AudioClip eatingSound;

    [Header("Pisadas")]
    public AudioClip crawlFootstepClip;
    public AudioClip walkFootstepClip;
    public float crawlFootstepInterval = 0.5f;
    public float walkFootstepInterval = 0.35f;
    public float pitchVariance = 0.1f;

    [Header("Hitboxes (Combate)")]
    public GameObject rightHandCollider;
    public GameObject leftHandCollider;

    [Header("VFX")]
    public Material roarMaterial;
    public float maxRoarDistortion = 0.03f;

    [Header("Suavizado de Animacion")]
    [Tooltip("Que tan rapido acelera/frena la animacion (Blend Tree)")]
    public float animationDampTime = 5f;

    
    private Coroutine footstepCoroutine;
    private AudioClip currentStepClip;
    private float currentStepInterval;

    
    private int _roarIntensityID;
    private int _isActiveID;
    private Coroutine roarVisualCoroutine;

    void Awake()
    {
        anim = GetComponent<Animator>();
        motor = GetComponent<EnemyMotor>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        DisableAllHitboxes();

        if (roarMaterial != null)
        {
            _roarIntensityID = Shader.PropertyToID("_RoarIntensity");
            _isActiveID = Shader.PropertyToID("_IsActive");
            roarMaterial.SetFloat(_isActiveID, 0f);
        }
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

    
    public void TriggerAttack(int attackIndex)
    {
        
        anim.ResetTrigger("Attack");

        
        anim.SetInteger("AttackIndex", attackIndex);
        anim.SetTrigger("Attack");
    }

    public void StopAttack()
    {
        
        
        DisableAllHitboxes();
    }

    public void TriggerRoar() => anim.SetTrigger("Roar");

    public void TriggerGetUp()
    {
        anim.ResetTrigger("ToCrawl"); 
        anim.SetTrigger("GetUp");
    }

    public void TriggerToCrawl()
    {
        anim.ResetTrigger("GetUp"); 
        anim.SetTrigger("ToCrawl");
    }

    
    public void SetSleep(bool state) => anim.SetBool("isSleeping", state);

    public void SetEat(bool state)
    {
        anim.SetBool("isEating", state);
        if (state && eatingSound)
        {
            if (!audioSource.isPlaying || audioSource.clip != eatingSound)
            {
                audioSource.clip = eatingSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else if (!state && audioSource.clip == eatingSound)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
    }

    
    public void PlayRoarSound() => PlayOneShot(roarClip);
    public void PlayAttackSound() { PlayOneShot(attackClip); PlayOneShot(secondaryAttackClip); }
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
        float timer = 0f;
        while (true)
        {
            timer += Time.deltaTime;
            float pulse = Mathf.Abs(Mathf.Sin(timer * 10f)) * maxRoarDistortion;
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