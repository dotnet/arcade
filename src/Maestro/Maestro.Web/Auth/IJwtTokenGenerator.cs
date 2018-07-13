namespace Maestro.Web
{
    public interface IJwtTokenGenerator
    {
        string Generate();
        string TryGenerate();
    }
}
