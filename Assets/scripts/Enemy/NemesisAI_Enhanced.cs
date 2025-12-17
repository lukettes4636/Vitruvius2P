using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(NemesisSoundDetector))]
public class NemesisAI_Enhanced : MonoBehaviour
{
    [Header("Nemesis Configuration - Enhanced AI")]
    [Tooltip("Walking speed for patrol/search mode")]
    public float walkSpeed = 3.5f;
    [Tooltip("Chase speed when pursuing targets")]
    public float chaseSpeed = 5.5f;
    [Tooltip("Maximum sprint speed when very close to target")]
    public float sprintSpeed = 7f;
    public float rotationSpeed = 10f;
    
    [Header("Detection Settings")]
    public float visualDetectionRadius = 25f;
    public float soundDetectionRadius = 35f;
    public float attackRange = 2.5f;
    public LayerMask detectionLayerMask;
    public LayerMask soundBlockerLayer;
    
    [Header("AI Behavior")]
    [Tooltip("How long to search after losing sight of target")]
    public float searchDuration = 8f;
    [Tooltip("How long to remember target position")]
    public float memoryDuration = 15f;
    [Tooltip("Delay between switching targets")]
    public float targetSwitchDelay = 2f;
    
    [Header("Patrol Settings")]
    public bool enableWander = true;
    public float wanderRadius = 10f;
    public float wanderInterval = 5f;
    
    [Header("Cinematic Settings")]
    public bool enableRoarOnDetection = true;
    public float roarDuration = 2f;
    public bool onlyWalkAnimation = true; 
    
    [Header("Attack Settings")]
    public float attackCooldown = 1.2f;
    public int attackDamage = 30;
    public float attackDuration = 0.9f;
    public float attackRangeMultiplier = 1.2f; 
    
    [Header("Targeting Priority")]
    public float npcPriority = 3f;
    public float playerPriority = 2f;
    public float soundPriority = 1f;
    
    [Header("Audio")]
    public AudioClip[] attackSounds;
    public AudioClip[] detectionSounds;
    public AudioClip[] footstepSounds;
    public AudioClip[] roarSounds;
    
    [Header("References")]
    public Animator animator;
    private NavMeshAgent agent;
    private AudioSource audioSource;
    private NemesisSoundDetector soundDetector;
    
    
    public enum NemesisState { Patrol, Alert, Chase, Search, Attack, Roar }
    private NemesisState currentState = NemesisState.Patrol;
    
    
    private Transform currentTarget;
    private Vector3 lastKnownPosition;
    private TargetInfo currentTargetInfo;
    private float lastTargetSwitchTime;
    private float lastDetectionTime;
    private float stateTimer;
    private float wanderTimer;
    private float roarTimer;
    
    
    private class TargetInfo
    {
        public Transform transform;
        public string tag;
        public float priority;
        public float distance;
        public float lastSeenTime;
        public Vector3 lastKnownPosition;
        public bool isAlive;
        
        public TargetInfo(Transform target, string targetTag, float targetPriority, float targetDistance)
        {
            transform = target;
            tag = targetTag;
            priority = targetPriority;
            distance = targetDistance;
            lastSeenTime = Time.time;
            lastKnownPosition = target.position;
            isAlive = true;
        }
        
        public void UpdateInfo(float newDistance)
        {
            distance = newDistance;
            lastSeenTime = Time.time;
            if (transform != null)
            {
                lastKnownPosition = transform.position;
            }
        }
    }
    
    
    private readonly int walkHash = Animator.StringToHash("Walk");
    private readonly int runHash = Animator.StringToHash("Run");
    private readonly int attackHash = Animator.StringToHash("Attack");
    private readonly int detectionHash = Animator.StringToHash("Detected");
    private readonly int searchHash = Animator.StringToHash("Search");
    
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        soundDetector = GetComponent<NemesisSoundDetector>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        ConfigureNavMeshAgent();
    }
    
    void Start()
    {
        currentState = NemesisState.Patrol;
        currentTarget = null;
        currentTargetInfo = null;
        stateTimer = 0f;
        
        
        animator.SetBool(walkHash, false);
        animator.SetBool(runHash, false);
        animator.SetBool(attackHash, false);
    }
    
    void Update()
    {
        UpdateAIState();
        ExecuteCurrentState();
        UpdateAnimation();
    }
    
    void ConfigureNavMeshAgent()
    {
        agent.speed = walkSpeed;
        agent.angularSpeed = 360f;
        agent.acceleration = 15f;
        agent.stoppingDistance = attackRange - 0.2f;
        agent.autoBraking = true;
        agent.updateRotation = true;
    }
    
    void UpdateAIState()
    {
        stateTimer += Time.deltaTime;
        
        
        if (currentState == NemesisState.Attack || currentState == NemesisState.Roar) return;
        
        ScanForTargets();
        
        
        if (currentTargetInfo != null && currentTargetInfo.isAlive)
        {
            if (CanSeeTarget(currentTargetInfo.transform))
            {
                
                if (currentState != NemesisState.Chase)
                {
                    if (enableRoarOnDetection && currentState != NemesisState.Alert)
                    {
                        StartRoarSequence();
                    }
                    else
                    {
                        currentState = NemesisState.Chase;
                    }
                }
                lastDetectionTime = Time.time;
            }
            else if (Time.time - lastDetectionTime < memoryDuration)
            {
                currentState = NemesisState.Search;
            }
            else
            {
                currentState = NemesisState.Patrol;
                currentTargetInfo = null;
            }
        }
        else if (Time.time - lastDetectionTime < searchDuration)
        {
            currentState = NemesisState.Search;
        }
        else
        {
            currentState = NemesisState.Patrol;
        }
    }
    
    void StartRoarSequence()
    {
        currentState = NemesisState.Roar;
        roarTimer = 0f;
        PlayRoarSound();
        animator.SetTrigger(detectionHash);
    }
    
    void ScanForTargets()
    {
        TargetInfo bestTarget = null;
        float bestScore = 0f;
        
        
        TargetInfo visualTarget = ScanVisualTargets();
        if (visualTarget != null)
        {
            bestScore = CalculateTargetScore(visualTarget);
            bestTarget = visualTarget;
        }
        
        
        TargetInfo soundTarget = ScanSoundTargets();
        if (soundTarget != null)
        {
            float soundScore = CalculateTargetScore(soundTarget) * 0.7f; 
            if (soundScore > bestScore)
            {
                bestScore = soundScore;
                bestTarget = soundTarget;
            }
        }
        
        
        if (bestTarget != null)
        {
            if (currentTargetInfo == null || bestTarget.transform != currentTargetInfo.transform)
            {
                
                if (Time.time - lastTargetSwitchTime > targetSwitchDelay)
                {
                    SwitchTarget(bestTarget);
                }
            }
            else
            {
                
                currentTargetInfo.UpdateInfo(bestTarget.distance);
            }
        }
        else if (currentTargetInfo != null && Time.time - lastDetectionTime > memoryDuration)
        {
            
            currentTargetInfo = null;
        }
    }
    
    TargetInfo ScanVisualTargets()
    {
        TargetInfo bestVisualTarget = null;
        float bestPriority = 0f;
        
        
        GameObject player1 = GameObject.FindGameObjectWithTag("Player1");
        if (player1 != null)
        {
            var target = EvaluateTarget(player1.transform, "Player1", playerPriority);
            if (target != null && target.priority > bestPriority)
            {
                bestPriority = target.priority;
                bestVisualTarget = target;
            }
        }
        
        
        GameObject player2 = GameObject.FindGameObjectWithTag("Player2");
        if (player2 != null)
        {
            var target = EvaluateTarget(player2.transform, "Player2", playerPriority);
            if (target != null && target.priority > bestPriority)
            {
                bestPriority = target.priority;
                bestVisualTarget = target;
            }
        }
        
        
        GameObject npc = GameObject.FindGameObjectWithTag("NPC");
        if (npc != null)
        {
            var target = EvaluateTarget(npc.transform, "NPC", npcPriority);
            if (target != null && target.priority > bestPriority)
            {
                bestPriority = target.priority;
                bestVisualTarget = target;
            }
        }
        
        return bestVisualTarget;
    }
    
    TargetInfo EvaluateTarget(Transform target, string targetTag, float basePriority)
    {
        if (target == null) return null;
        
        
        bool isAlive = IsTargetAlive(target, targetTag);
        if (!isAlive) return null;
        
        float distance = Vector3.Distance(transform.position, target.position);
        
        
        if (distance > visualDetectionRadius) return null;
        
        
        if (!HasLineOfSight(target)) return null;
        
        
        float priority = basePriority / (distance / visualDetectionRadius + 0.1f);
        
        return new TargetInfo(target, targetTag, priority, distance);
    }
    
    TargetInfo ScanSoundTargets()
    {
        if (!soundDetector.HasDetectedSounds()) return null;
        
        Transform soundSource;
        float intensity;
        Vector3 soundPosition;
        
        soundSource = soundDetector.GetLoudestSoundSource(out intensity, out soundPosition);
        
        if (soundSource != null && intensity > 0.3f)
        {
            float distance = Vector3.Distance(transform.position, soundPosition);
            if (distance <= soundDetectionRadius)
            {
                string soundTag = soundSource.tag;
                float priority = soundPriority * intensity;
                
                return new TargetInfo(soundSource, soundTag, priority, distance);
            }
        }
        
        return null;
    }
    
    bool IsTargetAlive(Transform target, string targetTag)
    {
        switch (targetTag)
        {
            case "Player1":
            case "Player2":
                var playerHealth = target.GetComponent<PlayerHealth>();
                return playerHealth != null && !playerHealth.IsDead;
                
            case "NPC":
                var npcHealth = target.GetComponent<NPCHealth>();
                return npcHealth != null && !npcHealth.IsDead;
                
            default:
                return true;
        }
    }
    
    bool HasLineOfSight(Transform target)
    {
        Vector3 direction = (target.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, target.position);
        
        
        if (Physics.Raycast(transform.position + Vector3.up, direction, distance, soundBlockerLayer))
        {
            return false; 
        }
        
        return true;
    }
    
    bool CanSeeTarget(Transform target)
    {
        if (target == null) return false;
        
        float distance = Vector3.Distance(transform.position, target.position);
        return distance <= visualDetectionRadius && HasLineOfSight(target);
    }
    
    float CalculateTargetScore(TargetInfo target)
    {
        if (target == null) return 0f;
        
        float distanceScore = 1f - (target.distance / visualDetectionRadius);
        float priorityScore = target.priority;
        
        return distanceScore * priorityScore;
    }
    
    void SwitchTarget(TargetInfo newTarget)
    {
        currentTargetInfo = newTarget;
        currentTarget = newTarget.transform;
        lastTargetSwitchTime = Time.time;
        lastDetectionTime = Time.time;
        
        if (enableRoarOnDetection)
        {
            StartRoarSequence();
        }
        else
        {
            animator.SetTrigger(detectionHash);
            PlayDetectionSound();
        }
    }
    
    void ExecuteCurrentState()
    {
        switch (currentState)
        {
            case NemesisState.Patrol:
                ExecutePatrol();
                break;
                
            case NemesisState.Alert:
                ExecuteAlert();
                break;
                
            case NemesisState.Chase:
                ExecuteChase();
                break;
                
            case NemesisState.Search:
                ExecuteSearch();
                break;
                
            case NemesisState.Attack:
                ExecuteAttack();
                break;
                
            case NemesisState.Roar:
                ExecuteRoar();
                break;
        }
    }
    
    void ExecutePatrol()
    {
        agent.speed = walkSpeed;
        
        if (!enableWander)
        {
            if (agent.remainingDistance < 0.5f || !agent.hasPath)
            {
                agent.ResetPath();
            }
            return;
        }
        
        wanderTimer += Time.deltaTime;
        
        if (wanderTimer > wanderInterval || agent.remainingDistance < 0.5f)
        {
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, 1))
            {
                agent.SetDestination(hit.position);
                wanderTimer = 0f;
            }
        }
    }
    
    void ExecuteRoar()
    {
        agent.ResetPath();
        roarTimer += Time.deltaTime;
        
        if (currentTarget != null)
        {
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 2f);
            }
        }
        
        if (roarTimer >= roarDuration)
        {
            currentState = NemesisState.Chase;
        }
    }
    
    void ExecuteAlert()
    {
        agent.speed = walkSpeed;
        
        
        if (currentTargetInfo != null)
        {
            Vector3 direction = (currentTargetInfo.lastKnownPosition - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }
    
    void ExecuteChase()
    {
        if (currentTargetInfo == null || currentTarget == null)
        {
            currentState = NemesisState.Search;
            return;
        }
        
        
        currentTargetInfo.UpdateInfo(Vector3.Distance(transform.position, currentTarget.position));
        
        
        float distanceToTarget = currentTargetInfo.distance;
        if (distanceToTarget < 5f)
        {
            agent.speed = sprintSpeed; 
        }
        else
        {
            agent.speed = chaseSpeed; 
        }
        
        
        agent.SetDestination(currentTarget.position);
        
        
        if (distanceToTarget <= attackRange)
        {
            currentState = NemesisState.Attack;
            StartCoroutine(AttackSequence());
        }
    }
    
    void ExecuteSearch()
    {
        agent.speed = walkSpeed;
        
        if (currentTargetInfo != null)
        {
            
            if (Vector3.Distance(transform.position, currentTargetInfo.lastKnownPosition) > 2f)
            {
                agent.SetDestination(currentTargetInfo.lastKnownPosition);
            }
            else
            {
                
                agent.ResetPath();
                animator.SetTrigger(searchHash);
                
                
                transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
            }
        }
    }
    
    void ExecuteAttack()
    {
        
    }
    
    System.Collections.IEnumerator AttackSequence()
    {
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
        
        
        yield return new WaitForSeconds(attackDuration * 0.4f);
        
        
        if (currentTarget != null && Vector3.Distance(transform.position, currentTarget.position) <= attackRange * attackRangeMultiplier)
        {
            DealDamageToTarget();
        }
        
        
        yield return new WaitForSeconds(attackDuration * 0.6f);
        
        
        animator.SetBool(attackHash, false);
        
        
        yield return new WaitForSeconds(attackCooldown);
        
        
        currentState = NemesisState.Chase;
    }
    
    void DealDamageToTarget()
    {
        if (currentTarget == null || currentTargetInfo == null) return;
        
        switch (currentTargetInfo.tag)
        {
            case "Player1":
            case "Player2":
                var playerHealth = currentTarget.GetComponent<PlayerHealth>();
                if (playerHealth != null && !playerHealth.IsDead)
                {
                    playerHealth.TakeDamage(attackDamage);
                }
                break;
                
            case "NPC":
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
        bool isRunning = agent.speed >= chaseSpeed && !onlyWalkAnimation;
        bool isChasing = currentState == NemesisState.Chase;
        
        animator.SetBool(walkHash, isMoving && !isRunning && currentState != NemesisState.Attack && currentState != NemesisState.Roar);
        animator.SetBool(runHash, isRunning && isMoving && currentState != NemesisState.Attack && currentState != NemesisState.Roar);
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
    
    void PlayRoarSound()
    {
        if (roarSounds.Length > 0)
        {
            AudioClip clip = roarSounds[Random.Range(0, roarSounds.Length)];
            audioSource.PlayOneShot(clip);
        }
    }
    
    
    public NemesisState GetCurrentState()
    {
        return currentState;
    }
    
    public void SetAlerted(bool alerted)
    {
        if (alerted && currentState == NemesisState.Patrol)
        {
            currentState = NemesisState.Alert;
        }
        else if (!alerted && currentState == NemesisState.Alert)
        {
            currentState = NemesisState.Patrol;
        }
    }
    
    public Transform GetCurrentTarget()
    {
        return currentTarget;
    }
    
    public bool IsAlerted()
    {
        return currentState != NemesisState.Patrol;
    }
    
    public void ForceAlert(Vector3 alertPosition)
    {
        currentState = NemesisState.Alert;
        lastKnownPosition = alertPosition;
        lastDetectionTime = Time.time;
        agent.speed = chaseSpeed;
    }
    
    void OnDrawGizmosSelected()
    {
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, visualDetectionRadius);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, soundDetectionRadius);
        
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
            Gizmos.DrawWireSphere(currentTarget.position, 1f);
        }
        
        
        if (currentTarget == null && lastKnownPosition != Vector3.zero)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f); 
            Gizmos.DrawLine(transform.position, lastKnownPosition);
            Gizmos.DrawWireSphere(lastKnownPosition, 1f);
        }
        
        
        Gizmos.color = Color.white;
        Vector3 textPosition = transform.position + Vector3.up * 3f;
        UnityEngine.Debug.DrawLine(transform.position + Vector3.up * 2.5f, textPosition, Color.white);
    }
}