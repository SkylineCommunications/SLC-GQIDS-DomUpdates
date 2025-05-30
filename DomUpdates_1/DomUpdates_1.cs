using System;
using System.Linq;

using DomUpdates_1;

using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.Messages;
using Skyline.DataMiner.Net.Messages.SLDataGateway;
using Skyline.DataMiner.Net.Sections;


[GQIMetaData(Name = "Incidents")]
public class DOMIncidentDataSource : IGQIDataSource, IGQIOnInit, IGQIUpdateable
{
    private static readonly GQIStringColumn _nameColumn = new GQIStringColumn("Name");
    private static readonly GQIIntColumn _impactColumn = new GQIIntColumn("Impact");
    private static SectionDefinitionID _sectionDefinitionID = new SectionDefinitionID(new Guid("cd2a71a1-bc75-4219-af29-05b3104cfc1f"));
    private static FieldDescriptorID _fieldDescriptorID = new FieldDescriptorID(new Guid("b98b6a28-48cb-448f-9ad9-7414fd498f22"));

    private static FilterElement<DomInstance> _definitionFilter = DomInstanceExposers.DomDefinitionId.Equal(new Guid("7bc4bc92-5da6-4a72-8a19-bbd34ed90a79"));

    private GQIDMS _dms;
    private DOMWatcher _watcher;
    private IGQIUpdater _updater;

    public OnInitOutputArgs OnInit(OnInitInputArgs args)
    {
        _dms = args.DMS;
        return new OnInitOutputArgs();
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
        var instances = helper.DomInstances.Read(_definitionFilter);

        var rows = instances.Select(x =>
        {
            return CreateRow(x);
        }).ToArray();

        return new GQIPage(rows.ToArray()) { HasNextPage = false };
    }

    public void OnStartUpdates(IGQIUpdater updater)
    {
        // Log("Start updates.");
        _updater = updater;

        _watcher = new DOMWatcher("incidents", _definitionFilter, _dms.GetConnection());
        _watcher.OnChanged += Watcher_OnChanged;
    }

    public void OnStopUpdates()
    {
        // Log("Stop updates.");
        // No need to dispose the watcher as the underlying connection and its subscriptions are cleaned up automatically by GQI
        // whenever the end of the life cycle of this data source is reached.
    }

    internal GQIRow CreateRow(DomInstance instance)
    {
        var domMetadata = new ObjectRefMetadata { Object = instance.ID };
        var rowMetadata = new GenIfRowMetadata(new[] { domMetadata });
        return new GQIRow(instance.ID.Id.ToString(), new GQICell[]
        {
            new GQICell() { Value = instance.Name },
            new GQICell() { Value = instance.GetFieldValue<int>(_sectionDefinitionID, _fieldDescriptorID)?.Value},
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
