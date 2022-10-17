# Microflow

Microflow is a serverless-capable micro-service orchestrator built on top of the Microsoft Durable Functions framework. It can execute complex workflows that will automatically scale out and back, with pay-as-you-go billing when running serverlessly. It is also possible to run it on the Azure App Service or on Docker or Kubernetes.

Microflow is more than just an "if-else" based workflow engine. Very complex workflows can be executed. The major benefit over Durable Functions, is that workflow JSON can be created and modified separately and then be upserted to Microflow to store and execute. All aspects of workflow editing can be done, upserted, and then executed without any code deployments.

> One instance of Microflow can store many JSON defined workflows, and execute these workflows in parallel without impacting each other. Furthermore, every workflow can  run as a singleton or as multiple parallel instances. Auto-scaling will ensure that there are always enough resources.

A Microflow workflow is a list of steps, with each step having child steps. Complex inter-step relations is possible, for example: a step can be both a parent and a sibling to another step. Each step can call an http endpoint via get or post. Child steps will wait for all its parents to complete before executing. Each step has a CalloutUrl property that can be set as the external micro-service call URL. A step can also have a webhook set that will then spawn and wait for a reply after the callout was done:

> Microflow is a dynamic and flexible micro-service orchestrator with webhook functionality.

Other functionality include:
- Protect resources from overloading by grouping steps with a ScaleGroupId and setting a maximum concurrent instance count per scale group.
- Pause, continue, and stop the running of a specicfic workflow.
- Workflows can be chained together by calling other workflows. A global guid can be set to tie all these workflows together, and then also be used to start, stop and pause by the global guid.
- All request and response data is logged to Azure blobs.
- Response data can be passed on from parent steps to child steps.

Read more about Microflow in the [wiki](https://github.com/andre-maree/Microflow/wiki "wiki") (under construction).

## API Overview:
The base URL for all calls is "microflow/v1". The full url will look like this: http://localhost:7071/microflow/v1/UpsertWorkflow/{globalKey?} 

- Workflow:
```r
UpsertWorkflow: [POST] UpsertWorkflow/{globalKey?}
GetWorkflow: [GET] GetWorkflow/{workflowName}
```
- Workflow control ({cmd} = pause, ready or stop):
```r
MicroflowStart: [GET,POST] Start/{workflowName}/{instanceId?}?globalKey={globalKey}&loop={loop}
GlobalControl: [GET] GlobalControl/{cmd}/{globalKey}
WorkflowControl: [GET] WorkflowControl/{cmd}/{workflowName}/{workflowVersion}
```
- Step:
```r
GetStep: [GET] GetStep/{workflowName}/{stepNumber}
UpsertStep: [PUT] UpsertStep
SetStepProperties: [PUT] SetStepProperties/{workflowName}/{stepNumber}
```
- Webhook:
```r
Webhook: [GET,POST] webhooks/{webhookId}/{action}
```
- Scalegroup:
```r
SetScaleGroup: [GET,POST] ScaleGroup/{scaleGroupId}/{maxInstanceCount}/{maxWaitSeconds:int?}
GetScaleGroup: [GET] ScaleGroup/{scaleGroupId}
```
## Getting Started:
Visual Stodio 2022 with C# is needed. Clone the repo locally and open the two solutions: Microflow.sln and MicroflowTest.sln. To get started with Microflow, only these two api calls are needed: UpsertWorkflow and MicroflowStart. This can be done by running the included unit test in the MicroflowTest.sln. First start running the MicroflowApp in Microflow.sln, and then run the GetStartedWorkflow test in MicroflowTest.sln:
```csharp

public class Test1_WorkflowExecution
{
    [TestMethod]
    public async Task GetStartedWorkflow()

```


## Example Workflow JSON of Test1_WorkflowExecution -> GetStartedWorkflow():

Four steps 1 to 4 is created. Step 1 has child steps 2 and 3, and step 4 has steps 2 and 3 as parents. Steps 2 and 3 will wait for step 1 to complete and then execute in parallel (siblings). Step 4 will wait for both steps 2 and 3 to complete. Every step in this example will make an http post call to https://reqbin.com/echo/post/json, as specified by "default_post_url" in the MergeFields collection. There are no webhooks enabled on any steps. This is a simplistic example to start with:
```json
{
  "WorkflowName": "Unit_test_workflow",
  "WorkflowVersion": "1.0",
  "MergeFields": {
    "default_post_url": "https://reqbin.com/echo/post/json"
  },
  "DefaultRetryOptions": {
    "DelaySeconds": 5,
    "MaxDelaySeconds": 120,
    "MaxRetries": 15,
    "BackoffCoefficient": 5,
    "TimeOutSeconds": 300
  },
  "Steps": [
    {
      "StepNumber": 1,
      "StepId": "myStep 1",
      "SubSteps": [
        2,
        3
      ],
      "WaitForAllParents": true,
      "CalloutUrl": "{default_post_url}",
      "CalloutTimeoutSeconds": 1000,
      "StopOnCalloutFailure": false,
      "SubStepsToRunForCalloutFailure": null,
      "IsHttpGet": false,
      "EnableWebhook": false,
      "WebhookId": null,
      "StopOnWebhookTimeout": true,
      "SubStepsToRunForWebhookTimeout": null,
      "WebhookTimeoutSeconds": 1000,
      "WebhookSubStepsMapping": null,
      "ScaleGroupId": null,
      "AsynchronousPollingEnabled": true,
      "ForwardResponseData": false,
      "RetryOptions": null
    },
    {
      "StepNumber": 2,
      "StepId": "myStep 2",
      "SubSteps": [
        4
      ],
      "WaitForAllParents": true,
      "CalloutUrl": "{default_post_url}",
      "CalloutTimeoutSeconds": 1000,
      "StopOnCalloutFailure": false,
      "SubStepsToRunForCalloutFailure": null,
      "IsHttpGet": false,
      "EnableWebhook": false,
      "WebhookId": null,
      "StopOnWebhookTimeout": true,
      "SubStepsToRunForWebhookTimeout": null,
      "WebhookTimeoutSeconds": 1000,
      "WebhookSubStepsMapping": null,
      "ScaleGroupId": null,
      "AsynchronousPollingEnabled": true,
      "ForwardResponseData": false,
      "RetryOptions": null
    },
    {
      "StepNumber": 3,
      "StepId": "myStep 3",
      "SubSteps": [
        4
      ],
      "WaitForAllParents": true,
      "CalloutUrl": "{default_post_url}",
      "CalloutTimeoutSeconds": 1000,
      "StopOnCalloutFailure": false,
      "SubStepsToRunForCalloutFailure": null,
      "IsHttpGet": false,
      "EnableWebhook": false,
      "WebhookId": null,
      "StopOnWebhookTimeout": true,
      "SubStepsToRunForWebhookTimeout": null,
      "WebhookTimeoutSeconds": 1000,
      "WebhookSubStepsMapping": null,
      "ScaleGroupId": null,
      "AsynchronousPollingEnabled": true,
      "ForwardResponseData": false,
      "RetryOptions": null
    },
    {
      "StepNumber": 4,
      "StepId": "myStep 4",
      "SubSteps": [],
      "WaitForAllParents": true,
      "CalloutUrl": "{default_post_url}",
      "CalloutTimeoutSeconds": 1000,
      "StopOnCalloutFailure": false,
      "SubStepsToRunForCalloutFailure": null,
      "IsHttpGet": false,
      "EnableWebhook": false,
      "WebhookId": null,
      "StopOnWebhookTimeout": true,
      "SubStepsToRunForWebhookTimeout": null,
      "WebhookTimeoutSeconds": 1000,
      "WebhookSubStepsMapping": null,
      "ScaleGroupId": null,
      "AsynchronousPollingEnabled": true,
      "ForwardResponseData": false,
      "RetryOptions": null
    }
  ]
}
```
