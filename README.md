# Microflow
Welcome to Microflow.

## Overview
Microflow is a dynamic servlerless micro-service workflow orchestration engine built with C#, .NET Core 3.1, and Azure Durable Functions. Microflow separates the workflow design from the function code so that workflows are no longer hard-coded as with normal Durable Functions. The workflow can be designed outside of Microflow and then passed in as JSON for execution, so no code changes or deployments are needed to modify any aspect of a workflow. Microflow can be deployed to the Azure Functions Consumption or Premium plans, to an Azure App Service, or to Kubernetes.

Microflow functionality:
- dynamic json workflows separate and changable outside of Microflow, workflows are dynamic and changable without Microflow knowing about it
- the micro-service implementations are cleanly separated from Microflow
- Microflow workflow projects can run as single instances (like a risk model that should always run as 1 instance), or to run as multiple parallel overlapping instances (like ecommerce orders)
- for custom logic like response interpretations, this can be included in Microflow, but best practice is to separate these response proxies as functions outside of Microflow, and then these will call back to Microflow
- parent-child-sibling dependencies, parallel optimized execution, parent steps execute in parallel
- auto scale out to 200 small virtual macines in the Consuption Plan, and to 100, 4 core, 14GB memory virtual machines in the Premium Plan
- easily manage step configs with merge fields
- do batch processing by looping the workflow execution with Microflow`s "Loop" setting, set up variation sets, 1 variation set per loop/batch
- set outgoing http calls to be inline (wait for response at the point of out call), or wait asyncrounously by setting CallbackAction (wait for external action/callbck)
- timeouts can be set per step for inline and callback
- retry policies can be set for each step and there can also be a default retry policy for the entire workflow
- StopOnActionFailed can be set per step to indicate for when there is a failure (not a success callback), which will make Microflow stop the workflow execution or to log the failure and continue with the workflow
- stateful and durable! Microflow leverages Durable Functions, so even when the vm crashes, Microflow will continue from where it left off, before the crash, when a new vm becomes available (Azure will do this in the background)
- leverage the Azure serverless plans: Serverless Consumption and Premium plans
- Microflow can run anywhere on Kubernetes when the Azure serverless environment is not available
- Microflow is light weight and will quickly autoscale under heavy load
- standardise micro-service orchestration accross development teams

Microflow use cases:
- any business workflow that wishes to take advantage of state-of-the-art serverless autoscaling durable stateful workflows
- avoid hard coded workflows
- use Microflow as a state-of-the-art back-end for your workflow designer, the workflow is just a simple json definition
- use the Microflow SDK to build code based workflows outside of Microflow, and then apply changes to the workflow without the need for any deployments

Future enhancements:
- workflow stop, pause and continue
- Azure AD authentication

## Test Examples
The code for these can be found in the console app\Tests.cs. There is also a SimpleSteps test that consists of 1 parent with 2 children and the 2 children have a common child step. This is not included in this diagram:

![2 Test cases](https://github.com/andre-maree/Microflow/blob/master/Tests.png)

## Solution Description

### MicroflowFunctionApp
This is the core of the workflow engine.

#### API
This currently contains 2 folders each with 1 class, 1 for internal and 1 for external api calls.

#### FlowControl
This contains 3 classes responsible for workflow execution.
  * ActivityCanExecuteNow.cs : Locks and checks the parent completed count to determine if a child step can execute, all parents must be completed for a child step to       start execution. Parent steps execute in parallel.
  * Microflow.cs : This conatains the recursive function ExecuteStep. It calls the action url and then calls ActivityCanExecuteNow for child steps of the current step.
  * MicroflowStart.cs : This is where the workflow JSON payload is received via http post and then prepares the workflow and calls start.
