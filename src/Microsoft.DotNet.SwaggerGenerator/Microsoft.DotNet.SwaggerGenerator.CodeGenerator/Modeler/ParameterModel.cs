namespace Microsoft.DotNet.SwaggerGenerator.Modeler
{
    public class ParameterModel
    {
        public ParameterModel(string name, bool required, ParameterLocation location, TypeReference type)
        {
            Name = name;
            Required = required;
            Location = location;
            Type = type;
            Verifyable = type is TypeReference.TypeModelReference typeModelRef &&
                         typeModelRef.Model is ClassTypeModel classModel &&
                         classModel.Verifyable;
        }

        public string Name { get; }
        public bool Required { get; }
        public ParameterLocation Location { get; }
        public TypeReference Type { get; }
        public bool Verifyable { get; }
        public bool IsConstant => Type is TypeReference.ConstantTypeReference;

        public override string ToString()
        {
            return $"{Type} {Name}; Required={Required}, In={Location}";
        }
    }
}
