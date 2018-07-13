namespace BuildAssetRegistryModel
{
    public enum MergePolicy
    {
        BuildSucceeded,
        UnitTestPassed, //Bulild + tests
        Never,
    }
}
