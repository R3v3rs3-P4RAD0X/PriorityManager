using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PriorityManager.Events
{
    /// <summary>
    /// Central event dispatcher for Priority Manager v2.0
    /// Manages event queues and notifies subscribers
    /// </summary>
    public class EventDispatcher
    {
        private static EventDispatcher instance;
        
        // Event queues by priority
        private List<PriorityEvent> criticalQueue = new List<PriorityEvent>();
        private List<PriorityEvent> highQueue = new List<PriorityEvent>();
        private List<PriorityEvent> normalQueue = new List<PriorityEvent>();
        private List<PriorityEvent> lowQueue = new List<PriorityEvent>();
        
        // Subscribers by event type
        private Dictionary<Type, List<Action<PriorityEvent>>> subscribers = new Dictionary<Type, List<Action<PriorityEvent>>>();
        
        // Statistics
        private int totalEventsProcessed = 0;
        private int eventsThisTick = 0;
        private Dictionary<Type, int> eventCounts = new Dictionary<Type, int>();
        
        // Configuration
        private const int MAX_EVENTS_PER_TICK = 50; // Prevent lag spikes
        private bool enabled = true;
        
        public static EventDispatcher Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new EventDispatcher();
                }
                return instance;
            }
        }
        
        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }
        
        private EventDispatcher()
        {
            Log.Message("[PriorityManager] EventDispatcher initialized");
        }
        
        /// <summary>
        /// Subscribe to a specific event type
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : PriorityEvent
        {
            Type eventType = typeof(T);
            
            if (!subscribers.ContainsKey(eventType))
            {
                subscribers[eventType] = new List<Action<PriorityEvent>>();
            }
            
            // Wrap the typed handler in a generic Action<PriorityEvent>
            subscribers[eventType].Add(e => handler((T)e));
        }
        
        /// <summary>
        /// Unsubscribe from a specific event type
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler) where T : PriorityEvent
        {
            Type eventType = typeof(T);
            
            if (subscribers.ContainsKey(eventType))
            {
                subscribers[eventType].RemoveAll(h => h.Target == handler.Target);
            }
        }
        
        /// <summary>
        /// Dispatch an event to the appropriate queue
        /// </summary>
        public void Dispatch(PriorityEvent evt)
        {
            if (!enabled || evt == null)
                return;
            
            using (PerformanceProfiler.Profile("EventDispatcher.Dispatch"))
            {
                switch (evt.priority)
                {
                    case EventPriority.Critical:
                        criticalQueue.Add(evt);
                        break;
                    case EventPriority.High:
                        highQueue.Add(evt);
                        break;
                    case EventPriority.Normal:
                        normalQueue.Add(evt);
                        break;
                    case EventPriority.Low:
                        lowQueue.Add(evt);
                        break;
                }
                
                // Track event type
                Type eventType = evt.GetType();
                if (!eventCounts.ContainsKey(eventType))
                {
                    eventCounts[eventType] = 0;
                }
                eventCounts[eventType]++;
            }
        }
        
        /// <summary>
        /// Process queued events (call once per tick)
        /// </summary>
        public void ProcessEvents()
        {
            if (!enabled)
                return;
            
            using (PerformanceProfiler.Profile("EventDispatcher.ProcessEvents"))
            {
                eventsThisTick = 0;
                
                // Process in priority order: Critical → High → Normal → Low
                ProcessQueue(criticalQueue);
                
                // Only process lower priority if we haven't hit the limit
                if (eventsThisTick < MAX_EVENTS_PER_TICK)
                    ProcessQueue(highQueue);
                
                if (eventsThisTick < MAX_EVENTS_PER_TICK)
                    ProcessQueue(normalQueue);
                
                if (eventsThisTick < MAX_EVENTS_PER_TICK)
                    ProcessQueue(lowQueue);
                
                // If queues are backing up, log warning
                int totalQueued = GetQueuedEventCount();
                if (totalQueued > MAX_EVENTS_PER_TICK * 2)
                {
                    Log.Warning($"[PriorityManager] Event queue backing up: {totalQueued} events queued");
                }
            }
        }
        
        private void ProcessQueue(List<PriorityEvent> queue)
        {
            if (queue.Count == 0)
                return;
            
            // Process events in FIFO order
            int processed = 0;
            while (queue.Count > 0 && eventsThisTick < MAX_EVENTS_PER_TICK)
            {
                PriorityEvent evt = queue[0];
                queue.RemoveAt(0);
                
                ProcessEvent(evt);
                processed++;
                eventsThisTick++;
                totalEventsProcessed++;
            }
        }
        
        private void ProcessEvent(PriorityEvent evt)
        {
            Type eventType = evt.GetType();
            
            if (subscribers.ContainsKey(eventType))
            {
                var handlers = subscribers[eventType];
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler(evt);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[PriorityManager] Error processing event {evt.GetDescription()}: {ex}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Get total number of queued events
        /// </summary>
        public int GetQueuedEventCount()
        {
            return criticalQueue.Count + highQueue.Count + normalQueue.Count + lowQueue.Count;
        }
        
        /// <summary>
        /// Get statistics about event processing
        /// </summary>
        public string GetStatistics()
        {
            int queued = GetQueuedEventCount();
            return $"Processed: {totalEventsProcessed}, Queued: {queued}, This tick: {eventsThisTick}";
        }
        
        /// <summary>
        /// Get event type breakdown
        /// </summary>
        public Dictionary<string, int> GetEventTypeBreakdown()
        {
            var breakdown = new Dictionary<string, int>();
            foreach (var kvp in eventCounts)
            {
                breakdown[kvp.Key.Name] = kvp.Value;
            }
            return breakdown;
        }
        
        /// <summary>
        /// Clear all queues (use when loading save, etc.)
        /// </summary>
        public void ClearQueues()
        {
            criticalQueue.Clear();
            highQueue.Clear();
            normalQueue.Clear();
            lowQueue.Clear();
            eventsThisTick = 0;
        }
        
        /// <summary>
        /// Reset all statistics
        /// </summary>
        public void ResetStatistics()
        {
            totalEventsProcessed = 0;
            eventsThisTick = 0;
            eventCounts.Clear();
        }
        
        /// <summary>
        /// Check if there are any subscribers for an event type
        /// </summary>
        public bool HasSubscribers<T>() where T : PriorityEvent
        {
            Type eventType = typeof(T);
            return subscribers.ContainsKey(eventType) && subscribers[eventType].Count > 0;
        }
        
        /// <summary>
        /// Get count of subscribers for an event type
        /// </summary>
        public int GetSubscriberCount<T>() where T : PriorityEvent
        {
            Type eventType = typeof(T);
            return subscribers.ContainsKey(eventType) ? subscribers[eventType].Count : 0;
        }
    }
}

