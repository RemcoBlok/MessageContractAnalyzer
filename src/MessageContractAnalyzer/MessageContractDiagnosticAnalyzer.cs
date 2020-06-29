using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MessageContractAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MessageContractDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        public const string StructurallyCompatibleRuleId = "MCA0001";
        public const string ValidMessageContractStructureRuleId = "MCA0002";
        public const string MissingPropertiesRuleId = "MCA0003";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization

        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor StructurallyCompatibleRule = new DiagnosticDescriptor(StructurallyCompatibleRuleId,
            "Anonymous type does not map to message contract",
            "Anonymous type does not map to message contract '{0}'. The following properties of the anonymous type are incompatible: {1}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
            description: "Anonymous type should map to message contract");

        private static readonly DiagnosticDescriptor ValidMessageContractStructureRule = new DiagnosticDescriptor(ValidMessageContractStructureRuleId,
            "Message contract does not have a valid structure",
            "Message contract '{0}' does not have a valid structure",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
            description: "Message contract should have a valid structure. Properties should be primitive, string or IReadOnlyList or ImmatableArray of a primitive, string or message contract");

        private static readonly DiagnosticDescriptor MissingPropertiesRule = new DiagnosticDescriptor(MissingPropertiesRuleId,
            "Anonymous type is missing properties that are in the message contract",
            "Anonymous type is missing properties that are in the message contract '{0}'. The following properties are missing: {1}",
            Category, DiagnosticSeverity.Info, isEnabledByDefault: true,
            description: "Anonymous type misses properties that are in the message contract");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(StructurallyCompatibleRule, ValidMessageContractStructureRule, MissingPropertiesRule);

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.AnonymousObjectCreationExpression);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var anonymousObject = (AnonymousObjectCreationExpressionSyntax)context.Node;
            
            if (anonymousObject.HasArgumentAncestor(out var argumentSyntax) &&
                argumentSyntax.IsActivator(context.SemanticModel, out var typeArgument))
            {
                if (typeArgument.HasMessageContract(out var messageContractType))
                {
                    var anonymousType = context.SemanticModel.GetTypeInfo(anonymousObject).Type;

                    var incompatibleProperties = new List<string>();
                    if (!TypesAreStructurallyCompatible(anonymousType, messageContractType, string.Empty, incompatibleProperties))
                    {
                        var diagnostic = Diagnostic.Create(StructurallyCompatibleRule, anonymousType.Locations[0],
                            messageContractType.Name, string.Join(", ", incompatibleProperties));
                        context.ReportDiagnostic(diagnostic);
                    }

                    var missingProperties = new List<string>();
                    if (HasMissingProperties(anonymousType, messageContractType, string.Empty, missingProperties))
                    {
                        var diagnostic = Diagnostic.Create(MissingPropertiesRule, anonymousType.Locations[0],
                            messageContractType.Name, string.Join(", ", missingProperties));
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                else
                {
                    var diagnostic = Diagnostic.Create(ValidMessageContractStructureRule, context.Node.GetLocation(), typeArgument.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool TypesAreStructurallyCompatible(ITypeSymbol messageType, ITypeSymbol messageContractType,
            string path, ICollection<string> incompatibleProperties)
        {
            if (SymbolEqualityComparer.Default.Equals(messageType, messageContractType))
            {
                return true;
            }

            var messageContractProperties = messageContractType.GetMessageContractProperties();
            var messageProperties = messageType.GetMessageProperties();

            var result = true;
            foreach (var messageProperty in messageProperties)
            {
                var messageContractProperty =
                    messageContractProperties.FirstOrDefault(m => m.Name == messageProperty.Name);

                var propertyPath = Append(path, messageProperty.Name);

                if (messageContractProperty == null)
                {
                    incompatibleProperties.Add(propertyPath);
                    result = false;
                }
                else if (!PropertyTypesAreStructurallyCompatible(messageProperty, messageContractProperty, propertyPath, incompatibleProperties))
                {
                    result = false;
                }
            }

            return result;
        }

        private static bool PropertyTypesAreStructurallyCompatible(IPropertySymbol messageProperty, IPropertySymbol messageContractProperty,
            string path, ICollection<string> incompatibleProperties)
        {
            if (SymbolEqualityComparer.Default.Equals(messageProperty.Type, messageContractProperty.Type))
            {
                return true;
            }

            var result = AnonymousTypeAndInterfaceAreStructurallyCompatible(messageProperty, messageContractProperty, path, incompatibleProperties) ??
                EnumerableTypesAreStructurallyCompatible(messageProperty, messageContractProperty, path, incompatibleProperties);
            if (result.HasValue)
            {
                return result.Value;
            }

            incompatibleProperties.Add(path);
            return false;
        }

        private static bool? AnonymousTypeAndInterfaceAreStructurallyCompatible(IPropertySymbol messageProperty, IPropertySymbol messageContractProperty,
            string path, ICollection<string> incompatibleProperties)
        {
            if (messageProperty.Type.IsAnonymousType)
            {
                if (messageContractProperty.Type.TypeKind == TypeKind.Interface)
                {
                    if (!TypesAreStructurallyCompatible(messageProperty.Type, messageContractProperty.Type, path, incompatibleProperties))
                    {
                        return false;
                    }
                }
                else
                {
                    incompatibleProperties.Add(path);
                    return false;
                }

                return true;
            }

            return null;
        }

        private static bool? EnumerableTypesAreStructurallyCompatible(IPropertySymbol messageProperty, IPropertySymbol messageContractProperty,
            string path, ICollection<string> incompatibleProperties)
        {
            if (messageProperty.Type.IsValidMessageEnumerable(out var messagePropertyTypeArgument))
            {
                if (messageContractProperty.Type.IsValidMessageContractEnumerable(out var messageContractPropertyTypeArgument))
                {
                    if (!EnumerableTypesAreStructurallyCompatible(messagePropertyTypeArgument, messageContractPropertyTypeArgument, path, incompatibleProperties))
                    {
                        return false;
                    }
                }
                else
                {
                    incompatibleProperties.Add(path);
                    return false;
                }

                return true;
            }

            return null;
        }

        private static bool EnumerableTypesAreStructurallyCompatible(ITypeSymbol messagePropertyTypeArgument, ITypeSymbol messageContractPropertyTypeArgument,
            string path, ICollection<string> incompatibleProperties)
        {
            if (SymbolEqualityComparer.Default.Equals(messagePropertyTypeArgument, messageContractPropertyTypeArgument))
            {
                return true;
            }

            if (messagePropertyTypeArgument.IsAnonymousType &&
                messageContractPropertyTypeArgument.TypeKind == TypeKind.Interface)
            {
                if (!TypesAreStructurallyCompatible(messagePropertyTypeArgument, messageContractPropertyTypeArgument,
                    path, incompatibleProperties))
                {
                    return false;
                }
            }
            else
            {
                incompatibleProperties.Add(path);
                return false;
            }

            return true;
        }

        private static bool HasMissingProperties(ITypeSymbol messageType, ITypeSymbol messageContractType, string path, ICollection<string> missingProperties)
        {
            var messageContractProperties = messageContractType.GetMessageContractProperties();
            var messageProperties = messageType.GetMessageProperties();

            var result = false;
            foreach (var messageContractProperty in messageContractProperties)
            {
                var messageProperty = messageProperties.FirstOrDefault(m => m.Name == messageContractProperty.Name);

                var propertyPath = Append(path, messageContractProperty.Name);

                if (messageProperty == null)
                {
                    missingProperties.Add(propertyPath);
                    result = true;
                }
                else if (HasMissingProperties(messageProperty, messageContractProperty, propertyPath, missingProperties))
                {
                    result = true;
                }
            }

            return result;
        }

        private static bool HasMissingProperties(IPropertySymbol messageProperty, IPropertySymbol messageContractProperty, string path, ICollection<string> missingProperties)
        {
            var result = EnumerableTypeHasMissingProperties(messageProperty, messageContractProperty, path, missingProperties) ??
                AnonymousTypeHasMissingProperties(messageProperty, messageContractProperty, path, missingProperties);

            return result ?? false;
        }

        private static bool? EnumerableTypeHasMissingProperties(IPropertySymbol messageProperty, IPropertySymbol messageContractProperty,
            string path, ICollection<string> missingProperties)
        {
            if (messageContractProperty.Type.IsValidMessageContractEnumerable(out var messageContractPropertyTypeArgument))
            {
                if (messageProperty.Type.IsValidMessageEnumerable(out var messagePropertyTypeArgument) &&
                    messageContractPropertyTypeArgument.TypeKind == TypeKind.Interface &&
                    messagePropertyTypeArgument.IsAnonymousType &&
                    HasMissingProperties(messagePropertyTypeArgument, messageContractPropertyTypeArgument, path, missingProperties))
                {
                    return true;
                }

                return false;
            }

            return null;
        }

        private static bool? AnonymousTypeHasMissingProperties(IPropertySymbol messageProperty, IPropertySymbol messageContractProperty,
            string path, ICollection<string> missingProperties)
        {
            if (messageContractProperty.Type.TypeKind == TypeKind.Interface)
            {
                if (messageProperty.Type.IsAnonymousType &&
                    HasMissingProperties(messageProperty.Type, messageContractProperty.Type, path, missingProperties))
                {
                    return true;
                }

                return false;
            }

            return null;
        }

        private static string Append(string path, string propertyName)
        {
            if (string.IsNullOrEmpty(path))
            {
                return propertyName;
            }

            if (path.EndsWith(".", StringComparison.Ordinal))
            {
                return $"{path}{propertyName}";
            }

            return $"{path}.{propertyName}";
        }
    }
}