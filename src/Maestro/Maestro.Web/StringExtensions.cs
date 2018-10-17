namespace Maestro.Web
{
    public static class StringExtensions
    {
        public static (string left, string right) Split2(this string value, char splitOn)
        {
            var idx = value.IndexOf(splitOn);

            if (idx < 0)
            {
                return (value, value.Substring(0, 0));
            }

            return (value.Substring(0, idx), value.Substring(idx + 1));
        }
    }
}