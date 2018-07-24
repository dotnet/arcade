namespace Maestro.Web.Data.Models
{
    public enum MergePolicy
    {
        None = 0,
        BuildSucceeded,
        UnitTestPassed, //Bulild + tests
        Never
    }
}
