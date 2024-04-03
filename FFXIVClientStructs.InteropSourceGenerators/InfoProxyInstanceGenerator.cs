using System.Collections.Immutable;
using FFXIVClientStructs.InteropGenerator;
using FFXIVClientStructs.InteropSourceGenerators.Extensions;
using FFXIVClientStructs.InteropSourceGenerators.Models;
using LanguageExt;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FFXIVClientStructs.InteropSourceGenerators;

[Generator]
public class InfoProxyInstanceGenerator : IIncrementalGenerator {
    private const string InfoProxyAttributeName = "FFXIVClientStructs.Attributes.InfoProxyAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        IncrementalValuesProvider<(Validation<DiagnosticInfo, StructInfo> StructInfo,
            Validation<DiagnosticInfo, InfoProxyInfo> InfoProxyInfo)> structAndInfoProxyInfos =
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    InfoProxyAttributeName,
                    static (node, _) => node is StructDeclarationSyntax {
                        AttributeLists.Count: > 0
                    },
                    static (context, _) => {
                        StructDeclarationSyntax structSyntax = (StructDeclarationSyntax)context.TargetNode;
                        INamedTypeSymbol symbol = (INamedTypeSymbol)context.TargetSymbol;
                        return (Struct: StructInfo.GetFromSyntax(structSyntax),
                            Info: InfoProxyInfo.GetFromRoslyn(structSyntax, symbol));
                    });

        // make sure caching is working
        IncrementalValuesProvider<Validation<DiagnosticInfo, StructWithInfoProxyInfos>> structsWithInfoProxyInfos =
            structAndInfoProxyInfos.Select(static (item, _) =>
                (item.StructInfo, item.InfoProxyInfo).Apply(static (si, ai) =>
                    new StructWithInfoProxyInfos(si, ai))
            );

        context.RegisterSourceOutput(structsWithInfoProxyInfos, (sourceContext, item) => {
            item.Match(
                Fail: diagnosticInfos => {
                    diagnosticInfos.Iter(dInfo => sourceContext.ReportDiagnostic(dInfo.ToDiagnostic()));
                },
                Succ: structWithInfoProxyInfos => {
                    sourceContext.AddSource(structWithInfoProxyInfos.GetFileName(), structWithInfoProxyInfos.RenderSource());
                });
        });

        IncrementalValueProvider<ImmutableArray<Validation<DiagnosticInfo, StructWithInfoProxyInfos>>>
            collectedStructs = structsWithInfoProxyInfos.Collect();

        context.RegisterSourceOutput(collectedStructs,
            (sourceContext, structs) => {
                sourceContext.AddSource("InfoModule.InfoProxyGetter.g.cs", BuildInfoProxyGettersSource(structs));
            });
    }
    private static string BuildInfoProxyGettersSource(
        ImmutableArray<Validation<DiagnosticInfo, StructWithInfoProxyInfos>> structInfos) {
        IndentedStringBuilder builder = new();

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine();

        builder.AppendLine("namespace FFXIVClientStructs.FFXIV.Client.UI.Info;");
        builder.AppendLine();

        builder.AppendLine("public unsafe partial struct InfoModule");
        builder.AppendLine("{");
        builder.Indent();

        structInfos.Iter(siv =>
            siv.IfSuccess(structWithInfoProxyInfos => structWithInfoProxyInfos.RenderInfoProxyGetter(builder)));

        builder.DecrementIndent();
        builder.AppendLine("}");

        return builder.ToString();
    }

    internal sealed record InfoProxyInfo(StructInfo StructInfo, uint InfoProxyId) {
        public static Validation<DiagnosticInfo, InfoProxyInfo> GetFromRoslyn(
            StructDeclarationSyntax structSyntax, INamedTypeSymbol namedTypeSymbol) {
            Validation<DiagnosticInfo, StructInfo> validStructInfo =
                StructInfo.GetFromSyntax(structSyntax);

            Option<AttributeData> infoProxyAttribute = namedTypeSymbol.GetFirstAttributeDataByTypeName(InfoProxyAttributeName);

            Validation<DiagnosticInfo, uint> validInfoProxyId =
                infoProxyAttribute.GetValidAttributeArgument<uint>("ID", 0, InfoProxyAttributeName, namedTypeSymbol);

            return (validStructInfo, validInfoProxyId: validInfoProxyId).Apply((structInfo, infoProxyId) =>
                new InfoProxyInfo(structInfo, infoProxyId));
        }
    }

    private sealed record StructWithInfoProxyInfos
        (StructInfo StructInfo, InfoProxyInfo InfoProxyInfo) {
        public string RenderSource() {
            IndentedStringBuilder builder = new();

            StructInfo.RenderStart(builder);

            builder.AppendLine();
            builder.AppendLine($"public static {StructInfo.Name}* Instance() => ({StructInfo.Name}*)InfoModule.Instance()->GetInfoProxyById((uint){InfoProxyInfo.InfoProxyId});");
            builder.AppendLine();

            StructInfo.RenderEnd(builder);

            return builder.ToString();
        }

        public string GetFileName() {
            return $"{StructInfo.Name}.InstanceGetter.g.cs";
        }

        public void RenderInfoProxyGetter(IndentedStringBuilder builder) {
            builder.AppendLine($"public {StructInfo.Name}* Get{StructInfo.Name}() => ({StructInfo.Name}*)GetInfoProxyById((uint){InfoProxyInfo.InfoProxyId});");
        }
    }
}
