using System.Collections.Generic;
using System.Linq;

namespace Microsoft.SignCheck.Verification.Jar
{
    public static class JarError
    {
        private static List<string> _errors = new List<string>();

        public static void AddError(string error)
        {
            _errors.Add(error);
        }

        public static void ClearErrors()
        {
            _errors.Clear();
        }

        public static bool HasErrors()
        {
            return _errors.Count() > 0;
        }
        
        public static string GetLastError()
        {
            return _errors.Last();
        }
    }
}
