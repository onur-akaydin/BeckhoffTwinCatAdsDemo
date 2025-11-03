namespace TwinCAT.Ads;

//
// Summary:
//     ADS Client / ADS Communication object.
//
// Remarks:
//     The class TwinCAT.Ads.AdsClient enables synchronous/asynchronous access to data
//     of an ADS Device.
[DebuggerDisplay("ID = { _id }, TargetAddress = {_target}, ClientAddress = { ClientAddress}, ConnectionState = {ConnectionState}")]
public sealed class AdsClient : IAdsDisposableConnection, IAdsConnectAddress, IAdsConnection, IConnection, IConnectionStateProvider, IAdsNotifications, IAdsSymbolicAccess, IAdsAnyAccess, IAdsHandle, IAdsReadWrite2, IAdsReadWrite, IAdsStateProvider, IAdsStateControl, IAdsSymbolChangedProvider, IAdsRpcInvoke, IAdsInjectAcceptor, IRouterNotificationProvider, IDisposable, IAdsHandleCacheProvider, ITcAdsRaw, IInterceptedClient, IClientNotificationReceiver, INotificationReceiver, IStateChangedReceiver, ISymbolVersionChangedReceiver, IRouterNotificationReceiver, INotificationProvider, IAdsSymbolCacheProvider, ILoggerFactoryProvider
{
    //
    // Summary:
    //     Enum SumAccessMode
    [Flags]
    internal enum AccessMethods : uint
    {
        //
        // Summary:
        //     Access by IndexGroup / IndexOffset
        IndexGroupIndexOffset = 1u,
        //
        // Summary:
        //     Accesses a value by handle
        ValueByHandle = 2u,
        //
        // Summary:
        //     Access a value by name
        ValueByName = 4u,
        //
        // Summary:
        //     Acquire handle by name
        AcquireHandleByName = 0x10u,
        //
        // Summary:
        //     Release handle
        ReleaseHandle = 0x20u,
        //
        // Summary:
        //     None / Uninitialized
        None = 0u,
        //
        // Summary:
        //     All Access methods are allowed
        Mask_All = 0x37u,
        //
        // Summary:
        //     Only Symbolic access is allowed (No Processimage IndexGroup/IndexOffset)
        Mask_Symbolic = 0x36u
    }

    //
    // Summary:
    //     The logger factory
    private ILoggerFactory? _loggerFactory;

    //
    // Summary:
    //     The logger
    private ILogger<AdsClient>? _logger;

    //
    // Summary:
    //     The configuration
    private IConfiguration? _configuration;

    //
    // Summary:
    //     Private AdsServer.
    private AdsClientServer? _server;

    //
    // Summary:
    //     The disposed indicator.
    private bool _disposed;

    //
    // Summary:
    //     Synchronization object
    private object _sync = new object();

    //
    // Summary:
    //     The notification receiver
    private NotificationReceiverBase? _notificationReceiver;

    //
    // Summary:
    //     The symbol table
    private HandleCache? _handleCache;

    //
    // Summary:
    //     List of Handle bags.
    private List<IDisposableHandleBag> _handleBags = new List<IDisposableHandleBag>();

    //
    // Summary:
    //     Static identifier counter of this TwinCAT.Ads.AdsClient.
    private static int s_id;

    //
    // Summary:
    //     TwinCAT.Ads.AdsClient identifier
    private int _id = ++s_id;

    //
    // Summary:
    //     The actual Target TwinCAT.Ads.AmsAddress.
    private AmsAddress _target = AmsAddress.Empty;

    //
    // Summary:
    //     Indicates that the TwinCAT.Ads.AdsClient is connected.
    private bool _isConnected;

    private bool _encodingsInitialized;

    private SymbolUploadInfo? _uploadInfo;

    //
    // Summary:
    //     Router notification event handler delegate
    private EventHandler<AmsRouterNotificationEventArgs>? _amsRouterNotificationEventHandlerDelegate;

    //
    // Summary:
    //     ADS State changed handler delegate
    private EventHandler<AdsStateChangedEventArgs>? _adsStateChangedEventHandlerDelegate;

    //
    // Summary:
    //     StateChangedNotification registered indicator.
    private bool _stateChangedNotificationRegistered;

    //
    // Summary:
    //     Delegate for TwinCAT.Ads.AdsClient.AdsSymbolVersionChanged events.
    private EventHandler<AdsSymbolVersionChangedEventArgs>? _symbolVersionChangedDelegate;

    private bool _symbolVersionChangedNotificationRegistered;

    //
    // Summary:
    //     The actual TwinCAT.Ads.AmsRouterState
    private AmsRouterState _routerState;

    private AnyTypeMarshaler _anyTypeMarshaller = new AnyTypeMarshaler();

    //
    // Summary:
    //     Cached timeout
    private int _timeout = 5000;

    //
    // Summary:
    //     The session object.
    private ISession? _session;

    //
    // Summary:
    //     The interceptors
    private CommunicationInterceptors? _interceptors;

    //
    // Summary:
    //     The symbol Name Encoding
    private Encoding? _symbolNameEncoding;

    //
    // Summary:
    //     The string value encoding
    private Encoding? _defaultValueEncoding;

    private ISymbolCache? _symbolCache;

    private readonly SemaphoreSlim _symbolCacheSema = new SemaphoreSlim(1, 1);

    private int _platformPointerSize;

    //
    // Summary:
    //     Gets the logger factory.
    //
    // Value:
    //     The logger factory.
    //
    // Exceptions:
    //   T:System.NotImplementedException:
    ILoggerFactory? ILoggerFactoryProvider.LoggerFactory => _loggerFactory;

    //
    // Summary:
    //     Gets the logger inteface.
    //
    // Value:
    //     The logger.
    public ILogger<AdsClient>? Logger => _logger;

    //
    // Summary:
    //     Gets the optional Configuration
    //
    // Value:
    //     The logger.
    public IConfiguration? Configuration => _configuration;

    //
    // Summary:
    //     Gets the the used channel protocol.
    //
    // Value:
    //     The channel protocol.
    public ChannelProtocol ChannelProtocol
    {
        get
        {
            if (_server != null)
            {
                return _server.ChannelProtocol;
            }

            return ChannelProtocol.None;
        }
    }

    //
    // Summary:
    //     Gets the channel port type.
    //
    // Value:
    //     The channel protocol.
    public ChannelPortType ChannelPortType
    {
        get
        {
            if (_server != null)
            {
                return _server.ChannelPortType;
            }

            return ChannelPortType.None;
        }
    }

    //
    // Summary:
    //     Gets a value indicating whether this instance is disposed.
    //
    // Value:
    //     true if this instance is disposed; otherwise, false.
    public bool IsDisposed => _disposed;

    //
    // Summary:
    //     Gets the TwinCAT.Ads.AdsClient Identifier.
    //
    // Value:
    //     The identifier.
    public int Id => _id;

    //
    // Summary:
    //     Gets the Name of the TwinCAT.Ads.AdsClient object.
    //
    // Value:
    //     The name.
    internal string Name => string.Format(CultureInfo.CurrentCulture, "AdsClient_{0}", Id);

    //
    // Summary:
    //     Gets a value indicating whether the local ADS port was opened successfully. It
    //     does not indicate if the target port is available. Use the method ReadState to
    //     determine if the target port is available.
    //
    // Value:
    //     true if this instance is connected; otherwise, false.
    public bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _isConnected;
            }
        }
    }

    //
    // Summary:
    //     Gets the target TwinCAT.Ads.AmsAddress of of the established ADS connection (Destination
    //     side).
    //
    // Value:
    //     The address.
    public AmsAddress Address => _target;

    //
    // Summary:
    //     Get the TwinCAT.Ads.AmsAddress of the ADS client.
    //
    // Value:
    //     The client address.
    [Obsolete("Use AdsClient.SourceAddress instead!")]
    public AmsAddress ClientAddress => SourceAddress;

    //
    // Summary:
    //     Get the client/source TwinCAT.Ads.AmsAddress (Source side).
    //
    // Value:
    //     The client address.
    public AmsAddress SourceAddress
    {
        get
        {
            AmsAddress result = AmsAddress.Empty;
            if (_server != null)
            {
                result = _server.ServerAddress;
            }

            return result;
        }
    }

    //
    // Summary:
    //     Gets a value indicating whether the ADS client is connected to a ADS Server on
    //     the local computer.
    //
    // Value:
    //     true if this instance is local; otherwise, false.
    //
    // Exceptions:
    //   T:System.NotImplementedException:
    public bool IsLocal
    {
        get
        {
            if (_target != null)
            {
                return _target.NetId.IsLocal;
            }

            return false;
        }
    }

    //
    // Summary:
    //     Sets the timeout for the ads communication. Unit is in ms.
    public int Timeout
    {
        get
        {
            return _timeout;
        }
        set
        {
            if (!IsDisposed && _timeout != value)
            {
                OnSetTimout(value);
            }
        }
    }

    //
    // Summary:
    //     Gets the session that initiated this TwinCAT.IConnection
    //
    // Value:
    //     The session or NULL
    //
    // Remarks:
    //     The Session can be null on standalone connections.
    public ISession? Session => _session;

    //
    // Summary:
    //     Gets the current Connection state of the TwinCAT.IConnectionStateProvider
    //
    // Value:
    //     The state of the connection.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ConnectionState ConnectionState
    {
        get
        {
            if (IsConnected)
            {
                return ConnectionState.Connected;
            }

            return ConnectionState.Disconnected;
        }
    }

    //
    // Summary:
    //     Gets the interceptors.
    //
    // Value:
    //     The interceptors.
    public CommunicationInterceptors? Interceptors => _interceptors;

    //
    // Summary:
    //     Gets the default value encoding.
    //
    // Value:
    //     The default value encoding.
    public Encoding DefaultValueEncoding
    {
        get
        {
            tryReadEncodings();
            if (_defaultValueEncoding != null)
            {
                return _defaultValueEncoding;
            }

            return StringMarshaler.DefaultEncoding;
        }
        set
        {
            SetEncodings(value, value, -1);
        }
    }

    //
    // Summary:
    //     Gets the symbol encoding.
    //
    // Value:
    //     The symbol encoding.
    public Encoding SymbolEncoding
    {
        get
        {
            tryReadEncodings();
            if (_symbolNameEncoding != null)
            {
                return _symbolNameEncoding;
            }

            return StringMarshaler.DefaultEncoding;
        }
        set
        {
            SetEncodings(value, value, -1);
        }
    }

    //
    // Summary:
    //     Gets the target platform pointer size
    //
    // Value:
    //     The size of the target platform.
    public int PlatformPointerSize
    {
        get
        {
            tryReadEncodings();
            return _platformPointerSize;
        }
        set
        {
            SetEncodings(null, null, value);
        }
    }

    //
    // Summary:
    //     Occurs when the connection state has been changed.
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    //
    // Summary:
    //     Occurs when Notifications are send (bundled notifications)
    //
    // Remarks:
    //     As an optimization, this event receives all ADS Notifications that occurred at
    //     one point in time together. As consequence, the overhead of handler code is reduced,
    //     what can be important if notifications are triggered in a high frequency and
    //     the event has to be synchronized to the UI thread context. Because multiple notifications
    //     are bound together, less thread synchronization is necessary. The TwinCAT.Ads.AdsClient.AdsNotification
    //     and TwinCAT.Ads.AdsClient.AdsNotificationEx events shouldn't be used when SumNotifications
    //     are registered, because they have an performance side effect to this TwinCAT.Ads.AdsClient.AdsSumNotification
    //     event. The full performance is reached only, when all notifications are handled
    //     on this event.
    public event EventHandler<AdsSumNotificationEventArgs>? AdsSumNotification;

    //
    // Summary:
    //     Occurs when Notification Unregistrations / Invalidates are received from the
    //     AdsServer
    //
    // Remarks:
    //     Some ADS servers are sending 0-size notifications, when the Notification handle
    //     is not valid anymore. If received, this event will be triggered, to notify any
    //     consumers to invalidate the notification handles. One example for these sort
    //     of invalidation is, if ADS Notifications are already registered at the PLC ADS
    //     Server, and the PLC Control downloads a new program. All registered notification
    //     handles are invalidated!
    public event EventHandler<AdsNotificationsInvalidatedEventArgs>? AdsNotificationsInvalidated;

    //
    // Summary:
    //     Occurs when the ADS device sends a notification to the client.
    //
    // Remarks:
    //     The Event Argument contains the raw data value of the notification, not marshaled
    //     to .NET types.
    public event EventHandler<AdsNotificationEventArgs>? AdsNotification;

    //
    // Summary:
    //     Occurs when a exception has occurred during notification management.
    //
    // Remarks:
    //     The occurrence of this event can have two different reasons:
    //
    //     1. Indicates an internal error occurred during Notification management.
    //     2. The registered notification becomes invalid on the server, eg. after a PLC
    //     Download / Online Change. If the ADS Server detects that the (still registered)
    //     Notification Sender is getting invalid, it sends an error notification so that
    //     the client will be informed about detached notifications. The event arguments
    //     contains the TwinCAT.Ads.AdsInvalidNotificationException which describes the
    //     invalid notification handle by its TwinCAT.Ads.AdsInvalidNotificationException.Handle
    //     property.
    public event EventHandler<AdsNotificationErrorEventArgs>? AdsNotificationError;

    //
    // Summary:
    //     Occurs when the ADS devices sends a notification to the client.
    //
    // Remarks:
    //     The Notification event arguments marshals the data value automatically to the
    //     specified .NET Type with ANY_TYPE marshallers.
    public event EventHandler<AdsNotificationExEventArgs>? AdsNotificationEx;

    //
    // Summary:
    //     (Local) Router state changed event.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    // Remarks:
    //     This event indicates, that a changed event is received from the Local AmsRouter
    //     independant of the connected target address. A remote system RouterStateChanged
    //     event cannot be received at another system - it cannot traverse TwinCAT systems.
    public event EventHandler<AmsRouterNotificationEventArgs>? RouterStateChanged
    {
        add
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(Name);
            }

            _amsRouterNotificationEventHandlerDelegate = (EventHandler<AmsRouterNotificationEventArgs>)Delegate.Combine(_amsRouterNotificationEventHandlerDelegate, value);
        }
        remove
        {
            _amsRouterNotificationEventHandlerDelegate = (EventHandler<AmsRouterNotificationEventArgs>)Delegate.Remove(_amsRouterNotificationEventHandlerDelegate, value);
        }
    }

    //
    // Summary:
    //     Occurs when the ADS state changes.
    //
    // Remarks:
    //     This works only for ports that support Notifications (e.g. Port 851 but not Port
    //     10000).
    public event EventHandler<AdsStateChangedEventArgs>? AdsStateChanged
    {
        add
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(Name);
            }

            AdsErrorCode adsErrorCode = AdsErrorCode.None;
            if (IsConnected && _adsStateChangedEventHandlerDelegate == null)
            {
                adsErrorCode = registerStateChangedNotification(_timeout);
                if (adsErrorCode.Failed())
                {
                    AdsErrorException e = AdsErrorException.Create(ResMan.GetString("AdsStateChangedRegistrationFailed_Message"), adsErrorCode);
                    ((INotificationReceiver)this).OnNotificationError((Exception)e);
                }
            }

            _adsStateChangedEventHandlerDelegate = (EventHandler<AdsStateChangedEventArgs>)Delegate.Combine(_adsStateChangedEventHandlerDelegate, value);
        }
        remove
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(Name);
            }

            _adsStateChangedEventHandlerDelegate = (EventHandler<AdsStateChangedEventArgs>)Delegate.Remove(_adsStateChangedEventHandlerDelegate, value);
            if (IsConnected && _adsStateChangedEventHandlerDelegate == null && _stateChangedNotificationRegistered)
            {
                unregisterStateChangedNotification(_timeout);
            }
        }
    }

    //
    // Summary:
    //     Occurs when the symbol version has been changed changes.
    //
    // Remarks:
    //     This is the case when the connected ADS server restarts. This invalidates all
    //     actual opened symbol handles. The SymbolVersion counter doesn't trigger, when
    //     an online change is made on the PLC (ports 801, ..., 851 ...)
    public event EventHandler<AdsSymbolVersionChangedEventArgs>? AdsSymbolVersionChanged
    {
        add
        {
            RegisterSymbolVersionChanged(value);
        }
        remove
        {
            UnregisterSymbolVersionChanged(value);
        }
    }

    //
    // Summary:
    //     Initializes a new instance of the TwinCAT.Ads.AdsClient class.
    //
    // Parameters:
    //   session:
    //     The session.
    //
    //   settings:
    //     The settings.
    //
    //   configuration:
    //     The configuration.
    //
    //   loggerFactory:
    //     The logger factory.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     settings
    public AdsClient(ISession? session, AdsClientSettings settings, IConfiguration? configuration, ILoggerFactory? loggerFactory)
    {
        if (settings == null)
        {
            throw new ArgumentNullException("settings");
        }

        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<AdsClient>();
        _configuration = configuration;
        _session = session;
        _interceptors = settings.Interceptors;
        _timeout = settings.Timeout;
        Logger?.LogInformation("AdsClient created: ID:{0}, Timeout: {1} ms", _id, _timeout);
    }

    //
    // Summary:
    //     Initializes a new instance of the TwinCAT.Ads.AdsClient class.
    //
    // Parameters:
    //   session:
    //     The session.
    //
    //   settings:
    //     The settings.
    //
    //   loggerFactory:
    //     The logger factory.
    public AdsClient(ISession? session, AdsClientSettings settings, ILoggerFactory? loggerFactory)
        : this(session, settings, null, loggerFactory)
    {
    }

    //
    // Summary:
    //     Initializes a new instance of the TwinCAT.Ads.AdsClient class.
    public AdsClient()
        : this(null, AdsClientSettings.Default, null, null)
    {
    }

    //
    // Summary:
    //     Initializes a new instance of the TwinCAT.Ads.AdsClient class with the specified
    //     settings.
    //
    // Parameters:
    //   settings:
    //     The settings.
    public AdsClient(AdsClientSettings settings)
        : this(null, settings, null, null)
    {
    }

    //
    // Summary:
    //     Initializes a new instance of the TwinCAT.Ads.AdsClient class bound to a session.
    //
    //
    // Parameters:
    //   session:
    //     The session.
    //
    //   configuration:
    //     The configuration
    //
    //   loggerFactory:
    //     The logger factory.
    public AdsClient(ISession session, IConfiguration? configuration, ILoggerFactory? loggerFactory)
        : this(session, AdsClientSettings.Default, configuration, loggerFactory)
    {
    }

    //
    // Summary:
    //     Initializes a new instance of the TwinCAT.Ads.AdsClient class.
    //
    // Parameters:
    //   configuration:
    //     The configuration
    //
    //   loggerFactory:
    //     The logger factory.
    public AdsClient(IConfiguration? configuration, ILoggerFactory? loggerFactory)
        : this(null, AdsClientSettings.Default, configuration, loggerFactory)
    {
    }

    //
    // Summary:
    //     Initializes a new instance of the TwinCAT.Ads.AdsClient class.
    //
    // Parameters:
    //   loggerFactory:
    //     The logger factory.
    public AdsClient(ILoggerFactory? loggerFactory)
        : this(null, AdsClientSettings.Default, null, loggerFactory)
    {
    }

    //
    // Summary:
    //     Finalizes an instance of the TwinCAT.Ads.AdsClient class.
    ~AdsClient()
    {
        Dispose(disposing: false);
    }

    //
    // Summary:
    //     Performs application-defined tasks associated with freeing, releasing, or resetting
    //     unmanaged resources.
    public void Dispose()
    {
        if (!_disposed)
        {
            Dispose(disposing: true);
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    //
    // Summary:
    //     Releases unmanaged and - optionally - managed resources.
    //
    // Parameters:
    //   disposing:
    //     true to release both managed and unmanaged resources; false to release only unmanaged
    //     resources.
    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disconnect();
        }

        if (CanLog(LogLevel.Information))
        {
            Logger?.LogInformation("Client:{Name}, Address: {Address} disposed!", Name, Address);
        }
    }

    //
    // Summary:
    //     Gets the symbol table.
    //
    // Returns:
    //     SymbolTable.
    [EditorBrowsable(EditorBrowsableState.Never)]
    IHandleCache? IAdsHandleCacheProvider.GetHandleCache()
    {
        if (_handleCache == null)
        {
            _handleCache = new HandleCache(this, _loggerFactory);
        }

        return _handleCache;
    }

    //
    // Summary:
    //     Creates a handle bag from symbol paths.
    //
    // Parameters:
    //   instancePath:
    //     A list of symbol paths.
    //
    //   relaxSubErrors:
    //     Don't leak exceptions on failed single handle creation.
    //
    // Returns:
    //     A handle bag that can be disposed.
    IDisposableHandleBag<string> IAdsHandleCacheProvider.CreateHandleBag(string[] instancePath, bool relaxSubErrors)
    {
        IDisposableHandleBag<string> disposableHandleBag = HandleBagFactory.CreateVariableHandleBag(this, instancePath, relaxSubErrors);
        _handleBags.Add(disposableHandleBag);
        return disposableHandleBag;
    }

    //
    // Summary:
    //     Creates a notification handle bag form the specified symbols.
    //
    // Parameters:
    //   symbols:
    //     The symbols.
    //
    //   relaxSubErrors:
    //     Don't leak exceptions on failed single handle creation.
    //
    //   settings:
    //     The Notification settings.
    //
    //   userData:
    //     The user data.
    //
    // Returns:
    //     A handle bag that can be disposed.
    IDisposableHandleBag<ISymbol> IAdsHandleCacheProvider.CreateNotificationHandleBag(ISymbol[] symbols, bool relaxSubErrors, NotificationSettings settings, object[]? userData)
    {
        IDisposableHandleBag<ISymbol> disposableHandleBag = HandleBagFactory.CreateNotificationHandleBag(this, symbols, relaxSubErrors, settings, userData);
        _handleBags.Add(disposableHandleBag);
        return disposableHandleBag;
    }

    //
    // Summary:
    //     Creates the notification ex handle bag.
    //
    // Parameters:
    //   symbols:
    //     The symbols.
    //
    //   relaxSubErrors:
    //     Don't leak exceptions on failed single handle creation.
    //
    //   settings:
    //     The settings.
    //
    //   userData:
    //     The user data.
    //
    // Returns:
    //     IDisposableHandleBag.
    IDisposableHandleBag<AnySymbolSpecifier> IAdsHandleCacheProvider.CreateNotificationExHandleBag(IList<AnySymbolSpecifier> symbols, bool relaxSubErrors, NotificationSettings settings, object[]? userData)
    {
        IDisposableHandleBag<AnySymbolSpecifier> disposableHandleBag = HandleBagFactory.CreateNotificationExHandleBag(this, symbols, relaxSubErrors, settings, userData);
        _handleBags.Add(disposableHandleBag);
        return disposableHandleBag;
    }

    //
    // Summary:
    //     Unregisters the handle bag from this TwinCAT.Ads.IAdsHandleTableProvider.
    //
    // Parameters:
    //   bag:
    //     The handle bag.
    void IAdsHandleCacheProvider.UnregisterHandleBag(IDisposableHandleBag bag)
    {
        _handleBags.Remove(bag);
        bag.Dispose();
    }

    //
    // Summary:
    //     Determines whether the used logger is enabled for the specified log level.
    //
    // Parameters:
    //   level:
    //     The level.
    //
    // Returns:
    //     true if this instance can log the specified level; otherwise, false.
    private bool CanLog(LogLevel level)
    {
        return _logger?.IsEnabled(level) ?? false;
    }

    //
    // Summary:
    //     Called when the TwinCAT.Ads.AdsClient.ConnectionState of the TwinCAT.Ads.AdsClient
    //     has changed.
    //
    // Parameters:
    //   newState:
    //     The new state.
    //
    //   oldState:
    //     The old state.
    private void OnConnectionStateChanged(ConnectionState newState, ConnectionState oldState)
    {
        ConnectionStateChangedReason reason = ConnectionStateChangedReason.None;
        switch (newState)
        {
            case ConnectionState.Disconnected:
                reason = ConnectionStateChangedReason.Closed;
                if (_interceptors != null)
                {
                    _interceptors.Disconnect(() => AdsErrorCode.NoError);
                }

                break;
            case ConnectionState.Connected:
                reason = ConnectionStateChangedReason.Established;
                if (_interceptors != null)
                {
                    _interceptors.Connect(() => AdsErrorCode.NoError);
                }

                break;
        }

        if (CanLog(LogLevel.Information))
        {
            Logger?.LogInformation("AdsClient::OnConnectionStateChanged, Client:{Name}, Target:{Target}, Local:{LocalAddress}, NewState:{NewState}, OldState:{OldState}", Name, Address, SourceAddress, newState, oldState);
        }

        if (this.ConnectionStateChanged != null)
        {
            this.ConnectionStateChanged(this, new ConnectionStateChangedEventArgs(reason, newState, oldState, null));
        }
    }

    //
    // Summary:
    //     Connects to the target address and waits until the TwinCAT.Ads.AdsClient is disconnected
    //     asynchronously.
    //
    // Parameters:
    //   address:
    //     The target address.
    //
    //   cancel:
    //     Cancellation Token.
    //
    // Returns:
    //     Returns a task object that represents the TwinCAT.Ads.AdsClient.ConnectAndWaitAsync(TwinCAT.Ads.AmsAddress,System.Threading.CancellationToken)
    //     operation as result.
    //
    // Remarks:
    //     This method is used for scenarios, where the TwinCAT.Ads.AdsClient disconnects
    //     from other code asynchronously. When this method returns, the connection is already
    //     terminated and only additional cleanup code should be processed.
    public async Task ConnectAndWaitAsync(AmsAddress address, CancellationToken cancel)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(ToString());
            }

            if (IsConnected)
            {
                Disconnect();
            }

            _target = address;
            _handleCache = new HandleCache(this, _loggerFactory);
            _notificationReceiver = new NotificationReceiver(this, _handleCache, this);
            if (CanLog(LogLevel.Debug))
            {
                Logger?.LogDebug("ConnectAndWaitAsync (Before), Client:{Name}, Address: {Target}, Local: {Local}", Name, Address, SourceAddress);
            }

            _server = new AdsClientServer(_notificationReceiver, this, _configuration, _loggerFactory);
            _server.Timeout = Timeout;
            _server.ServerConnectionStateChanged += _server_ServerConnectionStateChanged;
            _isConnected = true;
            AddEventNotifications();
        }

        OnConnected();
        AdsErrorCode adsErrorCode = await _server.ConnectServerAndWaitAsync(cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (CanLog(LogLevel.Debug))
        {
            Logger?.LogDebug("ConnectAndWaitAsync (After), Client:{Name}, Address: {Target}, Local: {Local}, ErrorCode: {2}", Name, Address, SourceAddress, adsErrorCode);
        }
    }

    private void _server_ServerConnectionStateChanged(object? sender, ServerConnectionStateChangedEventArgs e)
    {
        if (e.State != ServerConnectionState.Reconnected)
        {
            return;
        }

        if (TryResurrect(out AdsException error))
        {
            if (CanLog(LogLevel.Information))
            {
                Logger?.LogInformation("Server Connection resurrected!");
            }
        }
        else if (CanLog(LogLevel.Warning))
        {
            Logger?.LogWarning("Couldn't resurrect server connection. Error: {Error}", error.ToString());
        }
    }

    //
    // Summary:
    //     Connect this ADS server to the local ADS router. Thrown if the connect call fails.
    //
    //
    // Returns:
    //     System.UInt32.
    //
    // Exceptions:
    //   T:System.Exception:
    //     Target not specified!
    private uint ConnectServer()
    {
        if (_target == null || _server == null)
        {
            throw new Exception("Target not specified!");
        }

        return _server.ConnectServer();
    }

    //
    // Summary:
    //     Connects the target
    //
    // Parameters:
    //   address:
    //     The address.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    public void Connect(AmsAddress address)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(ToString());
        }

        try
        {
            lock (_sync)
            {
                if (IsConnected)
                {
                    Disconnect();
                }

                _handleCache = new HandleCache(this, _loggerFactory);
                _notificationReceiver = new NotificationReceiver(this, _handleCache, this);
                _server = new AdsClientServer(_notificationReceiver, this, _configuration, _loggerFactory);
                _server.Timeout = Timeout;
                _server.ServerConnectionStateChanged += _server_ServerConnectionStateChanged;
                _target = address;
                if (CanLog(LogLevel.Debug))
                {
                    Logger?.LogDebug("Connect (Before), Client:{Name}, Address: {Target}, Local: {Local}", Name, Address, SourceAddress);
                }

                _server.ConnectServer();
                _isConnected = true;
                AddEventNotifications();
            }

            OnConnected();
        }
        catch (AmsPortNotAvailableException innerException)
        {
            throw new AdsException("Cannot connect to the TwinCAT Router. Please ensure that a router is running that supports the TCP/IP Loopback channel (TwinCAT >= 4024.10)!", innerException);
        }
        catch (AmsServerException)
        {
            throw;
        }
    }

    //
    // Summary:
    //     Connects to the target address asynchronously.
    //
    // Parameters:
    //   address:
    //     The address.
    //
    //   cancel:
    //     The cancellation token that can be used by other objects or threads to receive
    //     notice of cancellation.
    //
    // Returns:
    //     Task.
    public Task ConnectAsync(AmsAddress address, CancellationToken cancel)
    {
        Connect(address);
        return Task.CompletedTask;
    }

    //
    // Summary:
    //     Reads and sets the Encodings specifed in SymbolUploadInfo
    //
    // Returns:
    //     AdsErrorCode.
    private ResultValue<SymbolUploadInfo> tryReadEncodings()
    {
        AdsErrorCode errorCode = AdsErrorCode.NoError;
        if (IsConnected && (!_encodingsInitialized || _uploadInfo == null))
        {
            if (_target.NetId.IsSubAddress)
            {
                SetEncodings(Encoding.UTF8, Encoding.UTF8, 4);
                _encodingsInitialized = true;
                _uploadInfo = new SymbolUploadInfo(Encoding.UTF8, utf8EncodedStringData: true, is64Bit: false);
            }
            else
            {
                errorCode = SymbolLoaderFactory.TryReadSymbolUploadInfo(this, out SymbolUploadInfo symbolInfo);
                if (errorCode.Succeeded() || errorCode.NotSupported())
                {
                    _encodingsInitialized = true;
                    _uploadInfo = symbolInfo;
                }
            }
        }

        return new ResultValue<SymbolUploadInfo>(errorCode, _uploadInfo);
    }

    //
    // Summary:
    //     Reads and sets the Encodings specifed in SymbolUploadInfo
    //
    // Parameters:
    //   cancel:
    //     The cancellation token that can be used by other objects or threads to receive
    //     notice of cancellation.
    //
    // Returns:
    //     A Task<AdsErrorCode> representing the asynchronous operation.
    private async Task<ResultValue<SymbolUploadInfo>> tryReadEncodingsAsync(CancellationToken cancel)
    {
        ResultValue<SymbolUploadInfo> resultValue = new ResultValue<SymbolUploadInfo>(AdsErrorCode.NoError, _uploadInfo);
        if (IsConnected && (!_encodingsInitialized || _uploadInfo == null))
        {
            if (_target.NetId.IsSubAddress)
            {
                SetEncodings(Encoding.UTF8, Encoding.UTF8, 4);
                _encodingsInitialized = true;
                _uploadInfo = new SymbolUploadInfo(Encoding.UTF8, utf8EncodedStringData: true, is64Bit: false);
                resultValue = new ResultValue<SymbolUploadInfo>(_uploadInfo);
            }
            else
            {
                resultValue = await SymbolLoaderFactory.readSymbolUploadInfoAsync(this, cancel).ConfigureAwait(continueOnCapturedContext: false);
                if (resultValue.ErrorCode.Succeeded() || resultValue.ErrorCode.NotSupported())
                {
                    _encodingsInitialized = true;
                    _uploadInfo = resultValue.Value;
                }
            }
        }

        return resultValue;
    }

    //
    // Summary:
    //     Connects to the target ADS Device.
    //
    // Parameters:
    //   netId:
    //     The AmsNetId of the target device.
    //
    //   port:
    //     The Ams Port number on the target device to connect to.
    public void Connect(AmsNetId netId, int port)
    {
        Connect(new AmsAddress(netId, port));
    }

    //
    // Summary:
    //     Connects to the local target ADS Device.
    //
    // Parameters:
    //   port:
    //     The port number of the local ADS target device to connect to.
    public void Connect(int port)
    {
        Connect(AmsNetId.Local, port);
    }

    //
    // Summary:
    //     Connects to the local target ADS Device.
    //
    // Parameters:
    //   port:
    //     The port number of the local ADS target device to connect to.
    public void Connect(AmsPort port)
    {
        Connect((int)port);
    }

    //
    // Summary:
    //     Connects to the target ADS Device.
    //
    // Parameters:
    //   netId:
    //     The TwinCAT.Ads.AmsNetId of the ADS target device specified as string.
    //
    //   port:
    //     The port number of the ADS target device.
    public void Connect(AmsNetId netId, AmsPort port)
    {
        Connect(netId, (int)port);
    }

    //
    // Summary:
    //     Connects to the target ADS Device.
    //
    // Parameters:
    //   netId:
    //     The TwinCAT.Ads.AmsNetId of the ADS target device specified as string.
    //
    //   port:
    //     The port number of the ADS target device.
    public void Connect(string netId, int port)
    {
        AmsNetId netId2 = AmsNetId.Parse(netId);
        Connect(netId2, port);
    }

    //
    // Summary:
    //     Handler function that is called, when the TwinCAT.Ads.AdsClient is connected.
    private void OnConnected()
    {
        OnConnectionStateChanged(ConnectionState.Connected, ConnectionState.Disconnected);
    }

    //
    // Summary:
    //     Adds the event notifications.
    private AdsErrorCode AddEventNotifications()
    {
        AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
        if (_notificationReceiver == null)
        {
            AdsErrorCode.ClientPortNotOpen.ThrowOnError();
        }

        if (_adsStateChangedEventHandlerDelegate != null)
        {
            adsErrorCode = registerStateChangedNotification(_timeout);
            if (adsErrorCode.Failed())
            {
                AdsErrorException ex = AdsErrorException.Create(ResMan.GetString("AdsStateChangedRegistrationFailed_Message"), adsErrorCode);
                Logger?.LogWarning(ex.Message);
            }
        }

        if (_symbolVersionChangedDelegate != null)
        {
            adsErrorCode = registerSymbolVersionChangedNotification(_timeout);
            if (adsErrorCode.Failed() && adsErrorCode != AdsErrorCode.DeviceServiceNotSupported)
            {
                AdsErrorException ex2 = AdsErrorException.Create(ResMan.GetString("SymbolVersionChangedRegistrationFailed_Message"), adsErrorCode);
                Logger?.LogWarning(ex2.Message);
            }
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Disconnects this TwinCAT.Ads.AdsClient from the local ADS router.
    //
    // Returns:
    //     true if disconnected, false otherwise.
    public bool Disconnect()
    {
        return OnDisconnect();
    }

    //
    // Summary:
    //     Disconnects this TwinCAT.Ads.AdsClient from the local ADS router.
    //
    // Returns:
    //     true if disconnected, false otherwise.
    public Task<bool> DisconnectAsync(CancellationToken cancel)
    {
        return OnDisconnectAsync(cancel);
    }

    //
    // Summary:
    //     Called when the TwinCAT.Ads.AdsClient is about to be disconnected.
    //
    // Returns:
    //     true if disconnected, false otherwise.
    private bool OnDisconnect()
    {
        bool flag = true;
        bool flag2 = false;
        if (CanLog(LogLevel.Debug))
        {
            Logger?.LogDebug("Before OnDisconnect: Client:{Name}, Address: {Address}", Name, Address);
        }

        lock (_sync)
        {
            flag2 = _isConnected;
            if (_symbolCache is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _symbolCache = null;
            if (_notificationReceiver != null)
            {
                _notificationReceiver.Dispose();
                _notificationReceiver = null;
                _symbolVersionChangedNotificationRegistered = false;
                _stateChangedNotificationRegistered = false;
            }

            if (_handleCache != null)
            {
                _handleCache.Dispose();
                _handleCache = null;
            }

            if (_server != null)
            {
                _server.Dispose();
            }

            _server = null;
            _isConnected = false;
        }

        if (CanLog(LogLevel.Debug))
        {
            Logger?.LogDebug("OnDisconnect: Client:{Name}, Address: {Address}", Name, Address);
        }

        if (flag && flag2)
        {
            OnConnectionStateChanged(ConnectionState.Disconnected, ConnectionState.Connected);
        }

        return true;
    }

    private Task<bool> OnDisconnectAsync(CancellationToken cancel)
    {
        if (CanLog(LogLevel.Debug))
        {
            Logger?.LogDebug("Before OnDisconnectAsync: Client:{Name}, Address: {Address}", Name, Address);
        }

        return Task.FromResult(OnDisconnect());
    }

    private static void ThrowIfFailed(Func<AdsErrorCode> action, string errorMessage)
    {
        AdsErrorCode adsErrorCode = action();
        if (adsErrorCode != 0)
        {
            if (errorMessage != null)
            {
                throw new AdsErrorException(errorMessage, adsErrorCode);
            }

            adsErrorCode.ThrowOnError();
        }
    }

    //
    // Summary:
    //     Throws an TwinCAT.Ads.AdsErrorException with the specified errorMessage, if the
    //     return value of the Function indicates an error.
    //
    // Parameters:
    //   action:
    //     The action.
    //
    //   errorMessage:
    //     The error message.
    //
    // Exceptions:
    //   T:TwinCAT.Ads.AdsErrorException:
    private static void ThrowIfFailed(Func<ResultAds> action, string errorMessage)
    {
        ResultAds resultAds = action();
        if (resultAds.ErrorCode != 0)
        {
            if (errorMessage != null)
            {
                throw new AdsErrorException(errorMessage, resultAds.ErrorCode);
            }

            resultAds.ErrorCode.ThrowOnError();
        }
    }

    private AdsErrorCode registerStateChangedNotification(int timeout)
    {
        AdsErrorCode adsErrorCode = AdsErrorCode.None;
        if (!_stateChangedNotificationRegistered)
        {
            if (_notificationReceiver != null)
            {
                adsErrorCode = _notificationReceiver.RegisterStateChangedNotification(NotificationSettings.ImmediatelyOnChange, timeout);
            }

            if (adsErrorCode.Succeeded())
            {
                _stateChangedNotificationRegistered = true;
            }
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Unregisters the state changed notification.
    //
    // Parameters:
    //   timeout:
    //     The timeout.
    //
    // Returns:
    //     AdsErrorCode.
    private AdsErrorCode unregisterStateChangedNotification(int timeout)
    {
        AdsErrorCode adsErrorCode = AdsErrorCode.None;
        if (_stateChangedNotificationRegistered && _notificationReceiver != null)
        {
            adsErrorCode = _notificationReceiver.UnregisterStateChangedNotification(timeout);
            if (adsErrorCode.Succeeded())
            {
                _stateChangedNotificationRegistered = false;
            }
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Registers for TwinCAT.Ads.AdsClient.AdsStateChanged events as an asynchronous
    //     operation.
    //
    // Parameters:
    //   handler:
    //     The handler function to be registered for AdsStateChanged calls.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'RegisterAdsStateChanged' operation.
    //     The TwinCAT.Ads.ResultAds parameter contains the state the TwinCAT.Ads.ResultAds.ErrorCode
    //     of the ADS communication after execution.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    public async Task<ResultAds> RegisterAdsStateChangedAsync(EventHandler<AdsStateChangedEventArgs> handler, CancellationToken cancel)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        ResultAds resultAds = ResultAds.CreateError(AdsErrorCode.InternalError);
        _adsStateChangedEventHandlerDelegate = (EventHandler<AdsStateChangedEventArgs>)Delegate.Combine(_adsStateChangedEventHandlerDelegate, handler);
        if (IsConnected && !_stateChangedNotificationRegistered)
        {
            if (_notificationReceiver != null)
            {
                resultAds = await _notificationReceiver.RegisterStateChangedNotificationAsync(NotificationSettings.ImmediatelyOnChange, cancel).ConfigureAwait(continueOnCapturedContext: false);
            }

            if (resultAds.Succeeded)
            {
                _stateChangedNotificationRegistered = true;
            }
        }
        else
        {
            resultAds = ResultAds.CreateError(AdsErrorCode.NoError);
        }

        return resultAds;
    }

    //
    // Summary:
    //     unregister ads state changed as an asynchronous operation.
    //
    // Parameters:
    //   handler:
    //     The handler function to be unregistered.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'UnregisterAdsStateChanged' operation.
    //     The TwinCAT.Ads.ResultAds parameter contains the state the TwinCAT.Ads.ResultAds.ErrorCode
    //     of the ADS communication after execution.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    public async Task<ResultAds> UnregisterAdsStateChangedAsync(EventHandler<AdsStateChangedEventArgs> handler, CancellationToken cancel)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        _adsStateChangedEventHandlerDelegate = (EventHandler<AdsStateChangedEventArgs>)Delegate.Remove(_adsStateChangedEventHandlerDelegate, handler);
        ResultAds resultAds = ResultAds.CreateError(AdsErrorCode.InternalError);
        if (_adsStateChangedEventHandlerDelegate == null && IsConnected && _stateChangedNotificationRegistered)
        {
            if (_notificationReceiver != null)
            {
                resultAds = await _notificationReceiver.UnregisterStateChangedNotificationAsync(cancel).ConfigureAwait(continueOnCapturedContext: false);
            }

            if (resultAds.Succeeded)
            {
                _stateChangedNotificationRegistered = false;
            }
        }
        else
        {
            resultAds = ResultAds.CreateError(AdsErrorCode.NoError);
        }

        return resultAds;
    }

    //
    // Summary:
    //     Registers for the TwinCAT.Ads.AdsClient.AdsSymbolVersionChanged event.
    //
    // Parameters:
    //   timeout:
    //     The timeout.
    //
    // Returns:
    //     AdsErrorCode.
    private AdsErrorCode registerSymbolVersionChangedNotification(int timeout)
    {
        AdsErrorCode adsErrorCode = AdsErrorCode.None;
        if (!_symbolVersionChangedNotificationRegistered)
        {
            adsErrorCode = _notificationReceiver.RegisterSymbolVersionChangedNotification(NotificationSettings.ImmediatelyOnChange, timeout);
            if (adsErrorCode.Succeeded())
            {
                _symbolVersionChangedNotificationRegistered = true;
            }
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Unregisters from the TwinCAT.Ads.AdsClient.AdsSymbolVersionChanged event.
    //
    // Parameters:
    //   timeout:
    //     The timeout.
    //
    // Returns:
    //     AdsErrorCode.
    private AdsErrorCode unregisterSymbolVersionChangedNotification(int timeout)
    {
        AdsErrorCode adsErrorCode = AdsErrorCode.None;
        if (_symbolVersionChangedNotificationRegistered && _notificationReceiver != null)
        {
            adsErrorCode = _notificationReceiver.UnregisterSymbolVersionChangedNotification(timeout);
            if (adsErrorCode.Succeeded())
            {
                _symbolVersionChangedNotificationRegistered = false;
            }
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Registers for an TwinCAT.Ads.AdsClient.AdsSymbolVersionChanged event as an asynchronous
    //     operation.
    //
    // Parameters:
    //   handler:
    //     The handler function to register.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'RegisterSymbolVersionChanged' operation.
    //     The TwinCAT.Ads.ResultAds parameter contains the value TwinCAT.Ads.ResultAds.ErrorCode
    //     of the ADS communication after execution.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    public async Task<ResultAds> RegisterSymbolVersionChangedAsync(EventHandler<AdsSymbolVersionChangedEventArgs> handler, CancellationToken cancel)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        _symbolVersionChangedDelegate = (EventHandler<AdsSymbolVersionChangedEventArgs>)Delegate.Combine(_symbolVersionChangedDelegate, handler);
        ResultAds resultAds = ResultAds.CreateError(AdsErrorCode.InternalError);
        if (IsConnected && !_symbolVersionChangedNotificationRegistered)
        {
            if (_notificationReceiver != null)
            {
                resultAds = await _notificationReceiver.RegisterSymbolVersionChangedNotificationAsync(NotificationSettings.ImmediatelyOnChange, cancel).ConfigureAwait(continueOnCapturedContext: false);
            }

            if (resultAds.Succeeded)
            {
                _symbolVersionChangedNotificationRegistered = true;
            }
            else if (resultAds.ErrorCode != AdsErrorCode.DeviceServiceNotSupported)
            {
                AdsErrorException e = AdsErrorException.Create(ResMan.GetString("SymbolVersionChangedRegistrationFailed_Message"), resultAds.ErrorCode);
                ((INotificationReceiver)this).OnNotificationError((Exception)e);
            }
        }
        else
        {
            resultAds = ResultAds.CreateError(AdsErrorCode.NoError);
        }

        return resultAds;
    }

    //
    // Summary:
    //     Registers for an TwinCAT.Ads.AdsClient.AdsSymbolVersionChanged event as an asynchronous
    //     operation.
    //
    // Parameters:
    //   handler:
    //     The handler function to register.
    //
    // Returns:
    //     A task that represents the asynchronous 'RegisterSymbolVersionChanged' operation.
    //     The TwinCAT.Ads.ResultAds parameter contains the value TwinCAT.Ads.ResultAds.ErrorCode
    //     of the ADS communication after execution.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    public AdsErrorCode RegisterSymbolVersionChanged(EventHandler<AdsSymbolVersionChangedEventArgs> handler)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
        if (_symbolVersionChangedDelegate == null && _notificationReceiver != null)
        {
            adsErrorCode = _notificationReceiver.RegisterSymbolVersionChangedNotification(NotificationSettings.ImmediatelyOnChange, _timeout);
            if (adsErrorCode.Succeeded())
            {
                _symbolVersionChangedNotificationRegistered = true;
            }
            else if (adsErrorCode != AdsErrorCode.DeviceServiceNotSupported)
            {
                AdsErrorException e = AdsErrorException.Create(ResMan.GetString("SymbolVersionChangedRegistrationFailed_Message"), adsErrorCode);
                ((INotificationReceiver)this).OnNotificationError((Exception)e);
            }
        }

        _symbolVersionChangedDelegate = (EventHandler<AdsSymbolVersionChangedEventArgs>)Delegate.Combine(_symbolVersionChangedDelegate, handler);
        return adsErrorCode;
    }

    //
    // Summary:
    //     Unregisters from an TwinCAT.Ads.AdsClient.AdsSymbolVersionChanged event as an
    //     asynchronous operation.
    //
    // Parameters:
    //   handler:
    //     The handler function to unregister.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'UnregisterSymbolVersionChangedAsync'
    //     operation. The TwinCAT.Ads.ResultAds parameter contains the value TwinCAT.Ads.ResultAds.ErrorCode
    //     of the ADS communication after execution.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    public async Task<ResultAds> UnregisterSymbolVersionChangedAsync(EventHandler<AdsSymbolVersionChangedEventArgs> handler, CancellationToken cancel)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        _symbolVersionChangedDelegate = (EventHandler<AdsSymbolVersionChangedEventArgs>)Delegate.Remove(_symbolVersionChangedDelegate, handler);
        ResultAds result = ResultAds.CreateError(AdsErrorCode.InternalError);
        if (_symbolVersionChangedDelegate == null && IsConnected && _symbolVersionChangedNotificationRegistered)
        {
            if (_notificationReceiver != null)
            {
                result = await _notificationReceiver.UnregisterSymbolVersionChangedNotificationAsync(cancel).ConfigureAwait(continueOnCapturedContext: false);
            }

            _symbolVersionChangedNotificationRegistered = false;
        }
        else
        {
            result = ResultAds.CreateError(AdsErrorCode.NoError);
        }

        return result;
    }

    //
    // Summary:
    //     Unregisters the symbol version changed.
    //
    // Parameters:
    //   handler:
    //     The handler function to unregister.
    //
    // Returns:
    //     AdsErrorCode.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    public AdsErrorCode UnregisterSymbolVersionChanged(EventHandler<AdsSymbolVersionChangedEventArgs> handler)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        AdsErrorCode result = AdsErrorCode.NoError;
        _symbolVersionChangedDelegate = (EventHandler<AdsSymbolVersionChangedEventArgs>)Delegate.Remove(_symbolVersionChangedDelegate, handler);
        if (_symbolVersionChangedDelegate == null && _notificationReceiver != null)
        {
            result = _notificationReceiver.UnregisterSymbolVersionChangedNotification(_timeout);
        }

        return result;
    }

    //
    // Summary:
    //     Handler Function for a Router Notification.
    //
    // Parameters:
    //   state:
    //     The route state.
    void IRouterNotificationReceiver.OnRouterNotification(AmsRouterState state)
    {
        AmsRouterState oldState = _routerState;
        if (oldState == state)
        {
            return;
        }

        _routerState = state;
        this.SetRouterState(state);
        if (CanLog(LogLevel.Information))
        {
            _logger?.LogInformation("TwinCAT Router sent '{State}' signal", state);
        }

        Task.Run(delegate
        {
            switch (state)
            {
                case AmsRouterState.Started:
                    {
                        if (oldState == AmsRouterState.Stopped && !TryResurrect(out AdsException error))
                        {
                            _logger?.LogWarning(error.Message);
                        }

                        break;
                    }
            }

            if (_amsRouterNotificationEventHandlerDelegate != null)
            {
                try
                {
                    _amsRouterNotificationEventHandlerDelegate(this, new AmsRouterNotificationEventArgs(state));
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception, "OnRouterNotification failed!");
                }
            }
        });
    }

    void IRouterNotificationReceiver.OnSystemServiceRemoved()
    {
        _ = _routerState;
        _logger?.LogInformation("System Service Removed");
    }

    //
    // Summary:
    //     Handles the SymbolVersionChanged event.
    //
    // Parameters:
    //   eventArgs:
    //     The TwinCAT.Ads.AdsSymbolVersionChangedEventArgs instance containing the event
    //     data.
    void ISymbolVersionChangedReceiver.OnSymbolVersionChanged(AdsSymbolVersionChangedEventArgs eventArgs)
    {
        if (_symbolVersionChangedDelegate != null)
        {
            try
            {
                _symbolVersionChangedDelegate(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }
        }
    }

    //
    // Summary:
    //     Handles the AdsStateChanged event.
    //
    // Parameters:
    //   eventArgs:
    //     The TwinCAT.Ads.AdsStateChangedEventArgs instance containing the event data.
    void IStateChangedReceiver.OnAdsStateChanged(AdsStateChangedEventArgs eventArgs)
    {
        if (_adsStateChangedEventHandlerDelegate != null)
        {
            _adsStateChangedEventHandlerDelegate(this, eventArgs);
        }
    }

    //
    // Summary:
    //     Handler function for Notification errors.
    //
    // Parameters:
    //   timeStamp:
    //     The time stamp.
    //
    //   notifications:
    //     The notifications.
    void INotificationReceiver.OnNotificationError(DateTimeOffset timeStamp, IList<Notification> notifications)
    {
        if (this.AdsNotificationError == null)
        {
            return;
        }

        foreach (Notification notification in notifications)
        {
            _logger?.LogError("Notification error Handle: {Handle}", notification.Handle);
            this.AdsNotificationError(this, new AdsNotificationErrorEventArgs(new AdsInvalidNotificationException(notification.Handle, timeStamp)));
        }
    }

    //
    // Summary:
    //     Handler function for Notification errors.
    //
    // Parameters:
    //   e:
    //     The e.
    void INotificationReceiver.OnNotificationError(Exception e)
    {
        _logger?.LogError(e.Message);
        if (this.AdsNotificationError != null)
        {
            this.AdsNotificationError(this, new AdsNotificationErrorEventArgs(e));
        }
    }

    //
    // Summary:
    //     Handler function Raw Notifications
    //
    // Parameters:
    //   timeStamp:
    //     The time stamp.
    //
    //   notifications:
    void INotificationReceiver.OnNotification(DateTimeOffset timeStamp, IList<Notification> notifications)
    {
        bool num = this.AdsNotification != null;
        bool flag = this.AdsNotificationEx != null;
        OnAdsSumNotifications(timeStamp, notifications);
        if (!(num || flag))
        {
            return;
        }

        foreach (Notification notification in notifications)
        {
            bool flag2 = false;
            if (this.AdsNotificationEx != null)
            {
                OnAdsNotificationEx(notification);
                flag2 = true;
            }

            if (this.AdsNotification != null)
            {
                OnAdsNotification(notification);
                flag2 = true;
            }

            if (!flag2)
            {
                Logger?.LogWarning("Notification event not registered!");
            }
        }
    }

    void INotificationReceiver.OnInvalidateHandles(DateTimeOffset timeStamp, IList<Notification> notifications)
    {
        OnAdsNotificationsInvalidated(timeStamp, notifications);
    }

    //
    // Summary:
    //     Called when [ads notification].
    //
    // Parameters:
    //   notification:
    //     The notification.
    private void OnAdsNotification(Notification notification)
    {
        if (this.AdsNotification != null)
        {
            this.AdsNotification(this, new AdsNotificationEventArgs(notification));
        }
    }

    //
    // Summary:
    //     Called when [ads notification ex].
    //
    // Parameters:
    //   notification:
    //     The notification.
    private void OnAdsNotificationEx(Notification notification)
    {
        if (this.AdsNotificationEx != null && notification.UserData != null && notification.UserData.GetType() == typeof(AdsNotificationExUserData))
        {
            AdsNotificationExUserData adsNotificationExUserData = (AdsNotificationExUserData)notification.UserData;
            _anyTypeMarshaller.Unmarshal(adsNotificationExUserData.type, adsNotificationExUserData.args, notification.Data.Span, DefaultValueEncoding, out object value);
            Notification notification2 = new Notification(notification.Handle, notification.TimeStamp, adsNotificationExUserData.userData, notification.Data);
            this.AdsNotificationEx(this, new AdsNotificationExEventArgs(notification2, value));
        }
    }

    //
    // Summary:
    //     Called when [ads sum notifications].
    //
    // Parameters:
    //   timeStamp:
    //     The time stamp.
    //
    //   notifications:
    //     The notifications.
    private void OnAdsSumNotifications(DateTimeOffset timeStamp, IList<Notification> notifications)
    {
        if (this.AdsSumNotification != null)
        {
            this.AdsSumNotification(this, new AdsSumNotificationEventArgs(timeStamp, notifications));
        }
    }

    private void OnAdsNotificationsInvalidated(DateTimeOffset timeStamp, IList<Notification> notifications)
    {
        if (this.AdsNotificationsInvalidated != null)
        {
            this.AdsNotificationsInvalidated(this, new AdsNotificationsInvalidatedEventArgs(timeStamp, notifications));
        }
    }

    //
    // Summary:
    //     Sets the Timeout internally.
    //
    // Parameters:
    //   value:
    //     The value.
    private void OnSetTimout(int value)
    {
        _timeout = value;
        if (CanLog(LogLevel.Debug))
        {
            Logger?.LogDebug("AdsClient: {Name}. Timeout set to '{Timeout}' ms", Name, _timeout);
        }
    }

    //
    // Summary:
    //     Reads the identification and version number of an ADS server.
    //
    // Returns:
    //     DeviceInfo struct containing the name of the device and the version information.
    //
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public DeviceInfo ReadDeviceInfo()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ResultDeviceInfo resultDeviceInfo = ReadDeviceInfoSync();
        resultDeviceInfo.ThrowOnError();
        return resultDeviceInfo.DeviceInfo;
    }

    //
    // Summary:
    //     Reads the identification and version number of an ADS server.
    //
    // Parameters:
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'ReadDeviceState' operation. The TwinCAT.Ads.ResultDeviceInfo
    //     parameter contains the value TwinCAT.Ads.ResultDeviceInfo.DeviceInfo and the
    //     TwinCAT.Ads.ResultAds.ErrorCode of the ADS communication after execution.
    public Task<ResultDeviceInfo> ReadDeviceInfoAsync(CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        Func<uint, Task<AdsErrorCode>> readStateRequest = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.ReadDeviceInfoRequestAsync(_target, id, cancel) : Task.FromResult(adsErrorCode);
        };
        Action<ResultDeviceInfo> confirmResult = delegate (ResultDeviceInfo r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        return _server.RequestReadDeviceInfoAsync(readStateRequest, confirmResult, _timeout, cancel);
    }

    //
    // Summary:
    //     Reads the identification and version number of an ADS server.
    //
    // Returns:
    //     A task that represents the asynchronous 'ReadDeviceState' operation. The TwinCAT.Ads.ResultDeviceInfo
    //     parameter contains the value TwinCAT.Ads.ResultDeviceInfo.DeviceInfo and the
    //     TwinCAT.Ads.ResultAds.ErrorCode of the ADS communication after execution.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    private ResultDeviceInfo ReadDeviceInfoSync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        Func<uint, AdsErrorCode> readStateRequest = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.ReadDeviceInfoRequestSync(_target, id) : adsErrorCode;
        };
        Action<ResultDeviceInfo> confirmResult = delegate (ResultDeviceInfo r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        return _server.RequestReadDeviceInfoSync(readStateRequest, confirmResult, _timeout);
    }

    //
    // Summary:
    //     Read data asynchronously.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   length:
    //     The length.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     Task<ResultReadBytes>.
    private Task<ResultReadBytes> readAsync(uint indexGroup, uint indexOffset, int length, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        Func<uint, Task<AdsErrorCode>> readRequest = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.ReadRequestAsync(_target, id, indexGroup, indexOffset, length, cancel) : Task.FromResult(adsErrorCode);
        };
        Action<ResultReadBytes> confirmResult = delegate (ResultReadBytes r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        return _server.RequestReadBytesAsync(readRequest, confirmResult, _timeout, cancel);
    }

    //
    // Summary:
    //     Read write as an asynchronous operation.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   readLength:
    //     Length of the read.
    //
    //   writeBuffer:
    //     The write buffer.
    //
    //   cancel:
    //     The cancellation token that can be used by other objects or threads to receive
    //     notice of cancellation.
    //
    // Returns:
    //     A Task<ResultReadWriteBytes> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    private async Task<ResultReadWriteBytes> ReadWriteAsync(uint indexGroup, uint indexOffset, int readLength, ReadOnlyMemory<byte> writeBuffer, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        Func<uint, Task<AdsErrorCode>> readRequest = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.ReadWriteRequestAsync(_target, id, indexGroup, indexOffset, readLength, writeBuffer, cancel) : Task.FromResult(adsErrorCode);
        };
        Action<ResultReadBytes> confirmResult = delegate (ResultReadBytes r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        ResultReadBytes resultReadBytes = await _server.RequestReadBytesAsync(readRequest, confirmResult, _timeout, cancel).ConfigureAwait(continueOnCapturedContext: false);
        return new ResultReadWriteBytes(resultReadBytes.ErrorCode, resultReadBytes.Data, resultReadBytes.InvokeId);
    }

    //
    // Summary:
    //     Reads the write synchronize.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   readLength:
    //     Length of the read.
    //
    //   writeBuffer:
    //     The write buffer.
    //
    // Returns:
    //     ResultReadWriteBytes.
    private ResultReadWriteBytes ReadWriteSync(uint indexGroup, uint indexOffset, int readLength, ReadOnlyMemory<byte> writeBuffer)
    {
        Func<uint, AdsErrorCode> readRequest = delegate
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.ReadWriteRequestSync(_target, (uint)Id, indexGroup, indexOffset, readLength, writeBuffer.Span) : adsErrorCode;
        };
        Action<ResultReadBytes> confirmResult = delegate (ResultReadBytes r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        ResultReadBytes resultReadBytes = _server.RequestAndReceiveReadBytesSync(readRequest, confirmResult, _timeout);
        return new ResultReadWriteBytes(resultReadBytes.ErrorCode, resultReadBytes.Data, resultReadBytes.InvokeId);
    }

    //
    // Summary:
    //     Adds a device notification as an asynchronous operation.
    //
    // Parameters:
    //   indexGroup:
    //     The index group number of the requested ADS service.
    //
    //   indexOffset:
    //     The index offset number of the requested ADS service.
    //
    //   dataSize:
    //     Maximum amount of data in bytes to receive with this ADS Notification.
    //
    //   settings:
    //     The notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    //   cancel:
    //     The Cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'AddDeviceNotification' operation. The
    //     TwinCAT.Ads.ResultHandle type parameter contains the created handle (TwinCAT.Ads.ResultHandle.Handle)
    //     and the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    //
    // Remarks:
    //     The
    //
    //     dataSize
    //
    //     Parameter defines the amount of bytes, that will be attached to the TwinCAT.Ads.AdsClient.AdsNotification
    //     as value. Because notifications allocate TwinCAT system resources, a complementary
    //     call to TwinCAT.Ads.IAdsNotifications.DeleteDeviceNotificationAsync(System.UInt32,System.Threading.CancellationToken)
    //     should always be called when the notification is not used anymore.
    public async Task<ResultHandle> AddDeviceNotificationAsync(uint indexGroup, uint indexOffset, int dataSize, NotificationSettings settings, object? userData, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        return await _notificationReceiver.AddDeviceNotificationAsync(indexGroup, indexOffset, dataSize, settings, userData, cancel).ConfigureAwait(continueOnCapturedContext: false);
    }

    //
    // Summary:
    //     Connects a variable to the ADS client. The ADS client will be notified by the
    //     TwinCAT.Ads.AdsClient.AdsNotification event.
    //
    // Parameters:
    //   symbolPath:
    //     Symbol / Instance path of the ADS variable.
    //
    //   dataSize:
    //     Maximum amount of data in bytes to receive with this ADS Notification.
    //
    //   settings:
    //     The settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    // Returns:
    //     The notification handle.
    //
    // Remarks:
    //     The
    //
    //     dataSize
    //
    //     Parameter defines the amount of bytes, that will be attached to the TwinCAT.Ads.AdsClient.AdsNotification
    //     as value. Because notifications allocate TwinCAT system resources, a complementary
    //     call to TwinCAT.Ads.AdsClient.DeleteDeviceNotification(System.UInt32) should
    //     always called when the notification is not used anymore.
    public uint AddDeviceNotification(string symbolPath, int dataSize, NotificationSettings settings, object? userData)
    {
        uint notificationHandle = 0u;
        TryAddDeviceNotification(symbolPath, dataSize, settings, userData, out notificationHandle).ThrowOnError();
        return notificationHandle;
    }

    //
    // Summary:
    //     Connects a variable to the ADS client. The ADS client will be notified by the
    //     TwinCAT.Ads.AdsClient.AdsNotification event.
    //
    // Parameters:
    //   symbolPath:
    //     The symbol/instance path of the ADS variable.
    //
    //   dataSize:
    //     Maximum amount of data in bytes to receive with this ADS Notification.
    //
    //   settings:
    //     The notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data.
    //
    //   notificationHandle:
    //     The notification handle.
    //
    // Returns:
    //     The ADS ErrorCode.
    //
    // Remarks:
    //     The
    //
    //     dataSize
    //
    //     Parameter defines the amount of bytes, that will be attached to the TwinCAT.Ads.AdsClient.AdsNotification
    //     as value. Because notifications allocate TwinCAT system resources, a complementary
    //     call to TwinCAT.Ads.AdsClient.TryDeleteDeviceNotification(System.UInt32) should
    //     always be called when the notification is not used anymore.
    public AdsErrorCode TryAddDeviceNotification(string symbolPath, int dataSize, NotificationSettings settings, object? userData, out uint notificationHandle)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        bool flag = false;
        uint handle = 0u;
        notificationHandle = 0u;
        AdsErrorCode adsErrorCode = AdsErrorCode.None;
        while (true)
        {
            adsErrorCode = _handleCache.TryCreateVariableHandle(symbolPath, _timeout, out handle);
            if (!adsErrorCode.Succeeded())
            {
                break;
            }

            adsErrorCode = TryAddDeviceNotification(61445u, handle, dataSize, settings, userData, out notificationHandle);
            if (adsErrorCode != AdsErrorCode.DeviceSymbolVersionInvalid || flag)
            {
                break;
            }

            adsErrorCode = _handleCache.Resurrect(handle);
            flag = true;
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Connects a variable to the ADS client. The ADS client will be notified by the
    //     TwinCAT.Ads.AdsClient.AdsNotificationEx event.
    //
    // Parameters:
    //   symbolPath:
    //     Symbol/Instance path of the ADS variable.
    //
    //   settings:
    //     The Notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    //   type:
    //     Type of the object stored in the event argument ('AnyType')
    //
    //   args:
    //     Additional arguments (for 'AnyType')
    //
    //   notificationHandle:
    //     The notification handle
    //
    // Returns:
    //     The ADS error code.
    //
    // Remarks:
    //     Because notifications allocate TwinCAT system resources, a complementary call
    //     to TwinCAT.Ads.AdsClient.TryDeleteDeviceNotification(System.UInt32) should always
    //     called when the notification is not used anymore.
    public AdsErrorCode TryAddDeviceNotificationEx(string symbolPath, NotificationSettings settings, object? userData, Type type, int[]? args, out uint notificationHandle)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        bool flag = false;
        uint handle = 0u;
        notificationHandle = 0u;
        AdsErrorCode adsErrorCode = AdsErrorCode.None;
        while (true)
        {
            adsErrorCode = _handleCache.TryCreateVariableHandle(symbolPath, _timeout, out handle);
            if (!adsErrorCode.Succeeded())
            {
                break;
            }

            adsErrorCode = TryAddDeviceNotificationEx(61445u, handle, settings, userData, type, args, out notificationHandle);
            if (adsErrorCode != AdsErrorCode.DeviceSymbolVersionInvalid || flag)
            {
                break;
            }

            adsErrorCode = _handleCache.Resurrect(handle);
            flag = true;
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Connects a variable to the ADS client asynchronously. The ADS client will be
    //     notified by the TwinCAT.Ads.AdsClient.AdsNotificationEx event.
    //
    // Parameters:
    //   symbolPath:
    //     The symbol/instance path of the ADS variable.
    //
    //   settings:
    //     The notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    //   type:
    //     Type of the object stored in the event argument ('AnyType')
    //
    //   args:
    //     Additional arguments (for 'AnyType')
    //
    //   cancel:
    //     The Cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'AddDeviceNotification' operation. The
    //     TwinCAT.Ads.ResultHandle type parameter contains the created handle (TwinCAT.Ads.ResultHandle.Handle)
    //     and the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    //
    // Remarks:
    //     Because notifications allocate TwinCAT system resources, a complementary call
    //     to TwinCAT.Ads.AdsClient.DeleteDeviceNotificationAsync(System.UInt32,System.Threading.CancellationToken)
    //     should always be called when the notification is not used anymore.
    public async Task<ResultHandle> AddDeviceNotificationExAsync(string symbolPath, NotificationSettings settings, object? userData, Type type, int[]? args, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        bool repeated = false;
        _ = ResultHandle.Empty;
        ResultHandle resultHandle;
        while (true)
        {
            resultHandle = await _handleCache.CreateVariableHandleAsync(symbolPath, cancel).ConfigureAwait(continueOnCapturedContext: false);
            if (!resultHandle.Succeeded)
            {
                break;
            }

            uint handle = resultHandle.Handle;
            resultHandle = await AddDeviceNotificationExAsync(61445u, resultHandle.Handle, settings, userData, type, args, cancel).ConfigureAwait(continueOnCapturedContext: false);
            if (resultHandle.ErrorCode != AdsErrorCode.DeviceSymbolVersionInvalid || repeated)
            {
                break;
            }

            await _handleCache.ResurrectAsync(handle, cancel).ConfigureAwait(continueOnCapturedContext: false);
            repeated = true;
        }

        return resultHandle;
    }

    //
    // Summary:
    //     Connects a variable to the ADS client asynchronously. The ADS client will be
    //     notified by the TwinCAT.Ads.AdsClient.AdsNotification event.
    //
    // Parameters:
    //   symbolPath:
    //     The symbol/instance path of the ADS variable.
    //
    //   dataSize:
    //     Maximum amount of data in bytes to receive with this ADS Notification.
    //
    //   settings:
    //     The notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    //   cancel:
    //     The Cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'AddDeviceNotification' operation. The
    //     TwinCAT.Ads.ResultHandle type parameter contains the created handle (TwinCAT.Ads.ResultHandle.Handle)
    //     and the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    //
    // Remarks:
    //     The
    //
    //     dataSize
    //
    //     Parameter defines the amount of bytes, that will be attached to the TwinCAT.Ads.AdsClient.AdsNotification
    //     as value. Because notifications allocate TwinCAT system resources, a complementary
    //     call to TwinCAT.Ads.AdsClient.DeleteDeviceNotificationAsync(System.UInt32,System.Threading.CancellationToken)
    //     should always be called when the notification is not used anymore.
    public async Task<ResultHandle> AddDeviceNotificationAsync(string symbolPath, int dataSize, NotificationSettings settings, object? userData, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        bool repeated = false;
        _ = ResultHandle.Empty;
        ResultHandle resultHandle;
        while (true)
        {
            resultHandle = await _handleCache.CreateVariableHandleAsync(symbolPath, cancel).ConfigureAwait(continueOnCapturedContext: false);
            if (!resultHandle.Succeeded)
            {
                break;
            }

            uint handle = resultHandle.Handle;
            resultHandle = await AddDeviceNotificationAsync(61445u, resultHandle.Handle, dataSize, settings, userData, cancel).ConfigureAwait(continueOnCapturedContext: false);
            if (resultHandle.ErrorCode != AdsErrorCode.DeviceSymbolVersionInvalid || repeated)
            {
                break;
            }

            await _handleCache.ResurrectAsync(handle, cancel).ConfigureAwait(continueOnCapturedContext: false);
            repeated = true;
        }

        return resultHandle;
    }

    //
    // Summary:
    //     Connects a variable to the ADS client. The ADS client will be notified by the
    //     AdsNotification event.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   dataSize:
    //     Maximum amount of data in bytes to receive with this ADS Notification.
    //
    //   settings:
    //     The Notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    // Returns:
    //     The notification handle.
    //
    // Remarks:
    //     The
    //
    //     dataSize
    //
    //     Parameter defines the amount of bytes, that will be attached to the TwinCAT.Ads.AdsClient.AdsNotification
    //     as value. Because notifications allocate TwinCAT system resources, a complementary
    //     call to TwinCAT.Ads.AdsClient.DeleteDeviceNotification(System.UInt32) should
    //     always called when the notification is not used anymore.
    public uint AddDeviceNotification(uint indexGroup, uint indexOffset, int dataSize, NotificationSettings settings, object? userData)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        uint handle = 0u;
        TryAddDeviceNotification(indexGroup, indexOffset, dataSize, settings, userData, out handle).ThrowOnError();
        return handle;
    }

    //
    // Summary:
    //     Connects a variable to the ADS client. The ADS client will be notified by the
    //     TwinCAT.Ads.AdsClient.AdsNotificationEx event.
    //
    // Parameters:
    //   symbolPath:
    //     Symbol/Instance path of the ADS variable.
    //
    //   settings:
    //     The Notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    //   type:
    //     Type of the object stored in the event argument ('AnyType')
    //
    // Returns:
    //     The notification handle.
    //
    // Remarks:
    //     Because notifications allocate TwinCAT system resources, a complementary call
    //     to TwinCAT.Ads.AdsClient.DeleteDeviceNotification(System.UInt32) should always
    //     called when the notification is not used anymore.
    public uint AddDeviceNotificationEx(string symbolPath, NotificationSettings settings, object? userData, Type type)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        uint notificationHandle = 0u;
        TryAddDeviceNotificationEx(symbolPath, settings, userData, type, null, out notificationHandle).ThrowOnError();
        return notificationHandle;
    }

    //
    // Summary:
    //     Connects a variable to the ADS client. The ADS client will be notified by the
    //     TwinCAT.Ads.AdsClient.AdsNotificationEx event.
    //
    // Parameters:
    //   symbolPath:
    //     Symbol/Instance path of the ADS variable.
    //
    //   settings:
    //     The Notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    //   type:
    //     Type of the object stored in the event argument ('AnyType')
    //
    //   args:
    //     Additional arguments (for 'AnyType')
    //
    // Returns:
    //     The notification handle.
    //
    // Remarks:
    //     Because notifications allocate TwinCAT system resources, a complementary call
    //     to TwinCAT.Ads.AdsClient.DeleteDeviceNotification(System.UInt32) should always
    //     called when the notification is not used anymore.
    public uint AddDeviceNotificationEx(string symbolPath, NotificationSettings settings, object? userData, Type type, int[]? args)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        uint notificationHandle = 0u;
        TryAddDeviceNotificationEx(symbolPath, settings, userData, type, args, out notificationHandle).ThrowOnError();
        return notificationHandle;
    }

    //
    // Summary:
    //     Connects a variable to the ADS client. The ADS client will be notified by the
    //     TwinCAT.Ads.AdsClient.AdsNotificationEx event.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   settings:
    //     The Notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    //   type:
    //     Type of the object stored in the event argument ('AnyType')
    //
    // Returns:
    //     The notification handle.
    //
    // Remarks:
    //     Because notifications allocate TwinCAT system resources, a complementary call
    //     to TwinCAT.Ads.AdsClient.DeleteDeviceNotification(System.UInt32) should always
    //     called when the notification is not used anymore.
    public uint AddDeviceNotificationEx(uint indexGroup, uint indexOffset, NotificationSettings settings, object? userData, Type type)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        return AddDeviceNotificationEx(indexGroup, indexOffset, settings, userData, type, null);
    }

    //
    // Summary:
    //     Connects a variable to the ADS client. The ADS client will be notified by the
    //     AdsNotification event. If type is a string type, the first element of the parameter
    //     args specifies the number of characters of the string. If type is an array type,
    //     the number of elements for each dimension has to be specified in the parameter
    //     args. Only primitive ('AnyType') types are allowed for the parameter type.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   settings:
    //     The Notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data.
    //
    //   type:
    //     Type of the object stored in the event argument.
    //
    //   args:
    //     Additional arguments for 'AnyType' types.
    //
    // Returns:
    //     The notification handle.
    //
    // Remarks:
    //     Because notifications allocate TwinCAT system resources, a complementary call
    //     to TwinCAT.Ads.AdsClient.DeleteDeviceNotification(System.UInt32) should always
    //     called when the notification is not used anymore.
    public uint AddDeviceNotificationEx(uint indexGroup, uint indexOffset, NotificationSettings settings, object? userData, Type type, int[]? args)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        uint handle = 0u;
        TryAddDeviceNotificationEx(indexGroup, indexOffset, settings, userData, type, args, out handle).ThrowOnError();
        return handle;
    }

    //
    // Summary:
    //     Deletes a registered notification.
    //
    // Parameters:
    //   notificationHandle:
    //     Notification handle.
    //
    // Remarks:
    //     This is the complementary method to TwinCAT.Ads.IAdsNotifications.AddDeviceNotification
    //     overloads and should be called when the notification is not needed anymore the
    //     free TwinCAT realtime resources.
    public void DeleteDeviceNotification(uint notificationHandle)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        TryDeleteDeviceNotification(notificationHandle).ThrowOnError();
    }

    //
    // Summary:
    //     Writes the state asynchronously
    //
    // Parameters:
    //   adsState:
    //     State of the ads.
    //
    //   deviceState:
    //     State of the device.
    //
    //   writeData:
    //     The write buffer.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'ReadState' operation. The TwinCAT.Ads.ResultAds
    //     parameter contains the TwinCAT.Ads.ResultAds.ErrorCode of the ADS communication
    //     after execution.
    public Task<ResultAds> WriteControlAsync(AdsState adsState, ushort deviceState, ReadOnlyMemory<byte> writeData, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        StateInfo info = new StateInfo(adsState, (short)deviceState);
        Func<uint, Task<AdsErrorCode>> request = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeWriteState(info);
                if (adsErrorCode.Succeeded())
                {
                    adsErrorCode = _interceptors.BeforeCommunicate();
                }
            }

            return adsErrorCode.Succeeded() ? _server.WriteControlRequestAsync(_target, id, adsState, deviceState, writeData, cancel) : Task.FromResult(adsErrorCode);
        };
        Action<ResultAds> confirmResult = delegate (ResultAds r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
                _interceptors.AfterWriteState(info, r);
            }
        };
        return _server.RequestAsync(request, confirmResult, _timeout, cancel);
    }

    //
    // Summary:
    //     Write Control (synchronous)
    //
    // Parameters:
    //   adsState:
    //     AdsState.
    //
    //   deviceState:
    //     DeviceState
    //
    //   writeData:
    //     Write data
    //
    // Returns:
    //     ResultAds.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    private ResultAds WriteControlSync(AdsState adsState, ushort deviceState, ReadOnlyMemory<byte> writeData)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        StateInfo info = new StateInfo(adsState, (short)deviceState);
        Func<uint, AdsErrorCode> request = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeWriteState(info);
                if (adsErrorCode.Succeeded())
                {
                    adsErrorCode = _interceptors.BeforeCommunicate();
                }
            }

            return adsErrorCode.Succeeded() ? _server.WriteControlRequestSync(_target, id, adsState, deviceState, writeData.Span) : adsErrorCode;
        };
        Action<ResultAds> confirmResult = delegate (ResultAds r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
                _interceptors.AfterWriteState(info, r);
            }
        };
        return _server.RequestAndReceiveSync(request, confirmResult, _timeout);
    }

    //
    // Summary:
    //     Closes this TwinCAT.Ads.AdsClient
    public void Close()
    {
        Dispose();
    }

    //
    // Summary:
    //     (Re)Connects the TwinCAT.IConnection when disconnected.
    //
    // Returns:
    //     true if succeeded, false otherwise.
    bool IConnection.Connect()
    {
        if (_target == null)
        {
            throw new ClientNotConnectedException(this);
        }

        Connect(_target);
        return true;
    }

    //
    // Summary:
    //     (Re)Connects the TwinCAT.IConnection when disconnected.
    //
    // Returns:
    //     true if succeeded, false otherwise.
    async Task<bool> IConnection.ConnectAsync(CancellationToken cancel)
    {
        if (_target == null)
        {
            throw new ClientNotConnectedException(this);
        }

        await ConnectAsync(_target, cancel).ConfigureAwait(continueOnCapturedContext: false);
        return true;
    }

    //
    // Summary:
    //     (Re)Connects the TwinCAT.IConnection when disconnected.
    //
    // Returns:
    //     true if succeeded, false otherwise.
    Task IConnection.ConnectAndWaitAsync(CancellationToken cancel)
    {
        if (_target == null)
        {
            throw new ClientNotConnectedException(this);
        }

        return ConnectAndWaitAsync(_target, cancel);
    }

    //
    // Summary:
    //     Reads as string from a specified address.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   len:
    //     The string length to be read.
    //
    //   encoding:
    //     The string value encoding (IAdsConnection.DefaultValueEncoding if null)
    //
    // Returns:
    //     System.String.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public string ReadAnyString(uint indexGroup, uint indexOffset, int len, Encoding? encoding)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (encoding == null)
        {
            encoding = DefaultValueEncoding;
        }

        StringMarshaler stringMarshaler = new StringMarshaler(encoding, StringConvertMode.FixedLength);
        int num = 0;
        bool flag = false;
        try
        {
            num = stringMarshaler.MarshalSize(encoding, len);
        }
        catch (NotSupportedException)
        {
            num = 4 * len;
            flag = true;
        }

        byte[] array = new byte[num];
        int readBytes = 0;
        TryRead(indexGroup, indexOffset, array.AsMemory(), out readBytes).ThrowOnError();
        stringMarshaler.Unmarshal(array.AsSpan(0, readBytes), encoding, out string value);
        if (flag && value.Length > len)
        {
            return value.Substring(0, len);
        }

        return value;
    }

    //
    // Summary:
    //     Reads a string from a specified address asynchronously.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   len:
    //     The string length to be read.
    //
    //   encoding:
    //     The string value encoding (IAdsConnection.DefaultValueEncoding if null)
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous read operation. The TwinCAT.Ads.ResultAnyValue
    //     parameter contains the read value (TwinCAT.Ads.ResultValue`1.Value) and the TwinCAT.Ads.ResultAds.ErrorCode
    //     after execution.
    public async Task<ResultAnyValue> ReadAnyStringAsync(uint indexGroup, uint indexOffset, int len, Encoding? encoding, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (encoding == null)
        {
            encoding = DefaultValueEncoding;
        }

        StringMarshaler marshaler = new StringMarshaler(encoding, StringConvertMode.FixedLength);
        bool shorten = false;
        int num;
        try
        {
            num = marshaler.MarshalSize(encoding, len);
        }
        catch (NotSupportedException)
        {
            num = 4 * len;
            shorten = true;
        }

        byte[] data = new byte[num];
        ResultRead resultRead = await ReadAsync(indexGroup, indexOffset, data.AsMemory(), cancel).ConfigureAwait(continueOnCapturedContext: false);
        ResultAnyValue result;
        if (resultRead.Succeeded)
        {
            marshaler.Unmarshal(data.AsSpan(0, resultRead.ReadBytes), encoding, out string value);
            if (shorten && value.Length > len)
            {
                value = value.Substring(0, len);
            }

            result = new ResultAnyValue(resultRead.ErrorCode, value, resultRead.InvokeId);
        }
        else
        {
            result = new ResultAnyValue(resultRead.ErrorCode, null, resultRead.InvokeId);
        }

        return result;
    }

    //
    // Summary:
    //     Reads a string from the specified symbol/variable.
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   len:
    //     The length.
    //
    //   encoding:
    //     The string value encoding (IAdsConnection.DefaultValueEncoding if null)
    //
    // Returns:
    //     The string value.
    public string ReadAnyString(uint variableHandle, int len, Encoding? encoding)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (encoding == null)
        {
            encoding = DefaultValueEncoding;
        }

        StringMarshaler stringMarshaler = new StringMarshaler(encoding, StringConvertMode.FixedLengthZeroTerminated);
        int num = 0;
        bool flag = false;
        try
        {
            num = stringMarshaler.MarshalSize(encoding, len);
        }
        catch (NotSupportedException)
        {
            num = 4 * len;
            flag = true;
        }

        byte[] array = new byte[num];
        int readBytes = 0;
        TryRead(variableHandle, array.AsMemory(), out readBytes).ThrowOnError();
        stringMarshaler.Unmarshal(array.AsSpan(0, readBytes), encoding, out string value);
        if (flag && value.Length > len)
        {
            return value.Substring(0, len);
        }

        return value;
    }

    //
    // Summary:
    //     Reads a string asynchronously from the specified symbol/variable
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   len:
    //     The length.
    //
    //   encoding:
    //     The string value encoding (IAdsConnection.DefaultValueEncoding if null)
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous read operation. The TwinCAT.Ads.ResultAnyValue
    //     parameter contains the read string (TwinCAT.Ads.ResultValue`1.Value) and the
    //     TwinCAT.Ads.ResultAds.ErrorCode after execution.
    public async Task<ResultAnyValue> ReadAnyStringAsync(uint variableHandle, int len, Encoding? encoding, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (encoding == null)
        {
            encoding = DefaultValueEncoding;
        }

        StringMarshaler conv = new StringMarshaler(encoding, StringConvertMode.FixedLengthZeroTerminated);
        bool shorten = false;
        int num;
        try
        {
            num = conv.MarshalSize(encoding, len);
        }
        catch (NotSupportedException)
        {
            num = 4 * len;
            shorten = true;
        }

        byte[] data = new byte[num];
        ResultRead resultRead = await ReadAsync(variableHandle, data.AsMemory(), cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (resultRead.Succeeded)
        {
            conv.Unmarshal(data.AsSpan(0, resultRead.ReadBytes), encoding, out string value);
            if (shorten && value.Length > len)
            {
                value = value.Substring(0, len);
            }

            return new ResultAnyValue(resultRead.ErrorCode, value, resultRead.InvokeId);
        }

        return new ResultAnyValue(resultRead.ErrorCode, null, resultRead.InvokeId);
    }

    //
    // Summary:
    //     Writes the string (Potentially unsafe!)
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   value:
    //     The value.
    //
    //   length:
    //     The length.
    //
    //   encoding:
    //     The string value encoding (IAdsConnection.DefaultValueEncoding if null)
    //
    // Remarks:
    //     ATTENTION: Potentially this method is unsafe because following data can be overwritten
    //     after the string symbol. Please be sure to specify the string length lower than
    //     the string size reserved within the process image!
    [Obsolete("This method is potentially unsafe!")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void WriteAnyString(uint indexGroup, uint indexOffset, string value, int length, Encoding? encoding)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (value == null)
        {
            throw new ArgumentNullException("value");
        }

        if (value.Length > length)
        {
            value = value.Substring(0, length);
        }

        if (encoding == null)
        {
            encoding = DefaultValueEncoding;
        }

        StringMarshaler stringMarshaler = new StringMarshaler(encoding, StringConvertMode.ZeroTerminated);
        byte[] array = new byte[stringMarshaler.MarshalSize(value)];
        stringMarshaler.Marshal(value, array.AsSpan());
        TryWrite(indexGroup, indexOffset, array.AsMemory()).ThrowOnError();
    }

    //
    // Summary:
    //     Writes the string (Potentially unsafe!)
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   value:
    //     The value.
    //
    //   length:
    //     The length.
    //
    //   encoding:
    //     The string value encoding (IAdsConnection.DefaultValueEncoding if null)
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     Task<ResultWrite>.
    //
    // Remarks:
    //     ATTENTION: Potentially this method is unsafe because following data can be overwritten
    //     after the string symbol. Please be sure to specify the string length lower than
    //     the string size reserved within the process image!
    [Obsolete("This method is potentially unsafe!")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<ResultWrite> WriteAnyStringAsync(uint indexGroup, uint indexOffset, string value, int length, Encoding? encoding, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (value == null)
        {
            throw new ArgumentNullException("value");
        }

        if (value.Length > length)
        {
            value = value.Substring(0, length);
        }

        if (encoding == null)
        {
            encoding = DefaultValueEncoding;
        }

        StringMarshaler stringMarshaler = new StringMarshaler(encoding, StringConvertMode.ZeroTerminated);
        byte[] array = new byte[stringMarshaler.MarshalSize(value)];
        stringMarshaler.Marshal(value, array.AsSpan());
        return WriteAsync(indexGroup, indexOffset, array.AsMemory(), cancel);
    }

    //
    // Summary:
    //     Writes the string (Potentially unsafe!)
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   value:
    //     The value.
    //
    //   length:
    //     The length of the string to write
    //
    //   encoding:
    //     The string value encoding (IAdsConnection.DefaultValueEncoding if null)
    //
    // Remarks:
    //     ATTENTION: Potentially this method is unsafe because following data can be overwritten
    //     after the string symbol. Please be sure to specify the string length lower than
    //     the string size reserved within the process image! The String is written with
    //     the specified encoding.
    public void WriteAnyString(uint variableHandle, string value, int length, Encoding? encoding)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (value == null)
        {
            throw new ArgumentNullException("value");
        }

        if (value.Length > length)
        {
            value = value.Substring(0, length);
        }

        if (encoding == null)
        {
            encoding = DefaultValueEncoding;
        }

        StringMarshaler stringMarshaler = new StringMarshaler(encoding, StringConvertMode.FixedLengthZeroTerminated);
        int num = stringMarshaler.MarshalSize(value);
        byte[] array = new byte[num];
        stringMarshaler.Marshal(value, num, array.AsSpan());
        TryWrite(variableHandle, array.AsMemory()).ThrowOnError();
    }

    //
    // Summary:
    //     Writes the string (Potentially unsafe!)
    //
    // Parameters:
    //   symbolPath:
    //     The symbol path.
    //
    //   value:
    //     The value.
    //
    //   length:
    //     The length of the string to write
    //
    //   encoding:
    //     The string value encoding (IAdsConnection.DefaultValueEncoding if null)
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     ATTENTION: Potentially this method is unsafe because following data can be overwritten
    //     after the string symbol. Please be sure to specify the string length lower than
    //     the string size reserved within the process image! The String is written with
    //     the specified encoding.
    public void WriteAnyString(string symbolPath, string value, int length, Encoding? encoding)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (value == null)
        {
            throw new ArgumentNullException("value");
        }

        if (value.Length > length)
        {
            value = value.Substring(0, length);
        }

        if (encoding == null)
        {
            encoding = DefaultValueEncoding;
        }

        TryWriteValue(symbolPath, value).ThrowOnError();
    }

    //
    // Summary:
    //     Writes the string (Potentially unsafe!)
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   value:
    //     The value.
    //
    //   length:
    //     The length of the string to write
    //
    //   encoding:
    //     The string value encoding (IAdsConnection.DefaultValueEncoding if null)
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     Task<ResultWrite>.
    //
    // Remarks:
    //     ATTENTION: Potentially this method is unsafe because following data can be overwritten
    //     after the string symbol. Please be sure to specify the string length lower than
    //     the string size reserved within the process image! The String is written with
    //     the specified encoding.
    public Task<ResultWrite> WriteAnyStringAsync(uint variableHandle, string value, int length, Encoding? encoding, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (value == null)
        {
            throw new ArgumentNullException("value");
        }

        if (value.Length > length)
        {
            value = value.Substring(0, length);
        }

        if (encoding == null)
        {
            encoding = DefaultValueEncoding;
        }

        StringMarshaler stringMarshaler = new StringMarshaler(encoding, StringConvertMode.FixedLengthZeroTerminated);
        int num = stringMarshaler.MarshalSize(value);
        byte[] array = new byte[num];
        stringMarshaler.Marshal(value, num, array.AsSpan());
        return WriteAsync(variableHandle, array.AsMemory(), cancel);
    }

    //
    // Summary:
    //     Writes the string (Potentially unsafe!)
    //
    // Parameters:
    //   symbolPath:
    //     The symbol path.
    //
    //   value:
    //     The value.
    //
    //   length:
    //     The length of the string to write
    //
    //   encoding:
    //     The string value encoding (IAdsConnection.DefaultValueEncoding if null)
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     Task<ResultWrite>.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     ATTENTION: Potentially this method is unsafe because following data can be overwritten
    //     after the string symbol. Please be sure to specify the string length lower than
    //     the string size reserved within the process image! The String is written with
    //     the specified encoding.
    [Obsolete("This method is potentially unsafe! Please remove")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<ResultWrite> WriteAnyStringAsync(string symbolPath, string value, int length, Encoding? encoding, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (value == null)
        {
            throw new ArgumentNullException("value");
        }

        if (value.Length > length)
        {
            value = value.Substring(0, length);
        }

        if (encoding == null)
        {
            encoding = DefaultValueEncoding;
        }

        return WriteValueAsync(symbolPath, value, cancel);
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    //   type:
    //     Type of the object to be read.
    //
    // Returns:
    //     The read object.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    //   T:TwinCAT.TypeSystem.MarshalException:
    public object ReadAny(uint variableHandle, Type type)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        byte[] array = new byte[_anyTypeMarshaller.MarshalValueSize(type, DefaultValueEncoding)];
        int readBytes = 0;
        TryRead(61445u, variableHandle, array.AsMemory(), out readBytes).ThrowOnError();
        _anyTypeMarshaller.Unmarshal(type, null, array.AsSpan(), DefaultValueEncoding, out object value);
        if (value == null)
        {
            throw new MarshalException();
        }

        return value;
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    // Type parameters:
    //   T:
    //     The type of the value to read.
    //
    // Returns:
    //     The value of the read symbol.
    public T ReadAny<T>(uint variableHandle)
    {
        return (T)ReadAny(variableHandle, typeof(T));
    }

    //
    // Summary:
    //     Reads any as result.
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    // Type parameters:
    //   T:
    //
    // Returns:
    //     TwinCAT.Ads.ResultValue<T>.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResultValue<T> ReadAnyAsResult<T>(uint variableHandle)
    {
        return ReadAnyAsResult<T>(variableHandle, null);
    }

    //
    // Summary:
    //     Read any as an asynchronous operation.
    //
    // Parameters:
    //   variableHandle:
    //     The variable/symbol handle.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Type parameters:
    //   T:
    //     The Type of the value to be read.
    //
    // Returns:
    //     A Task<ResultValue`1> representing the asynchronous operation.
    //
    // Remarks:
    //     As object types only primitive types are supported.
    public async Task<ResultValue<T>> ReadAnyAsync<T>(uint variableHandle, CancellationToken cancel)
    {
        ResultAnyValue resultAnyValue = await ReadAnyAsync(variableHandle, typeof(T), cancel).ConfigureAwait(continueOnCapturedContext: false);
        return new ResultValue<T>(resultAnyValue.ErrorCode, resultAnyValue.Value);
    }

    //
    // Summary:
    //     Read any as an asynchronous operation.
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   args:
    //     Additional arguments.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Type parameters:
    //   T:
    //     Type of the object to be read
    //
    // Returns:
    //     A Task<ResultValue`1> representing the asynchronous operation.
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public async Task<ResultValue<T>> ReadAnyAsync<T>(uint variableHandle, int[]? args, CancellationToken cancel)
    {
        ResultAnyValue resultAnyValue = await ReadAnyAsync(variableHandle, typeof(T), args, cancel).ConfigureAwait(continueOnCapturedContext: false);
        return new ResultValue<T>(resultAnyValue.ErrorCode, resultAnyValue.Value);
    }

    //
    // Summary:
    //     Read any as an asynchronous operation.
    //
    // Parameters:
    //   indexGroup:
    //     Index group of the ADS variable.
    //
    //   indexOffset:
    //     Index offset of the ADS variable.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Type parameters:
    //   T:
    //
    // Returns:
    //     A Task<ResultValue`1> representing the asynchronous operation.
    public async Task<ResultValue<T>> ReadAnyAsync<T>(uint indexGroup, uint indexOffset, CancellationToken cancel)
    {
        ResultAnyValue resultAnyValue = await ReadAnyAsync(indexGroup, indexOffset, typeof(T), cancel).ConfigureAwait(continueOnCapturedContext: false);
        return new ResultValue<T>(resultAnyValue.ErrorCode, resultAnyValue.Value);
    }

    //
    // Summary:
    //     Read any as an asynchronous operation.
    //
    // Parameters:
    //   indexGroup:
    //     Index group of the ADS variable.
    //
    //   indexOffset:
    //     Index offset of the ADS variable.
    //
    //   args:
    //     Additional arguments.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Type parameters:
    //   T:
    //     The type of the result value.
    //
    // Returns:
    //     A Task<ResultValue`1> representing the asynchronous operation.
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public async Task<ResultValue<T>> ReadAnyAsync<T>(uint indexGroup, uint indexOffset, int[]? args, CancellationToken cancel)
    {
        ResultAnyValue resultAnyValue = await ReadAnyAsync(indexGroup, indexOffset, typeof(T), args, cancel).ConfigureAwait(continueOnCapturedContext: false);
        return new ResultValue<T>(resultAnyValue.ErrorCode, resultAnyValue.Value);
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   indexGroup:
    //     Index group of the ADS variable.
    //
    //   indexOffset:
    //     Index offset of the ADS variable.
    //
    //   args:
    //     Additional arguments.
    //
    // Type parameters:
    //   T:
    //     The type of the object to be read.
    //
    // Returns:
    //     The read value.
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResultValue<T> ReadAnyAsResult<T>(uint indexGroup, uint indexOffset, int[]? args)
    {
        ResultAnyValue resultAnyValue = ReadAnyAsResult(indexGroup, indexOffset, typeof(T), args);
        return new ResultValue<T>(resultAnyValue.ErrorCode, resultAnyValue.Value);
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    //   type:
    //     Type of the object to be read.
    //
    //   args:
    //     Additional arguments.
    //
    // Returns:
    //     The read value.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    //   T:TwinCAT.TypeSystem.MarshalException:
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public object ReadAny(uint variableHandle, Type type, int[]? args)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        byte[] array = new byte[_anyTypeMarshaller.MarshalSize(type, args, DefaultValueEncoding)];
        int readBytes = 0;
        TryRead(61445u, variableHandle, array.AsMemory(), out readBytes).ThrowOnError();
        object value = null;
        _anyTypeMarshaller.Unmarshal(type, args, array.AsSpan(), DefaultValueEncoding, out value);
        if (value == null)
        {
            throw new MarshalException();
        }

        return value;
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   indexGroup:
    //     Index group of the ADS variable.
    //
    //   indexOffset:
    //     Index offset of the ADS variable.
    //
    //   args:
    //     Additional arguments.
    //
    // Type parameters:
    //   T:
    //     The type of the object to be read.
    //
    // Returns:
    //     The read value.
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public T ReadAny<T>(uint indexGroup, uint indexOffset, int[]? args)
    {
        return (T)ReadAny(indexGroup, indexOffset, typeof(T), args);
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    //   args:
    //     Additional arguments.
    //
    // Type parameters:
    //   T:
    //     The type of the value to read.
    //
    // Returns:
    //     The value of the read symbol.
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public T ReadAny<T>(uint variableHandle, int[]? args)
    {
        return (T)ReadAny(variableHandle, typeof(T), args);
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an result object.
    //
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    //   args:
    //     Additional arguments.
    //
    // Type parameters:
    //   T:
    //     The type of the value to read.
    //
    // Returns:
    //     The result value object.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    //   T:TwinCAT.TypeSystem.MarshalException:
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResultValue<T> ReadAnyAsResult<T>(uint variableHandle, int[]? args)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        byte[] array = new byte[_anyTypeMarshaller.MarshalSize(typeof(T), args, DefaultValueEncoding)];
        int readBytes = 0;
        AdsErrorCode errorCode = TryRead(61445u, variableHandle, array.AsMemory(), out readBytes);
        object value = null;
        if (errorCode.Succeeded())
        {
            _anyTypeMarshaller.Unmarshal(typeof(T), args, array.AsSpan(), DefaultValueEncoding, out value);
            if (value == null)
            {
                throw new MarshalException();
            }
        }

        return new ResultValue<T>(errorCode, value);
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   indexGroup:
    //     Index group of the ADS variable.
    //
    //   indexOffset:
    //     Index offset of the ADS variable.
    //
    // Type parameters:
    //   T:
    //     The type of the object to be read.
    //
    // Returns:
    //     The read value.
    public T ReadAny<T>(uint indexGroup, uint indexOffset)
    {
        return (T)ReadAny(indexGroup, indexOffset, typeof(T), null);
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an result object.
    //
    //
    // Parameters:
    //   indexGroup:
    //     Index group of the ADS variable.
    //
    //   indexOffset:
    //     Index offset of the ADS variable.
    //
    // Type parameters:
    //   T:
    //     The type of the object to be read.
    //
    // Returns:
    //     The result object.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResultValue<T> ReadAnyAsResult<T>(uint indexGroup, uint indexOffset)
    {
        return ReadAnyAsResult<T>(indexGroup, indexOffset, null);
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   indexGroup:
    //     Index group of the ADS variable.
    //
    //   indexOffset:
    //     Index offset of the ADS variable.
    //
    //   type:
    //     Type of the object to be read.
    //
    // Returns:
    //     The read value.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    //   T:TwinCAT.TypeSystem.MarshalException:
    public object ReadAny(uint indexGroup, uint indexOffset, Type type)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        byte[] array = new byte[_anyTypeMarshaller.MarshalValueSize(type, DefaultValueEncoding)];
        int readBytes = 0;
        TryRead(indexGroup, indexOffset, array.AsMemory(), out readBytes).ThrowOnError();
        object value = null;
        _anyTypeMarshaller.Unmarshal(type, null, array.AsSpan(), DefaultValueEncoding, out value);
        if (value == null)
        {
            throw new MarshalException();
        }

        return value;
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   indexGroup:
    //     Index group of the ADS variable.
    //
    //   indexOffset:
    //     Index offset of the ADS variable.
    //
    //   type:
    //     Type of the object to be read.
    //
    //   args:
    //     Additional arguments.
    //
    // Returns:
    //     The read value.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public object ReadAny(uint indexGroup, uint indexOffset, Type type, int[]? args)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (args == null)
        {
            return ReadAny(indexGroup, indexOffset, type);
        }

        byte[] array = new byte[_anyTypeMarshaller.MarshalSize(type, args, DefaultValueEncoding)];
        int readBytes = 0;
        TryRead(indexGroup, indexOffset, array.AsMemory(), out readBytes).ThrowOnError();
        _anyTypeMarshaller.Unmarshal(type, args, array.AsSpan(), DefaultValueEncoding, out object value);
        return value;
    }

    //
    // Summary:
    //     Writes an object synchronously to an ADS device.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    //   value:
    //     Object to write to the ADS device.
    public void WriteAny(uint variableHandle, object value)
    {
        WriteAny(variableHandle, value, null);
    }

    //
    // Summary:
    //     Writes an object synchronously to an ADS device.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    //   value:
    //     Object to write to the ADS device.
    //
    // Returns:
    //     ResultWrite.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResultWrite WriteAnyAsResult(uint variableHandle, object value)
    {
        return WriteAnyAsResult(variableHandle, value, null);
    }

    //
    // Summary:
    //     Writes an object synchronously to an ADS device.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   value:
    //     Object to write to the ADS device.
    public void WriteAny(uint indexGroup, uint indexOffset, object value)
    {
        WriteAny(indexGroup, indexOffset, value, null);
    }

    //
    // Summary:
    //     Writes an object synchronously to an ADS device.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   value:
    //     Object to write to the ADS device.
    //
    // Returns:
    //     ResultWrite.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResultWrite WriteAnyAsResult(uint indexGroup, uint indexOffset, object value)
    {
        return WriteAnyAsResult(indexGroup, indexOffset, value, null);
    }

    //
    // Summary:
    //     Writes an object synchronously to an ADS device. If the Type of the object to
    //     be written is a string type, the first element of parameter args specifies the
    //     number of characters of the string.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    //   value:
    //     Object to write to the ADS device.
    //
    //   args:
    //     Additional arguments.
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public void WriteAny(uint variableHandle, object value, int[]? args)
    {
        WriteAnyAsResult(variableHandle, value, args).ErrorCode.ThrowOnError();
    }

    //
    // Summary:
    //     Writes an object synchronously to an ADS device. If the Type of the object to
    //     be written is a string type, the first element of parameter args specifies the
    //     number of characters of the string.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    //   value:
    //     Object to write to the ADS device.
    //
    //   args:
    //     Additional arguments.
    //
    // Returns:
    //     ResultWrite.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResultWrite WriteAnyAsResult(uint variableHandle, object value, int[]? args)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        byte[] array = new byte[_anyTypeMarshaller.MarshalValueSize(value, args, DefaultValueEncoding)];
        _anyTypeMarshaller.Marshal(value, args, DefaultValueEncoding, array.AsSpan());
        return new ResultWrite(TryWrite(variableHandle, array.AsMemory()));
    }

    //
    // Summary:
    //     Writes an object asynchronously to an ADS device. If the Type of the object to
    //     be written is a string type, the first element of parameter args specifies the
    //     number of characters of the string.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    //   value:
    //     Object to write to the ADS device.
    //
    //   args:
    //     Additional arguments.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous task operation. The result parameter
    //     TwinCAT.Ads.ResultWrite of the write operation contains the TwinCAT.Ads.ResultAds.ErrorCode.
    //
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public Task<ResultWrite> WriteAnyAsync(uint variableHandle, object value, int[]? args, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        byte[] array = new byte[_anyTypeMarshaller.MarshalValueSize(value, args, DefaultValueEncoding)];
        _anyTypeMarshaller.Marshal(value, args, DefaultValueEncoding, array.AsSpan());
        return WriteAsync(variableHandle, array.AsMemory(), cancel);
    }

    //
    // Summary:
    //     Writes an object synchronously to an ADS device. If the Type of the object to
    //     be written is a string type, the first element of parameter args specifies the
    //     number of characters of the string.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable.
    //
    //   value:
    //     Object to write to the ADS device.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous task operation. The result parameter
    //     TwinCAT.Ads.ResultWrite of the write operation contains the TwinCAT.Ads.ResultAds.ErrorCode.
    public Task<ResultWrite> WriteAnyAsync(uint variableHandle, object value, CancellationToken cancel)
    {
        return WriteAnyAsync(variableHandle, value, null, cancel);
    }

    //
    // Summary:
    //     Writes an object synchronously to an ADS device.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   value:
    //     Object to write to the ADS device.
    //
    //   args:
    //     Additional arguments.
    //
    // Remarks:
    //     If the Type of the object to be written is a string type, the first element of
    //     parameter args specifies the number of characters of the string.
    public void WriteAny(uint indexGroup, uint indexOffset, object value, int[]? args)
    {
        WriteAnyAsResult(indexGroup, indexOffset, value, args).ErrorCode.ThrowOnError();
    }

    //
    // Summary:
    //     Writes an object synchronously to an ADS device.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   value:
    //     Object to write to the ADS device.
    //
    //   args:
    //     Additional arguments.
    //
    // Returns:
    //     ResultWrite.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     If the Type of the object to be written is a string type, the first element of
    //     parameter args specifies the number of characters of the string.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResultWrite WriteAnyAsResult(uint indexGroup, uint indexOffset, object value, int[]? args)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        byte[] array = new byte[_anyTypeMarshaller.MarshalValueSize(value, args, DefaultValueEncoding)];
        _anyTypeMarshaller.Marshal(value, args, DefaultValueEncoding, array.AsSpan());
        return new ResultWrite(TryWrite(indexGroup, indexOffset, array.AsMemory()));
    }

    //
    // Summary:
    //     Determines the Symbol handle by its instance path synchronously.
    //
    // Parameters:
    //   symbolPath:
    //     SymbolName / InstancePath.
    //
    // Returns:
    //     The symbols/variable handle
    //
    // Remarks:
    //     It is a good practice to release all variable handles after use to regain internal
    //     resources in the TwinCAT subsystem. The composite method to this TwinCAT.Ads.AdsClient.CreateVariableHandle(System.String)
    //     is the TwinCAT.Ads.AdsClient.DeleteVariableHandle(System.UInt32)
    public uint CreateVariableHandle(string symbolPath)
    {
        uint variableHandle = 0u;
        TryCreateVariableHandle(symbolPath, out variableHandle).ThrowOnError();
        return variableHandle;
    }

    //
    // Summary:
    //     Releases the specified symbol/variable handle synchronously.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable
    //
    // Returns:
    //     The ADS error code.
    //
    // Remarks:
    //     It is a good practice to release all variable handles after use to regain internal
    //     resources in the TwinCAT subsystem. The composite method to this TwinCAT.Ads.AdsClient.TryDeleteVariableHandle(System.UInt32)
    //     is the TwinCAT.Ads.AdsClient.TryCreateVariableHandle(System.String,System.UInt32@)
    public void DeleteVariableHandle(uint variableHandle)
    {
        TryDeleteVariableHandle(variableHandle).ThrowOnError();
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes to the specified readBuffer.
    //
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable
    //
    //   readBuffer:
    //     The read buffer / data
    //
    // Returns:
    //     Number of successfully returned data bytes.
    public int Read(uint variableHandle, Memory<byte> readBuffer)
    {
        int readBytes = 0;
        TryRead(variableHandle, readBuffer, out readBytes).ThrowOnError();
        return readBytes;
    }

    //
    // Summary:
    //     Reads the value synchronously data of the symbol, that is represented by the
    //     variable handle into the readBuffer.
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   readBuffer:
    //     The read buffer/data
    //
    //   readBytes:
    //     Number of read bytes.
    //
    // Returns:
    //     The ADS error code.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public AdsErrorCode TryRead(uint variableHandle, Memory<byte> readBuffer, out int readBytes)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        AdsErrorCode adsErrorCode = TryRead(61445u, variableHandle, readBuffer, out readBytes);
        if (_handleCache != null && adsErrorCode == AdsErrorCode.DeviceSymbolVersionInvalid)
        {
            _handleCache.Remove(variableHandle);
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Writes data synchronously to an ADS device and then Reads data from that target.
    //
    //
    // Parameters:
    //   variableHandle:
    //     Variable handle.
    //
    //   readBuffer:
    //     The read buffer.
    //
    //   writeBuffer:
    //     The write buffer.
    //
    // Returns:
    //     Number of successfully returned data bytes.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public int ReadWrite(uint variableHandle, Memory<byte> readBuffer, ReadOnlyMemory<byte> writeBuffer)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        int readBytes = 0;
        TryReadWrite(variableHandle, readBuffer, writeBuffer, out readBytes).ThrowOnError();
        return readBytes;
    }

    //
    // Summary:
    //     Determines the Symbol handle by its instance path asynchronously.
    //
    // Parameters:
    //   symbolPath:
    //     SymbolName / InstancePath.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'CreateVariableHandle' operation. The
    //     TwinCAT.Ads.ResultHandle parameter contains the variable handle (TwinCAT.Ads.ResultHandle.Handle)
    //     and the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    //
    // Remarks:
    //     It is a good practice to release all variable handles after use to regain internal
    //     resources in the TwinCAT subsystem. The composite method to this TwinCAT.Ads.AdsClient.CreateVariableHandleAsync(System.String,System.Threading.CancellationToken)
    //     is the TwinCAT.Ads.AdsClient.DeleteVariableHandleAsync(System.UInt32,System.Threading.CancellationToken)
    public async Task<ResultHandle> CreateVariableHandleAsync(string symbolPath, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        try
        {
            ResultValue<SymbolUploadInfo> resultValue = await tryReadEncodingsAsync(cancel).ConfigureAwait(continueOnCapturedContext: false);
            if (resultValue.Succeeded)
            {
                return await _handleCache.CreateVariableHandleAsync(symbolPath, cancel).ConfigureAwait(continueOnCapturedContext: false);
            }

            return ResultHandle.CreateError(resultValue.ErrorCode);
        }
        catch (Exception exception)
        {
            if (CanLog(LogLevel.Error))
            {
                Logger?.LogError(exception, "Cannot create variable handle for Symbol '{Symbol}'!", symbolPath);
            }

            throw;
        }
    }

    //
    // Summary:
    //     Releases the specified symbol/variable handle asynchronously.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'DeleteVariableHandle' operation. The
    //     TwinCAT.Ads.ResultAds parameter contains the TwinCAT.Ads.ResultAds.ErrorCode
    //     after execution.
    //
    // Remarks:
    //     It is a good practice to release all variable handles after use to regain internal
    //     resources in the TwinCAT subsystem. The composite method to this TwinCAT.Ads.AdsClient.DeleteVariableHandleAsync(System.UInt32,System.Threading.CancellationToken)
    //     is the TwinCAT.Ads.AdsClient.CreateVariableHandleAsync(System.String,System.Threading.CancellationToken)
    public Task<ResultAds> DeleteVariableHandleAsync(uint variableHandle, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        return _handleCache.DeleteVariableHandleAsync(variableHandle, cancel);
    }

    //
    // Summary:
    //     Releases the specified symbol/variable handle synchronously.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable
    //
    // Returns:
    //     The ADS error code.
    //
    // Remarks:
    //     It is a good practice to release all variable handles after use to regain internal
    //     resources in the TwinCAT subsystem. The composite method to this TwinCAT.Ads.AdsClient.TryDeleteVariableHandle(System.UInt32)
    //     is the TwinCAT.Ads.AdsClient.TryCreateVariableHandle(System.String,System.UInt32@)
    public AdsErrorCode TryDeleteVariableHandle(uint variableHandle)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        return _handleCache.TryDeleteVariableHandle(variableHandle, _timeout);
    }

    //
    // Summary:
    //     ReadWrites value data synchronously to/from the symbol represented by the variableHandle.
    //
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   readBuffer:
    //     The read buffer / read data.
    //
    //   writeBuffer:
    //     The write buffer / write data.
    //
    //   readBytes:
    //     Number of read bytes.
    //
    // Returns:
    //     The ADS error code.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public AdsErrorCode TryReadWrite(uint variableHandle, Memory<byte> readBuffer, ReadOnlyMemory<byte> writeBuffer, out int readBytes)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        AdsErrorCode adsErrorCode = TryReadWrite(61445u, variableHandle, readBuffer, writeBuffer, out readBytes);
        if (_handleCache != null && adsErrorCode == AdsErrorCode.DeviceSymbolVersionInvalid)
        {
            _handleCache.Remove(variableHandle);
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Writes data synchronously to an ADS device.
    //
    // Parameters:
    //   variableHandle:
    //     Handle of the ADS variable
    //
    //   writeBuffer:
    //     The write buffer / value to be written
    public void Write(uint variableHandle, ReadOnlyMemory<byte> writeBuffer)
    {
        TryWrite(variableHandle, writeBuffer).ThrowOnError();
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to the given readBuffer
    //
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   readBuffer:
    //     Memory location, where to read the data.
    //
    // Returns:
    //     Number of successfully returned (read) data bytes.
    public int Read(uint indexGroup, uint indexOffset, Memory<byte> readBuffer)
    {
        int readBytes = 0;
        TryRead(indexGroup, indexOffset, readBuffer, out readBytes).ThrowOnError();
        return readBytes;
    }

    //
    // Summary:
    //     Triggers a 'Write' call to the ADS device at the specified address.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    public void Write(uint indexGroup, uint indexOffset)
    {
        TryWrite(indexGroup, indexOffset, Memory<byte>.Empty).ThrowOnError();
    }

    //
    // Summary:
    //     Writes data synchronously to an ADS device.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   writeBuffer:
    //     The data to write.
    public void Write(uint indexGroup, uint indexOffset, ReadOnlyMemory<byte> writeBuffer)
    {
        TryWrite(indexGroup, indexOffset, writeBuffer).ThrowOnError();
    }

    //
    // Summary:
    //     Writes data synchronously to an ADS device.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   writeBuffer:
    //     The data buffer to be written.
    //
    // Returns:
    //     The ADS error code.
    public AdsErrorCode TryWrite(uint indexGroup, uint indexOffset, ReadOnlyMemory<byte> writeBuffer)
    {
        return WriteSync(indexGroup, indexOffset, writeBuffer).ErrorCode;
    }

    //
    // Summary:
    //     Reads the ADS status and the device status from an ADS server.
    //
    // Returns:
    //     The ADS statue and device status or an Exception with ErrorCode: TwinCAT.Ads.AdsErrorCode.DeviceServiceNotSupported.
    //
    //
    // Remarks:
    //     Not all ADS Servers support the State ADS Request.
    public StateInfo ReadState()
    {
        TryReadState(out var stateInfo).ThrowOnError();
        return stateInfo;
    }

    //
    // Summary:
    //     Reads the ADS status and the device status from an ADS server. Unlike the ReadState
    //     method this method does not call an exception on failure. Instead an AdsErrorCode
    //     is returned. If the return value is equal to AdsErrorCode.NoError the call was
    //     successful.
    //
    // Parameters:
    //   stateInfo:
    //     The ADS statue and device status.
    //
    // Returns:
    //     TwinCAT.Ads.AdsErrorCode of the ADS read state call. Check for TwinCAT.Ads.AdsErrorCode.NoError
    //     to see if call was successful.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     Not all ADS Servers support the State ADS Request
    public AdsErrorCode TryReadState(out StateInfo stateInfo)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ResultReadDeviceState resultReadDeviceState = ReadStateSync();
        stateInfo = resultReadDeviceState.State;
        return resultReadDeviceState.ErrorCode;
    }

    //
    // Summary:
    //     Reads the ADS status and the device status from an ADS server.
    //
    // Parameters:
    //   cancel:
    //     The cancellation token
    //
    // Returns:
    //     A task that represents the asynchronous 'ReadState' operation. The TwinCAT.Ads.ResultReadDeviceState
    //     parameter contains the state (TwinCAT.Ads.ResultReadDeviceState.State) as long
    //     as the TwinCAT.Ads.ResultAds.ErrorCode of the ADS communication after execution.
    //
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    // Remarks:
    //     Not all ADS Servers support the State ADS Request
    public Task<ResultReadDeviceState> ReadStateAsync(CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            return Task.FromResult(new ResultReadDeviceState(AdsErrorCode.ClientPortNotOpen, default(StateInfo), 0u));
        }

        Func<CancellationToken, Task<ResultReadDeviceState>> func = delegate (CancellationToken c)
        {
            Func<uint, Task<AdsErrorCode>> readStateRequest = (uint id) => _server.ReadDeviceStateRequestAsync(_target, id, c);
            return _server.RequestReadDeviceStateAsync(readStateRequest, null, _timeout, c);
        };
        if (cancel.IsCancellationRequested)
        {
            return Task.FromResult(ResultReadDeviceState.CreateError(AdsErrorCode.ClientRequestCancelled, 0u));
        }

        if (_interceptors != null)
        {
            return _interceptors.CommunicateReadStateAsync(func, cancel);
        }

        return func(cancel);
    }

    //
    // Summary:
    //     Reads the ADS status and the device status from an ADS server (synchronous)
    //
    // Returns:
    //     A task that represents the asynchronous 'ReadState' operation. The TwinCAT.Ads.ResultReadDeviceState
    //     parameter contains the state (TwinCAT.Ads.ResultReadDeviceState.State) as long
    //     as the TwinCAT.Ads.ResultAds.ErrorCode of the ADS communication after execution.
    private ResultReadDeviceState ReadStateSync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            return new ResultReadDeviceState(AdsErrorCode.ClientPortNotOpen, default(StateInfo), 0u);
        }

        Func<ResultReadDeviceState> func = delegate
        {
            Func<uint, AdsErrorCode> readStateRequest = (uint id) => _server.ReadDeviceStateRequestSync(_target, id);
            return _server.RequestReadDeviceState(readStateRequest, null, _timeout);
        };
        if (_interceptors != null)
        {
            return _interceptors.CommunicateReadState(func);
        }

        return func();
    }

    //
    // Summary:
    //     Changes the ADS status and device status of the ADS server asynchronously.
    //
    // Parameters:
    //   adsState:
    //     The ADS state.
    //
    //   deviceState:
    //     The device state.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'WriteControl' operation. The TwinCAT.Ads.ResultAds
    //     parameter contains the state the TwinCAT.Ads.ResultAds.ErrorCode of the ADS communication
    //     after execution.
    public Task<ResultAds> WriteControlAsync(AdsState adsState, ushort deviceState, CancellationToken cancel)
    {
        byte[] array = new byte[1];
        return WriteControlAsync(adsState, deviceState, array.AsMemory(), cancel);
    }

    //
    // Summary:
    //     Changes the ADS status and the device status of an ADS server.
    //
    // Parameters:
    //   stateInfo:
    //     New ADS status and device status.
    public void WriteControl(StateInfo stateInfo)
    {
        byte[] array = new byte[1];
        TryWriteControl(stateInfo, array).ThrowOnError();
    }

    //
    // Summary:
    //     Changes the ADS status and the device status of an ADS server.
    //
    // Parameters:
    //   stateInfo:
    //     New ADS status and device status.
    //
    //   writeBuffer:
    //     The write buffer.
    public void WriteControl(StateInfo stateInfo, ReadOnlyMemory<byte> writeBuffer)
    {
        TryWriteControl(stateInfo, writeBuffer).ThrowOnError();
    }

    //
    // Summary:
    //     Changes the ADS status and the device status of an ADS server.
    //
    // Parameters:
    //   stateInfo:
    //     New ADS status and device status.
    //
    // Returns:
    //     AdsErrorCode.
    public AdsErrorCode TryWriteControl(StateInfo stateInfo)
    {
        return TryWriteControl(stateInfo, new byte[1].AsMemory());
    }

    //
    // Summary:
    //     Determines the Symbol handle by its instance path synchronously.
    //
    // Parameters:
    //   symbolPath:
    //     SymbolName / InstancePath.
    //
    //   variableHandle:
    //     The symbols handle.
    //
    // Returns:
    //     The ADS error code.
    //
    // Remarks:
    //     It is a good practice to release all variable handles after use to regain internal
    //     resources in the TwinCAT subsystem. The composite method to this TwinCAT.Ads.AdsClient.TryCreateVariableHandle(System.String,System.UInt32@)
    //     is the TwinCAT.Ads.AdsClient.TryDeleteVariableHandle(System.UInt32)
    public AdsErrorCode TryCreateVariableHandle(string symbolPath, out uint variableHandle)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        AdsErrorCode adsErrorCode = AdsErrorCode.None;
        variableHandle = 0u;
        try
        {
            ResultValue<SymbolUploadInfo> resultValue = tryReadEncodings();
            adsErrorCode = resultValue.ErrorCode;
            variableHandle = 0u;
            if (resultValue.Succeeded)
            {
                adsErrorCode = _handleCache.TryCreateVariableHandle(symbolPath, _timeout, out variableHandle);
            }
        }
        catch (Exception exception)
        {
            if (CanLog(LogLevel.Error))
            {
                Logger?.LogError(exception, "Cannot create variable handle for Symbol '{Symbol}'!", symbolPath);
            }

            throw;
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Called when before the TwinCAT.Ads.AdsClient is disconnected.
    private void OnBeforeDisconnect()
    {
        if (_interceptors != null)
        {
            _interceptors.BeforeDisconnect(() => AdsErrorCode.NoError);
        }
    }

    //
    // Summary:
    //     Sets additional Communication Interceptors..
    //
    // Parameters:
    //   interceptors:
    //     The interceptors.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    public void SetCommunicationInterceptor(CommunicationInterceptors interceptors)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        _interceptors = interceptors;
    }

    //
    // Summary:
    //     Injects an TwinCAT.Ads.AdsErrorCode to the TwinCAT.Ads.Internal.IInterceptedClient.
    //
    //
    // Parameters:
    //   error:
    //     The error.
    //
    // Returns:
    //     The accepted TwinCAT.Ads.AdsErrorCode.
    AdsErrorCode IAdsInjectAcceptor.InjectError(AdsErrorCode error)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        AdsErrorCode result = AdsErrorCode.NoError;
        if (_interceptors != null)
        {
            result = _interceptors.Communicate(resurrect: false, (bool resurrect) => ResultAds.CreateError(error));
        }

        return result;
    }

    //
    // Summary:
    //     Resurrects the connection
    //
    // Parameters:
    //   error:
    //     The error if the resurrection failed
    //
    // Returns:
    //     true if resurrection was accepted, false otherwise.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    public bool TryResurrect([System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out AdsException? error)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        AdsErrorCode errorCode = ((IAdsResurrectHandles)_handleCache).Resurrect();
        if (errorCode.Succeeded())
        {
            errorCode = ((IAdsResurrectHandles)_notificationReceiver).Resurrect();
            if (errorCode.Succeeded())
            {
                IFailFastHandler failFastHandler = null;
                if (_interceptors != null)
                {
                    failFastHandler = (IFailFastHandler)_interceptors.CombinedInterceptors.FirstOrDefault((ICommunicationInterceptor item) => item is IFailFastHandler);
                }

                if (failFastHandler != null)
                {
                    failFastHandler.Reset();
                    error = null;
                }

                if (_interceptors != null)
                {
                    _interceptors.Communicate(resurrect: true, (bool resurrect) => ResultAds.CreateSuccess());
                }

                error = null;
                return true;
            }
        }

        error = new AdsErrorException("Cannot resurrect", errorCode);
        return false;
    }

    //
    // Summary:
    //     Gets the access method for the specified symbol.
    //
    // Parameters:
    //   symbol:
    //     The symbol.
    private AccessMethods getAccessMethod(ISymbol symbol)
    {
        AccessMethods result = AccessMethods.Mask_All;
        if (!(symbol is IAdsSymbol adsSymbol))
        {
            result = AccessMethods.Mask_Symbolic;
        }
        else if (adsSymbol.IndexGroup == 61462 || adsSymbol.IndexGroup == 61467)
        {
            result = AccessMethods.Mask_Symbolic;
        }
        else if (adsSymbol.IndexGroup == 61460 || adsSymbol.IndexGroup == 61466)
        {
            result = AccessMethods.Mask_Symbolic;
        }
        else if (adsSymbol.IndexGroup == 61465)
        {
            result = AccessMethods.Mask_Symbolic;
        }

        return result;
    }

    //
    // Summary:
    //     Read value as an asynchronous operation.
    //
    // Parameters:
    //   symbol:
    //     The symbol that should be read.
    //
    //   cancel:
    //     The cancel token.
    //
    // Returns:
    //     A Task<ResultAnyValue> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    //   T:System.ArgumentNullException:
    //     symbol
    //
    //   T:TwinCAT.TypeSystem.CannotResolveDataTypeException:
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public async Task<ResultAnyValue> ReadValueAsync(ISymbol symbol2, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (symbol2 == null)
        {
            throw new ArgumentNullException("symbol2");
        }

        ISymbol unwrappedSymbol = symbol2.Unwrap();
        if (unwrappedSymbol is IProcessImageAddress { IsVirtual: not false })
        {
            return new ResultAnyValue(AdsErrorCode.DeviceInvalidAccess, null, 0u);
        }

        IDataType dataType = unwrappedSymbol.DataType;
        if (dataType == null)
        {
            throw new CannotResolveDataTypeException(unwrappedSymbol);
        }

        getAccessMethod(unwrappedSymbol);
        bool num = (getAccessMethod(unwrappedSymbol) & AccessMethods.ValueByName) != 0;
        PrimitiveTypeMarshaler converter = PrimitiveTypeMarshaler.CreateFrom(dataType);
        if (!PrimitiveTypeMarshaler.TryGetManagedType(dataType, out Type managedType))
        {
            managedType = typeof(byte[]);
        }

        ResultAnyValue result;
        if (num)
        {
            result = await ReadValueAsync(unwrappedSymbol.InstancePath, managedType, cancel).ConfigureAwait(continueOnCapturedContext: false);
        }
        else
        {
            IAdsSymbol adsSymbol = (IAdsSymbol)unwrappedSymbol;
            byte[] buffer = new byte[unwrappedSymbol.ByteSize];
            ResultRead resultRead = await ReadAsync(adsSymbol.IndexGroup, adsSymbol.IndexOffset, buffer.AsMemory(), cancel).ConfigureAwait(continueOnCapturedContext: false);
            object val = null;
            if (unwrappedSymbol.Category == DataTypeCategory.Array)
            {
                converter.Unmarshal(managedType, buffer, DefaultValueEncoding, out val);
            }
            else
            {
                converter.Unmarshal(managedType, buffer, DefaultValueEncoding, out val);
            }

            result = new ResultAnyValue(resultRead.ErrorCode, val, resultRead.InvokeId);
        }

        return result;
    }

    //
    // Summary:
    //     Reads the value of a symbol and returns the value as (boxed) object.
    //
    // Parameters:
    //   symbol:
    //     The symbol that should be read.
    //
    // Returns:
    //     The value of the symbol as an object.
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public object ReadValue(ISymbol symbol)
    {
        TryReadValue(symbol, out object value).ThrowOnError();
        return value;
    }

    //
    // Summary:
    //     Tries to read the value of a symbol and returns the value as boxed object.
    //
    // Parameters:
    //   symbol:
    //     The symbol that should be read.
    //
    //   value:
    //     The value.
    //
    // Returns:
    //     The ADS Error Code
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    //   T:System.ArgumentNullException:
    //     symbol
    //
    //   T:TwinCAT.TypeSystem.CannotResolveDataTypeException:
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public AdsErrorCode TryReadValue(ISymbol symbol, out object? value)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (symbol == null)
        {
            throw new ArgumentNullException("symbol");
        }

        ISymbol symbol2 = symbol.Unwrap();
        if (symbol2 is IProcessImageAddress { IsVirtual: not false })
        {
            value = null;
            return AdsErrorCode.DeviceInvalidAccess;
        }

        IDataType dataType = symbol2.DataType;
        if (dataType == null)
        {
            throw new CannotResolveDataTypeException(symbol2);
        }

        getAccessMethod(symbol2);
        bool num = (getAccessMethod(symbol2) & AccessMethods.ValueByName) != 0;
        AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
        if (!PrimitiveTypeMarshaler.TryGetManagedType(dataType, out Type managed))
        {
            managed = typeof(byte[]);
        }

        PrimitiveTypeMarshaler primitiveTypeMarshaler = PrimitiveTypeMarshaler.CreateFrom(dataType);
        if (num)
        {
            adsErrorCode = TryReadValue(symbol2.InstancePath, managed, out value);
        }
        else
        {
            IAdsSymbol adsSymbol = (IAdsSymbol)symbol2;
            int readBytes = 0;
            byte[] array = new byte[symbol2.ByteSize];
            adsErrorCode = TryRead(adsSymbol.IndexGroup, adsSymbol.IndexOffset, array.AsMemory(), out readBytes);
            primitiveTypeMarshaler.Unmarshal(managed, array, DefaultValueEncoding, out value);
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Clears the internal symbol cache.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     Previously stored symbol information is cleared. As a consequence the symbol
    //     information must be obtained from the ADS server again if accessed, which which
    //     needs an extra ADS round trip.
    public void CleanupSymbolTable()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (_symbolCache != null)
        {
            _symbolCache.CleanupCache();
        }
    }

    //
    // Summary:
    //     Reads the value of a symbol specified with its instance path and returns the
    //     value as boxed object.
    //
    // Parameters:
    //   instancePath:
    //     Symbol Path of the ADS symbol.
    //
    // Returns:
    //     Value of the symbol
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public object ReadValue(string instancePath)
    {
        return ReadValue(instancePath, null);
    }

    //
    // Summary:
    //     Reads the value of a symbol specified with its instance path and returns the
    //     value as object of the specified type.
    //
    // Parameters:
    //   instancePath:
    //     Symbol/Instance Path of the ADS symbol.
    //
    //   type:
    //     Managed type (.NET Type) of the ADS symbol value that will be read, or NULL to
    //     use automatic marshalling for primitive types.
    //
    // Returns:
    //     Value of the symbol
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public object ReadValue(string instancePath, Type? type)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        object value = null;
        TryReadValue(instancePath, type, out value).ThrowOnError();
        return value;
    }

    //
    // Summary:
    //     Tries to the value of a symbol specified as instance path and returns the value
    //     as (boxed) object.
    //
    // Parameters:
    //   name:
    //     The name.
    //
    //   type:
    //     Managed type of the ADS symbol.
    //
    //   value:
    //     The value of the Symbol.
    //
    // Returns:
    //     The TwinCAT.Ads.AdsErrorCode.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public AdsErrorCode TryReadValue(string name, Type? type, out object? value)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ISymbolCache table = null;
        AdsErrorCode adsErrorCode = ((IAdsSymbolCacheProvider)this).TryGetSymbolCache(out table);
        if (adsErrorCode.Succeeded())
        {
            adsErrorCode = table.TryReadValue(name, type, out value);
        }
        else
        {
            value = null;
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Read value as an asynchronous operation.
    //
    // Parameters:
    //   instancePath:
    //     Name of the ADS symbol.
    //
    //   type:
    //     Managed type of the ADS symbol
    //
    //   cancel:
    //     The cancel token.
    //
    // Returns:
    //     A Task<ResultAnyValue> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public async Task<ResultAnyValue> ReadValueAsync(string instancePath, Type type, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ResultValue<ISymbolCache> resultValue = await ((IAdsSymbolCacheProvider)this).GetSymbolCacheAsync(cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (resultValue.Succeeded)
        {
            return await resultValue.Value.ReadValueAsync(instancePath, type, cancel).ConfigureAwait(continueOnCapturedContext: false);
        }

        return new ResultAnyValue(resultValue.ErrorCode, null, resultValue.InvokeId);
    }

    //
    // Summary:
    //     Gets the symbol table.
    //
    // Returns:
    //     SymbolInfoTable.
    AdsErrorCode IAdsSymbolCacheProvider.TryGetSymbolCache(out ISymbolCache? table)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        AdsErrorCode result = AdsErrorCode.NoError;
        _symbolCacheSema.Wait();
        try
        {
            if (_symbolCache == null)
            {
                ResultValue<SymbolUploadInfo> resultValue = tryReadEncodings();
                result = resultValue.ErrorCode;
                if (resultValue.Succeeded)
                {
                    SymbolLoaderSettings symbolLoaderSettings = new SymbolLoaderSettings(SymbolsLoadMode.Flat, ValueAccessMode.SymbolicByHandle);
                    _symbolCache = new SymbolCache(this, SymbolLoaderFactory.createValueAccessor(this, symbolLoaderSettings), symbolLoaderSettings.ValueAccessMode, _uploadInfo, _loggerFactory);
                }
            }
        }
        finally
        {
            _symbolCacheSema.Release();
        }

        table = _symbolCache;
        return result;
    }

    //
    // Summary:
    //     Gets the symbol table asynchronously.
    //
    // Parameters:
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     SymbolInfoTable.
    async Task<ResultValue<ISymbolCache>> IAdsSymbolCacheProvider.GetSymbolCacheAsync(CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        await _symbolCacheSema.WaitAsync(cancel);
        ResultValue<ISymbolCache> result;
        try
        {
            if (_symbolCache == null)
            {
                ResultValue<SymbolUploadInfo> resultValue = await tryReadEncodingsAsync(cancel).ConfigureAwait(continueOnCapturedContext: false);
                if (resultValue.Succeeded)
                {
                    SymbolUploadInfo value = resultValue.Value;
                    SymbolLoaderSettings symbolLoaderSettings = new SymbolLoaderSettings(SymbolsLoadMode.Flat, ValueAccessMode.SymbolicByHandle);
                    _symbolCache = new SymbolCache(this, SymbolLoaderFactory.createValueAccessor(this, symbolLoaderSettings), symbolLoaderSettings.ValueAccessMode, value, _loggerFactory);
                    result = new ResultValue<ISymbolCache>(AdsErrorCode.NoError, _symbolCache);
                }
                else
                {
                    result = new ResultValue<ISymbolCache>(resultValue.ErrorCode, null);
                }
            }
            else
            {
                result = new ResultValue<ISymbolCache>(AdsErrorCode.NoError, _symbolCache);
            }
        }
        finally
        {
            _symbolCacheSema.Release();
        }

        return result;
    }

    //
    // Summary:
    //     Reads the symbol.
    //
    // Parameters:
    //   name:
    //     The name.
    //
    // Returns:
    //     IAdsSymbol.
    //
    // Exceptions:
    //   T:System.ArgumentOutOfRangeException:
    //     name
    //
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public IAdsSymbol ReadSymbol(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentOutOfRangeException("name");
        }

        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        IAdsSymbol symbol = null;
        TryReadSymbol(name, out symbol).ThrowOnError();
        return symbol;
    }

    //
    // Summary:
    //     Read symbol as an asynchronous operation.
    //
    // Parameters:
    //   name:
    //     The name.
    //
    //   cancel:
    //     The cancellation token that can be used by other objects or threads to receive
    //     notice of cancellation.
    //
    // Returns:
    //     A Task<ResultValue`1> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ArgumentOutOfRangeException:
    //     name
    //
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public async Task<ResultValue<IAdsSymbol>> ReadSymbolAsync(string name, CancellationToken cancel)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentOutOfRangeException("name");
        }

        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ResultValue<ISymbolCache> resultValue = await ((IAdsSymbolCacheProvider)this).GetSymbolCacheAsync(cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (resultValue.Succeeded)
        {
            return await resultValue.Value.ReadSymbolAsync(name, bLookup: true, cancel).ConfigureAwait(continueOnCapturedContext: false);
        }

        return new ResultValue<IAdsSymbol>(resultValue.ErrorCode, null);
    }

    //
    // Summary:
    //     Reads/Determines the DataType Inforrmation with the specifed name.
    //
    // Parameters:
    //   typeName:
    //     Name of the data type (without namespace)
    //
    // Returns:
    //     An containing the requested type.
    //
    // Exceptions:
    //   T:System.ArgumentOutOfRangeException:
    //     typeName
    //
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public IDataType ReadDataType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            throw new ArgumentOutOfRangeException("typeName");
        }

        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        IDataType dataType = null;
        TryReadDataType(typeName, out dataType).ThrowOnError();
        return dataType;
    }

    //
    // Summary:
    //     Tries to Read/Determine the DataType of the specified type.
    //
    // Parameters:
    //   typeName:
    //     Name of the symbol.
    //
    //   dataType:
    //     The symbol.
    //
    // Returns:
    //     A TwinCAT.TypeSystem.IDataType containing the requested symbol information or
    //     null if symbol could not be found.
    //
    // Exceptions:
    //   T:System.ArgumentOutOfRangeException:
    //     typeName
    //
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public AdsErrorCode TryReadDataType(string typeName, out IDataType? dataType)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            throw new ArgumentOutOfRangeException("typeName");
        }

        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ISymbolCache table = null;
        dataType = null;
        AdsErrorCode adsErrorCode = ((IAdsSymbolCacheProvider)this).TryGetSymbolCache(out table);
        if (adsErrorCode.Succeeded())
        {
            adsErrorCode = table.TryReadType(typeName, lookup: true, out dataType);
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Read data type as an asynchronous operation.
    //
    // Parameters:
    //   typeName:
    //     Name of the data type.
    //
    //   cancel:
    //     The cancel token.
    //
    // Returns:
    //     A Task<ResultValue`1> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ArgumentOutOfRangeException:
    //     typeName
    //
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public async Task<ResultValue<IDataType>> ReadDataTypeAsync(string typeName, CancellationToken cancel)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            throw new ArgumentOutOfRangeException("typeName");
        }

        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ResultValue<ISymbolCache> resultValue = await ((IAdsSymbolCacheProvider)this).GetSymbolCacheAsync(cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (resultValue.Succeeded)
        {
            return await resultValue.Value.ReadTypeAsync(typeName, lookup: true, cancel).ConfigureAwait(continueOnCapturedContext: false);
        }

        return new ResultValue<IDataType>(resultValue.ErrorCode, null);
    }

    //
    // Summary:
    //     Writes a (boxed) value to the symbol as an asynchronous operation.
    //
    // Parameters:
    //   symbol:
    //     The symbol the value is written to.
    //
    //   val:
    //     The value to write.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'WriteSymbol' operation. The TwinCAT.Ads.ResultWrite
    //     parameter contains the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public Task<ResultWrite> WriteValueAsync(ISymbol symbol, object val, CancellationToken cancel)
    {
        if (symbol == null)
        {
            throw new ArgumentNullException("symbol");
        }

        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ISymbol symbol2 = symbol.Unwrap();
        if (symbol2 is IProcessImageAddress { IsVirtual: not false })
        {
            return Task.FromResult(ResultWrite.CreateError(AdsErrorCode.DeviceInvalidAccess));
        }

        IDataType? type = symbol2.DataType ?? throw new CannotResolveDataTypeException(symbol2);
        getAccessMethod(symbol2);
        bool flag = (getAccessMethod(symbol2) & AccessMethods.ValueByName) != 0;
        if (!PrimitiveTypeMarshaler.TryGetManagedType(type, out Type managed))
        {
            managed = val.GetType();
        }

        PrimitiveTypeMarshaler primitiveTypeMarshaler = PrimitiveTypeMarshaler.CreateFrom(type);
        object obj = val;
        if (val != null && managed != val.GetType())
        {
            obj = PrimitiveTypeMarshaler.Convert(val, managed);
        }

        if (flag)
        {
            return WriteSymbolAsync(symbol2.InstancePath, obj, cancel);
        }

        IAdsSymbol adsSymbol = (IAdsSymbol)symbol2;
        byte[] array = new byte[primitiveTypeMarshaler.MarshalValueSize(obj, DefaultValueEncoding)];
        primitiveTypeMarshaler.Marshal(obj, DefaultValueEncoding, array.AsSpan());
        return WriteAsync(adsSymbol.IndexGroup, adsSymbol.IndexOffset, array.AsMemory(), cancel);
    }

    //
    // Summary:
    //     Writes a (boxed) value to the symbol.
    //
    // Parameters:
    //   symbol:
    //     The symbol the value is written to.
    //
    //   val:
    //     The value.
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public void WriteValue(ISymbol symbol, object val)
    {
        TryWriteValue(symbol, val).ThrowOnError();
    }

    //
    // Summary:
    //     Writes a (boxed value) to the symbol instance specified by its instance/symbol
    //     path.
    //
    // Parameters:
    //   name:
    //     The name.
    //
    //   value:
    //     Object holding the value to be written to the ADS symbol
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public void WriteValue(string name, object value)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        TryWriteValue(name, value).ThrowOnError();
    }

    //
    // Summary:
    //     Writes the passed object value to the specified ADS symbol.The parameter type
    //     must have the same layout as the ADS symbol.
    //
    // Parameters:
    //   name:
    //     Name of the ADS symbol.
    //
    //   value:
    //     Object holding the value to be written to the ADS symbol
    //
    //   cancel:
    //     The cancel token.
    //
    // Returns:
    //     A task that represents the asynchronous 'WriteSymbol' operation. The TwinCAT.Ads.ResultWrite
    //     parameter contains the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    public async Task<ResultWrite> WriteSymbolAsync(string name, object value, CancellationToken cancel)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ResultValue<ISymbolCache> resultValue = await ((IAdsSymbolCacheProvider)this).GetSymbolCacheAsync(cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (resultValue.Succeeded)
        {
            return await resultValue.Value.WriteValueAsync(name, value, cancel).ConfigureAwait(continueOnCapturedContext: false);
        }

        return ResultWrite.CreateError(resultValue.ErrorCode);
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to the given stream.
    //
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   memory:
    //     The memory.
    //
    //   readBytes:
    //     The number of read bytes.
    //
    // Returns:
    //     The ADS error code.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public AdsErrorCode TryRead(uint indexGroup, uint indexOffset, Memory<byte> memory, out int readBytes)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        Func<uint, AdsErrorCode> readRequest = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            if (adsErrorCode.Succeeded())
            {
                adsErrorCode = _server.ReadRequestSync(_target, id, indexGroup, indexOffset, memory.Length);
            }

            return adsErrorCode;
        };
        Action<ResultReadBytes> confirmResult = delegate (ResultReadBytes r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        ResultReadBytes resultReadBytes = _server.RequestAndReceiveReadBytesSync(readRequest, confirmResult, _timeout);
        readBytes = resultReadBytes.ReadBytes;
        if (resultReadBytes.Succeeded && resultReadBytes.ReadBytes > 0)
        {
            int length = ((resultReadBytes.ReadBytes > memory.Length) ? memory.Length : resultReadBytes.ReadBytes);
            resultReadBytes.Data.CopyTo(memory.Slice(0, length));
        }

        return resultReadBytes.ErrorCode;
    }

    //
    // Summary:
    //     Read as an asynchronous operation.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   readBuffer:
    //     The read buffer.
    //
    //   cancel:
    //     The cancellation token that can be used by other objects or threads to receive
    //     notice of cancellation.
    //
    // Returns:
    //     A Task<ResultRead> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public async Task<ResultRead> ReadAsync(uint indexGroup, uint indexOffset, Memory<byte> readBuffer, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        Func<uint, Task<AdsErrorCode>> readRequest = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.ReadRequestAsync(_target, id, indexGroup, indexOffset, readBuffer.Length, cancel) : Task.FromResult(adsErrorCode);
        };
        Action<ResultRead> confirmResult = delegate (ResultRead r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        ResultReadBytes resultReadBytes = await _server.RequestReadBytesAsync(readRequest, confirmResult, _timeout, cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (resultReadBytes.ErrorCode == AdsErrorCode.NoError && resultReadBytes.ReadBytes > 0)
        {
            int length = ((resultReadBytes.ReadBytes > readBuffer.Length) ? readBuffer.Length : resultReadBytes.ReadBytes);
            resultReadBytes.Data.CopyTo(readBuffer.Slice(0, length));
        }

        return resultReadBytes;
    }

    //
    // Summary:
    //     Read write as an asynchronous operation.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   readBuffer:
    //     The read buffer.
    //
    //   writeBuffer:
    //     The write buffer.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A Task<ResultReadWrite> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public async Task<ResultReadWrite> ReadWriteAsync(uint indexGroup, uint indexOffset, Memory<byte> readBuffer, ReadOnlyMemory<byte> writeBuffer, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        Func<uint, Task<AdsErrorCode>> readRequest = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.ReadWriteRequestAsync(_target, id, indexGroup, indexOffset, readBuffer.Length, writeBuffer, cancel) : Task.FromResult(adsErrorCode);
        };
        Action<ResultReadBytes> confirmResult = delegate (ResultReadBytes r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        ResultReadBytes resultReadBytes = await _server.RequestReadBytesAsync(readRequest, confirmResult, _timeout, cancel).ConfigureAwait(continueOnCapturedContext: false);
        resultReadBytes.Data.CopyTo(readBuffer);
        return new ResultReadWrite(resultReadBytes.ErrorCode, resultReadBytes.ReadBytes, resultReadBytes.InvokeId);
    }

    //
    // Summary:
    //     Read/Writes data to/from the specified writeBuffer, readBuffer
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   readBuffer:
    //     The read buffer.
    //
    //   writeBuffer:
    //     The write buffer.
    //
    // Returns:
    //     A task that represents the asynchronous 'ReadWrite' operation. The TwinCAT.Ads.ResultReadWrite
    //     parameter contains the total number of bytes read into the buffer (TwinCAT.Ads.ResultRead.ReadBytes)
    //     and the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    private ResultReadWrite ReadWriteSync(uint indexGroup, uint indexOffset, Memory<byte> readBuffer, ReadOnlyMemory<byte> writeBuffer)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        Func<uint, AdsErrorCode> readRequest = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.ReadWriteRequestSync(_target, id, indexGroup, indexOffset, readBuffer.Length, writeBuffer.Span) : adsErrorCode;
        };
        Action<ResultReadBytes> confirmResult = delegate (ResultReadBytes r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        ResultReadBytes resultReadBytes = _server.RequestAndReceiveReadBytesSync(readRequest, confirmResult, _timeout);
        resultReadBytes.Data.CopyTo(readBuffer);
        return new ResultReadWrite(resultReadBytes.ErrorCode, resultReadBytes.ReadBytes, resultReadBytes.InvokeId);
    }

    //
    // Summary:
    //     Writes data synchronously to an ADS device and reads data from that device.
    //
    // Parameters:
    //   indexGroup:
    //     The index group number of the requested ADS service.
    //
    //   indexOffset:
    //     The index offset number of the requested ADS service.
    //
    //   readBuffer:
    //     The read buffer.
    //
    //   writeBuffer:
    //     The write buffer.
    //
    //   readBytes:
    //     The read bytes.
    //
    // Returns:
    //     The ADS Error code.
    public AdsErrorCode TryReadWrite(uint indexGroup, uint indexOffset, Memory<byte> readBuffer, ReadOnlyMemory<byte> writeBuffer, out int readBytes)
    {
        ResultReadWrite resultReadWrite = ReadWriteSync(indexGroup, indexOffset, readBuffer, writeBuffer);
        readBytes = resultReadWrite.ReadBytes;
        return resultReadWrite.ErrorCode;
    }

    //
    // Summary:
    //     Writes data synchronously to an ADS device and then Reads data from this device
    //     into the readBuffer
    //
    // Parameters:
    //   indexGroup:
    //     The index group number of the requested ADS service.
    //
    //   indexOffset:
    //     The index offset number of the requested ADS service.
    //
    //   readBuffer:
    //     The read buffer.
    //
    //   writeBuffer:
    //     The write buffer.
    //
    // Returns:
    //     Number of successfully returned (read) data bytes.
    public int ReadWrite(uint indexGroup, uint indexOffset, Memory<byte> readBuffer, ReadOnlyMemory<byte> writeBuffer)
    {
        int readBytes = 0;
        TryReadWrite(indexGroup, indexOffset, readBuffer, writeBuffer, out readBytes).ThrowOnError();
        return readBytes;
    }

    //
    // Summary:
    //     Writes the asynchronous.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   cancel:
    //     The cancellation token that can be used by other objects or threads to receive
    //     notice of cancellation.
    //
    // Returns:
    //     Task<ResultWrite>.
    public Task<ResultWrite> WriteAsync(uint indexGroup, uint indexOffset, CancellationToken cancel)
    {
        return WriteAsync(indexGroup, indexOffset, Memory<byte>.Empty, cancel);
    }

    //
    // Summary:
    //     Write as an asynchronous operation.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   writeBuffer:
    //     The write buffer.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A Task<ResultWrite> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public async Task<ResultWrite> WriteAsync(uint indexGroup, uint indexOffset, ReadOnlyMemory<byte> writeBuffer, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        Func<uint, Task<AdsErrorCode>> request = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.WriteRequestAsync(_target, id, indexGroup, indexOffset, writeBuffer, cancel) : Task.FromResult(adsErrorCode);
        };
        Action<ResultAds> confirmResult = delegate (ResultAds r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        ResultAds resultAds = await _server.RequestAsync(request, confirmResult, _timeout, cancel).ConfigureAwait(continueOnCapturedContext: false);
        return new ResultWrite(resultAds.ErrorCode, resultAds.InvokeId);
    }

    //
    // Summary:
    //     Writes the data / Value into the specified writeBuffer.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   writeBuffer:
    //     The write buffer.
    //
    // Returns:
    //     A task that represents the asynchronous 'Write' operation. The TwinCAT.Ads.ResultWrite
    //     parameter contains the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    private ResultWrite WriteSync(uint indexGroup, uint indexOffset, ReadOnlyMemory<byte> writeBuffer)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        Func<uint, AdsErrorCode> request = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.WriteRequestSync(_target, id, indexGroup, indexOffset, writeBuffer.Span) : adsErrorCode;
        };
        Action<ResultAds> confirmResult = delegate (ResultAds r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        ResultAds resultAds = _server.RequestAndReceiveSync(request, confirmResult, _timeout);
        return new ResultWrite(resultAds.ErrorCode, resultAds.InvokeId);
    }

    //
    // Summary:
    //     Read any as an asynchronous operation.
    //
    // Parameters:
    //   indexGroup:
    //     Index group of the ADS variable.
    //
    //   indexOffset:
    //     Index offset of the ADS variable.
    //
    //   type:
    //     Type of the object to be read.
    //
    //   args:
    //     Additional arguments.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A Task<ResultAnyValue> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public async Task<ResultAnyValue> ReadAnyAsync(uint indexGroup, uint indexOffset, Type type, int[]? args, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        _anyTypeMarshaller.CanMarshal(type, args, DefaultValueEncoding);
        int length = _anyTypeMarshaller.MarshalSize(type, args, DefaultValueEncoding);
        ResultReadBytes resultReadBytes = await readAsync(indexGroup, indexOffset, length, cancel).ConfigureAwait(continueOnCapturedContext: false);
        object value = null;
        if (resultReadBytes.Succeeded)
        {
            _anyTypeMarshaller.Unmarshal(type, args, resultReadBytes.Data.Span, DefaultValueEncoding, out value);
        }

        return new ResultAnyValue(resultReadBytes.ErrorCode, value, resultReadBytes.InvokeId);
    }

    //
    // Summary:
    //     Reads any as result.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   type:
    //     The type.
    //
    //   args:
    //     The arguments.
    //
    // Returns:
    //     ResultAnyValue.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResultAnyValue ReadAnyAsResult(uint indexGroup, uint indexOffset, Type type, int[]? args)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        _anyTypeMarshaller.CanMarshal(type, args, DefaultValueEncoding);
        byte[] array = new byte[_anyTypeMarshaller.MarshalSize(type, args, DefaultValueEncoding)];
        int readBytes = 0;
        AdsErrorCode errorCode = TryRead(indexGroup, indexOffset, array, out readBytes);
        object value = null;
        if (errorCode.Succeeded())
        {
            _anyTypeMarshaller.Unmarshal(type, args, array, DefaultValueEncoding, out value);
        }

        return new ResultAnyValue(errorCode, value, 0u);
    }

    //
    // Summary:
    //     Reads data asynchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   indexGroup:
    //     Index group of the ADS variable.
    //
    //   indexOffset:
    //     Index offset of the ADS variable.
    //
    //   type:
    //     Type of the object to be read.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous read operation. The TwinCAT.Ads.ResultAnyValue
    //     parameter contains the read value (TwinCAT.Ads.ResultValue`1.Value) and the TwinCAT.Ads.ResultAds.ErrorCode
    //     after execution.
    public Task<ResultAnyValue> ReadAnyAsync(uint indexGroup, uint indexOffset, Type type, CancellationToken cancel)
    {
        return ReadAnyAsync(indexGroup, indexOffset, type, null, cancel);
    }

    //
    // Summary:
    //     Read any as an asynchronous operation.
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   type:
    //     Type of the object to be read.
    //
    //   args:
    //     Additional arguments.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A Task<ResultAnyValue> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public async Task<ResultAnyValue> ReadAnyAsync(uint variableHandle, Type type, int[]? args, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        _anyTypeMarshaller.CanMarshal(type, args, DefaultValueEncoding);
        int length = _anyTypeMarshaller.MarshalSize(type, args, DefaultValueEncoding);
        ResultReadBytes resultReadBytes = await readAsync(61445u, variableHandle, length, cancel).ConfigureAwait(continueOnCapturedContext: false);
        ResultAnyValue result;
        if (resultReadBytes.Succeeded)
        {
            object value = null;
            _anyTypeMarshaller.Unmarshal(type, args, resultReadBytes.Data.Span, DefaultValueEncoding, out value);
            result = new ResultAnyValue(resultReadBytes.ErrorCode, value, resultReadBytes.InvokeId);
        }
        else
        {
            result = new ResultAnyValue(resultReadBytes.ErrorCode, null, resultReadBytes.InvokeId);
        }

        return result;
    }

    //
    // Summary:
    //     Reads data synchronously from an ADS device and writes it to an object.
    //
    // Parameters:
    //   variableHandle:
    //     The variable/symbol handle.
    //
    //   type:
    //     Type of the object to be read.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous read operation. The TwinCAT.Ads.ResultAnyValue
    //     parameter contains the read value (TwinCAT.Ads.ResultValue`1.Value) and the TwinCAT.Ads.ResultAds.ErrorCode
    //     after execution.
    //
    // Remarks:
    //     As object types only primitive types are supported.
    public Task<ResultAnyValue> ReadAnyAsync(uint variableHandle, Type type, CancellationToken cancel)
    {
        return ReadAnyAsync(variableHandle, type, null, cancel);
    }

    //
    // Summary:
    //     Writes an object asynchronously to an ADS device. If the Type of the object to
    //     be written is a string type, the first element of parameter args specifies the
    //     number of characters of the string.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   value:
    //     Object to write to the ADS device.
    //
    //   args:
    //     Additional arguments.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous task operation. The result parameter
    //     TwinCAT.Ads.ResultWrite of the write operation contains the TwinCAT.Ads.ResultAds.ErrorCode.
    //
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     As object types only primitive types are supported. If the Type of the object
    //     to be read is a string type, the first element of the parameter args specifies
    //     the number of characters of the string. If the Type of the object to be read
    //     is an array type, the number of elements for each dimension has to be specified
    //     in the parameter args.
    //
    //     Type of value Parameter –Necessary Arguments (args)
    //     string –args[0]: Number of characters in the string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     string[] –args[0]: Number of characters in each string typed as TwinCAT.TypeSystem.StringConvertMode.FixedLengthZeroTerminated.
    //
    //     Array –args: Dimensions of Array as int[] string : string[] : Array : args Dimensions
    //     of Array
    public Task<ResultWrite> WriteAnyAsync(uint indexGroup, uint indexOffset, object value, int[]? args, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        byte[] array = new byte[_anyTypeMarshaller.MarshalValueSize(value, args, DefaultValueEncoding)];
        _anyTypeMarshaller.Marshal(value, args, DefaultValueEncoding, array.AsSpan());
        return WriteAsync(indexGroup, indexOffset, array.AsMemory(), cancel);
    }

    //
    // Summary:
    //     Writes an object asynchronously to an ADS device. If the Type of the object to
    //     be written is a string type, the first element of parameter args specifies the
    //     number of characters of the string.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   value:
    //     Object to write to the ADS device.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous task operation. The result parameter
    //     TwinCAT.Ads.ResultWrite of the write operation contains the TwinCAT.Ads.ResultAds.ErrorCode.
    public Task<ResultWrite> WriteAnyAsync(uint indexGroup, uint indexOffset, object value, CancellationToken cancel)
    {
        return WriteAnyAsync(indexGroup, indexOffset, value, null, cancel);
    }

    //
    // Summary:
    //     Read as an asynchronous operation.
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   readBuffer:
    //     The read buffer/data.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A Task<ResultRead> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public async Task<ResultRead> ReadAsync(uint variableHandle, Memory<byte> readBuffer, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ResultRead resultRead = await ReadAsync(61445u, variableHandle, readBuffer, cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (_handleCache != null && resultRead.ErrorCode == AdsErrorCode.DeviceSymbolVersionInvalid)
        {
            _handleCache.Remove(variableHandle);
        }

        return resultRead;
    }

    //
    // Summary:
    //     Writes the value data synchronously that is represented in the writeBuffer to
    //     the symbol with the specified variableHandle.
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   writeBuffer:
    //     The write buffer / value.
    //
    // Returns:
    //     The ADS error code.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public AdsErrorCode TryWrite(uint variableHandle, ReadOnlyMemory<byte> writeBuffer)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        return TryWrite(61445u, variableHandle, writeBuffer);
    }

    //
    // Summary:
    //     Write as an asynchronous operation.
    //
    // Parameters:
    //   variableHandle:
    //     The variable handle.
    //
    //   writeBuffer:
    //     The write buffer/value.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A Task<ResultWrite> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public async Task<ResultWrite> WriteAsync(uint variableHandle, ReadOnlyMemory<byte> writeBuffer, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ResultWrite resultWrite = await WriteAsync(61445u, variableHandle, writeBuffer, cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (_handleCache != null && resultWrite.ErrorCode == AdsErrorCode.DeviceSymbolVersionInvalid)
        {
            _handleCache.Remove(variableHandle);
        }

        return resultWrite;
    }

    //
    // Summary:
    //     Read write as an asynchronous operation.
    //
    // Parameters:
    //   variableHandle:
    //     Variable handle.
    //
    //   readBuffer:
    //     The read data / value
    //
    //   writeBuffer:
    //     The write data / value.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A Task<ResultReadWrite> representing the asynchronous operation.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public async Task<ResultReadWrite> ReadWriteAsync(uint variableHandle, Memory<byte> readBuffer, ReadOnlyMemory<byte> writeBuffer, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ResultReadWrite obj = await ReadWriteAsync(61445u, variableHandle, readBuffer, writeBuffer, cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (obj.ErrorCode == AdsErrorCode.DeviceSymbolVersionInvalid)
        {
            _handleCache.Remove(variableHandle);
        }

        return obj;
    }

    //
    // Summary:
    //     Connects a variable to the ADS client. The ADS client will be notified by the
    //     TwinCAT.Ads.IAdsNotifications.AdsNotification event.
    //
    // Parameters:
    //   indexGroup:
    //     The index group number of the requested ADS service.
    //
    //   indexOffset:
    //     The index offset number of the requested ADS service.
    //
    //   dataSize:
    //     Maximum amount of data in bytes to receive with this ADS Notification.
    //
    //   settings:
    //     The Notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    //   handle:
    //     The notification handle.
    //
    // Returns:
    //     The ADS error code.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     The
    //
    //     dataSize
    //
    //     Parameter defines the amount of bytes, that will be attached to the TwinCAT.Ads.AdsClient.AdsNotification
    //     as value. Because notifications allocate TwinCAT system resources, a complementary
    //     call to TwinCAT.Ads.IAdsNotifications.TryDeleteDeviceNotification(System.UInt32)
    //     should always called when the notification is not used anymore.
    public AdsErrorCode TryAddDeviceNotification(uint indexGroup, uint indexOffset, int dataSize, NotificationSettings settings, object? userData, out uint handle)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        return _notificationReceiver.TryAddDeviceNotification(indexGroup, indexOffset, dataSize, settings, userData, _timeout, out handle);
    }

    //
    // Summary:
    //     Connects a variable to the ADS client. The ADS client will be notified by the
    //     TwinCAT.Ads.IAdsNotifications.AdsNotificationEx event.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   settings:
    //     The Notification settings.
    //
    //   userData:
    //     This object can be used to store user specific data (tag data)
    //
    //   anyType:
    //     Type of the object stored in the event argument ('AnyType')
    //
    //   args:
    //     The 'AnyType' arguments.
    //
    //   handle:
    //     The notification handle.
    //
    // Returns:
    //     The ADS Error code.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     If type is a string type, the first element of the parameter args specifies the
    //     number of characters of the string. If type is an array type, the number of elements
    //     for each dimension has to be specified in the parameter args. Only primitive
    //     types (AnyType) are supported by this method. Because notifications allocate
    //     TwinCAT system resources, a complementary call to TwinCAT.Ads.IAdsNotifications.DeleteDeviceNotification(System.UInt32)
    //     should always called when the notification is not used anymore.
    public AdsErrorCode TryAddDeviceNotificationEx(uint indexGroup, uint indexOffset, NotificationSettings settings, object? userData, Type anyType, int[]? args, out uint handle)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!_anyTypeMarshaller.CanMarshal(anyType, args, DefaultValueEncoding))
        {
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Cannot marshal the type '{0}' as 'ANY' type!", anyType), "anyType");
        }

        int dataSize = _anyTypeMarshaller.MarshalSize(anyType, args, DefaultValueEncoding);
        AdsNotificationExUserData userData2 = new AdsNotificationExUserData(anyType, args, userData);
        return _notificationReceiver.TryAddDeviceNotification(indexGroup, indexOffset, dataSize, settings, userData2, _timeout, out handle);
    }

    //
    // Summary:
    //     Connects a variable to the ADS client asynchronously. The ADS client will be
    //     notified by the TwinCAT.Ads.AdsClient.AdsNotificationEx event.
    //
    // Parameters:
    //   indexGroup:
    //     Contains the index group number of the requested ADS service.
    //
    //   indexOffset:
    //     Contains the index offset number of the requested ADS service.
    //
    //   settings:
    //     The settings.
    //
    //   userData:
    //     This object can be used to store user specific data.
    //
    //   anyType:
    //     Type of the object stored in the event argument, only Primitive 'AnyTypes' allowed.
    //
    //
    //   args:
    //     Additional arguments (for 'AnyType')
    //
    //   cancel:
    //     The Cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'AddDeviceNotification' operation. The
    //     TwinCAT.Ads.ResultHandle type parameter contains the created handle (TwinCAT.Ads.ResultHandle.Handle)
    //     and the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    //
    // Remarks:
    //     If type is a string type, the first element of the parameter args specifies the
    //     number of characters of the string. If type is an array type, the number of elements
    //     for each dimension has to be specified in the parameter args. Only primitive
    //     types (AnyType) are supported by this method. Because notifications allocate
    //     TwinCAT system resources, a complementary call to TwinCAT.Ads.AdsClient.DeleteDeviceNotificationAsync(System.UInt32,System.Threading.CancellationToken)
    //     should always called when the notification is not used anymore.
    public Task<ResultHandle> AddDeviceNotificationExAsync(uint indexGroup, uint indexOffset, NotificationSettings settings, object? userData, Type anyType, int[]? args, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (!_anyTypeMarshaller.CanMarshal(anyType, args, DefaultValueEncoding))
        {
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Cannot marshal the type '{0}' as 'ANY' type!", anyType), "anyType");
        }

        int dataSize = _anyTypeMarshaller.MarshalSize(anyType, args, DefaultValueEncoding);
        AdsNotificationExUserData userData2 = new AdsNotificationExUserData(anyType, args, userData);
        return _notificationReceiver.AddDeviceNotificationAsync(indexGroup, indexOffset, dataSize, settings, userData2, cancel);
    }

    //
    // Summary:
    //     Deletes a registered notification.
    //
    // Parameters:
    //   notificationHandle:
    //     Notification handle.
    //
    // Returns:
    //     The ADS error code.
    //
    // Remarks:
    //     This is the complementary method to TwinCAT.Ads.IAdsNotifications.TryAddDeviceNotification
    //     overloads and should be called when the notification is not needed anymore the
    //     free TwinCAT realtime resources.
    public AdsErrorCode TryDeleteDeviceNotification(uint notificationHandle)
    {
        return TryDeleteDeviceNotification(notificationHandle, _timeout);
    }

    //
    // Summary:
    //     Deletes a registered notification.
    //
    // Parameters:
    //   notificationHandle:
    //     Notification handle.
    //
    //   timeout:
    //     The timeout.
    //
    // Returns:
    //     The ADS error code.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     This is the complementary method to TwinCAT.Ads.IAdsNotifications.TryAddDeviceNotification
    //     overloads and should be called when the notification is not needed anymore the
    //     free TwinCAT realtime resources.
    public AdsErrorCode TryDeleteDeviceNotification(uint notificationHandle, int timeout)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        uint variableHandle = 0u;
        AdsErrorCode adsErrorCode = _notificationReceiver.TryDeleteDeviceNotification(notificationHandle, timeout, out variableHandle);
        if (_handleCache != null && adsErrorCode.Succeeded())
        {
            _handleCache.TryDeleteVariableHandle(variableHandle, timeout);
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Deletes a registered notification asynchronously.
    //
    // Parameters:
    //   notificationHandle:
    //     Notification handle.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     A task that represents the asynchronous 'DeleteDeviceNotification' operation.
    //     The TwinCAT.Ads.ResultAds.ErrorCode property contains the ADS error code after
    //     execution.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     This is the complementary method to TwinCAT.Ads.IAdsNotifications.AddDeviceNotificationAsync
    //     overloads and should be called when the notification is not needed anymore the
    //     free TwinCAT realtime resources.
    public async Task<ResultAds> DeleteDeviceNotificationAsync(uint notificationHandle, CancellationToken cancel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ResultHandle result = await _notificationReceiver.DeleteDeviceNotificationAsync(notificationHandle, cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (_handleCache != null && result.Succeeded)
        {
            await _handleCache.DeleteVariableHandleAsync(result.Handle, cancel).ConfigureAwait(continueOnCapturedContext: false);
        }

        return result;
    }

    //
    // Summary:
    //     Adds a DeviceNotification asynchronously.
    //
    // Parameters:
    //   indexGroup:
    //     The index group.
    //
    //   indexOffset:
    //     The index offset.
    //
    //   dataLength:
    //     Length of the data.
    //
    //   settings:
    //     The Notification settings.
    //
    //   notificationHandler:
    //     The notification handler.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     Task<ResultHandle>.
    Task<ResultHandle> INotificationProvider.RegisterNotificationInternalAsync(uint indexGroup, uint indexOffset, int dataLength, NotificationSettings settings, Action<AmsAddress, Dictionary<DateTimeOffset, NotificationQueueElement[]>> notificationHandler, CancellationToken cancel)
    {
        NotificationSettings settings2 = settings;
        Func<uint, Task<AdsErrorCode>> handleRequest = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.AddDeviceNotificationRequestAsync(_target, id, indexGroup, indexOffset, dataLength, settings2, cancel) : Task.FromResult(adsErrorCode);
        };
        Action<ResultHandle> confirmResult = delegate (ResultHandle r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        return _server.RequestHandleAsync(handleRequest, confirmResult, _timeout, cancel);
    }

    //
    // Summary:
    //     Deletes a Device Notification.
    //
    // Parameters:
    //   handle:
    //     The Notification handle.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     Task<ResultAds>.
    Task<ResultAds> INotificationProvider.UnregisterNotificationInternalAsync(uint handle, CancellationToken cancel)
    {
        Func<uint, Task<AdsErrorCode>> request = delegate (uint id)
        {
            AdsErrorCode adsErrorCode = AdsErrorCode.NoError;
            if (_interceptors != null)
            {
                adsErrorCode = _interceptors.BeforeCommunicate();
            }

            return adsErrorCode.Succeeded() ? _server.DeleteDeviceNotificationRequestAsync(_target, id, handle, cancel) : Task.FromResult(adsErrorCode);
        };
        Action<ResultAds> confirmResult = delegate (ResultAds r)
        {
            if (_interceptors != null)
            {
                _interceptors.AfterCommunicate(resurrect: false, r);
            }
        };
        return _server.RequestAsync(request, confirmResult, _timeout, cancel);
    }

    //
    // Summary:
    //     Removes / Deletes a Device Notification.
    //
    // Parameters:
    //   handle:
    //     The handle.
    //
    //   timeout:
    //     The timeout.
    //
    // Returns:
    //     AdsErrorCode.
    AdsErrorCode INotificationProvider.UnregisterNotificationInternal(uint handle, int timeout)
    {
        return TryDeleteDeviceNotification(handle, timeout);
    }

    //
    // Summary:
    //     Removes / Deletes a Device Notification
    //
    // Parameters:
    //   handles:
    //     The Notification handles.
    //
    //   subResults:
    //     The results of the Unregistering process.
    //
    // Returns:
    //     AdsErrorCode.
    AdsErrorCode INotificationProvider.UnregisterNotificationInternal(uint[] handles, out AdsErrorCode[]? subResults)
    {
        return new SumDeleteNotifications(this, handles).TryReleaseHandles(out subResults);
    }

    //
    // Summary:
    //     Changes the ADS status and the device status of an ADS server.
    //
    // Parameters:
    //   stateInfo:
    //     New ADS status and device status.
    //
    //   writeBuffer:
    //     The write buffer.
    //
    // Returns:
    //     AdsErrorCode.
    public AdsErrorCode TryWriteControl(StateInfo stateInfo, ReadOnlyMemory<byte> writeBuffer)
    {
        return WriteControlSync(stateInfo.AdsState, (ushort)stateInfo.DeviceState, writeBuffer).ErrorCode;
    }

    //
    // Summary:
    //     Injection of an SymbolVersionChanged event (just for Testing purposes)
    void IAdsInjectAcceptor.InjectSymbolVersionChanged()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        AdsSymbolVersionChangedEventArgs eventArgs = new AdsSymbolVersionChangedEventArgs(byte.MaxValue);
        ((ISymbolVersionChangedReceiver)this).OnSymbolVersionChanged(eventArgs);
    }

    //
    // Summary:
    //     Tries to read the symbol information object specified by the instance path.
    //
    // Parameters:
    //   name:
    //     The name.
    //
    //   symbol:
    //     The symbol.
    //
    // Returns:
    //     An TwinCAT.Ads.TypeSystem.IAdsSymbol containing the requested symbol information
    //     or null if symbol could not be found.
    //
    // Exceptions:
    //   T:System.ArgumentOutOfRangeException:
    //     name
    //
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    public AdsErrorCode TryReadSymbol(string name, out IAdsSymbol? symbol)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentOutOfRangeException("name");
        }

        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ISymbolCache table = null;
        symbol = null;
        AdsErrorCode adsErrorCode = ((IAdsSymbolCacheProvider)this).TryGetSymbolCache(out table);
        if (adsErrorCode.Succeeded())
        {
            adsErrorCode = table.TryReadSymbol(name, bLookup: true, out symbol);
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Tries to write a (boxed) value to the symbol
    //
    // Parameters:
    //   symbol:
    //     The symbol the value is written to.
    //
    //   val:
    //     The value.
    //
    // Returns:
    //     AdsErrorCode.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     symbol
    //
    //   T:System.ArgumentNullException:
    //     val
    //
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    //   T:TwinCAT.TypeSystem.CannotResolveDataTypeException:
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public AdsErrorCode TryWriteValue(ISymbol symbol, object? val)
    {
        if (symbol == null)
        {
            throw new ArgumentNullException("symbol");
        }

        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ISymbol symbol2 = symbol.Unwrap();
        if (symbol2 is IProcessImageAddress { IsVirtual: not false })
        {
            return AdsErrorCode.DeviceInvalidAccess;
        }

        IDataType? type = symbol2.DataType ?? throw new CannotResolveDataTypeException(symbol2);
        if (val == null)
        {
            throw new ArgumentNullException("val");
        }

        getAccessMethod(symbol2);
        bool flag = (getAccessMethod(symbol2) & AccessMethods.ValueByName) != 0;
        if (!PrimitiveTypeMarshaler.TryGetManagedType(type, out Type managed))
        {
            managed = val.GetType();
        }

        PrimitiveTypeMarshaler primitiveTypeMarshaler = PrimitiveTypeMarshaler.CreateFrom(type);
        object obj = val;
        if (val != null && managed != val.GetType())
        {
            obj = PrimitiveTypeMarshaler.Convert(val, managed);
        }

        if (flag)
        {
            return TryWriteValue(symbol2.InstancePath, obj);
        }

        IAdsSymbol adsSymbol = (IAdsSymbol)symbol2;
        val?.GetType();
        byte[] array = new byte[primitiveTypeMarshaler.MarshalValueSize(obj, DefaultValueEncoding)];
        primitiveTypeMarshaler.Marshal(obj, DefaultValueEncoding, array.AsSpan());
        return TryWrite(adsSymbol.IndexGroup, adsSymbol.IndexOffset, array.AsMemory());
    }

    //
    // Summary:
    //     Tries to write a (boxed) value to the symbol instance specified by its instance/symbol
    //     path.
    //
    // Parameters:
    //   name:
    //     The name.
    //
    //   value:
    //     Object holding the value to be written to the ADS symbol
    //
    // Returns:
    //     AdsErrorCode.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public AdsErrorCode TryWriteValue(string name, object value)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        ISymbolCache table = null;
        AdsErrorCode adsErrorCode = ((IAdsSymbolCacheProvider)this).TryGetSymbolCache(out table);
        if (adsErrorCode.Succeeded())
        {
            adsErrorCode = table.TryWriteValue(name, value);
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Sets the default encoding.
    //
    // Parameters:
    //   symbolNameEncoding:
    //     The encoding.
    //
    //   defaultValueEncoding:
    //
    //   platformPointerSize:
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void SetEncodings(Encoding? symbolNameEncoding, Encoding? defaultValueEncoding, int platformPointerSize)
    {
        if (symbolNameEncoding != null)
        {
            _symbolNameEncoding = symbolNameEncoding;
            _encodingsInitialized = true;
        }

        if (defaultValueEncoding != null)
        {
            _defaultValueEncoding = defaultValueEncoding;
        }

        if (platformPointerSize == 4 || platformPointerSize == 8)
        {
            _platformPointerSize = platformPointerSize;
        }

        if (CanLog(LogLevel.Information))
        {
            _logger?.LogInformation($"Setting AdsClient encodings: SymbolNameEncoding:{symbolNameEncoding?.EncodingName}, DefaultValueEncoding:{defaultValueEncoding?.EncodingName},PlatformPointerSize:{platformPointerSize}");
        }

        PrimitiveTypeMarshaler.SetDefaultEncoding(defaultValueEncoding);
    }

    //
    // Summary:
    //     Invokes the specified RPC Method
    //
    // Parameters:
    //   symbolPath:
    //     The symbol path.
    //
    //   methodName:
    //     The method name.
    //
    //   inParameters:
    //     The input parameters or NULL
    //
    // Returns:
    //     The return value of the Method (as object).
    //
    // Remarks:
    //     This method only supports primitive data types as inParameters. Any available
    //     outparameters will be ignored. Complex types will fall back to byte[] arrays.
    public object? InvokeRpcMethod(string symbolPath, string methodName, object[]? inParameters)
    {
        object[] outParameters;
        return InvokeRpcMethod(symbolPath, methodName, inParameters, out outParameters);
    }

    //
    // Summary:
    //     Invokes the specified RPC Method
    //
    // Parameters:
    //   symbolPath:
    //     The symbol path.
    //
    //   methodName:
    //     The method name.
    //
    //   inParameters:
    //     The input parameters or NULL
    //
    //   outParameters:
    //     The output parameters.
    //
    // Returns:
    //     The return value of the Method (as object).
    //
    // Remarks:
    //     Because this overload doesn't provide any TwinCAT.TypeSystem.AnyTypeSpecifier
    //     specifications, only primitive datatypes will be correctly marshalled by this
    //     method. Complex types will fall back to byte[] arrays.
    public object? InvokeRpcMethod(string symbolPath, string methodName, object[]? inParameters, out object[]? outParameters)
    {
        object retValue = null;
        TryInvokeRpcMethod(symbolPath, methodName, inParameters, out outParameters, out retValue).ThrowOnError();
        return retValue;
    }

    //
    // Summary:
    //     Invokes the specified RPC Method
    //
    // Parameters:
    //   symbolPath:
    //     The symbol path.
    //
    //   methodName:
    //     The method name.
    //
    //   inParameters:
    //     The parameters.
    //
    //   outSpecifiers:
    //     The out specifiers (specifiying the out types) or NULL.
    //
    //   retSpecifier:
    //     The ret specifier (specifiying the return value) or NULL.
    //
    //   outParameters:
    //     The out parameters.
    //
    // Returns:
    //     The return value of the Method (as object).
    //
    // Remarks:
    //     The RpcMethod optionally support In-Parameters, Out-Parameters and Return values.
    //     Therefore the parameters inParameters, outParameters, outSpecifiers, retSpecifier
    //     are allowed to be empty or NULL. In case of using primitive datatypes, the type
    //     specifier parameters (outSpecifiers and retSpecifier) are not necessary and should
    //     not be set.
    public object? InvokeRpcMethod(string symbolPath, string methodName, object[]? inParameters, AnyTypeSpecifier[]? outSpecifiers, AnyTypeSpecifier? retSpecifier, out object[]? outParameters)
    {
        object retValue = null;
        TryInvokeRpcMethod(symbolPath, methodName, inParameters, out outParameters, out retValue).ThrowOnError();
        return retValue;
    }

    //
    // Summary:
    //     Invokes the specified RPC Method
    //
    // Parameters:
    //   symbolPath:
    //     The symbol path.
    //
    //   methodName:
    //     The method name.
    //
    //   inParameters:
    //     The parameters.
    //
    //   retSpecifier:
    //     The ret specifier (specifiying the return value) or NULL.
    //
    // Returns:
    //     The return value of the Method (as object).
    //
    // Remarks:
    //     The RpcMethod optionally support In-Parameters, and Return values. Therefore
    //     the parameters inParameters, retSpecifier are allowed to be empty or NULL. In
    //     case of using primitive datatypes, the type specifier parameter (retSpecifier)
    //     is not necessary and should not be set.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public object? InvokeRpcMethod(string symbolPath, string methodName, object[]? inParameters, AnyTypeSpecifier? retSpecifier)
    {
        object retValue = null;
        object[] outParameters = null;
        TryInvokeRpcMethod(symbolPath, methodName, inParameters, null, retSpecifier, out outParameters, out retValue).ThrowOnError();
        return retValue;
    }

    //
    // Summary:
    //     Invokes the RPC Method
    //
    // Parameters:
    //   symbolPath:
    //     The symbol.
    //
    //   methodName:
    //     Name of the method.
    //
    //   inParameters:
    //     The parameters.
    //
    //   outSpecifiers:
    //     The out specifiers (specifying the out types) or NULL.
    //
    //   retSpecifier:
    //     The ret specifier (specifying the return value) or NULL.
    //
    //   outParameters:
    //     The out parameters.
    //
    //   retValue:
    //     The return value of the RPC method./>
    //
    // Returns:
    //     AdsErrorCode.
    //
    // Remarks:
    //     The RpcMethod optionally support In-Parameters, Out-Parameters and Return values.
    //     Therefore the parameters inParameters, outParameters, outSpecifiers, retSpecifier
    //     are allowed to be empty or NULL. In case of using primitive datatypes, the type
    //     specifier parameters (outSpecifiers and retSpecifier) are not necessary and should
    //     not be set.
    public AdsErrorCode TryInvokeRpcMethod(string symbolPath, string methodName, object[]? inParameters, AnyTypeSpecifier[]? outSpecifiers, AnyTypeSpecifier? retSpecifier, out object[]? outParameters, out object? retValue)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (string.IsNullOrEmpty(symbolPath))
        {
            throw new ArgumentOutOfRangeException("symbolPath");
        }

        if (string.IsNullOrEmpty(methodName))
        {
            throw new ArgumentOutOfRangeException("methodName");
        }

        retValue = null;
        outParameters = null;
        IAdsSymbol symbol = null;
        AdsErrorCode adsErrorCode = TryReadSymbol(symbolPath, out symbol);
        if (adsErrorCode.Succeeded())
        {
            if (!(symbol is IStructInstance structInstance))
            {
                throw new RpcMethodNotSupportedException(methodName, symbol);
            }

            if (!structInstance.RpcMethods.TryGetMethod(methodName, out IRpcMethod method))
            {
                throw new ArgumentOutOfRangeException("methodName", "Method not found!");
            }

            adsErrorCode = TryInvokeRpcMethod(structInstance, method, inParameters, outSpecifiers, retSpecifier, out outParameters, out retValue);
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Invokes the specified RPC Method
    //
    // Parameters:
    //   symbolPath:
    //     The symbol path.
    //
    //   methodName:
    //     The method name.
    //
    //   inParameters:
    //     The parameters.
    //
    //   retValue:
    //     The return value of the RPC method as object.
    //
    // Returns:
    //     The ADS Error Code.
    //
    // Remarks:
    //     Because this overload doesn't provide any TwinCAT.TypeSystem.AnyTypeSpecifier
    //     specifications, only primitive datatypes will be correctly marshalled by this
    //     method. Complex types will fall back to byte[] arrays.
    public AdsErrorCode TryInvokeRpcMethod(string symbolPath, string methodName, object[]? inParameters, out object? retValue)
    {
        object[] outParameters = null;
        return TryInvokeRpcMethod(symbolPath, methodName, inParameters, out outParameters, out retValue);
    }

    //
    // Summary:
    //     Invokes the specified RPC Method
    //
    // Parameters:
    //   symbolPath:
    //     The symbol path.
    //
    //   methodName:
    //     The method name.
    //
    //   inParameters:
    //     The parameters.
    //
    //   retValue:
    //     The return value of the RPC method as object.
    //
    //   outParameters:
    //     The out parameters.
    //
    // Returns:
    //     The ADS Error Code.
    //
    // Remarks:
    //     Because this overload doesn't provide any TwinCAT.TypeSystem.AnyTypeSpecifier
    //     specifications, only primitive datatypes will be correctly marshalled by this
    //     method. Complex types will fall back to byte[] arrays.
    public AdsErrorCode TryInvokeRpcMethod(string symbolPath, string methodName, object[]? inParameters, out object[]? outParameters, out object? retValue)
    {
        return TryInvokeRpcMethod(symbolPath, methodName, inParameters, null, null, out outParameters, out retValue);
    }

    //
    // Summary:
    //     Invokes the specified RPC Method asynchronously
    //
    // Parameters:
    //   symbolPath:
    //     The symbol/Instance path of the symbol.
    //
    //   methodName:
    //     The method name.
    //
    //   inParameters:
    //     The parameters.
    //
    //   cancel:
    //     The cancellation token
    //
    // Returns:
    //     A task that represents the asynchronous 'InvokeRpcMethod' operation. The TwinCAT.Ads.ResultRpcMethod
    //     results contains the return value together with the output parameters.
    //
    // Remarks:
    //     Because this overload doesn't provide any TwinCAT.TypeSystem.AnyTypeSpecifier
    //     specifications, only primitive datatypes will be correctly marshalled by this
    //     method. Complex types will fall back to byte[] arrays.
    public async Task<ResultRpcMethod> InvokeRpcMethodAsync(string symbolPath, string methodName, object[]? inParameters, CancellationToken cancel)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (string.IsNullOrEmpty(symbolPath))
        {
            throw new ArgumentOutOfRangeException("symbolPath");
        }

        if (string.IsNullOrEmpty(methodName))
        {
            throw new ArgumentOutOfRangeException("methodName");
        }

        if (inParameters == null)
        {
            throw new ArgumentNullException("inParameters");
        }

        ResultRpcMethod result = ResultRpcMethod.Empty;
        ResultValue<IAdsSymbol> resultValue = await ReadSymbolAsync(symbolPath, cancel).ConfigureAwait(continueOnCapturedContext: false);
        result.SetError(resultValue.ErrorCode);
        if (resultValue.Succeeded)
        {
            if (!(resultValue.Value is IStructInstance structInstance))
            {
                throw new RpcMethodNotSupportedException(methodName, resultValue.Value);
            }

            IRpcMethod method = null;
            if (!structInstance.RpcMethods.TryGetMethod(methodName, out method))
            {
                throw new RpcMethodNotSupportedException(methodName, resultValue.Value);
            }

            result = await InvokeRpcMethodAsync(structInstance, method, inParameters, cancel).ConfigureAwait(continueOnCapturedContext: false);
        }

        return result;
    }

    //
    // Summary:
    //     Invokes the specified RPC Method asynchronously
    //
    // Parameters:
    //   symbolPath:
    //     The symbol/Instance path of the symbol.
    //
    //   methodName:
    //     The method name.
    //
    //   inParameters:
    //     The parameters.
    //
    //   outSpecifiers:
    //     The out specifiers (specifying the out types) or NULL.
    //
    //   retSpecifier:
    //     The ret specifier (specifying the return value) or NULL.
    //
    //   cancel:
    //     The cancellation token
    //
    // Returns:
    //     A task that represents the asynchronous 'InvokeRpcMethod' operation. The TwinCAT.Ads.ResultRpcMethod
    //     results contains the return value together with the output parameters. The RpcMethod
    //     optionally support In-Parameters, Out-Parameters and Return values. Therefore
    //     the parameters inParameters, outSpecifiers, retSpecifier are allowed to be empty
    //     or NULL. In case of using primitive datatypes, the type specifier parameters
    //     (outSpecifiers and retSpecifier) are not necessary and should not be set. TwinCAT.Ads.ResultRpcMethod.ReturnValue
    //     and the TwinCAT.Ads.ResultAds.ErrorCode of the ADS communication after execution.
    public async Task<ResultRpcMethod> InvokeRpcMethodAsync(string symbolPath, string methodName, object[]? inParameters, AnyTypeSpecifier[]? outSpecifiers, AnyTypeSpecifier? retSpecifier, CancellationToken cancel)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (string.IsNullOrEmpty(symbolPath))
        {
            throw new ArgumentOutOfRangeException("symbolPath");
        }

        if (string.IsNullOrEmpty(methodName))
        {
            throw new ArgumentOutOfRangeException("methodName");
        }

        if (inParameters == null)
        {
            throw new ArgumentNullException("inParameters");
        }

        ResultRpcMethod result = ResultRpcMethod.Empty;
        ResultValue<IAdsSymbol> resultValue = await ReadSymbolAsync(symbolPath, cancel).ConfigureAwait(continueOnCapturedContext: false);
        result.SetError(resultValue.ErrorCode);
        if (resultValue.Succeeded)
        {
            if (!(resultValue.Value is IStructInstance structInstance))
            {
                throw new RpcMethodNotSupportedException(methodName, resultValue.Value);
            }

            IRpcMethod method = null;
            if (!structInstance.RpcMethods.TryGetMethod(methodName, out method))
            {
                throw new RpcMethodNotSupportedException(methodName, resultValue.Value);
            }

            result = await InvokeRpcMethodAsync(structInstance, method, inParameters, outSpecifiers, retSpecifier, cancel).ConfigureAwait(continueOnCapturedContext: false);
        }

        return result;
    }

    //
    // Summary:
    //     invoke RPC method as an asynchronous operation.
    //
    // Parameters:
    //   symbol:
    //     The symbol.
    //
    //   rpcMethod:
    //     The RPC method.
    //
    //   inParameters:
    //     The in parameters.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     Task<ResultRpcMethod>.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    //   T:System.ArgumentNullException:
    //     symbol
    //
    //   T:System.ArgumentNullException:
    //     rpcMethod
    public Task<ResultRpcMethod> InvokeRpcMethodAsync(IRpcCallableInstance symbol, IRpcMethod rpcMethod, object[]? inParameters, CancellationToken cancel)
    {
        return InvokeRpcMethodAsync(symbol, rpcMethod, inParameters, null, null, cancel);
    }

    //
    // Summary:
    //     invoke RPC method as an asynchronous operation.
    //
    // Parameters:
    //   symbol:
    //     The symbol.
    //
    //   rpcMethod:
    //     The RPC method.
    //
    //   inParameters:
    //     The in parameters.
    //
    //   outSpec:
    //     The out spec.
    //
    //   returnSpec:
    //     The return spec.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Returns:
    //     Task<ResultRpcMethod>.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    //   T:System.ArgumentNullException:
    //     symbol
    //
    //   T:System.ArgumentNullException:
    //     rpcMethod
    public async Task<ResultRpcMethod> InvokeRpcMethodAsync(IRpcCallableInstance symbol, IRpcMethod rpcMethod, object[]? inParameters, AnyTypeSpecifier[]? outSpec, AnyTypeSpecifier? returnSpec, CancellationToken cancel)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (symbol == null)
        {
            throw new ArgumentNullException("symbol");
        }

        if (rpcMethod == null)
        {
            throw new ArgumentNullException("rpcMethod");
        }

        ResultValue<ISymbolCache> resultValue = await ((IAdsSymbolCacheProvider)this).GetSymbolCacheAsync(cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (resultValue.Succeeded)
        {
            return await resultValue.Value.InvokeRpcMethodAsync(symbol, rpcMethod, inParameters, outSpec, returnSpec, cancel).ConfigureAwait(continueOnCapturedContext: false);
        }

        return new ResultRpcMethod(resultValue.ErrorCode, null, null, resultValue.InvokeId);
    }

    //
    // Summary:
    //     Tries the invoke RPC method.
    //
    // Parameters:
    //   symbol:
    //     The symbol.
    //
    //   rpcMethod:
    //     The RPC method.
    //
    //   inParameters:
    //     The in parameters.
    //
    //   outSpec:
    //     The out spec.
    //
    //   returnSpec:
    //     The return spec.
    //
    //   outParameters:
    //     The out parameters.
    //
    //   returnValue:
    //     The return value.
    //
    // Returns:
    //     AdsErrorCode.
    //
    // Exceptions:
    //   T:System.ObjectDisposedException:
    //
    //   T:TwinCAT.ClientNotConnectedException:
    //
    //   T:System.ArgumentNullException:
    //     symbol
    //
    //   T:System.ArgumentNullException:
    //     rpcMethod
    //
    // Remarks:
    //     The RpcMethod optionally support In-Parameters, Out-Parameters and Return values.
    //     Therefore the parameters inParameters, outParameters, are allowed to be empty
    //     or NULL.
    public AdsErrorCode TryInvokeRpcMethod(IRpcCallableInstance symbol, IRpcMethod rpcMethod, object[]? inParameters, AnyTypeSpecifier[]? outSpec, AnyTypeSpecifier? returnSpec, out object[]? outParameters, out object? returnValue)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(Name);
        }

        if (!IsConnected)
        {
            throw new ClientNotConnectedException(this);
        }

        if (symbol == null)
        {
            throw new ArgumentNullException("symbol");
        }

        if (rpcMethod == null)
        {
            throw new ArgumentNullException("rpcMethod");
        }

        ISymbolCache table = null;
        AdsErrorCode adsErrorCode = ((IAdsSymbolCacheProvider)this).TryGetSymbolCache(out table);
        outParameters = null;
        returnValue = null;
        if (adsErrorCode.Succeeded())
        {
            adsErrorCode = table.TryInvokeRpcMethod(symbol, rpcMethod, inParameters, outSpec, returnSpec, out outParameters, out returnValue);
        }

        return adsErrorCode;
    }

    //
    // Summary:
    //     Reads the value of a symbol and returns it as an typed object.
    //
    // Parameters:
    //   symbol:
    //     The symbol that should be read.
    //
    // Type parameters:
    //   T:
    //     The value type.
    //
    // Returns:
    //     The value of the symbol.
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public T ReadValue<T>(ISymbol symbol) where T : notnull
    {
        return PrimitiveTypeMarshaler.Convert<T>(ReadValue(symbol));
    }

    //
    // Summary:
    //     Reads the value of a symbol and returns the value as typed value.
    //
    // Parameters:
    //   symbol:
    //     The symbol that should be read.
    //
    //   value:
    //     The value.
    //
    // Type parameters:
    //   T:
    //     The value type.
    //
    // Returns:
    //     The ADS Error Code
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public AdsErrorCode TryReadValue<T>(ISymbol symbol, [System.Diagnostics.CodeAnalysis.AllowNull] out T? value)
    {
        object value2 = null;
        AdsErrorCode num = TryReadValue(symbol, out value2);
        if (num.Succeeded())
        {
            value = PrimitiveTypeMarshaler.Convert<T>(value2);
            return num;
        }

        value = default(T);
        return num;
    }

    //
    // Summary:
    //     Read value as an asynchronous operation.
    //
    // Parameters:
    //   symbol:
    //     The symbol.
    //
    //   cancel:
    //     The cancellation token that can be used by other objects or threads to receive
    //     notice of cancellation.
    //
    // Type parameters:
    //   TValue:
    //     The type of the t value.
    //
    // Returns:
    //     A Task<ResultValue`1> representing the asynchronous operation.
    public async Task<ResultValue<TValue>> ReadValueAsync<TValue>(ISymbol symbol, CancellationToken cancel)
    {
        ResultAnyValue resultAnyValue = await ReadValueAsync(symbol, cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (resultAnyValue.Succeeded)
        {
            return ResultValue<TValue>.CreateSuccess(PrimitiveTypeMarshaler.Convert<TValue>(resultAnyValue.Value));
        }

        return ResultValue<TValue>.CreateError(resultAnyValue.ErrorCode);
    }

    //
    // Summary:
    //     Reads the value.
    //
    // Parameters:
    //   name:
    //     The name.
    //
    // Type parameters:
    //   T:
    //
    // Returns:
    //     T.
    public T ReadValue<T>(string name) where T : notnull
    {
        TryReadValue<T>(name, out var value).ThrowOnError();
        return value;
    }

    //
    // Summary:
    //     Tries to reads the value of a symbol specified with instance path and returns
    //     the typed value.
    //
    // Parameters:
    //   name:
    //     The name.
    //
    //   value:
    //     The read value of the Symbol.
    //
    // Type parameters:
    //   T:
    //     The value type.
    //
    // Returns:
    //     The TwinCAT.Ads.AdsErrorCode.
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public AdsErrorCode TryReadValue<T>(string name, [System.Diagnostics.CodeAnalysis.AllowNull] out T value)
    {
        object value2;
        AdsErrorCode num = TryReadValue(name, typeof(T), out value2);
        if (num.Succeeded())
        {
            value = PrimitiveTypeMarshaler.Convert<T>(value2);
            return num;
        }

        value = default(T);
        return num;
    }

    //
    // Summary:
    //     Read value as an asynchronous operation.
    //
    // Parameters:
    //   instancePath:
    //     Name of the ADS symbol.
    //
    //   cancel:
    //     The cancel token.
    //
    // Type parameters:
    //   T:
    //     The value type.
    //
    // Returns:
    //     A Task<ResultValue`1> representing the asynchronous operation.
    //
    // Remarks:
    //     This method automatically marshals the read values to appropriate .NET objects
    //     if possible. The overall behaviour is described here in the interface description.
    public async Task<ResultValue<T>> ReadValueAsync<T>(string instancePath, CancellationToken cancel)
    {
        ResultAnyValue resultAnyValue = await ReadValueAsync(instancePath, typeof(T), cancel).ConfigureAwait(continueOnCapturedContext: false);
        if (resultAnyValue.Succeeded)
        {
            return ResultValue<T>.CreateSuccess((T)resultAnyValue.Value);
        }

        return ResultValue<T>.CreateError(resultAnyValue.ErrorCode);
    }

    //
    // Summary:
    //     Writes a (typed) value to the symbol.
    //
    // Parameters:
    //   symbol:
    //     The symbol the value is written to.
    //
    //   val:
    //     The value.
    //
    // Type parameters:
    //   T:
    //     The value type.
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public void WriteValue<T>(ISymbol symbol, T val) where T : notnull
    {
        WriteValue(symbol, (object)val);
    }

    //
    // Summary:
    //     Tries to write a value to the symbol.
    //
    // Parameters:
    //   symbol:
    //     The symbol the value is written to.
    //
    //   val:
    //     The value.
    //
    // Type parameters:
    //   T:
    //     The value type.
    //
    // Returns:
    //     AdsErrorCode.
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public AdsErrorCode TryWriteValue<T>(ISymbol symbol, [System.Diagnostics.CodeAnalysis.DisallowNull] T val) where T : notnull
    {
        return TryWriteValue(symbol, (object?)val);
    }

    //
    // Summary:
    //     Writes a (typed) value to the symbol as an asynchronous operation.
    //
    // Parameters:
    //   symbol:
    //     The symbol the value is written to.
    //
    //   value:
    //     The value to write.
    //
    //   cancel:
    //     The cancellation token.
    //
    // Type parameters:
    //   T:
    //     The value type.
    //
    // Returns:
    //     A task that represents the asynchronous 'WriteSymbol' operation. The TwinCAT.Ads.ResultWrite
    //     parameter contains the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public Task<ResultWrite> WriteValueAsync<T>(ISymbol symbol, [System.Diagnostics.CodeAnalysis.DisallowNull] T value, CancellationToken cancel) where T : notnull
    {
        return WriteValueAsync(symbol, (object)value, cancel);
    }

    //
    // Summary:
    //     Writes a typed value to the symbol instance specified by its instance/symbol
    //     path.
    //
    // Parameters:
    //   name:
    //     The name.
    //
    //   value:
    //     Object holding the value to be written to the ADS symbol
    //
    // Type parameters:
    //   T:
    //     the value type.
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public void WriteValue<T>(string name, [System.Diagnostics.CodeAnalysis.DisallowNull] T value) where T : notnull
    {
        WriteValue(name, (object)value);
    }

    //
    // Summary:
    //     Tries to Write a (typed) value to the symbol instance specified by its instance/symbol
    //     path.
    //
    // Parameters:
    //   name:
    //     The name.
    //
    //   value:
    //     Object holding the value to be written to the ADS symbol
    //
    // Type parameters:
    //   T:
    //     The value type.
    //
    // Returns:
    //     AdsErrorCode.
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public AdsErrorCode TryWriteValue<T>(string name, [System.Diagnostics.CodeAnalysis.DisallowNull] T value) where T : notnull
    {
        return TryWriteValue(name, (object)value);
    }

    //
    // Summary:
    //     Writes a (typed) value to the symbol instance specified by its instance/symbol
    //     path as an asynchronous operation.
    //
    // Parameters:
    //   symbolPath:
    //     Name of the ADS symbol.
    //
    //   value:
    //     Object holding the value to be written to the ADS symbol
    //
    //   cancel:
    //     The cancel token.
    //
    // Type parameters:
    //   T:
    //     The value type.
    //
    // Returns:
    //     A task that represents the asynchronous 'WriteSymbol' operation. The TwinCAT.Ads.ResultWrite
    //     parameter contains the TwinCAT.Ads.ResultAds.ErrorCode after execution.
    //
    // Remarks:
    //     This method automatically marshals the value from .NET objects into the TwinCAT
    //     representation if possible. The overall behaviour is described here in the interface
    //     description.
    public Task<ResultWrite> WriteValueAsync<T>(string symbolPath, [System.Diagnostics.CodeAnalysis.DisallowNull] T value, CancellationToken cancel) where T : notnull
    {
        return WriteSymbolAsync(symbolPath, value, cancel);
    }
}