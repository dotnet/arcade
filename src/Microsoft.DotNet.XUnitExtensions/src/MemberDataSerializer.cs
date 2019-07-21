// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit.Abstractions;

namespace Microsoft.DotNet.XUnitExtensions
{
    public class MemberDataSerializer<T> : IXunitSerializable
    {
        private const string ObjKey = "objBase64";
        public T Object { get; private set; }

        public MemberDataSerializer(T objectToSerialize = default)
        {
            Object = objectToSerialize;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            string base64Str = info.GetValue<string>(nameof(ObjKey));
            Object = (T)FromBase64String(base64Str);
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(ObjKey, ToBase64String(Object));
        }

        private static string ToBase64String(object obj)
        {
            var binaryFormatter = new BinaryFormatter();
            using (var memoryStream = new MemoryStream())
            {
                binaryFormatter.Serialize(memoryStream, obj);
                byte[] raw = memoryStream.ToArray();
                return Convert.ToBase64String(raw);
            }
        }

        private static object FromBase64String(string base64Str)
        {
            var binaryFormatter = new BinaryFormatter();
            byte[] raw = Convert.FromBase64String(base64Str);
            using (var memoryStream = new MemoryStream(raw))
            {
                return binaryFormatter.Deserialize(memoryStream);
            }
        }
    }
}
