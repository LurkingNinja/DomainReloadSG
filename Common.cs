using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LurkingNinja.DomainReloadSG
{
    internal static class Common
    {
        private const string LIST_SEPARATOR = ", ";

        internal const char TRIM_QUOTE = '"';

        private const string HOFA_ASSEMBLY_NAME = "HofA";

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
        
        internal static void Log(string message) =>
            File.AppendAllText(@"D:\log.txt", $"{message}\n", Encoding.UTF8);

        internal static string TrimQualifiers(string identifier) =>
            identifier.Split('.').Last().Trim(TRIM_QUOTE);
        
        internal static string ListJoin(IEnumerable<string> list, string separator = "") =>
            string.Join($"{LIST_SEPARATOR}{separator}", list);
        
        internal static void AddSource(SourceProductionContext context,
            string fileName, string source, bool log = false)
        {
            fileName = $"{fileName}_codegen.cs";
            context.AddSource(fileName, source);
            if (!log) return;
            Log($"<--- {fileName}\n{source}\n --->");
        }

        internal static void AddSource(GeneratorExecutionContext context,
            string fileName, string source, bool log = false)
        {
            fileName = $"{fileName}_codegen.cs";
            context.AddSource(fileName, source);
            if (!log) return;
            Log($"<--- {fileName}\n{source}\n --->");
        }
        
        internal static bool CheckAssembly(string assemblyName) =>
            string.IsNullOrEmpty(assemblyName) || !assemblyName.Equals(HOFA_ASSEMBLY_NAME);

        internal static bool CheckAssembly(GeneratorSyntaxContext context) =>
            string.IsNullOrEmpty(context.SemanticModel.Compilation.AssemblyName)
            || !context.SemanticModel.Compilation.AssemblyName.Equals(HOFA_ASSEMBLY_NAME);
        
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

        internal static (string, string) GetNamespaceTemplate(string potentialNamespace)
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

        internal static string GetClassName(SyntaxNode node)
        {
            var className = string.Empty;
            var potentialClassName = node.Parent;

            while (potentialClassName != null && !(potentialClassName is ClassDeclarationSyntax))
                potentialClassName = potentialClassName.Parent;

            if (!(potentialClassName is ClassDeclarationSyntax classParent)) return className;

            return classParent.Identifier.ToString();
        }

        public static string Capitalize(string input)
        {
            var replaceRegex = new Regex(@"^(\w?_+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            input = replaceRegex.Replace(input, string.Empty);
            return $"{input[0].ToString().ToUpper()}{input.Substring(1, input.Length - 1)}";
        }

        internal static (string, string) GetStatParam(MemberDeclarationSyntax member, string attributeName)
        {
            foreach (var attributeList in member.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (TrimQualifiers(attribute.Name.ToString()) != attributeName) continue;
                    if (attribute.ArgumentList is null
                        || attribute.ArgumentList?.Arguments.Count < 2) return (null, null);

                    var ability =
                        TrimQualifiers(attribute.ArgumentList?.Arguments[0].Expression.ToString());
                    var skill = TrimQualifiers(attribute.ArgumentList?.Arguments[1].Expression.ToString());
                    return (ability, skill);
                }
            }

            return (null, null);
        }
        
        internal static string GetStringParam(MemberDeclarationSyntax member, string attributeName, string defaultValue = "")
        {
            foreach (var attributeList in member.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (TrimQualifiers(attribute.Name.ToString()) != attributeName) continue;
                    if (attribute.ArgumentList is null
                        || attribute.ArgumentList?.Arguments.Count == 0) return defaultValue;

                    return attribute.ArgumentList?.Arguments[0].Expression.ToString().Trim(TRIM_QUOTE);
                }
            }

            return defaultValue;
        }
        
        internal static string GetNamedParams(MemberDeclarationSyntax member, string attributeName,
                int defaultValue = 0, int defaultMinValue = int.MinValue, int defaultMaxValue = int.MaxValue)
        {
            foreach (var attributeList in member.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (TrimQualifiers(attribute.Name.ToString()) != attributeName) continue;
                    if (attribute.ArgumentList is null)
                        return $"value: {defaultValue}, minValue: {defaultMinValue}, maxValue: {defaultMaxValue}";

                    var paramList = new [] { "value:", "minValue:", "maxValue:" };
                    var res = new List<string>();
                    for (var index = 0; index < attribute.ArgumentList?.Arguments.Count; index++)
                    {
                        var argument = attribute.ArgumentList.Arguments[index];
                        var argName = argument.NameColon?.ToString();
                        if (string.IsNullOrEmpty(argName)) argName = paramList[index];
                        res.Add($"{argName}{argument.Expression.ToString()}");
                    }
                    return string.Join(LIST_SEPARATOR, res);
                }
            }

            return null;
        }

        internal static bool CheckAttribute(string attributeName, string attributeToCheck)
        {
            attributeName = TrimQualifiers(attributeName);
            return attributeToCheck.Equals(attributeName) || $"{attributeToCheck}Attribute".Equals(attributeName);
        }
    }
}