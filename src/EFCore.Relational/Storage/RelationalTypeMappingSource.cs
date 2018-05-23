// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Storage
{
    /// <summary>
    ///     <para>
    ///         The base class for non-relational type mapping starting with version 2.1. Non-relational providers
    ///         should derive from this class and override <see cref="FindMapping(RelationalTypeMappingInfo)" />
    ///     </para>
    ///     <para>
    ///         This type is typically used by database providers (and other extensions). It is generally
    ///         not used in application code.
    ///     </para>
    /// </summary>
    public abstract class RelationalTypeMappingSource : TypeMappingSourceBase, IRelationalTypeMappingSource
    {
        private readonly ConcurrentDictionary<RelationalTypeMappingInfo, RelationalTypeMapping> _explicitMappings
            = new ConcurrentDictionary<RelationalTypeMappingInfo, RelationalTypeMapping>();

        /// <summary>
        ///     Initializes a new instance of the this class.
        /// </summary>
        /// <param name="dependencies"> Parameter object containing dependencies for this service. </param>
        /// <param name="relationalDependencies"> Parameter object containing relational-specific dependencies for this service. </param>
        protected RelationalTypeMappingSource(
            [NotNull] TypeMappingSourceDependencies dependencies,
            [NotNull] RelationalTypeMappingSourceDependencies relationalDependencies)
            : base(dependencies)
        {
            Check.NotNull(relationalDependencies, nameof(relationalDependencies));

            RelationalDependencies = relationalDependencies;
        }

        /// <summary>
        ///     <para>
        ///         Overridden by relational database providers to find a type mapping for the given info.
        ///     </para>
        ///     <para>
        ///         The mapping info is populated with as much information about the required type mapping as
        ///         is available. Use all the information necessary to create the best mapping. Return <c>null</c>
        ///         if no mapping is available.
        ///     </para>
        /// </summary>
        /// <param name="mappingInfo"> The mapping info to use to create the mapping. </param>
        /// <returns> The type mapping, or <c>null</c> if none could be found. </returns>
        protected abstract RelationalTypeMapping FindMapping(RelationalTypeMappingInfo mappingInfo);

        /// <summary>
        ///     Dependencies used to create this <see cref="RelationalTypeMappingSource" />
        /// </summary>
        protected virtual RelationalTypeMappingSourceDependencies RelationalDependencies { get; }

        /// <summary>
        ///     Overridden to call <see cref="FindMapping(RelationalTypeMappingInfo)" />
        /// </summary>
        /// <param name="mappingInfo"> The mapping info to use to create the mapping. </param>
        /// <returns> The type mapping, or <c>null</c> if none could be found. </returns>
        protected override CoreTypeMapping FindMapping(TypeMappingInfo mappingInfo)
            => throw new InvalidOperationException("FindMapping on a 'RelationalTypeMappingSource' with a non-relational 'TypeMappingInfo'.");

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected virtual RelationalTypeMapping FindMappingWithConversion(
            RelationalTypeMappingInfo mappingInfo,
            [CanBeNull] IProperty property)
        {
            Check.NotNull(mappingInfo, nameof(mappingInfo));

            var principals = property?.FindPrincipals().ToList();

            var customConverter = principals
                ?.Select(p => p.GetValueConverter())
                .FirstOrDefault(c => c != null);

            var providerClrType = principals
                ?.Select(p => p.GetProviderClrType())
                .FirstOrDefault(t => t != null)
                ?.UnwrapNullableType();

            var resolvedMapping = _explicitMappings.GetOrAdd(
                mappingInfo,
                k =>
                {
                    var mapping = providerClrType == null
                                  || providerClrType == mappingInfo.ClrType
                        ? FindMapping(mappingInfo)
                        : null;

                    if (mapping == null)
                    {
                        var sourceType = mappingInfo.ClrType;

                        if (sourceType != null)
                        {
                            foreach (var converterInfo in Dependencies
                                .ValueConverterSelector
                                .Select(sourceType, providerClrType))
                            {
                                var mappingInfoUsed = mappingInfo.WithConverter(converterInfo);
                                mapping = FindMapping(mappingInfoUsed);

                                if (mapping == null
                                    && providerClrType != null)
                                {
                                    foreach (var secondConverterInfo in Dependencies
                                        .ValueConverterSelector
                                        .Select(providerClrType))
                                    {
                                        mapping = FindMapping(mappingInfoUsed.WithConverter(secondConverterInfo));

                                        if (mapping != null)
                                        {
                                            mapping = (RelationalTypeMapping)mapping.Clone(secondConverterInfo.Create());
                                            break;
                                        }
                                    }
                                }

                                if (mapping != null)
                                {
                                    mapping = (RelationalTypeMapping)mapping.Clone(converterInfo.Create());
                                    break;
                                }
                            }
                        }
                    }

                    if (mapping != null
                        && customConverter != null)
                    {
                        mapping = (RelationalTypeMapping)mapping.Clone(customConverter);
                    }

                    return mapping;
                });

            ValidateMapping(resolvedMapping, property);

            return resolvedMapping;
        }

        /// <summary>
        ///     <para>
        ///         Finds the type mapping for a given <see cref="IProperty" />.
        ///     </para>
        ///     <para>
        ///         Note: providers should typically not need to override this method.
        ///     </para>
        /// </summary>
        /// <param name="property"> The property. </param>
        /// <returns> The type mapping, or <c>null</c> if none was found. </returns>
        public override CoreTypeMapping FindMapping(IProperty property)
            => property.FindRelationalMapping()
               ?? FindMappingWithConversion(new RelationalTypeMappingInfo(property), property);

        /// <summary>
        ///     <para>
        ///         Finds the type mapping for a given <see cref="Type" />.
        ///     </para>
        ///     <para>
        ///         Note: Only call this method if there is no <see cref="IProperty" />
        ///         or <see cref="MemberInfo" /> available, otherwise call <see cref="FindMapping(IProperty)" />
        ///         or <see cref="FindMapping(MemberInfo)" />
        ///     </para>
        ///     <para>
        ///         Note: providers should typically not need to override this method.
        ///     </para>
        /// </summary>
        /// <param name="type"> The CLR type. </param>
        /// <returns> The type mapping, or <c>null</c> if none was found. </returns>
        public override CoreTypeMapping FindMapping(Type type)
            => FindMappingWithConversion(new RelationalTypeMappingInfo(type), null);

        /// <summary>
        ///     <para>
        ///         Finds the type mapping for a given <see cref="MemberInfo" /> representing
        ///         a field or a property of a CLR type.
        ///     </para>
        ///     <para>
        ///         Note: Only call this method if there is no <see cref="IProperty" /> available, otherwise
        ///         call <see cref="FindMapping(IProperty)" />
        ///     </para>
        ///     <para>
        ///         Note: providers should typically not need to override this method.
        ///     </para>
        /// </summary>
        /// <param name="member"> The field or property. </param>
        /// <returns> The type mapping, or <c>null</c> if none was found. </returns>
        public override CoreTypeMapping FindMapping(MemberInfo member)
            => FindMappingWithConversion(new RelationalTypeMappingInfo(member), null);

        /// <summary>
        ///     <para>
        ///         Finds the type mapping for a given database type name.
        ///     </para>
        ///     <para>
        ///         Note: Only call this method if there is no <see cref="IProperty" /> available, otherwise
        ///         call <see cref="FindMapping(IProperty)" />
        ///     </para>
        ///     <para>
        ///         Note: providers should typically not need to override this method.
        ///     </para>
        /// </summary>
        /// <param name="storeTypeName"> The database type name. </param>
        /// <returns> The type mapping, or <c>null</c> if none was found. </returns>
        public virtual RelationalTypeMapping FindMapping(string storeTypeName)
            => FindMappingWithConversion(new RelationalTypeMappingInfo(storeTypeName), null);

        /// <summary>
        ///     <para>
        ///         Finds the type mapping for a given <see cref="Type" /> and additional facets.
        ///     </para>
        ///     <para>
        ///         Note: Only call this method if there is no <see cref="IProperty" /> available, otherwise
        ///         call <see cref="FindMapping(IProperty)" />
        ///     </para>
        ///     <para>
        ///         Note: providers should typically not need to override this method.
        ///     </para>
        /// </summary>
        /// <param name="type"> The CLR type. </param>
        /// <param name="storeTypeName"> The database type name. </param>
        /// <param name="keyOrIndex"> If <c>true</c>, then a special mapping for a key or index may be returned. </param>
        /// <param name="unicode"> Specifies Unicode or ANSI mapping, or <c>null</c> for default. </param>
        /// <param name="size"> Specifies a size for the mapping, or <c>null</c> for default. </param>
        /// <param name="rowVersion"> Specifies a row-version, or <c>null</c> for default. </param>
        /// <param name="fixedLength"> Specifies a fixed length mapping, or <c>null</c> for default. </param>
        /// <param name="precision"> Specifies a precision for the mapping, or <c>null</c> for default. </param>
        /// <param name="scale"> Specifies a scale for the mapping, or <c>null</c> for default. </param>
        /// <returns> The type mapping, or <c>null</c> if none was found. </returns>
        public virtual RelationalTypeMapping FindMapping(
            Type type,
            string storeTypeName,
            bool keyOrIndex = false,
            bool? unicode = null,
            int? size = null,
            bool? rowVersion = null,
            bool? fixedLength = null,
            int? precision = null,
            int? scale = null)
            => FindMappingWithConversion(
                new RelationalTypeMappingInfo(
                    type, storeTypeName, keyOrIndex, unicode, size, rowVersion, fixedLength, precision, scale), null);

        RelationalTypeMapping IRelationalTypeMappingSource.FindMapping(IProperty property)
            => (RelationalTypeMapping)FindMapping(property);

        RelationalTypeMapping IRelationalTypeMappingSource.FindMapping(Type type)
            => (RelationalTypeMapping)FindMapping(type);

        RelationalTypeMapping IRelationalTypeMappingSource.FindMapping(MemberInfo member)
            => (RelationalTypeMapping)FindMapping(member);
    }
}
