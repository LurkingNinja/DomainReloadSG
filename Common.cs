using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LurkingNinja.DomainReloadSG
{
    internal static class Common
    {
        private const string FILENAME_POSTFIX = "_codegen.cs";
        
        internal const string NO_DOMAIN_SUPPORT_ATTRIBUTE = "NoDomainReloadSupport";
        
        /*
         * {0} name space if exists
         * {1} closing bracket for namespace if needed
         * {2} class definition
         * {3} using directives
         */
        private const string NS_TEMPLATE = @"{3}
{0}
    {2}
{1}";

        internal const string ATTRIBUTES_SOURCE = @"using System;

namespace LurkingNinja.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class " + NO_DOMAIN_SUPPORT_ATTRIBUTE + @" : Attribute
    {
        public " + NO_DOMAIN_SUPPORT_ATTRIBUTE + @"() {}
    }
}";
        
        /**
         * {0} Class name
         * {1} Generated assignments
         */
        internal const string SOURCE_TEMPLATE = @"public partial class {0}
    {{
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ApplyStaticFieldsAndEventHandlers()
        {{
{1}
        }}
    }}";

        internal static string NamespaceTemplateResolve(string usingDirectives, string nameSpace, string source)
        {
            var ns = GetNamespaceTemplate(nameSpace);
            return string.Format(NS_TEMPLATE,
                /*{0}*/ns.Item1,
                /*{1}*/ns.Item2,
                /*{2}*/source,
                /*{3}*/usingDirectives);
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

        internal static bool HasAttribute(ClassDeclarationSyntax cds, string attributeName) =>
            cds.AttributeLists
                .Any(cdsAttributeList => cdsAttributeList.Attributes
                    .Any(cdsAttribute => cdsAttribute.ToString().Trim().ToLower()
                        .Equals(attributeName.Trim().ToLower())));

        internal static void AddSource(GeneratorExecutionContext context, string fileName, string source) =>
            context.AddSource($"{fileName}{FILENAME_POSTFIX}", source);

        internal static void AddSource(GeneratorInitializationContext context, string fileName, string source) =>
            context.RegisterForPostInitialization(ctx =>
                ctx.AddSource($"{fileName}{FILENAME_POSTFIX}",  SourceText.From(source, Encoding.UTF8)));

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
        
        internal static void Log(string message) =>
            File.AppendAllText(@"D:\log.txt", $"{message}\n", Encoding.UTF8);

    }
}