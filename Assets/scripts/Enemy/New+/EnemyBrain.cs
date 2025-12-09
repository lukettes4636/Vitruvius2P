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
    public float attackDuration = 1.0f;
    public float attackCooldown = 0.7f;

    [Header("Patrulla")]
    public Transform[] patrolPoints;
    public float patrolWaitTime = 2f;
    private int patrolIndex = 0;

    [Header("Tiempos de Transicion")]
    public float roarDuration = 1.2f;
    public float getUpDuration = 1.1f;
    public float toCrawlDuration = 1.0f;

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
                visuals.TriggerGetUp();     
                motor.SetAutoRotation(true);
                if (patrolPoints.Length > 0) GoToNextPatrolPoint();
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
        
        if (senses.CurrentPlayer != null)
        {
            var health = senses.CurrentPlayer.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead)
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
            float dist = Vector3.Distance(transform.position, senses.TargetPositionOfInterest);
            if (dist <= attackRange && !senses.CheckForWallInFront())
            {
                StartCoroutine(AttackPlayerRoutine());
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

    IEnumerator AttackPlayerRoutine()
    {
        currentState = State.Attacking;
        motor.Stop();
        motor.RotateTowards(senses.TargetPositionOfInterest);

        int randAttack = Random.Range(1, 4);
        visuals.TriggerAttack(randAttack);

        yield return new WaitForSeconds(attackDuration);

        visuals.StopAttack();
        currentState = State.Chasing;
        yield return new WaitForSeconds(attackCooldown);
    }

    IEnumerator AttackWallRoutine(GameObject wall)
    {
        currentState = State.Attacking;

        
        motor.MoveTo(wall.transform.position, walkSpeed, 0.6f);
        float timer = 0f;
        while (wall != null && Vector3.Distance(transform.position, wall.transform.position) > 1.8f && timer < 2f)
        {
            timer += Time.deltaTime;
            visuals.UpdateAnimationState(false);
            yield return null;
        }

        
        motor.Stop();
        if (wall != null) motor.RotateTowards(wall.transform.position);

        visuals.TriggerAttack(3);

        yield return new WaitForSeconds(0.4f);

        TryDestroyWall(wall);

        yield return new WaitForSeconds(attackDuration - 0.4f);
        visuals.StopAttack();

        currentState = State.Chasing;
        yield return new WaitForSeconds(attackCooldown);
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
            }
            else
            {
                motor.Stop();
                transform.Rotate(Vector3.up, 40f * Time.deltaTime);
                visuals.UpdateAnimationState(true); 
            }
            yield return null;
        }

        StartCoroutine(ReturnToPatrolRoutine());
    }

    IEnumerator ReturnToPatrolRoutine()
    {
        currentState = State.Transitioning;
        if (cameraController) cameraController.StopTrackingEnemy();

        motor.Stop();
        visuals.TriggerToCrawl();
        yield return new WaitForSeconds(toCrawlDuration);

        currentState = State.Patrol;
        motor.SetAutoRotation(true);
        GoToNextPatrolPoint();
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
        StartCoroutine(WakeUpAndRoarRoutine());
    }

    IEnumerator WakeUpAndRoarRoutine()
    {
        currentState = State.Transitioning;
        motor.Stop();
        motor.SetAutoRotation(false);

        if (cameraController) cameraController.StartTrackingEnemy(transform);

        visuals.TriggerGetUp();
        yield return new WaitForSeconds(getUpDuration);

        visuals.PlayRoarSound();
        visuals.TriggerRoar();
        yield return new WaitForSeconds(roarDuration);

        currentState = State.Chasing;
    }

    void GoToNextPatrolPoint()
    {
        if (patrolPoints.Length == 0) return;
        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        motor.MoveTo(patrolPoints[patrolIndex].position, crawlSpeed, 0.1f);
    }

    void TryDestroyWall(GameObject wall)
    {
        if (wall != null)
        {
            var wallScript = wall.GetComponent<Wall_Destruction>();
            if (wallScript != null)
            {
                wallScript.Explode(wall.transform.position, transform.forward);
                visuals.PlayWallBreakSound();
                DialogueManager.ShowEnemyWallBreakDialogue();
            }
        }
    }

    void HandleDialogueFeedback()
    {
        if (!senses.HasTargetOfInterest) return;

        if (!hasShownFirstDetection)
        {
            hasShownFirstDetection = true;
            lastReDetectionTime = Time.time;
        }
        else if (Time.time - lastReDetectionTime > 2.0f && currentState == State.Patrol)
        {
            DialogueManager.ShowEnemyDetectedAgainDialogue(senses.CurrentPlayer.gameObject);
            lastReDetectionTime = Time.time;
        }
    }

    public void OnEnemyDeath()
    {
        currentState = State.Dead;
        motor.Stop();
        StopAllCoroutines();
        if (cameraController) cameraController.StopTrackingEnemy();
    }
}