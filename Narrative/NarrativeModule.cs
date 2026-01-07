using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using TechtonicaFramework.Core;
using TMPro;
using UnityEngine;

namespace TechtonicaFramework.Narrative
{
    /// <summary>
    /// Narrative Module - Provides dialogue triggering using the game's actual dialogue system.
    /// Integrates with VOPlaybackManager for voice and DialogueEntryPopupUI for display.
    /// Includes patches to bypass localization for custom dialogue text.
    /// </summary>
    public class NarrativeModule : IFrameworkModule
    {
        public bool IsActive { get; private set; }

        // Custom dialogue entries
        private Dictionary<string, CustomDialogueEntry> customDialogues = new Dictionary<string, CustomDialogueEntry>();

        // Custom speakers (for display info only - voice uses game speakers)
        private Dictionary<string, CustomSpeaker> customSpeakers = new Dictionary<string, CustomSpeaker>();

        // Track played dialogues
        private HashSet<string> playedDialogues = new HashSet<string>();

        // Track custom NarrativeEntryData instances (to bypass localization)
        internal static HashSet<NarrativeEntryData> customNarrativeEntries = new HashSet<NarrativeEntryData>();

        // Map our speaker IDs to game Speaker enum
        private static readonly Dictionary<string, NarrativeEntryData.Speaker> SpeakerMap = new Dictionary<string, NarrativeEntryData.Speaker>
        {
            { "sparks", NarrativeEntryData.Speaker.Sparks },
            { "paladin", NarrativeEntryData.Speaker.Paladin },
            { "mirage", NarrativeEntryData.Speaker.Mirage },
            { "system", NarrativeEntryData.Speaker.System },
            { "unknown", NarrativeEntryData.Speaker.Unknown },
            { "groundbreaker", NarrativeEntryData.Speaker.TheGroundbreaker },
            { "sparks_radio", NarrativeEntryData.Speaker.SparksRadio },
            { "mirage_radio", NarrativeEntryData.Speaker.MirageRadio },
            { "tapwire_a", NarrativeEntryData.Speaker.TapwireA },
            { "tapwire_b", NarrativeEntryData.Speaker.TapwireB },
            // Custom speakers map to Unknown for voice (text-only)
            { "ancient_ai", NarrativeEntryData.Speaker.Unknown },
            { "corrupted_sparks", NarrativeEntryData.Speaker.Unknown }
        };

        // Speaker display names for custom speakers
        private static readonly Dictionary<string, string> CustomSpeakerNames = new Dictionary<string, string>
        {
            { "ancient_ai", "Ancient AI" },
            { "corrupted_sparks", "S̷p̴a̵r̷k̸s̵" }
        };

        public void Initialize()
        {
            IsActive = true;
            ApplyPatches();
            TechtonicaFrameworkPlugin.LogDebug("NarrativeModule: Initialized with game dialogue integration and localization bypass");
        }

        private void ApplyPatches()
        {
            try
            {
                var harmony = TechtonicaFrameworkPlugin.Harmony;

                // Patch StartNarrativeString to bypass localization for custom dialogue
                var originalMethod = AccessTools.Method(typeof(DialogueEntryPopupUI), "StartNarrativeString");
                var prefixMethod = new HarmonyMethod(typeof(NarrativePatches), nameof(NarrativePatches.StartNarrativeString_Prefix));

                if (originalMethod != null)
                {
                    harmony.Patch(originalMethod, prefix: prefixMethod);
                    TechtonicaFrameworkPlugin.LogDebug("NarrativeModule: Patched StartNarrativeString for localization bypass");
                }
                else
                {
                    TechtonicaFrameworkPlugin.Log.LogWarning("NarrativeModule: Could not find StartNarrativeString method");
                }
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogError($"NarrativeModule: Failed to apply patches: {ex.Message}");
            }
        }

        public void Update()
        {
            // Narrative module doesn't need per-frame updates
        }

        public void Shutdown()
        {
            IsActive = false;
            customDialogues.Clear();
            customSpeakers.Clear();
            TechtonicaFrameworkPlugin.LogDebug("NarrativeModule: Shutdown");
        }

        #region Custom Speaker Registration

        /// <summary>
        /// Register a custom speaker for dialogue display.
        /// </summary>
        public void RegisterSpeaker(string id, string displayName, Color textColor, Sprite portrait = null)
        {
            var speaker = new CustomSpeaker
            {
                Id = id,
                DisplayName = displayName,
                TextColor = textColor,
                Portrait = portrait
            };

            customSpeakers[id] = speaker;
            TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Registered speaker '{id}' ({displayName})");
        }

        /// <summary>
        /// Get a registered custom speaker.
        /// </summary>
        public CustomSpeaker GetSpeaker(string id)
        {
            return customSpeakers.TryGetValue(id, out var speaker) ? speaker : null;
        }

        /// <summary>
        /// Get the game's Speaker enum for a speaker ID.
        /// </summary>
        public NarrativeEntryData.Speaker GetGameSpeaker(string speakerId)
        {
            if (SpeakerMap.TryGetValue(speakerId.ToLower(), out var speaker))
            {
                return speaker;
            }
            return NarrativeEntryData.Speaker.Unknown;
        }

        #endregion

        #region Dialogue Management

        /// <summary>
        /// Register a custom dialogue entry.
        /// </summary>
        public CustomDialogueEntry RegisterDialogue(string id, string speakerId, string text, float duration = 5f, string voKey = null)
        {
            // Get custom speaker display name if applicable
            string customSpeakerName = null;
            var customSpeaker = GetSpeaker(speakerId);
            if (customSpeaker != null)
            {
                customSpeakerName = customSpeaker.DisplayName;
            }
            else if (CustomSpeakerNames.TryGetValue(speakerId.ToLower(), out var name))
            {
                customSpeakerName = name;
            }

            var entry = new CustomDialogueEntry
            {
                Id = id,
                SpeakerId = speakerId,
                Text = text,
                Duration = duration,
                NextDialogueId = null,
                Condition = null,
                GameSpeaker = GetGameSpeaker(speakerId),
                CustomSpeakerName = customSpeakerName,
                VOKey = voKey
            };

            customDialogues[id] = entry;
            TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Registered dialogue '{id}' for speaker '{speakerId}' (name={customSpeakerName ?? "default"})");
            return entry;
        }

        /// <summary>
        /// Chain a dialogue to play after another.
        /// </summary>
        public void ChainDialogue(string dialogueId, string nextDialogueId)
        {
            if (customDialogues.TryGetValue(dialogueId, out var entry))
            {
                entry.NextDialogueId = nextDialogueId;
            }
        }

        /// <summary>
        /// Set a condition for dialogue to play.
        /// </summary>
        public void SetDialogueCondition(string dialogueId, Func<bool> condition)
        {
            if (customDialogues.TryGetValue(dialogueId, out var entry))
            {
                entry.Condition = condition;
            }
        }

        /// <summary>
        /// Trigger a dialogue to play using the game's dialogue system.
        /// </summary>
        public void TriggerDialogue(string dialogueId)
        {
            if (!customDialogues.TryGetValue(dialogueId, out var entry))
            {
                TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Dialogue '{dialogueId}' not found");
                return;
            }

            // Check condition if set
            if (entry.Condition != null && !entry.Condition())
            {
                TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Dialogue '{dialogueId}' condition not met");
                return;
            }

            // Display the dialogue using the game's system
            DisplayDialogueViaGameUI(entry);

            // Mark as played
            playedDialogues.Add(dialogueId);
            FrameworkEvents.RaiseDialogueStarted(dialogueId);

            // Queue next dialogue if chained (handled by coroutine)
        }

        /// <summary>
        /// Check if a dialogue has been played.
        /// </summary>
        public bool HasDialoguePlayed(string dialogueId)
        {
            return playedDialogues.Contains(dialogueId);
        }

        /// <summary>
        /// Reset played dialogue tracking.
        /// </summary>
        public void ResetPlayedDialogues()
        {
            playedDialogues.Clear();
        }

        /// <summary>
        /// Display dialogue using the game's actual DialogueEntryPopupUI system.
        /// Custom dialogue bypasses the localization system to avoid "Cannot Translate" errors.
        /// </summary>
        private void DisplayDialogueViaGameUI(CustomDialogueEntry entry)
        {
            try
            {
                // Get the game's dialogue popup
                var dialoguePopup = UIManager.instance?.dialoguePopup;
                if (dialoguePopup == null)
                {
                    TechtonicaFrameworkPlugin.LogDebug("NarrativeModule: DialoguePopup not available, using fallback");
                    DisplayFallbackDialogue(entry);
                    return;
                }

                // Create a runtime NarrativeEntryData
                var narrativeData = ScriptableObject.CreateInstance<NarrativeEntryData>();
                narrativeData.shortText = entry.Text;
                narrativeData.speaker = entry.GameSpeaker;
                narrativeData.durationOnScreen = entry.Duration;
                narrativeData.addToLogAfterFirstPlayback = false;

                // Store custom speaker name for display (will be read by patch)
                narrativeData.name = entry.CustomSpeakerName ?? "";

                // For voice, we can optionally use game VO keys if the speaker has them
                // Leave empty for text-only dialogue (no voice)
                narrativeData.shortTextVOKey = entry.VOKey ?? "";

                // Track this as a custom entry so our patch knows to bypass localization
                customNarrativeEntries.Add(narrativeData);

                // Trigger the dialogue
                dialoguePopup.OnTriggerNarrativeEntry(narrativeData);

                TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Triggered dialogue '{entry.Id}' via game UI (localization bypass active)");

                // Handle chained dialogue
                if (!string.IsNullOrEmpty(entry.NextDialogueId))
                {
                    TechtonicaFrameworkPlugin.Instance?.StartCoroutine(TriggerChainedDialogue(entry.NextDialogueId, entry.Duration + 1f));
                }
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Error displaying dialogue: {ex.Message}");
                DisplayFallbackDialogue(entry);
            }
        }

        /// <summary>
        /// Coroutine to trigger chained dialogue after a delay.
        /// </summary>
        private IEnumerator TriggerChainedDialogue(string dialogueId, float delay)
        {
            yield return new WaitForSeconds(delay);
            TriggerDialogue(dialogueId);
        }

        /// <summary>
        /// Fallback dialogue display using notification system.
        /// </summary>
        private void DisplayFallbackDialogue(CustomDialogueEntry entry)
        {
            var speaker = GetSpeaker(entry.SpeakerId);
            string speakerName = speaker?.DisplayName ?? entry.SpeakerId;

            // Try notification system
            try
            {
                var systemLog = UIManager.instance?.systemLog;
                if (systemLog != null)
                {
                    // Use the game's notification system
                    systemLog.FlashMessage($"[{speakerName}] {entry.Text}");
                }
            }
            catch
            {
                // Last resort - just log it
                TechtonicaFrameworkPlugin.Log.LogInfo($"[DIALOGUE] [{speakerName}] {entry.Text}");
            }

            // Still handle chaining
            if (!string.IsNullOrEmpty(entry.NextDialogueId))
            {
                TechtonicaFrameworkPlugin.Instance?.StartCoroutine(TriggerChainedDialogue(entry.NextDialogueId, entry.Duration + 1f));
            }
        }

        #endregion

        #region Dialogue Sequence Builder

        /// <summary>
        /// Create a dialogue sequence builder for easier chaining.
        /// </summary>
        public DialogueSequenceBuilder CreateSequence(string sequenceId)
        {
            return new DialogueSequenceBuilder(this, sequenceId);
        }

        #endregion

        #region Quest Helpers

        /// <summary>
        /// Create a simple quest trigger that plays dialogue on start/end.
        /// </summary>
        public void CreateQuestDialogueTrigger(string questId, string startDialogueId, string endDialogueId)
        {
            TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Created quest dialogue trigger for '{questId}'");
        }

        #endregion
    }

    /// <summary>
    /// Custom speaker data.
    /// </summary>
    public class CustomSpeaker
    {
        public string Id;
        public string DisplayName;
        public Color TextColor;
        public Sprite Portrait;
    }

    /// <summary>
    /// Custom dialogue entry data.
    /// </summary>
    public class CustomDialogueEntry
    {
        public string Id;
        public string SpeakerId;
        public string Text;
        public float Duration;
        public string NextDialogueId;
        public Func<bool> Condition;
        public NarrativeEntryData.Speaker GameSpeaker;
        public string CustomSpeakerName; // Display name for custom speakers
        public string VOKey; // Optional: existing game VO key for voice playback
    }

    /// <summary>
    /// Helper class for building dialogue sequences.
    /// </summary>
    public class DialogueSequenceBuilder
    {
        private NarrativeModule module;
        private string sequenceId;
        private List<string> dialogueIds = new List<string>();
        private string lastDialogueId;

        public DialogueSequenceBuilder(NarrativeModule module, string sequenceId)
        {
            this.module = module;
            this.sequenceId = sequenceId;
        }

        /// <summary>
        /// Add a dialogue line to the sequence.
        /// </summary>
        public DialogueSequenceBuilder AddLine(string speakerId, string text, float duration = 5f)
        {
            string dialogueId = $"{sequenceId}_{dialogueIds.Count}";
            var entry = module.RegisterDialogue(dialogueId, speakerId, text, duration);

            // Chain to previous dialogue
            if (!string.IsNullOrEmpty(lastDialogueId))
            {
                module.ChainDialogue(lastDialogueId, dialogueId);
            }

            dialogueIds.Add(dialogueId);
            lastDialogueId = dialogueId;

            return this;
        }

        /// <summary>
        /// Set a condition for the entire sequence.
        /// </summary>
        public DialogueSequenceBuilder WithCondition(Func<bool> condition)
        {
            if (dialogueIds.Count > 0)
            {
                module.SetDialogueCondition(dialogueIds[0], condition);
            }
            return this;
        }

        /// <summary>
        /// Get the ID of the first dialogue in the sequence (use to trigger).
        /// </summary>
        public string GetStartId()
        {
            return dialogueIds.Count > 0 ? dialogueIds[0] : null;
        }

        /// <summary>
        /// Trigger the sequence to play.
        /// </summary>
        public void Play()
        {
            var startId = GetStartId();
            if (!string.IsNullOrEmpty(startId))
            {
                module.TriggerDialogue(startId);
            }
        }
    }

    /// <summary>
    /// Harmony patches for the narrative system to bypass localization for custom dialogue.
    /// </summary>
    internal static class NarrativePatches
    {
        /// <summary>
        /// Prefix patch for DialogueEntryPopupUI.StartNarrativeString.
        /// For custom dialogue entries, sets text directly without going through localization.
        /// This prevents "Cannot Translate" errors for custom dialogue text.
        /// </summary>
        public static bool StartNarrativeString_Prefix(DialogueEntryPopupUI __instance, NarrativeEntryData entryData)
        {
            // Check if this is a custom entry (one we created, not from game data)
            if (!NarrativeModule.customNarrativeEntries.Contains(entryData))
            {
                // Not our custom entry - let the original method handle it normally
                return true;
            }

            try
            {
                // Get private fields via reflection
                var messageTextField = AccessTools.Field(typeof(DialogueEntryPopupUI), "messageText");
                var speakerNameTextField = AccessTools.Field(typeof(DialogueEntryPopupUI), "speakerNameText");
                var speakerPortraitCGField = AccessTools.Field(typeof(DialogueEntryPopupUI), "speakerPortraitCanvasGroup");
                var speakerPortraitImageField = AccessTools.Field(typeof(DialogueEntryPopupUI), "speakerPortraitImage");
                var showingMessageField = AccessTools.Field(typeof(DialogueEntryPopupUI), "_showingMessage");
                var currentEntryField = AccessTools.Field(typeof(DialogueEntryPopupUI), "_currentEntryBeingShown");
                var timeOnScreenField = AccessTools.Field(typeof(DialogueEntryPopupUI), "timeOnScreen");

                var messageText = messageTextField?.GetValue(__instance) as TextMeshProUGUI;
                var speakerNameText = speakerNameTextField?.GetValue(__instance) as TextMeshProUGUI;
                var speakerPortraitCG = speakerPortraitCGField?.GetValue(__instance) as CanvasGroup;
                var speakerPortraitImage = speakerPortraitImageField?.GetValue(__instance) as UnityEngine.UI.Image;

                if (messageText == null || speakerNameText == null)
                {
                    TechtonicaFrameworkPlugin.LogDebug("NarrativePatches: Could not find text fields, falling back to original");
                    return true;
                }

                // Set state
                showingMessageField?.SetValue(__instance, true);
                currentEntryField?.SetValue(__instance, entryData);

                // SET TEXT DIRECTLY - bypass localization
                // This is the key fix for "Cannot Translate" errors
                messageText.text = entryData.shortText;

                // Handle speaker
                if (entryData.speaker == NarrativeEntryData.Speaker.None)
                {
                    speakerNameText.text = string.Empty;
                    speakerPortraitCG?.ToggleAlpha(false);
                }
                else
                {
                    // Check for custom speaker name stored in entryData.name
                    string speakerName = entryData.name;
                    if (string.IsNullOrEmpty(speakerName))
                    {
                        // Use game's speaker info
                        var speakerInfo = GameDefines.instance?.GetSpeakerInfo(entryData.speaker);
                        speakerName = speakerInfo?._displayName ?? entryData.speaker.ToString();
                    }

                    // Set speaker name directly - bypass localization
                    speakerNameText.text = speakerName;

                    // Handle portrait
                    var speakerInfo2 = GameDefines.instance?.GetSpeakerInfo(entryData.speaker);
                    if (speakerInfo2?._speakerPortrait != null)
                    {
                        speakerPortraitCG?.ToggleAlpha(true);
                        if (speakerPortraitImage != null)
                            speakerPortraitImage.sprite = speakerInfo2._speakerPortrait;
                    }
                    else
                    {
                        speakerPortraitCG?.ToggleAlpha(false);
                    }
                }

                // Handle duration
                if (entryData.durationOnScreen >= 0f)
                {
                    timeOnScreenField?.SetValue(__instance, entryData.durationOnScreen);
                }

                // Handle voice (if VO key provided)
                if (!string.IsNullOrEmpty(entryData.shortTextVOKey) && VOPlaybackManager.instance != null)
                {
                    var voInstance = VOPlaybackManager.instance.GetDialogueEventInstance(
                        entryData.shortTextVOKey, entryData.speaker, out float voTrackLength);

                    if (voInstance.isValid())
                    {
                        voInstance.getDescription(out var description);
                        description.is3D(out var is3D);
                        if (is3D)
                        {
                            voInstance.set3DAttributes(((MonoBehaviour)__instance).transform.To3DAttributes());
                        }
                        voInstance.start();
                        TechtonicaFrameworkPlugin.LogDebug($"NarrativePatches: Started VO playback for key '{entryData.shortTextVOKey}'");
                    }
                }

                // Start animation coroutine via reflection
                var animMethod = AccessTools.Method(typeof(DialogueEntryPopupUI), "AnimCoroutine");
                if (animMethod != null)
                {
                    var coroutine = animMethod.Invoke(__instance, null) as IEnumerator;
                    if (coroutine != null)
                    {
                        __instance.StartCoroutine(coroutine);
                    }
                }

                // Remove from tracking set (entry is now displayed)
                NarrativeModule.customNarrativeEntries.Remove(entryData);

                TechtonicaFrameworkPlugin.LogDebug($"NarrativePatches: Custom dialogue displayed (bypassed localization)");

                // Skip original method
                return false;
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogError($"NarrativePatches: Error in prefix: {ex.Message}");
                // Fall back to original method on error
                return true;
            }
        }
    }
}
