using System;
using System.Threading.Tasks;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microflow.FlowControl
{
    public static class Microflow
    {
        /// <summary>
        /// Recursive step execution and sub-step can execute now calculations
        /// </summary>
        [FunctionName("ExecuteStep")]
        public static async Task ExecuteStep([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger inLog)
        {
            ProjectRun projectRun = context.GetInput<ProjectRun>();
            MicroflowContext microflowContext = null;
            try
            {
                microflowContext = new MicroflowContext(context, projectRun, inLog);

                // call out to micro-services orchestration
                await microflowContext.RunMicroflow();

                // TODO: project stop, pause and continue
                //var stateTask = context.CallActivityAsync<int>("GetState", project.ProjectName);
                //var state = await stateTask;
                //if (state == 2)
                //{
                //    //var projectControlEnt = await context.CallActivityAsync<ProjectControlEntity>("GetProjectControl", project.ProjectName);

                //    // wait for external event
                //    await Task.Delay(30000);
                //}

            }
            catch (Exception e)
            {
                if(microflowContext != null)
                {

                    int? stepId = microflowContext.HttpCallWithRetries == null ? -1 : Convert.ToInt32(microflowContext.HttpCallWithRetries.RowKey);

                    // log to table workflow completed
                    LogErrorEntity errorEntity = new LogErrorEntity(projectRun.ProjectName, e.Message, projectRun.RunObject.RunId, stepId);
                    await context.CallActivityAsync("LogError", errorEntity);
                }
            }
        }
    }
}
