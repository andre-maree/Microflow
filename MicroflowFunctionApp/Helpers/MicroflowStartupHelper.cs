using Microflow.Models;
using System;
using System.Collections.Specialized;

namespace Microflow.Helpers
{
    public static class MicroflowStartupHelper
    {
        /// <summary>
        /// Create a new ProjectRun for stratup, set GlobalKey
        /// </summary>
        public static ProjectRun CreateStartupProjectRun(NameValueCollection data,
                                                         ref string instanceId,
                                                         string projectName)
        {
            var input = new
            {
                Loop = Convert.ToInt32(data["loop"]),
                GlobalKey = data["globalkey"]
            };

            // create a project run
            ProjectRun projectRun = new ProjectRun()
            {
                ProjectName = projectName,
                Loop = input.Loop != 0
                ? input.Loop
                : 1
            };

            // create a new run object
            RunObject runObj = new RunObject() { StepNumber = "-1" };
            projectRun.RunObject = runObj;

            // instanceId is set/singleton
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                // globalKey is set
                if (!string.IsNullOrWhiteSpace(input.GlobalKey))
                {
                    runObj.GlobalKey = input.GlobalKey;
                }
                else
                {
                    runObj.GlobalKey = Guid.NewGuid().ToString();
                }
            }
            // instanceId is not set/multiple concurrent instances
            else
            {
                instanceId = Guid.NewGuid().ToString();
                // globalKey is set
                if (!string.IsNullOrWhiteSpace(input.GlobalKey))
                {
                    runObj.GlobalKey = input.GlobalKey;
                }
                else
                {
                    runObj.GlobalKey = instanceId;
                }
            }

            projectRun.RunObject.StepNumber = "-1";
            projectRun.OrchestratorInstanceId = instanceId;

            return projectRun;
        }
    }
}
