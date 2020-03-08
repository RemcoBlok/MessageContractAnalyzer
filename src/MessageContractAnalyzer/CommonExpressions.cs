using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace MessageContractAnalyzer
{
    public static class CommonExpressions
    {
        public static bool IsActivator(this ArgumentSyntax argumentSyntax, SemanticModel semanticModel, out ITypeSymbol typeArgument)
        {
            if (argumentSyntax != null &&
                argumentSyntax.Parent is ArgumentListSyntax argumentListSyntax &&
                argumentListSyntax.Parent is InvocationExpressionSyntax invocationExpressionSyntax &&
                invocationExpressionSyntax.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax &&
                semanticModel.GetSymbolInfo(memberAccessExpressionSyntax).Symbol is IMethodSymbol method &&
                method.TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, method.TypeArguments[0]) &&
                method.Parameters.Length == 1 &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                method.GetAttributes().Any(a => a.AttributeClass.Name == "ActivatorAttribute"))
            {
                typeArgument = method.TypeArguments[0];
                return true;
            }

            typeArgument = null;
            return false;
        }


        public static bool HasMessageContract(this ITypeSymbol typeArgument, out ITypeSymbol messageContractType)
        {
            if (IsImmutableArray(typeArgument, out messageContractType) ||
                IsReadOnlyList(typeArgument, out messageContractType) ||
                IsList(typeArgument, out messageContractType) ||
                IsArray(typeArgument, out messageContractType))
            {
                if (messageContractType.TypeKind == TypeKind.Interface)
                {
                    return true;
                }
            }

            if (typeArgument.TypeKind == TypeKind.Interface)
            {
                messageContractType = typeArgument;
                return true;
            }

            if (typeArgument.TypeKind == TypeKind.TypeParameter &&
                typeArgument is ITypeParameterSymbol typeParameter &&
                typeParameter.ConstraintTypes.Length == 1 &&
                typeParameter.ConstraintTypes[0].TypeKind == TypeKind.Interface)
            {
                messageContractType = typeParameter.ConstraintTypes[0];
                return true;
            }

            messageContractType = null;
            return false;
        }

        public static bool IsImmutableArray(this ITypeSymbol type, out ITypeSymbol typeArgument)
        {
            if (type.TypeKind == TypeKind.Struct &&
                type.Name == "ImmutableArray" &&
                type.ContainingNamespace.ToString() == "System.Collections.Immutable" &&
                type is INamedTypeSymbol immutableArrayType &&
                immutableArrayType.IsGenericType &&
                immutableArrayType.TypeArguments.Length == 1)
            {
                typeArgument = immutableArrayType.TypeArguments[0];
                return true;
            }

            typeArgument = null;
            return false;
        }

        public static bool IsReadOnlyList(this ITypeSymbol type, out ITypeSymbol typeArgument)
        {
            if (type.TypeKind == TypeKind.Interface &&
                type.Name == "IReadOnlyList" &&
                type.ContainingNamespace.ToString() == "System.Collections.Generic" &&
                type is INamedTypeSymbol readOnlyListType &&
                readOnlyListType.IsGenericType &&
                readOnlyListType.TypeArguments.Length == 1)
            {
                typeArgument = readOnlyListType.TypeArguments[0];
                return true;
            }

            typeArgument = null;
            return false;
        }

        public static bool IsList(this ITypeSymbol type, out ITypeSymbol typeArgument)
        {
            if (type.TypeKind == TypeKind.Class &&
                type.Name == "List" &&
                type.ContainingNamespace.ToString() == "System.Collections.Generic" &&
                type is INamedTypeSymbol listType &&
                listType.IsGenericType &&
                listType.TypeArguments.Length == 1)
            {
                typeArgument = listType.TypeArguments[0];
                return true;
            }

            typeArgument = null;
            return false;
        }

        public static bool IsArray(this ITypeSymbol type, out ITypeSymbol elementType)
        {
            if (type.TypeKind == TypeKind.Array &&
                type is IArrayTypeSymbol arrayTypeSymbol)
            {
                elementType = arrayTypeSymbol.ElementType;
                return true;
            }

            elementType = null;
            return false;
        }

        public static bool IsNullable(this ITypeSymbol type, out ITypeSymbol typeArgument)
        {
            if (type.TypeKind == TypeKind.Struct &&
                type.Name == "Nullable" &&
                type.ContainingNamespace.Name == "System" &&
                type is INamedTypeSymbol nullableType &&
                nullableType.IsGenericType &&
                nullableType.TypeArguments.Length == 1)
            {
                typeArgument = nullableType.TypeArguments[0];
                return true;
            }

            typeArgument = null;
            return false;
        }

        public static IReadOnlyList<IPropertySymbol> GetMessageProperties(this ITypeSymbol messageType)
        {
            return messageType.GetMembers().OfType<IPropertySymbol>().ToList();
        }

        public static IReadOnlyList<IPropertySymbol> GetMessageContractProperties(this ITypeSymbol messageContractType)
        {
            var messageContractTypes = new List<ITypeSymbol> { messageContractType };
            messageContractTypes.AddRange(messageContractType.AllInterfaces);

            return messageContractTypes
                .SelectMany(i => i.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(p => !p.GetAttributes()
                        .Any(a => a.AttributeClass.Name == "ActivatorInitializedAttribute")
                    )
                )
                .ToList();
        }
    }
}
