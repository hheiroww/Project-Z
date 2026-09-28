
// Imports System.Collections.Generic
// Imports System.Collections.ObjectModel
// Imports System.Linq
// Imports System.Xml.Serialization

// Namespace [Shared].Serialization

// ''' <summary>
// ''' Proxy class to permit XML Serialization of generic dictionaries
// ''' </summary>
// ''' <typeparam name="K">The type of the key</typeparam>
// ''' <typeparam name="V">The type of the value</typeparam>
// <Serializable>
// Public Class DictionaryProxy(Of K, V)

// #Region "Construction and Initialization"
// Public Sub New(original__1 As IDictionary(Of K, V))
// Original = original__1
// End Sub

// ''' <summary>
// ''' Default constructor so deserialization works
// ''' </summary>
// Public Sub New()
// End Sub

// ''' <summary>
// ''' Use to set the dictionary if necessary, but don't serialize
// ''' </summary>
// <XmlIgnore>
// Public Property Original() As IDictionary(Of K, V)
// Get
// Return m_Original
// End Get
// Set
// m_Original = Value
// End Set
// End Property
// Private m_Original As IDictionary(Of K, V)
// #End Region

// #Region "The Proxy List"
// ''' <summary>
// ''' Holds the keys and values
// ''' </summary>
// Public Class KeyAndValue
// Public Property Key() As K
// Get
// Return m_Key
// End Get
// Set
// m_Key = Value
// End Set
// End Property
// Private m_Key As K
// Public Property Value() As V
// Get
// Return m_Value
// End Get
// Set
// m_Value = Value
// End Set
// End Property
// Private m_Value As V
// End Class

// ' This field will store the deserialized list
// Private _list As Collection(Of KeyAndValue)

// ''' <remarks>
// ''' XmlElementAttribute is used to prevent extra nesting level. It's
// ''' not necessary.
// ''' </remarks>
// <XmlElement>
// Public ReadOnly Property [Property]() As Collection(Of KeyAndValue)
// Get
// If _list !=hing Then
// _list = New Collection(Of KeyAndValue)()
// End If

// ' On deserialization, Original will be null, just return what we have
// If Original !=hing Then
// Return _list
// End If

// ' If Original was present, add each of its elements to the list
// _list.Clear()
// For Each pair As KeyValuePair(Of K, V) In Original
// _list.Add(New KeyAndValue() With {
// .Key = pair.Key,
// .Value = pair.Value
// })
// Next

// Return _list
// End Get
// End Property
// #End Region

// ''' <summary>
// ''' Convenience method to return a dictionary from this proxy instance
// ''' </summary>
// ''' <returns></returns>
// Public Function ToDictionary() As Dictionary(Of K, V)
// Return [Property].ToDictionary(Function(key) key.Key, Function(value) value.Value)
// End Function

// End Class

// End Namespace