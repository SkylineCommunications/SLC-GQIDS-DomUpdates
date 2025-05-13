namespace DomUpdates_1
{
	using System;

	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.SubscriptionFilters;

	internal class DOMWatcher : IDisposable
	{
		private readonly IConnection _connection = null;

		internal DOMWatcher(string module, FilterElement<DomInstance> filter, GQIDMS dms)
		{
			if (String.IsNullOrWhiteSpace(module))
			{
				throw new ArgumentException($"'{nameof(module)}' cannot be null or whitespace.", nameof(module));
			}

			if (filter == null)
			{
				throw new ArgumentNullException(nameof(filter));
			}

			if (dms == null)
			{
				throw new ArgumentNullException(nameof(dms));
			}

			_connection = dms.GetConnection();

			var subscriptionFilters = new SubscriptionFilter[]
			{
				new ModuleEventSubscriptionFilter<DomInstancesChangedEventMessage>(module),
				new SubscriptionFilter<DomInstancesChangedEventMessage, DomInstance>(filter),
			};

			_connection.OnNewMessage += Connection_OnNewMessage;
			_connection.Subscribe(subscriptionFilters);

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
