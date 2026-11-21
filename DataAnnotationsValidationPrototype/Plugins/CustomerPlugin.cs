using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace DataAnnotationsValidationPrototype.Plugins
{
    public class CustomerPlugin
    {
        [KernelFunction]
        public string GetCustomerGreeting([Required] string name, [DefaultValue("Hello")] string greeting)
        {
            return $"{greeting}, {name}!";
        }

        [KernelFunction]
        public string RegisterCustomer([Required, StringLength(100)] string name, [Required, EmailAddress] string email, [Range(18, 120)] int age)
        {
            return "Customer registered.";
        }
    }

}
