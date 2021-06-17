using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using ProjjSerializerTests.TestExampleTypes;

namespace ProjjSerializer.Tests
{
    [TestClass]
    public class TypeSerializerTests
    {
        TypeSerializer _typeSerializer = new TypeSerializer();

        [TestMethod]
        public void Serialize_Primitive_Types()
        {
            // IntPtr and UIntPtr not currently supported

            bool boolVal = true;
            byte byteVal = 255;
            SByte sByteVal = -50;
            Int16 int16Val = 10000;
            UInt16 uInt16Val = 1000;
            Int32 int32Val = 1000000; // aka int
            UInt32 uInt32Val = 1000000;
            Int64 int64Val = 8000000000;
            UInt64 uInt64Val = 16000000000;
            Char charVal = 'b';
            Double doubleVal = 15.55; // 64 bit
            Single singleVal = 20.5f; // 32 bit aka float


            bool boolResult = TestType(boolVal);
            byte byteResult = TestType(byteVal);
            SByte sByteResult = TestType(sByteVal);
            Int16 int16Result = TestType(int16Val);
            UInt16 uInt16Result = TestType(uInt16Val); ;
            Int32 int32Result = TestType(int32Val);
            UInt32 uInt32Result = TestType(uInt32Val);
            Int64 int64Result = TestType(int64Val);
            UInt64 uInt64Result = TestType(uInt64Val);
            Char charResult = TestType(charVal);
            Double doubleResult = TestType(doubleVal);
            Single singleResult = TestType(singleVal);

            Assert.AreEqual(boolVal, boolResult);
            Assert.AreEqual(byteVal, byteResult);
            Assert.AreEqual(sByteVal, sByteResult);
            Assert.AreEqual(int16Val, int16Result);
            Assert.AreEqual(uInt16Val, uInt16Result);
            Assert.AreEqual(int32Val, int32Result);
            Assert.AreEqual(uInt32Val, uInt32Result);
            Assert.AreEqual(int64Val, int64Result);
            Assert.AreEqual(uInt64Val, uInt64Result);
            Assert.AreEqual(charVal, charResult);
            Assert.AreEqual(doubleVal, doubleResult);
            Assert.AreEqual(singleVal, singleResult);
        }

        [TestMethod]
        public void Serialize_Primitive_Arrays()
        {
            bool[] boolVal = new bool[] { true, false };
            byte[] byteVal = new byte[] { 255, 100, 50, 10 };
            SByte[] sByteVal = new SByte[] { -50, -10, 5, 60 };
            Int16[] int16Val = new Int16[] { 10000, 500, 100 };
            UInt16[] uInt16Val = new UInt16[] { 1000, 100, 1000, 50 };
            Int32[] int32Val = new Int32[] { 1000000, 50, 100, -5000 };
            UInt32[] uInt32Val = new UInt32[] { 1000000, 0, 0, 100, 50 };
            Int64[] int64Val = new Int64[] { 8000000000, 50000000, -500000, -10 };
            UInt64[] uInt64Val = new UInt64[] { 16000000000, 1, 2, 3, 40000000 };
            Char[] charVal = new char[] { 'b', 'c', 'z', 't' };
            Double[] doubleVal = new Double[] { 15.55, 18.04, 12.01, -150.6 };
            Single[] singleVal = new Single[] { 20.5f, 10.0f, -50.75f };


            bool[] boolResult = TestType(boolVal);
            byte[] byteResult = TestType(byteVal);
            SByte[] sByteResult = TestType(sByteVal);
            Int16[] int16Result = TestType(int16Val);
            UInt16[] uInt16Result = TestType(uInt16Val); ;
            Int32[] int32Result = TestType(int32Val);
            UInt32[] uInt32Result = TestType(uInt32Val);
            Int64[] int64Result = TestType(int64Val);
            UInt64[] uInt64Result = TestType(uInt64Val);
            Char[] charResult = TestType(charVal);
            Double[] doubleResult = TestType(doubleVal);
            Single[] singleResult = TestType(singleVal);

            Assert.IsTrue(AreEqualArrays(boolVal, boolResult));
            Assert.IsTrue(AreEqualArrays(byteVal, byteResult));
            Assert.IsTrue(AreEqualArrays(sByteVal, sByteResult));
            Assert.IsTrue(AreEqualArrays(int16Val, int16Result));
            Assert.IsTrue(AreEqualArrays(uInt16Val, uInt16Result));
            Assert.IsTrue(AreEqualArrays(int32Val, int32Result));
            Assert.IsTrue(AreEqualArrays(uInt32Val, uInt32Result));
            Assert.IsTrue(AreEqualArrays(int64Val, int64Result));
            Assert.IsTrue(AreEqualArrays(uInt64Val, uInt64Result));
            Assert.IsTrue(AreEqualArrays(charVal, charResult));
            Assert.IsTrue(AreEqualArrays(doubleVal, doubleResult));
            Assert.IsTrue(AreEqualArrays(singleVal, singleResult));
        }


        [TestMethod]
        public void Serialize_String()
        {
            string stringVal = "Testing";
            string stringResult = TestType(stringVal);
            Assert.AreEqual(stringVal, stringResult);
        }

        [TestMethod]
        public void Serialize_String_Array()
        {
            string[] stringVal = new string[] { "Unit", "Testing", "String", "Array" };
            string[] stringResult = TestType(stringVal);
            Assert.IsTrue(AreEqualArrays(stringVal, stringResult));
        }
 
        [TestMethod]
        public void Serialize_Enum()
        {
            TestEnum enumVal = TestEnum.EnumVal2;
            TestEnum enumResult = TestType(enumVal);
            Assert.AreEqual(enumVal, enumResult);
        }


        [TestMethod]
        public void Serialize_Struct_Public_Fields()
        {
            TestStructPublic structVal = new TestStructPublic() { a = 5, b = 500, c = "Test" };

            TestStructPublic structResult = TestType(structVal);

            Assert.AreEqual(structVal.a, structResult.a);
            Assert.AreEqual(structVal.b, structResult.b);
            Assert.AreEqual(structVal.c, structResult.c);
        }


        [TestMethod]
        public void Serialize_Struct_Private_Fields()
        {
            TestStructPrivate structVal = new TestStructPrivate();
            structVal.SetA(10);
            structVal.SetB(100);
            structVal.SetC("TestString");

            TestStructPrivate structResult = TestType(structVal);

            Assert.AreEqual(structVal.GetA(), structResult.GetA());
            Assert.AreEqual(structVal.GetB(), structResult.GetB());
            Assert.AreEqual(structVal.GetC(), structResult.GetC());
        }


        [TestMethod]
        public void Serialize_Class_Public_Fields()
        {
            TestClassPublicFields testClassVal = new TestClassPublicFields();
            testClassVal.a = 5;
            testClassVal.b = 'z';

            TestClassPublicFields testClassResult = TestType(testClassVal);

            Assert.AreEqual(testClassVal.a, testClassResult.a);
            Assert.AreEqual(testClassVal.b, testClassResult.b);
        }

        [TestMethod]
        public void Serialize_Class_Private_Fields()
        {
            TestClassPrivateFields testClassVal = new TestClassPrivateFields();
            testClassVal.SetA(5);
            testClassVal.SetB('z');

            TestClassPrivateFields testClassResult = TestType(testClassVal);

            Assert.AreEqual(testClassVal.GetA(), testClassResult.GetA());
            Assert.AreEqual(testClassVal.GetB(), testClassResult.GetB());
        }

        [TestMethod]
        public void Serialize_Class_Generic()
        {
            TestClassGeneric<string> stringGenericVal = new TestClassGeneric<string>("Test1", "Test2");
            TestClassGeneric<int> intGenericVal = new TestClassGeneric<int>(1, 2);

            TestClassGeneric<string> stringGenericResult = TestType(stringGenericVal);
            TestClassGeneric<int> intGenericResult = TestType(intGenericVal);

            Assert.AreEqual(stringGenericVal.testVal1, stringGenericResult.testVal1);
            Assert.AreEqual(stringGenericVal.testVal2, stringGenericResult.testVal2);
            Assert.AreEqual(intGenericVal.testVal1, intGenericResult.testVal1);
            Assert.AreEqual(intGenericVal.testVal2, intGenericResult.testVal2);
        }

        [TestMethod]
        public void Serialize_Collection_Types()
        {
            List<int> intListVal = new List<int>() { 1, 2, 3, 4 };
            Dictionary<string, int> stringDictVal = new Dictionary<string, int>() { { "test1", 5 }, { "test2", 5 } };
            HashSet<float> floatHashsetVal = new HashSet<float>() { 5.0f, 10.0f, 500.51f };
            LinkedList<char> charLinkedListVal = new LinkedList<char>();
            charLinkedListVal.AddLast('#');
            charLinkedListVal.AddFirst('h');
            Queue<long> longQueueVal = new Queue<long>();
            longQueueVal.Enqueue(50000);
            longQueueVal.Enqueue(1239060);
            Stack<bool> boolStackVal = new Stack<bool>();
            boolStackVal.Push(true);
            boolStackVal.Push(false);

            List<int> intListResult = TestType(intListVal);
            Dictionary<string, int> stringDictResult = TestType(stringDictVal);
            HashSet<float> floatHashsetResult = TestType(floatHashsetVal);
            LinkedList<char> charLinkedListResult = TestType(charLinkedListVal);
            Queue<long> longQueueResult = TestType(longQueueVal);
            Stack<bool> boolStackResult = TestType(boolStackVal);

            Assert.IsTrue(intListVal.SequenceEqual(intListResult));
            Assert.IsTrue(stringDictVal.SequenceEqual(stringDictResult));
            Assert.IsTrue(floatHashsetVal.SequenceEqual(floatHashsetResult));
            Assert.IsTrue(charLinkedListVal.SequenceEqual(charLinkedListResult));
            Assert.IsTrue(longQueueVal.SequenceEqual(longQueueResult));
            Assert.IsTrue(boolStackVal.SequenceEqual(boolStackResult));
        }

        [TestMethod]
        public void Serialize_Class_Readonly_Fields()
        {
            TestClassReadonlyFields testVal = new TestClassReadonlyFields("Readonly", 50);
            TestClassReadonlyFields testResult = TestType(testVal);
            Assert.AreEqual(testVal.readOnlyInt, testResult.readOnlyInt);
            Assert.AreEqual(testVal.readOnlyString, testResult.readOnlyString);
        }

        [TestMethod]
        public void Serialize_Anonymous_Types()
        {
            var anonymousVal = new { Val1 = "Test", Val2 = 500 };
            var anonymousResult = TestType(anonymousVal);

            Assert.AreEqual(anonymousVal.Val1, anonymousResult.Val1);
            Assert.AreEqual(anonymousVal.Val2, anonymousResult.Val2);
        }

        [TestMethod]
        public void Serialize_Invalid_Types()
        {
            // Should ignore invalid types
            TestClassInvalidFields testVal = new TestClassInvalidFields();
            testVal.action += () => Console.WriteLine("Test");
            testVal.ptr = new IntPtr(1000);
            testVal.intVal = 999;

            TestClassInvalidFields testResult = TestType(testVal);

            Assert.AreEqual(testVal.intVal, testResult.intVal);
            Assert.AreEqual(testResult.action, null);
            Assert.AreEqual(testResult.ptr, IntPtr.Zero);
        }


        [TestMethod]
        public void Serialize_Ciruclar_Referring_Class()
        {
            TestClassCircularReferences testVal = new TestClassCircularReferences();
            testVal.val = 500;
            testVal.other = new TestClassCircularOther();
            testVal.other.initial = testVal;

            TestClassCircularReferences testResult = TestType(testVal);

            Assert.AreEqual(testVal.val, testResult.other.initial.val);
            Assert.AreEqual(testVal.val, testResult.other.initial.other.initial.val);
        }

        [TestMethod]
        public void Serialize_Interface_Type_Explicit()
        {
            TestInterface interfaceVal = new TestClassInheritingInterface(500);
            TestInterface interfaceResult = TestTypeExplicit<TestInterface>(interfaceVal);
            Assert.AreEqual(interfaceVal.GetValue(), interfaceResult.GetValue());
        }

        [TestMethod]
        public void Serialize_Abstract_Class()
        {
            TestAbstractClass abstractVal = new TestInheritingAbstractClass(99, false);
            TestAbstractClass abstractResult = TestTypeExplicit<TestAbstractClass>(abstractVal);
            Assert.AreEqual(abstractVal.GetVal(), abstractResult.GetVal());
        }

        [TestMethod]
        public void Serialize_Class_With_Field_Ignore_Attribute()
        {
            TestIgnoreAttributeClass testVal = new TestIgnoreAttributeClass(5000, "Test");
            TestIgnoreAttributeClass testResult = TestType(testVal);

            Assert.AreEqual(testVal.dontIgnoreMe, testResult.dontIgnoreMe);
            Assert.AreEqual(testResult.ignoreMe, 0);
        }

        [TestMethod]
        public void Serialize_Class_With_Class_Ignore_Attribute()
        {
            TestClassIgnoreClass testVal = new TestClassIgnoreClass(99);
            testVal.ignoreMe = new TestClassIgnoreFieldClass('b');

            TestClassIgnoreClass testResult = TestType(testVal);

            Assert.AreEqual(testVal.a, testResult.a);
            Assert.AreEqual(testResult.ignoreMe, null);
        }

        [TestMethod]
        public void Serialize_Class_With_Explicit_Ignore()
        {
            TestClassExplicitIgnore testVal = new TestClassExplicitIgnore();
            testVal.name = "Test";
            testVal.ignoreMe = new ExplicitIgnoreClass();
            testVal.ignoreMe.a = 5;
            testVal.ignoreMe.b = 10;

            _typeSerializer.IgnoreType(typeof(ExplicitIgnoreClass));

            TestClassExplicitIgnore testResult = TestType(testVal);

            Assert.AreEqual(testVal.name, testResult.name);
            Assert.AreEqual(testResult.ignoreMe, null);
        }

        [TestMethod]
        public void Serialize_Class_With_Explicit_Field_Ignore()
        {
            TestClassExplicitIgnore testVal = new TestClassExplicitIgnore();
            testVal.name = "Test";
            testVal.ignoreMe = new ExplicitIgnoreClass();
            testVal.ignoreMe.a = 5;
            testVal.ignoreMe.b = 10;

            _typeSerializer.IgnoreField(typeof(TestClassExplicitIgnore), "ignoreMe");

            TestClassExplicitIgnore testResult = TestType(testVal);

            Assert.AreEqual(testVal.name, testResult.name);
            Assert.AreEqual(testResult.ignoreMe, null);
        }

        [TestMethod]
        public void Serialize_Same_Type_Multiple_Times()
        {
            BasicClass testClass = new BasicClass();
            _typeSerializer.RegisterType(typeof(BasicClass));

            byte[] bytes = _typeSerializer.Serialize(typeof(BasicClass), testClass);
            _typeSerializer.Deserialize(typeof(BasicClass), bytes, out object result);
            BasicClass res = (BasicClass)result;
            bytes = _typeSerializer.Serialize(typeof(BasicClass), testClass);
            _typeSerializer.Deserialize(typeof(BasicClass), bytes, out object result2);
            res = (BasicClass)result2;
        }

        #region Test Helper Tools

        public T TestType<T>(T testValue) => _typeSerializer.DebugTestType(testValue);

        public T TestTypeExplicit<T>(object val) => _typeSerializer.DebugTestTypeExplicit<T>(val);

        public bool AreEqualArrays<T>(T[] a, T[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }

            return true;
        }

        #endregion

    }
}