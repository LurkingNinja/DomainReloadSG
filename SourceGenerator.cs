using System.Collections.Generic;
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
        /**
         * {0} Class name
         * {1} Generated assignments
         */
        private readonly string _sourceTemplate = @"public partial class {0}
    {{
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ApplyStaticFieldsAndEventHandlers()
        {{
{1}
        }}
    }}";
        
        public void Initialize(GeneratorInitializationContext context) =>
            context.RegisterForSyntaxNotifications(() => new DrSyntaxReceiver());

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is DrSyntaxReceiver dsr)) return;
            if (dsr.eventHandlers.Count == 0 && dsr.fields.Count == 0) return;

            var nameSpace = Common.GetNamespace(dsr.ClassToAugment);

            var lines = new StringBuilder();

            foreach (var field in dsr.fields)
            {
                if (field.Declaration.Variables.Count < 1) continue;
                for (var i = 0; i < field.Declaration.Variables.Count; i++)
                {
                    var identifier = field.Declaration.Variables[i].Identifier.ToFullString();
                    var value = field.Declaration.Variables[i].Initializer is null
                        ? "default"
                        : field.Declaration.Variables[i].Initializer.Value.ToFullString();
                    lines.AppendLine($"\t\t\t{identifier} = {value};");
                }
            }

            foreach (var handler in dsr.eventHandlers) lines.AppendLine($"\t\t\t{handler}");

            var source = string.Format(_sourceTemplate,
                /*{0}*/dsr.ClassToAugment.Identifier.ToFullString(),
                /*{1}*/lines);
            
            Common.AddSource(context, dsr.ClassToAugment.Identifier.ToFullString(), 
                Common.NamespaceTemplateResolve(nameSpace, source), true);
        }
    }

    internal class DrSyntaxReceiver : ISyntaxReceiver
    {
        private const string EVENT_SUBSCRIPTION_REGEX = @"^\w+[.\w]*\s*\+\=\s*{0}\s*;$";
        
        internal ClassDeclarationSyntax ClassToAugment { get; private set; }
        internal List<FieldDeclarationSyntax> fields = new List<FieldDeclarationSyntax>();
        internal List<string> eventHandlers = new List<string>();
        
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (!(syntaxNode is ClassDeclarationSyntax cds)) return;
            if (Common.IsAbstractClass(cds)) return;
            if (!Common.IsPartial(cds)) return;

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
                    foreach (var reg in potentialMethods.Select(potentialMethod =>
                            new Regex(string.Format(EVENT_SUBSCRIPTION_REGEX, potentialMethod),
                                    RegexOptions.IgnoreCase)).Where(reg => reg.IsMatch(source)))
                        eventHandlers.Add(source.Replace("+=", "-="));
                }
            }
            if (fields.Count > 0 || eventHandlers.Count > 0) ClassToAugment = cds;
        }
    }


}
