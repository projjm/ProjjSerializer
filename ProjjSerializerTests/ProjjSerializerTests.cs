using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjjSerializer;
using ProjjSerializerTests.TestExampleTypes;

namespace ProjjSerializer.Tests
{
    public enum TestMessageTypes
    {
        MessageType1,
        MessageType2,
        MessageType3
    }

    [TestClass]
    public class ProjjSerializerTests
    {
        ProjjSerializer<TestMessageTypes> serializer = new ProjjSerializer<TestMessageTypes>();

        [TestMethod]
        public void Test_Primitive_Case()
        {
            int result = 0;
            string[] result2 = null;

            serializer.BindMessageType<int>(TestMessageTypes.MessageType1, (i) => result = i);
            serializer.BindMessageType<string[]>(TestMessageTypes.MessageType2, (i) => result2 = i);

            byte[] toSend = serializer.GetSendBuffer(TestMessageTypes.MessageType1, 500);
            byte[] toSend2 = serializer.GetSendBuffer(TestMessageTypes.MessageType2, new string[] {"Test"});

            serializer.ReadIncomingData(toSend);
            serializer.ReadIncomingData(toSend2);

            Assert.AreEqual(result, 500);
            Assert.AreEqual(result2[0], "Test");
        }

        [TestMethod]
        public void Test_Complex_Object_Case()
        {
            TestClassCircularReferences testResult = null;

            TestClassCircularReferences testVal = new TestClassCircularReferences();
            testVal.val = 500;
            testVal.other = new TestClassCircularOther();
            testVal.other.initial = testVal;

            serializer.BindMessageType<TestClassCircularReferences>(TestMessageTypes.MessageType3, (r) => testResult = r);

            byte[] toSend = serializer.GetSendBuffer(TestMessageTypes.MessageType3, testVal);
            serializer.ReadIncomingData(toSend);

            Assert.AreEqual(testVal.val, testResult.other.initial.val);
            Assert.AreEqual(testVal.val, testResult.other.initial.other.initial.val);
        }

        [TestMethod]
        public void Test_Single_Partial_Buffer_Case()
        {
            var rand = new Random();
            string[] result = null;
            serializer.BindMessageType<string[]>(TestMessageTypes.MessageType1, (i) => result = i);

            string[] testVal = new string[] { "This is an example of a string", "array", "Testing for partial", "buffer" };

            byte[] toSend = serializer.GetSendBuffer(TestMessageTypes.MessageType1, testVal);
            int i = rand.Next(1, toSend.Length - 1);

            byte[] toSend1 = new byte[i];
            byte[] toSend2 = new byte[toSend.Length - i];

            Buffer.BlockCopy(toSend, 0, toSend1, 0, i);
            Buffer.BlockCopy(toSend, i, toSend2, 0, toSend.Length - i);

            serializer.ReadIncomingData(toSend1);
            Assert.AreEqual(result, null);
            serializer.ReadIncomingData(toSend2);
            Assert.AreEqual(result[1], "array");
            Assert.AreEqual(result[3], "buffer");
        }
    }
}
