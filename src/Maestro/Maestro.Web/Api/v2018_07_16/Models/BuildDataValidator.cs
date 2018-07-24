using FluentValidation;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class BuildDataValidator : AbstractValidator<BuildData>
    {
        public BuildDataValidator()
        {
            RuleFor(b => b.Assets).NotEmpty();
        }
    }
}
