using System;
using System.Collections.Generic;
using TechtonicaFramework.Core;
using UnityEngine;

namespace TechtonicaFramework.Narrative
{
    /// <summary>
    /// Narrative Module - Provides dialogue triggering, quest creation, and speaker management.
    /// </summary>
    public class NarrativeModule : IFrameworkModule
    {
        public bool IsActive { get; private set; }

        // Custom dialogue entries
        private Dictionary<string, CustomDialogueEntry> customDialogues = new Dictionary<string, CustomDialogueEntry>();

        // Custom speakers
        private Dictionary<string, CustomSpeaker> customSpeakers = new Dictionary<string, CustomSpeaker>();

        // Dialogue queue
        private Queue<string> dialogueQueue = new Queue<string>();

        // Track played dialogues
        private HashSet<string> playedDialogues = new HashSet<string>();

        public void Initialize()
        {
            IsActive = true;
            TechtonicaFrameworkPlugin.LogDebug("NarrativeModule: Initialized");
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
            dialogueQueue.Clear();
            TechtonicaFrameworkPlugin.LogDebug("NarrativeModule: Shutdown");
        }

        #region Custom Speaker Registration

        /// <summary>
        /// Register a custom speaker for dialogue.
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
        /// Get all registered custom speakers.
        /// </summary>
        public List<CustomSpeaker> GetAllSpeakers()
        {
            return new List<CustomSpeaker>(customSpeakers.Values);
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
                Condition = null
            };

            customDialogues[id] = entry;
            TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Registered dialogue '{id}'");
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
        /// Trigger a dialogue to play.
        /// </summary>
        public void TriggerDialogue(string dialogueId)
        {
            if (!customDialogues.TryGetValue(dialogueId, out var entry))
            {
                TechtonicaFrameworkPlugin.LogWarning($"NarrativeModule: Dialogue '{dialogueId}' not found");
                return;
            }

            // Check condition if set
            if (entry.Condition != null && !entry.Condition())
            {
                TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Dialogue '{dialogueId}' condition not met");
                return;
            }

            // Display the dialogue
            DisplayDialogue(entry);

            // Mark as played
            playedDialogues.Add(dialogueId);
            FrameworkEvents.RaiseDialogueStarted(dialogueId);

            // Queue next dialogue if chained
            if (!string.IsNullOrEmpty(entry.NextDialogueId))
            {
                QueueDialogue(entry.NextDialogueId, entry.Duration);
            }
        }

        /// <summary>
        /// Queue a dialogue to play after a delay.
        /// </summary>
        public void QueueDialogue(string dialogueId, float delay)
        {
            // TODO: Implement delayed dialogue triggering with coroutine or timer
            TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Queued dialogue '{dialogueId}' with {delay}s delay");
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

        private void DisplayDialogue(CustomDialogueEntry entry)
        {
            // Get speaker info
            var speaker = GetSpeaker(entry.SpeakerId);
            string speakerName = speaker?.DisplayName ?? entry.SpeakerId;

            TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: [{speakerName}] {entry.Text}");

            // TODO: Hook into the game's DialogueEntryPopupUI to display dialogue
            // This would require:
            // 1. Creating a NarrativeEntryData instance
            // 2. Calling the dialogue popup system
            // 3. Playing voice if available

            // For now, we can use the notification system as a fallback
            try
            {
                if (UIManager.instance != null)
                {
                    // Try to show via game notification system
                    // UIManager.instance.systemLog.AddNotification($"[{speakerName}] {entry.Text}");
                }
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.LogDebug($"NarrativeModule: Could not display dialogue via UI: {ex.Message}");
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
            // TODO: Hook into quest system to trigger dialogues
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
        public string VoiceKey; // For FMOD voice playback if available
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
