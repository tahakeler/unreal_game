namespace HarvestPanic.Core
{
    /// <summary>
    /// Run save / continue (save-persistence). Provider: Economy (registered at startup, survives scenes);
    /// consumer: Frontend UI main menu "Continue Farm". Add-only contract.
    /// </summary>
    public interface IRunSaveService
    {
        /// <summary>True when a loadable host run save exists on this machine.</summary>
        bool HasSave { get; }

        /// <summary>Short human summary, e.g. "Week 2 · WED · $340".</summary>
        string Summary { get; }

        /// <summary>When true, the next run the host starts loads the save instead of Week 1.</summary>
        bool ContinueRequested { get; set; }

        /// <summary>Deletes the save (abandon).</summary>
        void DeleteSave();
    }
}
