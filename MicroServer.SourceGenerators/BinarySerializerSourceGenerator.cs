using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MicroServer.SourceGenerators;

[Generator]
public sealed class BinarySerializerIncrementalGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "MicroServer.Model.GenerateBinarySerializerAttribute";
    
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol)
            .Where(static symbol => symbol is not null);
    
        var classModels = candidateClasses
            .Select(static (symbol, _) => CreateClassModel(symbol))
            .Where(static model => model is not null);
    
        context.RegisterSourceOutput(classModels, static (spc, model) =>
        {
            if (model is null)
                return;
            
            var source = GenerateSerializerClass(model);
            spc.AddSource(model.HintName, SourceText.From(source, Encoding.UTF8));
        });
    }

    // ---------- Модели ----------

    private sealed record PropertyModel
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public SpecialType SpecialType = SpecialType.None;
        public bool IsNullable { get; set; }
        public bool IsDateTime { get; set; }
        public bool IsNullableOfSupported { get; set; }
    }

    private sealed class ClassModel
    {
        public string Namespace { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ImmutableArray<PropertyModel> Properties { get; set; } = [];
        public string HintName { get; set; } = string.Empty;
    }

    private static ClassModel? CreateClassModel(INamedTypeSymbol classSymbol)
    {
        var ns = classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        var name = classSymbol.Name;

        var props = classSymbol
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                        p is { IsStatic: false, GetMethod: not null })
            .Select(CreatePropertyModel)
            .Where(static p => p is not null)
            .Cast<PropertyModel>()
            .ToImmutableArray();

        if (props.IsDefaultOrEmpty)
            return null;

        var hintName = $"{(string.IsNullOrEmpty(ns) ? "" : ns.Replace('.', '_') + "_")}{name}_BinarySerializer.g.cs";

        return new ClassModel
            {
                Namespace = ns,
                Name = name,
                Properties = props,
                HintName = hintName
            };
    }

    private static PropertyModel? CreatePropertyModel(IPropertySymbol prop)
    {
        var type = prop.Type;

        // Простые типы
        var special = type.SpecialType;
        var typeName = type.ToDisplayString();

        // DateTime
        var isDateTime = typeName == "System.DateTime";

        var isNullableOfSupported = false;
        var isNullable = false;

        if (type is INamedTypeSymbol named &&
            named.ConstructedFrom.ToDisplayString() == "System.Nullable<T>")
        {
            isNullable = true;
            var underlying = named.TypeArguments[0];
            var underlyingSpecial = underlying.SpecialType;
            var underlyingName = underlying.ToDisplayString();

            if (IsSupportedSpecialType(underlyingSpecial) || underlyingName == "System.DateTime")
            {
                isNullableOfSupported = true;
            }
            else
            {
                return null;
            }
        }
        else if (!IsSupportedSpecialType(special) && !isDateTime)
        {
            // Неподдерживаемый тип — игнор
            return null;
        }

        return new PropertyModel
        {
            Name = prop.Name,
            TypeName = typeName,
            SpecialType = special,
            IsNullable = isNullable,
            IsDateTime = isDateTime,
            IsNullableOfSupported = isNullableOfSupported
        };
    }

    private static bool IsSupportedSpecialType(SpecialType specialType) =>
        specialType is SpecialType.System_Int32
            or SpecialType.System_Int64
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_UInt32
            or SpecialType.System_UInt64
            or SpecialType.System_Byte
            or SpecialType.System_Boolean
            or SpecialType.System_Double
            or SpecialType.System_Single
            or SpecialType.System_Char
            or SpecialType.System_String;

    // ---------- Генерация кода ----------

    private static string GenerateSerializerClass(ClassModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.Append("namespace ").Append(model.Namespace).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("public partial class ").Append(model.Name).AppendLine();
        sb.AppendLine("{");
        
        // Serialization
        sb.AppendLine("    public void SerializeToBinary(Stream stream)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);");

        foreach (var prop in model.Properties)
        {
            var line = GeneratePropertyWrite(prop);
            if (!string.IsNullOrEmpty(line))
                sb.Append("        ").AppendLine(line);
        }

        sb.AppendLine("    }");
        
        // Deserialization
        sb.AppendLine("    public static UserProfile DeserializeFromBinary(Stream stream)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);\n" +
            "        var profile = new UserProfile\n" +
            "        {");

        foreach (var prop in model.Properties)
        {
            var line = GeneratePropertyRead(prop);
            if (!string.IsNullOrEmpty(line))
                sb.Append("            ").AppendLine(line + ",");
        }

        sb.AppendLine("        };\n" +
                      "        return profile;");
        sb.AppendLine("    }");
        sb.AppendLine("}"); // class closing bracket

        return sb.ToString();
    }

    private static string GeneratePropertyRead(PropertyModel prop)
    {
        var name = prop.Name;

        if (prop is { IsNullable: true, IsNullableOfSupported: true })
        {
            var sb = new StringBuilder();
            sb.Append(name).Append(" = reader.ReadBoolean() ? ").Append(SelectReadOperation(prop))
                .Append(": null");

            return sb.ToString();
        }

        if (prop.IsDateTime)
            return $"{name} = DateTime.FromBinary(reader.ReadInt64())";


        var readMethod = SelectReadOperation(prop);
        return string.IsNullOrEmpty(readMethod) 
            ? string.Empty 
            : name + " = " + readMethod;
    }
    
    private static string SelectReadOperation(PropertyModel propertyModel)
    {
        var command = propertyModel.SpecialType switch
        {
            SpecialType.System_Int32 => "reader.ReadInt32()",
            SpecialType.System_Int64 => "reader.ReadInt64()",
            SpecialType.System_Int16 => "reader.ReadInt16()",
            SpecialType.System_UInt16 => "reader.ReadUInt16()",
            SpecialType.System_UInt32 => "reader.ReadUInt32()",
            SpecialType.System_UInt64 => "reader.ReadUInt64()",
            SpecialType.System_Byte => "reader.ReadByte()",
            SpecialType.System_Boolean => "reader.ReadBoolean();",
            SpecialType.System_Double => "reader.ReadDouble()",
            SpecialType.System_Single => "reader.ReadSingle()",
            SpecialType.System_Char => "reader.ReadChar()",
            SpecialType.System_String => "reader.ReadString()",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(command))
        {
            command = propertyModel.TypeName.Trim('?') switch
            {
                "int" => "reader.ReadInt32()",
                "double" => "reader.ReadDouble()",
                // ...
                "string" => "reader.ReadString()",
                "System.DateTime" => "DateTime.FromBinary(reader.ReadInt64())",
                _ => string.Empty
            };
        }

        return command;
    }
    
    private static string GeneratePropertyWrite(PropertyModel prop)
    {
        var name = prop.Name;

        if (prop is { IsNullable: true, IsNullableOfSupported: true })
        {
            var sb = new StringBuilder();
            sb.Append("writer.Write(this.").Append(name).Append(".HasValue); ");

            if (prop.TypeName.StartsWith("System.Nullable<System.DateTime>", StringComparison.Ordinal))
            {
                sb.Append("if (this.").Append(name).Append(".HasValue) writer.Write(this.")
                    .Append(name).Append(".Value.ToBinary());");
                return sb.ToString();
            }

            sb.Append("if (this.").Append(name).Append(".HasValue) writer.Write(this.")
                .Append(name).Append(".Value);");
            return sb.ToString();
        }

        if (prop.IsDateTime)
        {
            return $"writer.Write(this.{name}.ToBinary());";
        }

        return prop.SpecialType switch
        {
            SpecialType.System_Int32 => $"writer.Write(this.{name});",
            SpecialType.System_Int64 => $"writer.Write(this.{name});",
            SpecialType.System_Int16 => $"writer.Write(this.{name});",
            SpecialType.System_UInt16 => $"writer.Write(this.{name});",
            SpecialType.System_UInt32 => $"writer.Write(this.{name});",
            SpecialType.System_UInt64 => $"writer.Write(this.{name});",
            SpecialType.System_Byte => $"writer.Write(this.{name});",
            SpecialType.System_Boolean => $"writer.Write(this.{name});",
            SpecialType.System_Double => $"writer.Write(this.{name});",
            SpecialType.System_Single => $"writer.Write(this.{name});",
            SpecialType.System_Char => $"writer.Write(this.{name});",
            SpecialType.System_String => $"writer.Write(this.{name} ?? string.Empty);",
            _ => string.Empty
        };
    }
}