using DataAnnotationsValidationPrototype.Plugins;
using DataAnnotationsValidationPrototype.Validation;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

// Populate values from your OpenAI deployment
var modelId = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_ID");
var endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT");
var apiKey = Environment.GetEnvironmentVariable("AZURE_AI_APIKEY");

// Create a kernel with Azure OpenAI chat completion
var builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);

// Add data annotations validations filter
builder.AddDataAnnotationsValidations();

// Build the kernel
Kernel kernel = builder.Build();
kernel.ImportPluginFromType<CustomerPlugin>("CustomerPlugin");

const string AgentName = "CustomerAgent";
const string AgentInstructions = """
You are a Customer Service Agent responsible for greeting customers and registering new customers using the available kernel functions.

Use the available tools to perform these operations.

After successful execution, provide a clear, friendly confirmation to the user.
""";

const string ValidationInstructions = """
Before calling a tool:
- Use argument values supplied by the user, trusted application context,
  or successful results from relevant tools.
- If a required value is missing, first check whether an available,
  authorized lookup tool can retrieve it.
- You may chain tool calls when one tool's result supplies another tool's
  required arguments. Wait for each result before using it.
- Only use retrieved values that clearly belong to the intended user or
  entity. If multiple matches exist, ask the user to clarify.
- Do not perform write operations merely to discover missing values.
- Apply only explicitly declared defaults.
- Never invent values or use placeholders.
- If the value cannot be reliably retrieved, ask the user for it.

When a tool returns validation_failed:
- Use a relevant lookup tool if it can resolve the reported issue.
- Otherwise, explain the relevant field errors and ask the user for
  missing or corrected values.
- Do not repeat a failed call unless its relevant arguments have changed.
- Do not retry with guessed values or report that the operation succeeded.
""";

ChatCompletionAgent agent =
    new()
    {
        Name = AgentName,
        Instructions = $"{AgentInstructions} {ValidationInstructions}",
        Kernel = kernel,
        Arguments = new(new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
    };

ChatHistoryAgentThread agentThread = new();

// Initiate a back-and-forth chat
string? userInput;
do
{
    // Collect user input
    Console.Write("User > ");
    userInput = Console.ReadLine();
    // Print the results
    Console.Write("Agent > ");

    await foreach (var streamedData in agent.InvokeStreamingAsync(userInput ?? "", agentThread))
    {
        Console.Write(streamedData.Message);
    }

    Console.WriteLine();

} while (userInput is not null);


