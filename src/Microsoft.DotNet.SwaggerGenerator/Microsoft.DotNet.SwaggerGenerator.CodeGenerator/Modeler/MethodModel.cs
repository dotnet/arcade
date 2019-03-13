using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using Microsoft.OpenApi.Models;

namespace Microsoft.DotNet.SwaggerGenerator.Modeler
{
    public class MethodModel
    {
        public MethodModel(
            string name,
            string path,
            HttpMethod httpMethod,
            TypeReference responseType,
            TypeReference errorType,
            IEnumerable<ParameterModel> parameters)
        {
            Name = name;
            Path = path;
            HttpMethod = httpMethod;
            ResponseType = responseType;
            ErrorType = errorType;
            Parameters = parameters.ToImmutableList();
        }

        public string Name { get; }
        public string Path { get; }
        public HttpMethod HttpMethod { get; }
        public IImmutableList<ParameterModel> Parameters { get; }
        public TypeReference ResponseType { get; }
        public TypeReference ErrorType { get; }

        public bool ResponseIsVoid => ResponseType == TypeReference.Void;

        public bool ResponseIsFile => ResponseType == TypeReference.File;

        public IEnumerable<ParameterModel> ConstantParameters =>
            Parameters.Where(p => p.Type is TypeReference.ConstantTypeReference).OrderBy(p => p.Name);

        public IEnumerable<ParameterModel> NonConstantParameters =>
            Parameters.Where(p => !(p.Type is TypeReference.ConstantTypeReference)).OrderBy(p => p.Name);

        public IEnumerable<ParameterModel> FormalParameters =>
            NonConstantParameters.OrderBy(p => p.Required ? 0 : 1).ThenBy(p => p.Name);

        public IEnumerable<ParameterModel> PathParameters =>
            Parameters.Where(p => p.Location == ParameterLocation.Path);

        public IEnumerable<ParameterModel> QueryParameters =>
            Parameters.Where(p => p.Location == ParameterLocation.Query);

        public IEnumerable<ParameterModel> HeaderParameters =>
            Parameters.Where(p => p.Location == ParameterLocation.Header);

        public ParameterModel BodyParameter => Parameters.SingleOrDefault(p => p.Location == null);
    }
}
