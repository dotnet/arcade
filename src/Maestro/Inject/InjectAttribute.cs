using System;
using Microsoft.Azure.WebJobs.Description;

namespace Maestro.Inject
{
    [Binding]
    [AttributeUsage(AttributeTargets.Parameter)]
    public class InjectAttribute : Attribute
    {
    }
}
