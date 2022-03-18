## Overview
The idea came from the business need to execute workflows in the best scalable way. Durable Functions is a great choice for this, but a drawback might be that Durable Function workflows are usually hard coded by developers. This leads to a situation where workflow changes must go through the normal development life cycle (dev, test, production). What if the users designing the workflows are not developers? What if the business needs to make workflow changes quickly without the need for code changes? These are the problems that Microflow addresses, keeping the greatness of serverless Durable Functions and making the workflows dynamic for easy changeability. The workflow can be designed outside of Microflow and then passed in as JSON for execution, so no code changes or deployments are needed to modify any aspect of a workflow. Microflow can be deployed to the Azure Functions Consumption or Premium plans, to an Azure App Service, or to Kubernetes. For ultimate scalability, deploy Microflow to a serverless hosting plan for Microflow only, then deploy your micro-services each to their own plans. Microflow can orchestrate any Api endpoint across hosting plans, other clouds, or any http endpoints.

Microflow functionality:
- dynamic json workflows separate and changeable outside of Microflow
- auto scale out to 200 small virtual machines in the Consumption Plan, and to 100, 4 core CPU, 14GB memory virtual machines in the Premium Plan
- parent-child-sibling-parent dependencies - complex inter step dependencies, parallel optimized execution, parent steps execute in parallel
- very complex workflows can be created (be careful of creating endless loops -  in the future validation will be built in to check this)
- Microflow workflows can run as single instances (like a risk model that should always run as 1 instance), or can run as multiple parallel overlapping instances (like ecommerce orders)
- run in split mode or standalone mode: split mode sepatates all non-workflow execution code from the admin API calls for optimum scaling performance and reauires both the Microflow and MicroflowApi function apps to be deployed, whereas standalone mode includes admin API calls within the Microflow app and thus only requires the Microflow app to be deployed but has a slightly larger code footprint to scale
- for custom logic like response interpretations, this can be included in Microflow, but best practice is to separate these response proxies as functions outside of Microflow, and then these will call back to Microflow
- easily manage step configs with merge fields
- do batch processing by looping the workflow execution with Microflow`s "Loop" setting, set up variation sets, 1 variation set per loop/batch
- set outgoing http calls to be inline (wait for response at the point of out call), or wait asynchronously by setting CallbackAction (wait for external action/callback)
- set AsynchronousPollingEnabled (per step) to true and the step will poll for completion before moving on, embed other Microflow workflows and wait for them; or set it to false for fire and forget, call another Microflow workflow and continue immediately with the next step, no waiting for completion
- there is a global id called "GlobalKey" in the logs (orchestrations, steps, errors) that can be used to tie up cross Microflow workflow calls, this key is passed along for logging purposes when a Microflow workflow calls/embeds other Microflow workflows
- workflows, as well as based on a GlobalKey, can be controlled via api calls for: stop, pause, and run
- timeouts can be set per step for inline and callback
- retry policies can be set for each step and there can also be a default retry policy for the entire workflow
- StopOnActionFailed can be set per step to indicate for when there is a failure (not a success callback), which will make Microflow stop the workflow execution or to log the failure and continue with the workflow
- scale group can be set per step, this will throttle the maximum concurrent step instances
- save and retrieve Microflow workflow json
- view in-step progress counts, this is more useful when running multiple concurrent instances
- stateful and durable! Microflow leverages Durable Functions, so even when the vm crashes, Microflow will continue from where it left off, before the crash, when a new vm becomes available (Azure will do this in the background)
- leverage the Azure serverless plans: Serverless Consumption and Premium plans
- Microflow can run anywhere on Kubernetes when the Azure serverless environment is not available
- Microflow is lightweight and will auto scale when there is a usage spike

Controlling execution with the Microflow Api calls: Ready, Pause, and Stop,
cmd is 1 of the following commands: "ready", "pause", or "stop":

- Microflow_GlobalControl: "/GlobalControl/{cmd}/{globalKey}"
- Microflow_WorkflowControl: "/WorkflowControl/{cmd}/{workflowName}"

To get states:
- getGlobalState: "/GlobalState/{globalKey}"
- getWorkflowState: "/WorkflowState/{workflowName}"

Microflow use cases:
- any business workflow that needs to leverage serverless autoscaling durable stateful workflows
- avoid hard coded workflows
- use Microflow as a state-of-the-art back end for your workflow designer, the workflow is just a simple json definition
- use the Microflow SDK to build code-based workflows outside of Microflow, and then apply changes to the workflow without the need for any deployments
- Microflow is open-source so feel free to modify Microflow to your needs

Future enhancements:
- Azure AD authentication?
- gRPC communication?

## Test Examples
The code for these can be found in the console app\Tests.cs. There is also a SimpleSteps test that consists of 1 parent with 2 children and the 2 children have a common child step (See JSON Workflow Example below). This is the diagram for the 2 more complex test workflows:

![2 Test cases](https://github.com/andre-maree/Microflow/blob/master/Images/Tests.png)


## Single Step with All JSON Config:
```
{
   "StepNumber":1,
   "StepId":"MyOwnStepReference_Id_1",
   "CalloutUrl":"https://reqbin.com/echo/post/json?mainorchestrationid=<mainorchestrationid>&stepid=<stepId>",
   "CallbackAction":"approve",
   "AsynchronousPollingEnabled":"true",
   "StopOnActionFailed":true,
   "IsHttpGet":true,
   "CalloutTimeoutSeconds": 15,
   "CallbackTimeoutSeconds":30,
   "ScaleGroupId": "myscalegroup",
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
   - **StepNumber**: Used internally by Microflow, but is also settable, must be unique
   - **StepId**: String can be set and used as a key or part of a key in the worker micro-service that is being called, must be unique
   - **CalloutUrl**: Worker micro-service http end-point, or another Microflow workflow`s start endpoint, that is called by Microflow
   - **CallbackAction**: When this is set Microflow will create a callback webhook and wait for this to be called, and when not set, Microflow will not create and wait for a callback, but will log the http response, and continue to the next step
   - **AsynchronousPollingEnabled**: set this to true and the step will poll for completion before moving on/embed other Microflow workflows and wait for them; or set it to false for fire and forget/call another Microflow workflow and continue immediately with the next step, no waiting for completion
   - **StopOnActionFailed**: If there is any type of failure for callouts or callbacks, including timeouts, and any non-success http responses, this will stop all execution if true, and log and continue to the next step if it is false
   - **IsHttpGet**: Http post to micro-service endpoint if false
   - **CalloutTimeoutSeconds**: This is for how long to wait for the http callout, no cloud costs are incurred during the wait
   - **CallbackTimeoutSeconds**: This is for how long an action callback will wait, it can be set for any time span and no cloud costs are incurred during the wait
   - **ScaleGroupId**: This is used to identify steps that are in a scale group, the maximum concurrent instances per scale group is looked up and used to throttle the concurrent step instance count in the scale group, call the Microwflow Api "api/ScaleGroup/{scaleGroupId}/{maxInstanceCount}" to set the maximum number of concurrent step instances for the scale group
   - **SubSteps**: These are the sub steps that are dependent on this step
   - **RetryOptions**: Set this to do retries for the micro-service end-point call
   
   
## JSON Workflow Example:
This simple workflow contains 1 parent step (StepId 1) with 2 sub steps (StepId 2 and StepId 3), and each sub step has 1 common sub step (StepId 4). This is the same structure as the included test Tests.CreateTestWorkflow_SimpleSteps(). StepId 1 has a callback action set, and StepId 3 has a retry set. There is 1 merge field set and is used as a default callout URL.
```
{
  "WorkflowName": "MicroflowTest",
  "DefaultRetryOptions": null,
  "Loop": 1,
  "MergeFields": {
    "default_post_url": "http://localhost:7071/SleepTestOrchestrator_Function"
  },
  "Steps": [
    {
      "StepId": "myStep 1",
      "StepNumber": 1,
      "CalloutUrl": "{default_post_url}",
      "CallbackAction": "approve",
      "StopOnActionFailed": true,
      "IsHttpGet": true,
      "CalloutTimeoutSeconds": 10,
      "CallbackTimeoutSeconds": 30,
      "ScaleGroupId": "myscalegroup",
      "AsynchronousPollingEnabled": true,
      "SubSteps": [
        2,
        3
      ],
      "RetryOptions": {
        "DelaySeconds": 1,
        "MaxDelaySeconds": 2,
        "MaxRetries": 1,
        "BackoffCoefficient": 1,
        "TimeOutSeconds": 60
      }
    },
    {
      "StepId": "myStep 2",
      "StepNumber": 2,
      "CalloutUrl": "{default_post_url}",
      "CallbackAction": null,
      "StopOnActionFailed": true,
      "IsHttpGet": false,
      "CalloutTimeoutSeconds": 1000,
      "CallbackTimeoutSeconds": 1000,
      "ScaleGroupId": "myscalegroup",
      "AsynchronousPollingEnabled": true,
      "SubSteps": [
        4
      ],
      "RetryOptions": null
    },
    {
      "StepId": "myStep 3",
      "StepNumber": 3,
      "CalloutUrl": "{default_post_url}",
      "CallbackAction": null,
      "StopOnActionFailed": true,
      "IsHttpGet": false,
      "CalloutTimeoutSeconds": 1000,
      "CallbackTimeoutSeconds": 1000,
      "ScaleGroupId": "myscalegroup",
      "AsynchronousPollingEnabled": true,
      "SubSteps": [
        4
      ],
      "RetryOptions": null
    },
    {
      "StepId": "myStep 4",
      "StepNumber": 4,
      "CalloutUrl": "{default_post_url}",
      "CallbackAction": null,
      "StopOnActionFailed": true,
      "IsHttpGet": false,
      "CalloutTimeoutSeconds": 1000,
      "CallbackTimeoutSeconds": 1000,
      "ScaleGroupId": "myscalegroup",
      "AsynchronousPollingEnabled": true,
      "SubSteps": [],
      "RetryOptions": null
    }
  ]
}
```


## Microflow Post or Query String Data
This is the data that can be sent to the worker micro-service via http post or get querystring.

Http post:
```csharp

public class MicroflowPostData
{
   public int StepNumber { get; set; }
   public string StepId { get; set; }
   // passed along and logged when a workflow step calls another workflow`s start
   public string GlobalKey { get; set; }
   public string WorkflowName { get; set; }
   public string MainOrchestrationId { get; set; }
   public string SubOrchestrationId { get; set; }
   public string CallbackUrl { get; set; }
   public string RunId { get; set; }
}

```
Http querystring:
```html
?WorkflowName=<WorkflowName>&MainOrchestrationId=<MainOrchestrationId>&SubOrchestrationId=<SubOrchestrationId>&CallbackUrl=<CallbackUrl>&RunId=<RunId>&StepNumber=<StepNumber>&StepId=<StepId>&GlobalKey=<GlobalKey>
```


## Solution Description

### MicroflowFunctionApp
This is the core of the workflow engine.

#### MicroflowStart
This contains 3 functions responsible starting workflow execution.
  * "UpsertWorkflow" : Must be called first after workflow creation or modification before "Microflow_HttpStart" can be called. This will persist the needed workflow meta data that is needed for the workflow to run. When running multiple concurrent instances, it is not needed to call this every time when a new instance starts to run.
  * "Microflow_HttpStart" : This will start the run of a workflow by calling "Start".
  * "Start" : This starts the workflow run by getting a list of top level steps and then calls "ExecuteStep" for each top level step.
 
#### FlowControl
This contains 4 classes responsible for workflow execution.
  * CanStepExecuteNow.cs : Locks and checks the parent completed count to determine if a child step can execute, all parents must be completed for a child step to       start execution. Parent steps execute in parallel.
  * MicroflowStep.cs : This contains the recursive function "ExecuteStep". It instantiates a MicroflowContext object and calls it`s "RunMicroflow" method.
  * MicroflowStart.cs : This is where the workflow JSON payload is received via http post and then prepares the workflow and calls start.
  * MicroflowContext.cs : This contains the core execution code.

### MicroflowApiFunctionApp
This is an "admin" Api function app that is used to add functionality that does not impact core execution. For example to get log data or to see live in-progress step counts.

### MicroflowConsoleApp
This console app is used to create test workflow pworkflows and post to Microflow. After created or modifying a workflow, always call "UpsertWorkflow" before calling the run http call "Microflow_HttpStart". After a call to "UpsertWorkflow" is made, then "Microflow_HttpStart" can be called multiple times as long as the workflow definition stays the same.

### MicroflowModels
This library holds model classes that is useful outside of the Microflow function app, but is also used by the Microflow function app.

### MicroflowSDK
This library is used to help with workflow and steps creates.


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
6. Then the final last log in red: "Workflow run MicroflowDemo completed successfully..." and "<!!! A GREAT SUCCESS !!!>"
7. The step completions are also logged to storage table "LogSteps"
8. Main orchestration completions are logged to storage table "LogOrchestration"


## Logging

### Console Logging:

   * Red at start: "Started Run ID db7d8fd4-e7e3-5e57-9017-e7ec3ad6bb01..."
   * Orange for each completed step: "Step 1 done at 10:55:02  -  Run ID: db7d8fd4-e7e3-5e57-9017-e7ec3ad6bb01"
   * Red at run end: "Run ID db7d8fd4-e7e3-5e57-9017-e7ec3ad6bb01 completed successfully..."
   * Red at workflow end: "Workflow run MicroflowDemo completed successfully..."
   * Red at workflow end: "<-----> !!! A GREAT SUCCESS !!! <----->"

### Table Storage Logging

Logs top level orchestration processing info to storage table "LogOrchestration", logs step processing info to storage table "LogSteps". For both there is a start and end date in the log entry. When an orchestration or step starts, an entry with the start date is saved, and when this item completes, the same entry with the start date will get an end date. Errors are logged to the table "MicroflowErrors". The Microflow storage account is configured in the local.settings.json file.

