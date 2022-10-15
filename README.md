# Microflow

Microflow is a serverless-capable micro-service orchestrator built on top of the Microsoft Durable Functions framework. It can execute complex workflows that will automatically scale out and back, with pay-as-you-go billing when running serverlessly. It is also possible to run it on the Azure App Service or on Docker or Kubernetes.

Microflow is more than just an "if-else" based workflow engine. Very complex workflows can be executed. The major benefit over Durable Functions, is that workflows can be created and modified externally and then be upserted as JSON. All aspects of workflow editing can be done and then executed without any code deployments.

> One instance of Microflow can store many JSON defined workflows, and execute these workflows in parallel without impacting each other. Every workflow can  run as a singleton or as many parallel instances. Auto-scaling will ensure that there are always enough resources.

A Microflow workflow is a list of steps, with each step having child steps. Each step can call an http endpoint via get or post. Child steps will wait for all its parents to complete before executing. Each step has a Callout property to be set as the external micro-service call url.

During the workflow creation and editing phases, a step can be set to call out to an endpoint, and then to also spawn a webhook to wait for a reply:

> Microflow is a dynamic and powerful micro-service orchestrator with webhook functionality.



## API Overview:
The base URL for all calls is "microflow/v1". The full url will look like this: http://localhost:7071/microflow/v1/UpsertWorkflow/{globalKey?} 

- Workflow:
```r
UpsertWorkflow: [POST] UpsertWorkflow/{globalKey?}
GetWorkflow: [GET] GetWorkflow/{workflowName}
```
- Workflow control ({cmd} = pause, ready or stop):
```r
MicroflowStart: [GET,POST] Start/{workflowName}/{instanceId?}
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
WebhookWithAction: [GET,POST] webhooks/{webhookId}/{action}
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

Four steps 1 to 4. Step 1 has child steps 2 and 3, and step 4 has steps 2 and 3 as parents. Steps 2 and 3 will wait for step 1 to complete and then execute in parallel (siblings). Step 4 will wait for both steps 2 and 3 to complete. Every step in this example will make an http post call to https://reqbin.com/echo/post/json, as specified by "default_post_url" in the MergeFields collection. There are no webhooks enabled on any steps. This is a simplistic example to start with:
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
