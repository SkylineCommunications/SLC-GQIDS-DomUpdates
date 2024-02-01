using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;
using Skyline.DataMiner.Net.SubscriptionFilters;
using Skyline.DataMiner.Net;
using System;

namespace DomUpdates_1
{

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
            _connection.AddSubscription("1", subscriptionFilter);
            _connection.SubscribePiggyBacked(null, null);

            // DOMIncidentDataSource.Log("DOMWatcher subscribed.");
        }

        internal event EventHandler<DomInstancesChangedEventMessage> OnChanged;

        public void Dispose()
        {
            _connection.Unsubscribe();
            _connection?.Close();
        }

        private void Connection_OnNewMessage(object sender, NewMessageEventArgs e)
        {
            // DOMIncidentDataSource.Log("Connection_OnNewMessage.");
            if (e.Message is DomInstancesChangedEventMessage domChange)
                OnChanged?.Invoke(this, domChange);
        }
    }
}
