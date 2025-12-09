using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyMonsterAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator anim;
    private AudioSource audioSource;

    private Coroutine footstepCoroutine = null;
    private AudioClip currentFootstepClip = null;
    private float currentFootstepInterval = 0f;

    [Header("=== ESTADO INICIAL ===")]
    [Tooltip("Estado en el que comienza el enemigo en este nivel")]
    public InitialState initialState = InitialState.Sleeping;

    public enum InitialState
    {
        Sleeping,
        Eating,
        Patrol
    }

    [Header("Deteccion de jugadores")]
    public Transform[] playerTargets;
    private Transform currentPlayer;

    
    private Vector3 targetPositionOfInterest;
    private bool hasTargetOfInterest = false;

    [Tooltip("Tiempo en segundos que recuerda la posicion antes de perderla")]
    public float memoryDuration = 3.0f;
    private float timeSinceLastHeard = 0f;

    [Header("=== SISTEMA DE AUDIO PROFESIONAL ===")]
    [Tooltip("Multiplicador de sensibilidad auditiva (1.0 = normal, 2.0 = doble alcance)")]
    [Range(0.5f, 3.0f)]
    public float audioSensitivity = 1.0f;

    [Tooltip("Distancia maxima a la que puede escuchar cualquier sonido")]
    public float maxHearingDistance = 20f;

    [Tooltip("Radio minimo para detectar incluso el sonido mas bajo")]
    public float minDetectionRadius = 1.5f;

    [Tooltip("Umbral minimo de fuerza de audio para activar deteccin (0.0-1.0)")]
    [Range(0.0f, 1.0f)]
    public float detectionThreshold = 0.2f;

    [Tooltip("Capa de obstaculos que bloquean el sonido")]
    public LayerMask soundBlockerLayer;

    [Tooltip("Reduccion de sonido por cada obstaculo (0.5 = 50% menos alcance)")]
    [Range(0.1f, 0.9f)]
    public float soundAttenuationPerWall = 0.7f;

    [Header("Estados de Alerta")]
    [Tooltip("Tiempo que permanece en estado de alerta despues de perder al jugador")]
    public float alertDuration = 5f;

    [Tooltip("Tiempo investigando la ultima posicion conocida")]
    public float investigationDuration = 8f;

    private float alertTimer = 0f;
    private AudioAlertLevel currentAlertLevel = AudioAlertLevel.Calm;

    private enum AudioAlertLevel
    {
        Calm,
        Suspicious,
        Alert,
        Hunting
    }

    [Header("Parametros de movimiento")]
    public float attackRange = 2.2f;
    public float crawlSpeed = 1.2f;
    public float walkSpeed = 2.5f;
    public float investigationSpeed = 1.8f;
    public float rotationSpeed = 8f;
    public float attackStopDistanceOffset = 0.5f;
    public float patrolStopDistance = 0.1f;

    [Header("Patrulla")]
    public Transform[] patrolPoints;
    public float patrolWaitTime = 2f;
    private int patrolIndex = 0;
    private bool isWaiting = false;

    [Header("Animaciones y Suavizado")]
    public float crawlToStateDuration = 1.1f;
    public float stateToCrawlDuration = 1.0f;
    public float roarDuration = 1.2f;
    public float attackDuration = 1f;
    public float attackCooldown = 0.7f;
    public string sleepAnimBool = "isSleeping";
    public string eatAnimBool = "isEating";

    
    [Tooltip("Tiempo de espera antes de pasar a Idle al detenerse (evita temblores)")]
    public float stopAnimDelay = 0.2f;
    private float stopAnimTimer = 0f;

    [Header("Audio")]
    public AudioClip roarClip;
    public AudioClip attackClip;
    public AudioClip secondaryAttackClip;
    public AudioClip crawlFootstepClip;
    public AudioClip walkFootstepClip;
    public AudioClip eatingSound;
    public float pitchVariance = 0.1f;

    [Header("Pisadas Programaticas")]
    public float crawlFootstepInterval = 0.5f;
    public float walkFootstepInterval = 0.35f;

    [Header("Damage Settings")]
    public GameObject rightHandCollider;
    public GameObject leftHandCollider;

    [Header("Destruccion de Pared")]
    public LayerMask destructibleWallLayer;
    public float wallDetectionDistance = 3.0f;
    [Tooltip("Grosor del rayo para detectar paredes (SphereCast)")]
    public float wallDetectionRadius = 0.5f;
    public AudioClip wallBreakSound;
    private GameObject currentWallTarget = null;

    [Header("Optimizacion de Transiciones")]
    public float transitionDelay = 0.15f;
    [Tooltip("Velocidad minima para considerar que esta en movimiento")]
    public float movementThreshold = 0.05f;

    [Header("Camera System")]
    public EnemyCameraController enemyCameraController;

    [Header("Shader FX - Roar")]
    [Tooltip("Material con shader de rugido")]
    public Material roarMaterial;
    [Range(0, 0.1f)]
    public float maxRoarDistortion = 0.03f;

    [Header("=== DEBUG AUDIO ===")]
    public bool showAudioDebug = false;
    public Color audioRangeColor = Color.yellow;
    public Color detectionColor = Color.red;

    [Header("=== DEBUG AVANZADO ===")]
    [Tooltip("Mostrar logs detallados de deteccin en consola")]
    public bool showDebugLogs = false;

    private int _roarIntensityID;
    private int _isActiveID;
    private Coroutine roarVisualCoroutine = null;

    private bool isAttacking = false;
    private bool isRising = false;
    private bool isRoaring = false;
    private bool returningToCrawl = false;
    private bool hasAwakened = false;
    private bool isTransitioning = false;
    private bool hasShownFirstDetection = false;
    public float reDetectionCooldown = 2f;
    private float lastReDetectionTime = -999f;

    private enum State { Sleeping, Eating, Patrol, Investigating, Chasing, Attacking, Rising, Roaring, Dead, ReturningToCrawl }
    private State currentState = State.Sleeping;
    private State previousState = State.Sleeping;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.playOnAwake = false;
        }

        if (agent != null)
        {
            agent.isStopped = true;
            
            
            agent.updateRotation = false;
            agent.stoppingDistance = patrolStopDistance;
            agent.speed = crawlSpeed;
            agent.acceleration = 12f; 
            agent.angularSpeed = 120f;
        }

        SetupInitialState();
        DisableAllHitboxes();

        if (enemyCameraController == null)
            enemyCameraController = FindObjectOfType<EnemyCameraController>();

        if (roarMaterial != null)
        {
            _roarIntensityID = Shader.PropertyToID("_RoarIntensity");
            _isActiveID = Shader.PropertyToID("_IsActive");
            roarMaterial.SetFloat(_isActiveID, 0f);
            roarMaterial.SetFloat(_roarIntensityID, 0f);
        }
    }

    void SetupInitialState()
    {
        anim.SetBool(sleepAnimBool, false);
        anim.SetBool(eatAnimBool, false);
        anim.SetBool("isCrawling", false);
        anim.SetBool("isWalking", false);
        anim.SetBool("isAttacking", false);

        switch (initialState)
        {
            case InitialState.Sleeping:
                currentState = State.Sleeping;
                anim.SetBool(sleepAnimBool, true);
                break;

            case InitialState.Eating:
                currentState = State.Eating;
                anim.SetBool(eatAnimBool, true);
                if (eatingSound != null && audioSource != null)
                {
                    audioSource.clip = eatingSound;
                    audioSource.loop = true;
                    audioSource.Play();
                }
                break;

            case InitialState.Patrol:
                currentState = State.Patrol;
                hasAwakened = true;
                agent.updateRotation = true; 
                if (patrolPoints.Length > 0)
                {
                    SetNextPatrol();
                }
                break;
        }
    }

    void Update()
    {
        if (currentState == State.Dead) return;

        
        ProcessAudioDetection();

        if (currentState == State.Sleeping || currentState == State.Eating)
        {
            HandlePassiveState();
            return;
        }

        if (currentState == State.Rising ||
            currentState == State.Roaring ||
            currentState == State.ReturningToCrawl ||
            isTransitioning)
        {
            return;
        }

        
        if (currentState == State.Attacking)
        {
            if (hasTargetOfInterest)
                RotateToTarget(targetPositionOfInterest);
            else if (currentWallTarget != null)
                RotateToTarget(currentWallTarget.transform.position);
            return;
        }

        
        if (currentAlertLevel != AudioAlertLevel.Calm && currentAlertLevel != AudioAlertLevel.Hunting)
        {
            alertTimer -= Time.deltaTime;
            if (alertTimer <= 0f)
            {
                currentAlertLevel = AudioAlertLevel.Calm;
            }
        }

        
        switch (currentState)
        {
            case State.Chasing:
                HandleChasingState();
                break;

            case State.Patrol:
                Patrol();
                if (hasTargetOfInterest)
                {
                    StartCoroutine(RiseAndRoar());
                }
                break;

            case State.Investigating:
                
                if (hasTargetOfInterest && currentAlertLevel >= AudioAlertLevel.Alert)
                {
                    StopAllCoroutines();
                    isTransitioning = false;
                    ChangeState(State.Chasing);
                }
                break;
        }
    }

    
    
    
    void ProcessAudioDetection()
    {
        Transform loudestPlayer = null;
        float maxAudioStrength = 0f;

        
        foreach (Transform player in playerTargets)
        {
            if (player == null) continue;

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead) continue;

            float dist = Vector3.Distance(transform.position, player.position);
            PlayerNoiseEmitter noiseEmitter = player.GetComponent<PlayerNoiseEmitter>();

            if (noiseEmitter == null || noiseEmitter.currentNoiseRadius < 0.1f) continue;

            float strength = CalculateAudioDetection(player, noiseEmitter, dist);

            if (strength > maxAudioStrength)
            {
                maxAudioStrength = strength;
                loudestPlayer = player;
            }
        }

        
        if (loudestPlayer != null && maxAudioStrength > 0f)
        {
            
            currentPlayer = loudestPlayer;
            targetPositionOfInterest = currentPlayer.position; 
            hasTargetOfInterest = true;
            timeSinceLastHeard = 0f; 

            
            if (maxAudioStrength > 0.8f) currentAlertLevel = AudioAlertLevel.Hunting;
            else if (maxAudioStrength > 0.5f) currentAlertLevel = AudioAlertLevel.Alert;
            else if (maxAudioStrength > 0.2f) currentAlertLevel = AudioAlertLevel.Suspicious;

            if (currentState == State.Chasing || currentState == State.Patrol)
            {
                alertTimer = alertDuration;
            }

            
            if (!hasShownFirstDetection)
            {
                hasShownFirstDetection = true;
                lastReDetectionTime = Time.time;
            }
            else if (Time.time - lastReDetectionTime > reDetectionCooldown && currentState == State.Patrol)
            {
                DialogueManager.ShowEnemyDetectedAgainDialogue(currentPlayer.gameObject);
                lastReDetectionTime = Time.time;
            }
        }
        else
        {
            
            timeSinceLastHeard += Time.deltaTime;

            if (timeSinceLastHeard > memoryDuration)
            {
                
                bool wasChasing = (currentState == State.Chasing);

                hasTargetOfInterest = false;
                currentPlayer = null;

                
                if (wasChasing && !isAttacking && !returningToCrawl && !isTransitioning)
                {
                    DialogueManager.ShowEnemyChaseEndedDialogue();
                    StartCoroutine(InvestigatePosition(targetPositionOfInterest));
                }
            }
        }
    }

    float CalculateAudioDetection(Transform player, PlayerNoiseEmitter noiseEmitter, float distance)
    {
        if (distance > maxHearingDistance) return 0f;

        float playerNoiseRadius = noiseEmitter.currentNoiseRadius * audioSensitivity;
        int wallCount = CountSoundBlockers(player);

        float attenuatedRadius = playerNoiseRadius * Mathf.Pow(soundAttenuationPerWall, wallCount);
        float effectiveRadius = Mathf.Max(attenuatedRadius, minDetectionRadius);

        if (distance <= effectiveRadius)
        {
            float strength = 1f - (distance / effectiveRadius);
            return Mathf.Clamp01(strength);
        }
        return 0f;
    }

    int CountSoundBlockers(Transform target)
    {
        Vector3 start = transform.position + Vector3.up * 1.0f;
        Vector3 end = target.position + Vector3.up * 1.0f;
        Vector3 direction = (end - start).normalized;
        float distance = Vector3.Distance(start, end);

        RaycastHit[] hits = Physics.RaycastAll(start, direction, distance, soundBlockerLayer);
        return hits.Length;
    }

    
    
    
    void HandleChasingState()
    {
        
        if (currentPlayer != null)
        {
            PlayerHealth health = currentPlayer.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead)
            {
                hasTargetOfInterest = false;
                currentPlayer = null;
                StartCoroutine(ReturnToCrawl());
                return;
            }
        }

        
        
        if (CheckForWallInFront())
        {
            StartCoroutine(AttackWallCycle());
            return;
        }

        
        if (hasTargetOfInterest && IsWallBetweenPositions(transform.position, targetPositionOfInterest))
        {
            StartCoroutine(AttackWallCycle());
            return;
        }

        
        agent.stoppingDistance = attackRange - attackStopDistanceOffset;
        agent.speed = walkSpeed;

        if (hasTargetOfInterest)
        {
            agent.SetDestination(targetPositionOfInterest);
        }

        
        if (agent.hasPath || agent.velocity.magnitude > 0.1f)
        {
            RotateToTarget(agent.steeringTarget);
        }

        
        float actualSpeed = agent.velocity.magnitude;
        
        bool isMovingEngine = actualSpeed > movementThreshold || (agent.desiredVelocity.magnitude > 0.5f && !agent.isStopped);

        if (isMovingEngine)
        {
            stopAnimTimer = stopAnimDelay; 
            anim.SetBool("isWalking", true);
        }
        else
        {
            stopAnimTimer -= Time.deltaTime;
            if (stopAnimTimer <= 0f)
            {
                anim.SetBool("isWalking", false);
            }
        }
        anim.SetBool("isCrawling", false);

        if (isMovingEngine) UpdateFootsteps(walkFootstepClip, walkFootstepInterval);

        
        if (hasTargetOfInterest)
        {
            float distToTarget = Vector3.Distance(transform.position, targetPositionOfInterest);
            
            if (distToTarget <= attackRange && !CheckForWallInFront())
            {
                StartCoroutine(AttackCycle());
            }
        }
    }

    
    bool CheckForWallInFront()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 direction = transform.forward;
        float checkDistance = 1.5f;

        if (Physics.SphereCast(origin, 0.4f, direction, out RaycastHit hit, checkDistance, destructibleWallLayer))
        {
            if (hit.collider.gameObject != gameObject)
            {
                currentWallTarget = hit.collider.gameObject;
                return true;
            }
        }
        return false;
    }

    bool IsWallBetweenPositions(Vector3 startPos, Vector3 endPos)
    {
        Vector3 start = startPos + Vector3.up * 1.2f;
        Vector3 direction = (endPos - start).normalized;
        float distance = Vector3.Distance(start, endPos);
        float checkDist = Mathf.Min(distance, wallDetectionDistance);

        RaycastHit[] hits = Physics.SphereCastAll(start, wallDetectionRadius, direction, checkDist, destructibleWallLayer);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject != gameObject)
            {
                currentWallTarget = hit.collider.gameObject;
                return true;
            }
        }
        currentWallTarget = null;
        return false;
    }

    void HandlePassiveState()
    {
        if (hasTargetOfInterest)
        {
            WakeUp();
        }
    }

    void WakeUp()
    {
        hasAwakened = true;

        if (currentState == State.Eating && audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }

        anim.SetBool(sleepAnimBool, false);
        anim.SetBool(eatAnimBool, false);
        agent.updateRotation = false;

        if (patrolPoints.Length > 0)
        {
            SetNextPatrol();
        }

        StartCoroutine(RiseAndRoar());
    }

    IEnumerator InvestigatePosition(Vector3 position)
    {
        if (currentState == State.Investigating || isTransitioning) yield break;

        ChangeState(State.Investigating);
        currentAlertLevel = AudioAlertLevel.Suspicious;

        agent.speed = investigationSpeed;
        agent.stoppingDistance = 1f;

        if (IsWallBetweenPositions(transform.position, position))
        {
            yield return StartCoroutine(AttackWallCycle());
            if (hasTargetOfInterest) yield break;
            ChangeState(State.Investigating);
        }

        agent.SetDestination(position);
        agent.isStopped = false;

        anim.SetBool("isWalking", false);
        UpdateFootsteps(crawlFootstepClip, crawlFootstepInterval);

        float investigationTimer = 0f;

        while (investigationTimer < investigationDuration)
        {
            investigationTimer += Time.deltaTime;

            if (hasTargetOfInterest && currentAlertLevel >= AudioAlertLevel.Alert)
            {
                yield break; 
            }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                StopMovementCompletely();
                
                transform.Rotate(Vector3.up, 40f * Time.deltaTime);
                anim.SetBool("isCrawling", false);
            }
            else
            {
                if (agent.isStopped) agent.isStopped = false;

                
                if (agent.velocity.magnitude > 0.1f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(agent.velocity.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
                }

                
                bool isMoving = agent.velocity.sqrMagnitude > movementThreshold;
                if (isMoving) stopAnimTimer = stopAnimDelay;
                else stopAnimTimer -= Time.deltaTime;

                anim.SetBool("isCrawling", stopAnimTimer > 0);
            }

            yield return null;
        }

        hasTargetOfInterest = false;
        currentAlertLevel = AudioAlertLevel.Calm;
        StartCoroutine(ReturnToCrawl());
    }

    void Patrol()
    {
        if (currentState != State.Patrol)
        {
            ChangeState(State.Patrol);
            agent.stoppingDistance = patrolStopDistance;
            agent.speed = crawlSpeed;
            agent.updateRotation = true;

            if (patrolPoints.Length > 0 && !agent.hasPath)
            {
                SetNextPatrol();
            }
        }

        if (patrolPoints.Length == 0)
        {
            StopMovementCompletely();
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (!isWaiting)
            {
                StopMovementCompletely();
                StartCoroutine(WaitAtPatrol());
            }
        }
        else
        {
            if (agent.isStopped) agent.isStopped = false;

            
            bool isMoving = agent.velocity.sqrMagnitude > movementThreshold;

            if (isMoving) stopAnimTimer = stopAnimDelay;
            else stopAnimTimer -= Time.deltaTime;

            bool animState = stopAnimTimer > 0;

            anim.SetBool("isCrawling", animState);
            anim.SetBool("isWalking", false);

            if (animState) UpdateFootsteps(crawlFootstepClip, crawlFootstepInterval);
        }
    }

    IEnumerator WaitAtPatrol()
    {
        isWaiting = true;
        anim.SetBool("isCrawling", false);

        yield return new WaitForSeconds(patrolWaitTime);

        SetNextPatrol();
        yield return new WaitForSeconds(transitionDelay);

        agent.isStopped = false;
        isWaiting = false;
    }

    void SetNextPatrol()
    {
        if (patrolPoints.Length == 0) return;
        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        agent.SetDestination(patrolPoints[patrolIndex].position);
    }

    IEnumerator AttackWallCycle()
    {
        if (currentWallTarget == null || isAttacking || isTransitioning) yield break;

        GameObject wallToAttack = currentWallTarget;

        
        agent.isStopped = false;
        agent.speed = walkSpeed;
        agent.stoppingDistance = 0.5f;
        agent.SetDestination(wallToAttack.transform.position);

        float timeout = 2.0f;
        float timer = 0f;

        
        while (wallToAttack != null && Vector3.Distance(transform.position, wallToAttack.transform.position) > 1.8f && timer < timeout)
        {
            timer += Time.deltaTime;
            anim.SetBool("isWalking", true);
            RotateToTarget(wallToAttack.transform.position);
            yield return null;
        }

        
        isTransitioning = true;
        ChangeState(State.Attacking);
        isAttacking = true;

        StopMovementCompletely();

        if (wallToAttack != null)
        {
            RotateToTarget(wallToAttack.transform.position);
        }

        anim.SetBool("isWalking", false);
        anim.SetBool("isCrawling", false);
        anim.SetBool("isAttacking", true);

        anim.SetTrigger("Attack3"); 

        yield return new WaitForSeconds(0.4f);

        TryToDestroyWall(); 

        yield return new WaitForSeconds(attackDuration - 0.4f);

        DisableAllHitboxes();

        anim.SetBool("isAttacking", false);
        isAttacking = false;
        isTransitioning = false;

        yield return new WaitForSeconds(attackCooldown);

        ChangeState(State.Chasing);
    }

    IEnumerator RiseAndRoar()
    {
        if (isRising || isRoaring || isTransitioning) yield break;
        if (currentState == State.Rising || currentState == State.Roaring) yield break;

        isTransitioning = true;
        ChangeState(State.Rising);
        isRising = true;

        if (enemyCameraController != null)
            enemyCameraController.StartTrackingEnemy(transform);

        StopMovementCompletely();
        ResetAllAnimations();

        anim.SetTrigger("GetUp"); 

        yield return new WaitForSeconds(crawlToStateDuration);

        isRising = false;
        isRoaring = true;
        ChangeState(State.Roaring);

        PlayRoarSound();
        anim.SetTrigger("Roar");

        yield return new WaitForSeconds(roarDuration);

        isRoaring = false;
        isTransitioning = false;
        ChangeState(State.Chasing);

        yield return new WaitForSeconds(transitionDelay);

        agent.speed = walkSpeed;
        agent.isStopped = false;
        agent.updateRotation = false; 
        anim.SetBool("isWalking", true);

        UpdateFootsteps(walkFootstepClip, walkFootstepInterval);

        if (!hasShownFirstDetection)
        {
            hasShownFirstDetection = true;
            lastReDetectionTime = Time.time;
        }
    }

    IEnumerator AttackCycle()
    {
        if (isAttacking || isTransitioning) yield break;
        if (currentState == State.Attacking) yield break;

        isTransitioning = true;
        ChangeState(State.Attacking);
        isAttacking = true;

        StopMovementCompletely();
        
        RotateToTarget(targetPositionOfInterest);

        anim.SetBool("isWalking", false);
        anim.SetBool("isCrawling", false);
        anim.SetBool("isAttacking", true);

        int rand = Random.Range(1, 4);
        anim.SetTrigger("Attack" + rand);

        yield return new WaitForSeconds(attackDuration);

        DisableAllHitboxes();

        anim.SetBool("isAttacking", false);
        isAttacking = false;
        isTransitioning = false;

        yield return null;

        if (hasTargetOfInterest)
        {
            ChangeState(State.Chasing);
            yield return new WaitForSeconds(transitionDelay);
            agent.speed = walkSpeed;
            agent.isStopped = false;
            
            UpdateFootsteps(walkFootstepClip, walkFootstepInterval);
        }
        else
        {
            if (!returningToCrawl && hasAwakened)
            {
                StartCoroutine(ReturnToCrawl());
            }
        }

        yield return new WaitForSeconds(attackCooldown);
    }

    IEnumerator ReturnToCrawl()
    {
        if (returningToCrawl || isTransitioning) yield break;

        isTransitioning = true;
        returningToCrawl = true;
        ChangeState(State.ReturningToCrawl);

        if (enemyCameraController != null)
            enemyCameraController.StopTrackingEnemy();

        StopMovementCompletely();
        ResetAllAnimations();

        anim.SetTrigger("ToCrawl");

        yield return new WaitForSeconds(stateToCrawlDuration);

        agent.stoppingDistance = patrolStopDistance;
        agent.speed = crawlSpeed;

        ChangeState(State.Patrol);

        SetNextPatrol();

        yield return new WaitForSeconds(transitionDelay);

        agent.isStopped = false;
        
        returningToCrawl = false;
        isTransitioning = false;

        UpdateFootsteps(crawlFootstepClip, crawlFootstepInterval);
    }

    void ChangeState(State newState)
    {
        if (currentState != newState)
        {
            previousState = currentState;
            currentState = newState;
        }
    }

    void StopMovementCompletely()
    {
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.ResetPath();
        StopFootsteps();
    }

    void ResetAllAnimations()
    {
        anim.SetBool("isCrawling", false);
        anim.SetBool("isWalking", false);
        anim.SetBool("isAttacking", false);
    }

    void RotateToTarget(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;

        if (dir != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotationSpeed);
        }
    }

    private void DisableAllHitboxes()
    {
        DisableRightHand();
        DisableLeftHand();
    }

    public void EnableRightHand()
    {
        if (rightHandCollider != null) rightHandCollider.SetActive(true);
    }

    public void DisableRightHand()
    {
        if (rightHandCollider != null) rightHandCollider.SetActive(false);
    }

    public void EnableLeftHand()
    {
        if (leftHandCollider != null) leftHandCollider.SetActive(true);
    }

    public void DisableLeftHand()
    {
        if (leftHandCollider != null) leftHandCollider.SetActive(false);
    }

    public void PlayAttackSound()
    {
        if (audioSource != null)
        {
            audioSource.pitch = 1f;
            if (attackClip != null) audioSource.PlayOneShot(attackClip);
            if (secondaryAttackClip != null) audioSource.PlayOneShot(secondaryAttackClip);
        }
    }

    public void TryToDestroyWall()
    {
        if (currentWallTarget != null)
        {
            Wall_Destruction wallScript = currentWallTarget.GetComponent<Wall_Destruction>();

            if (wallScript != null)
            {
                Vector3 impactPoint = currentWallTarget.transform.position;
                Vector3 impactDirection = transform.forward;

                wallScript.Explode(impactPoint, impactDirection);
                DialogueManager.ShowEnemyWallBreakDialogue();

                if (audioSource != null && wallBreakSound != null)
                    audioSource.PlayOneShot(wallBreakSound);

                currentWallTarget = null;
            }
        }
    }

    public void OnEnemyDeath()
    {
        if (enemyCameraController != null)
            enemyCameraController.StopTrackingEnemy();

        ChangeState(State.Dead);
        StopAllCoroutines();
        agent.isStopped = true;
    }

    private IEnumerator PlayFootsteps(AudioClip clip, float interval)
    {
        while (true)
        {
            if (currentState == State.Dead ||
                agent.velocity.sqrMagnitude <= movementThreshold ||
                currentFootstepClip != clip)
            {
                footstepCoroutine = null;
                yield break;
            }

            if (audioSource != null && clip != null)
            {
                audioSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
                audioSource.PlayOneShot(clip);
            }

            yield return new WaitForSeconds(interval);
        }
    }

    private void UpdateFootsteps(AudioClip clip, float interval)
    {
        if (footstepCoroutine != null && currentFootstepClip == clip && currentFootstepInterval == interval)
        {
            return;
        }

        StopFootsteps();

        currentFootstepClip = clip;
        currentFootstepInterval = interval;

        Invoke(nameof(StartFootstepsDelayed), 0.15f);
    }

    private void StartFootstepsDelayed()
    {
        if (agent.velocity.sqrMagnitude > movementThreshold && currentFootstepClip != null)
        {
            footstepCoroutine = StartCoroutine(PlayFootsteps(currentFootstepClip, currentFootstepInterval));
        }
    }

    private void StopFootsteps()
    {
        if (footstepCoroutine != null)
        {
            StopCoroutine(footstepCoroutine);
            footstepCoroutine = null;
        }

        currentFootstepClip = null;
        CancelInvoke(nameof(StartFootstepsDelayed));
    }

    public void PlayRoarSound()
    {
        if (audioSource != null && roarClip != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(roarClip);
        }
    }

    public void AE_StartRoarEffect()
    {
        if (roarMaterial == null) return;
        roarMaterial.SetFloat(_isActiveID, 1f);
        if (roarVisualCoroutine != null) StopCoroutine(roarVisualCoroutine);
        roarVisualCoroutine = StartCoroutine(RoarIntensityRoutine());
    }

    public void AE_StopRoarEffect()
    {
        if (roarMaterial == null) return;
        roarMaterial.SetFloat(_isActiveID, 0f);
        roarMaterial.SetFloat(_roarIntensityID, 0f);
        if (roarVisualCoroutine != null)
        {
            StopCoroutine(roarVisualCoroutine);
            roarVisualCoroutine = null;
        }
    }

    private IEnumerator RoarIntensityRoutine()
    {
        float timer = 0f;
        while (true)
        {
            timer += Time.deltaTime;
            float pulse = Mathf.Abs(Mathf.Sin(timer * 10f));
            float currentIntensity = pulse * maxRoarDistortion;
            roarMaterial.SetFloat(_roarIntensityID, currentIntensity);
            yield return null;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showAudioDebug) return;

        Gizmos.color = audioRangeColor;
        Gizmos.DrawWireSphere(transform.position, maxHearingDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, minDetectionRadius);

        Gizmos.color = Color.red;
        if (agent != null)
            Gizmos.DrawWireSphere(transform.position, agent.stoppingDistance);
        else
            Gizmos.DrawWireSphere(transform.position, attackRange * 0.7f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, wallDetectionDistance);

        if (hasTargetOfInterest)
        {
            Gizmos.color = detectionColor;
            Gizmos.DrawSphere(targetPositionOfInterest, 0.5f);
            Gizmos.DrawLine(transform.position, targetPositionOfInterest);
        }

        
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(origin, origin + transform.forward * 1.5f);
        Gizmos.DrawWireSphere(origin + transform.forward * 1.5f, 0.4f);
    }
}