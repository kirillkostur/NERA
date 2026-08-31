namespace NERA.Development
{
    /// <summary>
    /// Implemented by runtime systems with a player-visible timed progress.
    /// Developer cheats use this contract to finish only currently active work.
    /// </summary>
    public interface IDeveloperProgressSkippable
    {
        bool CompleteActiveProgressForDebug();
    }
}
