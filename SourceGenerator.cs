﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LurkingNinja.DomainReloadSG
{
    [Generator]
    public class SourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new DrSyntaxReceiver());
            Common.AddSource(context, "DomainReloadSG_Attributes", Common.ATTRIBUTES_SOURCE);
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is DrSyntaxReceiver dsr)) return;
            if (dsr.eventHandlers.Count == 0 && dsr.fields.Count == 0) return;

            var nameSpace = Common.GetNamespace(dsr.ClassToAugment);
            var usingDirectives = Common.GetUsingDirectives(dsr.ClassToAugment);

            var lines = new StringBuilder();
            
            foreach (var field in dsr.fields)
            {
                if (field.Declaration.Variables.Count < 1) continue;
                foreach (var vds in field.Declaration.Variables)
                {
                    var identifier = vds.Identifier.ToFullString();
                    var value = vds.Initializer is null
                        ? "default"
                        : vds.Initializer.Value.ToFullString();
                    lines.AppendLine($"\t\t\t{identifier} = {value};");
                }
            }

            foreach (var handler in dsr.eventHandlers) lines.AppendLine($"\t\t\t{handler}");

            var source = string.Format(Common.SOURCE_TEMPLATE,
                /*{0}*/dsr.ClassToAugment.Identifier.ToFullString(),
                /*{1}*/lines);
            
            Common.AddSource(context, dsr.ClassToAugment.Identifier.ToFullString().Trim(), 
                Common.NamespaceTemplateResolve(usingDirectives, nameSpace, source));
        }
    }

    internal class DrSyntaxReceiver : ISyntaxReceiver
    {
        // Something[.maybeSomething] += MethodNameFromCurrentClass;
        private const string EVENT_SUBSCRIPTION_REGEX = @"^\w+[.\w]*\s*\+\=\s*{0}\s*;$";
        
        internal ClassDeclarationSyntax ClassToAugment { get; private set; }
        internal readonly List<FieldDeclarationSyntax> fields = new List<FieldDeclarationSyntax>();
        internal readonly List<string> eventHandlers = new List<string>();

        private readonly string[] _nameSpaceBlackList = { "TMPro" };
        
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (!(syntaxNode is ClassDeclarationSyntax cds)) return;
            if (Common.IsAbstractClass(cds)) return;
            if (!Common.IsPartial(cds)) return;
            if (Common.HasAttribute(cds, Common.NO_DOMAIN_SUPPORT_ATTRIBUTE)) return;
            if (_nameSpaceBlackList.Contains(Common.GetNamespace(cds))) return;
            
            var potentialMethods = new List<string>();
            
            fields.Clear();
            foreach (var node in cds.ChildNodes())
            {
                // if we have static field we gather them.
                if (node is FieldDeclarationSyntax fds
                    && Common.IsStatic(fds)
                    && !Common.IsReadOnly(fds)) fields.Add(fds);
                // we gather potential static methods to check if they are events
                if (!(node is MethodDeclarationSyntax mds) || !Common.IsStatic(mds)) continue;
                // we do not count methods with return type other than void
                if (!Common.IsVoidFunction(mds)) continue;
                
                potentialMethods.Add(mds.Identifier.ToFullString());
            }

            // checking if there is any static event assignment in any methods of the class
            eventHandlers.Clear();
            foreach (var node in cds.ChildNodes())
            {
                if (!(node is MethodDeclarationSyntax mds)) continue;

                if (mds.Body is null) continue;
                
                foreach (var childNode in mds.Body.ChildNodes())
                {
                    if (!(childNode is ExpressionStatementSyntax ess)) continue;
                    if (!ess.Expression.IsKind(SyntaxKind.AddAssignmentExpression)) continue;

                    var source = childNode.ToFullString().Trim();
                    foreach (var _ in potentialMethods.Select(potentialMethod =>
                            new Regex(string.Format(EVENT_SUBSCRIPTION_REGEX, potentialMethod),
                                    RegexOptions.IgnoreCase)).Where(reg => reg.IsMatch(source)))
                    {
                        eventHandlers.Add(source.Replace("+=", "-="));
                    }
                }
            }
            
            if (fields.Count > 0 || eventHandlers.Count > 0) ClassToAugment = cds;
        }
    }
}
