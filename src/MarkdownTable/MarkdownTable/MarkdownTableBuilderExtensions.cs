// Code was originally found in marcolink/MarkdownTable, but the nuget package does not work.
// The original license is Apache 2.0: https://github.com/marcolink/MarkdownTable/blob/1fcf2935ab6dbfe4a3c56434f4c191d0003d0320/LICENSE
// The original code has been modified.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace InfrastructureTools.MarkdownTable;

public static class MarkdownTableBuilderExtensions
{
    public static string ToMardownTableString<T>(this IEnumerable<T> rows)
    {
        MarkdownTableBuilder builder = new();
        PropertyInfo[] properties = typeof(T).GetProperties().Where(p => p.PropertyType.IsRenderable()).ToArray();
        FieldInfo[] fields = typeof(T).GetFields().Where(f => f.FieldType.IsRenderable()).ToArray();

        builder.WithHeader(properties.Select(p => p.Name).Concat(fields.Select(f => f.Name)).ToArray());

        foreach (T row in rows)
        {
            IEnumerable<object> objects = properties
                .Select(p => p.GetValue(row, null) ?? throw new NullReferenceException())
                .Concat(fields.Select(f => f.GetValue(row) ?? throw new NullReferenceException()));

            builder.WithRow(objects);
        }

        return builder.ToString();
    }

    private static bool IsRenderable(this Type type)
    {
        return type.IsNumeric()
               || Type.GetTypeCode(type) == TypeCode.String
               || Type.GetTypeCode(type) == TypeCode.Boolean;
    }

    private static bool IsNumeric(this Type type)
    {
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                return true;
            case TypeCode.Object:
                if (type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                    Nullable.GetUnderlyingType(type) is Type obj)
                {
                    return obj.IsNumeric();
                }
                return false;
            default:
                return false;
        }
    }
}