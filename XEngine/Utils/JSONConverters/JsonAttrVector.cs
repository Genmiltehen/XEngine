using System.Text.Json.Serialization;

namespace XEngine.Core.Utils.JSONConverters;

public class JsonVector3 : JsonConverterAttribute
{
    public override JsonConverter? CreateConverter(Type typeToConvert) => new OTKVector3Converter();
}

public class JsonVector2 : JsonConverterAttribute
{
    public override JsonConverter? CreateConverter(Type typeToConvert) => new OTKVector2Converter();
}
