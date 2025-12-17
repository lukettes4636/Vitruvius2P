using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class NemesisAI_Enhanced : MonoBehaviour
{
    [Header("Nemesis Configuration - Simplified")]
    [Tooltip("Standard walking speed")]
    public float walkSpeed = 3.5f;
    [Tooltip("Chase speed (fast walk)")]
    public float chaseSpeed = 4.5f;
    public float rotationSpeed = 10f;
    
    [Header("Detection Settings")]
    public float detectionRadius = 40f; 
    public float attackRange = 2.5f;
    public LayerMask obstacleLayerMask; 
    
    [Header("AI Behavior")]
    public float memoryDuration = 10f;
    public float targetSwitchDelay = 2f;
    
    [Header("Attack Settings")]
    public float attackCooldown = 1.2f;
    public int attackDamage = 30;
    public float attackDuration = 0.9f;
    public float attackRangeMultiplier = 1.2f;
    
    [Header("Targeting Priority")]
    public float npcPriority = 3f;
    public float playerPriority = 2f;
    
    [Header("Audio")]
    public AudioClip[] attackSounds;
    public AudioClip[] detectionSounds;
    public AudioClip[] footstepSounds;
    
    [Header("References")]
    public Animator animator;
    private NavMeshAgent agent;
    private AudioSource audioSource;
    
    public enum NemesisState { Idle, Chase, Attack, Search }
    private NemesisState currentState = NemesisState.Idle;
    
    private Transform currentTarget;
    private TargetInfo currentTargetInfo;
    private Vector3 lastKnownPosition;
    private float lastDetectionTime;
    private float lastTargetSwitchTime;
    
    
    private List<Transform> potentialTargets = new List<Transform>();
    private float targetScanTimer;
    
    private readonly int walkHash = Animator.StringToHash("Walk");
    private readonly int attackHash = Animator.StringToHash("Attack");
    private readonly int detectionHash = Animator.StringToHash("Detected");
    
    private class TargetInfo
    {
        public Transform transform;
        public string tag;
        public float priority;
        public float distance;
        
        public TargetInfo(Transform t, string tag, float p, float d)
        {
            transform = t;
            this.tag = tag;
            priority = p;
            distance = d;
        }
    }
    
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        ConfigureNavMeshAgent();
        
        
        if (obstacleLayerMask == 0)
        {
            obstacleLayerMask = LayerMask.GetMask("Default", "Walls", "Obstacles");
        }
    }
    
    void Start()
    {
        currentState = NemesisState.Idle;
        FindTargets();
        
        
        animator.SetBool("Run", false);
    }
    
    void Update()
    {
        
        if (Time.time > targetScanTimer + 3f)
        {
            FindTargets();
            targetScanTimer = Time.time;
        }
        
        UpdateAIState();
        ExecuteCurrentState();
        UpdateAnimation();
    }
    
    void ConfigureNavMeshAgent()
    {
        agent.speed = walkSpeed;
        agent.angularSpeed = 360f;
        agent.acceleration = 15f;
        agent.stoppingDistance = attackRange - 0.5f;
        agent.autoBraking = true;
        agent.updateRotation = true;
    }
    
    void FindTargets()
    {
        potentialTargets.Clear();
        
        GameObject p1 = GameObject.FindGameObjectWithTag("Player1");
        if (p1) potentialTargets.Add(p1.transform);
        
        GameObject p2 = GameObject.FindGameObjectWithTag("Player2");
        if (p2) potentialTargets.Add(p2.transform);
        
        GameObject npc = GameObject.FindGameObjectWithTag("NPC");
        if (npc) potentialTargets.Add(npc.transform);
    }
    
    void UpdateAIState()
    {
        if (currentState == NemesisState.Attack) return;
        
        TargetInfo bestTarget = ScanForTargets();
        
        if (bestTarget != null)
        {
            
            if (currentState != NemesisState.Chase)
            {
                
                PlayDetectionSound();
                animator.SetTrigger(detectionHash);
                currentState = NemesisState.Chase;
            }
            
            
            if (currentTarget != bestTarget.transform)
            {
                if (currentTarget == null || Time.time - lastTargetSwitchTime > targetSwitchDelay)
                {
                    currentTarget = bestTarget.transform;
                    currentTargetInfo = bestTarget;
                    lastTargetSwitchTime = Time.time;
                }
            }
            else
            {
                
                currentTargetInfo = bestTarget;
            }
            
            lastDetectionTime = Time.time;
            lastKnownPosition = currentTarget.position;
        }
        else
        {
            
            if (currentState == NemesisState.Chase)
            {
                if (Time.time - lastDetectionTime < memoryDuration)
                {
                    currentState = NemesisState.Search;
                }
                else
                {
                    currentState = NemesisState.Idle;
                }
            }
            else if (currentState == NemesisState.Search)
            {
                if (Vector3.Distance(transform.position, lastKnownPosition) < 1f || Time.time - lastDetectionTime > memoryDuration)
                {
                    currentState = NemesisState.Idle;
                }
            }
        }
    }
    
    TargetInfo ScanForTargets()
    {
        TargetInfo best = null;
        float maxScore = -1f;
        
        foreach (var t in potentialTargets)
        {
            if (t == null) continue;
            
            
            if (!IsTargetAlive(t)) continue;
            
            float dist = Vector3.Distance(transform.position, t.position);
            if (dist > detectionRadius) continue;
            
            
            if (!HasLineOfSight(t)) continue;
            
            
            float priority = 1f;
            if (t.CompareTag("NPC")) priority = npcPriority;
            else if (t.CompareTag("Player1") || t.CompareTag("Player2")) priority = playerPriority;
            
            
            float score = priority * (1f - (dist / detectionRadius));
            
            if (score > maxScore)
            {
                maxScore = score;
                best = new TargetInfo(t, t.tag, priority, dist);
            }
        }
        return best;
    }
    
    bool IsTargetAlive(Transform t)
    {
        if (t.CompareTag("Player1") || t.CompareTag("Player2"))
        {
            var health = t.GetComponent<PlayerHealth>();
            return health != null && !health.IsDead;
        }
        else if (t.CompareTag("NPC"))
        {
            var health = t.GetComponent<NPCHealth>();
            return health != null && !health.IsDead;
        }
        return true;
    }
    
    bool HasLineOfSight(Transform t)
    {
        Vector3 start = transform.position + Vector3.up * 1.5f; 
        Vector3 end = t.position + Vector3.up * 1.0f; 
        Vector3 dir = (end - start).normalized;
        float dist = Vector3.Distance(start, end);
        
        
        if (Physics.Raycast(start, dir, out RaycastHit hit, dist, obstacleLayerMask))
        {
            
            if (hit.transform != t && !hit.transform.IsChildOf(t))
            {
                return false;
            }
        }
        return true;
    }
    
    void ExecuteCurrentState()
    {
        switch (currentState)
        {
            case NemesisState.Idle:
                agent.ResetPath();
                
                break;
                
            case NemesisState.Chase:
                if (currentTarget != null)
                {
                    agent.speed = chaseSpeed;
                    agent.SetDestination(currentTarget.position);
                    
                    float dist = Vector3.Distance(transform.position, currentTarget.position);
                    if (dist <= attackRange)
                    {
                        currentState = NemesisState.Attack;
                        StartCoroutine(AttackSequence());
                    }
                }
                else
                {
                    currentState = NemesisState.Search;
                }
                break;
                
            case NemesisState.Search:
                agent.speed = walkSpeed;
                agent.SetDestination(lastKnownPosition);
                break;
                
            case NemesisState.Attack:
                
                break;
        }
    }
    
    System.Collections.IEnumerator AttackSequence()
    {
        agent.ResetPath();
        
        
        if (currentTarget != null)
        {
            Vector3 dir = (currentTarget.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
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
        if (currentTarget == null) return;
        
        if (currentTarget.CompareTag("Player1") || currentTarget.CompareTag("Player2"))
        {
            var health = currentTarget.GetComponent<PlayerHealth>();
            if (health) health.TakeDamage(attackDamage);
        }
        else if (currentTarget.CompareTag("NPC"))
        {
            var health = currentTarget.GetComponent<NPCHealth>();
            if (health) health.TakeDamage(attackDamage);
        }
    }
    
    void UpdateAnimation()
    {
        bool moving = agent.velocity.magnitude > 0.1f;
        
        animator.SetBool(walkHash, moving);
    }
    
    void PlayDetectionSound()
    {
        if (detectionSounds != null && detectionSounds.Length > 0)
        {
            audioSource.PlayOneShot(detectionSounds[Random.Range(0, detectionSounds.Length)]);
        }
    }
    
    void PlayAttackSound()
    {
        if (attackSounds != null && attackSounds.Length > 0)
        {
            audioSource.PlayOneShot(attackSounds[Random.Range(0, attackSounds.Length)]);
        }
    }
    
    
    public NemesisState GetCurrentState() => currentState;
    public Transform GetCurrentTarget() => currentTarget;
    public void ForceAlert(Vector3 pos)
    {
        currentState = NemesisState.Search;
        lastKnownPosition = pos;
        lastDetectionTime = Time.time;
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        if (currentTarget != null)
        {
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}
