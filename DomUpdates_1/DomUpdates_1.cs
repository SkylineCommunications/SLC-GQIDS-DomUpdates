using System.Linq;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;
using System;
using System.IO;
using Skyline.DataMiner.Net;
using Skyline.DataMiner.Net.Exceptions;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.SubscriptionFilters;
using Skyline.DataMiner.Net.Messages.SLDataGateway;
using Skyline.DataMiner.Net.Sections;
using Skyline.DataMiner.Net.LogHelpers;
using SLDataGateway.API.Types.Results.Paging;

internal static class ConnectionHelper
{
    private const string APPLICATION_NAME = "GQI Ad hoc data source";

    internal static Connection CreateConnection(GQIDMS dms)
    {
        if (dms == null) throw new ArgumentNullException(nameof(dms));

        var attributes = ConnectionAttributes.AllowMessageThrottling;
        try
        {
            var connection = ConnectionSettings.GetConnection("localhost", attributes);
            connection.ClientApplicationName = APPLICATION_NAME;
            connection.AuthenticateUsingTicket(RequestCloneTicket(dms));
            connection.SubscribePiggyBacked(null, null);
            return connection;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to setup a connection with the DataMiner Agent: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Requests a one time ticket that can be used to authenticate another connection.
    /// <see cref="AuthenticateUsingTicket(string)"/>
    /// </summary>
    /// <returns></returns>
    private static string RequestCloneTicket(GQIDMS dms)
    {
        RequestTicketMessage requestInfo = new RequestTicketMessage(TicketType.Authentication, ExportConfig());
        TicketResponseMessage ticketInfo = dms.SendMessage(requestInfo) as TicketResponseMessage;
        if (ticketInfo == null)
            throw new DataMinerException("Did not receive ticket.");

        return ticketInfo.Ticket;
    }

    /// <summary>
    /// Exports the clientside configuration for polling, zipping etc. Does not include
    /// connection uris and the like.
    /// </summary>
    /// <returns></returns>
    private static byte[] ExportConfig()
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write((int)1); // version
                bw.Write(1000); // ms PollingInterval
                bw.Write(100); // ms PollingIntervalFast
                bw.Write(1000); // StackOverflowSize
                bw.Write(5000); // ms ConnectionCheckingInterval
                bw.Write(10); // MaxSimultaneousCalls

                ConnectionAttributes attributesToAdd = ConnectionAttributes.AllowMessageThrottling;
                bw.Write((int)attributesToAdd);

                bw.Write("r"); // connection is remoting or IPC (which inherits from remoting)
                bw.Write((int)1); // version
                bw.Write(30); // s PollingFallbackTime
            }

            return ms.ToArray();
        }
    }
}

internal class DOMWatcher : IDisposable
{
    private Connection _connection = null;

    internal DOMWatcher(string module, GQIDMS dms)
    {
        _connection = ConnectionHelper.CreateConnection(dms);
        if (_connection == null)
            throw new GenIfException("Could not create a connection.");

        var subscriptionFilter = new ModuleEventSubscriptionFilter<DomInstancesChangedEventMessage>(module);
        _connection.OnNewMessage += Connection_OnNewMessage;
        _connection.AddSubscription("testink", subscriptionFilter);
        _connection.SubscribePiggyBacked(null, null);
        // DOMIncidentDataSource.Log("DOMWatcher subscribed.");
    }

    private void Connection_OnNewMessage(object sender, NewMessageEventArgs e)
    {
        // DOMIncidentDataSource.Log("Connection_OnNewMessage.");
        if (e.Message is DomInstancesChangedEventMessage domChange)
            OnChanged?.Invoke(this, domChange);
    }

    internal event EventHandler<DomInstancesChangedEventMessage> OnChanged;

    public void Dispose()
    {
        _connection.Unsubscribe();
        _connection?.Close();
    }
}

[GQIMetaData(Name = "Incidents")]
public class DOMIncidentDataSource : IGQIDataSource, IGQIOnInit, IGQIOnPrepareFetch, IGQIUpdateable
{
    private static SectionDefinitionID _sectionDefinitionID = new SectionDefinitionID(new Guid("cd2a71a1-bc75-4219-af29-05b3104cfc1f"));
    private static FieldDescriptorID _fieldDescriptorID = new FieldDescriptorID(new Guid("b98b6a28-48cb-448f-9ad9-7414fd498f22"));

    private static readonly GQIStringColumn _nameColumn = new GQIStringColumn("Name");
    private static readonly GQIIntColumn _impactColumn = new GQIIntColumn("Impact");

    private GQIDMS _dms;
    private DOMWatcher _watcher;
    private IGQIUpdater _updater;

    public OnInitOutputArgs OnInit(OnInitInputArgs args)
    {
        _dms = args.DMS;
        return new OnInitOutputArgs();
    }

    public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
    {
        _watcher = new DOMWatcher("incidents", _dms);
        _watcher.OnChanged += Watcher_OnChanged;
        return new OnPrepareFetchOutputArgs();
    }

    private void Watcher_OnChanged(object sender, DomInstancesChangedEventMessage e)
    {
        // Log("Watcher_OnChanged.");
        e.Updated.ForEach(x =>
        {
            _updater.UpdateRow(CreateRow(x));
        });
    }

    public GQIColumn[] GetColumns()
    {
        return new GQIColumn[]
        {
            _nameColumn,
            _impactColumn,
        };
    }

    public GQIPage GetNextPage(GetNextPageInputArgs args)
    {
        var helper = new DomHelper(_dms.SendMessages, "incidents");
        var instances = helper.DomInstances.Read(DomInstanceExposers.DomDefinitionId.Equal(new Guid("7bc4bc92-5da6-4a72-8a19-bbd34ed90a79")));

        var rows = instances.Select(x =>
        {
            return CreateRow(x);
        }).ToArray();

        return new GQIPage(rows.ToArray()) { HasNextPage = false };
    }

    public void StartUpdates(IGQIUpdater updater)
    {
        // Log("Start updates.");
        _updater = updater;
    }

    public void StopUpdates()
    {
        // Log("Stop updates.");
        _watcher.Dispose();
    }

    internal GQIRow CreateRow(DomInstance instance)
    {
        var domMetadata = new ObjectRefMetadata { Object = instance.ID };
        var rowMetadata = new GenIfRowMetadata(new[] { domMetadata });
        return new GQIRow(instance.ID.Id.ToString(), new GQICell[]
            {
                new GQICell() { Value = instance.Name },
                new GQICell() { Value = instance.GetFieldValue<int>(_sectionDefinitionID, _fieldDescriptorID)?.Value}
            })
        { Metadata = rowMetadata };
    }

    //public static void Log(string msg)
    //{
    //    // Append the log message to the existing file
    //    using (StreamWriter sw = File.AppendText(@""))
    //    {
    //        sw.WriteLine($"{DateTime.Now} - {msg}");
    //    }
    //}
}
