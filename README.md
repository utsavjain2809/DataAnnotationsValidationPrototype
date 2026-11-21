# Data Annotations Validation Prototype

An experimental .NET console application demonstrating centralized Data Annotations validation for Microsoft Semantic Kernel functions through an invocation filter.

This prototype accompanies [Semantic Kernel feature proposal #14847: Built-in Data Annotations validation for kernel functions](https://github.com/microsoft/semantic-kernel/issues/14847). It is a standalone proof of concept, not an official Semantic Kernel feature or a production-ready implementation of the full proposal.

## Motivation

Kernel functions often repeat checks for required values, string lengths, ranges, and formats. Moving these checks into a reusable invocation filter lets functions declare constraints alongside their parameters and keep their bodies focused on business logic.

The sample also includes agent instructions for collecting missing values and recovering from validation failures. These instructions complement runtime validation; they do not replace it.

## What is implemented

- An opt-in `AddDataAnnotationsValidations()` kernel builder extension.
- A singleton `IFunctionInvocationFilter` registered with `TryAddEnumerable`, avoiding duplicate registration of this filter while preserving other registered filters.
- Reflection over method parameter attributes derived from `ValidationAttribute`.
- Application of `[DefaultValue]` when an argument is missing or explicitly `null`, before checking its validation attributes.
- A structured failure result containing parameter names and validation messages.
- Prevention of plugin method execution when validation returns errors.
- A `ChatCompletionAgent` console example using Azure OpenAI, automatic function selection, and streamed responses.

The sample plugin demonstrates `[Required]`, `[StringLength]`, `[EmailAddress]`, `[Range]`, and `[DefaultValue]`. The filter discovers other `ValidationAttribute` implementations as well, but their behavior depends on the supplied argument types and validation context; comprehensive compatibility is not established.

## Requirements

- .NET 10 SDK.
- An Azure OpenAI endpoint, API key, and deployed chat model that supports tool calling.
- Network access to restore NuGet packages and call the configured endpoint.

The project references Semantic Kernel packages at version `1.82.0`.

## Run the sample

From the repository root, configure these environment variables in PowerShell:

```powershell
$env:AZURE_AI_MODEL_ID = "<your-azure-openai-deployment-name>"
$env:AZURE_AI_ENDPOINT = "https://<your-resource-name>.openai.azure.com/"
$env:AZURE_AI_APIKEY = "<your-api-key>"

dotnet run --project ./DataAnnotationsValidationPrototype/DataAnnotationsValidationPrototype.csproj
```

Despite its name, `AZURE_AI_MODEL_ID` is passed as the Azure OpenAI deployment name. Replace the placeholders with your configuration. Keep credentials out of source control. These environment variables apply to the current PowerShell session and its child processes.

At the `User >` prompt, try:

```text
Greet Utsav.
Register Utsav with email utsav@example.com and age 25.
Register Utsav with email not-an-email and age 16.
Register Utsav, age 25. I have not supplied an email address.
```

These are exploration prompts, not deterministic tests. The agent may ask for corrections before invoking a tool. When invalid arguments reach the filter and produce validation errors, the plugin method is skipped and the structured failure is returned. Press `Ctrl+C` to stop the application.

`RegisterCustomer` only returns a confirmation string. It does not persist a customer or call an external registration service.

## Usage

Enable validation when configuring the kernel:

```csharp
using DataAnnotationsValidationPrototype.Validation;
using Microsoft.SemanticKernel;

var builder = Kernel.CreateBuilder();
builder.AddDataAnnotationsValidations();

// Configure AI services and import your plugins as needed.
var kernel = builder.Build();
```

Declare constraints on plugin method parameters:

```csharp
[KernelFunction]
public string RegisterCustomer(
    [Required, StringLength(100)] string name,
    [Required, EmailAddress] string email,
    [Range(18, 120)] int age)
{
    return "Customer registered.";
}
```

Declare a default value:

```csharp
[KernelFunction]
public string GetCustomerGreeting(
    [Required] string name,
    [DefaultValue("Hello")] string greeting)
{
    return $"{greeting}, {name}!";
}
```

In the current implementation, the default replaces both a missing argument and an explicit `null`. It does not replace an empty string.

## How validation works

1. The filter obtains the function's underlying .NET method. Functions without an underlying method pass through unchanged.
2. For each method parameter, it reads the argument using the parameter's CLR name.
3. If the value is missing or `null`, it applies any `[DefaultValue]` attribute and updates the arguments.
4. It evaluates the parameter's `ValidationAttribute` instances and collects returned errors.
5. If errors exist, it assigns a failure object to `context.Result` and returns without calling the next stage. Otherwise, it continues invocation.

An illustrative JSON representation of the failure object is:

```json
{
  "status": "validation_failed",
  "errors": {
    "email": ["The email field is required."],
    "age": ["The field age must be between 18 and 120."]
  },
  "require_user_input": true
}
```

The filter returns an object through `FunctionResult`, rather than explicitly serializing JSON. Messages depend on the attributes, custom error messages, and runtime culture. Direct callers must inspect the result; validation failure does not currently throw a dedicated exception.

## Agent instructions and value provenance

`Program.cs` explicitly combines the customer agent instructions with a validation policy. The policy tells the agent to:

- Use values supplied by the user, trusted application context, or relevant successful tool results.
- Retrieve missing information through an authorized lookup tool when one is available, or ask the user.
- Avoid invented values, placeholders, and write operations performed merely to discover missing information.
- Respond to `validation_failed` by resolving the reported errors, without repeating unchanged invalid calls or claiming success.

The sample plugin contains no lookup tool; lookup guidance applies when an application supplies one. Registering the filter does not automatically inject agent instructions.

A value such as `unknown@unknown.com` can satisfy both `[Required]` and `[EmailAddress]` without being the customer's actual email address. Data Annotations validate declared constraints, not ownership, identity, authorization, or the source of a value. Those guarantees remain application responsibilities.

## Current limitations

- **Argument conversion:** The filter validates values directly from `KernelArguments`. It does not integrate with Semantic Kernel's conversion and binding pipeline or guarantee validation of the final declared .NET types. Incompatible values or validators that throw are not converted into structured failures.
- **Parameter names:** Lookup uses CLR parameter names; alternate names exposed through function metadata are not explicitly resolved.
- **Defaults:** Missing arguments and explicit `null` are treated identically. Replacement is not configurable. C# optional parameter defaults are not resolved by the filter before validation, and precedence between default mechanisms is not implemented as a configurable policy.
- **Missing values:** The filter checks declared attributes, not every function-binding requirement. Attributes such as `[Range]` alone do not establish requiredness; binding can still fail after validation.
- **Complex inputs:** Property-level validation, nested object traversal, and `IValidatableObject` are not implemented.
- **Injected parameters:** Framework-supplied parameters such as `Kernel` and `CancellationToken` are not explicitly excluded.
- **Custom validation context:** Each parameter receives a `ValidationContext` backed by a new plain object. Model-aware, cross-field, or service-dependent validators are not fully supported.
- **Failure handling:** The result shape is fixed and `require_user_input` is always `true` on failure. Separate configurable policies for direct invocation and agent tool calls are not implemented.
- **Filter ordering:** The prototype does not enforce ordering relative to other filters that may modify arguments after validation.
- **Invocation coverage:** The application uses streamed agent responses, but comprehensive streaming and non-streaming function invocation coverage is not established. The repository contains no automated test suite.
- **Schema generation:** This prototype adds no Data Annotations-to-JSON Schema enrichment. The proposed enhancement below is not implemented.

## Proposed enhancement: expose constraints in tool JSON Schema

An additional idea is to reflect supported Data Annotations constraints in the function parameter JSON Schema exposed to the model. **This is a proposal only; the prototype does not implement these mappings.**

| Data Annotation | Proposed JSON Schema representation |
| --- | --- |
| `[Required]` | Add the property name to the containing object's `required` array. |
| `[StringLength]` | Applicable `minLength` and `maxLength` constraints. |
| `[MinLength]`, `[MaxLength]` | Applicable `minLength` / `maxLength` for strings or `minItems` / `maxItems` for supported collections. |
| `[Range]` | Applicable `minimum` and `maximum` constraints for representable numeric ranges. |
| `[RegularExpression]` | `pattern`, where compatible with JSON Schema regex syntax and matching semantics. |
| `[EmailAddress]` | `format: "email"`, where supported. |

These mappings need semantic care: JSON Schema `required` expresses property presence and does not by itself reproduce all .NET `[Required]` checks for null, empty, or whitespace-only strings. Provider support and differences between validation systems must be considered before emitting constraints.

The accompanying agent instructions could include:

```text
Follow the validation constraints declared in each tool's parameter schema
when preparing arguments. If available values do not satisfy those constraints,
obtain corrected values through a relevant lookup tool or ask the user.
Do not invent values to satisfy the schema.
```

This would expose constraints before the model generates a tool call, while invocation validation would continue enforcing them before execution.

Schema generation should account for each provider's supported JSON Schema subset. Custom validators and constraints that cannot be represented accurately should remain enforced at runtime, with descriptive guidance where appropriate. Schema hints and agent instructions would not replace deterministic runtime validation.

## Project layout

```text
DataAnnotationsValidationPrototype.slnx
DataAnnotationsValidationPrototype/
  DataAnnotationsValidationPrototype.csproj
  Program.cs
  Plugins/
    CustomerPlugin.cs
  Validation/
    DataAnnotationsValidationFilter.cs
    DataAnnotationsValidationKernelBuilderExtensions.cs
```

Feedback on the API shape, argument-binding integration, default semantics, failure handling, and schema mapping is welcome in the [feature proposal](https://github.com/microsoft/semantic-kernel/issues/14847).
