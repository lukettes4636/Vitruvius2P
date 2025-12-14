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

    [Header("Patrulla")]
    public Transform[] patrolPoints;
    public float patrolWaitTime = 2f;
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
                
                motor.MoveTo(senses.TargetPositionOfInterest, walkSpeed, 1f);
                visuals.UpdateAnimationState(false);
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
            motor.MoveTo(senses.TargetPositionOfInterest, walkSpeed, attackRange - 0.5f);
            visuals.UpdateAnimationState(false);
        }

        if (senses.HasTargetOfInterest)
        {
            if (Vector3.Distance(transform.position, senses.TargetPositionOfInterest) <= attackRange && !senses.CheckForWallInFront())
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
            StartCoroutine(WakeUpAndRoarRoutine());
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

        yield return new WaitUntil(() => visuals.AnimImpactReceived);

        TryDestroyWall(wall);

        yield return new WaitUntil(() => visuals.AnimFinishedReceived);

        visuals.StopAttack();
        currentState = State.Chasing;
        yield return new WaitForSeconds(attackCooldown);
    }

    IEnumerator AttackTargetRoutine()
    {
        currentState = State.Attacking;
        motor.Stop();
        motor.RotateTowards(senses.TargetPositionOfInterest);

        visuals.TriggerAttack(Random.Range(1, 4));

        yield return new WaitUntil(() => visuals.AnimFinishedReceived);

        visuals.StopAttack();
        currentState = State.Chasing;
        yield return new WaitForSeconds(attackCooldown);
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
        motor.MoveTo(pos, investigationSpeed, 1f);

        float timer = 0f;
        while (timer < 8.0f)
        {
            timer += Time.deltaTime;

            
            if (motor.GetRemainingDistance() > 0.2f)
            {
                visuals.UpdateAnimationState(true);
                
                visuals.SetInvestigatingMode(false);
            }
            else
            {
                
                motor.Stop();
                
                visuals.SetInvestigatingMode(true);
                visuals.UpdateAnimationState(true); 
            }
            yield return null;
        }

        
        visuals.SetInvestigatingMode(false);
        StartCoroutine(ReturnToPatrolRoutine());
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

    
    void WakeUp() { hasAwakened = true; visuals.SetPassiveState(0); StartCoroutine(WakeUpAndRoarRoutine()); }
    void GoToNextPatrolPoint() { if (patrolPoints.Length == 0) return; patrolIndex = (patrolIndex + 1) % patrolPoints.Length; motor.MoveTo(patrolPoints[patrolIndex].position, crawlSpeed, 0.1f); }
    void TryDestroyWall(GameObject w) { if (w) { var s = w.GetComponent<Wall_Destruction>(); if (s) { s.Explode(w.transform.position, transform.forward); visuals.PlayWallBreakSound(); DialogueManager.ShowEnemyWallBreakDialogue(); } } }
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
    public void OnEnemyDeath() { currentState = State.Dead; motor.Stop(); StopAllCoroutines(); if (cameraController) cameraController.StopTrackingEnemy(); }
}