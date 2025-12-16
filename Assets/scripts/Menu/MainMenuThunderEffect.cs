using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MainMenuThunderEffect : MonoBehaviour
{
    [Header("Visual Settings")]
    [Tooltip("La luz que simulara el relampago.")]
    [SerializeField] private Light lightningLight;
    
    [Tooltip("Intensidad maxima del destello.")]
    [SerializeField] private float maxIntensity = 2f;
    
    [Tooltip("Color del relampago (generalmente blanco azulado).")]
    [SerializeField] private Color lightningColor = new Color(0.8f, 0.9f, 1f);

    [Header("Audio Settings")]
    [Tooltip("Fuente de audio para emitir el sonido del trueno.")]
    [SerializeField] private AudioSource thunderAudioSource;
    
    [Tooltip("Lista de sonidos de truenos para variar.")]
    [SerializeField] private List<AudioClip> thunderClips;

    [Header("Timing Settings")]
    [Tooltip("Tiempo minimo y maximo entre truenos (en segundos).")]
    [SerializeField] private Vector2 intervalRange = new Vector2(5f, 15f);
    
    [Tooltip("Duracion del efecto visual de parpadeo.")]
    [SerializeField] private float flashDuration = 0.5f;

    private float originalIntensity;
    private Color originalColor;

    private void Start()
    {
        if (lightningLight != null)
        {
            originalIntensity = lightningLight.intensity;
            originalColor = lightningLight.color;
            
            lightningLight.intensity = 0; 
        }

        StartCoroutine(ThunderLoop());
    }

    private IEnumerator ThunderLoop()
    {
        while (true)
        {
            
            float waitTime = Random.Range(intervalRange.x, intervalRange.y);
            yield return new WaitForSeconds(waitTime);

            
            StartCoroutine(DoFlash());
            PlayThunderSound();
        }
    }

    private IEnumerator DoFlash()
    {
        if (lightningLight == null) yield break;

        lightningLight.color = lightningColor;
        
        
        lightningLight.intensity = maxIntensity;
        
        yield return new WaitForSeconds(0.05f);

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            
            float currentIntensity = Random.Range(0f, maxIntensity);
            lightningLight.intensity = currentIntensity;
            
            
            yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
            elapsed += 0.1f; 
        }

        
        lightningLight.intensity = 0;
        lightningLight.color = originalColor;
    }

    private void PlayThunderSound()
    {
        if (thunderAudioSource != null && thunderClips != null && thunderClips.Count > 0)
        {
            AudioClip clip = thunderClips[Random.Range(0, thunderClips.Count)];
            thunderAudioSource.PlayOneShot(clip);
        }
    }
}
