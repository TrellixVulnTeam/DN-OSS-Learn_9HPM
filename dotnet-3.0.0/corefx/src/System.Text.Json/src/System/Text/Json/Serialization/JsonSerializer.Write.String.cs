﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Json
{
    public static partial class JsonSerializer
    {
        /// <summary>
        /// Convert the provided value into a <see cref="System.String"/>.
        /// </summary>
        /// <returns>A <see cref="System.String"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <remarks>Using a <see cref="System.String"/> is not as efficient as using UTF-8
        /// encoding since the implementation internally uses UTF-8. See also <see cref="SerializeToUtf8Bytes"/>
        /// and <see cref="SerializeAsync"/>.
        /// </remarks>
        public static string Serialize<TValue>(TValue value, JsonSerializerOptions options = null)
        {
            return ToStringInternal(value, typeof(TValue), options);
        }

        /// <summary>
        /// Convert the provided value into a <see cref="System.String"/>.
        /// </summary>
        /// <returns>A <see cref="System.String"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <remarks>Using a <see cref="System.String"/> is not as efficient as using UTF-8
        /// encoding since the implementation internally uses UTF-8. See also <see cref="SerializeToUtf8Bytes"/>
        /// and <see cref="SerializeAsync"/>.
        /// </remarks>
        public static string Serialize(object value, Type inputType, JsonSerializerOptions options = null)
        {
            VerifyValueAndType(value, inputType);

            return ToStringInternal(value, inputType, options);
        }

        private static string ToStringInternal(object value, Type inputType, JsonSerializerOptions options)
        {
            return WriteCoreString(value, inputType, options);
        }
    }
}
