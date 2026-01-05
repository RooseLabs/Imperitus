using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using RooseLabs.Gameplay.Interactables;
using RooseLabs.Player;
using RooseLabs.Utils;
using RooseLabs.Vosk;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RooseLabs.Gameplay
{
    [RequireComponent(typeof(VoskSpeechToText))]
    public class LibraryReceptionPuzzle : NetworkBehaviour
    {
        #region Serialized
        [Header("Puzzle Configuration")]
        [SerializeField, Tooltip("List of spawn points where word objects will be placed")]
        private ObjectSpawnPoint[] objectSpawnPoints = Array.Empty<ObjectSpawnPoint>();

        [SerializeField, Tooltip("Radius around the door where the player's voice can be heard")]
        private float hearingRadius = 5f;

        [Header("Door Animation")]
        [SerializeField, Tooltip("The transform that will rotate when the puzzle is solved and the door opens")]
        private Transform doorTransform;

        [SerializeField, Tooltip("Duration in seconds for the door to fully open")]
        private float doorOpeningDuration = 1f;

        [SerializeField, Tooltip("The angle (in degrees) the door rotates around Y axis when opening")]
        private float doorOpeningAngle = -90f;
        #endregion

        private static readonly int ShaderPropLadyOpacity = Shader.PropertyToID("_LadyOpacity");
        private static readonly int ShaderPropWrath = Shader.PropertyToID("_Wrath");

        private static readonly string[] PuzzleSentences =
        {
            "The dragon ate homework",
            "Mermaids gossip at dawn",
            "Witches hate pop quizzes",
            "A phoenix fears rain",
            "Trolls host karaoke nights",
            "The moon forgot math",
            "Centaurs argue about coffee",
            "Magic hides in hallways",
            "Fairies cheat on exams",
            "Vampires dread sunlight class",
            "Potions spill during lunch",
            "The cat recites poetry",
            "A spell failed spectacularly",
            "Goblins run the cafeteria",
            "The broom has opinions",
            "Unicorns sparkle under pressure",
            "Demons fear group projects",
            "The cauldron never sleeps",
            "Werewolves skip full moons",
            "Enchantment smells like cinnamon",
            "Gnomes argue about snacks",
            "The potion glows nervously",
            "Spirits whisper test answers",
            "Dragons nap during algebra",
            "Wizards crave midnight noodles",
            "Shadows gossip in corners",
            "The spellbook rolled eyes",
            "Elves debate cosmic ethics",
            "The raven steals pens",
            "Time hiccups between bells",
            "A fairy failed chemistry",
            "Monsters fear pop quizzes",
            "The cauldron laughed first",
            "Ghosts text bad advice",
            "Destiny skipped homeroom again",
            "A spell smelled weird",
            "Witches duel in gym",
            "The frog dreams deeply",
            "Sorcery prefers quiet rooms",
            "The library guards secrets",
            "Owls grade homework harshly",
            "Magic hums beneath lockers",
            "The philosopher hugged chaos",
            "The moon tutored starlight",
            "The goblin started drama",
            "Spirits haunt detention hall",
            "The spell whispered rebellion",
            "Dreams flirt with reality"
        };

        private readonly SyncVar<bool> m_isDoorOpen = new();
        private readonly SyncVar<int> m_targetSentenceIndex = new(-1);

        private readonly List<WordCarrierObject> m_spawnedWordObjects = new();
        private int m_lastActiveClientCount;
        private VoskSpeechToText m_voskSpeechToText;
        private Quaternion m_doorClosedRotation;
        private Quaternion m_doorOpenRotation;
        private Coroutine m_doorAnimationCoroutine;
        private Material m_doorMaterial;
        private Coroutine m_ladyOpacityCoroutine;
        private Coroutine m_wrathCoroutine;
        private bool m_isPlayerInProximity;

        private void Awake()
        {
            TryGetComponent(out m_voskSpeechToText);

            // Setup door material for shader effects
            if ((bool)doorTransform && doorTransform.TryGetComponent(out Renderer r))
            {
                m_doorMaterial = r.material;
                m_doorMaterial.SetFloat(ShaderPropLadyOpacity, 0f);
                m_doorMaterial.SetFloat(ShaderPropWrath, 0f);
            }
        }

        public override void OnStartServer()
        {
            InitializePuzzle();

            // Subscribe to client connection changes
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            // Unsubscribe from connection events
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        public override void OnStartClient()
        {
            if (!IsServerInitialized)
                DestroySpawnPoints();
            SetupClientVoiceDetection();

            // Store door rotations
            if (doorTransform)
            {
                m_doorClosedRotation = doorTransform.localRotation;
                m_doorOpenRotation = m_doorClosedRotation * Quaternion.Euler(0, doorOpeningAngle, 0);
            }

            // Subscribe to network variable changes
            m_isDoorOpen.OnChange += OnDoorStateChanged;
        }

        private void Update()
        {
            if (!m_isDoorOpen.Value)
            {
                CheckPlayerProximity();
            }
        }

        private void OnDisable()
        {
            m_voskSpeechToText.OnTranscriptionResult -= OnVoiceTranscription;
            m_isDoorOpen.OnChange -= OnDoorStateChanged;
        }

        #region Server-Side Initialization and Management
        [Server]
        private void InitializePuzzle()
        {
            if (objectSpawnPoints.Length == 0)
            {
                this.LogWarning("No spawn points assigned!");
                return;
            }

            // Pick a random sentence index
            int sentenceIndex = Random.Range(0, PuzzleSentences.Length);
            m_targetSentenceIndex.Value = sentenceIndex;

            // Get the sentence and extract words
            string targetSentence = PuzzleSentences[sentenceIndex].ToLower();
            string[] words = targetSentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            this.LogInfo($"Initializing puzzle with target sentence: '{targetSentence}'");

            // Shuffle spawn points in place to randomize selection
            objectSpawnPoints.Shuffle();

            int spawnCount = Mathf.Min(words.Length, objectSpawnPoints.Length);

            // Get list of active clients
            var connectedClients = ServerManager.Clients.Values.Where(c => c.IsActive).ToList();
            m_lastActiveClientCount = connectedClients.Count;

            for (int i = 0; i < spawnCount; i++)
            {
                ObjectSpawnPoint spawnPoint = objectSpawnPoints[i];

                // Get valid objects from spawn point
                if (spawnPoint.AllowedObjects.Length == 0)
                {
                    this.LogWarning($"Spawn point '{spawnPoint.name}' has no allowed objects!");
                    continue;
                }
                GameObject[] validObjects = spawnPoint.AllowedObjects.Where(obj => (bool)obj).ToArray();
                if (validObjects.Length == 0)
                {
                    this.LogWarning($"Spawn point '{spawnPoint.name}' has no valid allowed objects!");
                    continue;
                }

                // Pick a random object from the allowed objects
                GameObject objectToSpawn = validObjects[Random.Range(0, validObjects.Length)];

                // Spawn the object
                GameObject spawnedObj = Instantiate(objectToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);

                // Assign the word to the object
                if (spawnedObj.TryGetComponent(out WordCarrierObject wordCarrier))
                {
                    m_spawnedWordObjects.Add(wordCarrier);
                    wordCarrier.SetWord(words[i]);

                    // Assign visibility to a specific client (round-robin distribution)
                    if (connectedClients.Count > 0)
                    {
                        int clientIndex = i % connectedClients.Count;
                        int clientId = connectedClients[clientIndex].ClientId;
                        wordCarrier.SetVisibleToClientId(clientId);
                        this.LogInfo($"Spawned word object '{words[i]}' at {spawnPoint.name} (visible to client {clientId})");
                    }
                    else
                    {
                        this.LogInfo($"Spawned word object '{words[i]}' at {spawnPoint.name} (visible to all)");
                    }
                }
                else
                {
                    this.LogWarning($"Spawned object at '{spawnPoint.name}' does not have WordCarrierObject component!");
                }

                Spawn(spawnedObj, scene: spawnPoint.gameObject.scene);
            }

            DestroySpawnPoints();
        }

        [Server]
        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            // Only handle Started and Stopped states
            if (args.ConnectionState != RemoteConnectionState.Started &&
                args.ConnectionState != RemoteConnectionState.Stopped)
            {
                return;
            }

            // Don't redistribute word visibility if the door is already open
            if (m_isDoorOpen.Value)
                return;

            // Get current active client count
            int currentActiveClientCount = ServerManager.Clients.Values.Count(c => c.IsActive);

            // Only redistribute if the count has changed
            if (currentActiveClientCount != m_lastActiveClientCount)
            {
                this.LogInfo($"Active client count changed from {m_lastActiveClientCount} to {currentActiveClientCount}. Redistributing word visibility...");
                RedistributeWordVisibility();
                m_lastActiveClientCount = currentActiveClientCount;
            }
        }

        [Server]
        private void RedistributeWordVisibility()
        {
            // Get active clients
            var activeClients = ServerManager.Clients.Values.Where(c => c.IsActive).ToList();
            if (activeClients.Count == 0) return;

            // Redistribute using round-robin
            for (int i = 0; i < m_spawnedWordObjects.Count; i++)
            {
                WordCarrierObject wordObject = m_spawnedWordObjects[i];
                if (!wordObject) continue;
                int clientIndex = i % activeClients.Count;
                int clientId = activeClients[clientIndex].ClientId;
                wordObject.SetVisibleToClientId(clientId);
                this.LogInfo($"Redistributed '{wordObject.GetWord()}' to client {clientId}");
            }

            this.LogInfo($"Redistributed {m_spawnedWordObjects.Count} words among {activeClients.Count} active clients");
        }
        #endregion

        #region Speech Recognition
        private void SetupClientVoiceDetection()
        {
            // Subscribe to transcription results
            m_voskSpeechToText.OnTranscriptionResult += OnVoiceTranscription;

            // Start VoskSpeechToText with all puzzle sentences as key phrases
            m_voskSpeechToText.StartVoskStt(keyPhrases: PuzzleSentences, startRecording: false);

            this.LogInfo("Client voice detection setup complete with puzzle sentence key phrases");
        }

        private void CheckPlayerProximity()
        {
            if (!PlayerCharacter.LocalCharacter) return;

            float distance = Vector3.Distance(transform.position, PlayerCharacter.LocalCharacter.transform.position);
            bool inProximity = distance <= hearingRadius;

            // Handle proximity state change
            if (inProximity != m_isPlayerInProximity)
            {
                m_isPlayerInProximity = inProximity;

                if (inProximity)
                {
                    this.LogInfo("Player entered hearing radius - starting voice detection");
                    m_voskSpeechToText.StartRecording();
                    AnimateLadyOpacity(1f);
                }
                else
                {
                    this.LogInfo("Player left hearing radius - stopping voice detection");
                    m_voskSpeechToText.StopRecording();
                    AnimateLadyOpacity(0f);
                }
            }
        }

        private void OnVoiceTranscription(string jsonResult)
        {
            if (m_isDoorOpen.Value) return;

            RecognitionResult recognitionResult = new RecognitionResult(jsonResult);

            if (recognitionResult.Partial || recognitionResult.Phrases == null || recognitionResult.Phrases.Length == 0)
                return;

            string targetSentence = PuzzleSentences[m_targetSentenceIndex.Value].ToLower();

            // Check if any recognized phrase matches the target sentence
            bool foundMatch = false;
            foreach (RecognizedPhrase phrase in recognitionResult.Phrases)
            {
                if (string.IsNullOrEmpty(phrase.Text)) continue;

                this.LogInfo($"Heard: '{phrase.Text}' (confidence: {phrase.Confidence:F2})");

                if (string.Equals(phrase.Text, targetSentence, StringComparison.CurrentCultureIgnoreCase))
                {
                    this.LogInfo("Correct sentence spoken! Sending to server...");
                    ServerValidateAndOpenDoor(phrase.Text);
                    foundMatch = true;
                    break;
                }
            }

            // If no match was found and we heard something, trigger Wrath
            if (!foundMatch && recognitionResult.Phrases.Length > 0)
            {
                this.LogInfo("Wrong sentence - triggering Wrath effect");
                AnimateWrath();
            }
        }
        #endregion

        [ServerRpc(RequireOwnership = false)]
        private void ServerValidateAndOpenDoor(string spokenSentence)
        {
            if (m_isDoorOpen.Value)
            {
                this.LogInfo("Door is already open");
                return;
            }

            if (string.Equals(spokenSentence, PuzzleSentences[m_targetSentenceIndex.Value], StringComparison.CurrentCultureIgnoreCase))
            {
                this.LogInfo("Server validated correct sentence from client. Opening door...");
                m_isDoorOpen.Value = true;
            }
            else
            {
                this.LogInfo($"Server rejected sentence from client: '{spokenSentence}'");
            }
        }

        private void OnDoorStateChanged(bool prev, bool next, bool asServer)
        {
            if (!next || !(bool)doorTransform) return;
            if (m_doorAnimationCoroutine != null)
            {
                StopCoroutine(m_doorAnimationCoroutine);
            }
            m_doorAnimationCoroutine = StartCoroutine(AnimateDoorOpen());

            // Handle shader effects when door opens
            if (m_doorMaterial)
            {
                // Stop any ongoing wrath animation and disable it
                if (m_wrathCoroutine != null)
                {
                    StopCoroutine(m_wrathCoroutine);
                    m_wrathCoroutine = null;
                }
                m_doorMaterial.SetFloat(ShaderPropWrath, 0f);

                // Handle LadyOpacity: fade to 1 (if not already), stay for 10 seconds, then fade to 0
                if (m_ladyOpacityCoroutine != null)
                {
                    StopCoroutine(m_ladyOpacityCoroutine);
                }
                m_ladyOpacityCoroutine = StartCoroutine(DoorOpenLadyOpacitySequence());
            }
        }

        private IEnumerator AnimateDoorOpen()
        {
            float elapsedTime = 0f;

            while (elapsedTime < doorOpeningDuration)
            {
                doorTransform.localRotation = Quaternion.Slerp(
                    m_doorClosedRotation,
                    m_doorOpenRotation,
                    elapsedTime / doorOpeningDuration
                );

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            doorTransform.localRotation = m_doorOpenRotation;
        }

        private void AnimateLadyOpacity(float targetOpacity)
        {
            if (!m_doorMaterial ) return;

            if (m_ladyOpacityCoroutine != null)
            {
                StopCoroutine(m_ladyOpacityCoroutine);
            }

            m_ladyOpacityCoroutine = StartCoroutine(AnimateLadyOpacityCoroutine(targetOpacity));
        }

        private IEnumerator AnimateLadyOpacityCoroutine(float targetOpacity)
        {
            float startOpacity = m_doorMaterial.GetFloat(ShaderPropLadyOpacity);
            float elapsedTime = 0f;
            float duration = 1f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                float currentOpacity = Mathf.Lerp(startOpacity, targetOpacity, t);
                m_doorMaterial.SetFloat(ShaderPropLadyOpacity, currentOpacity);
                yield return null;
            }

            m_doorMaterial.SetFloat(ShaderPropLadyOpacity, targetOpacity);
            m_ladyOpacityCoroutine = null;
        }

        private IEnumerator DoorOpenLadyOpacitySequence()
        {
            float currentOpacity = m_doorMaterial.GetFloat(ShaderPropLadyOpacity);

            // Phase 1: Fade to 1 if not already there
            if (currentOpacity < 1f)
            {
                yield return AnimateLadyOpacityCoroutine(1f);
            }

            // Phase 2: Hold at 1 for 9 seconds (10 total minus 1 second fade out)
            yield return new WaitForSeconds(9f);

            // Phase 3: Fade to 0 over 1 second
            yield return AnimateLadyOpacityCoroutine(0f);

            m_ladyOpacityCoroutine = null;
        }

        private void AnimateWrath()
        {
            if (!m_doorMaterial) return;

            if (m_wrathCoroutine != null)
            {
                StopCoroutine(m_wrathCoroutine);
            }

            m_wrathCoroutine = StartCoroutine(AnimateWrathCoroutine());
        }

        private IEnumerator AnimateWrathCoroutine()
        {
            // Set Wrath to 1
            m_doorMaterial.SetFloat(ShaderPropWrath, 1f);

            // Wait for 1 second
            yield return new WaitForSeconds(1f);

            // Set Wrath back to 0
            m_doorMaterial.SetFloat(ShaderPropWrath, 0f);
            m_wrathCoroutine = null;
        }

        private void DestroySpawnPoints()
        {
            foreach (var spawnPoint in objectSpawnPoints)
            {
                if (spawnPoint) Destroy(spawnPoint.gameObject);
            }
        }

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            // Draw hearing radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, hearingRadius);

            // Draw door opening angle visualization
            if (doorTransform)
            {
                Gizmos.color = Color.red;
                Vector3 doorPosition = doorTransform.position;

                // Use the door's current local rotation as the closed state
                Quaternion closedLocalRotation = doorTransform.localRotation;
                Quaternion openLocalRotation = closedLocalRotation * Quaternion.Euler(0, doorOpeningAngle, 0);

                // Convert to world space for visualization
                Quaternion parentRotation = doorTransform.parent ? doorTransform.parent.rotation : Quaternion.identity;
                Quaternion closedWorldRotation = parentRotation * closedLocalRotation;
                Quaternion openWorldRotation = parentRotation * openLocalRotation;

                // Draw closed door direction
                Vector3 closedDirection = closedWorldRotation * Vector3.left * 1.5f;
                Gizmos.DrawRay(doorPosition, closedDirection);

                // Draw open door direction
                Vector3 openDirection = openWorldRotation * Vector3.left * 1.5f;
                Gizmos.DrawRay(doorPosition, openDirection);

                // Draw arc between closed and open positions to show rotation angle
                int arcSegments = 20;
                float angleStep = doorOpeningAngle / arcSegments;
                for (int i = 0; i < arcSegments; i++)
                {
                    Quaternion stepLocalRotation1 = closedLocalRotation * Quaternion.Euler(0, angleStep * i, 0);
                    Quaternion stepLocalRotation2 = closedLocalRotation * Quaternion.Euler(0, angleStep * (i + 1), 0);
                    Quaternion stepWorldRotation1 = parentRotation * stepLocalRotation1;
                    Quaternion stepWorldRotation2 = parentRotation * stepLocalRotation2;
                    Vector3 from = stepWorldRotation1 * Vector3.left * 1.5f;
                    Vector3 to = stepWorldRotation2 * Vector3.left * 1.5f;
                    Gizmos.DrawLine(doorPosition + from, doorPosition + to);
                }
            }
        }
        #endregion
    }
}
