using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PriorityManager.ML
{
    /// <summary>
    /// Simple neural network for predicting optimal priority assignments
    /// v2.0: Learns from player's manual adjustments to improve auto-assignment
    /// </summary>
    public class AssignmentPredictor
    {
        private static AssignmentPredictor instance;
        
        // Training data
        private List<TrainingSample> trainingData = new List<TrainingSample>();
        private const int MAX_TRAINING_SAMPLES = 500;
        
        // Learned weights (simplified neural network)
        private Dictionary<string, float> featureWeights = new Dictionary<string, float>();
        private float learningRate = 0.1f;
        
        // Statistics
        private int totalPredictions = 0;
        private int correctPredictions = 0;
        private bool modelTrained = false;
        
        private class TrainingSample
        {
            public Dictionary<string, float> features;
            public WorkTypeDef assignedWork;
            public int priority;
            public bool wasManualOverride;
            public int tick;
        }
        
        public static AssignmentPredictor Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new AssignmentPredictor();
                }
                return instance;
            }
        }
        
        private AssignmentPredictor()
        {
            InitializeWeights();
            Log.Message("[PriorityManager] AssignmentPredictor initialized");
        }
        
        /// <summary>
        /// Record a manual priority adjustment (player override)
        /// </summary>
        public void RecordManualAdjustment(Pawn pawn, WorkTypeDef workType, int oldPriority, int newPriority)
        {
            using (PerformanceProfiler.Profile("AssignmentPredictor.RecordManual"))
            {
                if (pawn == null || workType == null)
                    return;
                
                var features = ExtractFeatures(pawn, workType);
                
                var sample = new TrainingSample
                {
                    features = features,
                    assignedWork = workType,
                    priority = newPriority,
                    wasManualOverride = true,
                    tick = Find.TickManager.TicksGame
                };
                
                trainingData.Add(sample);
                
                // Trim old samples
                if (trainingData.Count > MAX_TRAINING_SAMPLES)
                {
                    trainingData.RemoveAt(0);
                }
                
                // Retrain if enough data
                if (trainingData.Count >= 20 && trainingData.Count % 10 == 0)
                {
                    TrainModel();
                }
            }
        }
        
        /// <summary>
        /// Predict priority for pawn-worktype pair
        /// </summary>
        public int PredictPriority(Pawn pawn, WorkTypeDef workType)
        {
            using (PerformanceProfiler.Profile("AssignmentPredictor.PredictPriority"))
            {
                totalPredictions++;
                
                if (!modelTrained || trainingData.Count < 20)
                {
                    // Not enough data - use default
                    return 2; // Normal priority
                }
                
                var features = ExtractFeatures(pawn, workType);
                float score = ComputeScore(features);
                
                // Convert score to priority (1-4 range)
                int priority = ScoreToPriority(score);
                
                return priority;
            }
        }
        
        /// <summary>
        /// Suggest optimal assignment for a colonist
        /// </summary>
        public WorkTypeDef SuggestWork(Pawn pawn)
        {
            using (PerformanceProfiler.Profile("AssignmentPredictor.SuggestWork"))
            {
                if (!modelTrained || trainingData.Count < 20)
                    return null;
                
                var visibleWorkTypes = WorkTypeCache.VisibleWorkTypes;
                WorkTypeDef bestWork = null;
                float bestScore = float.MinValue;
                
                foreach (var workType in visibleWorkTypes)
                {
                    if (pawn.WorkTypeIsDisabled(workType))
                        continue;
                    
                    var features = ExtractFeatures(pawn, workType);
                    float score = ComputeScore(features);
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestWork = workType;
                    }
                }
                
                return bestWork;
            }
        }
        
        private Dictionary<string, float> ExtractFeatures(Pawn pawn, WorkTypeDef workType)
        {
            var features = new Dictionary<string, float>();
            
            // Skill-related features
            var relevantSkills = WorkTypeCache.GetRelevantSkills(workType);
            float avgSkill = 0f;
            float maxSkill = 0f;
            
            if (relevantSkills.Count > 0 && pawn.skills != null)
            {
                foreach (var skillDef in relevantSkills)
                {
                    var skill = pawn.skills.GetSkill(skillDef);
                    if (skill != null)
                    {
                        avgSkill += skill.Level;
                        maxSkill = Math.Max(maxSkill, skill.Level);
                    }
                }
                avgSkill /= relevantSkills.Count;
            }
            
            features["avgSkill"] = avgSkill / 20f; // Normalize 0-1
            features["maxSkill"] = maxSkill / 20f;
            
            // Passion
            float passion = 0f;
            if (relevantSkills.Count > 0 && pawn.skills != null)
            {
                foreach (var skillDef in relevantSkills)
                {
                    var skill = pawn.skills.GetSkill(skillDef);
                    if (skill != null)
                    {
                        passion += (int)skill.passion * 0.5f;
                    }
                }
                passion /= relevantSkills.Count;
            }
            features["passion"] = passion;
            
            // Health
            features["health"] = pawn.health?.summaryHealth.SummaryHealthPercent ?? 1f;
            
            // Current job count
            int currentJobs = 0;
            if (pawn.workSettings != null)
            {
                foreach (var wt in WorkTypeCache.VisibleWorkTypes)
                {
                    if (pawn.workSettings.GetPriority(wt) > 0)
                        currentJobs++;
                }
            }
            features["currentJobs"] = currentJobs / (float)WorkTypeCache.VisibleCount;
            
            // Work type importance (from settings)
            var settings = PriorityManagerMod.settings;
            JobImportance importance = settings.GetJobImportance(workType);
            features["importance"] = (int)importance / 5f; // Normalize 0-1
            
            return features;
        }
        
        private float ComputeScore(Dictionary<string, float> features)
        {
            float score = 0f;
            
            foreach (var kvp in features)
            {
                if (featureWeights.TryGetValue(kvp.Key, out float weight))
                {
                    score += kvp.Value * weight;
                }
            }
            
            return score;
        }
        
        private int ScoreToPriority(float score)
        {
            // Map score to priority 1-4
            if (score > 0.8f) return 1;
            if (score > 0.5f) return 2;
            if (score > 0.2f) return 3;
            return 4;
        }
        
        private void TrainModel()
        {
            using (PerformanceProfiler.Profile("AssignmentPredictor.TrainModel"))
            {
                if (trainingData.Count < 20)
                    return;
                
                Log.Message($"[AssignmentPredictor] Training model on {trainingData.Count} samples...");
                
                // Simple gradient descent
                int iterations = 10;
                for (int iter = 0; iter < iterations; iter++)
                {
                    foreach (var sample in trainingData)
                    {
                        float prediction = ComputeScore(sample.features);
                        float target = (5 - sample.priority) / 4f; // Convert priority to 0-1 score
                        float error = target - prediction;
                        
                        // Update weights
                        foreach (var kvp in sample.features)
                        {
                            if (!featureWeights.ContainsKey(kvp.Key))
                                featureWeights[kvp.Key] = 0f;
                            
                            featureWeights[kvp.Key] += learningRate * error * kvp.Value;
                        }
                    }
                }
                
                modelTrained = true;
                Log.Message($"[AssignmentPredictor] Model trained. Weights: {string.Join(", ", featureWeights.Select(kvp => $"{kvp.Key}={kvp.Value:F2}"))}");
            }
        }
        
        private void InitializeWeights()
        {
            // Initialize with reasonable defaults
            featureWeights["avgSkill"] = 1.0f;
            featureWeights["maxSkill"] = 0.8f;
            featureWeights["passion"] = 0.6f;
            featureWeights["health"] = 0.4f;
            featureWeights["currentJobs"] = -0.3f; // Negative = prefer colonists with fewer jobs
            featureWeights["importance"] = 0.5f;
        }
        
        /// <summary>
        /// Get accuracy of predictions
        /// </summary>
        public float GetAccuracy()
        {
            if (totalPredictions == 0)
                return 0f;
            
            return correctPredictions / (float)totalPredictions;
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public string GetStatistics()
        {
            float accuracy = GetAccuracy() * 100f;
            return $"Training samples: {trainingData.Count}, Predictions: {totalPredictions}, Accuracy: {accuracy:F1}%, Trained: {modelTrained}";
        }
        
        /// <summary>
        /// Clear all training data
        /// </summary>
        public void Clear()
        {
            trainingData.Clear();
            totalPredictions = 0;
            correctPredictions = 0;
            modelTrained = false;
            InitializeWeights();
        }
        
        /// <summary>
        /// Export training data for analysis
        /// </summary>
        public void ExportTrainingData(string filepath)
        {
            try
            {
                using (var writer = new System.IO.StreamWriter(filepath))
                {
                    // Header
                    writer.WriteLine("Tick,WorkType,Priority,ManualOverride,AvgSkill,MaxSkill,Passion,Health,CurrentJobs,Importance");
                    
                    // Data
                    foreach (var sample in trainingData)
                    {
                        sample.features.TryGetValue("avgSkill", out float avgSkill);
                        sample.features.TryGetValue("maxSkill", out float maxSkill);
                        sample.features.TryGetValue("passion", out float passion);
                        sample.features.TryGetValue("health", out float health);
                        sample.features.TryGetValue("currentJobs", out float currentJobs);
                        sample.features.TryGetValue("importance", out float importance);
                        
                        writer.WriteLine($"{sample.tick},{sample.assignedWork.defName},{sample.priority},{sample.wasManualOverride}," +
                            $"{avgSkill:F3},{maxSkill:F3},{passion:F3},{health:F3},{currentJobs:F3},{importance:F3}");
                    }
                }
                
                Log.Message($"[AssignmentPredictor] Exported {trainingData.Count} samples to {filepath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[AssignmentPredictor] Failed to export training data: {ex}");
            }
        }
    }
}

