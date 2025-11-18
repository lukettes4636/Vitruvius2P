using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class PuertaDobleAccion : MonoBehaviour
{
    [Header("Referencias de Puertas")]
    [SerializeField] private Transform puertaA;
    [SerializeField] private Transform puertaB;

    [Header("Opening Configuration")]
    [SerializeField] private int golpesNecesarios = 3;
    [SerializeField] private float velocidadApertura = 90f;
    [SerializeField] private Vector3 ejeRotacion = Vector3.up;

    [Header("Door Shake Effects")]
    [SerializeField] private float intensidadTemblor = 0.05f;
    [SerializeField] private float duracionTemblor = 0.3f;

    [Header("Camera Shake")]
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeMagnitude = 0.1f;

    [Header("Joystick Vibration (Rumble)")]
    [Range(0f, 1f)][SerializeField] private float lowFrequency = 0.8f;
    [Range(0f, 1f)][SerializeField] private float highFrequency = 0.3f;
    [SerializeField] private float rumbleDuration = 0.25f;

    [Header("Cooperation Time")]
    [SerializeField] private float ventanaDeTiempo = 1.0f;

    [Header("Modo de Apertura")]
    [SerializeField] private bool modoIndividual = false;

    [Header("Notification Feedback")]
    [SerializeField] private string needHelpMessage = "I need help with this door!";
    [SerializeField] private string readyToActMessage = "Let's go, together now!";

    [Header("Audio")]
    [SerializeField] private bool playDoorSounds = true;

    
    [Header("Outline Multiplayer")]
    [Tooltip("The color used when two or more players are in the trigger.")]
    [SerializeField] private Color cooperativeOutlineColor = Color.yellow;
    [SerializeField] private string outlineColorProperty = "_Outline_Color";
    [SerializeField] private string outlineScaleProperty = "_Outline_Scale";
    [SerializeField] private float activeOutlineScale = 0.0125f;

    private List<PlayerIdentifier> activePlayers = new List<PlayerIdentifier>();
    private List<Renderer> doorRenderers = new List<Renderer>();
    private MaterialPropertyBlock propertyBlock;
    private int outlineColorID;
    private int outlineScaleID;
    private Color originalOutlineColor = Color.black;

    
    private int golpesActuales = 0;
    private bool estaAbierta = false;
    private float anguloObjetivo = 0f;
    private float anguloActual = 0f;
    private bool isCoopMessageShown = false;

    private HashSet<GameObject> jugadoresEnTrigger = new HashSet<GameObject>();
    private Dictionary<GameObject, float> tiempoUltimoGolpe = new Dictionary<GameObject, float>();
    private Vector3 posicionOriginal;

    
    
    
    [Header("UI Prompt Settings")]
    [SerializeField] private Canvas promptCanvas;
    [SerializeField] private TextMeshProUGUI promptText;
    

    void Start()
    {
        if (puertaA == null || puertaB == null)
        {

            this.enabled = false;
            return;
        }

        posicionOriginal = transform.position;
        anguloObjetivo = anguloActual;

        
        if (puertaA != null)
        {
            Renderer rendererA = puertaA.GetComponent<Renderer>();
            if (rendererA != null) doorRenderers.Add(rendererA);
        }
        if (puertaB != null)
        {
            Renderer rendererB = puertaB.GetComponent<Renderer>();
            if (rendererB != null) doorRenderers.Add(rendererB);
        }

        if (doorRenderers.Count > 0)
        {
            propertyBlock = new MaterialPropertyBlock();
            outlineColorID = Shader.PropertyToID(outlineColorProperty);
            outlineScaleID = Shader.PropertyToID(outlineScaleProperty);
            SetOutlineState(Color.black, 0.0f);
        }

        
        if (promptCanvas != null)
        {
            promptCanvas.enabled = false;
        }
    }

    void Update()
    {
        if (estaAbierta)
        {
            anguloActual = Mathf.MoveTowards(anguloActual, anguloObjetivo, velocidadApertura * Time.deltaTime);

            puertaA.localRotation = Quaternion.AngleAxis(anguloActual, ejeRotacion);
            puertaB.localRotation = Quaternion.AngleAxis(-anguloActual, ejeRotacion);

            if (anguloActual == anguloObjetivo)
                this.enabled = false;
        }
    }

    
    private void SetOutlineState(Color color, float scale)
    {
        if (propertyBlock == null) return;

        foreach (Renderer rend in doorRenderers)
        {
            if (rend == null || rend.sharedMaterials.Length < 2) continue;
            rend.GetPropertyBlock(propertyBlock, 1);
            propertyBlock.SetColor(outlineColorID, color);
            propertyBlock.SetFloat(outlineScaleID, scale);
            rend.SetPropertyBlock(propertyBlock, 1);
        }
    }

    private void UpdateOutlineVisuals()
    {
        if (estaAbierta) return;
        if (activePlayers.Count == 0)
        {
            SetOutlineState(originalOutlineColor, 0.0f);
        }
        else if (activePlayers.Count == 1)
        {
            PlayerIdentifier singlePlayer = activePlayers[0];
            SetOutlineState(singlePlayer.PlayerOutlineColor, activeOutlineScale);
        }
        else
        {
            SetOutlineState(cooperativeOutlineColor, activeOutlineScale);
        }
    }

    private IEnumerator ShakeDoor()
    {
        float timer = 0f;
        while (timer < duracionTemblor)
        {
            float x = Random.Range(-1f, 1f) * intensidadTemblor;
            float z = Random.Range(-1f, 1f) * intensidadTemblor;

            transform.position = posicionOriginal + new Vector3(x, 0, z);
            timer += Time.deltaTime;
            yield return null;
        }
        transform.position = posicionOriginal;
    }

    public void IntentoDeAccion(GameObject jugador)
    {
        if (estaAbierta) return;
        if (!jugadoresEnTrigger.Contains(jugador)) return;

        float tiempoActual = Time.time;
        tiempoUltimoGolpe[jugador] = tiempoActual;

        if (modoIndividual)
        {
            GolpeExitosoIndividual(jugador);
            return;
        }

        if (jugadoresEnTrigger.Count < 2) return;

        GameObject otroJugador = null;
        foreach (GameObject p in jugadoresEnTrigger)
        {
            if (p != jugador)
            {
                otroJugador = p;
                break;
            }
        }

        if (otroJugador != null)
        {
            if (!tiempoUltimoGolpe.ContainsKey(otroJugador))
                tiempoUltimoGolpe[otroJugador] = float.MinValue;

            float tiempoOtro = tiempoUltimoGolpe[otroJugador];
            float diferenciaTiempo = Mathf.Abs(tiempoActual - tiempoOtro);

            if (diferenciaTiempo <= ventanaDeTiempo)
            {
                if (tiempoActual > tiempoOtro)
                {
                    GolpeExitoso(jugador, otroJugador);
                    tiempoUltimoGolpe[jugador] = float.MinValue;
                    tiempoUltimoGolpe[otroJugador] = float.MinValue;
                }
            }
        }
    }

    private void GolpeExitoso(GameObject playerA, GameObject playerB)
    {
        golpesActuales++;
        StartCoroutine(ShakeDoor());
        RequestCooperativeEffects(playerA);
        RequestCooperativeEffects(playerB);

        if (playDoorSounds && AudioManager.Instance != null)
        {
            var cfg = AudioManager.Instance.GetAudioConfig();
            if (cfg.doorOpenSounds.Length > 0)
                AudioManager.Instance.PlaySFX(cfg.doorOpenSounds[0], transform.position, 0.7f, 0.9f);
        }



        if (golpesActuales >= golpesNecesarios)
            AbrirPuerta();
    }

    private void GolpeExitosoIndividual(GameObject jugador)
    {
        golpesActuales++;
        StartCoroutine(ShakeDoor());
        RequestCooperativeEffects(jugador);

        if (playDoorSounds && AudioManager.Instance != null)
        {
            var cfg = AudioManager.Instance.GetAudioConfig();
            if (cfg.doorOpenSounds.Length > 0)
                AudioManager.Instance.PlaySFX(cfg.doorOpenSounds[0], transform.position, 0.7f, 0.9f);
        }



        if (golpesActuales >= golpesNecesarios)
            AbrirPuerta();
    }

    private void RequestCooperativeEffects(GameObject player)
    {
        if (player.GetComponent<MovJugador1>() is MovJugador1 mov1)
            mov1.StartCooperativeEffects(shakeDuration, shakeMagnitude, lowFrequency, highFrequency, rumbleDuration);
        else if (player.GetComponent<MovJugador2>() is MovJugador2 mov2)
            mov2.StartCooperativeEffects(shakeDuration, shakeMagnitude, lowFrequency, highFrequency, rumbleDuration);
    }

    private void AbrirPuerta()
    {
        estaAbierta = true;
        anguloObjetivo += 90f;
        SetOutlineState(Color.black, 0.0f);

        if (playDoorSounds && AudioManager.Instance != null)
        {
            var cfg = AudioManager.Instance.GetAudioConfig();
            if (cfg.doorOpenSounds.Length > 0)
                AudioManager.Instance.PlaySFX(cfg.doorOpenSounds[0], transform.position, 0.8f, 1f);
        }



        foreach (Collider col in GetComponents<Collider>())
            col.enabled = false;

        foreach (GameObject player in jugadoresEnTrigger)
        {
            if (player == null) continue;
            MovJugador1 mov1 = player.GetComponent<MovJugador1>();
            MovJugador2 mov2 = player.GetComponent<MovJugador2>();
            if (mov1 != null) mov1.ClearCurrentDoor(this);
            if (mov2 != null) mov2.ClearCurrentDoor(this);
        }

        jugadoresEnTrigger.Clear();
        if (promptCanvas != null) promptCanvas.enabled = false;
    }

    private PlayerUIController GetPlayerUIController(GameObject playerObject)
    {
        if (playerObject == null) return null;
        PlayerUIController ui = playerObject.GetComponent<PlayerUIController>();
        if (ui != null) return ui;
        return playerObject.GetComponentInParent<PlayerUIController>();
    }

    public void AddPlayer(GameObject player)
    {
        PlayerIdentifier playerIdentifier = player.GetComponent<PlayerIdentifier>();
        if (playerIdentifier != null)
        {
            if (!activePlayers.Contains(playerIdentifier))
                activePlayers.Add(playerIdentifier);
            UpdateOutlineVisuals();
        }

        if (estaAbierta) return;
        if (!jugadoresEnTrigger.Contains(player))
        {
            jugadoresEnTrigger.Add(player);
            tiempoUltimoGolpe[player] = float.MinValue;

            PlayerUIController uiController = GetPlayerUIController(player);

            if (jugadoresEnTrigger.Count == 1)
            {
                if (uiController != null)
                    uiController.ShowNotification(needHelpMessage);

                
                if (promptCanvas != null && promptText != null)
                {
                    promptCanvas.enabled = true;
                    promptText.text = "PRES (X) AT THE SAME TIME TO HIT THE DOOR";
                }

                isCoopMessageShown = false;
            }
            else if (jugadoresEnTrigger.Count >= 2 && !isCoopMessageShown)
            {
                if (uiController != null)
                    uiController.ShowNotification(readyToActMessage);

                foreach (GameObject otherPlayer in jugadoresEnTrigger)
                {
                    if (otherPlayer != player)
                    {
                        PlayerUIController otherUI = GetPlayerUIController(otherPlayer);
                        if (otherUI != null)
                            otherUI.ShowNotification(readyToActMessage);
                    }
                }
                isCoopMessageShown = true;
            }
        }
    }

    public void RemovePlayer(GameObject player)
    {
        PlayerIdentifier playerIdentifier = player.GetComponent<PlayerIdentifier>();
        if (playerIdentifier != null)
        {
            if (activePlayers.Contains(playerIdentifier))
                activePlayers.Remove(playerIdentifier);
            UpdateOutlineVisuals();
        }

        if (estaAbierta) return;
        if (jugadoresEnTrigger.Contains(player))
        {
            jugadoresEnTrigger.Remove(player);
            tiempoUltimoGolpe.Remove(player);

            if (jugadoresEnTrigger.Count < 2)
                isCoopMessageShown = false;

            if (jugadoresEnTrigger.Count == 1)
            {
                GameObject remainingPlayer = null;
                foreach (GameObject p in jugadoresEnTrigger) { remainingPlayer = p; break; }

                if (remainingPlayer != null)
                {
                    PlayerUIController remainingUI = GetPlayerUIController(remainingPlayer);
                    if (remainingUI != null)
                        remainingUI.ShowNotification(needHelpMessage);

                    if (promptCanvas != null && promptText != null)
                    {
                        promptCanvas.enabled = true;
                        promptText.text = "PRES (X) AT THE SAME TIME TO HIT THE DOOR";
                    }
                }
            }
            else if (jugadoresEnTrigger.Count == 0 && promptCanvas != null)
            {
                promptCanvas.enabled = false;
            }
        }
    }
}
