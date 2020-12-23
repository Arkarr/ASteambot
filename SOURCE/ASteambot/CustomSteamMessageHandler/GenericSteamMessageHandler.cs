using SteamKit2;
using SteamKit2.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.CustomSteamMessageHandler
{
	//Thanks to https://github.com/JustArchiNET/, so much new possibilities, I'll do some clean up.
	public class GenericSteamMessageHandler : ClientMsgHandler
	{
		internal const byte MaxGamesPlayedConcurrently = 32; // This is limit introduced by Steam Network

		private readonly SteamUnifiedMessages.UnifiedService<IChatRoom> UnifiedChatRoomService;
		private readonly SteamUnifiedMessages.UnifiedService<IClanChatRooms> UnifiedClanChatRoomsService;
		private readonly SteamUnifiedMessages.UnifiedService<IEcon> UnifiedEconService;
		private readonly SteamUnifiedMessages.UnifiedService<IFriendMessages> UnifiedFriendMessagesService;
		private readonly SteamUnifiedMessages.UnifiedService<IPlayer> UnifiedPlayerService;

		private readonly Bot bot;

		internal DateTime LastPacketReceived { get; private set; }

		internal GenericSteamMessageHandler(SteamUnifiedMessages steamUnifiedMessages, Bot bot)
		{
			if ((steamUnifiedMessages == null))
			{
				throw new ArgumentNullException(nameof(steamUnifiedMessages));
			}

			this.bot = bot;

			UnifiedChatRoomService = steamUnifiedMessages.CreateService<IChatRoom>();
			UnifiedClanChatRoomsService = steamUnifiedMessages.CreateService<IClanChatRooms>();
			UnifiedEconService = steamUnifiedMessages.CreateService<IEcon>();
			UnifiedFriendMessagesService = steamUnifiedMessages.CreateService<IFriendMessages>();
			UnifiedPlayerService = steamUnifiedMessages.CreateService<IPlayer>();
		}

		public override void HandleMsg(IPacketMsg packetMsg)
		{
			if ((packetMsg == null) || (Client == null))
			{
				throw new ArgumentNullException(nameof(packetMsg) + " || " + nameof(Client));
			}

			LastPacketReceived = DateTime.UtcNow;

			switch (packetMsg.MsgType)
			{
				case EMsg.ClientCommentNotifications:
					ClientMsgProtobuf<CMsgClientCommentNotifications> commentNotifications = new ClientMsgProtobuf<CMsgClientCommentNotifications>(packetMsg);
					Client.PostCallback(new UserNotificationsCallback(packetMsg.TargetJobID, commentNotifications.Body));

					break;
				case EMsg.ClientItemAnnouncements:
					ClientMsgProtobuf<CMsgClientItemAnnouncements> itemAnnouncements = new ClientMsgProtobuf<CMsgClientItemAnnouncements>(packetMsg);
					Client.PostCallback(new UserNotificationsCallback(packetMsg.TargetJobID, itemAnnouncements.Body));

					break;
				case EMsg.ClientPlayingSessionState:
					ClientMsgProtobuf<CMsgClientPlayingSessionState> playingSessionState = new ClientMsgProtobuf<CMsgClientPlayingSessionState>(packetMsg);
					Client.PostCallback(new PlayingSessionStateCallback(packetMsg.TargetJobID, playingSessionState.Body));

					break;
				case EMsg.ClientPurchaseResponse:
					ClientMsgProtobuf<CMsgClientPurchaseResponse> purchaseResponse = new ClientMsgProtobuf<CMsgClientPurchaseResponse>(packetMsg);
					Client.PostCallback(new PurchaseResponseCallback(packetMsg.TargetJobID, purchaseResponse.Body));

					break;
				case EMsg.ClientRedeemGuestPassResponse:
					ClientMsgProtobuf<CMsgClientRedeemGuestPassResponse> redeemGuestPassResponse = new ClientMsgProtobuf<CMsgClientRedeemGuestPassResponse>(packetMsg);
					Client.PostCallback(new RedeemGuestPassResponseCallback(packetMsg.TargetJobID, redeemGuestPassResponse.Body));

					break;
				case EMsg.ClientSharedLibraryLockStatus:
					ClientMsgProtobuf<CMsgClientSharedLibraryLockStatus> sharedLibraryLockStatus = new ClientMsgProtobuf<CMsgClientSharedLibraryLockStatus>(packetMsg);
					Client.PostCallback(new SharedLibraryLockStatusCallback(packetMsg.TargetJobID, sharedLibraryLockStatus.Body));

					break;
				case EMsg.ClientUserNotifications:
					ClientMsgProtobuf<CMsgClientUserNotifications> userNotifications = new ClientMsgProtobuf<CMsgClientUserNotifications>(packetMsg);
					Client.PostCallback(new UserNotificationsCallback(packetMsg.TargetJobID, userNotifications.Body));

					break;
				case EMsg.ClientVanityURLChangedNotification:
					ClientMsgProtobuf<CMsgClientVanityURLChangedNotification> vanityURLChangedNotification = new ClientMsgProtobuf<CMsgClientVanityURLChangedNotification>(packetMsg);
					Client.PostCallback(new VanityURLChangedCallback(packetMsg.TargetJobID, vanityURLChangedNotification.Body));

					break;

				case EMsg.EconTrading_InitiateTradeResult:
					ClientMsgProtobuf<CMsgTrading_InitiateTradeResponse> initateTradeResponse = new ClientMsgProtobuf<CMsgTrading_InitiateTradeResponse>(packetMsg);
					Client.PostCallback(new InitateTradeResponseCallback(packetMsg.TargetJobID, initateTradeResponse.Body));
					break;

				case EMsg.ClientUDSInviteToGame:
					HandleGameInviteMsg(packetMsg);
					break;

				default: return;
			}
		}

		internal void AckChatMessage(ulong chatGroupID, ulong chatID, uint timestamp)
		{
			if ((chatGroupID == 0) || (chatID == 0) || (timestamp == 0))
			{
				throw new ArgumentNullException(nameof(chatGroupID) + " || " + nameof(chatID) + " || " + nameof(timestamp));
			}

			if (!Client.IsConnected)
			{
				return;
			}

			CChatRoom_AckChatMessage_Notification request = new CChatRoom_AckChatMessage_Notification
			{
				chat_group_id = chatGroupID,
				chat_id = chatID,
				timestamp = timestamp
			};

			UnifiedChatRoomService.SendMessage(x => x.AckChatMessage(request), true);
		}

		internal void AckMessage(ulong steamID, uint timestamp)
		{
			if ((steamID == 0) || !new SteamID(steamID).IsIndividualAccount || (timestamp == 0))
			{
				throw new ArgumentNullException(nameof(steamID) + " || " + nameof(timestamp));
			}

			if (!Client.IsConnected)
			{
				return;
			}

			CFriendMessages_AckMessage_Notification request = new CFriendMessages_AckMessage_Notification
			{
				steamid_partner = steamID,
				timestamp = timestamp
			};

			UnifiedFriendMessagesService.SendMessage(x => x.AckMessage(request), true);
		}

		internal void AcknowledgeClanInvite(ulong steamID, bool acceptInvite)
		{
			if ((steamID == 0) || !new SteamID(steamID).IsClanAccount)
			{
				throw new ArgumentNullException(nameof(steamID));
			}

			if (!Client.IsConnected)
			{
				return;
			}

			ClientMsg<CMsgClientAcknowledgeClanInvite> request = new ClientMsg<CMsgClientAcknowledgeClanInvite>
			{
				Body = {
					ClanID = steamID,
					AcceptInvite = acceptInvite
				}
			};

			Client.Send(request);
		}

		internal async Task<bool> AddFriend(ulong steamID)
		{
			if ((steamID == 0) || !new SteamID(steamID).IsIndividualAccount)
			{
				throw new ArgumentNullException(nameof(steamID));
			}

			if (!Client.IsConnected)
			{
				return false;
			}

			CPlayer_AddFriend_Request request = new CPlayer_AddFriend_Request { steamid = steamID };

			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedPlayerService.SendMessage(x => x.AddFriend(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return false;
			}

			return response.Result == EResult.OK;
		}

		internal async Task<ulong> GetClanChatGroupID(ulong steamID)
		{
			if ((steamID == 0) || !new SteamID(steamID).IsClanAccount)
			{
				throw new ArgumentNullException(nameof(steamID));
			}

			if (!Client.IsConnected)
			{
				return 0;
			}

			CClanChatRooms_GetClanChatRoomInfo_Request request = new CClanChatRooms_GetClanChatRoomInfo_Request
			{
				autocreate = true,
				steamid = steamID
			};

			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedClanChatRoomsService.SendMessage(x => x.GetClanChatRoomInfo(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return 0;
			}

			if (response.Result != EResult.OK)
			{
				return 0;
			}

			CClanChatRooms_GetClanChatRoomInfo_Response body = response.GetDeserializedResponse<CClanChatRooms_GetClanChatRoomInfo_Response>();

			return body.chat_group_summary.chat_group_id;
		}

		internal async Task<uint?> GetLevel()
		{
			if (!Client.IsConnected)
			{
				return null;
			}

			CPlayer_GetGameBadgeLevels_Request request = new CPlayer_GetGameBadgeLevels_Request();
			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedPlayerService.SendMessage(x => x.GetGameBadgeLevels(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return null;
			}

			if (response.Result != EResult.OK)
			{
				return null;
			}

			CPlayer_GetGameBadgeLevels_Response body = response.GetDeserializedResponse<CPlayer_GetGameBadgeLevels_Response>();

			return body.player_level;
		}

		internal async Task<List<CChatRoomSummaryPair>?> GetMyChatGroups()
		{
			if (!Client.IsConnected)
			{
				return null;
			}

			CChatRoom_GetMyChatRoomGroups_Request request = new CChatRoom_GetMyChatRoomGroups_Request();

			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedChatRoomService.SendMessage(x => x.GetMyChatRoomGroups(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return null;
			}

			if (response.Result != EResult.OK)
			{
				return null;
			}

			CChatRoom_GetMyChatRoomGroups_Response body = response.GetDeserializedResponse<CChatRoom_GetMyChatRoomGroups_Response>();

			return body.chat_room_groups;
		}

		internal async Task<CPrivacySettings?> GetPrivacySettings()
		{
			if (!Client.IsConnected)
			{
				return null;
			}

			CPlayer_GetPrivacySettings_Request request = new CPlayer_GetPrivacySettings_Request();

			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedPlayerService.SendMessage(x => x.GetPrivacySettings(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return null;
			}

			if (response.Result != EResult.OK)
			{
				return null;
			}

			CPlayer_GetPrivacySettings_Response body = response.GetDeserializedResponse<CPlayer_GetPrivacySettings_Response>();

			return body.privacy_settings;
		}

		internal async Task<string?> GetTradeToken()
		{
			if (!Client.IsConnected)
			{
				return null;
			}

			CEcon_GetTradeOfferAccessToken_Request request = new CEcon_GetTradeOfferAccessToken_Request();

			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedEconService.SendMessage(x => x.GetTradeOfferAccessToken(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return null;
			}

			if (response.Result != EResult.OK)
			{
				return null;
			}

			CEcon_GetTradeOfferAccessToken_Response body = response.GetDeserializedResponse<CEcon_GetTradeOfferAccessToken_Response>();

			return body.trade_offer_access_token;
		}

		internal async Task<bool> JoinChatRoomGroup(ulong chatGroupID)
		{
			if (chatGroupID == 0)
			{
				throw new ArgumentNullException(nameof(chatGroupID));
			}

			if (!Client.IsConnected)
			{
				return false;
			}

			CChatRoom_JoinChatRoomGroup_Request request = new CChatRoom_JoinChatRoomGroup_Request { chat_group_id = chatGroupID };

			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedChatRoomService.SendMessage(x => x.JoinChatRoomGroup(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return false;
			}

			return response.Result == EResult.OK;
		}

		internal async Task PlayGames(IEnumerable<uint> gameIDs, string? gameName = null)
		{
			if (gameIDs == null)
			{
				throw new ArgumentNullException(nameof(gameIDs));
			}

			if (!Client.IsConnected)
			{
				return;
			}

			ClientMsgProtobuf<CMsgClientGamesPlayed> request = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayedWithDataBlob)
			{
				Body =
				{
					client_os_type = 10 //(uint) EOSType.Win7
				}
			};

			byte maxGamesCount = MaxGamesPlayedConcurrently;

			if (!string.IsNullOrEmpty(gameName))
			{
				// If we have custom name to display, we must workaround the Steam network broken behaviour and send request on clean non-playing session
				// This ensures that custom name will in fact display properly
				Client.Send(request);
				await Task.Delay(500).ConfigureAwait(false);

				request.Body.games_played.Add(
					new CMsgClientGamesPlayed.GamePlayed
					{
						game_extra_info = gameName,
						game_id = new GameID
						{
							AppType = GameID.GameType.Shortcut,
							ModID = uint.MaxValue
						}
					}
				);

				// Max games count is affected by valid AppIDs only, therefore gameName alone doesn't need exclusive slot
				maxGamesCount++;
			}

			foreach (uint gameID in gameIDs.Where(gameID => gameID != 0))
			{
				request.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed { game_id = new GameID(gameID) });

				if (request.Body.games_played.Count >= maxGamesCount)
				{
					break;
				}
			}

			Client.Send(request);
		}

		internal async Task<RedeemGuestPassResponseCallback?> RedeemGuestPass(ulong guestPassID)
		{
			if (guestPassID == 0)
			{
				throw new ArgumentNullException(nameof(guestPassID));
			}

			if (!Client.IsConnected)
			{
				return null;
			}

			ClientMsgProtobuf<CMsgClientRedeemGuestPass> request = new ClientMsgProtobuf<CMsgClientRedeemGuestPass>(EMsg.ClientRedeemGuestPass)
			{
				SourceJobID = Client.GetNextJobID(),
				Body = { guest_pass_id = guestPassID }
			};

			Client.Send(request);

			try
			{
				return await new AsyncJob<RedeemGuestPassResponseCallback>(Client, request.SourceJobID).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return null;
			}
		}

		internal async Task<PurchaseResponseCallback?> RedeemKey(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				throw new ArgumentNullException(nameof(key));
			}

			if (!Client.IsConnected)
			{
				return null;
			}

			ClientMsgProtobuf<CMsgClientRegisterKey> request = new ClientMsgProtobuf<CMsgClientRegisterKey>(EMsg.ClientRegisterKey)
			{
				SourceJobID = Client.GetNextJobID(),
				Body = { key = key }
			};

			Client.Send(request);

			try
			{
				return await new AsyncJob<PurchaseResponseCallback>(Client, request.SourceJobID).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return null;
			}
		}

		internal async Task<bool> RemoveFriend(ulong steamID)
		{
			if ((steamID == 0) || !new SteamID(steamID).IsIndividualAccount)
			{
				throw new ArgumentNullException(nameof(steamID));
			}

			if (!Client.IsConnected)
			{
				return false;
			}

			CPlayer_RemoveFriend_Request request = new CPlayer_RemoveFriend_Request { steamid = steamID };

			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedPlayerService.SendMessage(x => x.RemoveFriend(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return false;
			}

			return response.Result == EResult.OK;
		}

		internal void RequestItemAnnouncements()
		{
			if (!Client.IsConnected)
			{
				return;
			}

			ClientMsgProtobuf<CMsgClientRequestItemAnnouncements> request = new ClientMsgProtobuf<CMsgClientRequestItemAnnouncements>(EMsg.ClientRequestItemAnnouncements);
			Client.Send(request);
		}

		internal async Task<EResult> SendMessage(ulong steamID, string message)
		{
			if ((steamID == 0) || !new SteamID(steamID).IsIndividualAccount || string.IsNullOrEmpty(message))
			{
				throw new ArgumentNullException(nameof(steamID) + " || " + nameof(message));
			}

			if (!Client.IsConnected)
			{
				return EResult.NoConnection;
			}

			CFriendMessages_SendMessage_Request request = new CFriendMessages_SendMessage_Request
			{
				chat_entry_type = (int)EChatEntryType.ChatMsg,
				contains_bbcode = true,
				message = message,
				steamid = steamID
			};

			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedFriendMessagesService.SendMessage(x => x.SendMessage(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return EResult.Timeout;
			}

			return response.Result;
		}

		internal async Task<EResult> SendMessage(ulong chatGroupID, ulong chatID, string message)
		{
			if ((chatGroupID == 0) || (chatID == 0) || string.IsNullOrEmpty(message))
			{
				throw new ArgumentNullException(nameof(chatGroupID) + " || " + nameof(chatID) + " || " + nameof(message));
			}

			if (!Client.IsConnected)
			{
				return EResult.NoConnection;
			}

			CChatRoom_SendChatMessage_Request request = new CChatRoom_SendChatMessage_Request
			{
				chat_group_id = chatGroupID,
				chat_id = chatID,
				message = message
			};

			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedChatRoomService.SendMessage(x => x.SendChatMessage(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return EResult.Timeout;
			}

			return response.Result;
		}

		internal async Task<EResult> SendTypingStatus(ulong steamID)
		{
			if ((steamID == 0) || !new SteamID(steamID).IsIndividualAccount)
			{
				throw new ArgumentNullException(nameof(steamID));
			}

			if (!Client.IsConnected)
			{
				return EResult.NoConnection;
			}

			CFriendMessages_SendMessage_Request request = new CFriendMessages_SendMessage_Request
			{
				chat_entry_type = (int)EChatEntryType.Typing,
				steamid = steamID
			};

			SteamUnifiedMessages.ServiceMethodResponse response;

			try
			{
				response = await UnifiedFriendMessagesService.SendMessage(x => x.SendMessage(request)).ToLongRunningTask().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e);
				Console.ForegroundColor = ConsoleColor.White;

				return EResult.Timeout;
			}

			return response.Result;
		}

		internal void SetCurrentMode(uint chatMode)
		{
			/*if (chatMode == 0)
			{
				throw new ArgumentNullException(nameof(chatMode));
			}

			if (!Client.IsConnected)
			{
				return;
			}

			ClientMsgProtobuf<CMsgClientUIMode> request = new ClientMsgProtobuf<CMsgClientUIMode>(EMsg.ClientCurrentUIMode) { Body = { chat_mode = chatMode } };
			Client.Send(request);*/
		}

		public sealed class PurchaseResponseCallback : CallbackMsg
		{
			public readonly Dictionary<uint, string>? Items;

			public EPurchaseResultDetail PurchaseResultDetail { get; internal set; }
			public EResult Result { get; internal set; }

			internal PurchaseResponseCallback(EResult result, EPurchaseResultDetail purchaseResult)
			{
				if (!Enum.IsDefined(typeof(EResult), result) || !Enum.IsDefined(typeof(EPurchaseResultDetail), purchaseResult))
				{
					throw new ArgumentNullException(nameof(result) + " || " + nameof(purchaseResult));
				}

				Result = result;
				PurchaseResultDetail = purchaseResult;
			}

			internal PurchaseResponseCallback(JobID jobID, CMsgClientPurchaseResponse msg)
			{
				if ((jobID == null) || (msg == null))
				{
					throw new ArgumentNullException(nameof(jobID) + " || " + nameof(msg));
				}

				JobID = jobID;
				PurchaseResultDetail = (EPurchaseResultDetail)msg.purchase_result_details;
				Result = (EResult)msg.eresult;

				if (msg.purchase_receipt_info == null)
				{

					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine((nameof(msg.purchase_receipt_info)));
					Console.ForegroundColor = ConsoleColor.White;

					return;
				}

				KeyValue receiptInfo = new KeyValue();

				using (MemoryStream ms = new MemoryStream(msg.purchase_receipt_info))
				{
					if (!receiptInfo.TryReadAsBinary(ms))
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine((nameof(ms)));
						Console.ForegroundColor = ConsoleColor.White;

						return;
					}
				}

				List<KeyValue> lineItems = receiptInfo["lineitems"].Children;

				if (lineItems.Count == 0)
				{
					return;
				}

				Items = new Dictionary<uint, string>(lineItems.Count);

				foreach (KeyValue lineItem in lineItems)
				{
					uint packageID = lineItem["PackageID"].AsUnsignedInteger();

					if (packageID == 0)
					{
						// Coupons have PackageID of -1 (don't ask me why)
						// We'll use ItemAppID in this case
						packageID = lineItem["ItemAppID"].AsUnsignedInteger();

						if (packageID == 0)
						{
							Console.ForegroundColor = ConsoleColor.Red;
							Console.WriteLine((nameof(packageID)));
							Console.ForegroundColor = ConsoleColor.White;

							return;
						}
					}

					string? gameName = lineItem["ItemDescription"].AsString();

					if (string.IsNullOrEmpty(gameName))
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine((nameof(gameName)));
						Console.ForegroundColor = ConsoleColor.White;

						return;
					}

					// Apparently steam expects client to decode sent HTML
					gameName = WebUtility.HtmlDecode(gameName);
					Items[packageID] = gameName;
				}
			}
		}

		public sealed class UserNotificationsCallback : CallbackMsg
		{
			internal readonly Dictionary<EUserNotification, uint> Notifications;

			internal UserNotificationsCallback(JobID jobID, CMsgClientUserNotifications msg)
			{
				if ((jobID == null) || (msg == null))
				{
					throw new ArgumentNullException(nameof(jobID) + " || " + nameof(msg));
				}

				JobID = jobID;

				// We might get null body here, and that means there are no notifications related to trading
				// TODO: Check if this workaround is still needed
				Notifications = new Dictionary<EUserNotification, uint> { { EUserNotification.Trading, 0 } };

				if (msg.notifications == null)
				{
					return;
				}

				foreach (CMsgClientUserNotifications.Notification notification in msg.notifications)
				{
					EUserNotification type = (EUserNotification)notification.user_notification_type;

					switch (type)
					{
						case EUserNotification.AccountAlerts:
						case EUserNotification.Chat:
						case EUserNotification.Comments:
						case EUserNotification.GameTurns:
						case EUserNotification.Gifts:
						case EUserNotification.HelpRequestReplies:
						case EUserNotification.Invites:
						case EUserNotification.Items:
						case EUserNotification.ModeratorMessages:
						case EUserNotification.Trading:
							break;
						default:
							Console.ForegroundColor = ConsoleColor.Red;
							Console.WriteLine(string.Format(nameof(type)));
							Console.ForegroundColor = ConsoleColor.White;

							break;
					}

					Notifications[type] = notification.count;
				}
			}

			internal UserNotificationsCallback(JobID jobID, CMsgClientItemAnnouncements msg)
			{
				if ((jobID == null) || (msg == null))
				{
					throw new ArgumentNullException(nameof(jobID) + " || " + nameof(msg));
				}

				JobID = jobID;
				Notifications = new Dictionary<EUserNotification, uint>(1) { { EUserNotification.Items, msg.count_new_items } };
			}

			internal UserNotificationsCallback(JobID jobID, CMsgClientCommentNotifications msg)
			{
				if ((jobID == null) || (msg == null))
				{
					throw new ArgumentNullException(nameof(jobID) + " || " + nameof(msg));
				}

				JobID = jobID;
				Notifications = new Dictionary<EUserNotification, uint>(1) { { EUserNotification.Comments, msg.count_new_comments + msg.count_new_comments_owner + msg.count_new_comments_subscriptions } };
			}

			public enum EUserNotification : byte
			{
				Unknown,
				Trading = 1,
				GameTurns = 2,
				ModeratorMessages = 3,
				Comments = 4,
				Items = 5,
				Invites = 6,
				Gifts = 8,
				Chat = 9,
				HelpRequestReplies = 10,
				AccountAlerts = 11
			}
		}

		internal sealed class PlayingSessionStateCallback : CallbackMsg
		{
			internal readonly bool PlayingBlocked;

			internal PlayingSessionStateCallback(JobID jobID, CMsgClientPlayingSessionState msg)
			{
				if ((jobID == null) || (msg == null))
				{
					throw new ArgumentNullException(nameof(jobID) + " || " + nameof(msg));
				}

				JobID = jobID;
				PlayingBlocked = msg.playing_blocked;
			}
		}

		internal sealed class RedeemGuestPassResponseCallback : CallbackMsg
		{
			internal readonly EResult Result;

			internal RedeemGuestPassResponseCallback(JobID jobID, CMsgClientRedeemGuestPassResponse msg)
			{
				if ((jobID == null) || (msg == null))
				{
					throw new ArgumentNullException(nameof(jobID) + " || " + nameof(msg));
				}

				JobID = jobID;
				Result = (EResult)msg.eresult;
			}
		}

		internal sealed class SharedLibraryLockStatusCallback : CallbackMsg
		{
			internal readonly ulong LibraryLockedBySteamID;

			internal SharedLibraryLockStatusCallback(JobID jobID, CMsgClientSharedLibraryLockStatus msg)
			{
				if ((jobID == null) || (msg == null))
				{
					throw new ArgumentNullException(nameof(jobID) + " || " + nameof(msg));
				}

				JobID = jobID;

				if (msg.own_library_locked_by == 0)
				{
					return;
				}

				LibraryLockedBySteamID = new SteamID(msg.own_library_locked_by, EUniverse.Public, EAccountType.Individual);
			}
		}

		internal sealed class VanityURLChangedCallback : CallbackMsg
		{
			internal readonly string VanityURL;

			internal VanityURLChangedCallback(JobID jobID, CMsgClientVanityURLChangedNotification msg)
			{
				if ((jobID == null) || (msg == null))
				{
					throw new ArgumentNullException(nameof(jobID) + " || " + nameof(msg));
				}

				JobID = jobID;
				VanityURL = msg.vanity_url;
			}
		}

		internal enum EPrivacySetting : byte
		{
			Unknown,
			Private,
			FriendsOnly,
			Public
		}

		public void HandleGameInviteMsg(IPacketMsg packetMsg)
		{
			var chatMsg = new ClientMsgProtobuf<CMsgClientUDSInviteToGame>(packetMsg);
			//
			//steam_id_src = 76561198044361291
			// I got the message, now to create it is for later.
			int i = 0;
		}

		public void SendGameInvite(SteamID inviter, SteamID target)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}

			bot.PlayGames(440).ConfigureAwait(false);

			var chatMsg = new ClientMsgProtobuf<CMsgClientUDSInviteToGame>(EMsg.ClientUDSInviteToGame);

			chatMsg.Body.connect_string = "+tf_party_request_join_user " + 76561197991854757;
			chatMsg.Body.connect_stringSpecified = true;
			chatMsg.Body.steam_id_dest = target;//new SteamID(76561197991854757);
			chatMsg.Body.steam_id_destSpecified = true;
			chatMsg.Body.steam_id_src = inviter;
			chatMsg.Body.steam_id_srcSpecified = true;

			chatMsg.SteamID = target;

			this.Client.Send(chatMsg);
		}
	}

	internal class InitateTradeResponseCallback : CallbackMsg
	{
		private ulong targetJobID;
		private CMsgTrading_InitiateTradeResponse body;

		public InitateTradeResponseCallback(ulong targetJobID, CMsgTrading_InitiateTradeResponse body)
		{
			this.targetJobID = targetJobID;
			this.body = body;
		}
	}
}
