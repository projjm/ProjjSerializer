using System;
using ProjjSerializer.Attributes;

namespace ProjjSerializerTests.TestExampleTypes
{
    struct TestStructPublic
    {
        public int a;
        public long b;
        public string c;
    }

    struct TestStructPrivate
    {
        private int a;
        private long b;
        private string c;

        public void SetA(int v) => a = v;
        public void SetB(long v) => b = v;
        public void SetC(string v) => c = v;
        public int GetA() => a;
        public long GetB() => b;
        public string GetC() => c;
    }

    enum TestEnum
    {
        EnumVal1,
        EnumVal2,
        EnumVal3
    }

    class TestClassPublicFields
    {
        public int a;
        public char b;
    }

    class TestClassPrivateFields
    {
        private int a;
        private char b;
        public int SetA(int v) => a = v;
        public int SetB(char v) => b = v;
        public int GetA() => a;
        public char GetB() => b;
    }

    class TestClassReadonlyFields
    {

        public readonly string readOnlyString;
        public readonly int readOnlyInt;

        public TestClassReadonlyFields(string a, int b)
        {
            readOnlyString = a;
            readOnlyInt = b;
        }
    }

    class TestClassGeneric<T>
    {
        public T testVal1;
        public T testVal2;

        public TestClassGeneric(T a, T b)
        {
            testVal1 = a;
            testVal2 = b;
        }
    }

    class TestClassInvalidFields
    {
        public Action action;
        public IntPtr ptr;
        public int intVal;
    }

    class TestClassCircularReferences
    {
        public int val;
        public TestClassCircularOther other;
    }

    class TestClassCircularOther
    {
        public TestClassCircularReferences initial;
    }

    interface TestInterface
    {
        public int GetValue();
    }

    class TestClassInheritingInterface : TestInterface
    {
        public int val;
        public string c = "constant";
        public TestClassInheritingInterface(int a) => val = a;

        public int GetValue() => val;
    }

    abstract class TestAbstractClass
    {
        protected int a;
        protected bool b;

        public abstract int GetVal();
    }

    class TestInheritingAbstractClass : TestAbstractClass
    {
        public TestInheritingAbstractClass(int v, bool n)
        {
            a = v;
            b = n;
        }

        public override int GetVal() => a;
    }

    class TestIgnoreAttributeClass
    {
        [SerializerIgnoreAttribute]
        public int ignoreMe = 0;

        public string dontIgnoreMe;

        public TestIgnoreAttributeClass(int a, string b)
        {
            ignoreMe = a;
            dontIgnoreMe = b;
        }
    }

    class TestClassIgnoreClass
    {
        public int a;
        public TestClassIgnoreFieldClass ignoreMe;
        public TestClassIgnoreClass(int v) => a = v;
    }

    [SerializerIgnore]
    class TestClassIgnoreFieldClass
    {
        public char value;
        public TestClassIgnoreFieldClass(char c) => value = c;
    }

    class TestClassExplicitIgnore
    {
        public string name;
        public ExplicitIgnoreClass ignoreMe;
    }

    class ExplicitIgnoreClass
    {
        public int a;
        public int b;
    }


}
