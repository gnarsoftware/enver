namespace Enver.SourceGeneration.Generator.Model;

internal enum TypeDispatchKind
{
    String,
    Boolean,
    Number,
    Guid,
    Uri,
    Version,
    Enum,
    Utf8SpanParsable,
    SpanParsable,
    Parsable,
    Unsupported,
}
