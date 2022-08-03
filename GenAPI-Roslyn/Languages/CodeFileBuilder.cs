// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.SymbolDisplay;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ApiView
{
    public class CodeFileBuilder
    {
        private static readonly char[] _newlineChars = new char[] { '\r', '\n' };

        SymbolDisplayFormat _defaultDisplayFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
            propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral |
                                  SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                  SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix |
                                  SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                  SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
                                  // AllowDefaultLiteral, CollapseTupleTypes
            kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
            parameterOptions: SymbolDisplayParameterOptions.IncludeDefaultValue |
                              SymbolDisplayParameterOptions.IncludeExtensionThis |
                              SymbolDisplayParameterOptions.IncludeName |
                              SymbolDisplayParameterOptions.IncludeParamsRefOut |
                              SymbolDisplayParameterOptions.IncludeType,
            /// add option to print parameter names?
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                             SymbolDisplayGenericsOptions.IncludeTypeParameters |
                             SymbolDisplayGenericsOptions.IncludeVariance,
            memberOptions: SymbolDisplayMemberOptions.IncludeExplicitInterface |
                           SymbolDisplayMemberOptions.IncludeConstantValue |
                           SymbolDisplayMemberOptions.IncludeModifiers |
                           SymbolDisplayMemberOptions.IncludeParameters |
                           SymbolDisplayMemberOptions.IncludeType
        );
        SymbolDisplayFormat _defaultClassDisplayFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
            propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral |
                                  SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                  SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix |
                                  SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                  SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
            kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
            parameterOptions: SymbolDisplayParameterOptions.IncludeDefaultValue |
                              SymbolDisplayParameterOptions.IncludeExtensionThis |
                              SymbolDisplayParameterOptions.IncludeName |
                              SymbolDisplayParameterOptions.IncludeParamsRefOut |
                              SymbolDisplayParameterOptions.IncludeType,
            /// add option to print parameter names?
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                             SymbolDisplayGenericsOptions.IncludeTypeParameters |
                             SymbolDisplayGenericsOptions.IncludeVariance,
            memberOptions: SymbolDisplayMemberOptions.IncludeExplicitInterface |
                           SymbolDisplayMemberOptions.IncludeConstantValue |
                           SymbolDisplayMemberOptions.IncludeModifiers |
                           SymbolDisplayMemberOptions.IncludeParameters |
                           SymbolDisplayMemberOptions.IncludeType
        );

        private IAssemblySymbol _assembly;

        public ICodeFileBuilderSymbolOrderProvider SymbolOrderProvider { get; set; } = new CodeFileBuilderSymbolOrderProvider();

        public const string CurrentVersion = "22";

        private IEnumerable<INamespaceSymbol> EnumerateNamespaces(IAssemblySymbol assemblySymbol)
        {
            var stack = new Stack<INamespaceSymbol>();
            stack.Push(assemblySymbol.GlobalNamespace);
            while (stack.TryPop(out var currentNamespace))
            {
                /// add additional namespaces...
                /// 
                if (HasAnyPublicTypes(currentNamespace))
                {
                    yield return currentNamespace;
                }
                foreach (var subNamespace in currentNamespace.GetNamespaceMembers())
                {
                    stack.Push(subNamespace);
                }
            }
        }

        public CodeFile Build(IAssemblySymbol assemblySymbol)
        {
            _assembly = assemblySymbol;
            var builder = new CodeFileTokensBuilder();

            var navigationItems = new List<NavigationItem>();
            var namespaces = EnumerateNamespaces(assemblySymbol);
            foreach (var namespaceSymbol in SymbolOrderProvider.OrderNamespaces(namespaces))
            {
                if (namespaceSymbol.IsGlobalNamespace)
                {
                    foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namespaceSymbol.GetTypeMembers()))
                    {
                        BuildType(builder, namedTypeSymbol, navigationItems);
                    }
                }
                else
                {
                    Build(builder, namespaceSymbol, navigationItems);
                }
            }

            NavigationItem assemblyNavigationItem = new NavigationItem()
            {
                Text = assemblySymbol.Name + ".dll",
                ChildItems = navigationItems.ToArray(),
                Tags = { { "TypeKind", "assembly" } }
            };

            var node = new CodeFile()
            {
                Name = $"{assemblySymbol.Name} ({assemblySymbol.Identity.Version})",
                Language = "C#",
                Tokens = builder.Tokens.ToArray(),
                VersionString = CurrentVersion,
                Navigation = new[] { assemblyNavigationItem },
                PackageName = assemblySymbol.Name
            };

            return node;
        }

        private void Build(CodeFileTokensBuilder builder, INamespaceSymbol namespaceSymbol, List<NavigationItem> navigationItems)
        {
            builder.Keyword(SyntaxKind.NamespaceKeyword);
            builder.Space();
            BuildNamespaceName(builder, namespaceSymbol);

            builder.Space();
            builder.Punctuation(SyntaxKind.OpenBraceToken);
            builder.IncrementIndent();
            builder.NewLine();

            List<NavigationItem> namespaceItems = new List<NavigationItem>();
            foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namespaceSymbol.GetTypeMembers()))
            {
                BuildType(builder, namedTypeSymbol, namespaceItems);
            }

            CloseBrace(builder);

            var namespaceItem = new NavigationItem()
            {
                NavigationId = namespaceSymbol.GetId(),
                Text = namespaceSymbol.ToDisplayString(),
                ChildItems = namespaceItems.ToArray(),
                Tags = { { "TypeKind", "namespace" } }
            };
            navigationItems.Add(namespaceItem);
        }

        private void BuildNamespaceName(CodeFileTokensBuilder builder, INamespaceSymbol namespaceSymbol)
        {
            builder.Punctuation(namespaceSymbol.OriginalDefinition.ToString());
        }

        private bool HasAnyPublicTypes(INamespaceSymbol subNamespaceSymbol)
        {
            return subNamespaceSymbol.GetTypeMembers().Any(t => IsAccessible(t));
        }

        private void BuildType(CodeFileTokensBuilder builder, INamedTypeSymbol namedType, List<NavigationItem> navigationBuilder)
        {
            /// internal, private, etc!!!!
            if (!IsAccessible(namedType))
            {
                return;
            }

            var navigationItem = new NavigationItem()
            {
                NavigationId = namedType.GetId(),
                Text = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            };
            navigationBuilder.Add(navigationItem);
            navigationItem.Tags.Add("TypeKind", namedType.TypeKind.ToString().ToLowerInvariant());

            BuildAttributes(builder, namedType.GetAttributes());

            builder.WriteIndent();
            BuildVisibility(builder, namedType);

            builder.Space();

            switch (namedType.TypeKind)
            {
                case TypeKind.Class:
                    BuildClassModifiers(builder, namedType);
                    builder.Keyword(SyntaxKind.PartialKeyword);
                    builder.Space();
                    builder.Keyword(SyntaxKind.ClassKeyword);
                    break;
                case TypeKind.Delegate:
                    builder.Keyword(SyntaxKind.DelegateKeyword);
                    break;
                case TypeKind.Enum:
                    builder.Keyword(SyntaxKind.EnumKeyword);
                    break;
                case TypeKind.Interface:
                    builder.Keyword(SyntaxKind.InterfaceKeyword);
                    break;
                case TypeKind.Struct:
                    if (namedType.IsReadOnly)
                    {
                        builder.Keyword(SyntaxKind.ReadOnlyKeyword);
                        builder.Space();
                    }
                    builder.Keyword(SyntaxKind.PartialKeyword);
                    builder.Space();
                    builder.Keyword(SyntaxKind.StructKeyword);
                    break;
            }

            builder.Space();

            if (namedType.TypeKind == TypeKind.Class || namedType.TypeKind == TypeKind.Struct ||
                namedType.TypeKind == TypeKind.Enum || namedType.TypeKind == TypeKind.Interface ||
                namedType.TypeKind == TypeKind.Delegate)
            {
                builder.Append(new CodeFileToken()
                {
                    DefinitionId = namedType.GetId(),
                    Kind = CodeFileTokenKind.LineIdMarker
                });
                if (NeedsAccessibility(namedType))
                {
                    builder.Keyword(SyntaxFacts.GetText(ToEffectiveAccessibility(namedType.DeclaredAccessibility)));
                    builder.Space();
                }

                foreach (var symbolDisplayPart in namedType.ToDisplayParts(SymbolDisplayFormat.FullyQualifiedFormat))
                {
                    if (symbolDisplayPart.Kind == SymbolDisplayPartKind.ClassName)
                    {
                        var symbol = symbolDisplayPart.Symbol;
                        var text = symbolDisplayPart.ToString();
                    }
                }

                foreach (var symbolDisplayPart in namedType.ToDisplayParts(_defaultClassDisplayFormat))
                {
                    builder.Append(MapToken(namedType, symbolDisplayPart));
                }

            }
            else
            {
                NodeFromSymbol(builder, namedType);
            }
            if (namedType.TypeKind == TypeKind.Delegate)
            {
                builder.Punctuation(SyntaxKind.SemicolonToken);
                builder.NewLine();
                return;
            }

            builder.Space();

            BuildBaseType(builder, namedType);

            builder.Punctuation(SyntaxKind.OpenBraceToken);
            builder.IncrementIndent();
            builder.NewLine();

            foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namedType.GetTypeMembers()))
            {
                BuildType(builder, namedTypeSymbol, navigationBuilder);
            }
            foreach (var member in SymbolOrderProvider.OrderMembers(namedType.GetMembers()))
            {
                if (member.Kind == SymbolKind.NamedType || member.IsImplicitlyDeclared || !IsAccessible(member)) continue;
                //if (member.Kind == SymbolKind.NamedType || member.IsImplicitlyDeclared || (!(member.IsOverride || member.IsAbstract) && !IsAccessible(member))) continue;
                //if (member.Kind == SymbolKind.NamedType || member.IsImplicitlyDeclared || (!member.IsOverride && !IsAccessible(member))) continue;
                if (member is IMethodSymbol method)
                {
                    if (method.MethodKind == MethodKind.PropertyGet ||
                        method.MethodKind == MethodKind.PropertySet ||
                        method.MethodKind == MethodKind.EventAdd ||
                        method.MethodKind == MethodKind.EventRemove ||
                        method.MethodKind == MethodKind.EventRaise)
                    {
                        continue;
                    }
                }
                BuildMember(builder, member);
            }

            CloseBrace(builder);
        }

        private static void BuildClassModifiers(CodeFileTokensBuilder builder, INamedTypeSymbol namedType)
        {
            if (namedType.IsAbstract)
            {
                builder.Keyword(SyntaxKind.AbstractKeyword);
                builder.Space();
            }

            if (namedType.IsStatic)
            {
                builder.Keyword(SyntaxKind.StaticKeyword);
                builder.Space();
            }

            if (namedType.IsSealed)
            {
                builder.Keyword(SyntaxKind.SealedKeyword);
                builder.Space();
            }
        }

        private void BuildBaseType(CodeFileTokensBuilder builder, INamedTypeSymbol namedType)
        {
            bool first = true;

            if (namedType.BaseType != null &&
                namedType.BaseType.SpecialType == SpecialType.None)
            {
                builder.Punctuation(SyntaxKind.ColonToken);
                builder.Space();
                first = false;

                DisplayName(builder, namedType.BaseType);
            }

            foreach (var typeInterface in namedType.Interfaces)
            {
                if (!IsAccessible(typeInterface)) continue;

                if (!first)
                {
                    builder.Punctuation(SyntaxKind.CommaToken);
                    builder.Space();
                }
                else
                {
                    builder.Punctuation(SyntaxKind.ColonToken);
                    builder.Space();
                    first = false;
                }

                DisplayName(builder, typeInterface);
            }

            if (!first)
            {
                builder.Space();
            }
        }

        private static void CloseBrace(CodeFileTokensBuilder builder)
        {
            builder.DecrementIndent();
            builder.WriteIndent();
            builder.Punctuation(SyntaxKind.CloseBraceToken);
            builder.NewLine();
        }

        private void BuildMember(CodeFileTokensBuilder builder, ISymbol member)
        {
            BuildAttributes(builder, member.GetAttributes());

            builder.WriteIndent();
            NodeFromSymbol(builder, member);

            if (member.Kind == SymbolKind.Field && member.ContainingType.TypeKind == TypeKind.Enum)
            {
                builder.Punctuation(SyntaxKind.CommaToken);
            }
            else if (member.Kind != SymbolKind.Property && member.Kind != SymbolKind.Event && member.Kind != SymbolKind.Field && !member.IsAbstract)
            {
                builder.Space();
                builder.Punctuation(SyntaxKind.OpenBraceToken);

                builder.Space();
                builder.Keyword(SyntaxKind.ThrowKeyword);
                builder.Space();
                builder.Keyword(SyntaxKind.NullKeyword);
                builder.Punctuation(SyntaxKind.SemicolonToken);

                builder.Space();
                builder.Punctuation(SyntaxKind.CloseBraceToken);
            }
            else if (member.Kind == SymbolKind.Event)
            {
                // TODO:
                if (!member.IsAbstract)
                {
                    builder.Punctuation(" { add { } remove { } }");
                }
                else
                {
                    builder.Punctuation(SyntaxKind.SemicolonToken);
                }
            }
            else if (member.Kind == SymbolKind.Field)
            {
                builder.Punctuation(SyntaxKind.SemicolonToken);
            }
            else if (member.Kind == SymbolKind.Method && member.IsAbstract)
            {
                builder.Punctuation(SyntaxKind.SemicolonToken);
            }

            builder.NewLine();
        }

        private void BuildAttributes(CodeFileTokensBuilder builder, ImmutableArray<AttributeData> attributes)
        {
            foreach (var attribute in attributes)
            {
                if (!IsAccessible(attribute.AttributeClass) || IsSkippedAttribute(attribute.AttributeClass))
                {
                    continue;
                }
                builder.WriteIndent();
                builder.Punctuation(SyntaxKind.OpenBracketToken);
                var str = attribute.ToString();
                builder.Punctuation(str);

                builder.Punctuation(SyntaxKind.CloseBracketToken);
                builder.NewLine();
            }
        }

        private bool IsSkippedAttribute(INamedTypeSymbol attributeAttributeClass)
        {
            switch (attributeAttributeClass.Name)
            {
                case "DebuggerStepThroughAttribute":
                case "AsyncStateMachineAttribute":
                case "IteratorStateMachineAttribute":
                case "DefaultMemberAttribute":
                case "AsyncIteratorStateMachineAttribute":
                case "TupleElementNamesAttribute":
                    return true;
                default:
                    return false;
            }
        }

        private void BuildTypedConstant(CodeFileTokensBuilder builder, TypedConstant typedConstant)
        {
            if (typedConstant.IsNull)
            {
                builder.Keyword(SyntaxKind.NullKeyword);
            }
            else if (typedConstant.Kind == TypedConstantKind.Enum)
            {
                new CodeFileBuilderEnumFormatter(builder).Format(typedConstant.Type, typedConstant.Value);
            }
            else if (typedConstant.Kind == TypedConstantKind.Type)
            {
                builder.Keyword(SyntaxKind.TypeOfKeyword);
                builder.Punctuation("(");
                DisplayName(builder, (ITypeSymbol)typedConstant.Value);
                builder.Punctuation(")");
            }
            else if (typedConstant.Kind == TypedConstantKind.Array)
            {
                builder.Keyword(SyntaxKind.NewKeyword);
                builder.Punctuation("[] {");

                bool first = true;

                foreach (var value in typedConstant.Values)
                {
                    if (!first)
                    {
                        builder.Punctuation(SyntaxKind.CommaToken);
                        builder.Space();
                    }
                    else
                    {
                        first = false;
                    }

                    BuildTypedConstant(builder, value);
                }
                builder.Punctuation("}");
            }
            else
            {
                if (typedConstant.Value is string s)
                {
                    builder.Append(
                        ObjectDisplay.FormatLiteral(s, ObjectDisplayOptions.UseQuotes | ObjectDisplayOptions.EscapeNonPrintableCharacters),
                        CodeFileTokenKind.StringLiteral);
                }
                else
                {
                    builder.Append(
                        ObjectDisplay.FormatPrimitive(typedConstant.Value, ObjectDisplayOptions.None),
                        CodeFileTokenKind.Literal);
                }
            }
        }

        private void NodeFromSymbol(CodeFileTokensBuilder builder, ISymbol symbol)
        {
            builder.Append(new CodeFileToken()
            {
                DefinitionId = symbol.GetId(),
                Kind = CodeFileTokenKind.LineIdMarker
            });
            DisplayName(builder, symbol, symbol);
        }

        private void BuildVisibility(CodeFileTokensBuilder builder, ISymbol symbol)
        {
            builder.Keyword(SyntaxFacts.GetText(ToEffectiveAccessibility(symbol.DeclaredAccessibility)));
        }

        private void DisplayName(CodeFileTokensBuilder builder, ISymbol symbol, ISymbol definedSymbol = null)
        {
            if (NeedsAccessibility(symbol))
            {
                if (symbol.DeclaredAccessibility == Accessibility.ProtectedOrFriend && symbol.IsOverride)
                {
                    builder.Keyword(SyntaxFacts.GetText(Accessibility.Protected));
                    builder.Space();
                }
                else
                {
                    builder.Keyword(SyntaxFacts.GetText(ToEffectiveAccessibility(symbol.DeclaredAccessibility)));
                    builder.Space();
                }
            }
            bool setOrGetKeywordFound = false;
            foreach (var symbolDisplayPart in symbol.ToDisplayParts(_defaultDisplayFormat))
            {
                if (setOrGetKeywordFound)
                {
                    setOrGetKeywordFound = false;
                    continue;
                }

                builder.Append(MapToken(definedSymbol, symbolDisplayPart));

                if (symbolDisplayPart.Kind == SymbolDisplayPartKind.Keyword && !symbol.IsAbstract)
                {
                    if (symbolDisplayPart.ToString() == "set")
                    {
                        builder.Punctuation(" { }");
                        setOrGetKeywordFound = true;
                    }
                    else if (symbolDisplayPart.ToString() == "get")
                    {
                        builder.Punctuation(" { throw null; }");
                        setOrGetKeywordFound = true;
                    }
                }
            }
        }

        private bool NeedsAccessibility(ISymbol symbol)
        {
            return symbol switch
            {
                INamespaceSymbol => false,
                INamedTypeSymbol => false,
                IFieldSymbol fieldSymbol => fieldSymbol.ContainingType.TypeKind != TypeKind.Enum,
                IMethodSymbol methodSymbol => !methodSymbol.ExplicitInterfaceImplementations.Any() &&
                                              methodSymbol.ContainingType.TypeKind != TypeKind.Interface,
                IPropertySymbol propertySymbol => !propertySymbol.ExplicitInterfaceImplementations.Any() &&
                                                  propertySymbol.ContainingType.TypeKind != TypeKind.Interface,
                _ => true
            };
        }

        private CodeFileToken MapToken(ISymbol definedSymbol, SymbolDisplayPart symbolDisplayPart)
        {
            CodeFileTokenKind kind;

            switch (symbolDisplayPart.Kind)
            {
                case SymbolDisplayPartKind.TypeParameterName:
                case SymbolDisplayPartKind.ParameterName:
                case SymbolDisplayPartKind.AliasName:
                case SymbolDisplayPartKind.AssemblyName:
                case SymbolDisplayPartKind.ClassName:
                case SymbolDisplayPartKind.DelegateName:
                case SymbolDisplayPartKind.EnumName:
                case SymbolDisplayPartKind.ErrorTypeName:
                case SymbolDisplayPartKind.InterfaceName:
                case SymbolDisplayPartKind.StructName:
                    kind = CodeFileTokenKind.TypeName;
                    break;
                case SymbolDisplayPartKind.Keyword:
                    kind = CodeFileTokenKind.Keyword;
                    break;
                case SymbolDisplayPartKind.LineBreak:
                    kind = CodeFileTokenKind.Newline;
                    break;
                case SymbolDisplayPartKind.StringLiteral:
                    kind = CodeFileTokenKind.StringLiteral;
                    break;
                case SymbolDisplayPartKind.Punctuation:
                    kind = CodeFileTokenKind.Punctuation;
                    break;
                case SymbolDisplayPartKind.Space:
                    kind = CodeFileTokenKind.Whitespace;
                    break;
                case SymbolDisplayPartKind.PropertyName:
                case SymbolDisplayPartKind.EventName:
                case SymbolDisplayPartKind.FieldName:
                case SymbolDisplayPartKind.MethodName:
                case SymbolDisplayPartKind.Operator:
                case SymbolDisplayPartKind.EnumMemberName:
                case SymbolDisplayPartKind.ExtensionMethodName:
                case SymbolDisplayPartKind.ConstantName:
                    kind = CodeFileTokenKind.MemberName;
                    break;
                default:
                    kind = CodeFileTokenKind.Text;
                    break;
            }

            string navigateToId = null;
            var symbol = symbolDisplayPart.Symbol;

            if (symbol is INamedTypeSymbol &&
                (definedSymbol == null || !SymbolEqualityComparer.Default.Equals(definedSymbol, symbol)) &&
                SymbolEqualityComparer.Default.Equals(_assembly, symbol.ContainingAssembly))
            {
                navigateToId = symbol.GetId();
            }

            return new CodeFileToken()
            {
                DefinitionId = (definedSymbol != null && SymbolEqualityComparer.Default.Equals(definedSymbol, symbol)) ? definedSymbol.GetId() : null,
                NavigateToId = navigateToId,
                Value = symbolDisplayPart.ToString(),
                Kind = kind
            };
        }

        private Accessibility ToEffectiveAccessibility(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Internal:
                    return Accessibility.Internal;
                case Accessibility.Protected:
                    return Accessibility.Protected;
                case Accessibility.Private:
                    return Accessibility.Private;
                default:
                    return accessibility;
            }
        }

        private bool IsAccessible(ISymbol s)
        {
            switch (s.DeclaredAccessibility)
            {
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                case Accessibility.Public:
                    return true;
                default:
                    return IsAccessibleExplicitInterfaceImplementation(s);
            }
        }

        private bool IsAccessibleExplicitInterfaceImplementation(ISymbol s)
        {
            return s switch
            {
                IMethodSymbol methodSymbol => methodSymbol.ExplicitInterfaceImplementations.Any(i => IsAccessible(i.ContainingType)),
                IPropertySymbol propertySymbol => propertySymbol.ExplicitInterfaceImplementations.Any(i => IsAccessible(i.ContainingType)),
                _ => false
            };
        }

        internal class CodeFileBuilderEnumFormatter : AbstractSymbolDisplayVisitor
        {
            private readonly CodeFileTokensBuilder _builder;

            public CodeFileBuilderEnumFormatter(CodeFileTokensBuilder builder) : base(null, SymbolDisplayFormat.FullyQualifiedFormat, false, null, 0, false)
            {
                _builder = builder;
            }

            protected override AbstractSymbolDisplayVisitor MakeNotFirstVisitor(bool inNamespaceOrType = false)
            {
                return this;
            }

            protected override void AddLiteralValue(SpecialType type, object value)
            {
                _builder.Append(ObjectDisplay.FormatPrimitive(value, ObjectDisplayOptions.None), CodeFileTokenKind.Literal);
            }

            protected override void AddExplicitlyCastedLiteralValue(INamedTypeSymbol namedType, SpecialType type, object value)
            {
                _builder.Append(ObjectDisplay.FormatPrimitive(value, ObjectDisplayOptions.None), CodeFileTokenKind.Literal);
            }

            protected override void AddSpace()
            {
                _builder.Space();
            }

            protected override void AddBitwiseOr()
            {
                _builder.Punctuation(SyntaxKind.BarToken);
            }

            public override void VisitField(IFieldSymbol symbol)
            {
                _builder.Append(symbol.Type.Name, CodeFileTokenKind.TypeName);
                _builder.Punctuation(SyntaxKind.DotToken);
                _builder.Append(symbol.Name, CodeFileTokenKind.MemberName);
            }

            public void Format(ITypeSymbol type, object typedConstantValue)
            {
                AddNonNullConstantValue(type, typedConstantValue);
            }
        }
    }
}
