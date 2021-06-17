using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace ProjjSerializer
{
    public class TypeSerializer
    {
        private bool _registeredFirstType;
        private TypeCacher _typeCacher;
        private ObjectIDGenerator _idGenerator;
        private Dictionary<long, int> _serializedObjects;
        private Dictionary<int, object> _deserializedObjects;

        public TypeSerializer()
        {
            _typeCacher = new TypeCacher(); ;
            _idGenerator = new ObjectIDGenerator();
            _serializedObjects = new Dictionary<long, int>(); // id, buffer position
            _deserializedObjects = new Dictionary<int, object>(); // pos, object ref
        }

        public void RegisterType(Type type)
        {
            _registeredFirstType = true;
            _typeCacher.ExploreType(type);
        }

        public void IgnoreType(Type type)
        {
            if (_registeredFirstType)
                throw new Exception("All explicit ignore type calls must be made before any type registration");

            _typeCacher.IgnoreType(type);
        }

        public void IgnoreField(Type type, string fieldName)
        {
            if (_registeredFirstType)
                throw new Exception("All explicit ignore field calls must be made before any type registration");

            _typeCacher.IgnoreField(type, fieldName);
        }

        #region Core Serialization Logic

        public byte[] Serialize(Type rootType, object data)
        {
            if (!_typeCacher.TypeExplored(rootType))
                throw new Exception("Cannot serialize data type that has not been registered");

            List<byte> buffer = new List<byte>();
            CachedTypeInfo cacheTypeInfo = _typeCacher.GetCache(rootType);
            _serializedObjects.Clear();
            SerializeRecursively(cacheTypeInfo, buffer, data, true);
            return buffer.ToArray();
        }

        private void SerializeRecursively(CachedTypeInfo typeInfo, List<byte> buffer, object data, bool generateObjId = false)
        {
            if (IsInvalidType(typeInfo))
                return;

            if (data == null)
            {
                SerializeValidObjectFlag(buffer, false);
                return;
            }
            else
            {
                SerializeValidObjectFlag(buffer, true);
            }

            if (typeInfo.isPrimitive)
            {
                SerializePrimitiveType(typeInfo.type, buffer, data);
                return;
            }

            if (typeInfo.isArray)
            {
                SerializeArrayType(typeInfo.type, buffer, data);
                return;
            }

            if (typeInfo.isString) // Built in reference type
            {
                SerializeString(buffer, data);
                return;
            }

            Type runtimeType = data.GetType();

            Type nullable = Nullable.GetUnderlyingType(typeInfo.type);
            if (nullable != null)
            {
                SerializeRecursively(_typeCacher.GetCache(nullable), buffer, data);
                return;
            }

            bool isSubclass = runtimeType.IsSubclassOf(typeInfo.type);
            if (isSubclass)
                SerializeSubClassTypeFlag(buffer, true);
            else
                SerializeSubClassTypeFlag(buffer, false);

            if (typeInfo.isAbstractOrInterface || isSubclass)
            {
                CachedTypeInfo dTypeInfo;
                if (typeInfo.HasDerivedTypeInternal(runtimeType)) // Internal assembly derived type
                {
                    dTypeInfo = _typeCacher.GetCache(runtimeType);
                    SerializeExternalTypeFlag(buffer, false);
                    SerializeDerivedType(typeInfo, dTypeInfo, buffer);
                }
                else // External assembly derived type
                {
                    SerializeExternalTypeFlag(buffer, true);
                    dTypeInfo = _typeCacher.ExploreExternalDerivedType(runtimeType);
                    SerializeExternalDerivedTypeName(runtimeType, buffer);
                }

                SerializeRecursively(dTypeInfo, buffer, data);
                return;
            }

            long id = _idGenerator.GetId(data, out bool firstTime);
            if (_serializedObjects.ContainsKey(id))
            {
                int pos = _serializedObjects[id];
                SerializeExistingObjectFlag(buffer, true);
                buffer.AddRangeAndPrefixSize(BitConverter.GetBytes(pos));
                return;
            }
            else
            {
                SerializeExistingObjectFlag(buffer, false);
                int objectPos = buffer.Count;
                _serializedObjects.Add(id, objectPos);
            }

            if (!HasFieldMembers(typeInfo))
                return;

            SerializeClassFields(typeInfo, buffer, data);
        }

        #endregion

        #region Deserialization

        public void Deserialize(Type type, byte[] payload, out object result)
        {
            CachedTypeInfo cacheTypeInfo = _typeCacher.GetCache(type);
            int pos = 0;
            _deserializedObjects.Clear();
            result = DeserializeRecursively(cacheTypeInfo, payload, ref pos);
        }

        private object DeserializeRecursively(CachedTypeInfo typeInfo, byte[] data, ref int pos)
        {
            if (IsInvalidType(typeInfo))
                return null;

            bool validObject = ReadFlag(data, ref pos);

            if (!validObject)
                return null;

            if (typeInfo.isPrimitive)
                return DeserializePrimitiveType(typeInfo.type, data, ref pos);

            if (typeInfo.isArray)
                return DeserializeArrayType(typeInfo.type, data, ref pos);

            if (typeInfo.isString) // Built in reference type
                return DeserializeString(data, ref pos);

            Type runtimeType = data.GetType();

            Type nullable = Nullable.GetUnderlyingType(typeInfo.type);

            if (nullable != null)
                return DeserializeRecursively(_typeCacher.GetCache(nullable), data, ref pos);

            bool isSubclass = ReadFlag(data, ref pos);

            if (typeInfo.isAbstractOrInterface || isSubclass)
            {
                bool isExternalType = ReadFlag(data, ref pos);
                if (!isExternalType)
                {
                    Type derivedType = ReadDerivedType(typeInfo, data, ref pos);
                    return DeserializeRecursively(_typeCacher.GetCache(derivedType), data, ref pos);
                }
                else
                {
                    string typeName = ReadExternalDerivedTypeName(data, ref pos);
                    if (typeInfo.TryGetDerivedTypeRuntime(typeName, out Type result))
                    {
                        return DeserializeRecursively(_typeCacher.GetCache(result), data, ref pos);
                    }
                    else
                    {
                        CachedTypeInfo derived = RegisterExternalDeriviedType(typeInfo, typeName);
                        return DeserializeRecursively(derived, data, ref pos);
                    }
                }
            }

            bool existingObj = ReadFlag(data, ref pos);
            if (existingObj)
            {
                int objectPos = (int)DeserializePrimitiveType(typeof(int), data, ref pos);
                object old = _deserializedObjects[objectPos];
                return old;
            }

            object instance = CreateInstance(typeInfo.type);

            _deserializedObjects.Add(pos, instance);

            if (!HasFieldMembers(typeInfo))
                return instance;

            DeserializeClassFields(typeInfo, instance, data, ref pos);
            return instance;
        }

        #endregion

        #region Serialization Helpers

        private object CreateInstance(Type type) => FormatterServices.GetUninitializedObject(type);

        private bool HasFieldMembers(CachedTypeInfo typeInfo) => typeInfo.fieldMembers != null && typeInfo.fieldMembers.Count != 0;

        private void SerializeValidObjectFlag(List<byte> buffer, bool flagValue) => buffer.Add(flagValue ? (byte)1 : (byte)0);

        private void SerializeExistingObjectFlag(List<byte> buffer, bool flagValue) => buffer.Add(flagValue ? (byte)1 : (byte)0);

        private void SerializeExternalTypeFlag(List<byte> buffer, bool flagValue) => buffer.Add(flagValue ? (byte)1 : (byte)0);

        private void SerializeSubClassTypeFlag(List<byte> buffer, bool flagValue) => buffer.Add(flagValue ? (byte)1 : (byte)0);

        private bool IsInvalidType(CachedTypeInfo typeInfo)
        {
            if (typeInfo.isPointer)
                return true;

            if (typeInfo.type == typeof(IntPtr))
                return true;

            if (typeInfo.type == typeof(Action))
                return true;

            if (typeInfo.type == typeof(Delegate))
                return true;

            return false;
        }

        private int ReadDataLength(byte[] data, ref int pos, bool shouldMovePos = true)
        {
            byte[] lengthBytes = new byte[4];
            Buffer.BlockCopy(data, pos, lengthBytes, 0, 4);
            int byteLength = BitConverter.ToInt32(lengthBytes);

            if (shouldMovePos)
                pos += 4;

            return byteLength;
        }

        private void SerializeExternalDerivedTypeName(Type deriviedType, List<byte> buffer)
        {
            string name = deriviedType.AssemblyQualifiedName;
            SerializeString(buffer, name);
        }

        private string ReadExternalDerivedTypeName(byte[] data, ref int pos)
        {
            return (string)DeserializeString(data, ref pos);
        }

        private CachedTypeInfo RegisterExternalDeriviedType(CachedTypeInfo baseType, string name)
        {
            return _typeCacher.ExploreExternalDerivedType(baseType, name);
        }

        private void SerializeDerivedType(CachedTypeInfo baseType, CachedTypeInfo derivedType, List<byte> buffer)
        {
            buffer.AddRange(BitConverter.GetBytes(baseType.GetDerivedTypeId(derivedType.type)));
        }

        private Type ReadDerivedType(CachedTypeInfo baseType, byte[] data, ref int pos, bool shouldMovePos = true)
        {
            byte[] typeBytes = new byte[4];
            Buffer.BlockCopy(data, pos, typeBytes, 0, 4);
            int typeId = BitConverter.ToInt32(typeBytes);

            Type derived = baseType.GetDerivedTypeFromId(typeId);
            if (shouldMovePos)
                pos += 4;

            return derived;
        }

        public bool ReadFlag(byte[] data, ref int pos, bool shouldMovePos = true)
        {
            byte val = data[pos];
            pos += 1;
            return (val == 1) ? true : false;
        }

        private void SerializeClassFields(CachedTypeInfo rootTypeInfo, List<byte> buffer, object data)
        {
            Type debugType = data.GetType();

            foreach (CachedTypeInfo childCacheTypeInfo in rootTypeInfo.fieldMembers)
            {
                FieldInfo fieldInfo = childCacheTypeInfo.fieldInfo;

                if (fieldInfo.IsLiteral)
                    continue;

                if (fieldInfo.IsStatic && fieldInfo.IsInitOnly)
                    continue;

                object fieldData = fieldInfo.GetValue(data);
                SerializeRecursively(childCacheTypeInfo, buffer, fieldData);
            }
        }

        private object GetNullableFieldValue(FieldInfo fieldInfo, CachedTypeInfo nullableObjectInfo, object nullableObject)
        {
            Type type = nullableObjectInfo.type;
            Type underlyingType = Nullable.GetUnderlyingType(type);

            Type thisType = typeof(TypeSerializer);
            MethodInfo method = thisType.GetMethod(nameof(GenericFieldGetValue), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo generic = method.MakeGenericMethod(type);
            return generic.Invoke(this, new object[] { fieldInfo, nullableObject });

        }

        private object GenericFieldGetValue<ObjectType>(FieldInfo fieldInfo, object obj)
        {
            return fieldInfo.GetValue(((ObjectType)obj));
        }

        private void DeserializeClassFields(CachedTypeInfo rootTypeInfo, object rootInstance, byte[] data, ref int pos)
        {
            foreach (CachedTypeInfo childCacheTypeInfo in rootTypeInfo.fieldMembers)
            {
                FieldInfo fieldInfo = childCacheTypeInfo.fieldInfo;

                if (fieldInfo.IsLiteral)
                    continue;

                if (fieldInfo.IsStatic && fieldInfo.IsInitOnly)
                    continue;

                object fieldVal = DeserializeRecursively(childCacheTypeInfo, data, ref pos);

                if (childCacheTypeInfo.fieldInfo.IsStatic)
                    fieldInfo.SetValue(null, fieldVal);
                else
                    fieldInfo.SetValue(rootInstance, fieldVal);
            }
        }

        private void SerializeString(List<byte> buffer, object data)
        {
            if (data == null)
            {
                SerializeValidObjectFlag(buffer, false);
                return;
            }
            else
            {
                SerializeValidObjectFlag(buffer, true);
            }

            buffer.AddRangeAndPrefixSize(Encoding.ASCII.GetBytes((string)data));
        }

        private object DeserializeString(byte[] data, ref int pos)
        {
            bool validObject = ReadFlag(data, ref pos);
            if (!validObject)
                return null;

            int stringByteLength = ReadDataLength(data, ref pos);
            if (stringByteLength == 0)
                return null;

            byte[] stringBytes = new byte[stringByteLength];
            Buffer.BlockCopy(data, pos, stringBytes, 0, stringByteLength);
            pos += stringByteLength;

            return Encoding.ASCII.GetString(stringBytes);
        }

        private void SerializePrimitiveType(Type primitiveType, List<byte> buffer, object data)
        {
            buffer.AddRangeAndPrefixSize(GetPrimitiveBytes(primitiveType, data));
        }

        private object DeserializePrimitiveType(Type primitiveType, byte[] data, ref int pos)
        {
            int byteLength = ReadDataLength(data, ref pos);

            if (byteLength == 0)
                return null;

            byte[] primitiveBytes = new byte[byteLength];
            Buffer.BlockCopy(data, pos, primitiveBytes, 0, byteLength);
            pos += byteLength;

            return GetPrimitiveFromBytes(primitiveType, primitiveBytes);
        }

        private void SerializeArrayType(Type arrayType, List<byte> buffer, object data)
        {
            Type elementType = arrayType.GetElementType();

            if (elementType.IsPrimitive)
            {
                Type thisType = typeof(TypeSerializer);
                MethodInfo method = thisType.GetMethod(nameof(GetPrimitiveArrayBytes), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo generic = method.MakeGenericMethod(elementType);
                byte[] typeArray = (byte[])generic.Invoke(this, new object[] { data });
                buffer.AddRangeAndPrefixSize(typeArray);
            }
            else if (elementType == typeof(string))
            {
                List<byte> stringArray = new List<byte>();
                string[] source = (string[])data;

                Array.ForEach(source, s =>
                {
                    bool isValid = s != null;
                    SerializeValidObjectFlag(stringArray, isValid);

                    if (isValid)
                        stringArray.AddRangeAndPrefixSize(Encoding.ASCII.GetBytes(s));
                });

                buffer.AddRangeAndPrefixSize(stringArray.ToArray());
            }
            else
            {
                Type thisType = typeof(TypeSerializer);
                MethodInfo method = thisType.GetMethod(nameof(RecursivelySerializeTypeArray), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo generic = method.MakeGenericMethod(elementType);
                generic.Invoke(this, new object[] { buffer, data });
            }
        }

        private object DeserializeArrayType(Type arrayType, byte[] data, ref int pos)
        {
            Type elementType = arrayType.GetElementType();

            if (elementType.IsPrimitive)
            {
                int arrayLengthBytes = ReadDataLength(data, ref pos);

                byte[] arrayBytes = new byte[arrayLengthBytes];
                Buffer.BlockCopy(data, pos, arrayBytes, 0, arrayLengthBytes);
                pos += arrayLengthBytes;

                Type thisType = typeof(TypeSerializer);
                MethodInfo method = thisType.GetMethod(nameof(GetPrimitiveArray), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo generic = method.MakeGenericMethod(elementType);
                return generic.Invoke(this, new object[] { arrayBytes });
            }
            else if (elementType == typeof(string))
            {

                int stringArrayBytes = ReadDataLength(data, ref pos);

                List<string> result = new List<string>();
                int currentPos = pos;
                while (pos < currentPos + stringArrayBytes)
                {
                    bool validString = ReadFlag(data, ref pos);
                    if (!validString)
                    {
                        result.Add(null);
                        continue;
                    }

                    int stringLengthBytes = ReadDataLength(data, ref pos);
                    if (stringLengthBytes == 0)
                    {
                        result.Add(string.Empty);
                        continue;
                    }

                    byte[] stringBytes = new byte[stringLengthBytes];
                    Buffer.BlockCopy(data, pos, stringBytes, 0, stringLengthBytes);
                    pos += stringLengthBytes;
                    result.Add(Encoding.ASCII.GetString(stringBytes));
                }
                return result.ToArray();
            }
            else
            {
                Type thisType = typeof(TypeSerializer);
                MethodInfo method = thisType.GetMethod(nameof(RecursivelyDeserializeTypeArray), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo generic = method.MakeGenericMethod(elementType);
                object[] args = new object[] { data, pos };
                object returnVal = generic.Invoke(this, args);
                pos = (int)args[1];
                return returnVal;
            }
        }

        private void RecursivelySerializeTypeArray<T>(List<byte> buffer, object array)
        {
            T[] typeArray = (T[])array;
            buffer.AddRange(BitConverter.GetBytes(typeArray.Length));
            foreach (T val in typeArray)
            {
                if (val == null)
                {
                    SerializeValidObjectFlag(buffer, false);
                    continue;
                }
                else
                {
                    SerializeValidObjectFlag(buffer, true);
                }

                SerializeRecursively(_typeCacher.GetCache(typeof(T)), buffer, val);
            }
        }

        private T[] RecursivelyDeserializeTypeArray<T>(byte[] data, ref int pos)
        {
            Type type = typeof(T);
            int arrayLength = ReadDataLength(data, ref pos);

            T[] typeArray = new T[arrayLength];

            for (int i = 0; i < arrayLength; i++)
            {
                bool validObject = ReadFlag(data, ref pos);

                if (!validObject)
                {
                    typeArray[i] = default(T); //or null?
                    continue;
                }

                typeArray[i] = (T)DeserializeRecursively(_typeCacher.GetCache(type), data, ref pos);
            }

            return typeArray;
        }

        private object GetPrimitiveArray<T>(byte[] data)
        {
            Type type = typeof(T);
            int elementSize = GetPrimitiveTypeSize(type);
            List<T> typeList = new List<T>();

            int bytesRead = 0;
            while (bytesRead < data.Length)
            {
                byte[] valBytes = new byte[elementSize];
                Buffer.BlockCopy(data, bytesRead, valBytes, 0, elementSize);
                object val = GetPrimitiveFromBytes(type, valBytes);
                typeList.Add((T)val);
                bytesRead += elementSize;
            }

            return typeList.ToArray();
        }

        private byte[] GetPrimitiveArrayBytes<T>(object obj)
        {
            Type type = typeof(T);
            T[] array = (T[])obj;
            int primitiveSize = GetPrimitiveTypeSize(typeof(T));
            byte[] arrayBytes = new byte[array.Length * primitiveSize];

            for (int i = 0; i < array.Length; i++)
            {
                Buffer.BlockCopy(GetPrimitiveBytes(type, array[i]), 0, arrayBytes, i * primitiveSize, primitiveSize);
            }

            return arrayBytes;
        }

        private byte[] GetPrimitiveBytes(Type type, object obj)
        {
            switch (type)
            {
                case Type _ when type == typeof(byte):
                    return new byte[] { (byte)obj };
                case Type _ when type == typeof(sbyte):
                    return new byte[] { unchecked((byte)(sbyte)obj)};
                case Type _ when type == typeof(short):
                    return BitConverter.GetBytes((short)obj);
                case Type _ when type == typeof(ushort):
                    return BitConverter.GetBytes((ushort)obj);
                case Type _ when type == typeof(int):
                    return BitConverter.GetBytes((int)obj);
                case Type _ when type == typeof(uint):
                    return BitConverter.GetBytes((uint)obj);
                case Type _ when type == typeof(long):
                    return BitConverter.GetBytes((long)obj);
                case Type _ when type == typeof(ulong):
                    return BitConverter.GetBytes((ulong)obj);
                case Type _ when type == typeof(float):
                    return BitConverter.GetBytes((float)obj);
                case Type _ when type == typeof(double):
                    return BitConverter.GetBytes((double)obj);
                case Type _ when type == typeof(decimal):
                    return BitConverter.GetBytes(Convert.ToDouble(obj));
                case Type _ when type == typeof(bool):
                    return BitConverter.GetBytes((bool)obj);
                case Type _ when type == typeof(char):
                    return BitConverter.GetBytes((char)obj);
                default:
                    return null;
            }
        }

        private int GetPrimitiveTypeSize(Type type)
        {
            int typeSize;
            switch (type)
            {
                case Type _ when type == typeof(byte):
                    typeSize = 1;
                    break;
                case Type _ when type == typeof(sbyte):
                    typeSize = 1;
                    break;
                case Type _ when type == typeof(short):
                    typeSize = 2;
                    break;
                case Type _ when type == typeof(ushort):
                    typeSize = 2;
                    break;
                case Type _ when type == typeof(int):
                    typeSize = 4;
                    break;
                case Type _ when type == typeof(uint):
                    typeSize = 4;
                    break;
                case Type _ when type == typeof(long):
                    typeSize = 8;
                    break;
                case Type _ when type == typeof(ulong):
                    typeSize = 8;
                    break;
                case Type _ when type == typeof(float):
                    typeSize = 4;
                    break;
                case Type _ when type == typeof(double):
                    typeSize = 8;
                    break;
                case Type _ when type == typeof(decimal):
                    typeSize = 24;
                    break;
                case Type _ when type == typeof(bool):
                    typeSize = 1;
                    break;
                case Type _ when type == typeof(char):
                    typeSize = 2;
                    break;
                default:
                    typeSize = -1;
                    break;
            }

            if (typeSize == -1)
                throw new ArgumentException("Argument type is not primitive");
            return typeSize;
        }

        private object GetPrimitiveFromBytes(Type type, byte[] data)
        {
            switch (type)
            {
                case Type _ when type == typeof(byte):
                    return (object)data[0];
                case Type _ when type == typeof(sbyte):
                    return (object)(sbyte)data[0];
                case Type _ when type == typeof(short):
                    return (object)(BitConverter.ToInt16(data));
                case Type _ when type == typeof(ushort):
                    return (object)(BitConverter.ToUInt16(data));
                case Type _ when type == typeof(int):
                    return (object)(BitConverter.ToInt32(data));
                case Type _ when type == typeof(uint):
                    return (object)(BitConverter.ToUInt32(data));
                case Type _ when type == typeof(long):
                    return (object)(BitConverter.ToInt64(data));
                case Type _ when type == typeof(ulong):
                    return (object)(BitConverter.ToUInt64(data));
                case Type _ when type == typeof(float):
                    return (object)(BitConverter.ToSingle(data));
                case Type _ when type == typeof(double):
                    return (object)(BitConverter.ToDouble(data));
                case Type _ when type == typeof(decimal):
                    return (object)(Convert.ToDecimal(BitConverter.ToDouble(data)));
                case Type _ when type == typeof(bool):
                    return (object)(BitConverter.ToBoolean(data, 0));
                case Type _ when type == typeof(char):
                    return (object)(BitConverter.ToChar(data, 0));
                default:
                    throw new ArgumentOutOfRangeException("Method type is not primitive or cannot be converted from byte array");
            }
        }

        private bool HasDefaultConstructor(Type t)
        {
            return t.IsValueType || t.GetConstructor(Type.EmptyTypes) != null;
        }

        private object GetDefault(Type type)
        {
            if (type.IsValueType)
                return Activator.CreateInstance(type);

            return null;
        }

        #endregion


        #region Testing Public Methods

        public T DebugTestType<T>(T example)
        {
            Type type = typeof(T);
            RegisterType(type);
            Deserialize(type, Serialize(type, example), out object result);
            return (T)result;
        }

        public T DebugTestTypeExplicit<T>(object example)
        {
            Type type = typeof(T);
            RegisterType(type);
            Deserialize(type, Serialize(type, example), out object result);
            return (T)result;
        }

        #endregion
    }

    public static class ListExtensions
    {
        public static void AddRangeAndPrefixSize(this List<byte> list, byte[] toAdd)
        {
            list.AddRange(BitConverter.GetBytes(toAdd.Length));
            list.AddRange(toAdd);
        }
    }
}