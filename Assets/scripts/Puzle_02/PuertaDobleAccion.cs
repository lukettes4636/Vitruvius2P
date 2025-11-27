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
    [SerializeField] private AudioClip hitSuccessSound;
    [SerializeField] private AudioClip hitErrorSound;
    [SerializeField] private AudioClip buttonPressSound;
    [SerializeField] private float hitSoundVolume = 0.8f;
    [SerializeField] private float errorSoundVolume = 0.6f;
    [SerializeField] private float buttonPressSoundVolume = 0.5f;

    [Header("Button Indicators")]
    [SerializeField] private GameObject buttonIndicatorPrefab;
    [SerializeField] private Vector3 player1ButtonOffset = new Vector3(-1f, 2f, 0f);
    [SerializeField] private Vector3 player2ButtonOffset = new Vector3(1f, 2f, 0f);
    [SerializeField] private float buttonAnimDuration = 0.3f;
    [SerializeField] private bool usePlayerColors = true;
    [SerializeField] private bool useBillboardEffect = true;

    [Header("Outline Multiplayer")]
    [Tooltip("The color used when two or more players are in the trigger.")]
    [SerializeField] private Color cooperativeOutlineColor = Color.yellow;
    [SerializeField] private string outlineColorProperty = "_Outline_Color";
    [SerializeField] private string outlineScaleProperty = "_Outline_Scale";
    [SerializeField] private float activeOutlineScale = 0.0125f;

    [Header("Shader FX - Cooperative Stress")]
    [Tooltip("Arrastra aqu el Material creado con tu SG_PuzzleStress (Mat_Puzzle)")]
    [SerializeField] private Material puzzleFullscreenMat;
    [SerializeField] private float stressDecaySpeed = 0.8f;
    [SerializeField] private float stressAddedPerHit = 0.4f;
    [SerializeField] private float baseTension = 0.15f;

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

    private float currentStress = 0f;
    private int _stressLevelID;

    private HashSet<GameObject> jugadoresEnTrigger = new HashSet<GameObject>();
    private Dictionary<GameObject, float> tiempoUltimoGolpe = new Dictionary<GameObject, float>();
    private Vector3 posicionOriginal;

    
    private GameObject buttonIndicator1;
    private GameObject buttonIndicator2;
    private Dictionary<GameObject, GameObject> playerButtonMap = new Dictionary<GameObject, GameObject>();

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

        if (puzzleFullscreenMat != null)
        {
            _stressLevelID = Shader.PropertyToID("_StressLevel");
            puzzleFullscreenMat.SetFloat(_stressLevelID, 0f);
        }

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

        
        if (buttonIndicatorPrefab != null)
        {
            buttonIndicator1 = Instantiate(buttonIndicatorPrefab, transform.position + player1ButtonOffset, Quaternion.identity, transform);
            buttonIndicator1.SetActive(false);

            
            if (useBillboardEffect)
            {
                AddBillboardEffect(buttonIndicator1);
            }

            buttonIndicator2 = Instantiate(buttonIndicatorPrefab, transform.position + player2ButtonOffset, Quaternion.identity, transform);
            buttonIndicator2.SetActive(false);

            if (useBillboardEffect)
            {
                AddBillboardEffect(buttonIndicator2);
            }
        }
    }

    void Update()
    {
        if (estaAbierta)
        {
            anguloActual = Mathf.MoveTowards(anguloActual, anguloObjetivo, velocidadApertura * Time.deltaTime);

            puertaA.localRotation = Quaternion.AngleAxis(anguloActual, ejeRotacion);
            puertaB.localRotation = Quaternion.AngleAxis(-anguloActual, ejeRotacion);

            if (puzzleFullscreenMat != null && currentStress > 0)
            {
                currentStress = Mathf.MoveTowards(currentStress, 0f, Time.deltaTime * 2f);
                puzzleFullscreenMat.SetFloat(_stressLevelID, currentStress);
            }

            if (anguloActual == anguloObjetivo && currentStress <= 0)
                this.enabled = false;
        }
        else
        {
            HandleShaderUpdate();
        }
    }

    private void HandleShaderUpdate()
    {
        if (puzzleFullscreenMat == null) return;

        float targetBase = (jugadoresEnTrigger.Count >= 2) ? baseTension : 0f;

        if (currentStress > targetBase)
        {
            currentStress -= Time.deltaTime * stressDecaySpeed;
        }
        else if (currentStress < targetBase)
        {
            currentStress = Mathf.MoveTowards(currentStress, targetBase, Time.deltaTime);
        }

        puzzleFullscreenMat.SetFloat(_stressLevelID, currentStress);
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

    private void AddBillboardEffect(GameObject buttonObject)
    {
        if (buttonObject == null) return;

        
        BillboardEffect billboard = buttonObject.GetComponent<BillboardEffect>();
        if (billboard == null)
        {
            billboard = buttonObject.AddComponent<BillboardEffect>();
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

    private IEnumerator AnimateButtonPress(GameObject buttonIndicator)
    {
        if (buttonIndicator == null) yield break;

        
        ButtonIndicatorController controller = buttonIndicator.GetComponent<ButtonIndicatorController>();
        if (controller != null)
        {
            controller.TriggerPress();
        }

        
        yield return new WaitForSeconds(buttonAnimDuration);
    }

    private void PlayButtonPressAudio()
    {
        if (buttonPressSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(buttonPressSound, transform.position, buttonPressSoundVolume, 1f);
        }
    }

    private void PlayHitSuccessAudio()
    {
        if (hitSuccessSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(hitSuccessSound, transform.position, hitSoundVolume, 1f);
        }
    }

    private void PlayErrorAudio()
    {
        if (hitErrorSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(hitErrorSound, transform.position, errorSoundVolume, 1f);
        }
    }

    private void SetButtonPlayerColor(GameObject buttonIndicator, PlayerIdentifier playerIdentifier)
    {
        if (!usePlayerColors || buttonIndicator == null || playerIdentifier == null) return;

        ButtonIndicatorController controller = buttonIndicator.GetComponent<ButtonIndicatorController>();
        if (controller != null)
        {
            controller.SetPlayerColor(playerIdentifier.PlayerOutlineColor);
        }
    }

    public void IntentoDeAccion(GameObject jugador)
    {
        if (estaAbierta) return;
        if (!jugadoresEnTrigger.Contains(jugador)) return;

        float tiempoActual = Time.time;
        tiempoUltimoGolpe[jugador] = tiempoActual;

        
        if (playerButtonMap.ContainsKey(jugador))
        {
            StartCoroutine(AnimateButtonPress(playerButtonMap[jugador]));
            PlayButtonPressAudio();
        }

        if (modoIndividual)
        {
            GolpeExitosoIndividual(jugador);
            return;
        }

        if (jugadoresEnTrigger.Count < 2)
        {
            
            PlayErrorAudio();
            return;
        }

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
            else
            {
                
                PlayErrorAudio();
            }
        }
    }

    private void GolpeExitoso(GameObject playerA, GameObject playerB)
    {
        golpesActuales++;

        currentStress = Mathf.Clamp01(currentStress + stressAddedPerHit);

        StartCoroutine(ShakeDoor());
        RequestCooperativeEffects(playerA);
        RequestCooperativeEffects(playerB);

        
        PlayHitSuccessAudio();

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

        currentStress = Mathf.Clamp01(currentStress + stressAddedPerHit);

        StartCoroutine(ShakeDoor());
        RequestCooperativeEffects(jugador);

        
        PlayHitSuccessAudio();

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

        
        if (buttonIndicator1 != null) buttonIndicator1.SetActive(false);
        if (buttonIndicator2 != null) buttonIndicator2.SetActive(false);
        playerButtonMap.Clear();
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

            
            if (buttonIndicatorPrefab != null)
            {
                if (jugadoresEnTrigger.Count == 1)
                {
                    buttonIndicator1.SetActive(true);
                    playerButtonMap[player] = buttonIndicator1;

                    
                    if (playerIdentifier != null)
                    {
                        SetButtonPlayerColor(buttonIndicator1, playerIdentifier);
                    }
                }
                else if (jugadoresEnTrigger.Count == 2)
                {
                    buttonIndicator2.SetActive(true);

                    
                    playerButtonMap[player] = buttonIndicator2;

                    
                    if (playerIdentifier != null)
                    {
                        SetButtonPlayerColor(buttonIndicator2, playerIdentifier);
                    }
                }
            }

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
            
            if (playerButtonMap.ContainsKey(player))
            {
                GameObject buttonToHide = playerButtonMap[player];
                if (buttonToHide != null) buttonToHide.SetActive(false);
                playerButtonMap.Remove(player);
            }

            jugadoresEnTrigger.Remove(player);
            tiempoUltimoGolpe.Remove(player);

            if (jugadoresEnTrigger.Count < 2)
            {
                isCoopMessageShown = false;
            }

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

    void OnDestroy()
    {
        
        if (buttonIndicator1 != null) Destroy(buttonIndicator1);
        if (buttonIndicator2 != null) Destroy(buttonIndicator2);
    }
}