using System;
using System.Threading;
using System.Threading.Tasks;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microflow.FlowControl
{
    public static class MicroflowStep
    {
        /// <summary>
        /// Recursive step execution and sub-step can execute now calculations
        /// </summary>
        [FunctionName("ExecuteStep")]
        public static async Task ExecuteStep([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger inLog)
        {
            ProjectRun projectRun = context.GetInput<ProjectRun>();
            MicroflowContext microflowContext = null;
            EntityId projStateId = new EntityId("ProjectState", projectRun.ProjectName); 
            EntityId globalStateId = new EntityId("GlobalState", projectRun.RunObject.GlobalKey);

            try
            {
                Task<int> projStateTask = context.CallEntityAsync<int>(projStateId, "get");
                Task<int> globalSateTask = context.CallEntityAsync<int>(globalStateId, "get");
                int projState = await projStateTask;
                int globalState = await globalSateTask;

                if (projState == 0 && globalState == 0)
                {
                    microflowContext = new MicroflowContext(context, projectRun, inLog);

                    // call out to micro-services orchestration
                    await microflowContext.RunMicroflow();
                }
                else if (projState == 1 || globalState == 1)
                {
                    DateTime endDate = context.CurrentUtcDateTime.AddDays(7);
                    int count = 15;
                    int max = 60;

                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        try
                        {
                            while (context.CurrentUtcDateTime < endDate)
                            {
                                //context.SetCustomStatus("paused");

                                DateTime deadline = context.CurrentUtcDateTime.Add(TimeSpan.FromSeconds(count < max ? count : max));
                                await context.CreateTimer(deadline, cts.Token);
                                count++;

                                projStateTask = context.CallEntityAsync<int>(projStateId, "get");
                                globalSateTask = context.CallEntityAsync<int>(globalStateId, "get");
                                projState = await projStateTask;
                                globalState = await globalSateTask;

                                if (projState != 1 && globalState != 1)
                                {
                                    break;
                                }
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            inLog.LogCritical("========================TaskCanceledException==========================");
                        }
                        finally
                        {
                            cts.Dispose();
                        }
                    }

                    //context.SetCustomStatus("running");
                    if (projState == 0 && globalState == 0)
                    {
                        microflowContext = new MicroflowContext(context, projectRun, inLog);

                        // call out to micro-services orchestration
                        await microflowContext.RunMicroflow();
                    }
                }
            }
            catch (Exception e)
            {
                if (microflowContext != null)
                {
                    string stepNumber = microflowContext.HttpCallWithRetries == null ? "-2" : microflowContext.HttpCallWithRetries.RowKey;

                    // log to table workflow completed
                    LogErrorEntity errorEntity = new LogErrorEntity(projectRun?.ProjectName, Convert.ToInt32(stepNumber), e.Message, projectRun?.RunObject?.RunId);
                    await context.CallActivityAsync("LogError", errorEntity);

                    throw;
                }
            }
        }
    }
}
