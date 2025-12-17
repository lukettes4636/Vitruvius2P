using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemySenses), typeof(EnemyMotor), typeof(EnemyVisuals))]
public class EnemyBrain : MonoBehaviour
{
    private EnemySenses senses;
    private EnemyMotor motor;
    private EnemyVisuals visuals;
    private EnemyCameraController cameraController;

    [Header("Configuracion de Estado Inicial")]
    public InitialState initialState = InitialState.Sleeping;
    public enum InitialState { Sleeping, Eating, Patrol }

    [Header("Velocidades")]
    public float crawlSpeed = 1.2f;
    public float walkSpeed = 2.5f;
    public float investigationSpeed = 1.8f;

    [Header("Combate")]
    public float attackRange = 2.2f;
    [Tooltip("Tiempo de espera despues de terminar un ataque")]
    public float attackCooldown = 0.5f;
public int attackDamage = 25;

    [Header("Patrulla")]
    public Transform[] patrolPoints;
    public float patrolWaitTime = 1f;
    private int patrolIndex = 0;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private enum State { Sleeping, Eating, Patrol, Investigating, Chasing, Attacking, Transitioning, Dead }
    [SerializeField] private State currentState;

    private bool hasAwakened = false;
    private bool isWaitingAtPatrol = false;
    private float alertTimer = 0f;
    private bool hasShownFirstDetection = false;
    private float lastReDetectionTime = -999f;
    private Vector3 preInvestigatePosition;
    private bool isInvestigatingObjectNoise = false;

    void Start()
    {
        senses = GetComponent<EnemySenses>();
        motor = GetComponent<EnemyMotor>();
        visuals = GetComponent<EnemyVisuals>();
        cameraController = FindObjectOfType<EnemyCameraController>();

        SetupInitialState();
    }

    void SetupInitialState()
    {
        visuals.UpdateAnimationState(false);

        switch (initialState)
        {
            case InitialState.Sleeping:
                currentState = State.Sleeping;
                visuals.SetPassiveState(0);
                break;
            case InitialState.Eating:
                currentState = State.Eating;
                visuals.SetPassiveState(1);
                break;
            case InitialState.Patrol:
                currentState = State.Patrol;
                hasAwakened = true;
                visuals.SetPassiveState(0);
                StartCoroutine(WakeUpAndRoarRoutine());
                break;
        }
    }

    void Update()
    {
        if (currentState == State.Dead) return;
        senses.Tick();

        if (currentState == State.Sleeping || currentState == State.Eating)
        {
            if (senses.HasTargetOfInterest) WakeUp();
            return;
        }

        if (currentState == State.Transitioning || currentState == State.Attacking) return;

        switch (currentState)
        {
            case State.Chasing:
                HandleChasing();
                break;
            case State.Patrol:
                HandlePatrol();
                break;
            case State.Investigating:
                if (senses.HasTargetOfInterest && senses.CurrentAlertLevel > 0.5f)
                {
                    StopAllCoroutines();
                    currentState = State.Chasing;
                }
                break;
        }
        HandleDialogueFeedback();
    }

    void HandleChasing()
    {
        
        Transform currentTarget = senses.CurrentTarget;
        if (currentTarget != null)
        {
            var playerHealth = currentTarget.GetComponent<PlayerHealth>();
            var npcHealth = currentTarget.GetComponent<NPCHealth>();
            
            
            if (senses.CurrentNoisyObject != null && currentTarget == senses.CurrentNoisyObject)
            {
                
                motor.MoveTo(senses.TargetPositionOfInterest, crawlSpeed, 1f);
                visuals.UpdateAnimationState(true);
                return;
            }
            
            bool isDead = (playerHealth != null && playerHealth.IsDead) || 
                         (npcHealth != null && npcHealth.IsDead);
            
            if (isDead)
            {
                senses.ForgetTarget();
                StartCoroutine(ReturnToPatrolRoutine());
                return;
            }
        }

        if (senses.CheckForWallInFront())
        {
            StartCoroutine(AttackWallRoutine(senses.CurrentWallTarget));
            return;
        }

        if (senses.HasTargetOfInterest && senses.CheckWallInPathToTarget())
        {
            StartCoroutine(AttackWallRoutine(senses.CurrentWallTarget));
            return;
        }

        if (senses.HasTargetOfInterest)
        {
            
            SelectClosestAliveTarget();

            motor.MoveTo(senses.TargetPositionOfInterest, walkSpeed, attackRange - 0.5f);
            visuals.UpdateAnimationState(false);
        }

        if (senses.HasTargetOfInterest)
        {
            bool hasCharacterTarget = senses.CurrentPlayer != null || senses.CurrentNPCTarget != null;
            if (hasCharacterTarget && Vector3.Distance(transform.position, senses.TargetPositionOfInterest) <= attackRange && !senses.CheckForWallInFront())
            {
                
                StartCoroutine(AttackTargetRoutine());
            }
        }
        else
        {
            DialogueManager.ShowEnemyChaseEndedDialogue();
            StartCoroutine(InvestigateRoutine(senses.TargetPositionOfInterest));
        }
    }

    void HandlePatrol()
    {
        if (senses.HasTargetOfInterest)
        {
            if (senses.CurrentNoisyObject != null && senses.CurrentPlayer == null && senses.CurrentNPCTarget == null)
            {
                StartCoroutine(WakeUpQuietRoutine());
            }
            else
            {
                StartCoroutine(WakeUpAndRoarRoutine());
            }
            return;
        }
        if (patrolPoints.Length == 0) return;

        if (motor.GetRemainingDistance() <= 0.2f)
        {
            if (!isWaitingAtPatrol) StartCoroutine(PatrolWaitRoutine());
        }
        else
        {
            visuals.UpdateAnimationState(true);
        }
    }

    

    IEnumerator WakeUpAndRoarRoutine()
    {
        currentState = State.Transitioning;
        motor.Stop();
        motor.SetAutoRotation(false);
        if (cameraController) cameraController.StartTrackingEnemy(transform);

        visuals.TriggerGetUp();
        yield return new WaitUntil(() => visuals.AnimFinishedReceived);

        visuals.TriggerRoar();
        visuals.PlayRoarSound();
        yield return new WaitUntil(() => visuals.AnimFinishedReceived);

        currentState = State.Chasing;
    }

    IEnumerator AttackWallRoutine(GameObject wall)
    {
        currentState = State.Attacking;
        motor.MoveTo(wall.transform.position, walkSpeed, 0.8f);

        float timer = 0f;
        while (wall != null && Vector3.Distance(transform.position, wall.transform.position) > 1.5f && timer < 3f)
        {
            timer += Time.deltaTime;
            visuals.UpdateAnimationState(false);
            yield return null;
        }

        motor.Stop();
        if (wall != null) motor.RotateTowards(wall.transform.position);
        yield return null;

        visuals.TriggerAttack(3);

        float impactTimer = 0f;
        while (!visuals.AnimImpactReceived && impactTimer < 2.0f)
        {
            impactTimer += Time.deltaTime;
            yield return null;
        }

        TryDestroyWall(wall);

        float finishTimer = 0f;
        while (!visuals.AnimFinishedReceived && finishTimer < 1.5f)
        {
            finishTimer += Time.deltaTime;
            yield return null;
        }

        visuals.StopAttack();
        currentState = State.Chasing;
        yield return new WaitForSeconds(attackCooldown);
    }

    IEnumerator AttackTargetRoutine()
    {
        bool hasCharacterTarget = senses.CurrentPlayer != null || senses.CurrentNPCTarget != null;
        if (!hasCharacterTarget)
        {
            currentState = State.Chasing;
            yield break;
        }
        Transform tgt = senses.CurrentNPCTarget != null ? senses.CurrentNPCTarget : senses.CurrentPlayer;
        var pHealth = tgt != null ? tgt.GetComponent<PlayerHealth>() : null;
        var nHealth = tgt != null ? tgt.GetComponent<NPCHealth>() : null;
        if ((pHealth != null && pHealth.IsDead) || (nHealth != null && nHealth.IsDead))
        {
            senses.ForgetTarget();
            StartCoroutine(ReturnToPatrolRoutine());
            yield break;
        }
        currentState = State.Attacking;
        motor.Stop();
        motor.RotateTowards(senses.TargetPositionOfInterest);

        visuals.TriggerAttack(Random.Range(1, 4));

        float safetyTimer = 0f;
        bool impactHappened = false;
        
        while (!visuals.AnimImpactReceived && !visuals.AnimFinishedReceived && safetyTimer < 2.0f)
        {
            safetyTimer += Time.deltaTime;
            if ((pHealth != null && pHealth.IsDead) || (nHealth != null && nHealth.IsDead))
            {
                visuals.StopAttack();
                senses.ForgetTarget();
                StartCoroutine(ReturnToPatrolRoutine());
                yield break;
            }
            if (visuals.AnimImpactReceived) impactHappened = true;
            yield return null;
        }
        
        
        visuals.EnableRightHand();
        visuals.EnableLeftHand();
        yield return new WaitForSeconds(0.2f);
        visuals.StopAttack();

        
        if ((pHealth != null && !pHealth.IsDead) || (nHealth != null && !nHealth.IsDead))
        {
            if (impactHappened || visuals.AnimImpactReceived || visuals.AnimFinishedReceived)
            {
                if (pHealth != null && !pHealth.IsDead) pHealth.TakeDamage(attackDamage);
                if (nHealth != null && !nHealth.IsDead) nHealth.TakeDamage(attackDamage);
            }
        }
        
        
        float finishTimer = 0f;
        while (!visuals.AnimFinishedReceived && finishTimer < 1.5f)
        {
            finishTimer += Time.deltaTime;
            yield return null;
        }

        visuals.StopAttack();
        if ((pHealth != null && pHealth.IsDead) || (nHealth != null && nHealth.IsDead))
        {
            senses.ForgetTarget();
            StartCoroutine(ReturnToPatrolRoutine());
            yield break;
        }
        currentState = State.Chasing;
        yield return new WaitForSeconds(attackCooldown);

        
        SelectClosestAliveTarget();
    }

    IEnumerator ReturnToPatrolRoutine()
    {
        currentState = State.Transitioning;
        if (cameraController) cameraController.StopTrackingEnemy();
        motor.Stop();

        visuals.TriggerToCrawl();
        yield return new WaitUntil(() => visuals.AnimFinishedReceived);

        currentState = State.Patrol;
        motor.SetAutoRotation(true);
        GoToNextPatrolPoint();
    }

    IEnumerator InvestigateRoutine(Vector3 pos)
    {
        currentState = State.Investigating;
        motor.MoveTo(pos, investigationSpeed, 0.1f);

        float timer = 0f;
        while (timer < 6.0f)
        {
            timer += Time.deltaTime;

            
            if (motor.GetRemainingDistance() > 0.15f)
            {
                visuals.UpdateAnimationState(true);
                
                visuals.SetInvestigatingMode(false);
            }
            else
            {
                
                motor.Stop();
                
                visuals.UpdateAnimationState(true);
                yield return StartCoroutine(visuals.RunScanCycles(4));
                break;
            }
            yield return null;
        }

        
        visuals.SetInvestigatingMode(false);
        senses.ForgetTarget();
        if (isInvestigatingObjectNoise)
        {
            senses.IgnoreCurrentNoisyObjectFor(8f);
            StartCoroutine(ReturnToPreviousSpotRoutine());
        }
        else
        {
            StartCoroutine(ReturnToPatrolRoutine());
        }
        isInvestigatingObjectNoise = false;
    }

    
    void SelectClosestAliveTarget()
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        bool isNPC = false;

        void CheckList(Transform[] list, bool npcFlag)
        {
            if (list == null) return;
            foreach (var t in list)
            {
                if (t == null) continue;
                var ph = t.GetComponent<PlayerHealth>();
                var nh = t.GetComponent<NPCHealth>();
                if ((ph != null && ph.IsDead) || (nh != null && nh.IsDead)) continue;

                float d = Vector3.Distance(transform.position, t.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = t;
                    isNPC = npcFlag;
                }
            }
        }

        CheckList(senses.playerTargets, false);
        CheckList(senses.npcTargets, true);

        if (best != null)
        {
            if (isNPC)
            {
                senses.SetNPCTarget(best);
            }
            else
            {
                senses.SetPlayerTarget(best);
            }
        }
    }

    IEnumerator PatrolWaitRoutine()
    {
        isWaitingAtPatrol = true;
        motor.Stop();
        visuals.UpdateAnimationState(true);
        yield return new WaitForSeconds(patrolWaitTime);
        GoToNextPatrolPoint();
        isWaitingAtPatrol = false;
    }

    
    void WakeUp() 
    { 
        hasAwakened = true; 
        visuals.SetPassiveState(0); 
        if (senses.CurrentNoisyObject != null && senses.CurrentPlayer == null && senses.CurrentNPCTarget == null)
        {
            StartCoroutine(WakeUpQuietRoutine());
        }
        else
        {
            StartCoroutine(WakeUpAndRoarRoutine());
        }
    }
    void GoToNextPatrolPoint() { if (patrolPoints.Length == 0) return; patrolIndex = (patrolIndex + 1) % patrolPoints.Length; motor.MoveTo(patrolPoints[patrolIndex].position, crawlSpeed, 0.1f); }
    void TryDestroyWall(GameObject w) { if (w) { var s = w.GetComponent<Wall_Destruction>(); if (s) { if (!w.activeSelf) w.SetActive(true); s.Explode(w.transform.position, transform.forward); visuals.PlayWallBreakSound(); DialogueManager.ShowEnemyWallBreakDialogue(); } } }
    void HandleDialogueFeedback() 
    { 
        if (!senses.HasTargetOfInterest) return; 
        Transform currentTarget = senses.CurrentTarget;
        if (currentTarget == null) return;
        
        if (!hasShownFirstDetection) 
        { 
            hasShownFirstDetection = true; 
            lastReDetectionTime = Time.time; 
        } 
        else if (Time.time - lastReDetectionTime > 2.0f && currentState == State.Patrol) 
        { 
            DialogueManager.ShowEnemyDetectedAgainDialogue(currentTarget.gameObject); 
            lastReDetectionTime = Time.time; 
        } 
    }
    public void OnEnemyDeath() 
    { 
        senses.ForgetTarget();
        visuals.StopAttack();
        visuals.SetInvestigatingMode(false);
        motor.Stop(); 
        StopAllCoroutines(); 
        if (cameraController) cameraController.StopTrackingEnemy(); 
        currentState = State.Dead; 
    }

    IEnumerator WakeUpQuietRoutine()
    {
        currentState = State.Transitioning;
        motor.Stop();
        motor.SetAutoRotation(false);
        visuals.UpdateAnimationState(true);
        preInvestigatePosition = transform.position;
        isInvestigatingObjectNoise = true;
        Vector3 pos = senses.TargetPositionOfInterest;
        StartCoroutine(InvestigateRoutine(pos));
        yield return null;
    }

    IEnumerator ReturnToPreviousSpotRoutine()
    {
        currentState = State.Transitioning;
        visuals.UpdateAnimationState(true);
        motor.MoveTo(preInvestigatePosition, crawlSpeed, 0.1f);
        float t = 0f;
        while (motor.GetRemainingDistance() > 0.2f && t < 6f)
        {
            t += Time.deltaTime;
            visuals.UpdateAnimationState(true);
            yield return null;
        }
        currentState = State.Patrol;
        motor.SetAutoRotation(true);
    }
}
