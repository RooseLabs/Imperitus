using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Comprehensive debugger for Persistent Managers object.
/// Attach this to your Persistent Managers GameObject to track when and why it becomes inactive.
/// </summary>
public class PersistentManagersDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool logChildrenDetails = true;
    [SerializeField] private bool logComponentDetails = true;
    [SerializeField] private bool continuousMonitoring = true;
    [SerializeField] private float monitoringInterval = 0.1f;

    private bool wasActive = true;
    private int frameCount = 0;
    private Dictionary<string, bool> childStates = new Dictionary<string, bool>();

    void Awake()
    {
        LogHeader("AWAKE");
        LogObjectState("Awake");
        LogSceneInfo();

        if (logChildrenDetails)
        {
            LogAllChildren(transform);
            StoreChildStates();
        }

        // Apply DontDestroyOnLoad if not already in DontDestroyOnLoad scene
        if (gameObject.scene.name != "DontDestroyOnLoad")
        {
            DontDestroyOnLoad(gameObject);
            Debug.Log($"<color=green>[PMDebug] DontDestroyOnLoad applied to {gameObject.name}</color>");
        }
        else
        {
            Debug.Log($"<color=yellow>[PMDebug] Already in DontDestroyOnLoad scene</color>");
        }

        LogFooter();
    }

    void OnEnable()
    {
        LogHeader("OnEnable");
        Debug.Log($"<color=green>[PMDebug] OnEnable called - Frame: {Time.frameCount}</color>");
        LogObjectState("OnEnable");
        LogFooter();

        // Subscribe to scene events
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDisable()
    {
        LogHeader("OnDisable - CRITICAL EVENT");
        Debug.LogError($"[PMDebug] ⚠️ OnDisable called - Frame: {Time.frameCount}");
        Debug.LogError($"[PMDebug] This is likely when the object is being deactivated!");

        LogObjectState("OnDisable");

        // Log stack trace to see what called this
        Debug.LogError($"<color=red>[PMDebug] STACK TRACE:</color>\n{System.Environment.StackTrace}");

        LogFooter();

        // Unsubscribe from scene events
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    void Start()
    {
        LogHeader("START");
        LogObjectState("Start");

        if (continuousMonitoring)
        {
            InvokeRepeating(nameof(MonitorState), monitoringInterval, monitoringInterval);
            Debug.Log($"<color=cyan>[PMDebug] Continuous monitoring enabled (every {monitoringInterval}s)</color>");
        }

        LogFooter();
    }

    void Update()
    {
        frameCount++;

        // Check if active state changed
        if (gameObject.activeSelf != wasActive)
        {
            LogHeader("STATE CHANGE DETECTED IN UPDATE");
            Debug.LogError($"[PMDebug] ⚠️⚠️⚠️ ACTIVE STATE CHANGED! Frame: {frameCount}");
            Debug.LogError($"[PMDebug] Was: {wasActive} → Now: {gameObject.activeSelf}");
            LogObjectState("StateChange");
            LogFooter();

            wasActive = gameObject.activeSelf;
        }

        // Check children state changes
        if (logChildrenDetails)
        {
            CheckChildStateChanges();
        }
    }

    void OnDestroy()
    {
        LogHeader("OnDestroy - CRITICAL EVENT");
        Debug.LogError($"[PMDebug] ⚠️ OnDestroy called - Frame: {Time.frameCount}");
        Debug.LogError($"[PMDebug] STACK TRACE:\n{System.Environment.StackTrace}");
        LogFooter();
    }

    void MonitorState()
    {
        if (!gameObject.activeSelf)
        {
            Debug.LogError($"<color=red>[PMDebug] ⚠️ INACTIVE DETECTED in MonitorState! Frame: {frameCount}</color>");
            LogObjectState("MonitorCheck");

            // Try to find what's wrong
            DiagnoseInactiveState();
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LogHeader($"Scene Loaded: {scene.name}");
        Debug.Log($"[PMDebug] Scene: {scene.name}, Mode: {mode}, Frame: {frameCount}");
        LogObjectState("SceneLoaded");
        LogAllLoadedScenes();
        LogFooter();
    }

    void OnSceneUnloaded(Scene scene)
    {
        LogHeader($"Scene Unloaded: {scene.name}");
        Debug.Log($"[PMDebug] Scene: {scene.name}, Frame: {frameCount}");
        LogObjectState("SceneUnloaded");
        LogFooter();
    }

    // ===== LOGGING METHODS =====

    void LogHeader(string title)
    {
        Debug.Log($"\n<color=cyan>{'═',60}</color>");
        Debug.Log($"<color=cyan>[PMDebug] {title} - Frame: {frameCount}</color>");
        Debug.Log($"<color=cyan>{'═',60}</color>");
    }

    void LogFooter()
    {
        Debug.Log($"<color=cyan>{'═',60}\n</color>");
    }

    void LogObjectState(string context)
    {
        Debug.Log($"[PMDebug][{context}] GameObject: {gameObject.name}");
        Debug.Log($"[PMDebug][{context}] activeSelf: {gameObject.activeSelf}");
        Debug.Log($"[PMDebug][{context}] activeInHierarchy: {gameObject.activeInHierarchy}");
        Debug.Log($"[PMDebug][{context}] enabled (this script): {enabled}");
        Debug.Log($"[PMDebug][{context}] Scene: {gameObject.scene.name}");
        Debug.Log($"[PMDebug][{context}] Parent: {(transform.parent != null ? transform.parent.name : "NULL (Root)")}");
        Debug.Log($"[PMDebug][{context}] Layer: {LayerMask.LayerToName(gameObject.layer)}");
        Debug.Log($"[PMDebug][{context}] Tag: {gameObject.tag}");
        Debug.Log($"[PMDebug][{context}] InstanceID: {gameObject.GetInstanceID()}");

        if (logComponentDetails)
        {
            LogComponents();
        }
    }

    void LogComponents()
    {
        var components = GetComponents<Component>();
        Debug.Log($"[PMDebug] Components on this object ({components.Length}):");

        foreach (var comp in components)
        {
            if (comp == null)
            {
                Debug.LogError($"[PMDebug]   ⚠️ NULL/MISSING COMPONENT DETECTED!");
            }
            else
            {
                var behaviour = comp as Behaviour;
                if (behaviour != null)
                {
                    Debug.Log($"[PMDebug]   - {comp.GetType().Name} (enabled: {behaviour.enabled})");
                }
                else
                {
                    Debug.Log($"[PMDebug]   - {comp.GetType().Name}");
                }
            }
        }
    }

    void LogAllChildren(Transform parent, int depth = 0)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"[PMDebug] Children of {parent.name} ({parent.childCount}):");

        foreach (Transform child in parent)
        {
            Debug.Log($"[PMDebug] {indent}↳ {child.name} (Active: {child.gameObject.activeSelf})");

            if (logComponentDetails)
            {
                var components = child.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        Debug.LogError($"[PMDebug] {indent}  ⚠️ NULL COMPONENT on {child.name}!");
                    }
                    else if (!(comp is Transform))
                    {
                        var behaviour = comp as Behaviour;
                        if (behaviour != null)
                        {
                            Debug.Log($"[PMDebug] {indent}  - {comp.GetType().Name} (enabled: {behaviour.enabled})");
                        }
                        else
                        {
                            Debug.Log($"[PMDebug] {indent}  - {comp.GetType().Name}");
                        }
                    }
                }
            }

            // Recursively log nested children
            if (child.childCount > 0 && depth < 3) // Limit depth to avoid spam
            {
                LogAllChildren(child, depth + 1);
            }
        }
    }

    void LogSceneInfo()
    {
        Debug.Log($"[PMDebug] Current Scene Info:");
        Debug.Log($"[PMDebug]   - Scene Name: {gameObject.scene.name}");
        Debug.Log($"[PMDebug]   - Scene Path: {gameObject.scene.path}");
        Debug.Log($"[PMDebug]   - Scene BuildIndex: {gameObject.scene.buildIndex}");
        Debug.Log($"[PMDebug]   - Scene isLoaded: {gameObject.scene.isLoaded}");
        Debug.Log($"[PMDebug]   - Scene isDirty: {gameObject.scene.isDirty}");
        Debug.Log($"[PMDebug]   - Scene rootCount: {gameObject.scene.rootCount}");
    }

    void LogAllLoadedScenes()
    {
        Debug.Log($"[PMDebug] All Loaded Scenes ({SceneManager.sceneCount}):");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Debug.Log($"[PMDebug]   [{i}] {scene.name} (isLoaded: {scene.isLoaded}, rootObjects: {scene.rootCount})");
        }
        Debug.Log($"[PMDebug] Active Scene: {SceneManager.GetActiveScene().name}");
    }

    void StoreChildStates()
    {
        childStates.Clear();
        foreach (Transform child in transform)
        {
            childStates[child.name] = child.gameObject.activeSelf;
        }
    }

    void CheckChildStateChanges()
    {
        foreach (Transform child in transform)
        {
            if (childStates.TryGetValue(child.name, out bool wasChildActive))
            {
                if (wasChildActive != child.gameObject.activeSelf)
                {
                    Debug.LogWarning($"[PMDebug] Child state changed: {child.name} was {wasChildActive} → now {child.gameObject.activeSelf}");
                    childStates[child.name] = child.gameObject.activeSelf;
                }
            }
        }
    }

    void DiagnoseInactiveState()
    {
        Debug.LogError("[PMDebug] 🔍 DIAGNOSING INACTIVE STATE:");

        // Check parent
        if (transform.parent != null)
        {
            Debug.LogError($"[PMDebug] Has parent: {transform.parent.name} (active: {transform.parent.gameObject.activeSelf})");
        }

        // Check for duplicate instances
        var allPersistentManagers = FindObjectsByType<PersistentManagersDebugger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.LogError($"[PMDebug] Found {allPersistentManagers.Length} PersistentManagersDebugger instances:");
        foreach (var pm in allPersistentManagers)
        {
            Debug.LogError($"[PMDebug]   - {pm.gameObject.name} (Active: {pm.gameObject.activeSelf}, Scene: {pm.gameObject.scene.name}, InstanceID: {pm.gameObject.GetInstanceID()})");
        }

        // Check scene
        Debug.LogError($"[PMDebug] Current scene: {gameObject.scene.name}");
        Debug.LogError($"[PMDebug] Scene is loaded: {gameObject.scene.isLoaded}");

        // Check all root objects in scene
        if (gameObject.scene.isLoaded)
        {
            var roots = gameObject.scene.GetRootGameObjects();
            Debug.LogError($"[PMDebug] Root objects in scene ({roots.Length}):");
            foreach (var root in roots)
            {
                Debug.LogError($"[PMDebug]   - {root.name} (Active: {root.activeSelf})");
            }
        }
    }

    // ===== MENU COMMANDS (for testing) =====

    [ContextMenu("Force Log Current State")]
    void ForceLogState()
    {
        LogHeader("MANUAL STATE CHECK");
        LogObjectState("ManualCheck");
        LogAllLoadedScenes();
        LogFooter();
    }

    [ContextMenu("Find All Persistent Manager Instances")]
    void FindAllInstances()
    {
        var all = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(go => go.name.Contains("Persistent")).ToArray();
        Debug.Log($"[PMDebug] Found {all.Length} GameObjects with 'Persistent' in name:");
        foreach (var obj in all)
        {
            Debug.Log($"[PMDebug]   - {obj.name} (Active: {obj.activeSelf}, Scene: {obj.scene.name})");
        }
    }
}