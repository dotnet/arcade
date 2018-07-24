using System.Collections.Generic;

namespace Maestro.Web
{
    public class ApiError
    {
        public ApiError(string message, IEnumerable<string> errors = null)
        {
            Message = message;
            Errors = errors;
        }

        public string Message { get; }
        public IEnumerable<string> Errors { get; }
    }
}
