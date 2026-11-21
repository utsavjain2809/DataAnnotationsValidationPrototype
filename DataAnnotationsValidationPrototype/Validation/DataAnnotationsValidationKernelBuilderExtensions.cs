using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;

namespace DataAnnotationsValidationPrototype.Validation
{
    public static class DataAnnotationsValidationKernelBuilderExtensions
    {
        public static IKernelBuilder AddDataAnnotationsValidations(this IKernelBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder); 
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IFunctionInvocationFilter, DataAnnotationsValidationFilter>());
            return builder;
        }
    }
}
