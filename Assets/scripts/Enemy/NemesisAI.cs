using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(NemesisDetectionHelper))]
public class NemesisAI : MonoBehaviour
{
    [Header("Nemesis AI Configuration")]
    [Tooltip("Walking speed only - no crawling for this nemesis")]
    public float walkSpeed = 3.5f;
    public float chaseSpeed = 5f;
    public float rotationSpeed = 8f;
    
    [Header("Detection Settings")]
    public float detectionRadius = 25f;
    public float attackRange = 2.5f;
    public float soundDetectionRadius = 30f;
    public LayerMask detectionLayerMask;
    public LayerMask soundBlockerLayer;
    
    [Header("Attack Settings")]
    public float attackCooldown = 1.5f;
    public int attackDamage = 25;
    public float attackDuration = 0.8f;
    
    [Header("Targeting Priority")]
    [Tooltip("Higher priority targets will be focused first")]
    public float npcPriority = 2f;
    public float playerPriority = 1f;
    
    [Header("Audio")]
    public AudioClip[] attackSounds;
    public AudioClip[] detectionSounds;
    public AudioClip[] footstepSounds;
    
    [Header("References")]
    public Animator animator;
    private NavMeshAgent agent;
    private AudioSource audioSource;
    
    
    private Transform currentTarget;
    private Vector3 lastKnownPosition;
    private float lastDetectionTime;
    private bool isAlerted;
    private bool isAttacking;
    private bool canAttack = true;
    
    private NemesisDetectionHelper detectionHelper;
    
    private enum TargetType { None, Player1, Player2, NPC }
    private TargetType currentTargetType;
    
    
    private readonly int walkHash = Animator.StringToHash("Walk");
    private readonly int attackHash = Animator.StringToHash("Attack");
    private readonly int detectionHash = Animator.StringToHash("Detected");
    
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        detectionHelper = GetComponent<NemesisDetectionHelper>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (detectionHelper == null)
        {
            detectionHelper = gameObject.AddComponent<NemesisDetectionHelper>();
        }
        
        ConfigureNavMeshAgent();
    }
    
    void Start()
    {
        
        isAlerted = false;
        currentTarget = null;
        currentTargetType = TargetType.None;
        
        
        animator.SetBool(walkHash, false);
        animator.SetBool(attackHash, false);
        
        
        if (detectionHelper != null)
        {
            detectionHelper.obstacleLayerMask = soundBlockerLayer;
            detectionHelper.targetLayerMask = detectionLayerMask;
        }
        
        NemesisTester tester = GetComponent<NemesisTester>();
        if (tester == null)
        {
            tester = gameObject.AddComponent<NemesisTester>();
        }
        
        NemesisValidator validator = GetComponent<NemesisValidator>();
        if (validator == null)
        {
            validator = gameObject.AddComponent<NemesisValidator>();
        }
    }
    
    void Update()
    {
        if (isAttacking) return;
        
        
        DetectTargets();
        
        
        if (currentTarget != null)
        {
            ChaseTarget();
        }
        else if (isAlerted)
        {
            SearchLastKnownPosition();
        }
        else
        {
            SearchForTargets();
        }
        
        UpdateAnimation();
    }
    
    void ConfigureNavMeshAgent()
    {
        agent.speed = walkSpeed;
        agent.angularSpeed = 360f;
        agent.acceleration = 12f;
        agent.stoppingDistance = attackRange - 0.3f;
        agent.autoBraking = true;
        agent.updateRotation = true;
    }
    
    void DetectTargets()
    {
        Transform bestTarget = null;
        TargetType bestTargetType = TargetType.None;
        float bestPriority = 0f;
        float closestDistance = Mathf.Infinity;
        
        
        if (detectionHelper != null)
        {
            
            GameObject player1 = GameObject.FindGameObjectWithTag("Player1");
            if (player1 != null && detectionHelper.CheckTargetDetection(player1.transform, detectionRadius, "Player1"))
            {
                var playerHealth = player1.GetComponent<PlayerHealth>();
                if (playerHealth != null && !playerHealth.IsDead)
                {
                    float distance = Vector3.Distance(transform.position, player1.transform.position);
                    float priority = playerPriority / distance;
                    if (priority > bestPriority)
                    {
                        bestPriority = priority;
                        bestTarget = player1.transform;
                        bestTargetType = TargetType.Player1;
                        closestDistance = distance;
                    }
                }
            }
        }
        else
        {
            
            GameObject player1 = GameObject.FindGameObjectWithTag("Player1");
            if (player1 != null)
            {
                float distance = Vector3.Distance(transform.position, player1.transform.position);
                if (distance <= detectionRadius && CanDetectTarget(player1.transform))
                {
                    var playerHealth = player1.GetComponent<PlayerHealth>();
                    if (playerHealth != null && !playerHealth.IsDead)
                    {
                        float priority = playerPriority / distance;
                        if (priority > bestPriority)
                        {
                            bestPriority = priority;
                            bestTarget = player1.transform;
                            bestTargetType = TargetType.Player1;
                            closestDistance = distance;
                        }
                    }
                }
            }
        }
        
        
        GameObject player2 = GameObject.FindGameObjectWithTag("Player2");
        if (player2 != null)
        {
            if (detectionHelper != null)
            {
                if (detectionHelper.CheckTargetDetection(player2.transform, detectionRadius, "Player2"))
                {
                    var playerHealth = player2.GetComponent<PlayerHealth>();
                    if (playerHealth != null && !playerHealth.IsDead)
                    {
                        float distance = Vector3.Distance(transform.position, player2.transform.position);
                        float priority = playerPriority / distance;
                        if (priority > bestPriority)
                        {
                            bestPriority = priority;
                            bestTarget = player2.transform;
                            bestTargetType = TargetType.Player2;
                            closestDistance = distance;
                        }
                    }
                }
            }
            else
            {
                
                float distance = Vector3.Distance(transform.position, player2.transform.position);
                if (distance <= detectionRadius && CanDetectTarget(player2.transform))
                {
                    var playerHealth = player2.GetComponent<PlayerHealth>();
                    if (playerHealth != null && !playerHealth.IsDead)
                    {
                        float priority = playerPriority / distance;
                        if (priority > bestPriority)
                        {
                            bestPriority = priority;
                            bestTarget = player2.transform;
                            bestTargetType = TargetType.Player2;
                            closestDistance = distance;
                        }
                    }
                }
            }
        }
        
        
        
        GameObject npc = GameObject.FindGameObjectWithTag("NPC");
        if (npc != null)
        {
            if (detectionHelper != null)
            {
                if (detectionHelper.CheckTargetDetection(npc.transform, detectionRadius, "NPC"))
                {
                    var npcHealth = npc.GetComponent<NPCHealth>();
                    if (npcHealth != null && !npcHealth.IsDead)
                    {
                        float distance = Vector3.Distance(transform.position, npc.transform.position);
                        float priority = npcPriority / distance;
                    if (priority > bestPriority)
                    {
                        bestPriority = priority;
                        bestTarget = npc.transform;
                        bestTargetType = TargetType.NPC;
                        closestDistance = distance;
                    }
                }
            }
            else
            {
                
                float distance = Vector3.Distance(transform.position, npc.transform.position);
                if (distance <= detectionRadius && CanDetectTarget(npc.transform))
                {
                    var npcHealth = npc.GetComponent<NPCHealth>();
                    if (npcHealth != null && !npcHealth.IsDead)
                    {
                        float priority = npcPriority / distance;
                        if (priority > bestPriority)
                        {
                            bestPriority = priority;
                            bestTarget = npc.transform;
                            bestTargetType = TargetType.NPC;
                            closestDistance = distance;
                        }
                    }
                }
            }
        }
        
        
        if (bestTarget == null)
        {
            bestTarget = DetectSounds(out bestTargetType);
        }
        
        
        if (bestTarget != null && bestTarget != currentTarget)
        {
            
            currentTarget = bestTarget;
            currentTargetType = bestTargetType;
            lastKnownPosition = currentTarget.position;
            lastDetectionTime = Time.time;
            isAlerted = true;
            
            
            animator.SetTrigger(detectionHash);
            PlayDetectionSound();
            
            
            agent.speed = chaseSpeed;
        }
        else if (bestTarget == null && currentTarget != null)
        {
            
            currentTarget = null;
            currentTargetType = TargetType.None;
        }
    }
    
    bool CanDetectTarget(Transform target)
    {
        if (detectionHelper != null)
        {
            return detectionHelper.CanDetectTarget(target, detectionRadius);
        }
        
        
        Vector3 direction = (target.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, target.position);
        
        if (Physics.Raycast(transform.position + Vector3.up, direction, distance, soundBlockerLayer))
        {
            return false; 
        }
        
        return true;
    }
    
    Transform DetectSounds(out TargetType detectedType)
    {
        detectedType = TargetType.None;
        Transform soundSource = null;
        float maxSoundStrength = 0f;
        
        
        GameObject player1 = GameObject.FindGameObjectWithTag("Player1");
        if (player1 != null)
        {
            var noiseEmitter = player1.GetComponent<PlayerNoiseEmitter>();
            if (noiseEmitter != null && noiseEmitter.currentNoiseRadius > 0.1f)
            {
                float distance = Vector3.Distance(transform.position, player1.transform.position);
                if (distance <= soundDetectionRadius)
                {
                    float soundStrength = CalculateSoundStrength(player1.transform, noiseEmitter.currentNoiseRadius, distance);
                    if (soundStrength > maxSoundStrength)
                    {
                        maxSoundStrength = soundStrength;
                        soundSource = player1.transform;
                        detectedType = TargetType.Player1;
                    }
                }
            }
        }
        
        
        GameObject player2 = GameObject.FindGameObjectWithTag("Player2");
        if (player2 != null)
        {
            var noiseEmitter = player2.GetComponent<PlayerNoiseEmitter>();
            if (noiseEmitter != null && noiseEmitter.currentNoiseRadius > 0.1f)
            {
                float distance = Vector3.Distance(transform.position, player2.transform.position);
                if (distance <= soundDetectionRadius)
                {
                    float soundStrength = CalculateSoundStrength(player2.transform, noiseEmitter.currentNoiseRadius, distance);
                    if (soundStrength > maxSoundStrength)
                    {
                        maxSoundStrength = soundStrength;
                        soundSource = player2.transform;
                        detectedType = TargetType.Player2;
                    }
                }
            }
        }
        
        
        GameObject npc = GameObject.FindGameObjectWithTag("NPC");
        if (npc != null)
        {
            var noiseEmitter = npc.GetComponent<NPCNoiseEmitter>();
            if (noiseEmitter != null && noiseEmitter.currentNoiseRadius > 0.1f)
            {
                float distance = Vector3.Distance(transform.position, npc.transform.position);
                if (distance <= soundDetectionRadius)
                {
                    float soundStrength = CalculateSoundStrength(npc.transform, noiseEmitter.currentNoiseRadius, distance);
                    if (soundStrength > maxSoundStrength)
                    {
                        maxSoundStrength = soundStrength;
                        soundSource = npc.transform;
                        detectedType = TargetType.NPC;
                    }
                }
            }
        }
        
        return soundSource;
    }
    
    float CalculateSoundStrength(Transform source, float noiseRadius, float distance)
    {
        if (distance > soundDetectionRadius) return 0f;
        
        
        Vector3 direction = (source.position - transform.position).normalized;
        if (Physics.Raycast(transform.position + Vector3.up, direction, distance, soundBlockerLayer))
        {
            return 0f; 
        }
        
        
        float effectiveRadius = Mathf.Max(noiseRadius, 2f);
        if (distance <= effectiveRadius)
        {
            return Mathf.Clamp01(1f - (distance / effectiveRadius));
        }
        
        return 0f;
    }
    
    void ChaseTarget()
    {
        if (currentTarget == null) return;
        
        
        lastKnownPosition = currentTarget.position;
        lastDetectionTime = Time.time;
        
        
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        
        if (distanceToTarget <= attackRange && canAttack)
        {
            StartCoroutine(AttackTarget());
        }
        else if (distanceToTarget > attackRange)
        {
            
            agent.SetDestination(currentTarget.position);
            
            
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }
    
    void SearchLastKnownPosition()
    {
        if (Vector3.Distance(transform.position, lastKnownPosition) > 2f)
        {
            agent.SetDestination(lastKnownPosition);
        }
        else
        {
            
            isAlerted = false;
            agent.speed = walkSpeed;
        }
    }
    
    void Patrol()
    {
        
        agent.speed = walkSpeed;
        
        
        if (agent.remainingDistance < 0.5f || !agent.hasPath)
        {
            
            agent.ResetPath();
        }
    }
}
    
    System.Collections.IEnumerator AttackTarget()
    {
        isAttacking = true;
        canAttack = false;
        
        
        agent.ResetPath();
        
        
        if (currentTarget != null)
        {
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = lookRotation;
            }
        }
        
        
        animator.SetBool(attackHash, true);
        
        
        PlayAttackSound();
        
        
        yield return new WaitForSeconds(attackDuration * 0.5f);
        
        
        if (currentTarget != null && Vector3.Distance(transform.position, currentTarget.position) <= attackRange)
        {
            DealDamageToTarget();
        }
        
        yield return new WaitForSeconds(attackDuration * 0.5f);
        
        
        animator.SetBool(attackHash, false);
        isAttacking = false;
        
        
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }
    
    void DealDamageToTarget()
    {
        if (currentTarget == null) return;
        
        
        switch (currentTargetType)
        {
            case TargetType.Player1:
            case TargetType.Player2:
                var playerHealth = currentTarget.GetComponent<PlayerHealth>();
                if (playerHealth != null && !playerHealth.IsDead)
                {
                    playerHealth.TakeDamage(attackDamage);
                }
                break;
                
            case TargetType.NPC:
                var npcHealth = currentTarget.GetComponent<NPCHealth>();
                if (npcHealth != null && !npcHealth.IsDead)
                {
                    npcHealth.TakeDamage(attackDamage);
                }
                break;
        }
    }
    
    void UpdateAnimation()
    {
        
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool(walkHash, isMoving && !isAttacking);
    }
    
    void ChaseTarget()
    {
        if (currentTarget == null) return;
        
        agent.speed = chaseSpeed;
        agent.SetDestination(currentTarget.position);
        
        
        Vector3 direction = (currentTarget.position - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }
    }
    
    void SearchLastKnownPosition()
    {
        if (Vector3.Distance(transform.position, lastKnownPosition) > 2f)
        {
            agent.SetDestination(lastKnownPosition);
        }
        else
        {
            
            isAlerted = false;
            agent.speed = walkSpeed;
        }
    }
    
    void SearchForTargets()
    {
        
        agent.speed = walkSpeed;
        
        
        
        
        if (Time.time % 2f < Time.deltaTime)
        {
            Vector3 randomDirection = Random.insideUnitSphere * 10f;
            randomDirection += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, 10f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }
    
    void PlayDetectionSound()
    {
        if (detectionSounds.Length > 0)
        {
            AudioClip clip = detectionSounds[Random.Range(0, detectionSounds.Length)];
            audioSource.PlayOneShot(clip);
        }
    }
    
    void PlayAttackSound()
    {
        if (attackSounds.Length > 0)
        {
            AudioClip clip = attackSounds[Random.Range(0, attackSounds.Length)];
            audioSource.PlayOneShot(clip);
        }
    }
    
    public void SetAlerted(bool alerted)
    {
        isAlerted = alerted;
        if (alerted)
        {
            agent.speed = chaseSpeed;
        }
        else
        {
            agent.speed = walkSpeed;
        }
    }
    
    public Transform GetCurrentTarget()
    {
        return currentTarget;
    }
    
    public bool IsAlerted()
    {
        return isAlerted;
    }
    
    public bool IsAttacking()
    {
        return isAttacking;
    }
    
    void OnDrawGizmosSelected()
    {
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, soundDetectionRadius);
        
        
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
        
        
        if (isAlerted && currentTarget == null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f); 
            Gizmos.DrawLine(transform.position, lastKnownPosition);
        }
    }
}