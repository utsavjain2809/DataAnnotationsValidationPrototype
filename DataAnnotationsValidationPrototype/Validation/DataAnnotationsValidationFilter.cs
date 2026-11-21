using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace DataAnnotationsValidationPrototype.Validation
{
    public sealed class DataAnnotationsValidationFilter : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(Microsoft.SemanticKernel.FunctionInvocationContext context, Func<Microsoft.SemanticKernel.FunctionInvocationContext, Task> next)
        {
            var method = context.Function.UnderlyingMethod;

            if (method is null)
            {
                await next(context);
                return;
            }

            var errors = new Dictionary<string, List<string>>();

            foreach (var parameter in method.GetParameters())
            {
                var name = parameter.Name!;
                context.Arguments.TryGetValue(name, out var value);

                var defaultValueAttribute = parameter.GetCustomAttribute<DefaultValueAttribute>();

                if (value is null && defaultValueAttribute is not null)
                {
                    value = defaultValueAttribute.Value;
                    context.Arguments[name] = value;
                }

                // Run ValidationAttribute checks after applying the default.

                var validationContext = new ValidationContext(new object())
                {
                    MemberName = name,
                    DisplayName = name
                };

                foreach (var attribute in parameter
                    .GetCustomAttributes(typeof(ValidationAttribute), inherit: true)
                    .Cast<ValidationAttribute>())
                {
                    var result = attribute.GetValidationResult(value, validationContext);

                    if (result != ValidationResult.Success)
                    {
                        if (!errors.TryGetValue(name, out var messages))
                        {
                            messages = new List<string>();
                            errors[name] = messages;
                        }

                        messages.Add(result!.ErrorMessage ?? $"{name} is invalid.");
                    }
                }
            }

            if (errors.Count > 0)
            {
                context.Result = new FunctionResult(context.Function, new
                {
                    status = "validation_failed",
                    errors,
                    require_user_input = true,
                });

                return; // Prevent execution of the plugin method.
            }

            await next(context);
        }
    }
}
