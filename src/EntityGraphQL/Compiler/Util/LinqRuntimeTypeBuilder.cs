using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// Builds .NET types at runtime and caches them to be reused
/// </summary>
public static class LinqRuntimeTypeBuilder
{
    public static readonly string DynamicAssemblyName = "EntityGraphQL.DynamicTypes";
    public static readonly string DynamicTypePrefix = "Dynamic_";
    private static readonly AssemblyName assemblyName = new() { Name = DynamicAssemblyName };
    private static readonly ModuleBuilder moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run).DefineDynamicModule(assemblyName.Name);
    private static readonly Dictionary<string, Type> builtTypes = [];

#if NET9_0_OR_GREATER
    private static readonly Lock lockObj = new();
#else
    private static readonly object lockObj = new();
#endif

    // We build a class name based on all the selected fields so we can cache the anonymous types we built
    // Names can't be > 1024 length, so we store them against a shorter Guid string
    private static readonly Dictionary<string, string> typesByFullName = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetTypeKey(Dictionary<string, Type> fields) => fields.OrderBy(f => f.Key).Aggregate(DynamicTypePrefix, (current, field) => current + field.Key + field.Value.GetHashCode());

    /// <summary>
    /// Build a dynamic type based on the fields. Types are cached so they only are created once
    /// </summary>
    /// <param name="fields">Field names and the type of the field.</param>
    /// <param name="description">An optional description string. Helps with debugging - e.g. the field the type is built for</param>
    /// <param name="parentType">If the type inherits from another type</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static Type GetDynamicType(Dictionary<string, Type> fields, string description, Type? parentType = null)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(fields, nameof(fields));
#else
        if (null == fields)
            throw new ArgumentNullException(nameof(fields));
#endif

        string classFullName = GetTypeKey(fields) + parentType?.Name.GetHashCode();
        lock (lockObj)
        {
            if (!typesByFullName.TryGetValue(classFullName, out var classId))
            {
                classId = $"{DynamicTypePrefix}{(description != null ? $"{description}_" : "")}{Guid.NewGuid()}";
                typesByFullName[classFullName] = classId;
            }

            if (builtTypes.TryGetValue(classId, out var builtType))
                return builtType;

            var typeBuilder = moduleBuilder.DefineType(classId.ToString(), TypeAttributes.Public | TypeAttributes.Class, parentType);

            foreach (var field in fields)
            {
                if (parentType != null && parentType.GetField(field.Key) != null)
                    continue;

                typeBuilder.DefineField(field.Key, field.Value, FieldAttributes.Public);
            }

            builtTypes[classId] = typeBuilder.CreateTypeInfo()!.AsType();
            return builtTypes[classId];
        }
    }
}
