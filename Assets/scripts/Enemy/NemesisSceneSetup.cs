using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AI;
#endif

public class NemesisSceneSetup : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("The DaVinciP1 scene GameObject")]
    public GameObject sceneRoot;
    
    [Header("Nemesis Configuration")]
    [Tooltip("Nemesis enemy prefab or GameObject")]
    public GameObject nemesisPrefab;
    
    [Tooltip("Spawn position for the nemesis in DaVinciP1 scene")]
    public Vector3 nemesisSpawnPosition = new Vector3(0, 0, 0);
    
    [Tooltip("Should the nemesis be automatically spawned?")]
    public bool autoSpawnNemesis = true;
    
    [Header("Player References")]
    [Tooltip("Player1 GameObject (will be found automatically if null)")]
    public GameObject player1;
    
    [Tooltip("Player2 GameObject (will be found automatically if null)")]
    public GameObject player2;
    
    [Header("NPC References")]
    [Tooltip("NPC GameObject (will be found automatically if null)")]
    public GameObject npc;
    
    [Header("Navigation")]
    [Tooltip("Bake NavMesh automatically if not present")]
    public bool autoBakeNavMesh = true;
    
    [Tooltip("NavMesh surface (will be found automatically if null)")]
    public GameObject navMeshSurface;
    
    [Header("Audio Setup")]
    [Tooltip("Assign audio clips for the nemesis")]
    public AudioClip[] attackSounds;
    public AudioClip[] detectionSounds;
    public AudioClip[] footstepSounds;
    public AudioClip[] roarSounds;
    
    [Header("Behavior Settings")]
    [Tooltip("Allow Nemesis to wander when patrolling")]
    public bool enableWander = true;
    [Tooltip("Play roar animation when detecting target")]
    public bool enableRoar = true;
    [Tooltip("Force walk animation even when chasing")]
    public bool forceWalkOnly = true;
    
    [Header("Performance")]
    [Tooltip("Update interval for AI (lower = more responsive but more CPU intensive)")]
    public float aiUpdateInterval = 0.1f;
    
    public NemesisAI_Enhanced nemesisAI;
    public NemesisSoundDetector soundDetector;
    public GameObject spawnedNemesis;
    
    void Start()
    {
        SetupScene();
    }
    
    [ContextMenu("Setup Scene")]
    public void SetupScene()
    {

        
        
        FindReferences();
        
        
        SetupNavigation();
        
        
        if (autoSpawnNemesis)
        {
            SpawnNemesis();
        }
        
        
        ConfigureNemesis();
        
        
        SetupAudio();
        
        
        ValidateSetup();
        

    }
    
    void FindReferences()
    {
        
        if (sceneRoot == null)
        {
            sceneRoot = GameObject.Find("DaVinciP1");
            if (sceneRoot == null)
            {

                sceneRoot = gameObject;
            }
        }
        
        
        if (player1 == null)
        {
            player1 = GameObject.FindGameObjectWithTag("Player1");
            if (player1 == null)
            {

            }
        }
        
        if (player2 == null)
        {
            player2 = GameObject.FindGameObjectWithTag("Player2");
            if (player2 == null)
            {

            }
        }
        
        
        if (npc == null)
        {
            npc = GameObject.FindGameObjectWithTag("NPC");
            if (npc == null)
            {

            }
        }
        
        
        if (navMeshSurface == null)
        {
            
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.GetComponent<NavMeshAgent>() != null || obj.name.ToLower().Contains("navmesh"))
                {
                    navMeshSurface = obj;

                    break;
                }
            }
            
            if (navMeshSurface == null)
            {

            }
        }
    }
    
    void SetupNavigation()
    {
        
        UnityEngine.AI.NavMeshHit hit;
        bool hasNavMesh = UnityEngine.AI.NavMesh.SamplePosition(Vector3.zero, out hit, 1.0f, UnityEngine.AI.NavMesh.AllAreas);
        
        if (hasNavMesh)
        {

        }
        else if (autoBakeNavMesh)
        {


        }
        else
        {

        }
    }
    
    void SpawnNemesis()
    {
        if (nemesisPrefab != null)
        {
            
            spawnedNemesis = Instantiate(nemesisPrefab, nemesisSpawnPosition, Quaternion.identity);
            spawnedNemesis.name = "Nemesis_DaVinciP1";

        }
        else
        {
            
            spawnedNemesis = new GameObject("Nemesis_DaVinciP1");
            spawnedNemesis.transform.position = nemesisSpawnPosition;
            
            
            AddRequiredComponents(spawnedNemesis);
            

        }
        
        
        if (sceneRoot != null)
        {
            spawnedNemesis.transform.SetParent(sceneRoot.transform, true);
        }
    }
    
    void AddRequiredComponents(GameObject nemesis)
    {
        
        NavMeshAgent agent = nemesis.AddComponent<NavMeshAgent>();
        ConfigureNavMeshAgent(agent);
        
        
        Animator animator = nemesis.AddComponent<Animator>();
        ConfigureAnimator(animator);
        
        
        AudioSource audioSource = nemesis.AddComponent<AudioSource>();
        ConfigureAudioSource(audioSource);
        
        
        nemesis.AddComponent<NemesisAI_Enhanced>();
        nemesis.AddComponent<NemesisSoundDetector>();
    }
    
    void ConfigureNavMeshAgent(NavMeshAgent agent)
    {
        agent.speed = 3.5f;
        agent.angularSpeed = 360f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 2.3f;
        agent.autoBraking = true;
        agent.updateRotation = true;
        agent.radius = 0.5f;
        agent.height = 2f;
    }
    
    void ConfigureAnimator(Animator animator)
    {
        
        RuntimeAnimatorController horrorController = Resources.Load<RuntimeAnimatorController>("Horror");
        if (horrorController != null)
        {
            animator.runtimeAnimatorController = horrorController;

        }
        else
        {

        }
    }
    
    void ConfigureAudioSource(AudioSource audioSource)
    {
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; 
        audioSource.volume = 0.8f;
        audioSource.pitch = 1f;
        audioSource.maxDistance = 50f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
    }
    
    void ConfigureNemesis()
    {
        if (spawnedNemesis == null)
        {

            return;
        }
        
        
        nemesisAI = spawnedNemesis.GetComponent<NemesisAI_Enhanced>();
        if (nemesisAI != null)
        {
            ConfigureNemesisAI(nemesisAI);
        }
        else
        {
            
            NemesisAI basicAI = spawnedNemesis.GetComponent<NemesisAI>();
            if (basicAI != null)
            {
                ConfigureBasicNemesisAI(basicAI);
            }
        }
        
        
        soundDetector = spawnedNemesis.GetComponent<NemesisSoundDetector>();
        if (soundDetector != null)
        {
            ConfigureSoundDetector(soundDetector);
        }
        else
        {

        }
    }
    
    void ConfigureNemesisAI(NemesisAI_Enhanced ai)
    {
        
        ai.walkSpeed = 3.5f;
        ai.chaseSpeed = 5.5f;
        ai.sprintSpeed = 7f;
        ai.rotationSpeed = 10f;
        
        
        ai.visualDetectionRadius = 25f;
        ai.soundDetectionRadius = 35f;
        ai.attackRange = 2.5f;
        
        
        ai.searchDuration = 8f;
        ai.memoryDuration = 15f;
        ai.targetSwitchDelay = 2f;
        
        
        ai.attackCooldown = 1.2f;
        ai.attackDamage = 30;
        ai.attackDuration = 0.9f;
        
        
        ai.npcPriority = 3f;
        ai.playerPriority = 2f;
        ai.soundPriority = 1f;
        
        ai.enableWander = enableWander;
        ai.enableRoarOnDetection = enableRoar;
        ai.onlyWalkAnimation = forceWalkOnly;
        
        
        if (spawnedNemesis != null)
        {
            NemesisDetectionHelper detectionHelper = ai.GetComponent<NemesisDetectionHelper>();
            if (detectionHelper != null)
            {
                detectionHelper.enableCollisions = true;
                detectionHelper.collisionRadius = 0.5f;
                detectionHelper.collisionForce = 10f;
                detectionHelper.showDebugRays = true;
                
                
                detectionHelper.obstacleLayerMask = LayerMask.GetMask("Default", "Walls", "Obstacles");
                detectionHelper.targetLayerMask = LayerMask.GetMask("Default", "Player", "NPC");
            }
        }
        
        
        ai.attackSounds = attackSounds;
        ai.detectionSounds = detectionSounds;
        ai.footstepSounds = footstepSounds;
        ai.roarSounds = roarSounds;
        


    }
    
    void ConfigureSoundDetector(NemesisSoundDetector detector)
    {
        detector.maxHearingDistance = 35f;
        detector.soundAttenuationPerWall = 0.7f;
        detector.soundBlockerLayer = LayerMask.GetMask("Default", "Walls");
        detector.showDebugGizmos = false;
        

    }
    
    void SetupAudio()
    {
        

    }
    
    void ValidateSetup()
    {
        bool setupValid = true;
        
        
        if (spawnedNemesis == null)
        {

            setupValid = false;
        }
        else
        {
            if (nemesisAI == null)
            {

                setupValid = false;
            }
            
            if (soundDetector == null)
            {

                setupValid = false;
            }
            
            if (spawnedNemesis.GetComponent<NavMeshAgent>() == null)
            {

                setupValid = false;
            }
            
            if (spawnedNemesis.GetComponent<Animator>() == null)
            {

                setupValid = false;
            }
        }
        
        
        if (player1 == null)
        {

        }
        
        if (player2 == null)
        {

        }
        
        if (npc == null)
        {

        }
        
        
        if (navMeshSurface == null)
        {

        }
        
        if (setupValid)
        {



        }
        else
        {

        }
    }
    
    [ContextMenu("Test Nemesis Detection")]
    public void TestDetection()
    {
        if (nemesisAI != null)
        {
            
            Vector3 testPosition = Vector3.zero;
            
            if (player1 != null)
                testPosition = player1.transform.position;
            else if (player2 != null)
                testPosition = player2.transform.position;
            else if (npc != null)
                testPosition = npc.transform.position;
            
            nemesisAI.ForceAlert(testPosition);

        }
        else
        {

        }
    }
    
    [ContextMenu("Reset Nemesis Position")]
    public void ResetNemesisPosition()
    {
        if (spawnedNemesis != null)
        {
            spawnedNemesis.transform.position = nemesisSpawnPosition;
            spawnedNemesis.transform.rotation = Quaternion.identity;

        }
    }
    
    void OnDrawGizmosSelected()
    {
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(nemesisSpawnPosition, 1f);
        Gizmos.DrawLine(nemesisSpawnPosition, nemesisSpawnPosition + Vector3.up * 2f);
        
        
        if (sceneRoot != null)
        {
            Gizmos.color = Color.blue;
            Bounds bounds = CalculateSceneBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
    
    Bounds CalculateSceneBounds()
    {
        Bounds bounds = new Bounds(sceneRoot.transform.position, Vector3.one);
        
        
        Renderer[] renderers = sceneRoot.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        
        return bounds;
    }

    void ConfigureBasicNemesisAI(NemesisAI ai)
    {
        ai.walkSpeed = 3.5f;
        ai.chaseSpeed = 5f;
        ai.rotationSpeed = 8f;
        ai.detectionRadius = 25f;
        ai.attackRange = 2.5f;
        ai.soundDetectionRadius = 30f;
        ai.attackCooldown = 1.5f;
        ai.attackDamage = 25;
        ai.attackDuration = 0.8f;
        ai.npcPriority = 2f;
        ai.playerPriority = 1f;
        
        ai.attackSounds = attackSounds;
        ai.detectionSounds = detectionSounds;
        ai.footstepSounds = footstepSounds;
        
        NemesisDetectionHelper detectionHelper = ai.GetComponent<NemesisDetectionHelper>();
        if (detectionHelper != null)
        {
            detectionHelper.enableCollisions = true;
            detectionHelper.collisionRadius = 0.5f;
            detectionHelper.collisionForce = 10f;
            detectionHelper.showDebugRays = true;
            
            detectionHelper.obstacleLayerMask = LayerMask.GetMask("Default", "Walls", "Obstacles");
            detectionHelper.targetLayerMask = LayerMask.GetMask("Default", "Player", "NPC");
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(NemesisSceneSetup))]
public class NemesisSceneSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        NemesisSceneSetup setup = (NemesisSceneSetup)target;
        
        GUILayout.Space(10);
        GUILayout.Label("Scene Setup Tools", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Setup Scene"))
        {
            setup.SetupScene();
        }
        
        if (GUILayout.Button("Test Nemesis Detection"))
        {
            setup.TestDetection();
        }
        
        if (GUILayout.Button("Reset Nemesis Position"))
        {
            setup.ResetNemesisPosition();
        }
        
        GUILayout.Space(10);
        GUILayout.Label("Validation Status", EditorStyles.boldLabel);
        
        
        bool hasNemesis = setup.spawnedNemesis != null;
        bool hasAI = setup.nemesisAI != null;
        bool hasSoundDetector = setup.soundDetector != null;
        bool hasPlayer1 = setup.player1 != null;
        bool hasPlayer2 = setup.player2 != null;
        bool hasNPC = setup.npc != null;
        
        EditorGUILayout.LabelField("Nemesis Spawned:", hasNemesis ? "✅ Yes" : "❌ No");
        EditorGUILayout.LabelField("AI Component:", hasAI ? "✅ Present" : "❌ Missing");
        EditorGUILayout.LabelField("Sound Detector:", hasSoundDetector ? "✅ Present" : "❌ Missing");
        EditorGUILayout.LabelField("Player1 Found:", hasPlayer1 ? "✅ Yes" : "❌ No");
        EditorGUILayout.LabelField("Player2 Found:", hasPlayer2 ? "✅ Yes" : "❌ No");
        EditorGUILayout.LabelField("NPC Found:", hasNPC ? "✅ Yes" : "❌ No");
    }
    

}
#endif
