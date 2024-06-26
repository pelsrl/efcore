// <auto-generated />
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;

#pragma warning disable 219, 612, 618
#nullable disable

namespace TestNamespace
{
    [EntityFrameworkInternal]
    public partial class SpatialTypesEntityType
    {
        public static RuntimeEntityType Create(RuntimeModel model, RuntimeEntityType baseEntityType = null)
        {
            var runtimeEntityType = model.AddEntityType(
                "Microsoft.EntityFrameworkCore.Scaffolding.CompiledModelRelationalTestBase+SpatialTypes",
                typeof(CompiledModelRelationalTestBase.SpatialTypes),
                baseEntityType,
                propertyCount: 2,
                keyCount: 1);

            var id = runtimeEntityType.AddProperty(
                "Id",
                typeof(int),
                propertyInfo: typeof(CompiledModelTestBase.AbstractBase).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                fieldInfo: typeof(CompiledModelTestBase.AbstractBase).GetField("<Id>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                valueGenerated: ValueGenerated.OnAdd,
                afterSaveBehavior: PropertySaveBehavior.Throw,
                sentinel: 0);
            id.SetGetter(
                (CompiledModelRelationalTestBase.SpatialTypes entity) => UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id(entity),
                (CompiledModelRelationalTestBase.SpatialTypes entity) => UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id(entity) == 0,
                (CompiledModelRelationalTestBase.SpatialTypes instance) => UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id(instance),
                (CompiledModelRelationalTestBase.SpatialTypes instance) => UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id(instance) == 0);
            id.SetSetter(
                (CompiledModelRelationalTestBase.SpatialTypes entity, int value) => UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id(entity) = value);
            id.SetMaterializationSetter(
                (CompiledModelRelationalTestBase.SpatialTypes entity, int value) => UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id(entity) = value);
            id.SetAccessors(
                (InternalEntityEntry entry) => entry.FlaggedAsStoreGenerated(0) ? entry.ReadStoreGeneratedValue<int>(0) : entry.FlaggedAsTemporary(0) && UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id((CompiledModelRelationalTestBase.SpatialTypes)entry.Entity) == 0 ? entry.ReadTemporaryValue<int>(0) : UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id((CompiledModelRelationalTestBase.SpatialTypes)entry.Entity),
                (InternalEntityEntry entry) => UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id((CompiledModelRelationalTestBase.SpatialTypes)entry.Entity),
                (InternalEntityEntry entry) => entry.ReadOriginalValue<int>(id, 0),
                (InternalEntityEntry entry) => entry.ReadRelationshipSnapshotValue<int>(id, 0),
                (ValueBuffer valueBuffer) => valueBuffer[0]);
            id.SetPropertyIndexes(
                index: 0,
                originalValueIndex: 0,
                shadowIndex: -1,
                relationshipIndex: 0,
                storeGenerationIndex: 0);
            id.TypeMapping = IntTypeMapping.Default.Clone(
                comparer: new ValueComparer<int>(
                    (int v1, int v2) => v1 == v2,
                    (int v) => v,
                    (int v) => v),
                keyComparer: new ValueComparer<int>(
                    (int v1, int v2) => v1 == v2,
                    (int v) => v,
                    (int v) => v),
                providerValueComparer: new ValueComparer<int>(
                    (int v1, int v2) => v1 == v2,
                    (int v) => v,
                    (int v) => v));
            id.SetCurrentValueComparer(new EntryCurrentValueComparer<int>(id));
            id.AddAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);
            id.AddRuntimeAnnotation("UnsafeAccessors", new[] { ("SpatialTypesEntityType.UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id", "TestNamespace") });

            var point = runtimeEntityType.AddProperty(
                "Point",
                typeof(Point),
                nullable: true,
                valueGenerated: ValueGenerated.OnAdd,
                valueConverter: new CastingConverter<Point, Point>(),
                valueComparer: new CompiledModelTestBase.CustomValueComparer<Point>(),
                providerValueComparer: new CompiledModelTestBase.CustomValueComparer<Point>());
            point.SetPropertyIndexes(
                index: 1,
                originalValueIndex: 1,
                shadowIndex: 0,
                relationshipIndex: -1,
                storeGenerationIndex: 1);
            point.TypeMapping = null;
            point.AddAnnotation("Relational:ColumnType", "geometry");
            point.AddAnnotation("Relational:DefaultValue", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=0;POINT Z(0 0 0)"));
            point.AddAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.None);

            var key = runtimeEntityType.AddKey(
                new[] { id });
            runtimeEntityType.SetPrimaryKey(key);

            return runtimeEntityType;
        }

        public static void CreateAnnotations(RuntimeEntityType runtimeEntityType)
        {
            var id = runtimeEntityType.FindProperty("Id")!;
            var point = runtimeEntityType.FindProperty("Point")!;
            runtimeEntityType.SetOriginalValuesFactory(
                (InternalEntityEntry source) =>
                {
                    var entity = (CompiledModelRelationalTestBase.SpatialTypes)source.Entity;
                    return (ISnapshot)new Snapshot<int, Point>(((ValueComparer<int>)id.GetValueComparer()).Snapshot(source.GetCurrentValue<int>(id)), source.GetCurrentValue<Point>(point) == null ? null : ((ValueComparer<Point>)point.GetValueComparer()).Snapshot(source.GetCurrentValue<Point>(point)));
                });
            runtimeEntityType.SetStoreGeneratedValuesFactory(
                () => (ISnapshot)new Snapshot<int, Point>(((ValueComparer<int>)id.GetValueComparer()).Snapshot(default(int)), default(Point) == null ? null : ((ValueComparer<Point>)point.GetValueComparer()).Snapshot(default(Point))));
            runtimeEntityType.SetTemporaryValuesFactory(
                (InternalEntityEntry source) => (ISnapshot)new Snapshot<int, Point>(default(int), default(Point)));
            runtimeEntityType.SetShadowValuesFactory(
                (IDictionary<string, object> source) => (ISnapshot)new Snapshot<Point>(source.ContainsKey("Point") ? (Point)source["Point"] : null));
            runtimeEntityType.SetEmptyShadowValuesFactory(
                () => (ISnapshot)new Snapshot<Point>(default(Point)));
            runtimeEntityType.SetRelationshipSnapshotFactory(
                (InternalEntityEntry source) =>
                {
                    var entity = (CompiledModelRelationalTestBase.SpatialTypes)source.Entity;
                    return (ISnapshot)new Snapshot<int>(((ValueComparer<int>)id.GetKeyValueComparer()).Snapshot(source.GetCurrentValue<int>(id)));
                });
            runtimeEntityType.Counts = new PropertyCounts(
                propertyCount: 2,
                navigationCount: 0,
                complexPropertyCount: 0,
                originalValueCount: 2,
                shadowCount: 1,
                relationshipCount: 1,
                storeGeneratedCount: 2);
            runtimeEntityType.AddAnnotation("Relational:FunctionName", null);
            runtimeEntityType.AddAnnotation("Relational:Schema", null);
            runtimeEntityType.AddAnnotation("Relational:SqlQuery", null);
            runtimeEntityType.AddAnnotation("Relational:TableName", "SpatialTypes");
            runtimeEntityType.AddAnnotation("Relational:ViewName", null);
            runtimeEntityType.AddAnnotation("Relational:ViewSchema", null);

            Customize(runtimeEntityType);
        }

        static partial void Customize(RuntimeEntityType runtimeEntityType);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<Id>k__BackingField")]
        public static extern ref int UnsafeAccessor_Microsoft_EntityFrameworkCore_Scaffolding_AbstractBase_Id(CompiledModelRelationalTestBase.SpatialTypes @this);
    }
}
