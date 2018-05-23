// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;

// ReSharper disable once CheckNamespace
namespace System.Reflection
{
    [DebuggerStepThrough]
    internal static class PropertyInfoExtensions
    {
        public static bool IsStatic(this PropertyInfo property)
            => (property.GetMethod ?? property.SetMethod).IsStatic;

        public static bool IsCandidateProperty(this PropertyInfo propertyInfo, bool needsWrite = true, bool publicOnly = true)
            => !propertyInfo.IsStatic()
               && propertyInfo.GetIndexParameters().Length == 0
               && propertyInfo.CanRead
               && (!needsWrite || propertyInfo.FindSetterProperty() != null)
               && propertyInfo.GetMethod != null && (!publicOnly || propertyInfo.GetMethod.IsPublic);


        public static Type FindCandidateNavigationPropertyType(
            this PropertyInfo propertyInfo,
            ITypeMappingSource typeMappingSource,
            IParameterBindingFactories parameterBindingFactories)
        {
            var targetType = propertyInfo.PropertyType;
            var targetSequenceType = targetType.TryGetSequenceType();
            if (!propertyInfo.IsCandidateProperty(targetSequenceType == null))
            {
                return null;
            }

            targetType = targetSequenceType ?? targetType;
            targetType = targetType.UnwrapNullableType();

            if (targetType.GetTypeInfo().IsInterface
                || targetType.GetTypeInfo().IsValueType
                || targetType == typeof(object)
                || parameterBindingFactories.FindFactory(propertyInfo.PropertyType, propertyInfo.Name) != null
                || typeMappingSource.FindMapping(targetType) != null)
            {
                return null;
            }

            return targetType;
        }

        public static PropertyInfo FindGetterProperty([NotNull] this PropertyInfo propertyInfo)
            => propertyInfo.DeclaringType
                .GetPropertiesInHierarchy(propertyInfo.Name)
                .FirstOrDefault(p => p.GetMethod != null);

        public static PropertyInfo FindSetterProperty([NotNull] this PropertyInfo propertyInfo)
            => propertyInfo.DeclaringType
                .GetPropertiesInHierarchy(propertyInfo.Name)
                .FirstOrDefault(p => p.SetMethod != null);
    }
}
