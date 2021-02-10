using SAEA.Audio.NAudio.Utils;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
    [StructLayout(LayoutKind.Explicit)]
    public struct PropVariant
    {
        [FieldOffset(0)]
        private short vt;

        [FieldOffset(2)]
        private short wReserved1;

        [FieldOffset(4)]
        private short wReserved2;

        [FieldOffset(6)]
        private short wReserved3;

        [FieldOffset(8)]
        private sbyte cVal;

        [FieldOffset(8)]
        private byte bVal;

        [FieldOffset(8)]
        private short iVal;

        [FieldOffset(8)]
        private ushort uiVal;

        [FieldOffset(8)]
        private int lVal;

        [FieldOffset(8)]
        private uint ulVal;

        [FieldOffset(8)]
        private int intVal;

        [FieldOffset(8)]
        private uint uintVal;

        [FieldOffset(8)]
        private long hVal;

        [FieldOffset(8)]
        private long uhVal;

        [FieldOffset(8)]
        private float fltVal;

        [FieldOffset(8)]
        private double dblVal;

        [FieldOffset(8)]
        private short boolVal;

        [FieldOffset(8)]
        private int scode;

        [FieldOffset(8)]
        private System.Runtime.InteropServices.ComTypes.FILETIME filetime;

        [FieldOffset(8)]
        private Blob blobVal;

        [FieldOffset(8)]
        private IntPtr pointerValue;

        public VarEnum DataType
        {
            get
            {
                return (VarEnum)this.vt;
            }
        }

        public object Value
        {
            get
            {
                VarEnum dataType = this.DataType;
                if (dataType > VarEnum.VT_INT)
                {
                    if (dataType <= VarEnum.VT_BLOB)
                    {
                        if (dataType == VarEnum.VT_LPWSTR)
                        {
                            return Marshal.PtrToStringUni(this.pointerValue);
                        }
                        if (dataType != VarEnum.VT_BLOB)
                        {
                            goto IL_116;
                        }
                    }
                    else
                    {
                        if (dataType == VarEnum.VT_CLSID)
                        {
                            return MarshalHelpers.PtrToStructure<Guid>(this.pointerValue);
                        }
                        if (dataType != (VarEnum)4113)
                        {
                            goto IL_116;
                        }
                    }
                    return this.GetBlob();
                }
                if (dataType <= VarEnum.VT_I4)
                {
                    if (dataType == VarEnum.VT_I2)
                    {
                        return this.iVal;
                    }
                    if (dataType == VarEnum.VT_I4)
                    {
                        return this.lVal;
                    }
                }
                else if (dataType != VarEnum.VT_BOOL)
                {
                    switch (dataType)
                    {
                        case VarEnum.VT_I1:
                            return this.bVal;
                        case VarEnum.VT_UI4:
                            return this.ulVal;
                        case VarEnum.VT_I8:
                            return this.hVal;
                        case VarEnum.VT_UI8:
                            return this.uhVal;
                        case VarEnum.VT_INT:
                            return this.iVal;
                    }
                }
                else
                {
                    short num = this.boolVal;
                    if (num == -1)
                    {
                        return true;
                    }
                    if (num != 0)
                    {
                        throw new NotSupportedException("PropVariant VT_BOOL must be either -1 or 0");
                    }
                    return false;
                }
            IL_116:
                throw new NotImplementedException("PropVariant " + dataType);
            }
        }

        public static PropVariant FromLong(long value)
        {
            return new PropVariant
            {
                vt = 20,
                hVal = value
            };
        }

        private byte[] GetBlob()
        {
            byte[] array = new byte[this.blobVal.Length];
            Marshal.Copy(this.blobVal.Data, array, 0, array.Length);
            return array;
        }

        public T[] GetBlobAsArrayOf<T>()
        {
            int length = this.blobVal.Length;
            int num = Marshal.SizeOf((T)((object)Activator.CreateInstance(typeof(T))));
            if (length % num != 0)
            {
                throw new InvalidDataException(string.Format("Blob size {0} not a multiple of struct size {1}", length, num));
            }
            int num2 = length / num;
            T[] array = new T[num2];
            for (int i = 0; i < num2; i++)
            {
                array[i] = (T)((object)Activator.CreateInstance(typeof(T)));
                Marshal.PtrToStructure(new IntPtr((long)this.blobVal.Data + (long)(i * num)), array[i]);
            }
            return array;
        }

        [Obsolete("Call with pointer instead")]
        public void Clear()
        {
            PropVariantNative.PropVariantClear(ref this);
        }

        public static void Clear(IntPtr ptr)
        {
            PropVariantNative.PropVariantClear(ptr);
        }
    }
}
