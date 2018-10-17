// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.ApiPagination
{
    public static class ObjectExtensions
    {
        public static IEnumerable<Type> GetAllInterfaces(this Type type)
        {
            var set = new HashSet<Type>();
            while (type != null)
            {
                foreach (Type iface in type.GetInterfaces())
                {
                    if (iface.IsConstructedGenericType)
                    {
                        set.Add(iface.GetGenericTypeDefinition());
                    }
                    else
                    {
                        set.Add(iface);
                    }
                }

                type = type.BaseType;
            }

            return set;
        }

        /// <summary>
        ///     Returns <see langword="true" /> when <see paramref="value" /> implements a constructed version of the open generic
        ///     interface type <see paramref="openGenericType" /> and sets <see paramref="param" /> to the type parameter of the
        ///     generic interface.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="openGenericType"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public static bool IsGenericInterface(this object value, Type openGenericType, out Type param)
        {
            param = null;
            Type type = value?.GetType();
            if (type == null)
            {
                return false;
            }

            if (!openGenericType.IsGenericTypeDefinition)
            {
                return false;
            }

            if (!openGenericType.IsInterface)
            {
                return false;
            }

            if (!type.IsConstructedGenericType)
            {
                return false;
            }

            if (type.GetGenericTypeDefinition().GetAllInterfaces().Any(iface => iface == openGenericType))
            {
                param = type.GetGenericArguments()[0];
                return true;
            }

            return false;
        }
    }
}
