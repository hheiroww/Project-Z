
// Option Strict Off

// Imports System.Collections
// Imports System.Collections.Generic
// Imports System.IO
// Imports System.Linq
// Imports System.Reflection
// Imports System.Runtime.Serialization.Formatters.Binary
// Imports Microsoft.Xna.Framework
// Imports ProjectZ.Shared.Drawing
// Imports ProjectZ.Shared.Drawing.UI

// Namespace [Shared].Serialization

// Public Class ObjectConverter

// #Region "Serialization Dictionary"

// Public Shared Property SerializationConverters As New Dictionary(Of Type, Func(Of Object, Object)) From {
// {GetType(SceneElement), AddressOf ConvertToMetaData},
// {GetType(SceneElement()), AddressOf IterateValueList},
// {GetType(ChildCollection), AddressOf ConvertToMetaData},
// {GetType(Scene), AddressOf ConvertToMetaData},
// {GetType(Point), AddressOf ConvertToMetaData},
// {GetType(Point()), AddressOf ConvertToMetaData},
// {GetType(Vector2), AddressOf ConvertToMetaData},
// {GetType(Vector2()), AddressOf ConvertToMetaData},
// {GetType(Vector3), AddressOf ConvertToMetaData},
// {GetType(Vector3()), AddressOf ConvertToMetaData},
// {GetType(Vector4), AddressOf ConvertToMetaData},
// {GetType(Vector4()), AddressOf ConvertToMetaData},
// {GetType(Color), AddressOf ConvertToMetaData},
// {GetType(Color()), AddressOf ConvertToMetaData},
// {GetType(Rectangle), AddressOf ConvertToMetaData},
// {GetType(Rectangle()), AddressOf ConvertToMetaData},
// {GetType(IList(Of SceneElement)), AddressOf IterateValueList},
// {(New SortedList(Of Integer, SceneElement)).Values.GetType, AddressOf IterateValueList}
// }

// Private Shared Function is_Array(obj As Object) As Boolean
// Dim Array As Boolean = IsArray(obj)
// Dim isEnumerableOrList As Boolean = obj.GetType.IsAssignableFrom(GetType(IEnumerable))
// Return Array Or isEnumerableOrList
// End Function

// Private Shared Function ConvertToMetaData(obj) As Object
// If is_Array(obj) Then
// Dim Result As MetaData
// Dim A As MetaData() = {}
// For Each Element As Object In obj
// A.Add(New MetaData(Element))
// Next
// Result = New MetaData(A)
// Return Result
// Else
// Return New MetaData(obj)
// End If

// End Function

// Private Shared Function IterateValueList(obj As Object) As Object
// Dim A As MetaData() = {}
// For Each Element As SceneElement In obj
// A.Add(New MetaData(Element))
// Next
// Return A
// End Function

// Private Shared Function IterateValueList2(obj As Object) As Object
// Dim A As MetaData() = {}
// If DirectCast(obj, ChildCollection).Count = 0 Then Return A
// For Each Element As SceneElement In obj
// A.Add(New MetaData(Element))
// Next
// Return A
// End Function

// #End Region

// #Region "Deserialization Dictionary"

// Public Shared ReadOnly Property DeserializationTypes As Dictionary(Of String, Type)
// Get
// If Not _Initialized Then
// Dim Assemblies As Assembly() = AppDomain.CurrentDomain.GetAssemblies()
// For Each a As Assembly In Assemblies
// For Each C As Type In a.GetExportedTypes().Where(Function(t) t.IsClass)
// _DeserializationTypes.Add(C.FullName, C)
// Next
// Next

// _Initialized = True
// End If
// Return _DeserializationTypes
// End Get
// End Property
// Private Shared _DeserializationTypes As New Dictionary(Of String, Type)
// Private Shared _Initialized As Boolean = False

// #End Region

// Public Shared ReflectionFlags As BindingFlags = BindingFlags.Instance Or BindingFlags.[Public]

// Public Overloads Shared Function SerializeProperties(Obj As Object) As Dictionary(Of String, MetaData)
// Return SerializeProperties(Obj, {"Scene", "Parent"})
// End Function

// Public Overloads Shared Function SerializeProperties(Obj As Object, IgnorePropertyNames As String()) As Dictionary(Of String, MetaData)
// Dim Value As New Dictionary(Of String, MetaData)
// Dim type As Type = Obj.[GetType]()
// Dim properties As PropertyInfo() = type.GetProperties(ReflectionFlags)

// For Each p As PropertyInfo In properties
// If IgnorePropertyNames.Contains(p.Name) Then Continue For
// Try
// Dim v As Object = p.GetValue(Obj, Nothing)
// If v IsNot Nothing AndAlso (IsSerializable(v) OrElse Convertable(v.GetType)) Then
// If GetSerializedValue(v) IsNot Nothing Then
// Dim obj1 As Object = GetSerializedValue(v)
// If IsArray(obj1) Then
// For Each objject As Object In obj1
// Value.Add(p.Name, objject)
// Next
// Else
// Value.Add(p.Name, obj1)
// End If
// End If
// End If
// Catch ex As Exception
// ' Property type not convertable.
// End Try
// Next
// Return Value
// End Function

// Public Shared Sub DeserializeProperties(Obj As Object, Properties As Dictionary(Of String, MetaData))

// Dim type As Type = Obj.[GetType]()
// Dim objProperties As PropertyInfo() = type.GetProperties(ReflectionFlags)

// For Each p As PropertyInfo In objProperties
// Dim MetaObject As MetaData = Properties(p.Name)
// Dim MetaType As Type = DeserializationTypes(MetaObject.DataType)

// If p.CanWrite Then
// Dim instance As Object = Activator.CreateInstance(MetaType)
// DeserializeProperties(instance, MetaObject.Properties)
// p.SetValue(Obj, instance, Nothing)
// End If

// Next

// End Sub

// Public Shared Function GetTypeFromNameManual(TypeName As String) As Type
// For Each A As Assembly In AppDomain.CurrentDomain.GetAssemblies()
// If A.FullName = TypeName Then
// Return A.GetType(TypeName)
// End If
// Next
// Return Nothing
// End Function

// Private Shared Function IsSerializable(obj As Object) As Boolean
// Return (TypeOf obj Is Runtime.Serialization.ISerializable) OrElse (Attribute.IsDefined(obj.GetType, GetType(SerializableAttribute)))
// End Function
// Public Shared Function Convertable(T As Type) As Boolean
// For Each T2 As Type In SerializationConverters.Keys
// Dim isSubClass As Boolean = T.IsSubclassOf(T2)
// Dim isAssignable As Boolean = T.IsAssignableFrom(T2)
// If isSubClass Or isAssignable Then
// Return True
// End If
// Next
// Return False
// End Function

// Public Shared Function TryConvertObject(Obj As Object) As Object
// Dim T As Type = Obj.GetType
// For Each T2 As Type In SerializationConverters.Keys
// Dim isSubClass As Boolean = T.IsSubclassOf(T2)
// Dim isAssignable As Boolean = T.IsAssignableFrom(T2)
// If isSubClass Or isAssignable Then
// Return SerializationConverters(T2).Invoke(Obj)
// End If
// Next
// Return Nothing
// End Function
// Public Shared Function GetSerializedValue(Obj As Object) As Object
// Return TryConvertObject(Obj)
// End Function

// Public Shared Function GetDeserializedValue(Str As String) As Object
// Return Newtonsoft.Json.JsonConvert.DeserializeObject(Str)
// End Function

// End Class

// End Namespace
