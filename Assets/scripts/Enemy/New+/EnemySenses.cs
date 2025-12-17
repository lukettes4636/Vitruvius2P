using UnityEngine;
using System.Collections.Generic;

public class EnemySenses : MonoBehaviour
{
    [Header("Deteccion de Jugadores")]
    public Transform[] playerTargets;

    [Header("Deteccion de NPCs")]
    [Tooltip("NPCs que el enemigo puede detectar y atacar")]
    public Transform[] npcTargets;

    [Header("Deteccion de Objetos")]
    [Tooltip("Sistema de deteccion de objetos que emiten ruido")]
    public ObjectNoiseDetection objectNoiseDetection;

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
    public Transform CurrentNPCTarget { get; private set; }
    public Transform CurrentNoisyObject { get; private set; }
    public GameObject CurrentWallTarget { get; private set; }
    public float CurrentAlertLevel { get; private set; }
    
    
    
    
    public Transform CurrentTarget => CurrentPlayer ?? CurrentNPCTarget ?? CurrentNoisyObject;

    public bool showDebugGizmos = true;
    private Dictionary<Transform, float> ignoredObjectsUntil = new Dictionary<Transform, float>();

    public void Tick()
    {
        PruneIgnoredObjects();
        ProcessAudioDetection();
        ProcessObjectNoiseDetection();
    }

    private void ProcessAudioDetection()
    {
        Transform bestTarget = null;
        float minDistance = float.MaxValue;
        float maxAudioStrength = 0f;
        bool isTargetNPC = false;

        
        void CheckTarget(Transform target, float noiseRadius, bool isNPC)
        {
            if (target == null) return;
            
            float dist = Vector3.Distance(transform.position, target.position);
            
            
            float strength = CalculateAudioStrength(target, noiseRadius, dist);
            
            if (strength > detectionThreshold)
            {
                
                if (strength > maxAudioStrength) maxAudioStrength = strength;

                
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTarget = target;
                    isTargetNPC = isNPC;
                }
            }
        }

        foreach (Transform player in playerTargets)
        {
            if (player == null) continue;
            var health = player.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead) continue;
            var noise = player.GetComponent<PlayerNoiseEmitter>();
            if (noise == null || noise.currentNoiseRadius < 0.1f) continue;
            
            CheckTarget(player, noise.currentNoiseRadius, false);
        }

        foreach (Transform npc in npcTargets)
        {
            if (npc == null) continue;
            var health = npc.GetComponent<NPCHealth>();
            if (health != null && health.IsDead) continue;
            var noise = npc.GetComponent<NPCNoiseEmitter>();
            if (noise == null || noise.currentNoiseRadius < 0.1f) continue;

            CheckTarget(npc, noise.currentNoiseRadius, true);
        }

        CurrentAlertLevel = maxAudioStrength;

        if (bestTarget != null)
        {
            if (isTargetNPC)
            {
                CurrentNPCTarget = bestTarget;
                CurrentPlayer = null;
            }
            else
            {
                CurrentPlayer = bestTarget;
                CurrentNPCTarget = null;
            }
            TargetPositionOfInterest = bestTarget.position;
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
                CurrentNPCTarget = null;
            }
        }
    }

    private float CalculateAudioStrength(Transform target, float noiseRadius, float distance)
    {
        if (distance > maxHearingDistance) return 0f;
        float rawRadius = noiseRadius * audioSensitivity;
        int walls = CountSoundBlockers(target);
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

    private void ProcessObjectNoiseDetection()
    {
        
        if (HasTargetOfInterest && (CurrentPlayer != null || CurrentNPCTarget != null))
        {
            CurrentNoisyObject = null;
            return;
        }

        
        if (objectNoiseDetection != null && objectNoiseDetection.HasNoisyObjectNearby())
        {
            if (objectNoiseDetection.GetLoudestObject(out Transform noisyObject, out Vector3 objectPosition))
            {
                if (noisyObject != null && IsObjectIgnored(noisyObject))
                {
                    
                }
                else
                {
                    CurrentNoisyObject = noisyObject;
                    TargetPositionOfInterest = objectPosition;
                    HasTargetOfInterest = true;
                    timeSinceLastHeard = 0f;
                    return;
                }
            }
        }

        
        if (CurrentPlayer == null && CurrentNPCTarget == null)
        {
            CurrentNoisyObject = null;
        }
    }

    public void ForgetTarget()
    {
        HasTargetOfInterest = false;
        CurrentPlayer = null;
        CurrentNPCTarget = null;
        CurrentNoisyObject = null;
    }

    public void SetPlayerTarget(Transform player)
    {
        CurrentPlayer = player;
        CurrentNPCTarget = null;
        if (player != null)
        {
            TargetPositionOfInterest = player.position;
            HasTargetOfInterest = true;
        }
    }

    public void SetNPCTarget(Transform npc)
    {
        CurrentNPCTarget = npc;
        CurrentPlayer = null;
        if (npc != null)
        {
            TargetPositionOfInterest = npc.position;
            HasTargetOfInterest = true;
        }
    }

    public void IgnoreCurrentNoisyObjectFor(float seconds)
    {
        if (CurrentNoisyObject != null)
        {
            ignoredObjectsUntil[CurrentNoisyObject] = Time.time + seconds;
            CurrentNoisyObject = null;
            HasTargetOfInterest = false;
        }
    }
    private bool IsObjectIgnored(Transform obj)
    {
        if (obj == null) return false;
        if (ignoredObjectsUntil.TryGetValue(obj, out float until))
        {
            return Time.time < until;
        }
        return false;
    }
    private void PruneIgnoredObjects()
    {
        if (ignoredObjectsUntil.Count == 0) return;
        var keys = new List<Transform>(ignoredObjectsUntil.Keys);
        foreach (var k in keys)
        {
            if (ignoredObjectsUntil[k] <= Time.time)
                ignoredObjectsUntil.Remove(k);
        }
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
