using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace MessageContractAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MessageContractCodeFixProvider)), Shared]
    public class MessageContractCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add missing properties";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(MessageContractDiagnosticAnalyzer.MissingPropertiesRuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var anonymousObject = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<AnonymousObjectCreationExpressionSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: cancellationToken => AddMissingProperties(context.Document, anonymousObject, cancellationToken),
                    equivalenceKey: Title),
                diagnostic);
        }

        private static async Task<Document> AddMissingProperties(Document document,
            AnonymousObjectCreationExpressionSyntax anonymousObject, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (anonymousObject.HasArgumentAncestor(out var argumentSyntax) &&
                argumentSyntax.IsActivator(semanticModel, out var typeArgument) &&
                typeArgument.HasMessageContract(out var messageContractType))
            {
                var dictionary = new Dictionary<AnonymousObjectCreationExpressionSyntax, ITypeSymbol>();

                FindAnonymousTypesWithMessageContractsInTree(anonymousObject, messageContractType, dictionary);

                var newRoot = AddMissingProperties(root, dictionary);

                var formattedRoot = Formatter.Format(newRoot, Formatter.Annotation, document.Project.Solution.Workspace, document.Project.Solution.Workspace.Options);

                return document.WithSyntaxRoot(formattedRoot);
            }

            return document;
        }

        private static void FindAnonymousTypesWithMessageContractsInTree(AnonymousObjectCreationExpressionSyntax anonymousObject,
            ITypeSymbol messageContractType, IDictionary<AnonymousObjectCreationExpressionSyntax, ITypeSymbol> dictionary)
        {
            var messageContractProperties = messageContractType.GetMessageContractProperties();

            foreach (var initializer in anonymousObject.Initializers)
            {
                var name = GetName(initializer);

                var messageContractProperty = messageContractProperties.FirstOrDefault(p => p.Name == name);

                if (messageContractProperty != null)
                {
                    FindAnonymousTypesWithMessageContractsInTree(initializer, messageContractProperty, dictionary);
                }
            }

            dictionary.Add(anonymousObject, messageContractType);
        }

        private static void FindAnonymousTypesWithMessageContractsInTree(AnonymousObjectMemberDeclaratorSyntax initializer,
            IPropertySymbol messageContractProperty, IDictionary<AnonymousObjectCreationExpressionSyntax, ITypeSymbol> dictionary)
        {
            if (initializer.Expression is ImplicitArrayCreationExpressionSyntax implicitArrayCreationExpressionSyntax)
            {
                if (messageContractProperty.Type.IsValidMessageContractEnumerable(out var messageContractPropertyTypeArgument))
                {
                    FindAnonymousTypesWithMessageContractsInTree(implicitArrayCreationExpressionSyntax, messageContractPropertyTypeArgument, dictionary);
                }
            }
            else if (initializer.Expression is AnonymousObjectCreationExpressionSyntax anonymousObjectProperty)
            {
                FindAnonymousTypesWithMessageContractsInTree(anonymousObjectProperty, messageContractProperty.Type, dictionary);
            }
        }

        private static void FindAnonymousTypesWithMessageContractsInTree(ImplicitArrayCreationExpressionSyntax implicitArrayCreationExpressionSyntax,
            ITypeSymbol messageContractPropertyTypeArgument, IDictionary<AnonymousObjectCreationExpressionSyntax, ITypeSymbol> dictionary)
        {
            var expressions = implicitArrayCreationExpressionSyntax.Initializer.Expressions;
            foreach (var expression in expressions)
            {
                if (expression is AnonymousObjectCreationExpressionSyntax anonymousObjectArrayInitializer)
                {
                    FindAnonymousTypesWithMessageContractsInTree(anonymousObjectArrayInitializer, messageContractPropertyTypeArgument, dictionary);
                }
            }
        }

        private static string GetName(AnonymousObjectMemberDeclaratorSyntax initializer)
        {
            string name;
            if (initializer.NameEquals == null)
            {
                var expression = (MemberAccessExpressionSyntax)initializer.Expression;
                name = expression.Name.Identifier.Text;
            }
            else
            {
                name = initializer.NameEquals.Name.Identifier.Text;
            }

            return name;
        }

        private static SyntaxNode AddMissingProperties(SyntaxNode root, IDictionary<AnonymousObjectCreationExpressionSyntax, ITypeSymbol> dictionary)
        {
            var newRoot = root.TrackNodes(dictionary.Keys);

            foreach (var keyValuePair in dictionary)
            {
                var anonymousObject = newRoot.GetCurrentNode(keyValuePair.Key);
                var messageContractType = keyValuePair.Value;
                newRoot = AddMissingProperties(newRoot, anonymousObject, messageContractType);
            }

            return newRoot;
        }

        private static SyntaxNode AddMissingProperties(SyntaxNode root,
            AnonymousObjectCreationExpressionSyntax anonymousObject, ITypeSymbol messageContractType)
        {
            var newRoot = root;

            var messageContractProperties = messageContractType.GetMessageContractProperties();

            var propertiesToAdd = new List<AnonymousObjectMemberDeclaratorSyntax>();
            foreach (var messageContractProperty in messageContractProperties)
            {
                var initializer =
                    anonymousObject.Initializers.FirstOrDefault(i => GetName(i) == messageContractProperty.Name);

                if (initializer == null)
                {
                    var propertyToAdd = CreateProperty(messageContractProperty);
                    propertiesToAdd.Add(propertyToAdd);
                }
            }

            if (propertiesToAdd.Any())
            {
                var newAnonymousObject = anonymousObject
                    .AddInitializers(propertiesToAdd.ToArray())
                    .WithAdditionalAnnotations(Formatter.Annotation);
                newRoot = newRoot.ReplaceNode(anonymousObject, newAnonymousObject);
            }

            return newRoot;
        }

        private static AnonymousObjectMemberDeclaratorSyntax[] CreateProperties(ITypeSymbol messageContractType)
        {
            var messageContractProperties = messageContractType.GetMessageContractProperties();

            var propertiesToAdd = new List<AnonymousObjectMemberDeclaratorSyntax>();
            foreach (var messageContractProperty in messageContractProperties)
            {
                var propertyToAdd = CreateProperty(messageContractProperty);
                propertiesToAdd.Add(propertyToAdd);
            }

            return propertiesToAdd.ToArray();
        }

        private static AnonymousObjectMemberDeclaratorSyntax CreateProperty(IPropertySymbol messageContractProperty)
        {
            ExpressionSyntax expression;
            if (messageContractProperty.Type.IsValidMessageContractEnumerable(out var messageContractPropertyTypeArgument))
            {
                expression = CreateImplicitArray(messageContractPropertyTypeArgument);
            }
            else if (messageContractProperty.Type.TypeKind == TypeKind.Interface)
            {
                expression = CreateAnonymousObject(messageContractProperty.Type);
            }
            else if (messageContractProperty.Type.IsNullable(out var nullableTypeArgument))
            {
                expression = CreateDefaultNullable(nullableTypeArgument);
            }
            else
            {
                expression = CreateDefault(messageContractProperty.Type);
            }

            return SyntaxFactory.AnonymousObjectMemberDeclarator(SyntaxFactory.NameEquals(messageContractProperty.Name), expression)
                .WithAdditionalAnnotations(Formatter.Annotation)
                .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed);
        }

        private static ImplicitArrayCreationExpressionSyntax CreateImplicitArray(ITypeSymbol type)
        {
            ExpressionSyntax node;
            if (type.TypeKind == TypeKind.Interface)
            {
                node = CreateAnonymousObject(type);
            }
            else
            {
                node = CreateDefault(type);
            }

            var nodes = new ExpressionSyntax[] { node };
            var initializer = SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression)
                .WithExpressions(SyntaxFactory.SeparatedList(nodes));
            return SyntaxFactory.ImplicitArrayCreationExpression(initializer)
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        private static AnonymousObjectCreationExpressionSyntax CreateAnonymousObject(ITypeSymbol type)
        {
            var propertiesToAdd = CreateProperties(type);
            return SyntaxFactory.AnonymousObjectCreationExpression()
                .WithInitializers(SyntaxFactory.SeparatedList(propertiesToAdd))
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        private static DefaultExpressionSyntax CreateDefault(ITypeSymbol type)
        {
            return SyntaxFactory.DefaultExpression(SyntaxFactory.ParseTypeName(type.Name).WithAdditionalAnnotations(Simplifier.Annotation));
        }

        private static DefaultExpressionSyntax CreateDefaultNullable(ITypeSymbol type)
        {
            return SyntaxFactory.DefaultExpression(SyntaxFactory.NullableType(SyntaxFactory.ParseTypeName(type.Name).WithAdditionalAnnotations(Simplifier.Annotation)));
        }
    }
}