// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace XliffTasks
{
    internal abstract class TranslatableNode
    {
        protected TranslatableNode(string id, string source, string note)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Note = note ?? string.Empty;

            ThrowIfEmpty(id, nameof(id));
            ThrowIfEmpty(source, nameof(source));
        }

        private static void ThrowIfEmpty(string value, string parameterName)
        {
            Debug.Assert(value != null);
            if (value.Length == 0)
            {
                throw new ArgumentException($"{parameterName} cannot be empty", parameterName);
            }
        }

        /// <summary>
        /// The unique ID of the node within a translatable document.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The original text of the node before any translation.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// A comment associated with the node.
        /// </summary>
        public string Note { get; }

        /// <summary>
        /// Mutates the parent document such that subsequent calls to <see cref="TranslatableDocument.Save(string)" />
        /// will replace <see cref="Source"/> with <paramref name="translation"/> in this node.
        /// <summary>
        public abstract void Translate(string translation);
    }
}
