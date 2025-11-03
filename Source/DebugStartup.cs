using Verse;

namespace PriorityManager
{
    [StaticConstructorOnStartup]
    public static class DebugStartup
    {
        static DebugStartup()
        {
            Log.Error("===========================================");
            Log.Error("PRIORITY MANAGER DEBUG: Assembly loaded!");
            Log.Error("PRIORITY MANAGER DEBUG: Static constructor ran!");
            Log.Error("===========================================");
        }
    }
}

