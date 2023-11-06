using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LurkingNinja.DomainReloadSG
{
    internal static class Common
    {
        /*
         * {0} name space if exists
         * {1} closing bracket for namespace if needed
         * {2} class definition
         */
        private const string NS_TEMPLATE = @"using System;
using UnityEngine;

{0}
    {2}
{1}";

        internal static string NamespaceTemplateResolve(string nameSpace, string source)
        {
            var ns = GetNamespaceTemplate(nameSpace);
            return string.Format(NS_TEMPLATE,
                /*{0}*/ns.Item1,
                /*{1}*/ns.Item2,
                /*{2}*/source);
        }

        internal static bool IsPartial(ClassDeclarationSyntax cds) =>
            cds.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword));

        internal static bool IsAbstractClass(ClassDeclarationSyntax cds) =>
            cds.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword));

        internal static bool IsStatic(FieldDeclarationSyntax fds) =>
            fds.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword));

        internal static bool IsReadOnly(FieldDeclarationSyntax fds) =>
            fds.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword));

        internal static bool IsStatic(MethodDeclarationSyntax mds) =>
            mds.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword));

        internal static bool IsVoidFunction(MethodDeclarationSyntax ms) =>
            ms.ReturnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

        internal static void AddSource(GeneratorExecutionContext context,
            string fileName, string source)
        {
            fileName = $"{fileName}_codegen.cs";
            context.AddSource(fileName, source);
        }
        
        internal static string GetNamespace(SyntaxNode node)
        {
            var nameSpace = string.Empty;
            var potentialNamespaceParent = node.Parent;

            while (potentialNamespaceParent != null
                   && !(potentialNamespaceParent is NamespaceDeclarationSyntax))
                potentialNamespaceParent = potentialNamespaceParent.Parent;

            if (!(potentialNamespaceParent is NamespaceDeclarationSyntax namespaceParent)) return nameSpace;
            
            nameSpace = namespaceParent.Name.ToString();

            while (true)
            {
                if (!(namespaceParent.Parent is NamespaceDeclarationSyntax parent)) break;

                nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                namespaceParent = parent;
            }

            return string.IsNullOrEmpty(nameSpace)
                ? string.Empty
                : nameSpace;
        }

        private static (string, string) GetNamespaceTemplate(string potentialNamespace)
        {
            var isNullOrEmpty = string.IsNullOrEmpty(potentialNamespace); 
            return (
                isNullOrEmpty
                    ? string.Empty
                    : $"namespace {potentialNamespace}\n{{",
                isNullOrEmpty
                    ? string.Empty
                    : "}");
        }
    }
}