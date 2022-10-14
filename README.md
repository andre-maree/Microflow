# Microflow

Microflow is a serverless capable micro-service orchestrator built on top of the Microsoft Durable Functions framework. It can execute complex workflows that will auto scale out, and back, with pay-as-you-go billing when running serverlessly. It is also possible to run it in the Azure App Service or in Docker or Kubernetes.

Microflow is more than just an "if-else" based workflow engine. Very complex workflows can be executed. The major benefit Microflow provides over Durable Functions, is that workflows can be created and modified externally and then be upserted as Json - all aspects of workflow creation can be done and then executed without any code deployments!

One instance of Microflow can store many Json defined workflows, and execute these workflows in parallel without impacting each other. Every workflow can also run as a singleton or as many parallel instances. Auto scaling will ensure that there is always enough resources.

> A Microflow workflow is a list of steps, with each step having children steps. Each step can call an http endpoint via get or post. Children steps will wait for all its parents to complete before executing. A step can be seen as a call to a micro-service.

During the workflow creation phase, a step can be set to call out to an endpoint, and then to also spawn a webhook to wait for a reply. 

> Microflow is a dynamic and powerful micro-service orchestrator with webhook functionality.

### Api Overview:
The url base for all calls is "microflow/v1". The full url will look like this:

http://localhost:7071/microflow/v1/UpsertWorkflow/{globalKey?}


```r
UpsertWorkflow: [POST] UpsertWorkflow/{globalKey?}
MicroflowStart: [GET,POST] Start/{workflowName}/{instanceId?}
GetWebhooks: [GET,POST] GetWebhooks/{workflowName}/{webhookId}/{stepNumber}/{instanceGuid?}
GetWorkflow: [GET] GetWorkflow/{workflowName}
GlobalControl: [GET] GlobalControl/{cmd}/{globalKey}
GetStep: [GET] GetStep/{workflowName}/{stepNumber}
SetScaleGroup: [GET,POST] ScaleGroup/{scaleGroupId}/{maxInstanceCount}/{maxWaitSeconds:int?}
SetStepProperties: [PUT] SetStepProperties/{workflowName}/{stepNumber}
UpsertStep: [PUT] UpsertStep
GetScaleGroup: [GET] ScaleGroup/{scaleGroupId}
Webhook: [GET,POST] webhooks/{webhookId}
WebhookWithAction: [GET,POST] webhooks/{webhookId}/{action}
WorkflowControl: [GET] WorkflowControl/{cmd}/{workflowName}/{workflowVersion}
```

### Getting Started:
Visual Stodio 2022 with C# is needed. Clone the repo locally and open the two solutions: Microflow.sln and MicroflowTest.sln. To get started with Microflow, only these two api calls are needed: UpsertWorkflow and MicroflowStart. This can be done by simply running included unit test in the MicroflowTest.sln. First start running the MicroflowApp in the Microflow.sln, and then run the GetStartedWorkflow test in the MicroflowTest.sln.
   
```csharp

public class Test1_WorkflowExecution
{
    [TestMethod]
    public async Task GetStartedWorkflow()

```
