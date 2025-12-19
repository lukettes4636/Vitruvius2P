using UnityEngine;
using System.Collections.Generic;

public class ObjectNoiseDetection : MonoBehaviour
{
    [Header("Deteccion de Objetos")]
    public ObjectNoiseEmitter[] objectNoiseTargets;
    public bool autoFindObjects = true;

    [Header("Configuracion")]
    public float maxDetectionDistance = 20f;
    public float detectionThreshold = 2f;

    private List<ObjectNoiseEmitter> activeNoiseObjects = new List<ObjectNoiseEmitter>();

    void Start()
    {
        if (autoFindObjects) { FindAllNoiseObjects(); }
        else { activeNoiseObjects.AddRange(objectNoiseTargets); }
    }

    void FindAllNoiseObjects()
    {
        ObjectNoiseEmitter[] allObjects = Object.FindObjectsOfType<ObjectNoiseEmitter>();
        activeNoiseObjects.Clear();

        foreach (var obj in allObjects)
        {
            
            if (obj.isDebris) continue;

            if (!activeNoiseObjects.Contains(obj))
                activeNoiseObjects.Add(obj);
        }
    }

    public bool GetLoudestObject(out Transform objectTransform, out Vector3 position)
    {
        objectTransform = null;
        position = Vector3.zero;
        float maxNoise = 0f;
        Transform loudest = null;

        foreach (var noiseObj in activeNoiseObjects)
        {
            if (noiseObj == null || noiseObj.currentNoiseRadius < detectionThreshold) continue;

            float dist = Vector3.Distance(transform.position, noiseObj.transform.position);
            if (dist > maxDetectionDistance) continue;

            float effectiveNoise = noiseObj.currentNoiseRadius * (1f - (dist / maxDetectionDistance));
            if (effectiveNoise > maxNoise)
            {
                maxNoise = effectiveNoise;
                loudest = noiseObj.transform;
            }
        }

        if (loudest != null)
        {
            objectTransform = loudest;
            position = loudest.position;
            return true;
        }
        return false;
    }

    public bool HasNoisyObjectNearby()
    {
        foreach (var noiseObj in activeNoiseObjects)
        {
            if (noiseObj == null || noiseObj.currentNoiseRadius < detectionThreshold) continue;
            float dist = Vector3.Distance(transform.position, noiseObj.transform.position);
            if (dist <= maxDetectionDistance && dist <= noiseObj.currentNoiseRadius) return true;
        }
        return false;
    }
}