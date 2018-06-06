using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Maestro.Inject
{
    public class InjectBinding : IBinding
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Type _type;

        public InjectBinding(IServiceProvider serviceProvider, Type type)
        {
            _serviceProvider = serviceProvider;
            _type = type;
        }

        public bool FromAttribute => true;

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            return Task.FromResult((IValueProvider) new ValueProvider(value));
        }

        public async Task<IValueProvider> BindAsync(BindingContext context)
        {
            await Task.Yield();

            IServiceScope scope = InjectBindingProvider.Scopes.GetOrAdd(
                context.FunctionInstanceId,
                _ =>
                {
                    IServiceScope s = _serviceProvider.CreateScope();
                    var factory = s.ServiceProvider.GetRequiredService<ILoggerFactory>();
                    var container = s.ServiceProvider.GetRequiredService<LoggerContainer>();
                    container.Logger = factory.CreateLogger(
                        LogCategories.CreateFunctionUserCategory(context.ValueContext.FunctionContext.MethodName));
                    return s;
                });

            object value = scope.ServiceProvider.GetRequiredService(_type);
            return await BindAsync(value, context.ValueContext);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor();
        }

        private class ValueProvider : IValueProvider
        {
            private readonly object _value;

            public ValueProvider(object value)
            {
                _value = value;
            }

            public Task<object> GetValueAsync()
            {
                return Task.FromResult(_value);
            }

            public string ToInvokeString()
            {
                return _value.ToString();
            }

            public Type Type => _value.GetType();
        }
    }
}
