// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlClient;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Internal;

namespace Maestro.Web
{
    public class HandleDuplicateKeyRowsAttribute : ActionFilterAttribute
    {
        public HandleDuplicateKeyRowsAttribute(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        public string ErrorMessage { get; }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();
            if (executedContext.Exception is DbUpdateException dbEx &&
                dbEx.InnerException is SqlException sqlEx &&
                sqlEx.Message.Contains("Cannot insert duplicate key row"))
            {
                executedContext.Exception = null;

                var message = ErrorMessage;
                foreach (var argument in context.ActionArguments)
                {
                    message = message.Replace("{" + argument.Key + "}", argument.Value.ToString());
                }

                executedContext.Result =
                    new ObjectResult(new ApiError(message)) {StatusCode = (int) HttpStatusCode.Conflict};
            }
        }
    }
}
