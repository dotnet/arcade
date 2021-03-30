using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility
{
    public class DiffingSettings
    {
        public IRuleDriverFactory RuleDriverFactory { get; }
        public IDiffingFilter Filter { get; }

        public DiffingSettings(IRuleDriverFactory ruleDriverFactory = null, IDiffingFilter filter = null)
        {
            RuleDriverFactory = ruleDriverFactory ?? new RuleDriverFactory();
            Filter = filter ?? new AccessibilityFilter(includeInternalSymbols: false);
        }
    }
}