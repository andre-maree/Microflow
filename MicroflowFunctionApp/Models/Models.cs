﻿namespace Microflow
{
    #region POCOs

    /// <summary>
    /// This is the minimalist project object that is passed during execution
    /// </summary>
    public class ProjectRun
    {
        public string ProjectId { get; set; }
        public RunObject RunObject { get; set; }
        public int PausedStepId { get; set; }
        public int Loop { get; set; } = 1;
        public int CurrentLoop { get; set; } = 1;
        public bool DoneContainingStep { get; set; }
    }

    /// <summary>
    /// Used when locking for parent step concurrency count checking
    /// </summary>
    public class CanExecuteNowObject
    {
        public string RunId { get; set; }
        public int StepId { get; set; }
        public int ParentCount { get; set; }
        public string ProjectId { get; set; }
    }

    /// <summary>
    /// This is the minimalist run object that is passed during execution inside the project run object
    /// </summary>
    public class RunObject
    {
        public string RunId { get; set; }
        public int StepId { get; set; }
    }

    #endregion
}