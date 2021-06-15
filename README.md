# Microflow
Welcome to Microflow.

## Overview
Microflow is a dynamic servlerless micro-service workflow orchestration engine built with C#, .NET Core 3.1, and Azure Durable Functions. Microflow separates the workflow design from the function code so that workflows are no longer hard-coded as with normal Durable Functions. The workflow can be designed outside of Microflow and then passed in as JSON for execution, so no code changes or deployments are needed to modify any aspect of a workflow. Microflow can be deployed to the Azure Functions Consumption or Premium plans, to an Azure App Service, or to Kubernetes.

## Solution Description

### MicroflowFunctionApp
This is the core of the workflow engine.

#### API
This currently contains 1 class with all the api calls.

#### FlowControl
This contains 3 classes responsible for workflow execution.
  * ActivityCanExecuteNow.cs : Locks and checks the parent completed count to determine if a child step can execute, all parents must be completed for a child step to       start xecution. Parent steps execute in parallel.
  * Microflow.cs : This conatains the recursive function ExecuteStep. It calls the action url and then calls ActivityCanExecuteNow for child steps of the current step.
  * MicroflowStart.cs : This is where the workflow JSON payload is received via http post and then prepares the workflow and calls start.
