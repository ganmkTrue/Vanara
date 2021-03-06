﻿using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Vanara.Extensions;
using Vanara.PInvoke;

namespace Vanara.InteropServices
{
	/// <summary>
	/// For structures with a single array as the last field that are intended to be variable length, this class manages the structure and
	/// automatically marshals the correct structure to memory.
	/// </summary>
	/// <typeparam name="T">The type of the structure.</typeparam>
	public class SafeAnysizeStruct<T> : SafeAnysizeStructBase<T>
	{
		private FieldInfo fiCount;

		/// <summary>Initializes a new instance of the <see cref="SafeAnysizeStruct{T}"/> class.</summary>
		/// <param name="value">The initial value of the structure, if provided.</param>
		/// <param name="sizeFieldName">
		/// The name of the field in <typeparamref name="T"/> that holds the length of the array. If <see langword="null"/>, the first
		/// public field will be selected.
		/// </param>
		/// <exception cref="InvalidOperationException">This class can only manange sequential layout structures.</exception>
		public SafeAnysizeStruct(in T value, string sizeFieldName = null) : base(baseSz)
		{
			InitCountField(sizeFieldName);
			ToNative(value);
		}

		/// <summary>Initializes a new instance of the <see cref="SafeAnysizeStruct{T}"/> class from a pointer.</summary>
		/// <param name="allocatedMemory">A pointer to memory that holds the value of an instance of <typeparamref name="T"/>.</param>
		/// <param name="size">The size of the allocated memory in <paramref name="allocatedMemory"/> in bytes.</param>
		/// <param name="sizeFieldName">
		/// The name of the field in <typeparamref name="T"/> that holds the length of the array. If <see langword="null"/>, the first
		/// public field will be selected.
		/// </param>
		public SafeAnysizeStruct(IntPtr allocatedMemory, SizeT size, string sizeFieldName = null) : base(allocatedMemory, size, false)
		{
			if (allocatedMemory == IntPtr.Zero) throw new ArgumentNullException(nameof(allocatedMemory));
			if (baseSz > size) throw new OutOfMemoryException();
			InitCountField(sizeFieldName);
		}

		/// <summary>Initializes a new instance of the <see cref="SafeAnysizeStruct{T}"/> class with an initial empty memory allocation.</summary>
		/// <param name="size">The size of the reserved memory in bytes.</param>
		/// <param name="sizeFieldName">
		/// The name of the field in <typeparamref name="T"/> that holds the length of the array. If <see langword="null"/>, the first
		/// public field will be selected.
		/// </param>
		public SafeAnysizeStruct(SizeT size, string sizeFieldName = null) : base(size) => InitCountField(sizeFieldName);

		/// <summary>Performs an implicit conversion from <typeparamref name="T"/> to <see cref="SafeAnysizeStructBase{T}"/>.</summary>
		/// <param name="s">The <typeparamref name="T"/> instance.</param>
		/// <returns>The result of the conversion.</returns>
#if !(NET20 || NET35 || NET40)
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
		public static implicit operator SafeAnysizeStruct<T>(in T s) => new SafeAnysizeStruct<T>(s);

		/// <summary>Gets the length of the array from the structure.</summary>
		/// <param name="local">The local, system marshaled, structure instance extracted from the pointer.</param>
		/// <returns>The element length of the 'anysize' array.</returns>
#if !(NET20 || NET35 || NET40)
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
		protected override int GetArrayLength(in T local) => Convert.ToInt32(fiCount.GetValue(local));

		private void InitCountField(string sizeFieldName)
		{
			fiCount = string.IsNullOrEmpty(sizeFieldName) ? structType.GetOrderedFields().First() : structType.GetField(sizeFieldName, BindingFlags.Public | BindingFlags.Instance);
			if (fiCount is null) throw new ArgumentException("Invalid size field name.", nameof(sizeFieldName));
		}
	}

	/// <summary>
	/// For structures with a single array as the last field that are intended to be variable length, this class manages the structure and
	/// automatically marshals the correct structure to memory.
	/// </summary>
	/// <typeparam name="T">The type of the structure.</typeparam>
	public abstract class SafeAnysizeStructBase<T> : SafeMemoryHandle<CoTaskMemoryMethods>
	{
		/// <summary>The base, unextended size of T.</summary>
		protected static readonly int baseSz;

		/// <summary>The type of the structure and type of the anysize array's elements.</summary>
		protected static readonly Type elemType, structType;

		/// <summary>The reflected field of the array.</summary>
		protected static readonly FieldInfo fiArray;

		static SafeAnysizeStructBase()
		{
			structType = typeof(T);
			if (!structType.IsLayoutSequential)
				throw new InvalidOperationException("This class can only manange sequential layout structures.");
			baseSz = Marshal.SizeOf(structType);
			fiArray = structType.GetOrderedFields().Last();
			if (!fiArray.FieldType.IsArray)
				throw new ArgumentException("The field information must be for an array.", nameof(fiArray));
			elemType = fiArray.FieldType.FindElementType();
		}

		/// <summary>Initializes a new instance of the <see cref="SafeAnysizeStructBase{T}"/> class.</summary>
		/// <param name="size">The size of memory to allocate, in bytes.</param>
		protected SafeAnysizeStructBase(SizeT size) : base(size) { }

		/// <summary>Initializes a new instance of the <see cref="SafeAnysizeStructBase{T}"/> class.</summary>
		/// <param name="allocatedMemory">The allocated memory.</param>
		/// <param name="size">The size.</param>
		/// <param name="ownsHandle">if set to <see langword="true"/> [owns handle].</param>
		protected SafeAnysizeStructBase(IntPtr allocatedMemory, SizeT size, bool ownsHandle) : base(allocatedMemory, size, ownsHandle) { }

		/// <summary>Gets or sets the structure value.</summary>
		public T Value { get => FromNative(handle, Size); set => ToNative(value); }

		/// <summary>
		/// Performs an explicit conversion from <see cref="SafeAnysizeStructBase{T}"/> to <see cref="IntPtr"/>. The <c>IntPtr</c> is the
		/// memory location of the fully marshaled structure with the full final field array.
		/// </summary>
		/// <param name="s">The <see cref="SafeAnysizeStructBase{T}"/> instance.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator IntPtr(SafeAnysizeStructBase<T> s) => s?.handle ?? IntPtr.Zero;

		/// <summary>Performs an explicit conversion from <see cref="SafeAnysizeStructBase{T}"/> to <typeparamref name="T"/>.</summary>
		/// <param name="s">The <see cref="SafeAnysizeStructBase{T}"/> instance.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator T(SafeAnysizeStructBase<T> s) => s is null ? default : s.Value;

		/// <summary>Converts the native memory to <typeparamref name="T"/>.</summary>
		/// <param name="allocatedMemory">The allocated memory.</param>
		/// <param name="size">The size.</param>
		/// <returns></returns>
		protected virtual T FromNative(IntPtr allocatedMemory, int size)
		{
			var local = (T)Marshal.PtrToStructure(allocatedMemory, structType); // Can't use Convert or get circular ref.
			var cnt = GetArrayLength(local);
			var arrOffset = Marshal.OffsetOf(structType, fiArray.Name).ToInt32();
			fiArray.SetValueDirect(__makeref(local), allocatedMemory.ToArray(elemType, cnt, arrOffset, size));
			return local;
		}

		/// <summary>Gets the length of the array from the structure.</summary>
		/// <param name="local">The local, system marshaled, structure instance extracted from the pointer.</param>
		/// <returns>The element length of the 'anysize' array.</returns>
		protected abstract int GetArrayLength(in T local);

		/// <summary>Converts the managed instance to native.</summary>
		/// <param name="value">The managed value.</param>
		protected virtual void ToNative(T value)
		{
			// Get the current array for the last field (or create one if needed)
			var arrVal = fiArray.GetValue(value);
			if (arrVal is null || ((Array)arrVal).Length == 0)
			{
				arrVal = Array.CreateInstance(elemType, 1);
				((Array)arrVal).SetValue(Activator.CreateInstance(elemType), 0);
				fiArray.SetValueDirect(__makeref(value), arrVal);
			}
			// Determine mem required for current struct and last field value
			var arrElemSz = Marshal.SizeOf(elemType);
			var arrLen = ((Array)arrVal).Length;
			var memSz = baseSz + arrElemSz * (arrLen - 1);
			// Set memory size
			Size = memSz;
			// Marshal base structure - don't use Write to prevent loops
			Marshal.StructureToPtr(value, handle, false);
			// Push each element of the array into memory, starting with second item in array since first was pushed by StructureToPtr
			for (var i = 1; i < arrLen; i++)
				handle.Write(((Array)arrVal).GetValue(i), baseSz + arrElemSz * (i - 1), memSz);
		}
	}

	/// <summary>
	/// A marshaler implementation of <see cref="IVanaraMarshaler"/> to set the marshaler as an attribute using <see
	/// cref="SafeAnysizeStruct{T}"/>. Use the cookie paramter of <see
	/// cref="SafeAnysizeStructMarshaler{T}.SafeAnysizeStructMarshaler(string)"/> to specify the name of the field in <typeparamref
	/// name="T"/> that specifies the number of elements in the last field of <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The structure type to be marshaled.</typeparam>
	/// <seealso cref="Vanara.InteropServices.IVanaraMarshaler"/>
	public class SafeAnysizeStructMarshaler<T> : IVanaraMarshaler
	{
		private string sizeFieldName;

		/// <summary>Initializes a new instance of the <see cref="SafeAnysizeStructMarshaler{T}"/> class.</summary>
		/// <param name="cookie">
		/// The name of the field in <typeparamref name="T"/> that specifies the number of elements in the last field of <typeparamref name="T"/>.
		/// </param>
		public SafeAnysizeStructMarshaler(string cookie) => sizeFieldName = cookie;

		SizeT IVanaraMarshaler.GetNativeSize() => Marshal.SizeOf(typeof(T));

		SafeAllocatedMemoryHandle IVanaraMarshaler.MarshalManagedToNative(object managedObject) =>
			managedObject is null ? SafeCoTaskMemHandle.Null : (SafeAllocatedMemoryHandle)new SafeAnysizeStruct<T>((T)managedObject, sizeFieldName);

		object IVanaraMarshaler.MarshalNativeToManaged(IntPtr pNativeData, SizeT allocatedBytes)
		{
			if (pNativeData == IntPtr.Zero) return null;
			using var s = new SafeAnysizeStruct<T>(pNativeData, allocatedBytes, sizeFieldName);
			return s.Value;
		}
	}
}