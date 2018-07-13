using Maestro.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;

namespace Maestro.Web.Controllers
{
    public class BaseController : Controller
    {
        public ActionResult HandleNotFoundResponse<T>(T result, object obj) where T : class
        {
            return result == null ?
                NotFound(obj) :
                StatusCode((int)HttpStatusCode.OK, result);
        }

        public ActionResult CreateQueryExpression<T>(T entity, out Expression<Func<T, bool>> expressionFunction)
        {
            Type type = typeof(T);
            ParameterExpression parameter = Expression.Parameter(type, "e");
            List<BinaryExpression> expressions = new List<BinaryExpression>();
            expressionFunction = null;

            foreach (PropertyInfo property in type.GetProperties())
            {
                object val = property.GetValue(entity);
                string propertyName = property.Name;
                Type propertyType = property.PropertyType;
                
                if (!Equals(val, GetDefault(propertyType)))
                {
                    expressions.Add(
                        Expression.Equal(
                            Expression.Property(parameter, propertyName),
                            Expression.Constant(val, propertyType)
                        )
                    );
                }
            }

            if (!expressions.Any())
            {
                Error error = new Error("No query parameters were provided...");
                return BadRequest(error);
            }

            BinaryExpression expression = null;

            if (expressions.Count == 1)
            {
                expression = expressions[0];
            }
            else
            {
                expression = Expression.AndAlso(expressions[0], expressions[1]);

                for (int i = 2; i < expressions.Count; i++)
                {
                    expression = Expression.AndAlso(expression, expressions[i]);
                }
            }

            expressionFunction = Expression.Lambda<Func<T, bool>>(expression, parameter);

            return null;
        }

        private object GetDefault(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }
    }
}
