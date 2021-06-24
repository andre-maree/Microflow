//using System;
//using System.Threading.Tasks;
//using Microsoft.Azure.Cosmos.Table;
//using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.DurableTask;
//using Microsoft.Extensions.Logging;

namespace Microflow
{
    //public static class ActivityCanExecuteNow_old
    //{
    //    [FunctionName("CanExecuteNowold")]
    //    public static async Task<bool> CanExecuteNowold([ActivityTrigger] CanExecuteNowObject canExecuteNowObject, ILogger log)
    //    {
    //        // get the cloud table
    //        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
    //        CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
    //        CloudTable table = tableClient.GetTableReference($"RunControl{canExecuteNowObject.ProjectName}");

    //        // get countEnt for step
    //        var parentCountCompletedEnt = await GetCountEnt(table, canExecuteNowObject.RunId, canExecuteNowObject.StepId);

    //        // try to insert a new countEnt if its null
    //        if (parentCountCompletedEnt == null)
    //        {
    //            parentCountCompletedEnt = new ParentCountCompletedEntity(canExecuteNowObject.RunId, canExecuteNowObject.StepId.ToString()) { ParentCountCompleted = 0 };

    //            var insertTask = await TryInsert(table, parentCountCompletedEnt);

    //            // new counEnt inserted, return false because this is parent 1 completing
    //            if (insertTask)
    //            {
    //                return false;
    //            }
    //            // false returned means a conflict, already inserted, retry
    //            else
    //            {
    //                await Task.Delay(1500);
    //                return await CanExecuteNowold(canExecuteNowObject, log);
    //            }
    //        }
    //        // countEnt already inserted, update the count
    //        else
    //        {
    //            // if only 1 more parent, now it can execute
    //            if (parentCountCompletedEnt.ParentCountCompleted + 1 >= canExecuteNowObject.ParentCount)
    //            {
    //                return true;
    //            }
    //            // more parents must complete, update the count
    //            else
    //            {
    //                return await UpdateCount(table, parentCountCompletedEnt, canExecuteNowObject.RunId, canExecuteNowObject.StepId, canExecuteNowObject.ParentCount);
    //            }
    //        }
    //    }

    //    private static async Task<bool> TryInsert(CloudTable table, ParentCountCompletedEntity parentCountCompletedEnt)
    //    {
    //        TableOperation op = TableOperation.Insert(parentCountCompletedEnt);

    //        try
    //        {
    //            parentCountCompletedEnt.ParentCountCompleted = 1;

    //            await table.ExecuteAsync(op);

    //            return true;
    //        }
    //        catch (StorageException ex)
    //        {
    //            if (ex.RequestInformation.HttpStatusCode == 409)
    //            {
    //                return false;
    //            }

    //            throw;
    //        }
    //    }
    //    private static async Task<bool> UpdateCount(CloudTable table, ParentCountCompletedEntity parentCountCompletedEnt, string runId, int stepId, int parentCount)
    //    {
    //        bool res = false;
    //        try
    //        {
    //            // if only 1 more parent, now it can execute
    //            if (parentCountCompletedEnt.ParentCountCompleted + 1 >= parentCount)
    //            {
    //                res = true;
    //            }
    //            else
    //            {
    //                // else update count
    //                parentCountCompletedEnt.ParentCountCompleted++;
    //                TableOperation op = TableOperation.Merge(parentCountCompletedEnt);

    //                await table.ExecuteAsync(op);
    //            }
    //        }
    //        catch (StorageException ex)
    //        {
    //            // if conflict, refresh countEnt and retry
    //            if (ex.RequestInformation.HttpStatusCode == 412)
    //            {
    //                // randomize a bit to try avoid conflict
    //                Random ran = new Random();
    //                await Task.Delay(ran.Next(1500, 5000));

    //                // refresh the count in the table ent
    //                parentCountCompletedEnt = await GetCountEnt(table, runId, stepId);

    //                // if only 1 more parent, now it can execute
    //                if (parentCountCompletedEnt.ParentCountCompleted + 1 >= parentCount)
    //                {
    //                    res = true;
    //                }
    //                else
    //                {
    //                    // retry
    //                    res = await UpdateCount(table, parentCountCompletedEnt, runId, stepId, parentCount);
    //                }
    //            }
    //        }

    //        return res;
    //    }

    //    private static async Task<ParentCountCompletedEntity> GetCountEnt(CloudTable table, string runId, object stepId)
    //    {
    //        try
    //        {
    //            TableOperation retrieveOperation = TableOperation.Retrieve<ParentCountCompletedEntity>(runId, stepId.ToString());
    //            TableResult result = await table.ExecuteAsync(retrieveOperation);
    //            ParentCountCompletedEntity parentCountCompleteEnt = result.Result as ParentCountCompletedEntity;

    //            return parentCountCompleteEnt;
    //        }
    //        catch (StorageException ex)
    //        {
    //            var r = 0;
    //            throw;
    //        }
    //    }
    //}
}
