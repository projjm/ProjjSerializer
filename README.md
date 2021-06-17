# ProjjSerializer

A C# (.NET Core) binary serialization and network buffer encapsulation library designed with ease of use in mind. 

# About
This library will automatically serialize any type without the need for custom attributes. The library is formed by two parts, the core serializer class TypeSerializer and the wrapper class ProjjSerializer which also handles writing and reading buffers, partial buffer reading and implementing message headers.

# Features
* Automatic serialization/deserizliation of any* type.
* Automatic headers and buffer encapsulation.
* Works with interfaces and abstract data types.
* Works with private, protected and readonly fields
* Will never serialize the same object twice into the same buffer.
* Resolves circular references.
* Optional ignoring of types/fields.

# Usage 
Before utilising this library it's important to know which features you want from it first.
If you just want Type serialization/deserialization and don't need to encapsulate the serialized data in a message payload then you will probably only be interested in the **TypeSerializer** class.

Otherwise, you will probably using the ProjjSerializer class, this will handle message binding and serialization all in one.

# ProjjSerializer
Before initialising ProjjSerializer you need to have defined an enum which represents the different types of messages you are going to be sending and receiving.
You will then need to bind each message type to an actual data type of which you will be sending.
Optionally, when binding a message type you can include a callback that will be invoked whenever a message of that type is received.
In example of a networked music player:

```csharp
public enum AppMsg
{
        PLAYBACK_START,
        PLAYBACK_PAUSE,
        SKIP_SONG,
        SONG_PLAYING
}
    
private ProjjSerializer<AppMsg> _serializer;

private NetworkClass()
{
    _serializer.BindMessageType<SongInfo>(AppMsg.SONG_PLAYING, OnReceivedSongPlaying)
    // Bind other message types here
}

private OnReceivedSongPlaying(SongInfo songInfo)
{
    // Do something with songInfo
}
```

To send a message, it's very straight forward. In an example using sockets:
```csharp
byte[] buffer = _dataHandler.GetSendBuffer(type, payload);
client.Client.Send(buffer);
```

Receiving data is even easier if you are utilising message callbacks:
```csharp
client.Client.Receive(buffer);
_serializer.ReadIncomingData(buffer);
```
If you aren't utilising the callback, receiving messages is still easy:
```csharp
client.Client.Receive(buffer);
_serializer.ReadIncomingData(buffer);

_dataHandler.ReadIncomingData(b, out bool completeData, out var parsed);

foreach (var parsed in parsed)
{
    AppMsg type = parsed.messageType;
    var data = parsed.GetData<SongInfo>();
    // Do something with the data
}

```

ProjjSerializer can be passed SerializerOptions in it's constructor, currently there is only one option which is **IgnorePartialBuffers**, by default missing bytes in a message buffer will be stored and resolved from the next buffer. This is useful for solving things like unwanted TCP data segmentation. However, if you don't want this feature then simply pass the IgnorePartialBuffers flag as mentioned.

# TypeSerializer
This class does all the heavy lifting for the actual serialization of the data you pass in, it's integral to ProjjSerializer but can also be used independently if needed.

```csharp
TypeSerializer _typeSerializer = new TypeSerializer();
byte[] buffer = _typeSerializer.Serialize(dataType, data);
_typeSerializer.Deserialize(dataType, buffer, out object result)
```
Alternatively if you don't want to deal with casting the result, you can use the generic version of Serialize/Deserialize:

```csharp
TypeSerializer _typeSerializer = new TypeSerializer();
byte[] buffer = _typeSerializer.Serialize(data);
DataType result = _typeSerializer.Deserialize<DataType>(buffer)
```

# Excluding types and fields

If you want to exclude a specific type or field from being serialized, you have two options. You can either add the custom attribute [SerializerIgnore] to any class definition or above any field. Otherwise, you can utilise the **IgnoreType** and **IgnoreField** methods in both ProjjSerializer and TypeSerializer:
```csharp
[SerializerIgnore]
class ClassToIgnore
{
  // Class implementation
}

AND / OR

_typeSerializer.IgnoreType(typeof(string));
_typeSerializer.IgnoreField(typeof(Example), "exampleField")

```

# Types Support
Almost all types are supported, currently the types that will be ignored during serialization are:
* IntPtrs
* Delegates
* Actions

Anonymous types are currently supported **but** they will **not** work across the network (or different machines). The library relies on caching type data using reflection, because anonymous type field structures are only known at runtime, data cannot be received reliably. To be safe I recommend avoiding them.
