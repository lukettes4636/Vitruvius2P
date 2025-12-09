using UnityEngine;

public class EnemySenses : MonoBehaviour
{
    [Header("Deteccion de Jugadores")]
    public Transform[] playerTargets;

    [Header("Configuracion de Audio")]
    [Range(0.5f, 3.0f)] public float audioSensitivity = 1.0f;
    public float maxHearingDistance = 20f;
    public float minDetectionRadius = 1.5f;
    [Range(0.0f, 1.0f)] public float detectionThreshold = 0.2f;

    [Header("Obstaculos de Audio")]
    public LayerMask soundBlockerLayer;
    [Range(0.1f, 0.9f)] public float soundAttenuationPerWall = 0.7f;

    [Header("Memoria (Persistencia)")]
    public float memoryDuration = 3.0f;
    private float timeSinceLastHeard = 0f;

    [Header("Deteccion de Paredes")]
    public LayerMask destructibleWallLayer;
    public float wallDetectionDistance = 3.0f;
    public float wallDetectionRadius = 0.5f;

    
    public Vector3 TargetPositionOfInterest { get; private set; }
    public bool HasTargetOfInterest { get; private set; }
    public Transform CurrentPlayer { get; private set; }
    public GameObject CurrentWallTarget { get; private set; }
    public float CurrentAlertLevel { get; private set; }

    public bool showDebugGizmos = true;

    public void Tick()
    {
        ProcessAudioDetection();
    }

    private void ProcessAudioDetection()
    {
        Transform loudestPlayer = null;
        float maxAudioStrength = 0f;

        foreach (Transform player in playerTargets)
        {
            if (player == null) continue;
            var health = player.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead) continue;

            float dist = Vector3.Distance(transform.position, player.position);
            var noiseEmitter = player.GetComponent<PlayerNoiseEmitter>();

            if (noiseEmitter == null || noiseEmitter.currentNoiseRadius < 0.1f) continue;
            float strength = CalculateAudioStrength(player, noiseEmitter, dist);

            if (strength > maxAudioStrength)
            {
                maxAudioStrength = strength;
                loudestPlayer = player;
            }
        }

        CurrentAlertLevel = maxAudioStrength;

        if (loudestPlayer != null && maxAudioStrength > detectionThreshold)
        {
            CurrentPlayer = loudestPlayer;
            TargetPositionOfInterest = CurrentPlayer.position;
            HasTargetOfInterest = true;
            timeSinceLastHeard = 0f;
        }
        else
        {
            timeSinceLastHeard += Time.deltaTime;
            if (timeSinceLastHeard > memoryDuration)
            {
                HasTargetOfInterest = false;
                CurrentPlayer = null;
            }
        }
    }

    private float CalculateAudioStrength(Transform player, PlayerNoiseEmitter noiseEmitter, float distance)
    {
        if (distance > maxHearingDistance) return 0f;
        float rawRadius = noiseEmitter.currentNoiseRadius * audioSensitivity;
        int walls = CountSoundBlockers(player);
        float attenuatedRadius = rawRadius * Mathf.Pow(soundAttenuationPerWall, walls);
        float effectiveRadius = Mathf.Max(attenuatedRadius, minDetectionRadius);
        if (distance <= effectiveRadius) return Mathf.Clamp01(1f - (distance / effectiveRadius));
        return 0f;
    }

    private int CountSoundBlockers(Transform target)
    {
        Vector3 start = transform.position + Vector3.up;
        Vector3 end = target.position + Vector3.up;
        Vector3 dir = (end - start).normalized;
        float dist = Vector3.Distance(start, end);
        return Physics.RaycastAll(start, dir, dist, soundBlockerLayer).Length;
    }

    
    
    public bool CheckForWallInFront()
    {
        Vector3 origin = transform.position + Vector3.up;

        
        Vector3 checkPos = origin + transform.forward * 0.8f;
        float checkRadius = 0.6f;

        
        
        Collider[] hits = Physics.OverlapSphere(checkPos, checkRadius, destructibleWallLayer);

        foreach (var col in hits)
        {
            if (col.gameObject != gameObject)
            {
                CurrentWallTarget = col.gameObject;
                return true;
            }
        }
        return false;
    }

    public bool CheckWallInPathToTarget()
    {
        if (!HasTargetOfInterest) return false;

        Vector3 start = transform.position + Vector3.up * 1.2f;
        Vector3 end = TargetPositionOfInterest + Vector3.up;
        Vector3 direction = (end - start).normalized;
        float distance = Vector3.Distance(start, end);
        float checkDist = Mathf.Min(distance, wallDetectionDistance);

        RaycastHit[] hits = Physics.SphereCastAll(start, wallDetectionRadius, direction, checkDist, destructibleWallLayer);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject != gameObject)
            {
                CurrentWallTarget = hit.collider.gameObject;
                return true;
            }
        }
        CurrentWallTarget = null;
        return false;
    }

    public void ForgetTarget()
    {
        HasTargetOfInterest = false;
        CurrentPlayer = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxHearingDistance);

        if (HasTargetOfInterest)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, TargetPositionOfInterest);
        }

        
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up;
        Vector3 checkPos = origin + transform.forward * 0.8f;
        Gizmos.DrawWireSphere(checkPos, 0.6f);
    }
}