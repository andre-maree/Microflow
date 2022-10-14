# Microflow

Microflow is a serverless capable micro-service orchestrator built on top of the Microsoft Durable Functions framework. It can execute complex workflows that will auto scale out, and back, with pay-as-you-go billing when running serverlessly. It is also possible to run it in the Azure App Service or in Docker or Kubernetes.

Microflow is more than just an "if-else" based workflow engine. Very complex workflows can be executed. The major benefit Microflow provides over Durable Functions, is that workflows can be created and modified externally and then be upserted as Json - all aspects of workflow editing can be done and then executed without any code deployments.

One instance of Microflow can store many JSON defined workflows, and execute these workflows in parallel without impacting each other. Every workflow can also run as a singleton or as many parallel instances. Auto-scaling will ensure that there is always enough resources.

A Microflow workflow is a list of steps, with each step having children steps. Each step can call an http endpoint via get or post. Children steps will wait for all its parents to complete before executing. Each step has a Callout property to be set as the external micro-service call url.

During the workflow creation/editing phase, a step can be set to call out to an endpoint, and then to also spawn a webhook to wait for a reply:

> Microflow is a dynamic and powerful micro-service orchestrator with webhook functionality.



## Api Overview:
The url base for all calls is "microflow/v1". The full url will look like this: http://localhost:7071/microflow/v1/UpsertWorkflow/{globalKey?} 

- Workflow:
```r
UpsertWorkflow: [POST] UpsertWorkflow/{globalKey?}
MicroflowStart: [GET,POST] Start/{workflowName}/{instanceId?}
GetWorkflow: [GET] GetWorkflow/{workflowName}
```
- Step:
```r
GetStep: [GET] GetStep/{workflowName}/{stepNumber}
UpsertStep: [PUT] UpsertStep
SetStepProperties: [PUT] SetStepProperties/{workflowName}/{stepNumber}
```
- Webhook:
```r
Webhook: [GET,POST] webhooks/{webhookId}
WebhookWithAction: [GET,POST] webhooks/{webhookId}/{action}
GetWebhooks: [GET,POST] GetWebhooks/{workflowName}/{webhookId}/{stepNumber}/{instanceGuid?}
```
- Workflow control (pause, continue and stop):
```r
GlobalControl: [GET] GlobalControl/{cmd}/{globalKey}
WorkflowControl: [GET] WorkflowControl/{cmd}/{workflowName}/{workflowVersion}
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


## Example Worklow JSON of Test1_WorkflowExecution -> GetStartedWorkflow():

Four steps 1 to 4. Step 1 has children steps 2 and 3, and step 4 has steps 2 and 3 as parents. Steps 2 and 3 will wait for step 1 to complete and then execute in parallel (siblings). Step 4 will wait for both steps 2 and 3 to complete. Every step in this example will do an http post call to https://reqbin.com/echo/post, as specified by "default_post_url" in the MergeFields collection. There is no webhooks enabled on any steps, this is just a simplistic example to start with:
```json
{
  "WorkflowName": "Myflow_ClientX2",
  "WorkflowVersion": "2.1",
  "MergeFields": {
    "default_post_url": "https://reqbin.com/echo/post/json?WorkflowName=<WorkflowName>&MainOrchestrationId=<MainOrchestrationId>&SubOrchestrationId=<SubOrchestrationId>&WebhookId=<WebhookId>&RunId=<RunId>&StepNumber=<StepNumber>&GlobalKey=<GlobalKey>&StepId=<StepId>"
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
      "StepId": "myStep 1",
      "StepNumber": 1,
      "CalloutUrl": "{default_post_url}",
      "ScaleGroupId": null,
      "StopOnWebhookFailed": true,
      "IsHttpGet": false,
      "CalloutTimeoutSeconds": 1000,
      "AsynchronousPollingEnabled": true,
      "ForwardResponseData": false,
      "SubStepsToRunForWebhookTimeout": null,
      "WebhookId": null,
      "EnableWebhook": false,
      "WebhookTimeoutSeconds": 1000,
      "WebhookSubStepsMapping": null,
      "WaitForAllParents": true,
      "SubSteps": [
        2,
        3
      ],
      "RetryOptions": null
    },
    {
      "StepId": "myStep 2",
      "StepNumber": 2,
      "CalloutUrl": "{default_post_url}",
      "ScaleGroupId": null,
      "StopOnWebhookFailed": true,
      "IsHttpGet": false,
      "CalloutTimeoutSeconds": 1000,
      "AsynchronousPollingEnabled": true,
      "ForwardResponseData": false,
      "SubStepsToRunForWebhookTimeout": null,
      "WebhookId": null,
      "EnableWebhook": false,
      "WebhookTimeoutSeconds": 1000,
      "WebhookSubStepsMapping": null,
      "WaitForAllParents": true,
      "SubSteps": [
        4
      ],
      "RetryOptions": null
    },
    {
      "StepId": "myStep 3",
      "StepNumber": 3,
      "CalloutUrl": "{default_post_url}",
      "ScaleGroupId": null,
      "StopOnWebhookFailed": true,
      "IsHttpGet": false,
      "CalloutTimeoutSeconds": 1000,
      "AsynchronousPollingEnabled": true,
      "ForwardResponseData": false,
      "SubStepsToRunForWebhookTimeout": null,
      "WebhookId": null,
      "EnableWebhook": false,
      "WebhookTimeoutSeconds": 1000,
      "WebhookSubStepsMapping": null,
      "WaitForAllParents": true,
      "SubSteps": [
        4
      ],
      "RetryOptions": null
    },
    {
      "StepId": "myStep 4",
      "StepNumber": 4,
      "CalloutUrl": "{default_post_url}",
      "ScaleGroupId": null,
      "StopOnWebhookFailed": true,
      "IsHttpGet": false,
      "CalloutTimeoutSeconds": 1000,
      "AsynchronousPollingEnabled": true,
      "ForwardResponseData": false,
      "SubStepsToRunForWebhookTimeout": null,
      "WebhookId": null,
      "EnableWebhook": false,
      "WebhookTimeoutSeconds": 1000,
      "WebhookSubStepsMapping": null,
      "WaitForAllParents": true,
      "SubSteps": [],
      "RetryOptions": null
    }
  ]
}
```
