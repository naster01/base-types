﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace AD.BaseTypes.Generator
{
    [Generator]
    public class BaseTypeGenerator : ISourceGenerator
    {
        static readonly Regex
            BaseTypeRegex = new Regex("^AD.BaseTypes.IBaseTypeDefinition<(?<type>.+)>$"),
            ValidatedBaseTypeRegex = new Regex("^AD.BaseTypes.IBaseTypeValidation<(?<type>.+)>$");

        public void Initialize(GeneratorInitializationContext context)
        {
            //AttachDebugger();
            context.RegisterForSyntaxNotifications(() => new PartialRecordsWithAttributesReceiver());
        }

        class PartialRecordsWithAttributesReceiver : ISyntaxReceiver
        {
            public List<RecordDeclarationSyntax> Records { get; } = new List<RecordDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is RecordDeclarationSyntax record &&
                    record.Modifiers.Any(SyntaxKind.PartialKeyword) &&
                    GetAllAttributes(record).Any())
                {
                    Records.Add(record);
                }
            }
        }

        public void Execute(GeneratorExecutionContext context)
        {
            foreach (var record in ((PartialRecordsWithAttributesReceiver)context.SyntaxReceiver).Records)
            {
                var semantics = context.Compilation.GetSemanticModel(record.SyntaxTree);

                var attributes = GetAllAttributes(record);
                if (!TryGetBaseType(semantics, attributes, out var baseType)) continue;

                var sourceBuilder = new IndentedStringBuilder();

                var @namespace = GetNamespace(record);
                var hasNamespace = !string.IsNullOrEmpty(@namespace);
                if (hasNamespace)
                {
                    //namespace start
                    sourceBuilder.AppendLine($"namespace {@namespace}");
                    sourceBuilder.AppendLine("{");
                    sourceBuilder.IncreaseIndent();
                    //*****
                }

                //record start
                var recordName = record.Identifier.Text;
                sourceBuilder.AppendLine($"[System.Text.Json.Serialization.JsonConverter(typeof(AD.BaseTypes.Json.BaseTypeJsonConverter<{recordName}, {baseType}>))]");
                sourceBuilder.AppendLine($"sealed partial record {recordName} : System.IComparable<{recordName}>, System.IComparable, AD.BaseTypes.IBaseType<{baseType}>");
                sourceBuilder.AppendLine("{");
                sourceBuilder.IncreaseIndent();
                //*****

                //constructor start
                sourceBuilder.AppendLine($"public {recordName}({baseType} value)");
                sourceBuilder.AppendLine("{");
                sourceBuilder.IncreaseIndent();
                //*****

                AppendValidations(sourceBuilder, semantics, attributes, baseType);
                sourceBuilder.AppendLine("this.Value = value;");

                //constructor end
                sourceBuilder.DecreaseIndent();
                sourceBuilder.AppendLine("}");
                //*****

                sourceBuilder.AppendLine($"public {baseType} Value {{ get; }}");
                sourceBuilder.AppendLine("public override string ToString() => Value.ToString();");
                sourceBuilder.AppendLine($"public int CompareTo(object obj) => CompareTo(obj as {recordName});");
                sourceBuilder.AppendLine($"public int CompareTo({record.Identifier.Text} other) => other is null ? 1 : System.Collections.Generic.Comparer<{baseType}>.Default.Compare(Value, other.Value);");
                sourceBuilder.AppendLine($"public static implicit operator {baseType}({recordName} item) => item.Value;");
                sourceBuilder.AppendLine($"public static {recordName} Create({baseType} value) => new(value);");

                //record end
                sourceBuilder.DecreaseIndent();
                sourceBuilder.AppendLine("}");
                //*****

                if (hasNamespace)
                {
                    //namespace end
                    sourceBuilder.DecreaseIndent();
                    sourceBuilder.AppendLine("}");
                    //*****
                }

                var fileHint = hasNamespace ? $"{@namespace}.{recordName}" : recordName;
                context.AddSource($"{fileHint}.g", sourceBuilder.ToString());
            }
        }

        static IEnumerable<AttributeSyntax> GetAllAttributes(RecordDeclarationSyntax record) =>
            record.AttributeLists.SelectMany(_ => _.Attributes);

        static bool TryGetBaseType(SemanticModel semantics, IEnumerable<AttributeSyntax> attributes, out string baseType)
        {
            var baseTypes = GetBaseTypes(semantics, attributes);
            if (baseTypes.Length != 1)
            {
                baseType = default;
                return false;
            }
            baseType = baseTypes[0];
            return true;
        }

        static string[] GetBaseTypes(SemanticModel semantics, IEnumerable<AttributeSyntax> attributes) =>
            attributes.SelectMany(attribute =>
                semantics.GetSymbolInfo(attribute).Symbol.ContainingType.AllInterfaces.Select(@interface =>
                {
                    var match = BaseTypeRegex.Match(@interface.ToDisplayString());
                    return match.Success ? match.Groups["type"].Value : null;
                }))
            .Where(_ => _ != null).Distinct().ToArray();

        static string GetNamespace(RecordDeclarationSyntax record)
        {
            var namespaces = FindParentNamespaces(record).Select(_ => _.Name).Reverse();
            return string.Join(".", namespaces);
        }

        static IEnumerable<NamespaceDeclarationSyntax> FindParentNamespaces(SyntaxNode node)
        {
            for (; node != null; node = node.Parent)
            {
                if (node is NamespaceDeclarationSyntax @namespace) yield return @namespace;
            }
        }

        static void AppendValidations(IndentedStringBuilder sourceBuilder, SemanticModel semantics, IEnumerable<AttributeSyntax> attributes, string baseType)
        {
            foreach (var validation in GetAllValidations(semantics, attributes, baseType))
            {
                var validationType = semantics.GetSymbolInfo(validation).Symbol.ContainingType;
                var args = validation?.ArgumentList?.Arguments.ToString() ?? "";
                sourceBuilder.AppendLine($"new {validationType.ToDisplayString()}({args}).Validate(value);");
            }
        }

        static IEnumerable<AttributeSyntax> GetAllValidations(SemanticModel semantics, IEnumerable<AttributeSyntax> attributes, string baseType) =>
            attributes.Where(a =>
                semantics.GetSymbolInfo(a).Symbol.ContainingType.AllInterfaces.Any(i =>
                {
                    var match = ValidatedBaseTypeRegex.Match(i.ToDisplayString());
                    return match.Success && match.Groups["type"].Value == baseType;
                }));

#pragma warning disable IDE0051 // Remove unused private members
        [Conditional("DEBUG")]
        static void AttachDebugger()
        {
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
        }
#pragma warning restore IDE0051 // Remove unused private members
    }
}
