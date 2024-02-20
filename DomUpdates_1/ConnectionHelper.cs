namespace DomUpdates_1
{
	using System;
	using System.IO;

	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Exceptions;
	using Skyline.DataMiner.Net.Messages;

	internal static class ConnectionHelper
	{
		private const string APPLICATION_NAME = "GQI Ad hoc data source";

		internal static Connection CreateConnection(GQIDMS dms)
		{
			if (dms == null)
				throw new ArgumentNullException(nameof(dms));

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
		/// </summary>
		/// <returns>Ticket.</returns>
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
		/// <returns>Flags.</returns>
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

}
