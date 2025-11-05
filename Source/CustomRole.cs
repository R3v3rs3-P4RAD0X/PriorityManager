using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PriorityManager
{
    /// <summary>
    /// Represents a single job entry in a custom role with its importance level
    /// </summary>
    public class CustomRoleJobEntry : IExposable
    {
        public string workTypeDefName;
        public JobImportance importance = JobImportance.Normal;
        public int sortOrder = 0;

        public CustomRoleJobEntry()
        {
        }

        public CustomRoleJobEntry(WorkTypeDef workType, JobImportance importance, int sortOrder)
        {
            this.workTypeDefName = workType.defName;
            this.importance = importance;
            this.sortOrder = sortOrder;
        }

        public CustomRoleJobEntry(string defName, JobImportance importance, int sortOrder)
        {
            this.workTypeDefName = defName;
            this.importance = importance;
            this.sortOrder = sortOrder;
        }

        public WorkTypeDef GetWorkTypeDef()
        {
            return DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref workTypeDefName, "workTypeDefName");
            Scribe_Values.Look(ref importance, "importance", JobImportance.Normal);
            Scribe_Values.Look(ref sortOrder, "sortOrder", 0);
        }
    }

    /// <summary>
    /// Represents a user-defined custom role with ordered job priorities
    /// </summary>
    public class CustomRole : IExposable
    {
        public string roleId;
        public string roleName;
        public List<CustomRoleJobEntry> jobs = new List<CustomRoleJobEntry>();

        public CustomRole()
        {
            this.roleId = Guid.NewGuid().ToString();
            this.roleName = "New Role";
            this.jobs = new List<CustomRoleJobEntry>();
        }

        public CustomRole(string name)
        {
            this.roleId = Guid.NewGuid().ToString();
            this.roleName = name;
            this.jobs = new List<CustomRoleJobEntry>();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref roleId, "roleId");
            Scribe_Values.Look(ref roleName, "roleName", "New Role");
            Scribe_Collections.Look(ref jobs, "jobs", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (jobs == null)
                    jobs = new List<CustomRoleJobEntry>();
                if (string.IsNullOrEmpty(roleId))
                    roleId = Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// Add a job to the role
        /// </summary>
        public void AddJob(WorkTypeDef workType, JobImportance importance)
        {
            if (HasJob(workType))
                return;

            int nextOrder = jobs.Count > 0 ? jobs.Max(j => j.sortOrder) + 1 : 0;
            jobs.Add(new CustomRoleJobEntry(workType, importance, nextOrder));
        }

        /// <summary>
        /// Remove a job from the role
        /// </summary>
        public void RemoveJob(string workTypeDefName)
        {
            jobs.RemoveAll(j => j.workTypeDefName == workTypeDefName);
            ReorderJobs();
        }

        /// <summary>
        /// Check if role already has this job
        /// </summary>
        public bool HasJob(WorkTypeDef workType)
        {
            return jobs.Any(j => j.workTypeDefName == workType.defName);
        }

        /// <summary>
        /// Move a job from one position to another
        /// </summary>
        public void MoveJob(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= jobs.Count || toIndex < 0 || toIndex >= jobs.Count)
                return;

            var job = jobs[fromIndex];
            jobs.RemoveAt(fromIndex);
            jobs.Insert(toIndex, job);
            ReorderJobs();
        }

        /// <summary>
        /// Reorder jobs to have sequential sortOrder values
        /// </summary>
        private void ReorderJobs()
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                jobs[i].sortOrder = i;
            }
        }

        /// <summary>
        /// Get jobs sorted by their order
        /// </summary>
        public List<CustomRoleJobEntry> GetSortedJobs()
        {
            return jobs.OrderBy(j => j.sortOrder).ToList();
        }

        /// <summary>
        /// Validate that the role has at least one job
        /// </summary>
        public bool IsValid()
        {
            return jobs.Count > 0 && !string.IsNullOrEmpty(roleName);
        }

        /// <summary>
        /// Get a display-friendly summary of the role
        /// </summary>
        public string GetSummary()
        {
            if (jobs.Count == 0)
                return "No jobs assigned";
            
            return $"{jobs.Count} job(s)";
        }

        /// <summary>
        /// Clone this role with a new ID
        /// </summary>
        public CustomRole Clone()
        {
            var clone = new CustomRole(roleName + " (Copy)");
            foreach (var job in jobs)
            {
                clone.jobs.Add(new CustomRoleJobEntry(job.workTypeDefName, job.importance, job.sortOrder));
            }
            return clone;
        }
    }
}

