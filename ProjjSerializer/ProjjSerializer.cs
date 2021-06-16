using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjjSerializer
{
    public class ProjjSerializer<MsgType>
    {
        [Flags]
        public enum SerializerOptions
        {
            IgnorePartialBuffers
        }

        private struct IncompleteDataPackage
        {
            public MsgType messageType;
            public bool receivedLength;
            public bool receivedPayload;
            public int bytesMissingLength;
            public int bytesMissingPayload;
            public byte[] incompleteLengthBuffer;
            public byte[] incompletePayloadBuffer;
        }

        public class ParsedMsgPackage
        {
            public MsgType messageType;
            public Type dataType;
            public object data;

            public T GetData<T>()
            {
                return (T)data;
            }
        }

        private TypeSerializer _typeSerializer;

        private Dictionary<MsgType, Type> _registeredTypes;
        private Dictionary<MsgType, List<Action<object>>> _messageRecvCallbacks;
        private Dictionary<MsgType, byte> _msgTypeBytes;
        private Dictionary<byte, MsgType> _byteMsgType;

        private IncompleteDataPackage? _currentlyAwaiting = null;
        private bool _ignorePartialBuffers;

        public ProjjSerializer(SerializerOptions? options = null)
        {
            if (options != null)
                SetOptions(options.Value);

            GetMappedMsgDictionary(out _msgTypeBytes, out _byteMsgType);
            _typeSerializer = new TypeSerializer();
            _registeredTypes = new Dictionary<MsgType, Type>();
            _messageRecvCallbacks = new Dictionary<MsgType, List<Action<object>>>();
        }

        public void SetOptions(SerializerOptions options)
        {
            _ignorePartialBuffers = options.HasFlag(SerializerOptions.IgnorePartialBuffers);
        }

        public void BindMessageType<DataType>(MsgType messageType)
        {
            Type type = typeof(DataType);
            if (!_registeredTypes.ContainsKey(messageType))
                _registeredTypes.Add(messageType, type);
            _typeSerializer.RegisterType(type);
        }

        public void BindMessageType<DataType>(MsgType messageType, Action<DataType> OnReceivedCallback)
        {
            if (!_messageRecvCallbacks.ContainsKey(messageType))
                _messageRecvCallbacks.Add(messageType, new List<Action<object>>());

            _messageRecvCallbacks[messageType].Add(ConvertToObjectAction(OnReceivedCallback));
            BindMessageType<DataType>(messageType);
        }

        public void IgnoreType(Type type) => _typeSerializer.IgnoreType(type);

        public void IgnoreField(Type type, string fieldName) => _typeSerializer.IgnoreField(type, fieldName);

        public void AddMsgReceivedCallback(MsgType messageType, Action<object> OnReceivedCallback)
        {
            if (!_messageRecvCallbacks.ContainsKey(messageType))
                _messageRecvCallbacks.Add(messageType, new List<Action<object>>());

            _messageRecvCallbacks[messageType].Add(OnReceivedCallback);
        }

        public void AddMsgReceivedCallbacks(MsgType messageType, Action<object>[] OnReceivedCallbacks)
        {
            if (!_messageRecvCallbacks.ContainsKey(messageType))
                _messageRecvCallbacks.Add(messageType, new List<Action<object>>());

            foreach (var action in OnReceivedCallbacks)
            {
                _messageRecvCallbacks[messageType].Add(action);
            }
        }

        public void RemoveCallbacks(MsgType messageType)
        {
            if (!_messageRecvCallbacks.ContainsKey(messageType))
                _messageRecvCallbacks[messageType].Clear();
        }

        public Action<object> ConvertToObjectAction<T>(Action<T> actionT)
        {
            return new Action<object>(o => actionT?.Invoke((T)o));
        }

        public byte[] GetSendBuffer(MsgType messageType, object data)
        {
            Type runtimeType = data.GetType();

            if (!_registeredTypes.ContainsKey(messageType))
                throw new Exception("Cannot send data type that has not been registered/bound");

            Type registeredType = _registeredTypes[messageType];

            if (_registeredTypes[messageType] != runtimeType && !runtimeType.IsSubclassOf(registeredType))
                throw new Exception("Message type and data type are not registered/bound together");

            byte[] payload = _typeSerializer.Serialize(registeredType, data);
            byte[] buffer = new byte[payload.Length + 5];
            byte[] bPayloadLength = BitConverter.GetBytes(payload.Length);

            buffer[0] = _msgTypeBytes[messageType];
            Buffer.BlockCopy(bPayloadLength, 0, buffer, 1, 4);
            Buffer.BlockCopy(payload, 0, buffer, 5, payload.Length);
            return buffer;
        }

        // Fire and Forget version of method below
        public void ReadIncomingData(byte[] rawData, bool invokeCallbacks = true)
        {
            var disp = new List<ParsedMsgPackage>();
            ReadIncomingData(rawData, out bool completeData, out disp, invokeCallbacks);
        }

        public void ReadIncomingData(byte[] rawData, out bool completeData, out List<ParsedMsgPackage> parsed, bool invokeCallbacks = true)
        {
            int bytesRead = 0;
            completeData = false;
            parsed = new List<ParsedMsgPackage>();
            IncompleteDataPackage? incomplete = null;

            while (bytesRead < rawData.Length)
            {
                bool awaiting = !_ignorePartialBuffers && _currentlyAwaiting != null;
                if (awaiting)
                    ReadPartialBuffer(rawData, ref bytesRead, parsed, out incomplete);
                else
                    ReadNewBuffer(rawData, ref bytesRead, parsed, out incomplete);
            }

            if (!_ignorePartialBuffers && incomplete != null)
                _currentlyAwaiting = incomplete;

            if (parsed.Count > 0)
                completeData = true;

            if (!invokeCallbacks)
                return;

            foreach (ParsedMsgPackage parsedMsg in parsed)
            {
                if (_messageRecvCallbacks.ContainsKey(parsedMsg.messageType))
                    _messageRecvCallbacks[parsedMsg.messageType].ForEach(action => action?.Invoke(parsedMsg.data));
            }
        }

        private void ReadPartialBuffer(byte[] rawData, ref int bytesRead, List<ParsedMsgPackage> parsedList, out IncompleteDataPackage? incomplete)
        {
            IncompleteDataPackage awaiting = _currentlyAwaiting.Value;
            MsgType messageType = awaiting.messageType;

            int length;
            byte[] completePayload = new byte[0];

            if (!awaiting.receivedLength)
            {
                byte[] incompleteLength = awaiting.incompleteLengthBuffer;
                int missing = awaiting.bytesMissingLength;

                if (!TryReadBuffer(rawData, ref bytesRead, missing, out byte[] bLength, out awaiting.bytesMissingLength))
                {
                    byte[] newIncomplete = new byte[incompleteLength.Length + bytesRead];
                    Buffer.BlockCopy(incompleteLength, 0, newIncomplete, 0, incompleteLength.Length);
                    Buffer.BlockCopy(bLength, 0, newIncomplete, incompleteLength.Length, bLength.Length);

                    awaiting.incompleteLengthBuffer = newIncomplete;
                    incomplete = awaiting;
                    return;
                }

                byte[] _length = new byte[incompleteLength.Length + bytesRead];
                Buffer.BlockCopy(incompleteLength, 0, _length, 0, incompleteLength.Length);
                Buffer.BlockCopy(bLength, 0, _length, incompleteLength.Length, bLength.Length);

                length = BitConverter.ToInt32(bLength);
                awaiting.receivedLength = true;
            }

            if (!awaiting.receivedPayload)
            {
                byte[] incompletePayload = awaiting.incompletePayloadBuffer;
                int missing = awaiting.bytesMissingPayload;

                if (!TryReadBuffer(rawData, ref bytesRead, missing, out byte[] payload, out awaiting.bytesMissingPayload))
                {
                    byte[] newIncomplete = new byte[incompletePayload.Length + bytesRead];
                    Buffer.BlockCopy(incompletePayload, 0, newIncomplete, 0, incompletePayload.Length);
                    Buffer.BlockCopy(payload, 0, newIncomplete, incompletePayload.Length, payload.Length);

                    awaiting.incompletePayloadBuffer = newIncomplete;
                    incomplete = awaiting;
                    return;
                }

                completePayload = new byte[incompletePayload.Length + bytesRead];
                Buffer.BlockCopy(incompletePayload, 0, completePayload, 0, incompletePayload.Length);
                Buffer.BlockCopy(payload, 0, completePayload, incompletePayload.Length, payload.Length);

                awaiting.receivedPayload = true;
            }

            ParsedMsgPackage parsed = new ParsedMsgPackage();
            parsed.messageType = messageType;
            parsed.dataType = _registeredTypes[messageType];
            _typeSerializer.Deserialize(parsed.dataType, completePayload, out parsed.data);

            parsedList.Add(parsed);
            _currentlyAwaiting = null;
            incomplete = null;
        }

        private void ReadNewBuffer(byte[] rawData, ref int bytesRead, List<ParsedMsgPackage> parsedList, out IncompleteDataPackage? incomplete)
        {
            IncompleteDataPackage dataPackage = new IncompleteDataPackage();
            MsgType messageType = _byteMsgType[rawData[bytesRead]];
            bytesRead += 1;

            dataPackage.messageType = messageType;
            dataPackage.receivedLength = false;
            dataPackage.receivedPayload = false;

            if (!TryReadBuffer(rawData, ref bytesRead, 4, out byte[] bLength, out dataPackage.bytesMissingLength))
            {
                dataPackage.incompleteLengthBuffer = bLength; // here is the issue
                incomplete = dataPackage;
                return;
            }
            dataPackage.receivedLength = true;

            int length = BitConverter.ToInt32(bLength);

            if (!TryReadBuffer(rawData, ref bytesRead, length, out byte[] payload, out dataPackage.bytesMissingPayload))
            {
                dataPackage.incompletePayloadBuffer = payload;
                incomplete = dataPackage;
                return;
            }
            dataPackage.receivedPayload = true;

            ParsedMsgPackage parsed = new ParsedMsgPackage();
            parsed.messageType = messageType;
            parsed.dataType = _registeredTypes[messageType];
            _typeSerializer.Deserialize(parsed.dataType, payload, out parsed.data);

            parsedList.Add(parsed);
            incomplete = null;
        }

        private bool TryReadBuffer(byte[] buffer, ref int bufferIndex, int count, out byte[] output, out int bytesMissing)
        {
            if (bufferIndex + count <= buffer.Length)
            {
                byte[] read = new byte[count];
                Buffer.BlockCopy(buffer, bufferIndex, read, 0, count);
                bytesMissing = 0;
                output = read;
                bufferIndex += count;
                return true;
            }
            else
            {
                int bytesAvailable = buffer.Length - bufferIndex;
                bytesMissing = count - bytesAvailable;
                byte[] read = new byte[bytesAvailable];
                Buffer.BlockCopy(buffer, bufferIndex, read, 0, bytesAvailable);
                output = read;
                bufferIndex += bytesAvailable;
                return false;
            }
        }


        private T GetTypeFromObj<T>(object data)
        {
            return (T)data;
        }

        private void GetMappedMsgDictionary(out Dictionary<MsgType, byte> forward, out Dictionary<byte, MsgType> backward)
        {
            forward = new Dictionary<MsgType, byte>();
            backward = new Dictionary<byte, MsgType>();

            MsgType[] vals = Enum.GetValues(typeof(MsgType))
                .Cast<MsgType>()
                .OrderBy(i => i.ToString())
                .ToArray();

            for (byte i = 0; i < vals.Length; i++)
            {
                forward.Add(vals[i], i);
                backward.Add(i, vals[i]);
            }
        }

    }
}
