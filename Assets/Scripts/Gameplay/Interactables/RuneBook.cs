using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class RuneBook : Item, IRuneContainer
    {
        #region Serialized
        [Header("Rune Book Data")]
        [SerializeField] private Animator animator;
        #endregion

        private static readonly int AnimParamIsOpen = Animator.StringToHash("IsOpen");

        private static readonly int ShaderPropBaseTextureIndex = Shader.PropertyToID("_BaseTextureIndex");
        private static readonly int ShaderPropRuneTexture = Shader.PropertyToID("_RuneTexture");
        private static readonly int ShaderPropRuneTextureST = Shader.PropertyToID("_RuneTexture_ST");
        private static readonly int ShaderPropRuneOpacity = Shader.PropertyToID("_RuneOpacity");
        private static readonly int ShaderPropHasRune = Shader.PropertyToID("_HasRune");

        private readonly SyncVar<int> m_bookTextureIndex = new(5);
        private readonly SyncVar<int> m_runeIndex = new(-1, new SyncTypeSettings(WritePermission.ClientUnsynchronized));

        private Renderer[] m_renderers;
        private Material m_sharedMaterialInstance;
        private Coroutine m_runeFadeCoroutine;

        protected override void Awake()
        {
            base.Awake();
            m_renderers = GetComponentsInChildren<Renderer>(true);
            if (m_renderers == null || m_renderers.Length == 0)
                return;

            Material template = m_renderers[0].sharedMaterial;
            if (!template) return;

            // Create a new material instance for this book and assign it to all renderers
            m_sharedMaterialInstance = new Material(template);
            foreach (var r in m_renderers)
            {
                r.sharedMaterial = m_sharedMaterialInstance;
            }

            // Ensure the material uses the current book texture index on awake
            SetBaseTextureIndex(m_bookTextureIndex.Value);
        }

        private void OnEnable()
        {
            m_bookTextureIndex.OnChange += BookTextureIndex_OnChange;
            m_runeIndex.OnChange += RuneIndex_OnChange;
        }

        private void OnDisable()
        {
            m_bookTextureIndex.OnChange -= BookTextureIndex_OnChange;
            m_runeIndex.OnChange -= RuneIndex_OnChange;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            m_bookTextureIndex.Value = Random.Range(0, 6);
        }

        public override void OnPickupStart()
        {
            if (!IsOwner) return;
            animator.SetBool(AnimParamIsOpen, true);
        }

        public override void OnPickupEnd()
        {
            if (!IsOwner) return;
            if (m_runeIndex.Value > -1)
            {
                if (HolderCharacter.Notebook.CollectRune(m_runeIndex.Value))
                {
                    RuneCollected_ServerRPC();
                }
            }
        }

        public override void OnDrop()
        {
            if (!IsOwner) return;
            animator.SetBool(AnimParamIsOpen, false);
        }

        [ServerRpc(RunLocally = true)]
        private void RuneCollected_ServerRPC()
        {
            m_runeIndex.Value = -1;
        }

        public override string GetInteractionText() => "Open";

        public void SetContainedRune(RuneSO rune)
        {
            m_runeIndex.Value = GameManager.Instance.RuneDatabase.IndexOf(rune);
        }

        private void SetBaseTextureIndex(int index)
        {
            m_sharedMaterialInstance?.SetInteger(ShaderPropBaseTextureIndex, index);
        }

        private void SetRuneTexture(RuneSO rune)
        {
            if (!m_sharedMaterialInstance) return;

            if (m_runeFadeCoroutine != null)
            {
                StopCoroutine(m_runeFadeCoroutine);
                m_runeFadeCoroutine = null;
            }

            if ((bool)rune && (bool)rune.Sprite)
            {
                m_sharedMaterialInstance.SetTexture(ShaderPropRuneTexture, rune.Sprite.texture);
                m_sharedMaterialInstance.SetInteger(ShaderPropHasRune, 1);
                m_sharedMaterialInstance.SetFloat(ShaderPropRuneOpacity, 1f);
                if (rune.Sprite.packed)
                {
                    Texture tex = rune.Sprite.texture;
                    Rect texRect = rune.Sprite.textureRect;
                    Vector4 st = new Vector4(texRect.width / tex.width, texRect.height / tex.height,
                        texRect.x / tex.width, texRect.y / tex.height);
                    m_sharedMaterialInstance.SetVector(ShaderPropRuneTextureST, st);
                }
                else
                {
                    m_sharedMaterialInstance.SetVector(ShaderPropRuneTextureST, new Vector4(1f, 1f, 0f, 0f));
                }
            }
            else
            {
                // Smoothly fade rune opacity to 0, then clear the rune texture and set HasRune to 0
                StartRuneRemoval();
            }
        }

        private void StartRuneRemoval()
        {
            if (m_runeFadeCoroutine != null) StopCoroutine(m_runeFadeCoroutine);
            m_runeFadeCoroutine = StartCoroutine(FadeOutRuneCoroutine(0.75f));
        }

        private IEnumerator FadeOutRuneCoroutine(float duration)
        {
            if (!m_sharedMaterialInstance) yield break;

            // Delay before starting fade (this is to account for the time it takes to pick up the book)
            yield return new WaitForSeconds(1f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / duration);
                m_sharedMaterialInstance.SetFloat(ShaderPropRuneOpacity, t);
                yield return null;
            }

            // Ensure completely invisible, then mark as having no rune
            m_sharedMaterialInstance.SetFloat(ShaderPropRuneOpacity, 0f);
            m_sharedMaterialInstance.SetInteger(ShaderPropHasRune, 0);

            m_runeFadeCoroutine = null;
        }

        private void BookTextureIndex_OnChange(int prev, int next, bool asServer)
        {
            SetBaseTextureIndex(next);
        }

        private void RuneIndex_OnChange(int prev, int next, bool asServer)
        {
            SetRuneTexture(next > -1 ? GameManager.Instance.RuneDatabase[next] : null);
        }
    }
}
