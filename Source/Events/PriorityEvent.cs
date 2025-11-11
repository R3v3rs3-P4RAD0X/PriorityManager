using System;
using RimWorld;
using Verse;

namespace PriorityManager.Events
{
    /// <summary>
    /// Base class for all Priority Manager events
    /// </summary>
    public abstract class PriorityEvent
    {
        public int tick;
        public EventPriority priority;
        
        protected PriorityEvent(EventPriority priority)
        {
            this.tick = Find.TickManager.TicksGame;
            this.priority = priority;
        }
        
        public abstract string GetDescription();
    }
    
    /// <summary>
    /// Event priority levels (higher = processed first)
    /// </summary>
    public enum EventPriority
    {
        Low = 0,        // Job completion, low-impact changes
        Normal = 1,     // Skill changes, minor updates
        High = 2,       // Health changes, colonist state changes
        Critical = 3    // Colonist added/removed, game-changing events
    }
    
    // ============================================================================
    // COLONIST EVENTS
    // ============================================================================
    
    /// <summary>
    /// Fired when a colonist joins the colony
    /// </summary>
    public class ColonistAddedEvent : PriorityEvent
    {
        public Pawn pawn;
        
        public ColonistAddedEvent(Pawn pawn) : base(EventPriority.Critical)
        {
            this.pawn = pawn;
        }
        
        public override string GetDescription()
        {
            return $"Colonist added: {pawn?.Name?.ToStringShort ?? "Unknown"}";
        }
    }
    
    /// <summary>
    /// Fired when a colonist leaves the colony (death, kidnapped, etc.)
    /// </summary>
    public class ColonistRemovedEvent : PriorityEvent
    {
        public Pawn pawn;
        public string reason;
        
        public ColonistRemovedEvent(Pawn pawn, string reason = null) : base(EventPriority.Critical)
        {
            this.pawn = pawn;
            this.reason = reason;
        }
        
        public override string GetDescription()
        {
            return $"Colonist removed: {pawn?.Name?.ToStringShort ?? "Unknown"} ({reason ?? "unknown reason"})";
        }
    }
    
    /// <summary>
    /// Fired when a colonist's health changes significantly
    /// </summary>
    public class HealthChangedEvent : PriorityEvent
    {
        public Pawn pawn;
        public float oldHealthPercent;
        public float newHealthPercent;
        public bool becameIll;
        public bool recovered;
        
        public HealthChangedEvent(Pawn pawn, float oldHealth, float newHealth, bool becameIll = false, bool recovered = false) 
            : base(EventPriority.High)
        {
            this.pawn = pawn;
            this.oldHealthPercent = oldHealth;
            this.newHealthPercent = newHealth;
            this.becameIll = becameIll;
            this.recovered = recovered;
        }
        
        public override string GetDescription()
        {
            if (becameIll)
                return $"{pawn?.Name?.ToStringShort ?? "Colonist"} became ill/injured";
            if (recovered)
                return $"{pawn?.Name?.ToStringShort ?? "Colonist"} recovered";
            return $"{pawn?.Name?.ToStringShort ?? "Colonist"} health changed: {oldHealthPercent:P0} → {newHealthPercent:P0}";
        }
    }
    
    /// <summary>
    /// Fired when a colonist's skill level changes
    /// </summary>
    public class SkillChangedEvent : PriorityEvent
    {
        public Pawn pawn;
        public SkillDef skill;
        public int oldLevel;
        public int newLevel;
        
        public SkillChangedEvent(Pawn pawn, SkillDef skill, int oldLevel, int newLevel) 
            : base(EventPriority.Normal)
        {
            this.pawn = pawn;
            this.skill = skill;
            this.oldLevel = oldLevel;
            this.newLevel = newLevel;
        }
        
        public override string GetDescription()
        {
            return $"{pawn?.Name?.ToStringShort ?? "Colonist"} skill {skill?.label ?? "unknown"}: {oldLevel} → {newLevel}";
        }
    }
    
    /// <summary>
    /// Fired when a colonist becomes idle for extended period
    /// </summary>
    public class ColonistIdleEvent : PriorityEvent
    {
        public Pawn pawn;
        public int idleDuration;
        
        public ColonistIdleEvent(Pawn pawn, int idleDuration) : base(EventPriority.Normal)
        {
            this.pawn = pawn;
            this.idleDuration = idleDuration;
        }
        
        public override string GetDescription()
        {
            return $"{pawn?.Name?.ToStringShort ?? "Colonist"} idle for {idleDuration} ticks";
        }
    }
    
    // ============================================================================
    // WORK EVENTS
    // ============================================================================
    
    /// <summary>
    /// Fired when a work designation is added (construction, mining, etc.)
    /// </summary>
    public class JobDesignatedEvent : PriorityEvent
    {
        public WorkTypeDef workType;
        public IntVec3 location;
        public Map map;
        
        public JobDesignatedEvent(WorkTypeDef workType, IntVec3 location, Map map) 
            : base(EventPriority.Low)
        {
            this.workType = workType;
            this.location = location;
            this.map = map;
        }
        
        public override string GetDescription()
        {
            return $"Job designated: {workType?.label ?? "Unknown"} at {location}";
        }
    }
    
    /// <summary>
    /// Fired when a work item is completed
    /// </summary>
    public class WorkCompletedEvent : PriorityEvent
    {
        public Pawn pawn;
        public WorkTypeDef workType;
        
        public WorkCompletedEvent(Pawn pawn, WorkTypeDef workType) : base(EventPriority.Low)
        {
            this.pawn = pawn;
            this.workType = workType;
        }
        
        public override string GetDescription()
        {
            return $"{pawn?.Name?.ToStringShort ?? "Colonist"} completed {workType?.label ?? "work"}";
        }
    }
    
    /// <summary>
    /// Fired when role assignment changes (manual or via UI)
    /// </summary>
    public class RoleChangedEvent : PriorityEvent
    {
        public Pawn pawn;
        public RolePreset oldRole;
        public RolePreset newRole;
        public string customRoleId;
        
        public RoleChangedEvent(Pawn pawn, RolePreset oldRole, RolePreset newRole, string customRoleId = null) 
            : base(EventPriority.High)
        {
            this.pawn = pawn;
            this.oldRole = oldRole;
            this.newRole = newRole;
            this.customRoleId = customRoleId;
        }
        
        public override string GetDescription()
        {
            return $"{pawn?.Name?.ToStringShort ?? "Colonist"} role changed: {oldRole} → {newRole}";
        }
    }
    
    /// <summary>
    /// Fired when settings change that affect assignments
    /// </summary>
    public class SettingsChangedEvent : PriorityEvent
    {
        public string settingName;
        public object oldValue;
        public object newValue;
        
        public SettingsChangedEvent(string settingName, object oldValue, object newValue) 
            : base(EventPriority.Normal)
        {
            this.settingName = settingName;
            this.oldValue = oldValue;
            this.newValue = newValue;
        }
        
        public override string GetDescription()
        {
            return $"Setting changed: {settingName}";
        }
    }
    
    /// <summary>
    /// Fired to request full recalculation (manual trigger)
    /// </summary>
    public class RecalculateRequestEvent : PriorityEvent
    {
        public bool force;
        public Pawn specificPawn;
        
        public RecalculateRequestEvent(bool force = false, Pawn specificPawn = null) 
            : base(EventPriority.High)
        {
            this.force = force;
            this.specificPawn = specificPawn;
        }
        
        public override string GetDescription()
        {
            if (specificPawn != null)
                return $"Recalculate request for {specificPawn.Name.ToStringShort}";
            return force ? "Force recalculate all" : "Recalculate all";
        }
    }
}

