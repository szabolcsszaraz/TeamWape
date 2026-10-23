using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerators;

public static class OperatorUtilities
{
    public static INamedTypeSymbol? GetClassWithAttribute(GeneratorSyntaxContext context, string attributeName)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel;
        var classSymbol = model.GetDeclaredSymbol(classDecl);

        if (classSymbol == null)
            return null;

        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == attributeName ||
                attr.AttributeClass?.Name == attributeName + "Attribute")
            {
                return classSymbol;
            }
        }

        return null;
    }

    // list of all public fields of an Operator that aren't 'inputs' or 'outputs'
    public static List<IFieldSymbol> GetAllPublicFields(INamedTypeSymbol type)
    {
        var fields = new List<IFieldSymbol>();
        var current = type;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.Name is "inputs" or "outputs")
                    continue;
                if (member.DeclaredAccessibility == Accessibility.Public)
                    fields.Add(member);
            }

            current = current.BaseType;
        }

        return fields;
    }

    public static bool TryGetAttribute(INamedTypeSymbol classSymbol, string attributeName, out AttributeData attributeData)
    {
        var current = classSymbol;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var attr in current.GetAttributes())
            {
                if (attr.AttributeClass.Name.Equals(attributeName) || attr.AttributeClass.Name.Equals(attributeName + "Attribute"))
                {
                    attributeData = attr;
                    return true;
                }
            }

            current = current.BaseType;
        }

        attributeData = null;
        return false;
    }

    public static bool InheritsFrom(INamedTypeSymbol type, string baseType)
    {
        var current = type;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (current.Name.Equals(baseType))
                return true;
            current = current.BaseType;
        }

        return false;
    }
}
