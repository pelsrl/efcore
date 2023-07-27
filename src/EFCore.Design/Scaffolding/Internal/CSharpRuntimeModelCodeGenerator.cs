// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Microsoft.EntityFrameworkCore.Scaffolding.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class CSharpRuntimeModelCodeGenerator : ICompiledModelCodeGenerator
{
    private readonly ICSharpHelper _code;
    private readonly ICSharpRuntimeAnnotationCodeGenerator _annotationCodeGenerator;

    private const string FileExtension = ".cs";
    private const string ModelSuffix = "Model";
    private const string ModelBuilderSuffix = "ModelBuilder";
    private const string EntityTypeSuffix = "EntityType";

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public CSharpRuntimeModelCodeGenerator(
        ICSharpRuntimeAnnotationCodeGenerator annotationCodeGenerator,
        ICSharpHelper cSharpHelper)
    {
        _annotationCodeGenerator = annotationCodeGenerator;
        _code = cSharpHelper;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual string Language
        => "C#";

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual IReadOnlyCollection<ScaffoldedFile> GenerateModel(
        IModel model,
        CompiledModelCodeGenerationOptions options)
    {
        // Translated expressions don't have nullability annotations
        var nullable = false;
        var scaffoldedFiles = new List<ScaffoldedFile>();
        var modelCode = CreateModel(options.ModelNamespace, options.ContextType, nullable);
        var modelFileName = options.ContextType.ShortDisplayName() + ModelSuffix + FileExtension;
        scaffoldedFiles.Add(new ScaffoldedFile { Path = modelFileName, Code = modelCode });

        var entityTypeIds = new Dictionary<IEntityType, (string Variable, string Class)>();
        var modelBuilderCode = CreateModelBuilder(
            model, options.ModelNamespace, options.ContextType, entityTypeIds, nullable);
        var modelBuilderFileName = options.ContextType.ShortDisplayName() + ModelBuilderSuffix + FileExtension;
        scaffoldedFiles.Add(new ScaffoldedFile { Path = modelBuilderFileName, Code = modelBuilderCode });

        foreach (var (entityType, (_, @class)) in entityTypeIds)
        {
            var generatedCode = GenerateEntityType(
                entityType, options.ModelNamespace, @class, nullable);

            var entityTypeFileName = @class + FileExtension;
            scaffoldedFiles.Add(new ScaffoldedFile { Path = entityTypeFileName, Code = generatedCode });
        }

        return scaffoldedFiles;
    }

    private static string GenerateHeader(SortedSet<string> namespaces, string currentNamespace, bool nullable)
    {
        for (var i = 0; i < currentNamespace.Length; i++)
        {
            if (currentNamespace[i] != '.')
            {
                continue;
            }

            namespaces.Remove(currentNamespace[..i]);
        }

        namespaces.Remove(currentNamespace);

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        foreach (var @namespace in namespaces)
        {
            builder
                .Append("using ")
                .Append(@namespace)
                .AppendLine(";");
        }

        builder.AppendLine()
            .AppendLine("#pragma warning disable 219, 612, 618");

        builder.AppendLine(nullable ? "#nullable enable" : "#nullable disable");

        builder.AppendLine();

        return builder.ToString();
    }

    private string CreateModel(
        string @namespace,
        Type contextType,
        bool nullable)
    {
        var mainBuilder = new IndentedStringBuilder();
        var namespaces = new SortedSet<string>(new NamespaceComparer())
        {
            typeof(RuntimeModel).Namespace!, typeof(DbContextAttribute).Namespace!
        };

        AddNamespace(contextType, namespaces);

        if (!string.IsNullOrEmpty(@namespace))
        {
            mainBuilder
                .Append("namespace ").AppendLine(_code.Namespace(@namespace))
                .AppendLine("{");
            mainBuilder.Indent();
        }

        var className = _code.Identifier(contextType.ShortDisplayName()) + ModelSuffix;
        mainBuilder
            .Append("[DbContext(typeof(").Append(_code.Reference(contextType)).AppendLine("))]")
            .Append("public partial class ").Append(className).AppendLine(" : " + nameof(RuntimeModel))
            .AppendLine("{");

        using (mainBuilder.Indent())
        {
            mainBuilder
                .Append("static ").Append(className).Append("()")
                .AppendLines(
                    @"
{
    var model = new "
                    + className
                    + @"();
    model.Initialize();
    model.Customize();
    _instance = model;
}")
                .AppendLine()
                .Append("private static ").Append(className).AppendLine(" _instance;")
                .AppendLine("public static IModel Instance => _instance;")
                .AppendLine()
                .AppendLine("partial void Initialize();")
                .AppendLine()
                .AppendLine("partial void Customize();");
        }

        mainBuilder.AppendLine("}");

        if (!string.IsNullOrEmpty(@namespace))
        {
            mainBuilder.DecrementIndent();
            mainBuilder.AppendLine("}");
        }

        return GenerateHeader(namespaces, @namespace, nullable) + mainBuilder;
    }

    private string CreateModelBuilder(
        IModel model,
        string @namespace,
        Type contextType,
        Dictionary<IEntityType, (string Variable, string Class)> entityTypeIds,
        bool nullable)
    {
        var mainBuilder = new IndentedStringBuilder();
        var methodBuilder = new IndentedStringBuilder();
        var namespaces = new SortedSet<string>(new NamespaceComparer())
        {
            typeof(RuntimeModel).Namespace!, typeof(DbContextAttribute).Namespace!
        };

        if (!string.IsNullOrEmpty(@namespace))
        {
            mainBuilder
                .Append("namespace ").AppendLine(_code.Namespace(@namespace))
                .AppendLine("{");
            mainBuilder.Indent();
        }

        var className = _code.Identifier(contextType.ShortDisplayName()) + ModelSuffix;
        mainBuilder
            .Append("public partial class ").AppendLine(className)
            .AppendLine("{");

        using (mainBuilder.Indent())
        {
            mainBuilder
                .AppendLine("partial void Initialize()")
                .AppendLine("{");
            using (mainBuilder.Indent())
            {
                var entityTypes = model.GetEntityTypesInHierarchicalOrder();
                var variables = new HashSet<string>();

                var anyEntityTypes = false;
                foreach (var entityType in entityTypes)
                {
                    anyEntityTypes = true;
                    var variableName = _code.Identifier(entityType.ShortName(), variables, capitalize: false);

                    var firstChar = variableName[0] == '@' ? variableName[1] : variableName[0];
                    var entityClassName = firstChar == '_'
                        ? EntityTypeSuffix + variableName[1..]
                        : char.ToUpperInvariant(firstChar) + variableName[(variableName[0] == '@' ? 2 : 1)..] + EntityTypeSuffix;

                    entityTypeIds[entityType] = (variableName, entityClassName);

                    mainBuilder
                        .Append("var ")
                        .Append(variableName)
                        .Append(" = ")
                        .Append(entityClassName)
                        .Append(".Create(this");

                    if (entityType.BaseType != null)
                    {
                        mainBuilder
                            .Append(", ")
                            .Append(entityTypeIds[entityType.BaseType].Variable);
                    }

                    mainBuilder
                        .AppendLine(");");
                }

                if (anyEntityTypes)
                {
                    mainBuilder.AppendLine();
                }

                var anyForeignKeys = false;
                foreach (var (entityType, namePair) in entityTypeIds)
                {
                    var foreignKeyNumber = 1;
                    var (variableName, entityClassName) = namePair;
                    foreach (var foreignKey in entityType.GetDeclaredForeignKeys())
                    {
                        anyForeignKeys = true;
                        var principalVariable = entityTypeIds[foreignKey.PrincipalEntityType].Variable;

                        mainBuilder
                            .Append(entityClassName)
                            .Append(".CreateForeignKey")
                            .Append(foreignKeyNumber++.ToString())
                            .Append("(")
                            .Append(variableName)
                            .Append(", ")
                            .Append(principalVariable)
                            .AppendLine(");");
                    }
                }

                if (anyForeignKeys)
                {
                    mainBuilder.AppendLine();
                }

                var anySkipNavigations = false;
                foreach (var (entityType, namePair) in entityTypeIds)
                {
                    var navigationNumber = 1;
                    var (variableName, entityClassName) = namePair;
                    foreach (var navigation in entityType.GetDeclaredSkipNavigations())
                    {
                        anySkipNavigations = true;
                        var targetVariable = entityTypeIds[navigation.TargetEntityType].Variable;
                        var joinVariable = entityTypeIds[navigation.JoinEntityType].Variable;

                        mainBuilder
                            .Append(entityClassName)
                            .Append(".CreateSkipNavigation")
                            .Append(navigationNumber++.ToString())
                            .Append("(")
                            .Append(variableName)
                            .Append(", ")
                            .Append(targetVariable)
                            .Append(", ")
                            .Append(joinVariable)
                            .AppendLine(");");
                    }
                }

                if (anySkipNavigations)
                {
                    mainBuilder.AppendLine();
                }

                foreach (var (_, namePair) in entityTypeIds)
                {
                    var (variableName, entityClassName) = namePair;

                    mainBuilder
                        .Append(entityClassName)
                        .Append(".CreateAnnotations")
                        .Append("(")
                        .Append(variableName)
                        .AppendLine(");");
                }

                if (anyEntityTypes)
                {
                    mainBuilder.AppendLine();
                }

                var parameters = new CSharpRuntimeAnnotationCodeGeneratorParameters(
                    "this",
                    className,
                    mainBuilder,
                    methodBuilder,
                    namespaces,
                    variables,
                    nullable);

                foreach (var typeConfiguration in model.GetTypeMappingConfigurations())
                {
                    Create(typeConfiguration, parameters);
                }

                CreateAnnotations(model, _annotationCodeGenerator.Generate, parameters);
            }

            mainBuilder
                .AppendLine("}");

            var methods = methodBuilder.ToString();
            if (!string.IsNullOrEmpty(methods))
            {
                mainBuilder.AppendLines(methods);
            }
        }

        mainBuilder.AppendLine("}");

        if (!string.IsNullOrEmpty(@namespace))
        {
            mainBuilder.DecrementIndent();
            mainBuilder.AppendLine("}");
        }

        return GenerateHeader(namespaces, @namespace, nullable) + mainBuilder;
    }

    private void Create(
        ITypeMappingConfiguration typeConfiguration,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        var variableName = _code.Identifier("type", parameters.ScopeVariables, capitalize: false);

        var mainBuilder = parameters.MainBuilder;
        mainBuilder
            .Append("var ").Append(variableName).Append(" = ").Append(parameters.TargetName).AppendLine(".AddTypeMappingConfiguration(")
            .IncrementIndent()
            .Append(_code.Literal(typeConfiguration.ClrType));

        AddNamespace(typeConfiguration.ClrType, parameters.Namespaces);

        if (typeConfiguration.GetMaxLength() != null)
        {
            mainBuilder.AppendLine(",")
                .Append("maxLength: ")
                .Append(_code.Literal(typeConfiguration.GetMaxLength()));
        }

        if (typeConfiguration.IsUnicode() != null)
        {
            mainBuilder.AppendLine(",")
                .Append("unicode: ")
                .Append(_code.Literal(typeConfiguration.IsUnicode()));
        }

        if (typeConfiguration.GetPrecision() != null)
        {
            mainBuilder.AppendLine(",")
                .Append("precision: ")
                .Append(_code.Literal(typeConfiguration.GetPrecision()));
        }

        if (typeConfiguration.GetScale() != null)
        {
            mainBuilder.AppendLine(",")
                .Append("scale: ")
                .Append(_code.Literal(typeConfiguration.GetScale()));
        }

        var providerClrType = typeConfiguration.GetProviderClrType();
        if (providerClrType != null)
        {
            AddNamespace(providerClrType, parameters.Namespaces);

            mainBuilder.AppendLine(",")
                .Append("providerPropertyType: ")
                .Append(_code.Literal(providerClrType));
        }

        var valueConverterType = (Type?)typeConfiguration[CoreAnnotationNames.ValueConverterType];
        if (valueConverterType != null)
        {
            AddNamespace(valueConverterType, parameters.Namespaces);

            mainBuilder.AppendLine(",")
                .Append("valueConverter: new ")
                .Append(_code.Reference(valueConverterType))
                .Append("()");
        }

        mainBuilder
            .AppendLine(");")
            .DecrementIndent();

        CreateAnnotations(
            typeConfiguration,
            _annotationCodeGenerator.Generate,
            parameters with { TargetName = variableName });

        mainBuilder.AppendLine();
    }

    private string GenerateEntityType(IEntityType entityType, string @namespace, string className, bool nullable)
    {
        var mainBuilder = new IndentedStringBuilder();
        var methodBuilder = new IndentedStringBuilder();
        var namespaces = new SortedSet<string>(new NamespaceComparer())
        {
            typeof(BindingFlags).Namespace!, typeof(RuntimeEntityType).Namespace!
        };

        if (!string.IsNullOrEmpty(@namespace))
        {
            mainBuilder
                .Append("namespace ").AppendLine(_code.Namespace(@namespace))
                .AppendLine("{");
            mainBuilder.Indent();
        }

        mainBuilder
            .Append("internal partial class ").AppendLine(className)
            .AppendLine("{");
        using (mainBuilder.Indent())
        {
            CreateEntityType(entityType, mainBuilder, methodBuilder, namespaces, className, nullable);

            foreach (var complexProperty in entityType.GetDeclaredComplexProperties())
            {
                CreateComplexProperty(complexProperty, mainBuilder, methodBuilder, namespaces, className, nullable);
            }

            var foreignKeyNumber = 1;
            foreach (var foreignKey in entityType.GetDeclaredForeignKeys())
            {
                CreateForeignKey(foreignKey, foreignKeyNumber++, mainBuilder, methodBuilder, namespaces, className, nullable);
            }

            var navigationNumber = 1;
            foreach (var navigation in entityType.GetDeclaredSkipNavigations())
            {
                CreateSkipNavigation(navigation, navigationNumber++, mainBuilder, methodBuilder, namespaces, className, nullable);
            }

            CreateAnnotations(entityType, mainBuilder, methodBuilder, namespaces, className, nullable);

            var methods = methodBuilder.ToString();
            if (!string.IsNullOrEmpty(methods))
            {
                mainBuilder.AppendLines(methods);
            }
        }

        mainBuilder.AppendLine("}");

        if (!string.IsNullOrEmpty(@namespace))
        {
            mainBuilder.DecrementIndent();
            mainBuilder.AppendLine("}");
        }

        return GenerateHeader(namespaces, @namespace, nullable) + mainBuilder;
    }

    private void CreateEntityType(
        IEntityType entityType,
        IndentedStringBuilder mainBuilder,
        IndentedStringBuilder methodBuilder,
        SortedSet<string> namespaces,
        string className,
        bool nullable)
    {
        mainBuilder
            .Append("public static RuntimeEntityType Create")
            .Append("(RuntimeModel model, RuntimeEntityType");

        if (nullable)
        {
            mainBuilder
                .Append("?");
        }

        mainBuilder.AppendLine(" baseEntityType = null)")
            .AppendLine("{");

        using (mainBuilder.Indent())
        {
            const string entityTypeVariable = "runtimeEntityType";
            var variables = new HashSet<string>
            {
                "model",
                "baseEntityType",
                entityTypeVariable
            };

            var parameters = new CSharpRuntimeAnnotationCodeGeneratorParameters(
                entityTypeVariable,
                className,
                mainBuilder,
                methodBuilder,
                namespaces,
                variables,
                nullable);

            Create(entityType, parameters);

            var propertyVariables = new Dictionary<object, string>();
            foreach (var property in entityType.GetDeclaredProperties())
            {
                Create(property, propertyVariables, parameters);
            }

            foreach (var property in entityType.GetDeclaredServiceProperties())
            {
                Create(property, parameters);
            }

            foreach (var complexProperty in entityType.GetDeclaredComplexProperties())
            {
                mainBuilder
                    .Append(_code.Identifier(complexProperty.Name, capitalize: true))
                    .Append("ComplexProperty")
                    .Append(".Create")
                    .Append("(")
                    .Append(entityTypeVariable)
                    .AppendLine(");");
            }

            foreach (var key in entityType.GetDeclaredKeys())
            {
                Create(key, propertyVariables, parameters, nullable);
            }

            foreach (var index in entityType.GetDeclaredIndexes())
            {
                Create(index, propertyVariables, parameters, nullable);
            }

            foreach (var trigger in entityType.GetDeclaredTriggers())
            {
                Create(trigger, parameters);
            }

            mainBuilder
                .Append("return ")
                .Append(entityTypeVariable)
                .AppendLine(";");
        }

        mainBuilder
            .AppendLine("}");
    }

    private void Create(IEntityType entityType, CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        var runtimeEntityType = entityType as IRuntimeEntityType;
        if ((entityType.ConstructorBinding is not null
                && ((runtimeEntityType?.GetConstructorBindingConfigurationSource()).OverridesStrictly(ConfigurationSource.Convention)
                    || entityType.ConstructorBinding is FactoryMethodBinding))
            || (runtimeEntityType?.ServiceOnlyConstructorBinding is not null
                && (runtimeEntityType.GetServiceOnlyConstructorBindingConfigurationSource()
                        .OverridesStrictly(ConfigurationSource.Convention)
                    || runtimeEntityType.ServiceOnlyConstructorBinding is FactoryMethodBinding)))
        {
            throw new InvalidOperationException(
                DesignStrings.CompiledModelConstructorBinding(
                    entityType.ShortName(), "Customize()", parameters.ClassName));
        }

        if (entityType.GetQueryFilter() != null)
        {
            throw new InvalidOperationException(DesignStrings.CompiledModelQueryFilter(entityType.ShortName()));
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (entityType.GetDefiningQuery() != null)
        {
            // TODO: Move to InMemoryCSharpRuntimeAnnotationCodeGenerator, see #21624
            throw new InvalidOperationException(DesignStrings.CompiledModelDefiningQuery(entityType.ShortName()));
        }
#pragma warning restore CS0618 // Type or member is obsolete

        AddNamespace(entityType.ClrType, parameters.Namespaces);

        var mainBuilder = parameters.MainBuilder;
        mainBuilder
            .Append("var ")
            .Append(parameters.TargetName)
            .AppendLine(" = model.AddEntityType(")
            .IncrementIndent()
            .Append(_code.Literal(entityType.Name)).AppendLine(",")
            .Append(_code.Literal(entityType.ClrType)).AppendLine(",")
            .Append("baseEntityType");

        if (entityType.HasSharedClrType)
        {
            mainBuilder.AppendLine(",")
                .Append("sharedClrType: ")
                .Append(_code.Literal(entityType.HasSharedClrType));
        }

        var discriminatorProperty = entityType.GetDiscriminatorPropertyName();
        if (discriminatorProperty != null)
        {
            mainBuilder.AppendLine(",")
                .Append("discriminatorProperty: ")
                .Append(_code.Literal(discriminatorProperty));
        }

        var changeTrackingStrategy = entityType.GetChangeTrackingStrategy();
        if (changeTrackingStrategy != ChangeTrackingStrategy.Snapshot)
        {
            parameters.Namespaces.Add(typeof(ChangeTrackingStrategy).Namespace!);

            mainBuilder.AppendLine(",")
                .Append("changeTrackingStrategy: ")
                .Append(_code.Literal(changeTrackingStrategy));
        }

        var indexerPropertyInfo = entityType.FindIndexerPropertyInfo();
        if (indexerPropertyInfo != null)
        {
            mainBuilder.AppendLine(",")
                .Append("indexerPropertyInfo: RuntimeEntityType.FindIndexerProperty(")
                .Append(_code.Literal(entityType.ClrType))
                .Append(')');
        }

        if (entityType.IsPropertyBag)
        {
            mainBuilder.AppendLine(",")
                .Append("propertyBag: ")
                .Append(_code.Literal(true));
        }

        var discriminatorValue = entityType.GetDiscriminatorValue();
        if (discriminatorValue != null)
        {
            AddNamespace(discriminatorValue.GetType(), parameters.Namespaces);

            mainBuilder.AppendLine(",")
                .Append("discriminatorValue: ")
                .Append(_code.UnknownLiteral(discriminatorValue));
        }

        mainBuilder
            .AppendLine(");")
            .AppendLine()
            .DecrementIndent();
    }

    private void SetTypeBaseProperties(
        IRuntimeTypeBase typeBase,
        Dictionary<object, string> knownInstances,
        Dictionary<(MemberInfo, bool), string>? replacementMethods,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        var mainBuilder = parameters.MainBuilder;

        var originalValuesFactory = OriginalValuesFactoryFactory.Instance.CreateExpression(typeBase);
        mainBuilder
            .Append(parameters.TargetName).AppendLine(".SetOriginalValuesFactory(")
            .IncrementIndent()
            .AppendLines(_code.Expression(originalValuesFactory, knownInstances, null, parameters.Namespaces, out _), skipFinalNewline: true)
            .AppendLine(");")
            .DecrementIndent();

        var storeGeneratedValuesFactory = StoreGeneratedValuesFactoryFactory.Instance.CreateEmptyExpression(typeBase);
        mainBuilder
            .Append(parameters.TargetName).AppendLine(".SetStoreGeneratedValuesFactory(")
            .IncrementIndent()
            .AppendLines(_code.Expression(storeGeneratedValuesFactory, knownInstances, null, parameters.Namespaces, out _), skipFinalNewline: true)
            .AppendLine(");")
            .DecrementIndent();

        var temporaryValuesFactory = TemporaryValuesFactoryFactory.Instance.CreateExpression(typeBase);
        mainBuilder
            .Append(parameters.TargetName).AppendLine(".SetTemporaryValuesFactory(")
            .IncrementIndent()
            .AppendLines(_code.Expression(temporaryValuesFactory, knownInstances, null, parameters.Namespaces, out _), skipFinalNewline: true)
            .AppendLine(");")
            .DecrementIndent();

        var shadowValuesFactory = ShadowValuesFactoryFactory.Instance.CreateExpression(typeBase);
        mainBuilder
            .Append(parameters.TargetName).AppendLine(".SetShadowValuesFactory(")
            .IncrementIndent()
            .AppendLines(_code.Expression(shadowValuesFactory, knownInstances, null, parameters.Namespaces, out _), skipFinalNewline: true)
            .AppendLine(");")
            .DecrementIndent();

        var emptyShadowValuesFactory = EmptyShadowValuesFactoryFactory.Instance.CreateEmptyExpression(typeBase);
        mainBuilder
            .Append(parameters.TargetName).AppendLine(".SetEmptyShadowValuesFactory(")
            .IncrementIndent()
            .AppendLines(_code.Expression(emptyShadowValuesFactory, knownInstances, null, parameters.Namespaces, out _), skipFinalNewline: true)
            .AppendLine(");")
            .DecrementIndent();

        AddNamespace(typeof(PropertyCounts), parameters.Namespaces);
        var counts = typeBase.Counts;
        mainBuilder
            .Append(parameters.TargetName).AppendLine(".Counts = new PropertyCounts(")
            .IncrementIndent()
            .Append("propertyCount: ").Append(_code.Literal(counts.PropertyCount)).AppendLine(",")
            .Append("navigationCount: ").Append(_code.Literal(counts.NavigationCount)).AppendLine(",")
            .Append("complexPropertyCount: ").Append(_code.Literal(counts.ComplexPropertyCount)).AppendLine(",")
            .Append("originalValueCount: ").Append(_code.Literal(counts.OriginalValueCount)).AppendLine(",")
            .Append("shadowCount: ").Append(_code.Literal(counts.ShadowCount)).AppendLine(",")
            .Append("relationshipCount: ").Append(_code.Literal(counts.RelationshipCount)).AppendLine(",")
            .Append("storeGeneratedCount: ").Append(_code.Literal(counts.StoreGeneratedCount)).AppendLine(");")
            .DecrementIndent();
    }

    private void Create(
        IProperty property,
        Dictionary<object, string> propertyVariables,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        var variableName = _code.Identifier(property.Name, parameters.ScopeVariables, capitalize: false);
        propertyVariables[property] = variableName;

        Create(property, variableName, propertyVariables, parameters);

        CreateAnnotations(
            property,
            _annotationCodeGenerator.Generate,
            parameters with { TargetName = variableName });

        parameters.MainBuilder.AppendLine();
    }

    private void Create(
        IProperty property,
        string variableName,
        Dictionary<object, string> propertyVariables,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        var valueGeneratorFactoryType = (Type?)property[CoreAnnotationNames.ValueGeneratorFactoryType];
        if (valueGeneratorFactoryType == null
            && property.GetValueGeneratorFactory() != null)
        {
            throw new InvalidOperationException(
                DesignStrings.CompiledModelValueGenerator(
                    property.DeclaringType.ShortName(), property.Name, nameof(PropertyBuilder.HasValueGeneratorFactory)));
        }

        var mainBuilder = parameters.MainBuilder;
        mainBuilder
            .Append("var ").Append(variableName).Append(" = ").Append(parameters.TargetName).AppendLine(".AddProperty(")
            .IncrementIndent()
            .Append(_code.Literal(property.Name));

        PropertyBaseParameters(property, parameters);

        if (property.IsNullable)
        {
            mainBuilder.AppendLine(",")
                .Append("nullable: ")
                .Append(_code.Literal(true));
        }

        if (property.IsConcurrencyToken)
        {
            mainBuilder.AppendLine(",")
                .Append("concurrencyToken: ")
                .Append(_code.Literal(true));
        }

        if (property.ValueGenerated != ValueGenerated.Never)
        {
            mainBuilder.AppendLine(",")
                .Append("valueGenerated: ")
                .Append(_code.Literal(property.ValueGenerated));
        }

        if (property.GetBeforeSaveBehavior() != PropertySaveBehavior.Save)
        {
            mainBuilder.AppendLine(",")
                .Append("beforeSaveBehavior: ")
                .Append(_code.Literal(property.GetBeforeSaveBehavior()));
        }

        if (property.GetAfterSaveBehavior() != PropertySaveBehavior.Save)
        {
            mainBuilder.AppendLine(",")
                .Append("afterSaveBehavior: ")
                .Append(_code.Literal(property.GetAfterSaveBehavior()));
        }

        if (property.GetMaxLength() != null)
        {
            mainBuilder.AppendLine(",")
                .Append("maxLength: ")
                .Append(_code.Literal(property.GetMaxLength()));
        }

        if (property.IsUnicode() != null)
        {
            mainBuilder.AppendLine(",")
                .Append("unicode: ")
                .Append(_code.Literal(property.IsUnicode()));
        }

        if (property.GetPrecision() != null)
        {
            mainBuilder.AppendLine(",")
                .Append("precision: ")
                .Append(_code.Literal(property.GetPrecision()));
        }

        if (property.GetScale() != null)
        {
            mainBuilder.AppendLine(",")
                .Append("scale: ")
                .Append(_code.Literal(property.GetScale()));
        }

        var providerClrType = property.GetProviderClrType();
        if (providerClrType != null)
        {
            AddNamespace(providerClrType, parameters.Namespaces);

            mainBuilder.AppendLine(",")
                .Append("providerPropertyType: ")
                .Append(_code.Literal(providerClrType));
        }

        if (valueGeneratorFactoryType != null)
        {
            AddNamespace(valueGeneratorFactoryType, parameters.Namespaces);

            mainBuilder.AppendLine(",")
                .Append("valueGeneratorFactory: new ")
                .Append(_code.Reference(valueGeneratorFactoryType))
                .Append("().Create");
        }

        var valueConverterType = GetValueConverterType(property);
        if (valueConverterType != null)
        {
            AddNamespace(valueConverterType, parameters.Namespaces);

            mainBuilder.AppendLine(",")
                .Append("valueConverter: new ")
                .Append(_code.Reference(valueConverterType))
                .Append("()");
        }

        var valueComparerType = (Type?)property[CoreAnnotationNames.ValueComparerType];
        if (valueComparerType != null)
        {
            AddNamespace(valueComparerType, parameters.Namespaces);

            mainBuilder.AppendLine(",")
                .Append("valueComparer: new ")
                .Append(_code.Reference(valueComparerType))
                .Append("()");
        }

        var providerValueComparerType = (Type?)property[CoreAnnotationNames.ProviderValueComparerType];
        if (providerValueComparerType != null)
        {
            AddNamespace(providerValueComparerType, parameters.Namespaces);

            mainBuilder.AppendLine(",")
                .Append("providerValueComparer: new ")
                .Append(_code.Reference(providerValueComparerType))
                .Append("()");
        }

        var sentinel = property.Sentinel;
        if (sentinel != null)
        {
            mainBuilder.AppendLine(",")
                .Append("sentinel: ")
                .Append(_code.UnknownLiteral(sentinel));
        }

        var jsonValueReaderWriterType = (Type?)property[CoreAnnotationNames.JsonValueReaderWriterType];
        if (jsonValueReaderWriterType != null)
        {
            mainBuilder.AppendLine(",")
                .Append("jsonValueReaderWriter: ");

            CSharpRuntimeAnnotationCodeGenerator.CreateJsonValueReaderWriter(jsonValueReaderWriterType, parameters, _code);
        }

        mainBuilder
            .AppendLine(");")
            .DecrementIndent();

        var propertyParameters = parameters with { TargetName = variableName };

        SetPropertyBaseProperties(property, propertyParameters);

        mainBuilder.Append(variableName).Append(".TypeMapping = ");
        _annotationCodeGenerator.Create(property.GetTypeMapping(), property, propertyParameters);
        mainBuilder.AppendLine(";");

        var currentComparerType = CurrentValueComparerFactory.Instance.GetComparerType(property);

        mainBuilder
            .Append(variableName).Append(".SetCurrentValueComparer(new ")
            .Append(_code.Reference(currentComparerType))
            .AppendLine($"({variableName}));");
    }

    private (Dictionary<object, string> KnownInstances, Dictionary<(MemberInfo, bool), string>? ReplacementMethods)
        SetPropertyBaseProperties(
        IPropertyBase property,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        var variableName = parameters.TargetName;
        var mainBuilder = parameters.MainBuilder;
        var knownInstances = new Dictionary<object, string> { { property, variableName } };
        Dictionary<(MemberInfo, bool), string>? replacementMethods = null;
        if (!property.IsShadowProperty())
        {
            replacementMethods = CreatePrivateAccessors(property, parameters);

            ClrPropertyGetterFactory.Instance.Create(property, out var getterExpression, out var hasSentinelExpression);

            mainBuilder
                .Append(variableName).AppendLine(".SetGetter(")
                .IncrementIndent()
                .AppendLines(_code.Expression(getterExpression, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(",")
                .AppendLines(_code.Expression(hasSentinelExpression, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(");")
                .DecrementIndent();

            ClrPropertySetterFactory.Instance.Create(property, out var setterExpression);

            mainBuilder
                .Append(variableName).AppendLine(".SetSetter(")
                .IncrementIndent()
                .AppendLines(_code.Expression(setterExpression, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(");")
                .DecrementIndent();

            ClrPropertyMaterializationSetterFactory.Instance.Create(property, out var materializationSetterExpression);

            mainBuilder
                .Append(variableName).AppendLine(".SetMaterializationSetter(")
                .IncrementIndent()
                .AppendLines(_code.Expression(materializationSetterExpression, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(");")
                .DecrementIndent();

            PropertyAccessorsFactory.Instance.Create(property,
                out var currentValueGetter,
                out var preStoreGeneratedCurrentValueGetter,
                out var originalValueGetter,
                out var relationshipSnapshotGetter,
                out var valueBufferGetter);

            mainBuilder
                .Append(variableName).AppendLine(".SetAccessors(")
                .IncrementIndent()
                .AppendLines(_code.Expression(currentValueGetter, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(",")
                .AppendLines(_code.Expression(preStoreGeneratedCurrentValueGetter, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(",")
                .AppendLines(originalValueGetter == null
                    ? "null"
                    : _code.Expression(originalValueGetter, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(",")
                .AppendLines(_code.Expression(relationshipSnapshotGetter, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(",")
                .AppendLines(valueBufferGetter == null
                    ? "null"
                    : _code.Expression(valueBufferGetter, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(");")
                .DecrementIndent();
        }

        AddNamespace(typeof(PropertyIndexes), parameters.Namespaces);
        var propertyIndexes = ((IRuntimePropertyBase)property).PropertyIndexes;
        mainBuilder
            .Append(variableName).AppendLine(".SetPropertyIndexes(new(")
            .IncrementIndent()
            .Append("index: ").Append(_code.Literal(propertyIndexes.Index)).AppendLine(",")
            .Append("originalValueIndex: ").Append(_code.Literal(propertyIndexes.OriginalValueIndex)).AppendLine(",")
            .Append("shadowIndex: ").Append(_code.Literal(propertyIndexes.ShadowIndex)).AppendLine(",")
            .Append("relationshipIndex: ").Append(_code.Literal(propertyIndexes.RelationshipIndex)).AppendLine(",")
            .Append("storeGenerationIndex: ").Append(_code.Literal(propertyIndexes.StoreGenerationIndex)).AppendLine("));")
            .DecrementIndent();

        return (knownInstances, replacementMethods);
    }

    private Dictionary<(MemberInfo, bool), string>? CreatePrivateAccessors(
        IPropertyBase property,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters,
        bool create = true)
    {
        Dictionary<(MemberInfo, bool), string>? replacementMethods = null;
        if (!property.IsShadowProperty()
            && !property.IsIndexerProperty())
        {
            replacementMethods = CreatePrivateAccessor(property, forMaterialization: false, forSet: false, create, replacementMethods, parameters);
            replacementMethods = CreatePrivateAccessor(property, forMaterialization: false, forSet: true, create, replacementMethods, parameters);
            replacementMethods = CreatePrivateAccessor(property, forMaterialization: true, forSet: false, create, replacementMethods, parameters);
            replacementMethods = CreatePrivateAccessor(property, forMaterialization: true, forSet: true, create, replacementMethods, parameters);
        }

        return replacementMethods;
    }

    private Dictionary<(MemberInfo, bool), string>? CreatePrivateAccessor(
        IPropertyBase property,
        bool forMaterialization,
        bool forSet,
        bool create,
        Dictionary<(MemberInfo, bool), string>? replacementMethods,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        var member = property.GetMemberInfo(forMaterialization, forSet);
        if (member is not FieldInfo field
            || field.IsPublic)
        {
            return replacementMethods;
        }

        if (replacementMethods?.ContainsKey((member, forSet)) != true)
        {
            replacementMethods ??= new();

            var methodName = (forSet ? "Write" : "Read") + property.Name;
            if (create)
            {
                var methodBuilder = parameters.MethodBuilder;
                if (!replacementMethods.ContainsKey((member, !forSet)))
                {
                    AddNamespace(typeof(UnsafeAccessorAttribute), parameters.Namespaces);
                    methodBuilder
                        .AppendLine()
                        .AppendLine($"[UnsafeAccessor(UnsafeAccessorKind.Field, Name = \"{field.Name}\")]")
                        .Append($"extern static ref {_code.Reference(member.GetMemberType())} Get{property.Name}(")
                        .AppendLine($"{_code.Reference(property.DeclaringType.ClrType)} @this);");
                }

                // Expression trees cannot contain calls to methods that have a ref return, so we need to wrap the call
                // This approach will not work if the declaring type of the member is a value type

                methodBuilder
                    .AppendLine()
                    .Append($"private static {(forSet ? "void" : _code.Reference(member.GetMemberType()))} {methodName}(")
                    .Append($"{_code.Reference(property.DeclaringType.ClrType)} @this");
                if (forSet)
                {
                    methodBuilder
                        .Append($", {_code.Reference(member.GetMemberType())} value");
                }

                methodBuilder
                    .AppendLine(")")
                    .IncrementIndent()
                    .Append($"=> Get{property.Name}(@this)");

                if (forSet)
                {
                    methodBuilder
                        .Append(" = value");
                }

                methodBuilder
                    .AppendLine(";")
                    .DecrementIndent();
            }

            replacementMethods.Add((field, forSet), methodName);
        }

        return replacementMethods;
    }

    private static Type? GetValueConverterType(IProperty property)
    {
        var annotation = property.FindAnnotation(CoreAnnotationNames.ValueConverterType);
        if (annotation != null)
        {
            return (Type?)annotation.Value;
        }

        var principalProperty = property;
        var i = 0;
        for (; i < ForeignKey.LongestFkChainAllowedLength; i++)
        {
            IProperty? nextProperty = null;
            foreach (var foreignKey in principalProperty.GetContainingForeignKeys())
            {
                for (var propertyIndex = 0; propertyIndex < foreignKey.Properties.Count; propertyIndex++)
                {
                    if (principalProperty == foreignKey.Properties[propertyIndex])
                    {
                        var newPrincipalProperty = foreignKey.PrincipalKey.Properties[propertyIndex];
                        if (newPrincipalProperty == property
                            || newPrincipalProperty == principalProperty)
                        {
                            break;
                        }

                        annotation = newPrincipalProperty.FindAnnotation(CoreAnnotationNames.ValueConverterType);
                        if (annotation != null)
                        {
                            return (Type?)annotation.Value;
                        }

                        nextProperty = newPrincipalProperty;
                    }
                }
            }

            if (nextProperty == null)
            {
                break;
            }

            principalProperty = nextProperty;
        }

        return i == ForeignKey.LongestFkChainAllowedLength
            ? throw new InvalidOperationException(CoreStrings.RelationshipCycle(
                property.DeclaringType.DisplayName(), property.Name, "ValueConverterType"))
            : null;
    }

    private void PropertyBaseParameters(
        IPropertyBase property,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters,
        bool skipType = false)
    {
        var mainBuilder = parameters.MainBuilder;

        if (!skipType)
        {
            AddNamespace(property.ClrType, parameters.Namespaces);
            mainBuilder.AppendLine(",")
                .Append(_code.Literal(property.ClrType));
        }

        var propertyInfo = property.PropertyInfo;
        if (propertyInfo != null)
        {
            AddNamespace(propertyInfo.DeclaringType!, parameters.Namespaces);

            mainBuilder.AppendLine(",")
                .Append("propertyInfo: ");

            if (property.IsIndexerProperty())
            {
                mainBuilder
                    .Append(parameters.TargetName)
                    .Append(".FindIndexerPropertyInfo()");
            }
            else
            {
                mainBuilder
                    .Append(_code.Literal(propertyInfo.DeclaringType!))
                    .Append(".GetProperty(")
                    .Append(_code.Literal(propertyInfo.Name))
                    .Append(", ")
                    .Append(propertyInfo.GetAccessors().Length != 0 ? "BindingFlags.Public" : "BindingFlags.NonPublic")
                    .Append(propertyInfo.IsStatic() ? " | BindingFlags.Static" : " | BindingFlags.Instance")
                    .Append(" | BindingFlags.DeclaredOnly)");
            }
        }

        var fieldInfo = property.FieldInfo;
        if (fieldInfo != null)
        {
            AddNamespace(fieldInfo.DeclaringType!, parameters.Namespaces);

            mainBuilder.AppendLine(",")
                .Append("fieldInfo: ")
                .Append(_code.Literal(fieldInfo.DeclaringType!))
                .Append(".GetField(")
                .Append(_code.Literal(fieldInfo.Name))
                .Append(", ")
                .Append(fieldInfo.IsPublic ? "BindingFlags.Public" : "BindingFlags.NonPublic")
                .Append(fieldInfo.IsStatic ? " | BindingFlags.Static" : " | BindingFlags.Instance")
                .Append(" | BindingFlags.DeclaredOnly)");
        }

        var propertyAccessMode = property.GetPropertyAccessMode();
        if (propertyAccessMode != Model.DefaultPropertyAccessMode)
        {
            parameters.Namespaces.Add(typeof(PropertyAccessMode).Namespace!);

            mainBuilder.AppendLine(",")
                .Append("propertyAccessMode: ")
                .Append(_code.Literal(propertyAccessMode));
        }
    }

    private void FindProperties(
        string entityTypeVariable,
        IEnumerable<IProperty> properties,
        IndentedStringBuilder mainBuilder,
        bool nullable,
        Dictionary<object, string>? propertyVariables = null)
    {
        mainBuilder.Append("new[] { ");
        var first = true;
        foreach (var property in properties)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                mainBuilder.Append(", ");
            }

            if (propertyVariables != null
                && propertyVariables.TryGetValue(property, out var propertyVariable))
            {
                mainBuilder.Append(propertyVariable);
            }
            else
            {
                mainBuilder
                    .Append(entityTypeVariable)
                    .Append(".FindProperty(")
                    .Append(_code.Literal(property.Name))
                    .Append(')');

                if (nullable)
                {
                    mainBuilder
                        .Append('!');
                }
            }
        }

        mainBuilder.Append(" }");
    }

    private void Create(
        IServiceProperty property,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        var variableName = _code.Identifier(property.Name, parameters.ScopeVariables, capitalize: false);

        var mainBuilder = parameters.MainBuilder;
        mainBuilder
            .Append("var ").Append(variableName).Append(" = ").Append(parameters.TargetName).AppendLine(".AddServiceProperty(")
            .IncrementIndent()
            .Append(_code.Literal(property.Name));

        PropertyBaseParameters(property, parameters, skipType: true);

        AddNamespace(property.ClrType, parameters.Namespaces);
        mainBuilder
            .AppendLine(",")
            .Append("serviceType: typeof(" + _code.Reference(property.ClrType) + ")");

        mainBuilder
            .AppendLine(");")
            .DecrementIndent();

        var propertyParameters = parameters with { TargetName = variableName };

        SetPropertyBaseProperties(property, propertyParameters);

        CreateAnnotations(property, _annotationCodeGenerator.Generate, propertyParameters);

        mainBuilder.AppendLine();
    }

    private void Create(
        IKey key,
        Dictionary<object, string> propertyVariables,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters,
        bool nullable)
    {
        var variableName = _code.Identifier("key", parameters.ScopeVariables);

        var mainBuilder = parameters.MainBuilder;
        mainBuilder
            .Append("var ").Append(variableName).Append(" = ").Append(parameters.TargetName).AppendLine(".AddKey(")
            .IncrementIndent();
        FindProperties(parameters.TargetName, key.Properties, mainBuilder, nullable, propertyVariables);
        mainBuilder
            .AppendLine(");")
            .DecrementIndent();

        if (key.IsPrimaryKey())
        {
            mainBuilder
                .Append(parameters.TargetName)
                .Append(".SetPrimaryKey(")
                .Append(variableName)
                .AppendLine(");");
        }

        CreateAnnotations(
            key,
            _annotationCodeGenerator.Generate,
            parameters with { TargetName = variableName });

        mainBuilder.AppendLine();
    }

    private void Create(
        IIndex index,
        Dictionary<object, string> propertyVariables,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters,
        bool nullable)
    {
        var variableName = _code.Identifier(index.Name ?? "index", parameters.ScopeVariables, capitalize: false);

        var mainBuilder = parameters.MainBuilder;
        mainBuilder
            .Append("var ").Append(variableName).Append(" = ").Append(parameters.TargetName).AppendLine(".AddIndex(")
            .IncrementIndent();

        FindProperties(parameters.TargetName, index.Properties, mainBuilder, nullable, propertyVariables);

        if (index.Name != null)
        {
            mainBuilder.AppendLine(",")
                .Append("name: ")
                .Append(_code.Literal(index.Name));
        }

        if (index.IsUnique)
        {
            mainBuilder.AppendLine(",")
                .Append("unique: ")
                .Append(_code.Literal(true));
        }

        mainBuilder
            .AppendLine(");")
            .DecrementIndent();

        CreateAnnotations(
            index,
            _annotationCodeGenerator.Generate,
            parameters with { TargetName = variableName });

        mainBuilder.AppendLine();
    }

    private void CreateComplexProperty(
        IComplexProperty complexProperty,
        IndentedStringBuilder mainBuilder,
        IndentedStringBuilder methodBuilder,
        SortedSet<string> namespaces,
        string topClassName,
        bool nullable)
    {
        mainBuilder
            .AppendLine()
            .Append("private static class ")
            .Append(_code.Identifier(complexProperty.Name, capitalize: true))
            .AppendLine("ComplexProperty")
            .AppendLine("{");

        methodBuilder = new IndentedStringBuilder();

        var complexType = complexProperty.ComplexType;
        using (mainBuilder.Indent())
        {
            var declaringTypeVariable = "declaringType";
            mainBuilder
                .Append("public static RuntimeComplexProperty Create(")
                .Append(complexProperty.DeclaringType is IEntityType ? "RuntimeEntityType " : "RuntimeComplexType ")
                .Append(declaringTypeVariable)
                .AppendLine(")")
                .AppendLine("{");

            using (mainBuilder.Indent())
            {
                const string complexPropertyVariable = "complexProperty";
                const string complexTypeVariable = "complexType";
                var variables = new HashSet<string>
                {
                    declaringTypeVariable,
                    complexPropertyVariable,
                    complexTypeVariable
                };

                mainBuilder
                    .Append("var ").Append(complexPropertyVariable).Append(" = ")
                    .Append(declaringTypeVariable).Append(".AddComplexProperty(")
                    .IncrementIndent()
                    .Append(_code.Literal(complexProperty.Name))
                    .AppendLine(",")
                    .Append(_code.Literal(complexProperty.ClrType))
                    .AppendLine(",")
                    .Append(_code.Literal(complexType.Name))
                    .AppendLine(",")
                    .Append(_code.Literal(complexType.ClrType));

                AddNamespace(complexProperty.ClrType, namespaces);
                AddNamespace(complexType.ClrType, namespaces);                

                var parameters = new CSharpRuntimeAnnotationCodeGeneratorParameters(
                    declaringTypeVariable,
                    topClassName,
                    mainBuilder,
                    methodBuilder,
                    namespaces,
                    variables,
                    nullable);

                PropertyBaseParameters(complexProperty, parameters, skipType: true);

                if (complexProperty.IsNullable)
                {
                    mainBuilder.AppendLine(",")
                        .Append("nullable: ")
                        .Append(_code.Literal(true));
                }

                if (complexProperty.IsCollection)
                {
                    mainBuilder.AppendLine(",")
                        .Append("collection: ")
                        .Append(_code.Literal(true));
                }

                var changeTrackingStrategy = complexType.GetChangeTrackingStrategy();
                if (changeTrackingStrategy != ChangeTrackingStrategy.Snapshot)
                {
                    namespaces.Add(typeof(ChangeTrackingStrategy).Namespace!);

                    mainBuilder.AppendLine(",")
                        .Append("changeTrackingStrategy: ")
                        .Append(_code.Literal(changeTrackingStrategy));
                }

                var indexerPropertyInfo = complexType.FindIndexerPropertyInfo();
                if (indexerPropertyInfo != null)
                {
                    mainBuilder.AppendLine(",")
                        .Append("indexerPropertyInfo: RuntimeEntityType.FindIndexerProperty(")
                        .Append(_code.Literal(complexType.ClrType))
                        .Append(")");
                }

                if (complexType.IsPropertyBag)
                {
                    mainBuilder.AppendLine(",")
                        .Append("propertyBag: ")
                        .Append(_code.Literal(true));
                }

                mainBuilder
                    .AppendLine(");")
                    .AppendLine()
                    .DecrementIndent();

                mainBuilder
                    .Append("var ").Append(complexTypeVariable).Append(" = ")
                    .Append(complexPropertyVariable).AppendLine(".ComplexType;");

                var complexTypeParameters = parameters with { TargetName = complexTypeVariable };
                var complexPropertyParameters = parameters with { TargetName = complexPropertyVariable };

                var propertyVariables = new Dictionary<object, string>();
                Dictionary<(MemberInfo, bool), string>? replacementMethods = null;
                foreach (var property in complexType.GetProperties())
                {
                    Create(property, propertyVariables, complexTypeParameters);
                    replacementMethods = CreatePrivateAccessors(property, complexTypeParameters, false);
                }

                foreach (var nestedComplexProperty in complexType.GetComplexProperties())
                {
                    mainBuilder
                        .Append(_code.Identifier(nestedComplexProperty.Name, capitalize: true))
                        .Append("ComplexProperty")
                        .Append(".Create")
                        .Append("(")
                        .Append(complexTypeVariable)
                        .AppendLine(");");
                }

                SetTypeBaseProperties((IRuntimeTypeBase)complexType, propertyVariables, replacementMethods, complexTypeParameters);
                SetPropertyBaseProperties(complexProperty, complexPropertyParameters);

                CreateAnnotations(complexType, _annotationCodeGenerator.Generate, complexTypeParameters);
                CreateAnnotations(complexProperty, _annotationCodeGenerator.Generate, complexPropertyParameters);

                mainBuilder
                    .Append("return ")
                    .Append(complexPropertyVariable)
                    .AppendLine(";");
            }

            mainBuilder.AppendLine("}");
        }

        using (mainBuilder.Indent())
        {
            foreach (var nestedComplexProperty in complexType.GetComplexProperties())
            {
                CreateComplexProperty(nestedComplexProperty, mainBuilder, methodBuilder, namespaces, topClassName, nullable);
            }
        }

        var methods = methodBuilder.ToString();
        if (!string.IsNullOrEmpty(methods))
        {
            mainBuilder.AppendLines(methods);
        }

        mainBuilder.AppendLine("}");
    }

    private void CreateForeignKey(
        IForeignKey foreignKey,
        int foreignKeyNumber,
        IndentedStringBuilder mainBuilder,
        IndentedStringBuilder methodBuilder,
        SortedSet<string> namespaces,
        string className,
        bool nullable)
    {
        const string declaringEntityType = "declaringEntityType";
        const string principalEntityType = "principalEntityType";
        mainBuilder.AppendLine()
            .Append("public static RuntimeForeignKey CreateForeignKey").Append(foreignKeyNumber.ToString())
            .Append("(RuntimeEntityType ").Append(declaringEntityType)
            .Append(", RuntimeEntityType ").Append(principalEntityType).AppendLine(")")
            .AppendLine("{");

        using (mainBuilder.Indent())
        {
            const string foreignKeyVariable = "runtimeForeignKey";
            var variables = new HashSet<string>
            {
                declaringEntityType,
                principalEntityType,
                foreignKeyVariable
            };

            mainBuilder
                .Append("var ").Append(foreignKeyVariable).Append(" = ")
                .Append(declaringEntityType).Append(".AddForeignKey(").IncrementIndent();
            FindProperties(declaringEntityType, foreignKey.Properties, mainBuilder, nullable);

            mainBuilder.AppendLine(",")
                .Append(principalEntityType).Append(".FindKey(");
            FindProperties(principalEntityType, foreignKey.PrincipalKey.Properties, mainBuilder, nullable);
            mainBuilder.Append(")");
            if (nullable)
            {
                mainBuilder.Append("!");
            }

            mainBuilder.AppendLine(",")
                .Append(principalEntityType);

            if (foreignKey.DeleteBehavior != ForeignKey.DefaultDeleteBehavior)
            {
                namespaces.Add(typeof(DeleteBehavior).Namespace!);

                mainBuilder.AppendLine(",")
                    .Append("deleteBehavior: ")
                    .Append(_code.Literal(foreignKey.DeleteBehavior));
            }

            if (foreignKey.IsUnique)
            {
                mainBuilder.AppendLine(",")
                    .Append("unique: ")
                    .Append(_code.Literal(true));
            }

            if (foreignKey.IsRequired)
            {
                mainBuilder.AppendLine(",")
                    .Append("required: ")
                    .Append(_code.Literal(true));
            }

            if (foreignKey.IsRequiredDependent)
            {
                mainBuilder.AppendLine(",")
                    .Append("requiredDependent: ")
                    .Append(_code.Literal(true));
            }

            if (foreignKey.IsOwnership)
            {
                mainBuilder.AppendLine(",")
                    .Append("ownership: ")
                    .Append(_code.Literal(true));
            }

            mainBuilder
                .AppendLine(");")
                .AppendLine()
                .DecrementIndent();

            var parameters = new CSharpRuntimeAnnotationCodeGeneratorParameters(
                foreignKeyVariable,
                className,
                mainBuilder,
                methodBuilder,
                namespaces,
                variables,
                nullable);

            var navigation = foreignKey.DependentToPrincipal;
            if (navigation != null)
            {
                Create(navigation, foreignKeyVariable, parameters with { TargetName = declaringEntityType });
            }

            navigation = foreignKey.PrincipalToDependent;
            if (navigation != null)
            {
                Create(navigation, foreignKeyVariable, parameters with { TargetName = principalEntityType });
            }

            CreateAnnotations(
                foreignKey,
                _annotationCodeGenerator.Generate,
                parameters);

            mainBuilder
                .Append("return ")
                .Append(foreignKeyVariable)
                .AppendLine(";");
        }

        mainBuilder
            .AppendLine("}");
    }

    private void Create(
        INavigation navigation,
        string foreignKeyVariable,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        var mainBuilder = parameters.MainBuilder;
        var navigationVariable = _code.Identifier(navigation.Name, parameters.ScopeVariables, capitalize: false);
        mainBuilder
            .Append("var ").Append(navigationVariable).Append(" = ")
            .Append(parameters.TargetName).Append(".AddNavigation(").IncrementIndent()
            .Append(_code.Literal(navigation.Name)).AppendLine(",")
            .Append(foreignKeyVariable).AppendLine(",")
            .Append("onDependent: ").Append(_code.Literal(navigation.IsOnDependent));

        PropertyBaseParameters(navigation, parameters);

        if (navigation.IsEagerLoaded)
        {
            mainBuilder.AppendLine(",")
                .Append("eagerLoaded: ").Append(_code.Literal(true));
        }

        if (!navigation.LazyLoadingEnabled)
        {
            mainBuilder.AppendLine(",")
                .Append("lazyLoadingEnabled: ").Append(_code.Literal(false));
        }

        mainBuilder
            .AppendLine(");")
            .AppendLine()
            .DecrementIndent();

        var navigationParameters = parameters with { TargetName = navigationVariable };

        var (knownInstances, replacementMethods) = SetPropertyBaseProperties(navigation, navigationParameters);

        SetNavigationBaseProperties(navigation, knownInstances, replacementMethods, navigationParameters);

        CreateAnnotations(navigation, _annotationCodeGenerator.Generate, navigationParameters);
    }

    private void SetNavigationBaseProperties(
        INavigationBase navigation,
        Dictionary<object, string> knownInstances,
        Dictionary<(MemberInfo, bool), string>? replacementMethods,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        if (navigation.IsCollection)
        {
            var mainBuilder = parameters.MainBuilder;
            ClrCollectionAccessorFactory.Instance.Create(
                navigation,
                out var entityType,
                out var propertyType,
                out var elementType,
                out var getCollection,
                out var setCollection,
                out var setCollectionForMaterialization,
                out var createAndSetCollection,
                out var createCollection);

            AddNamespace(propertyType, parameters.Namespaces);
            mainBuilder
                .Append(parameters.TargetName)
                .AppendLine($".SetCollectionAccessor<{_code.Reference(entityType)}, {_code.Reference(propertyType)}, {_code.Reference(elementType)}>(")
                .IncrementIndent()
                .AppendLines(getCollection == null
                    ? "null"
                    : _code.Expression(getCollection, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(",")
                .AppendLines(setCollection == null
                    ? "null"
                    : _code.Expression(setCollection, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(",")
                .AppendLines(setCollectionForMaterialization == null
                    ? "null"
                    : _code.Expression(setCollectionForMaterialization, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(",")
                .AppendLines(createAndSetCollection == null
                    ? "null"
                    : _code.Expression(createAndSetCollection, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(",")
                .AppendLines(createCollection == null
                    ? "null"
                    : _code.Expression(createCollection, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(");")
                .DecrementIndent();
        }
    }

    private void CreateSkipNavigation(
        ISkipNavigation navigation,
        int navigationNumber,
        IndentedStringBuilder mainBuilder,
        IndentedStringBuilder methodBuilder,
        SortedSet<string> namespaces,
        string className,
        bool nullable)
    {
        const string declaringEntityType = "declaringEntityType";
        const string targetEntityType = "targetEntityType";
        const string joinEntityType = "joinEntityType";
        mainBuilder.AppendLine()
            .Append("public static RuntimeSkipNavigation CreateSkipNavigation")
            .Append(navigationNumber.ToString())
            .Append("(RuntimeEntityType ").Append(declaringEntityType)
            .Append(", RuntimeEntityType ").Append(targetEntityType)
            .Append(", RuntimeEntityType ").Append(joinEntityType).AppendLine(")")
            .AppendLine("{");

        using (mainBuilder.Indent())
        {
            const string navigationVariable = "skipNavigation";
            var variables = new HashSet<string>
            {
                declaringEntityType,
                targetEntityType,
                joinEntityType,
                navigationVariable
            };

            var parameters = new CSharpRuntimeAnnotationCodeGeneratorParameters(
                navigationVariable,
                className,
                mainBuilder,
                methodBuilder,
                namespaces,
                variables,
                nullable);

            mainBuilder
                .Append("var ").Append(navigationVariable).Append(" = ")
                .Append(declaringEntityType).AppendLine(".AddSkipNavigation(").IncrementIndent()
                .Append(_code.Literal(navigation.Name)).AppendLine(",")
                .Append(targetEntityType).AppendLine(",")
                .Append(joinEntityType).AppendLine(".FindForeignKey(");
            using (mainBuilder.Indent())
            {
                FindProperties(joinEntityType, navigation.ForeignKey.Properties, mainBuilder, nullable);
                mainBuilder.AppendLine(",")
                    .Append(declaringEntityType).Append(".FindKey(");
                FindProperties(declaringEntityType, navigation.ForeignKey.PrincipalKey.Properties, mainBuilder, nullable);
                mainBuilder.Append(")");
                if (nullable)
                {
                    mainBuilder.Append("!");
                }

                mainBuilder.AppendLine(",")
                    .Append(declaringEntityType).Append(")");
                if (nullable)
                {
                    mainBuilder.Append("!");
                }
            }

            mainBuilder.AppendLine(",")
                .Append(_code.Literal(navigation.IsCollection)).AppendLine(",")
                .Append(_code.Literal(navigation.IsOnDependent));

            PropertyBaseParameters(navigation, parameters with { TargetName = declaringEntityType });

            if (navigation.IsEagerLoaded)
            {
                mainBuilder.AppendLine(",")
                    .Append("eagerLoaded: ").Append(_code.Literal(true));
            }

            if (!navigation.LazyLoadingEnabled)
            {
                mainBuilder.AppendLine(",")
                    .Append("lazyLoadingEnabled: ").Append(_code.Literal(false));
            }

            mainBuilder
                .AppendLine(");")
                .DecrementIndent();

            mainBuilder.AppendLine();

            variables.Add("inverse");
            mainBuilder
                .Append("var inverse = ").Append(targetEntityType).Append(".FindSkipNavigation(")
                .Append(_code.Literal(navigation.Inverse.Name)).AppendLine(");")
                .AppendLine("if (inverse != null)")
                .AppendLine("{");
            using (mainBuilder.Indent())
            {
                mainBuilder
                    .Append(navigationVariable).AppendLine(".Inverse = inverse;")
                    .Append("inverse.Inverse = ").Append(navigationVariable).AppendLine(";");
            }

            mainBuilder
                .AppendLine("}")
                .AppendLine();

            var (knownInstances, replacementMethods) = SetPropertyBaseProperties(navigation, parameters);

            SetNavigationBaseProperties(navigation, knownInstances, replacementMethods, parameters);

            CreateAnnotations(navigation, _annotationCodeGenerator.Generate, parameters);

            mainBuilder
                .Append("return ")
                .Append(navigationVariable)
                .AppendLine(";");
        }

        mainBuilder
            .AppendLine("}");
    }

    private void Create(ITrigger trigger, CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        var triggerVariable = _code.Identifier(trigger.ModelName, parameters.ScopeVariables, capitalize: false);

        var mainBuilder = parameters.MainBuilder;
        mainBuilder
            .Append("var ").Append(triggerVariable).Append(" = ").Append(parameters.TargetName).AppendLine(".AddTrigger(")
            .IncrementIndent()
            .Append(_code.Literal(trigger.ModelName))
            .AppendLine(");")
            .DecrementIndent();

        CreateAnnotations(
            trigger,
            _annotationCodeGenerator.Generate,
            parameters with { TargetName = triggerVariable });

        mainBuilder.AppendLine();
    }

    private void CreateAnnotations(
        IEntityType entityType,
        IndentedStringBuilder mainBuilder,
        IndentedStringBuilder methodBuilder,
        SortedSet<string> namespaces,
        string className,
        bool nullable)
    {
        mainBuilder.AppendLine()
            .Append("public static void CreateAnnotations")
            .AppendLine("(RuntimeEntityType runtimeEntityType)")
            .AppendLine("{");

        using (mainBuilder.Indent())
        {
            const string entityTypeVariable = "runtimeEntityType";
            var variables = new HashSet<string> { entityTypeVariable };

            var parameters = new CSharpRuntimeAnnotationCodeGeneratorParameters(
                    entityTypeVariable,
                    className,
                    mainBuilder,
                    methodBuilder,
                    namespaces,
                    variables,
                    nullable);

            var typeBase = (IRuntimeTypeBase)entityType;
            var knownInstances = new Dictionary<object, string>();
            Dictionary<(MemberInfo, bool), string>? replacementMethods = null;
            foreach (var property in typeBase.GetProperties())
            {
                var variableName = _code.Identifier(property.Name, parameters.ScopeVariables, capitalize: false);
                knownInstances[property] = variableName;

                replacementMethods = CreatePrivateAccessors(property, parameters, create: false);

                mainBuilder
                    .Append($"var {variableName} = ")
                    .AppendLine($"{parameters.TargetName}.FindProperty({_code.Literal(property.Name)})!;");
            }

            foreach (var navigation in entityType.GetNavigations())
            {
                var variableName = _code.Identifier(navigation.Name, parameters.ScopeVariables, capitalize: false);
                knownInstances[navigation] = variableName;

                replacementMethods = CreatePrivateAccessors(navigation, parameters, create: false);

                mainBuilder
                    .Append($"var {variableName} = ")
                    .AppendLine($"{parameters.TargetName}.FindNavigation({_code.Literal(navigation.Name)})!;");
            }

            SetTypeBaseProperties(typeBase, knownInstances, replacementMethods, parameters);

            var relationshipSnapshotFactory = RelationshipSnapshotFactoryFactory.Instance.CreateExpression(typeBase);

            mainBuilder
                .Append(parameters.TargetName).AppendLine(".SetRelationshipSnapshotFactory(")
                .IncrementIndent()
                .AppendLines(_code.Expression(relationshipSnapshotFactory, knownInstances, replacementMethods, parameters.Namespaces, out _), skipFinalNewline: true)
                .AppendLine(");")
                .DecrementIndent();

            CreateAnnotations(entityType, _annotationCodeGenerator.Generate, parameters);

            mainBuilder
                .AppendLine()
                .AppendLine("Customize(runtimeEntityType);");
        }

        mainBuilder
            .AppendLine("}")
            .AppendLine()
            .AppendLine("static partial void Customize(RuntimeEntityType runtimeEntityType);");
    }

    private static void CreateAnnotations<TAnnotatable>(
        TAnnotatable annotatable,
        Action<TAnnotatable, CSharpRuntimeAnnotationCodeGeneratorParameters> process,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
        where TAnnotatable : IAnnotatable
    {
        process(
            annotatable,
            parameters with { Annotations = annotatable.GetAnnotations().ToDictionary(a => a.Name, a => a.Value), IsRuntime = false });

        process(
            annotatable,
            parameters with
            {
                Annotations = annotatable.GetRuntimeAnnotations().ToDictionary(a => a.Name, a => a.Value), IsRuntime = true
            });
    }

    private static void AddNamespace(Type type, ISet<string> namespaces)
        => CSharpRuntimeAnnotationCodeGenerator.AddNamespace(type, namespaces);
}
