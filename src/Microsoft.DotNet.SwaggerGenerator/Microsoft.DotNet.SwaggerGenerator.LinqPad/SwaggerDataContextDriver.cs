using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using LINQPad.Extensibility.DataContext;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.SwaggerGenerator.Modeler;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public class SwaggerDataContextDriver : DynamicDataContextDriver
    {
        public SwaggerDataContextDriver()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                var shortName = new AssemblyName(e.Name).Name;
                var dllPath = Path.Combine(GetDriverFolder(), shortName + ".dll");

                if (File.Exists(dllPath))
                {
                    return LoadAssemblySafely(dllPath);
                }

                return null;
            };
        }

        public override string GetConnectionDescription(IConnectionInfo cxInfo)
        {
            return new SwaggerProperties(cxInfo).Uri;
        }

        public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection)
        {
            using (var dialog = new ConnectionDialog(new SwaggerProperties(cxInfo)))
            {
                return dialog.ShowDialog() == DialogResult.OK;
            }
        }

        public override string Name => "Swagger";
        public override string Author => "Microsoft";

        public override List<ExplorerItem> GetSchemaAndBuildAssembly(
            IConnectionInfo cxInfo,
            AssemblyName assemblyToBuild,
            ref string nameSpace,
            ref string typeName)
        {
            var properties = new SwaggerProperties(cxInfo);
            var uri = properties.Uri;


            var options = new GeneratorOptions
            {
                Namespace = nameSpace,
                ClientName = typeName + "ApiClient",
                LanguageName = "csharp",
            };
            ServiceClientModel model = GetModelAsync(uri, options).GetAwaiter().GetResult();

            var codeFactory = new ServiceClientCodeFactory();
            var code = codeFactory.GenerateCode(model, options, NullLogger.Instance);

            var contextClass = $@"
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.SwaggerGenerator.LinqPad;

namespace {nameSpace}
{{
    public class {typeName} : {typeName}ApiClient, ISwaggerContext
    {{
        private ISwaggerContextLogger _logger;
        ISwaggerContextLogger ISwaggerContext.SwaggerContextLogger
        {{
            set => _logger = value;
        }}

        public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {{
            if (_logger != null)
            {{
                await _logger.RequestStarting(request);
            }}
            var response = await base.SendAsync(request, cancellationToken);
            if (_logger != null)
            {{
                await _logger.RequestFinished(request, response);
            }}

            return response;
        }}
    }}
}}
";
            code.Add(new CodeFile("Context.cs", contextClass));

            BuildAssembly(code, assemblyToBuild);

            return GetSchema(model).ToList();
        }

        public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
        {
            var ctx = (ISwaggerContext) context;
            ctx.SwaggerContextLogger = new SwaggerContextLogger(executionManager.SqlTranslationWriter);
            base.InitializeContext(cxInfo, context, executionManager);
        }

        public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo)
        {
            return new[]
            {
                "Microsoft.DotNet.SwaggerGenerator.LinqPad.dll",
                "Microsoft.Rest.ClientRuntime.dll",
                "Newtonsoft.Json.dll",
                "System.Collections.Immutable.dll",
                "System.Net.Http.dll",
            };
        }

        public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo cxInfo)
        {
            return new[]
            {
                "Microsoft.Rest",
                "Newtonsoft.Json",
                "Newtonsoft.Json.Linq",
                "System.Collections.Immutable",
            };
        }

        private IEnumerable<ExplorerItem> GetSchema(ServiceClientModel model)
        {
            yield return new ExplorerItem("Definitions", ExplorerItemKind.Category, ExplorerIcon.Box)
            {
                Children = GetDefinitions(model).ToList(),
            };
            yield return new ExplorerItem("Apis", ExplorerItemKind.Category, ExplorerIcon.Box)
            {
                Children = GetApis(model).ToList(),
            };
        }

        private IEnumerable<ExplorerItem> GetDefinitions(ServiceClientModel model)
        {
            foreach (var type in model.Types)
            {
                if (type is EnumTypeModel enumType)
                {
                    yield return EnumExplorerItem(enumType);
                }
                else
                {
                    yield return TypeExplorerItem((ClassTypeModel)type);
                }
            }
        }

        private ExplorerItem EnumExplorerItem(EnumTypeModel type)
        {
            return new ExplorerItem(type.Name, ExplorerItemKind.Schema, ExplorerIcon.Table)
            {
                Children = type.Values.Select(v => new ExplorerItem(v, ExplorerItemKind.Property, ExplorerIcon.Blank)).ToList(),
            };
        }

        private ExplorerItem TypeExplorerItem(ClassTypeModel type)
        {
            return new ExplorerItem(type.Name, ExplorerItemKind.Schema, ExplorerIcon.Table)
            {
                Children = type.Properties.Select(p => new ExplorerItem($"{p.Name}: {p.Type}", ExplorerItemKind.Property, ExplorerIcon.Blank)).ToList(),
            };
        }

        private IEnumerable<ExplorerItem> GetApis(ServiceClientModel model)
        {
            foreach (var group in model.MethodGroups)
            {
                yield return new ExplorerItem(group.Name, ExplorerItemKind.Category, ExplorerIcon.Box)
                {
                    Children = group.Methods.Select(Operation).ToList(),
                };
            }
        }

        private ExplorerItem Operation(MethodModel method)
        {
            return new ExplorerItem(method.Name, ExplorerItemKind.Property, ExplorerIcon.StoredProc)
            {
                Children = method.Parameters.Where(p => !p.IsConstant).Select(Parameter).ToList(),
            };
        }

        private ExplorerItem Parameter(ParameterModel model)
        {
            return new ExplorerItem($"{model.Name}: {model.Type}", ExplorerItemKind.Parameter, ExplorerIcon.Parameter);
        }

        private void BuildAssembly(List<CodeFile> code, AssemblyName name)
        {
            var referenceDir = Path.Combine(GetDriverFolder(), "refs");
            var references = Directory.EnumerateFiles(referenceDir, "*.dll")
                .Concat(Directory.EnumerateFiles(GetDriverFolder(), "*.dll"));
            var compilation = CSharpCompilation.Create(
                name.Name,
                code.Select(
                    f => CSharpSyntaxTree.ParseText(f.Contents, new CSharpParseOptions(LanguageVersion.Latest), f.Path)),
                references.Select(path => MetadataReference.CreateFromFile(path)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using (var fileStream = new FileStream(name.CodeBase, FileMode.Create))
            {
                var result = compilation.Emit(fileStream);
                var importantDiagnostics = result.Diagnostics.Where(d => !IsIgnored(d)).ToList();
                if (importantDiagnostics.Any())
                {
                    throw new Exception(
                        "Cannot compile typed context:\n" + string.Join(
                            "\n",
                            importantDiagnostics.Select(d => d.ToString())));
                }
            }
        }

        private bool IsIgnored(Diagnostic diagnostic)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Hidden)
            {
                return true;
            }

            if (diagnostic.Severity == DiagnosticSeverity.Warning && diagnostic.Id == "CS1701")
            {
                return true;
            }

            return false;
        }

        private static async Task<ServiceClientModel> GetModelAsync(string uri, GeneratorOptions options)
        {
            SwaggerDocument document;
            using (var client = new HttpClient())
            {
                using (var docStream = await client.GetStreamAsync(uri))
                using (var reader = new StreamReader(docStream))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    document = SwaggerSerializer.Deserialize(jsonReader);
                }
            }

            var generator = new ServiceClientModelFactory(options);
            return generator.Create(document);
        }
    }
}
