// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.ApiPagination
{
    [AttributeUsage(AttributeTargets.Method)]
    [PublicAPI]
    public class PaginatedAttribute : ActionFilterAttribute
    {
        public PaginatedAttribute(Type resultType)
        {
            ResultType = resultType;
        }

        public string PageParameterName { get; set; } = "page";

        public string PageSizeParameterName { get; set; } = "perPage";

        public int PageSizeLimit { get; set; } = 100;

        public int DefaultPageSize { get; set; } = 30;

        public Type ResultType { get; }

        /// <summary>
        ///     Method that exists to allow creation of ParameterModel objects
        /// </summary>
        [UsedImplicitly]
        private static void Method(int? page, int? perPage)
        {
        }

        public IEnumerable<ParameterModel> CreateParameterModels()
        {
            MethodInfo methodMethod = typeof(PaginatedAttribute).GetTypeInfo()
                .DeclaredMethods.Single(m => m.Name == "Method");

            ParameterInfo pageParameterInfo = methodMethod.GetParameters()[0];
            var pageParameter = new ParameterModel(pageParameterInfo, Array.Empty<object>())
            {
                ParameterName = PageParameterName
            };

            ParameterInfo perPageParameterInfo = methodMethod.GetParameters()[1];
            var perPageParameter = new ParameterModel(perPageParameterInfo, Array.Empty<object>())
            {
                ParameterName = PageSizeParameterName
            };

            yield return pageParameter;
            yield return perPageParameter;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            HttpRequest req = context.HttpContext.Request;
            StringValues pageText = req.Query[PageParameterName];
            int page;
            if (string.IsNullOrEmpty(pageText))
            {
                page = 1;
            }
            else if (!int.TryParse(pageText, out page))
            {
                context.ModelState.TryAddModelError(
                    PageParameterName,
                    $"The parameter '{PageParameterName}' must be an integer.");
            }

            if (page < 1)
            {
                context.ModelState.TryAddModelError(
                    PageParameterName,
                    $"The parameter '{PageParameterName}' must be greater than 0");
            }

            StringValues perPageText = req.Query[PageSizeParameterName];
            int perPage;
            if (string.IsNullOrEmpty(perPageText))
            {
                perPage = DefaultPageSize;
            }
            else if (!int.TryParse(req.Query[PageSizeParameterName], out perPage))
            {
                context.ModelState.TryAddModelError(
                    PageSizeParameterName,
                    $"The parameter '{PageSizeParameterName}' must be an integer.");
            }

            if (perPage <= 1)
            {
                context.ModelState.TryAddModelError(
                    PageSizeParameterName,
                    $"The parameter '{PageSizeParameterName}' must be greater than 1");
            }
            else if (perPage > PageSizeLimit)
            {
                context.ModelState.TryAddModelError(
                    PageSizeParameterName,
                    $"The parameter '{PageSizeParameterName}' cannot be greater than {PageSizeLimit}");
            }

            context.RouteData.Values[nameof(PaginatedAttribute) + ".page"] = page;
            context.RouteData.Values[nameof(PaginatedAttribute) + ".perPage"] = perPage;

            await next();
        }

        public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            await TransformResultAsync(context);
            await base.OnResultExecutionAsync(context, next);
        }

        [UsedImplicitly]
        private async Task TransformResultAsync<TData>(
            ResultExecutingContext context,
            ObjectResult result,
            IQueryable<TData> query)
        {
            var page = (int) context.RouteData.Values[nameof(PaginatedAttribute) + ".page"];
            var perPage = (int) context.RouteData.Values[nameof(PaginatedAttribute) + ".perPage"];

            int rowCount = await query.CountAsync();

            if (rowCount == 0)
            {
                result.Value = Array.Empty<object>();
                return;
            }

            var pageCount = (int) Math.Ceiling((double) rowCount / perPage);
            if (page > pageCount)
            {
                context.Result = new NotFoundResult();
                return;
            }

            AddLinkHeader(context, page, perPage, pageCount);

            IServiceProvider requestServices = context.HttpContext.RequestServices;
            var urlHelperFactory = requestServices.GetRequiredService<IUrlHelperFactory>();
            IUrlHelper urlHelper = urlHelperFactory.GetUrlHelper(context);
            var serviceProvider = new ExtendedServiceProvider(requestServices)
            {
                urlHelper,
                context.HttpContext
            };
            result.Value = query.Skip((page - 1) * perPage)
                .Take(perPage)
                .AsEnumerable()
                .Select(o => ActivatorUtilities.CreateInstance(serviceProvider, ResultType, o));
        }

        private async Task TransformResultAsync(ResultExecutingContext context)
        {
            if (context.Result is ObjectResult result &&
                result.Value.IsGenericInterface(typeof(IQueryable<>), out Type typeParam))
            {
                await (Task) typeof(PaginatedAttribute).GetTypeInfo()
                    .DeclaredMethods.Single(m => m.IsGenericMethod && m.Name == "TransformResultAsync")
                    .MakeGenericMethod(typeParam)
                    .Invoke(this, new[] {context, result, result.Value});
            }
        }

        private void AddLinkHeader(ResultExecutingContext context, int page, int perPage, int pageCount)
        {
            HttpRequest req = context.HttpContext.Request;
            var currentUri = new UriBuilder
            {
                Path = req.Path,
                Scheme = "https",
                Host = req.Host.Host
            };
            if (req.Host.Port.HasValue)
            {
                currentUri.Port = req.Host.Port.Value;
            }

            IQueryCollection query = context.HttpContext.Request.Query;
            var links = new List<(string rel, string href)>();

            void AddLink(string rel, int p)
            {
                var linkQuery =
                    new QueryBuilder(
                        query.Where(pair => pair.Key != PageParameterName && pair.Key != PageSizeParameterName)
                            .SelectMany(pair => pair.Value.Select(v => new KeyValuePair<string, string>(pair.Key, v))))
                    {
                        {PageParameterName, p.ToString()},
                        {PageSizeParameterName, perPage.ToString()}
                    };

                links.Add((rel, new UriBuilder(currentUri.Uri) {Query = linkQuery.ToString()}.Uri.AbsoluteUri));
            }

            if (page > 1)
            {
                AddLink("first", 1);
                AddLink("prev", page - 1);
            }

            if (page < pageCount)
            {
                AddLink("next", page + 1);
                AddLink("last", pageCount);
            }

            context.HttpContext.Response.Headers["Link"] = string.Join(
                ", ",
                links.Select(l => $"<{l.href}>; rel=\"{l.rel}\""));
        }
    }

    internal class ExtendedServiceProvider : Dictionary<Type, object>, IServiceProvider
    {
        public ExtendedServiceProvider(IServiceProvider inner)
        {
            Inner = inner;
        }

        public IServiceProvider Inner { get; }

        public object GetService(Type serviceType)
        {
            if (TryGetValue(serviceType, out object value))
            {
                return value;
            }

            return Inner.GetService(serviceType);
        }

        public void Add<T>(T value)
        {
            Add(typeof(T), value);
        }
    }
}
