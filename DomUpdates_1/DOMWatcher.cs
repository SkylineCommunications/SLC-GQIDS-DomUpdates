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
		private readonly object _lock = new object();
		private readonly IConnection _connection;

		private readonly string _subscriptionSetId;
		private readonly SubscriptionFilter[] _subscriptionFilters;

		private int _subscriberCount;

		internal DOMWatcher(string module, FilterElement<DomInstance> filter, IConnection connection)
		{
			if (String.IsNullOrWhiteSpace(module))
			{
				throw new ArgumentException($"'{nameof(module)}' cannot be null or whitespace.", nameof(module));
			}

			if (filter == null)
			{
				throw new ArgumentNullException(nameof(filter));
			}

			if (connection == null)
			{
				throw new ArgumentNullException(nameof(connection));
			}

			_connection = connection;

			_subscriptionSetId = $"DomInstanceSubscription_{nameof(DOMWatcher)}_{Guid.NewGuid()}";
			_subscriptionFilters = new SubscriptionFilter[]
			{
				new ModuleEventSubscriptionFilter<DomInstancesChangedEventMessage>(module),
				new SubscriptionFilter<DomInstancesChangedEventMessage, DomInstance>(filter),
			};
		}

		internal event EventHandler<DomInstancesChangedEventMessage> OnChanged
		{
			add
			{
				lock (_lock)
				{
					CheckAndSubscribe();
					Changed += value;
				}
			}

			remove
			{
				lock (_lock)
				{
					Changed -= value;
					CheckAndUnsubscribe();
				}
			}
		}

		private event EventHandler<DomInstancesChangedEventMessage> Changed;

		public void Dispose()
		{
			_connection.ClearSubscriptions(_subscriptionSetId);
			_connection.OnNewMessage -= Connection_OnNewMessage;
		}

		private void Connection_OnNewMessage(object sender, NewMessageEventArgs e)
		{
			if (!e.FromSet(_subscriptionSetId))
			{
				// Not for our subscription
				return;
			}

			if (e.Message is DomInstancesChangedEventMessage domChange)
			{
				Changed?.Invoke(this, domChange);
			}
		}

		private void CheckAndSubscribe()
		{
			if (_subscriberCount <= 0)
			{
				_connection.OnNewMessage += Connection_OnNewMessage;
				_connection.AddSubscription(_subscriptionSetId, _subscriptionFilters);
				_connection.Subscribe();
			}

			_subscriberCount++;
		}

		private void CheckAndUnsubscribe()
		{
			_subscriberCount--;

			if (_subscriberCount <= 0)
			{
				_connection.OnNewMessage -= Connection_OnNewMessage;
				_connection.ClearSubscriptions(_subscriptionSetId);
			}
		}
	}
}
