namespace Maestro.Web.Api.v2018_07_16.Models
{
    public enum MergePolicy
    {
        None = 0,
        BuildSucceeded,
        UnitTestPassed, //Bulild + tests
        Never
    }
}
