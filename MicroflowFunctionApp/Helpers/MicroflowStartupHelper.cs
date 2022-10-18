using MicroflowModels;
using System;
using System.Collections.Specialized;

namespace Microflow.Helpers
{
    public static class MicroflowStartupHelper
    {
        /// <summary>
        /// Create a new workflowRun for stratup, set GlobalKey
        /// </summary>
        public static MicroflowRun CreateStartupRun(NameValueCollection data,
                                                         ref string instanceId,
                                                         string workflowName)
        {
            var input = new
            {
                Loop = Convert.ToInt32(data["loop"]),
                GlobalKey = data["globalkey"]
            };

            // create a workflow run
            MicroflowRun workflowRun = new()
            {
                WorkflowName = workflowName,
                Loop = input.Loop != 0
                ? input.Loop
                : 1
            };

            // create a new run object
            RunObject runObj = new();
            workflowRun.RunObject = runObj;

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

            //workflowRun.RunObject.StepNumber = "-1";
            workflowRun.OrchestratorInstanceId = instanceId;

            return workflowRun;
        }
    }
}
