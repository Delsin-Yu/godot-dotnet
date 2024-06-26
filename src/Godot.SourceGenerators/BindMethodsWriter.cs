using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Godot.SourceGenerators;

internal static class BindMethodsWriter
{
    public static void Write(IndentedStringBuilder sb, GodotClassSpec spec)
    {
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(spec.FullyQualifiedNamespace))
        {
            sb.AppendLine($"namespace {spec.FullyQualifiedNamespace};");
            sb.AppendLine();
        }

        for (int i = spec.ContainingTypeSymbols.Count - 1; i >= 0; i--)
        {
            var containingSymbol = spec.ContainingTypeSymbols[i];
            sb.Append("partial ");
            sb.Append(containingSymbol.SymbolKind switch
            {
                ContainingSymbol.Kind.Interface => "interface ",
                ContainingSymbol.Kind.Class => "class ",
                ContainingSymbol.Kind.Struct => "struct ",
                ContainingSymbol.Kind.RecordClass => "record class ",
                ContainingSymbol.Kind.RecordStruct => "record struct ",
                var kind => throw new InvalidOperationException($"Invalid symbol kind '{kind}'."),
            });
            sb.AppendLine(containingSymbol.SymbolName);
            sb.OpenBlock();
        }

        sb.AppendLine($"partial class {spec.SymbolName}");
        sb.OpenBlock();

        WriteCachedStringNames(sb, spec);

        sb.AppendLine("internal static void BindMethods(global::Godot.Bridge.ClassDBRegistrationContext context)");
        sb.OpenBlock();

        WriteBindConstructor(sb, spec);

        WriteBindMethods(sb, spec);

        WriteBindProperties(sb, spec);

        WriteBindSignals(sb, spec);

        sb.CloseBlock();

        sb.CloseBlock();

        for (int i = 0; i < spec.ContainingTypeSymbols.Count; i++)
        {
            sb.CloseBlock();
        }
    }

    private static void WriteCachedStringNames(IndentedStringBuilder sb, GodotClassSpec spec)
    {
        string baseTypeFullName = spec.FullyQualifiedBaseTypeName;

        if (spec.Methods.Count > 0)
        {
            sb.AppendLine($"public new class MethodName : {baseTypeFullName}.MethodName");
            sb.OpenBlock();
            foreach (var method in spec.Methods)
            {
                string value = !string.IsNullOrEmpty(method.NameOverride)
                    ? method.NameOverride!
                    : method.SymbolName;
                AddCachedStringName(method.SymbolName, value);
            }
            sb.CloseBlock();
        }

        if (spec.Properties.Count > 0)
        {
            sb.AppendLine($"public new class PropertyName : {baseTypeFullName}.PropertyName");
            sb.OpenBlock();
            foreach (var property in spec.Properties)
            {
                string value = !string.IsNullOrEmpty(property.NameOverride)
                    ? property.NameOverride!
                    : property.SymbolName;
                AddCachedStringName(property.SymbolName, value);
            }
            sb.CloseBlock();
        }

        if (spec.Signals.Count > 0)
        {
            sb.AppendLine($"public new class SignalName : {baseTypeFullName}.SignalName");
            sb.OpenBlock();
            foreach (var signal in spec.Signals)
            {
                string value = !string.IsNullOrEmpty(signal.NameOverride)
                    ? signal.NameOverride!
                    : signal.SymbolName;
                AddCachedStringName(signal.SymbolName, value);
            }
            sb.CloseBlock();
        }

        void AddCachedStringName(string symbolName, string value)
        {
            sb.AppendLine($$"""public static global::Godot.StringName @{{symbolName}} { get; } = global::Godot.StringName.CreateStaticFromAscii("{{value}}"u8);""");
        }
    }

    private static void WriteBindConstructor(IndentedStringBuilder sb, GodotClassSpec spec)
    {
        if (spec.Constructor is null)
        {
            // Class is not instantiable, don't bind a constructor.
            return;
        }

        if (string.IsNullOrEmpty(spec.Constructor.Value.MethodSymbolName))
        {
            // The spec doesn't specify a method name, just use the class constructor.
            sb.AppendLine($"context.BindConstructor(() => new {spec.SymbolName}());");
        }
        else
        {
            sb.AppendLine($"context.BindConstructor(() => {spec.SymbolName}.@{spec.Constructor.Value.MethodSymbolName}());");
        }
    }

    private static void WriteBindMethods(IndentedStringBuilder sb, GodotClassSpec spec)
    {
        foreach (var method in spec.Methods)
        {
            if (method.IsStatic)
            {
                Debug.Assert(!method.IsVirtual, $"Static methods can't be virtual. Method: '{method.SymbolName}'.");
            }

            sb.Append($"context.{method switch
            {
                _ when method.IsStatic => "BindStaticMethod",
                _ when method.IsVirtual => "BindVirtualMethod",
                _ => "BindMethod"
            }}");

            if (method.IsVirtual && (method.Parameters.Count != 0 || method.ReturnParameter is not null))
            {
                sb.Append('<');
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    var parameter = method.Parameters[i];
                    sb.Append(parameter.MarshalInfo.FullyQualifiedTypeName);
                    if (method.ReturnParameter is not null || i < method.Parameters.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }
                if (method.ReturnParameter is not null)
                {
                    sb.Append(method.ReturnParameter.Value.MarshalInfo.FullyQualifiedTypeName);
                }
                sb.Append('>');
            }

            sb.Append($"(MethodName.@{method.SymbolName}");

            if (method.IsVirtual && method.Parameters.Count == 0 && method.ReturnParameter is null)
            {
                sb.AppendLine(");");
                continue;
            }
            else
            {
                sb.AppendLine(',');
                sb.Indent++;
            }

            for (int i = 0; i < method.Parameters.Count; i++)
            {
                var parameter = method.Parameters[i];
                sb.AppendParameterInfo(parameter);
                if (method.ReturnParameter is not null || i < method.Parameters.Count - 1)
                {
                    sb.AppendLine(',');
                }
            }
            if (method.ReturnParameter is not null)
            {
                sb.AppendReturnInfo(method.ReturnParameter.Value);
            }

            if (method.IsVirtual)
            {
                sb.AppendLine(");");
                sb.Indent--;
                continue;
            }

            if (method.Parameters.Count != 0 || method.ReturnParameter is not null)
            {
                sb.AppendLine(',');
            }

            // Generate the function.

            sb.Append("static (");
            if (!method.IsStatic)
            {
                sb.Append($"{spec.SymbolName} __instance");
            }
            if (method.Parameters.Count != 0)
            {
                if (!method.IsStatic)
                {
                    sb.Append(", ");
                }
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    GodotPropertySpec parameter = method.Parameters[i];
                    sb.Append($"{parameter.MarshalInfo.FullyQualifiedTypeName} @{parameter.SymbolName}");
                    if (i < method.Parameters.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }
            }

            sb.AppendLine(") =>");

            sb.AppendLine("{");
            sb.Indent++;

            if (method.ReturnParameter is not null)
            {
                sb.Append("return ");
            }
            sb.Append(method.IsStatic ? spec.SymbolName : "__instance");
            sb.Append($".@{method.SymbolName}(");
            if (method.Parameters.Count != 0)
            {
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    GodotPropertySpec parameter = method.Parameters[i];
                    sb.Append($"@{parameter.SymbolName}");
                    if (i < method.Parameters.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }
            }
            sb.Append(")");

            sb.AppendLine(';');

            sb.Indent--;
            sb.AppendLine("});");

            sb.Indent--;
        }
    }

    private static void WriteBindProperties(IndentedStringBuilder sb, GodotClassSpec spec)
    {
        foreach (var property in spec.Properties)
        {
            sb.Append("context.BindProperty(");
            sb.Indent++;

            sb.AppendPropertyInfo(property);
            sb.AppendLine(',');

            // Generate getter.
            sb.AppendLine($"static ({spec.SymbolName} __instance) =>");
            sb.AppendLine('{');
            sb.Indent++;
            sb.AppendLine($"return __instance.@{property.SymbolName};");
            sb.Indent--;
            sb.Append('}');
            sb.AppendLine(',');

            // Generate setter.
            sb.AppendLine($"static ({spec.SymbolName} __instance, {property.MarshalInfo.FullyQualifiedTypeName} value) =>");
            sb.AppendLine('{');
            sb.Indent++;
            sb.AppendLine($"__instance.@{property.SymbolName} = value;");
            sb.Indent--;
            sb.Append('}');

            sb.AppendLine(");");
            sb.Indent--;
        }
    }

    private static void WriteBindSignals(IndentedStringBuilder sb, GodotClassSpec spec)
    {
        foreach (var signal in spec.Signals)
        {
            sb.Append("context.BindSignal(new global::Godot.Bridge.SignalInfo(");
            sb.Append($"SignalName.@{signal.SymbolName})");
            if (signal.Parameters.Count == 0)
            {
                sb.AppendLine(");");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine('{');
                sb.Indent++;

                sb.AppendLine("Parameters =");
                sb.AppendLine('{');
                sb.Indent++;
                foreach (var parameter in signal.Parameters)
                {
                    sb.AppendParameterInfo(parameter);
                    sb.AppendLine(',');
                }
                sb.Indent--;
                sb.AppendLine("},");

                sb.Indent--;
                sb.AppendLine("});");
            }
        }
    }

    private static void AppendReturnInfo(this IndentedStringBuilder sb, GodotPropertySpec returnParameter)
    {
        sb.Append("new global::Godot.Bridge.ReturnInfo(");
        sb.Append(returnParameter.MarshalInfo.VariantType.FullNameWithGlobal());
        if (returnParameter.MarshalInfo.VariantTypeMetadata != VariantTypeMetadata.None)
        {
            sb.Append(", ");
            sb.Append(returnParameter.MarshalInfo.VariantTypeMetadata.FullNameWithGlobal());
        }
        sb.Append(')');
        AppendPropertyInfoObjectInitializer(sb, returnParameter);
    }

    private static void AppendParameterInfo(this IndentedStringBuilder sb, GodotPropertySpec parameter)
    {
        string nameValue = !string.IsNullOrEmpty(value: parameter.NameOverride)
            ? parameter.NameOverride!
            : parameter.SymbolName;

        sb.Append("new global::Godot.Bridge.ParameterInfo(");
        sb.Append($"""global::Godot.StringName.CreateStaticFromAscii("{nameValue}"u8), """);
        sb.Append(parameter.MarshalInfo.VariantType.FullNameWithGlobal());
        if (parameter.MarshalInfo.VariantTypeMetadata != VariantTypeMetadata.None || parameter.HasExplicitDefaultValue)
        {
            sb.Append(", ");
            sb.Append(parameter.MarshalInfo.VariantTypeMetadata.FullNameWithGlobal());
        }
        if (parameter.HasExplicitDefaultValue)
        {
            sb.Append($", {parameter.ExplicitDefaultValue}");
        }
        sb.Append(')');
        AppendPropertyInfoObjectInitializer(sb, parameter);
    }

    private static void AppendPropertyInfo(this IndentedStringBuilder sb, GodotPropertySpec property)
    {
        sb.Append("new global::Godot.Bridge.PropertyInfo(");
        sb.Append($"PropertyName.@{property.SymbolName}, ");
        sb.Append(property.MarshalInfo.VariantType.FullNameWithGlobal());
        if (property.MarshalInfo.VariantTypeMetadata != VariantTypeMetadata.None)
        {
            sb.Append(", ");
            sb.Append(property.MarshalInfo.VariantTypeMetadata.FullNameWithGlobal());
        }
        sb.Append(')');
        AppendPropertyInfoObjectInitializer(sb, property);
    }

    private static void AppendPropertyInfoObjectInitializer(IndentedStringBuilder sb, GodotPropertySpec property)
    {
        var marshalInfo = property.MarshalInfo;

        List<string> lines = [];

        if (marshalInfo.Hint != PropertyHint.None)
        {
            lines.Add($"Hint = {marshalInfo.Hint.FullNameWithGlobal()},");
        }
        if (!string.IsNullOrEmpty(marshalInfo.HintString))
        {
            lines.Add($"""HintString = "{marshalInfo.HintString}",""");
        }
        if (marshalInfo.Usage != PropertyUsageFlags.None)
        {
            lines.Add($"Usage = {marshalInfo.Usage.FullNameWithGlobal()},");
        }
        if (!string.IsNullOrEmpty(marshalInfo.ClassName))
        {
            lines.Add($"""ClassName = global::Godot.StringName.CreateStaticFromAscii("{marshalInfo.ClassName}"u8),""");
        }

        if (lines.Count == 0)
        {
            // All the properties have the default value, no need to append anything.
            return;
        }

        sb.AppendLine();
        sb.AppendLine('{');
        sb.Indent++;

        foreach (string line in lines)
        {
            sb.AppendLine(line);
        }

        sb.Indent--;
        sb.Append('}');
    }
}
