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

        [SerializeField, Tooltip("Rotation speed of the door")]
        private float doorRotationSpeed = 90f;
        #endregion

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
            "The oracle joined drama club",
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

        private void Awake()
        {
            TryGetComponent(out m_voskSpeechToText);
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
                m_doorOpenRotation = m_doorClosedRotation * Quaternion.Euler(0, 90, 0);
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

            // Start/stop recording based on proximity
            if (distance <= hearingRadius && !m_voskSpeechToText.IsRecording)
            {
                this.LogInfo("Player entered hearing radius - starting voice detection");
                m_voskSpeechToText.StartRecording();
            }
            else if (distance > hearingRadius && m_voskSpeechToText.IsRecording)
            {
                this.LogInfo("Player left hearing radius - stopping voice detection");
                m_voskSpeechToText.StopRecording();
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
            foreach (RecognizedPhrase phrase in recognitionResult.Phrases)
            {
                if (string.IsNullOrEmpty(phrase.Text)) continue;

                this.LogInfo($"Heard: '{phrase.Text}' (confidence: {phrase.Confidence:F2})");

                if (string.Equals(phrase.Text, targetSentence, StringComparison.CurrentCultureIgnoreCase))
                {
                    this.LogInfo("Correct sentence spoken! Sending to server...");
                    ServerValidateAndOpenDoor(phrase.Text);
                    return;
                }
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
        }

        private IEnumerator AnimateDoorOpen()
        {
            float elapsedTime = 0f;
            float duration = 90f / doorRotationSpeed; // Time to rotate 90 degrees

            while (elapsedTime < duration)
            {
                doorTransform.localRotation = Quaternion.Slerp(
                    m_doorClosedRotation,
                    m_doorOpenRotation,
                    elapsedTime / duration
                );

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            doorTransform.localRotation = m_doorOpenRotation;
            this.LogInfo("Door fully opened");
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
        }
        #endregion
    }
}
