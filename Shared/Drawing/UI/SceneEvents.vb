Imports System.Collections.Concurrent
Imports System.Runtime.CompilerServices

Namespace [Shared].Drawing.UI
    ''' <summary>Mutable paste command shared by native input and routed import handlers.</summary>
    Public NotInheritable Class ScenePastingContext
        Public ReadOnly Property SourceDataObject As Object
        Public Property DataObject As Object
        Public Property FormatToApply As String
        Public Property Cancelled As Boolean
        Public Sub New(data As Object, format As String)
            SourceDataObject = data : DataObject = data : FormatToApply = format
        End Sub
    End Class

    Public Enum SceneEventRouting
        Direct
        Bubble
        Tunnel
    End Enum

    ''' <summary>A renderer-owned event identity shared by native controls and imported views.</summary>
    Public NotInheritable Class SceneRoutedEvent
        Private Shared ReadOnly registry As New ConcurrentDictionary(Of String, SceneRoutedEvent)(StringComparer.Ordinal)
        Public ReadOnly Property Name As String
        Public ReadOnly Property Routing As SceneEventRouting
        Private Sub New(name As String, routing As SceneEventRouting)
            Me.Name = name : Me.Routing = routing
        End Sub
        Public Shared Function Register(name As String, routing As SceneEventRouting) As SceneRoutedEvent
            Dim result = registry.GetOrAdd(name, Function(key) New SceneRoutedEvent(key, routing))
            If result.Routing <> routing Then Throw New ArgumentException("An event already has a different routing strategy.", NameOf(routing))
            Return result
        End Function
    End Class

    Public NotInheritable Class SceneRoutedEventArgs
        Public ReadOnly Property RoutedEvent As SceneRoutedEvent
        Public ReadOnly Property OriginalSource As SceneElement
        Public Property Source As SceneElement
        Public ReadOnly Property Payload As Object
        Public Property Handled As Boolean
        Public Sub New(routedEvent As SceneRoutedEvent, source As SceneElement, Optional payload As Object = Nothing)
            Me.RoutedEvent = routedEvent : Me.Source = source : OriginalSource = source : Me.Payload = payload
        End Sub
    End Class

    ''' <summary>Routes events through the real Project-Z element tree, including native content hosts.</summary>
    Public NotInheritable Class SceneEvents
        Private NotInheritable Class HandlerEntry
            Public ReadOnly Callback As Action(Of SceneElement, SceneRoutedEventArgs)
            Public ReadOnly HandledEventsToo As Boolean
            Public Sub New(callback As Action(Of SceneElement, SceneRoutedEventArgs), handledEventsToo As Boolean)
                Me.Callback = callback : Me.HandledEventsToo = handledEventsToo
            End Sub
        End Class
        Private NotInheritable Class HandlerStore
            Public ReadOnly Events As New Dictionary(Of SceneRoutedEvent, List(Of HandlerEntry))
        End Class
        Private NotInheritable Class Subscription
            Implements IDisposable
            Private store As HandlerStore
            Private ReadOnly routedEvent As SceneRoutedEvent
            Private ReadOnly entry As HandlerEntry
            Public Sub New(store As HandlerStore, routedEvent As SceneRoutedEvent, entry As HandlerEntry)
                Me.store = store : Me.routedEvent = routedEvent : Me.entry = entry
            End Sub
            Public Sub Dispose() Implements IDisposable.Dispose
                If store Is Nothing Then Return
                If store.Events.ContainsKey(routedEvent) Then
                    store.Events(routedEvent).Remove(entry)
                    If store.Events(routedEvent).Count = 0 Then store.Events.Remove(routedEvent)
                End If
                store = Nothing
            End Sub
        End Class
        Private Shared ReadOnly handlers As New ConditionalWeakTable(Of SceneElement, HandlerStore)
        Public Shared Function [AddHandler](element As SceneElement, routedEvent As SceneRoutedEvent, callback As Action(Of SceneElement, SceneRoutedEventArgs), Optional handledEventsToo As Boolean = False) As IDisposable
            If element Is Nothing OrElse routedEvent Is Nothing OrElse callback Is Nothing Then Throw New ArgumentNullException()
            Dim store = handlers.GetOrCreateValue(element)
            If Not store.Events.ContainsKey(routedEvent) Then store.Events.Add(routedEvent, New List(Of HandlerEntry))
            Dim entry As New HandlerEntry(callback, handledEventsToo)
            store.Events(routedEvent).Add(entry)
            Return New Subscription(store, routedEvent, entry)
        End Function
        Public Shared Sub [RaiseEvent](source As SceneElement, args As SceneRoutedEventArgs)
            If source Is Nothing OrElse args Is Nothing Then Throw New ArgumentNullException()
            Dim route As New List(Of SceneElement)
            Dim node = source
            While node IsNot Nothing
                route.Add(node)
                If args.RoutedEvent.Routing = SceneEventRouting.Direct Then Exit While
                node = node.RoutedParent
            End While
            If args.RoutedEvent.Routing = SceneEventRouting.Tunnel Then route.Reverse()
            For Each target In route
                Dim store As HandlerStore = Nothing
                If Not handlers.TryGetValue(target, store) OrElse Not store.Events.ContainsKey(args.RoutedEvent) Then Continue For
                For Each entry In store.Events(args.RoutedEvent).ToArray()
                    If Not args.Handled OrElse entry.HandledEventsToo Then entry.Callback(target, args)
                Next
            Next
        End Sub
        Public Shared Sub ClearHandlers(element As SceneElement)
            handlers.Remove(element)
        End Sub
    End Class
End Namespace
