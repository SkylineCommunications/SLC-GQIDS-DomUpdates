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
using DomUpdates_1;


[GQIMetaData(Name = "Incidents")]
public class DOMIncidentDataSource : IGQIDataSource, IGQIOnInit, IGQIOnPrepareFetch, IGQIUpdateable
{
    private static readonly GQIStringColumn _nameColumn = new GQIStringColumn("Name");
    private static readonly GQIIntColumn _impactColumn = new GQIIntColumn("Impact");
    private static SectionDefinitionID _sectionDefinitionID = new SectionDefinitionID(new Guid("cd2a71a1-bc75-4219-af29-05b3104cfc1f"));
    private static FieldDescriptorID _fieldDescriptorID = new FieldDescriptorID(new Guid("b98b6a28-48cb-448f-9ad9-7414fd498f22"));

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
