#pragma warning disable CA1052 // Static holder types should be static
#pragma warning disable IDE0022 // Use expression body for method
#pragma warning disable IDE0040 // Add accessibility modifiers
#pragma warning disable IDE0161 // Convert to file-scoped namespace

#if XUNIT_NULLABLE
#nullable enable
#else
// In case this is source-imported with global nullable enabled but no XUNIT_NULLABLE
#pragma warning disable CS8603
#endif

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Xunit
{
	/// <summary>
	/// Contains various static methods that are used to verify that conditions are met during the
	/// process of running tests.
	/// </summary>
#if XUNIT_VISIBILITY_INTERNAL
	internal
#else
	public
#endif
	partial class Assert
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Assert"/> class.
		/// </summary>
		protected Assert() { }

		/// <summary>Do not call this method.</summary>
		[Obsolete("This is an override of Object.Equals(). Call Assert.Equal() instead.", true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static new bool Equals(
			object a,
			object b)
		{
			throw new InvalidOperationException("Assert.Equals should not be used");
		}

		/// <summary>Do not call this method.</summary>
		[Obsolete("This is an override of Object.ReferenceEquals(). Call Assert.Same() instead.", true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static new bool ReferenceEquals(
			object a,
			object b)
		{
			throw new InvalidOperationException("Assert.ReferenceEquals should not be used");
		}

		/// <summary>
		/// Safely perform <see cref="Type.GetGenericTypeDefinition"/>, returning <c>null</c> when the
		/// type is not generic.
		/// </summary>
		/// <param name="type">The potentially generic type</param>
		/// <returns>The generic type definition, when <paramref name="type"/> is generic; <c>null</c>, otherwise.</returns>
		[return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
		[UnconditionalSuppressMessage("Trimmability", "IL2073", Justification = "The interfaces on a generic type definition won't be trimmed if they're preserved for an instantation.")]
#if XUNIT_NULLABLE
		static Type? SafeGetGenericTypeDefinition([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type? type)
#else
		static Type SafeGetGenericTypeDefinition([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
#endif
		{
			if (type == null)
				return null;

#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP2_0_OR_GREATER || NETFRAMEWORK
			if (!type.IsGenericType)
				return null;
#endif

			// We need try/catch for target frameworks that don't support IsGenericType; notably, this
			// would include .NET Core 1.x and .NET Standard 1.x, which are still supported for v2.
			try
			{
				return type.GetGenericTypeDefinition();
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}
	}
}
