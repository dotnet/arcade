using System.Data.SqlClient;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Maestro.Web
{
    public class HandleDuplicateKeyRowsAttribute : ActionFilterAttribute
    {
        public string ErrorMessage { get; }

        public HandleDuplicateKeyRowsAttribute(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            try
            {
                await base.OnActionExecutionAsync(context, next);
            }
            catch (DbUpdateException dbEx) when (dbEx.InnerException is SqlException sqlEx &&
                                                 sqlEx.Message.Contains("Cannot insert duplicate key row"))
            {
                context.Result = new ObjectResult(new ApiError(ErrorMessage)) {StatusCode = (int) HttpStatusCode.Conflict};
            }
        }
    }
}