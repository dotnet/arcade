using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using Swashbuckle.AspNetCore.Swagger;

namespace Microsoft.DotNet.SwaggerGenerator.Modeler
{
    public class ServiceClientModelFactory
    {
        public static readonly AttachedProperty<Schema, string> SchemaName = new AttachedProperty<Schema, string>();

        public static readonly AttachedProperty<Schema, Schema> ResolvedReference =
            new AttachedProperty<Schema, Schema>();

        private static readonly Regex ReferenceRegex = new Regex("^#/definitions/(?<name>.+)$");


        private static readonly AttachedProperty<Schema, TypeModel> TypeModel =
            new AttachedProperty<Schema, TypeModel>();

        private readonly Dictionary<string, EnumTypeModel> _enumTypeModels = new Dictionary<string, EnumTypeModel>();

        private readonly GeneratorOptions _generatorOptions;

        private readonly AsyncLocal<Stack<string>> _propertyNameStack = new AsyncLocal<Stack<string>>();
        private readonly AsyncLocal<Stack<string>> _typeNameStack = new AsyncLocal<Stack<string>>();
        private readonly AsyncLocal<Stack<string>> _parameterNameStack = new AsyncLocal<Stack<string>>();
        private readonly AsyncLocal<Stack<string>> _methodNameStack = new AsyncLocal<Stack<string>>();

        private readonly List<TypeModel> Types = new List<TypeModel>();

        public ServiceClientModelFactory(GeneratorOptions options)
        {
            _generatorOptions = options;
            _typeNameStack.Value = new Stack<string>();
            _propertyNameStack.Value = new Stack<string>();
            _parameterNameStack.Value = new Stack<string>();
            _methodNameStack.Value = new Stack<string>();
        }
        private string CurrentMethodName => _methodNameStack.Value.Count != 0 ? _methodNameStack.Value.Peek() : null;

        private string CurrentParameterName => _parameterNameStack.Value.Count != 0 ? _parameterNameStack.Value.Peek() : "Value";

        private string CurrentTypeName => _typeNameStack.Value.Count != 0 ? _typeNameStack.Value.Peek() : null;

        private string CurrentPropertyName => _propertyNameStack.Value.Count != 0 ? _propertyNameStack.Value.Peek() : "Value";

        private static void ResolveReferences(SwaggerDocument document)
        {
            void MatchRef(Schema schema)
            {
                if (schema == null)
                {
                    return;
                }

                if (schema.Type == "array")
                {
                    MatchRef(schema.Items);
                    return;
                }

                if (string.IsNullOrEmpty(schema.Ref))
                {
                    return;
                }

                Match match = ReferenceRegex.Match(schema.Ref);
                if (match.Success)
                {
                    string name = match.Groups["name"].ToString();
                    Schema resolved = document.Definitions[name];
                    SchemaName.Set(resolved, name);
                    ResolvedReference.Set(schema, resolved);
                }
                else
                {
                    throw new ArgumentException($"Ref '{schema.Ref}' not found.");
                }
            }

            foreach ((string defName, Schema scheme) in document.Definitions)
            {
                foreach ((string propName, Schema prop) in scheme.Properties)
                {
                    MatchRef(prop);
                }
            }

            foreach ((string path, PathItem pathItem) in document.Paths)
            {
                foreach ((string method, Operation operation) in GetOperations(pathItem))
                {
                    if (operation.Parameters != null)
                    {
                        foreach (IParameter parameter in operation.Parameters)
                        {
                            if (parameter is BodyParameter bodyParameter)
                            {
                                MatchRef(bodyParameter.Schema);
                            }
                        }
                    }

                    foreach ((string status, Response response) in operation.Responses)
                    {
                        MatchRef(response.Schema);
                    }
                }
            }
        }

        public static IEnumerable<(string method, Operation operation)> GetOperations(PathItem pathItem)
        {
            if (pathItem.Get != null)
            {
                yield return ("get", pathItem.Get);
            }

            if (pathItem.Put != null)
            {
                yield return ("put", pathItem.Put);
            }

            if (pathItem.Post != null)
            {
                yield return ("post", pathItem.Post);
            }

            if (pathItem.Delete != null)
            {
                yield return ("delete", pathItem.Delete);
            }

            if (pathItem.Options != null)
            {
                yield return ("options", pathItem.Options);
            }

            if (pathItem.Head != null)
            {
                yield return ("head", pathItem.Head);
            }

            if (pathItem.Patch != null)
            {
                yield return ("patch", pathItem.Patch);
            }
        }

        private IDisposable WithMethodName(string name)
        {
            _methodNameStack.Value.Push(name);
            return Disposable.Create(
                () =>
                {
                    if (_methodNameStack.Value.Pop() != name)
                    {
                        throw new InvalidOperationException(
                            $"Method name '{name}' popped when it wasn't the top of the stack.");
                    }
                });
        }

        private IDisposable WithParameterName(string name)
        {
            _parameterNameStack.Value.Push(name);
            return Disposable.Create(
                () =>
                {
                    if (_parameterNameStack.Value.Pop() != name)
                    {
                        throw new InvalidOperationException(
                            $"Parameter name '{name}' popped when it wasn't the top of the stack.");
                    }
                });
        }

        private IDisposable WithTypeName(string name)
        {
            _typeNameStack.Value.Push(name);
            return Disposable.Create(
                () =>
                {
                    if (_typeNameStack.Value.Pop() != name)
                    {
                        throw new InvalidOperationException(
                            $"Type name '{name}' popped when it wasn't the top of the stack.");
                    }
                });
        }

        private IDisposable WithPropertyName(string name)
        {
            _propertyNameStack.Value.Push(name);
            return Disposable.Create(
                () =>
                {
                    if (_propertyNameStack.Value.Pop() != name)
                    {
                        throw new InvalidOperationException(
                            $"Property name '{name}' popped when it wasn't the top of the stack.");
                    }
                });
        }

        public ServiceClientModel Create(SwaggerDocument document)
        {
            ResolveReferences(document);

            GeneratorOptions options = _generatorOptions;
            ImmutableList<MethodGroupModel> methodGroups = document.Paths
                .SelectMany(p => GetOperations(p.Value).Select(o => (path: p.Key, o.method, o.operation)))
                .ToLookup(t => t.Item3.Tags.FirstOrDefault())
                .Select(g => CreateMethodGroupModel(g.Key, g))
                .ToImmutableList();
            return new ServiceClientModel(
                options.ClientName,
                options.Namespace,
                document.Host,
                document.Schemes.First(),
                Types.Concat(_enumTypeModels.Values).OrderBy(m => m.Name),
                methodGroups);
        }

        private MethodGroupModel CreateMethodGroupModel(
            string name,
            IEnumerable<(string path, string method, Operation operation)> operations)
        {
            var methods = new List<MethodModel>();
            foreach ((string path, string method, Operation operation) in operations)
            {
                methods.Add(CreateMethodModel(path, method, operation));
            }

            return new MethodGroupModel(name, _generatorOptions.Namespace, methods);
        }

        private MethodModel CreateMethodModel(string path, string method, Operation operation)
        {

            string name = operation.OperationId;
            string firstTag = operation.Tags.FirstOrDefault();
            if (firstTag != null && name.StartsWith(firstTag))
            {
                name = name.Substring(firstTag.Length);
            }

            name = name.TrimStart('_');
            using (WithMethodName(name))
            {
                IList<ParameterModel> parameters =
                    (IList<ParameterModel>) operation.Parameters?.Select(CreateParameterModel).ToList() ??
                    Array.Empty<ParameterModel>();

                TypeReference errorType = operation.Responses.TryGetValue("default", out Response errorResponse)
                    ? ResolveType(errorResponse.Schema, name)
                    : null;

                TypeReference responseType = operation.Responses.Where(r => r.Key.StartsWith("2"))
                    .Select(r => ResolveType(r.Value.Schema, name))
                    .FirstOrDefault();

                return new MethodModel(name, path, GetHttpMethod(method), responseType, errorType, parameters);
            }
        }

        private HttpMethod GetHttpMethod(string method)
        {
            switch (method.ToLower())
            {
                case "get":
                    return HttpMethod.Get;
                case "put":
                    return HttpMethod.Put;
                case "post":
                    return HttpMethod.Post;
                case "delete":
                    return HttpMethod.Delete;
                case "options":
                    return HttpMethod.Options;
                case "head":
                    return HttpMethod.Head;
                case "patch":
                    return new HttpMethod("PATCH");
                default:
                    throw new NotSupportedException(method);
            }
        }

        private ParameterModel CreateParameterModel(IParameter parameter)
        {
            using (WithParameterName(parameter.Name))
            {
                TypeReference type = null;
                if (parameter is BodyParameter bodyParameter)
                {
                    type = ResolveType(bodyParameter.Schema);
                }

                if (parameter is NonBodyParameter nonBodyParameter)
                {
                    type = ResolveType(nonBodyParameter);
                }

                ParameterLocation location;
                switch (parameter.In)
                {
                    case "query":
                        location = ParameterLocation.Query;
                        break;
                    case "path":
                        location = ParameterLocation.Path;
                        break;
                    case "header":
                        location = ParameterLocation.Header;
                        break;
                    case "body":
                        location = ParameterLocation.Body;
                        break;
                    default:
                        throw new NotSupportedException(parameter.In);
                }

                return new ParameterModel(parameter.Name, parameter.Required, location, type);
            }
        }

        private TypeReference ResolveType(object schema)
        {
            if (schema == null)
            {
                return TypeReference.Void;
            }

            string type;
            string format;
            object items;
            IList<object> enumeration;
            {
                if (schema is Schema s)
                {
                    schema = s = ResolvedReference.Get(s) ?? s;
                    type = s.Type;
                    format = s.Format;
                    enumeration = s.Enum;
                    items = s.Items;
                }
                else if (schema is PartialSchema ps)
                {
                    type = ps.Type;
                    format = ps.Format;
                    enumeration = ps.Enum;
                    items = ps.Items;
                }
                else
                {
                    throw new NotSupportedException(schema?.GetType()?.FullName ?? "null");
                }
            }
            switch (type)
            {
                case "boolean":
                    return TypeReference.Boolean;
                case "integer":
                    switch (format)
                    {
                        case "int32":
                            return TypeReference.Int32;
                        case "int64":
                        default:
                            return TypeReference.Int64;
                    }
                case "number":
                    switch (format)
                    {
                        case "float":
                            return TypeReference.Float;
                        case "double":
                        default:
                            return TypeReference.Double;
                    }
                case "string":
                    if (enumeration != null)
                    {
                        if (enumeration.Count == 1)
                        {
                            return TypeReference.Constant((string) enumeration[0]);
                        }

                        string enumName;
                        if (CurrentTypeName != null)
                        {
                            enumName =
                                $"{Helpers.PascalCase(CurrentTypeName.AsSpan())}{Helpers.PascalCase(CurrentPropertyName.AsSpan())}";
                        }
                        else
                        {
                            enumName =
                                $"{Helpers.PascalCase(CurrentMethodName.AsSpan())}{Helpers.PascalCase(CurrentParameterName.AsSpan())}";

                        }


                        return TypeReference.Object(ResolveEnumTypeModel(enumeration, enumName));
                    }

                    switch (format)
                    {
                        case "byte":
                            return TypeReference.Byte;
                        case "date":
                            return TypeReference.Date;
                        case "date-time":
                            return TypeReference.DateTime;
                        default:
                            return TypeReference.String;
                    }
                case "array":
                    return TypeReference.Array(ResolveType(items));
                case "object" when schema is Schema s:
                    if (s.Properties == null && s.AdditionalProperties != null)
                    {
                        return TypeReference.Dictionary(ResolveType(s.AdditionalProperties));
                    }

                    return TypeReference.Object(ResolveTypeModel(s));
                case "file":
                    return TypeReference.File;
                case null:
                    return TypeReference.Any;
                case "null":
                default:
                    throw new NotSupportedException(type);
            }
        }

        private TypeModel ResolveEnumTypeModel(IList<object> enumeration, string enumName)
        {
            if (!_enumTypeModels.TryGetValue(enumName, out EnumTypeModel value))
            {
                _enumTypeModels[enumName] = value = new EnumTypeModel(
                    enumName,
                    _generatorOptions.Namespace,
                    enumeration.Select(v => (string) v));
            }

            return value;
        }

        private TypeReference ResolveType(PartialSchema schema)
        {
            return ResolveType((object) schema);
        }

        private TypeReference ResolveType(Schema schema, string operationId)
        {
            using (WithTypeName(operationId))
            using (WithPropertyName("Response"))
            {
                return ResolveType((object) schema);
            }
        }

        private TypeReference ResolveType(Schema schema)
        {
            return ResolveType((object) schema);
        }

        private TypeModel ResolveTypeModel(Schema schema)
        {
            if (schema.Type != "object")
            {
                throw new ArgumentException("Schema must be object", nameof(schema));
            }

            return TypeModel.GetOrAdd(schema, CreateTypeModel);
        }

        private TypeModel CreateTypeModel(Schema schema)
        {
            string name = SchemaName.Get(schema);
            if (string.IsNullOrEmpty(name))
            {
                name = Helpers.PascalCase((CurrentTypeName + "-" + CurrentPropertyName).AsSpan());
            }

            using (WithTypeName(name))
            {
                HashSet<string> requiredProperties =
                    schema.Required != null ? new HashSet<string>(schema.Required) : null;
                IEnumerable<PropertyModel> properties = schema.Properties != null
                    ? schema.Properties.Select(
                        p =>
                        {
                            using (WithPropertyName(p.Key))
                            {
                                return CreatePropertyModel(
                                    p.Key,
                                    p.Value,
                                    requiredProperties?.Contains(p.Key) ?? false);
                            }
                        })
                    : Array.Empty<PropertyModel>();
                TypeReference additionalProperties;
                using (WithPropertyName("Items"))
                {
                    additionalProperties = schema.AdditionalProperties != null
                        ? ResolveType(schema.AdditionalProperties)
                        : null;
                }

                var model = new ClassTypeModel(name, _generatorOptions.Namespace, properties, additionalProperties);
                Types.Add(model);
                return model;
            }
        }

        private PropertyModel CreatePropertyModel(string name, Schema type, bool required)
        {
            return new PropertyModel(name, required, type.ReadOnly ?? false, ResolveType(type));
        }
    }
}
