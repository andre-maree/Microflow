# Microflow
Welcome to Microflow.

## Overview
Microflow is a dynamic servlerless micro-service workflow orchestration engine built with C#, .NET Core 3.1, and Azure Durable Functions. Microflow separates the workflow design from the function code so that workflows don`t have to be hard-coded as with normal Durable Functions. The workflow can be designed outside of Microflow and then passed in as JSON for execution, so no code changes or deployments are needed to modify any aspect of a workflow. Microflow can be deployed to the Azure Functions Consumption or Premium plans, to an Azure App Service, or to Kubernetes.

Microflow functionality:
- dynamic json workflows separate and changable outside of Microflow, workflows are dynamic and changable without Microflow knowing about it
- the micro-service implementations are cleanly separated from Microflow
- Microflow workflow projects can run as single instances (like a risk model that should always run as 1 instance), or can run as multiple parallel overlapping instances (like ecommerce orders)
- for custom logic like response interpretations, this can be included in Microflow, but best practice is to separate these response proxies as functions outside of Microflow, and then these will call back to Microflow
- parent-child-sibling dependencies, parallel optimized execution, parent steps execute in parallel
- auto scale out to 200 small virtual macines in the Consuption Plan, and to 100, 4 core cpu, 14GB memory virtual machines in the Premium Plan
- easily manage step configs with merge fields
- do batch processing by looping the workflow execution with Microflow`s "Loop" setting, set up variation sets, 1 variation set per loop/batch
- set outgoing http calls to be inline (wait for response at the point of out call), or wait asyncrounously by setting CallbackAction (wait for external action/callbck)
- timeouts can be set per step for inline and callback
- retry policies can be set for each step and there can also be a default retry policy for the entire workflow
- StopOnActionFailed can be set per step to indicate for when there is a failure (not a success callback), which will make Microflow stop the workflow execution or to log the failure and continue with the workflow
- stateful and durable! Microflow leverages Durable Functions, so even when the vm crashes, Microflow will continue from where it left off, before the crash, when a new vm becomes available (Azure will do this in the background)
- leverage the Azure serverless plans: Serverless Consumption and Premium plans
- Microflow can run anywhere on Kubernetes when the Azure serverless environment is not available
- Microflow is light-weight and will autoscale when there is a usage spike

Microflow use cases:
- any business workflow that needs to leverage serverless autoscaling durable stateful workflows that can run anywhere
- avoid hard coded workflows
- use Microflow as a state-of-the-art back-end for your workflow designer, the workflow is just a simple json definition
- use the Microflow SDK to build code based workflows outside of Microflow, and then apply changes to the workflow without the need for any deployments
- Microflow is open-source so feel free to modify Microflow to your needs

Future enhancements:
- workflow stop, pause and continue
- Azure AD authentication

## Test Examples
The code for these can be found in the console app\Tests.cs. There is also a SimpleSteps test that consists of 1 parent with 2 children and the 2 children have a common child step (See JSON Workflow Example below). This is the diagram for the 2 more complex test workflows:

![2 Test cases](https://github.com/andre-maree/Microflow/blob/master/Images/Tests.png)

## JSON Single Step With All Config:
```
{
   "StepId":1,
   "CalloutUrl":"http://localhost:7071/api/SleepTestOrchestrator_HttpStart",
   "CallbackAction":"approve",
   "StopOnActionFailed":true,
   "IsHttpGet":true,
   "ActionTimeoutSeconds":30,
   "SubSteps":[2,3],
   "RetryOptions":{
      "DelaySeconds":5,
      "MaxDelaySeconds":60,
      "MaxRetries":5,
      "BackoffCoefficient":1,
      "TimeOutSeconds":30
   }
}
```

## JSON Workflow Example:
This simple workflow contains 1 parent step (StepId 1) with 2 sub steps (StepId 2 and StepId 3), and each sub step has 1 common sub step (StepId 4). This is the same structure as the included test Tests.CreateTestWorkflow_SimpleSteps(). StepId 1 has a callback action set, and StepId 3 has a retry set. There is 1 merge field set and is used as a default callout url.
```
{
  "ProjectName": "MicroflowDemo",
  "DefaultRetryOptions": null,
  "Loop": 1,
  "MergeFields": {
    "default_post_url": "https://reqbin.com/echo/post/json?workflowid=<workflowId>stepid=<stepId>"
  },
  "Steps": [
    {
      "StepId": 1,
      "CalloutUrl": "{default_post_url}",
      "CallbackAction": "approve",
      "StopOnActionFailed": true,
      "ActionTimeoutSeconds": 30,
      "SubSteps": [2,3],
      "RetryOptions": null
    },
    {
      "StepId": 2,
      "CalloutUrl": "{default_post_url}",
      "CallbackAction": null,
      "StopOnActionFailed": true,
      "ActionTimeoutSeconds": 1000,
      "SubSteps": [4],
      "RetryOptions": null
    },
    {
      "StepId": 3,
      "CalloutUrl": "{default_post_url}",
      "CallbackAction": null,
      "StopOnActionFailed": true,
      "ActionTimeoutSeconds": 1000,
      "SubSteps": [4],
      "RetryOptions": {
        "DelaySeconds": 5,
        "MaxDelaySeconds": 10,
        "MaxRetries": 2,
        "BackoffCoefficient": 5,
        "TimeOutSeconds": 30
      }
    },
    {
      "StepId": 4,
      "CalloutUrl": "{default_post_url}",
      "CallbackAction": null,
      "StopOnActionFailed": true,
      "ActionTimeoutSeconds": 1000,
      "SubSteps": [],
      "RetryOptions": null
    }
  ]
}
```

## Solution Description

### MicroflowFunctionApp
This is the core of the workflow engine.

#### API
This currently contains 2 folders each with 1 class, 1 for internal and 1 for external api calls.

#### FlowControl
This contains 3 classes responsible for workflow execution.
  * CanStepExecuteNow.cs : Locks and checks the parent completed count to determine if a child step can execute, all parents must be completed for a child step to       start execution. Parent steps execute in parallel.
  * Microflow.cs : This contains the recursive function ExecuteStep. It calls the action url and then calls CanExecuteNow for child steps of the current step.
  * MicroflowStart.cs : This is where the workflow JSON payload is received via http post and then prepares the workflow and calls start.
  
## Setup Guide
Clone the repo locally. It is advised to separate the MicroflowConsoleApp from the MicroflowFunctionApp in Visual Studio, this is to be able to run MicroflowFunctionApp separately, and then run the MicroflowConsoleApp to post workflows to it:

Microflow Solution:<br>
![2 Test cases](https://github.com/andre-maree/Microflow/blob/080cf39f512dbd3a5fa1c99c12b22732465f28d6/MicroflowFunctionApp%20Solution.PNG)

MicroflowConsoleApp Solution:<br>
![2 Test cases](https://github.com/andre-maree/Microflow/blob/master/Images/MicroflowConsoleApp%20Solution.PNG)

Microflow Solution Nugets:<br>
![2 Test cases](https://github.com/andre-maree/Microflow/blob/master/Images/MicroflowFunctionApp%20Nuget.PNG)

MicroflowConsoleApp Solution Nugets:<br>
![2 Test cases](https://github.com/andre-maree/Microflow/blob/master/Images/MicroflowConsoleApp%20Nuget.PNG)

1. Run the MicroflowApp (Function App)
2. Run the MicroflowConsole
   - Choose which of the test workflows to post (in the file Tests.cs: CreateTestWorkflow_SimpleSteps(), CreateTestWorkflow_Complex1(), or CreateTestWorkflow_10StepsParallel())
   - look at Program.cs: by default 1 instance with id 39806875-9c81-4736-81c0-9be562dae71e will run, but there is also a commented out loop for multiple concurrent instances
3. The run will 1st log to console in red: "Started Run ID 2d779289-01a5-50c5-b4f4-e6fa22a9fc96..."
4. Then each step will log success in orange" "Step 1 done at 10:38:57  -  Run ID: 2d779289-01a5-50c5-b4f4-e6fa22a9fc96"
5. Then the run end will log in red: "Run ID 2d779289-01a5-50c5-b4f4-e6fa22a9fc96 completed successfully..."
6. Then the final last log in red: "Project run MicroflowDemo completed successfully..." and "<!!! A GREAT SUCCESS !!!>"
7. The step completions are also logged to storage table "LogSteps"
8. Main orchestration completions are logged to storage table "LogOrchestration"
