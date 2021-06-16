using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProjjSerializer.Attributes;

namespace ProjjSerializer
{
    public class CachedTypeInfo
    {
        public Type type;
        public FieldInfo fieldInfo;
        public string fieldName;
        public bool isPrimitive;
        public bool isString;
        public bool isArray;
        public bool isPointer;
        public bool isAbstractOrInterface;
        public List<CachedTypeInfo> fieldMembers;

        private Dictionary<Type, int> derivedTypes;
        private Dictionary<int, Type> _derivedTypes;
        private Dictionary<string, Type> _runtimeDerivedTypes;

        public CachedTypeInfo()
        {
            _runtimeDerivedTypes = new Dictionary<string, Type>();
            _derivedTypes = new Dictionary<int, Type>();
            derivedTypes = new Dictionary<Type, int>();
        }

        public void SetDerivedTypes(Dictionary<Type, int> a, Dictionary<int, Type> b)
        {
            derivedTypes = a;
            _derivedTypes = b;
        }

        public void AddDerivedTypeRuntime(string name, Type type)
        {
            _runtimeDerivedTypes.Add(name, type);
        }

        public bool TryGetDerivedTypeRuntime(string name, out Type result)
        {
            if (_runtimeDerivedTypes.ContainsKey(name))
            {
                result = _runtimeDerivedTypes[name];
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        public bool HasDerivedTypeInternal(Type type) => derivedTypes.ContainsKey(type);
        public int GetDerivedTypeId(Type type) => derivedTypes[type];
        public Type GetDerivedTypeFromId(int id) => _derivedTypes[id];
    }

    class TypeCacher
    {
        private Dictionary<Type, CachedTypeInfo> _typeInfoCache;
        private HashSet<Type> _explicitlyIgnoredTypes;
        private Dictionary<Type, HashSet<FieldInfo>> _explicitlyIgnoredFields;

        public CachedTypeInfo GetCache(Type type) => _typeInfoCache[type];

        public TypeCacher()
        {
            _typeInfoCache = new Dictionary<Type, CachedTypeInfo>();
            _explicitlyIgnoredTypes = new HashSet<Type>();
            _explicitlyIgnoredFields = new Dictionary<Type, HashSet<FieldInfo>>();
        }

        public void IgnoreType(Type type) => _explicitlyIgnoredTypes.Add(type);

        public void ExploreType(Type type) => ExploreTypesRecursively(type);

        public bool TypeExplored(Type type) => _typeInfoCache.ContainsKey(type);

        public void IgnoreField(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new MissingFieldException("Field name specified in IgnoreField arguments does not exist");

            if (!_explicitlyIgnoredFields.ContainsKey(type))
                _explicitlyIgnoredFields.Add(type, new HashSet<FieldInfo>());

            _explicitlyIgnoredFields[type].Add(field);
        }

        private void ExploreTypesRecursively(Type type)
        {
            if (_typeInfoCache.ContainsKey(type))
                return;

            bool shouldIgnore = Attribute.GetCustomAttribute(type, typeof(SerializerIgnoreAttribute)) != null
                || _explicitlyIgnoredTypes.Contains(type);

            if (shouldIgnore)
                return;

            CachedTypeInfo typeCache = new CachedTypeInfo();
            _typeInfoCache.Add(type, typeCache);

            typeCache.type = type;
            typeCache.isArray = type.IsArray;
            typeCache.isPrimitive = type.IsPrimitive;
            typeCache.isString = type == typeof(string);
            typeCache.isPointer = type.IsPointer;
            typeCache.fieldMembers = new List<CachedTypeInfo>();
            typeCache.isAbstractOrInterface = type.IsAbstract || type.IsInterface;

            if (typeCache.isAbstractOrInterface)
                ExploreAllDerivedTypes(typeCache.type, typeCache);

            if (typeCache.isArray)
                ExploreTypesRecursively(type.GetElementType());

            List<FieldInfo> fieldInfos = GetSortedFields(type);
            if (fieldInfos == null || fieldInfos.Count == 0)
                return;

            foreach (FieldInfo field in fieldInfos)
            {
                Type fieldType = field.FieldType;

                shouldIgnore = Attribute.GetCustomAttribute(field, typeof(SerializerIgnoreAttribute)) != null
                    || Attribute.GetCustomAttribute(fieldType, typeof(SerializerIgnoreAttribute)) != null
                    || _explicitlyIgnoredTypes.Contains(fieldType)
                    || (_explicitlyIgnoredFields.ContainsKey(type) && _explicitlyIgnoredFields[type].Contains(field));

                if (shouldIgnore)
                    continue;

                CachedTypeInfo cfi = new CachedTypeInfo();

                if (!_typeInfoCache.ContainsKey(fieldType))
                    ExploreTypesRecursively(fieldType);

                CachedTypeInfo existing = _typeInfoCache[fieldType];

                cfi.type = fieldType;
                cfi.fieldName = field.Name;
                cfi.fieldInfo = field;

                cfi.isAbstractOrInterface = existing.isAbstractOrInterface;
                cfi.isArray = existing.isArray;
                cfi.isPrimitive = existing.isPrimitive;
                cfi.isString = existing.isString;
                cfi.fieldMembers = existing.fieldMembers;

                typeCache.fieldMembers.Add(cfi);
            }

        }

        private void ExploreAllDerivedTypes(Type baseType, CachedTypeInfo baseTypeInfo)
        {
            Assembly assembly = Assembly.GetAssembly(baseType);
            List<Type> types = Assembly.GetAssembly(baseType).GetTypes().Where(t => t != baseType && (baseType.IsAssignableFrom(t) || t.IsSubclassOf(baseType))).ToList();
            types.ForEach(type => ExploreType(type));
            types.OrderBy(type => type.FullName);
            var a = new Dictionary<Type, int>();
            var b = new Dictionary<int, Type>();
            for (int i = 0; i < types.Count; i++)
            {
                a.Add(types[i], i);
                b.Add(i, types[i]);
            }
            baseTypeInfo.SetDerivedTypes(a, b);
        }

        public CachedTypeInfo ExploreExternalDerivedType(CachedTypeInfo baseTypeInfo, string assemblyQualifiedName)
        {
            Type derived = Type.GetType(assemblyQualifiedName);
            baseTypeInfo.AddDerivedTypeRuntime(assemblyQualifiedName, derived);
            return ExploreExternalDerivedType(derived);
        }

        public CachedTypeInfo ExploreExternalDerivedType(Type derived)
        {
            if (_typeInfoCache.ContainsKey(derived))
                return _typeInfoCache[derived];

            ExploreType(derived);
            return GetCache(derived);
        }

        private List<FieldInfo> GetSortedFields(Type declaringType)
        {
            List<FieldInfo> fields = declaringType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance).ToList();

            Type currType = declaringType;
            while (currType.BaseType != null)
            {
                currType = currType.BaseType;
                fields.AddRange(currType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance).ToList());
            }

            return fields.OrderBy(field =>
                field.Name.Length.ToString() + field.FieldType.Name +
                '/' + field.FieldType.Name.Length.ToString() + field.Name
                + '{' + (field.Name.Length + field.FieldType.Name.Length).ToString() + '}'
                + '!' + field.Name.Reverse() + field.FieldType.Name.Reverse()).ToList();

            // From -  Typ emax;
            // To Key - 4Typ/3emax{7}!xamepyt

            // This should be unique for each type inside a declaring type
            // Basis of this key is that two fields can't share a name within the declaring type (AFAIK there is no way around that restriction)
            // Prefixing the key with the length and including illegal field name characters ('/',  '{') should prevent collision from types that share substrings
            // This is likely complete overkill but since I'm not sure how properties etc are converted into fields by the compiler it feels like the safer route
            // Especially since this is only executed once for each type.
        }


    }
}
