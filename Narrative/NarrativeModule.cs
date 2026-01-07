using System;
using System.Collections;
using System.Collections.Generic;
using TechtonicaFramework.Core;
using UnityEngine;

namespace TechtonicaFramework.Narrative
{
    /// <summary>
    /// Narrative Module - Provides dialogue triggering using the game's actual dialogue system.
    /// Integrates with VOPlaybackManager for voice and DialogueEntryPopupUI for display.
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

        public void Initialize()
        {
            IsActive = true;
            TechtonicaFrameworkPlugin.LogDebug("NarrativeModule: Initialized with game dialogue integration");
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
        public CustomDialogueEntry RegisterDialogue(string id, string speakerId, string text, float duration = 5f)
        {
            var entry = new CustomDialogueEntry
            {
                Id = id,
                SpeakerId = speakerId,
                Text = text,
                Duration = duration,
                NextDialogueId = null,
                Condition = null,
                GameSpeaker = GetGameSpeaker(speakerId)
            };

            customDialogues[id] = entry;
            TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Registered dialogue '{id}' for speaker '{speakerId}'");
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

                // For known game speakers, try to use voice
                // Voice keys follow pattern like "sparks_idle_01" for existing game VO
                // Our custom dialogue won't have voice, which is fine
                narrativeData.shortTextVOKey = ""; // No voice for custom dialogue

                // Trigger the dialogue
                dialoguePopup.OnTriggerNarrativeEntry(narrativeData);

                TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Triggered dialogue '{entry.Id}' via game UI");

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
}
