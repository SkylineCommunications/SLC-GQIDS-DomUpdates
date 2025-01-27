namespace DomUpdates_1
{
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.Messages;
    using Skyline.DataMiner.Net.SubscriptionFilters;
    using System;

    internal class DOMWatcher : IDisposable
    {
        private readonly IConnection _connection = null;

        internal DOMWatcher(string module, GQIDMS dms)
        {
            _connection = dms.GetConnection();

            var subscriptionFilter = new ModuleEventSubscriptionFilter<DomInstancesChangedEventMessage>(module);
            _connection.OnNewMessage += Connection_OnNewMessage;
            _connection.Subscribe(subscriptionFilter);

            // DOMIncidentDataSource.Log("DOMWatcher subscribed.");
        }

        internal event EventHandler<DomInstancesChangedEventMessage> OnChanged;

        public void Dispose()
        {
            _connection.Dispose();
        }

        private void Connection_OnNewMessage(object sender, NewMessageEventArgs e)
        {
            // DOMIncidentDataSource.Log("Connection_OnNewMessage.");
            if (e.Message is DomInstancesChangedEventMessage domChange)
                OnChanged?.Invoke(this, domChange);
        }
    }
}
