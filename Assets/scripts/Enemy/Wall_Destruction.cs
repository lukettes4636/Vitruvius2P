using UnityEngine;
using System.Collections;
using UnityEngine.VFX; 

public class Wall_Destruction : MonoBehaviour
{
    
    public GameObject fracturedWallPrefab;

    [Header("Efectos Visuales")] 
    
    public GameObject dustExplosionVFXPrefab;
    private const float VfxCleanupTime = 2.0f; 


    [Header("Parametros de Destruccion")]
    public float baseExplosionForce = 800f; 
    public float maxAngle = 45.0f;          
    public ForceMode forceMode = ForceMode.Impulse;

    [Header("Control de Fisica Post-Destruccion")]
    public float physicsSimulationTime = 3.0f; 
    public float cleanupTime = 0.0f;          


    
    public void Explode(Vector3 impactPoint, Vector3 impactDirection)
    {
        
        if (fracturedWallPrefab == null)
        {

            Destroy(gameObject);
            return;
        }

        
        
        GameObject brokenWall = Instantiate(fracturedWallPrefab, Vector3.zero, Quaternion.identity);

        
        brokenWall.transform.position = transform.position;
        brokenWall.transform.rotation = transform.rotation;

        
        if (dustExplosionVFXPrefab != null)
        {
            
            GameObject dustVFXInstance = Instantiate(dustExplosionVFXPrefab, impactPoint, Quaternion.identity);
            StartCoroutine(CleanupVFX(dustVFXInstance));
        }

        
        if (!brokenWall.activeSelf)
        {
            brokenWall.SetActive(true);
        }

        
        StartCoroutine(SimulateAndFreeze(brokenWall.transform, impactPoint, impactDirection));

        
        Destroy(gameObject);
    }

    private IEnumerator SimulateAndFreeze(Transform parent, Vector3 impactPoint, Vector3 impactDirection)
    {
        

        
        Vector3 directionCentral = impactDirection.normalized;

        Rigidbody[] fragments = parent.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in fragments)
        {
            
            rb.isKinematic = false;
            rb.useGravity = true;

            
            float angleDeviation = maxAngle / 2f;
            float angleX = Random.Range(-angleDeviation, angleDeviation);
            float angleY = Random.Range(-angleDeviation, angleDeviation);

            
            Quaternion rotation = Quaternion.AngleAxis(angleX, Vector3.right) * Quaternion.AngleAxis(angleY, Vector3.up);
            Vector3 directionPush = rotation * directionCentral;


            
            float forceMagnitude = baseExplosionForce * rb.mass;
            Vector3 forceVector = directionPush.normalized * forceMagnitude;

            
            rb.AddForceAtPosition(forceVector, impactPoint, forceMode);
            rb.AddTorque(Random.insideUnitSphere * forceMagnitude * 0.01f, forceMode);
        }

        
        yield return new WaitForSeconds(physicsSimulationTime);

        

        foreach (Rigidbody rb in fragments)
        {
            
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        
        if (cleanupTime > 0)
        {
            yield return new WaitForSeconds(cleanupTime);
            Destroy(parent.gameObject);
        }
    }

    
    
    
    
    private IEnumerator CleanupVFX(GameObject vfxObject)
    {
        
        yield return new WaitForSeconds(VfxCleanupTime);

        
        if (vfxObject != null)
        {
            Destroy(vfxObject);
        }
    }
}
