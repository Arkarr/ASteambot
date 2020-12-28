using System;
using SteamAuth;
using System.IO;
using System.Linq;
using System.Threading;
using System.ComponentModel;
using System.Collections.Generic;
using System.Security.Cryptography;
using ASteambot.SteamGroups;
using ASteambot.Networking;
using System.Net;
using System.Globalization;
using System.Reflection;
using ASteambot.SteamMarketUtility;
using Newtonsoft.Json.Linq;
using SteamKit2;
using ASteambot.CustomSteamMessageHandler;
using SteamTrade.TradeOffer;
using SteamTrade;
using static SteamTrade.GenericInventory;
using System.Text;
using System.Threading.Tasks;
using ASteambot.SteamTrade;
using SteamKit2.GC;

namespace ASteambot
{
    public class Bot
    {
        private readonly DateTime NullDate = new DateTime(1970, 1, 1);

        private Database DB;
        private bool InTrade;
        private bool renaming;
        private string myUniqueId;
        private int maxfriendCount;
        private string myUserNonce;
        private LoginInfo loginInfo;
        public SteamUser steamUser;
        private TCPInterface socket;
        private List<string> finishedTO;
        private Thread tradeOfferThread;
        private CallbackManager cbManager;
        private BackgroundWorker botThread;
        private HandleMessage messageHandler;
        private List<SteamProfile> steamprofiles;
        private Dictionary<ulong, string> tradetokens;
        private SteamGuardAccount steamGuardAccount;

        public SteamGameCoordinator SteamGameCoordinator { get; private set; }
        public SteamClient SteamClient { get; private set; }
        public string Name { get; private set; }
        public SteamProfile.Infos SteamProfileInfo { get; private set; }
        public bool Running { get; private set; }
        public Config Config { get; private set; }
        public bool LoggedIn { get; private set; }
        public bool WebLoggedIn { get; private set; }
        public Manager Manager { get; private set; }
        public List<SteamID> Friends { get; private set; }
        public SteamFriends SteamFriends { get; private set; }
        public GenericSteamMessageHandler GSMH { get; private set; }
        public SteamWebCustom SteamWeb { get; private set; }
        public HandleSteamChat SteamchatHandler { get; private set; }
        public GenericInventory MyGenericInventory { get; private set; }
        public TradeOfferManager TradeOfferManager { get; private set; }
        public Dictionary<string, string> TradeoffersGS { get; private set; }
        public Dictionary<string, double> TradeOfferValue { get; private set; }
        public Dictionary<SteamID, int> ChatListener { get; private set; }
        public GenericInventory OtherGenericInventory { get; private set; }
        public int SteamInventoryItemCount { get; private set; }
        public Translation.Translation TranslationAdmins { get; private set; }
        public Translation.Translation TranslationPublic { get; private set; }
        public TradeManager TradeManager { get; private set; }

        private int steamInventoryTF2Items;
        public int SteamInventoryTF2Items
        {
            get
            {
                return steamInventoryTF2Items;
            }
            set
            {
                if(SteamInventoryItemCount != 0)
                    steamInventoryTF2Items = (value / SteamInventoryItemCount) * 100;
            }
        }

        private int steamInventoryCSGOItems;
        public int SteamInventoryCSGOItems
        {
            get
            {
                return steamInventoryCSGOItems;
            }
            set
            {
                if (SteamInventoryItemCount != 0)
                    steamInventoryCSGOItems = (value / SteamInventoryItemCount) * 100;
            }
        }

        private int steamInventoryDOTA2Items;
        public int SteamInventoryDOTA2Items
        {
            get
            {
                return steamInventoryDOTA2Items;
            }
            set
            {
                if (SteamInventoryItemCount != 0)
                    steamInventoryDOTA2Items = (value / SteamInventoryItemCount) * 100;
            }
        }

        private DateTime lastInventoryCheck;
        private DateTime lastTradeOfferCheck;

        private List<TradeOfferInfo> lastTradeInfos;
        public List<TradeOfferInfo> LastTradeInfos
        {
            get
            {
                TimeSpan ts = DateTime.Now.Subtract(lastTradeOfferCheck);
                if (ts.TotalMinutes >= 1)
                {
                    lastTradeInfos = new List<TradeOfferInfo>();
                    lastTradeOfferCheck = DateTime.Now;

                    string[] rows = { "tradeOfferID" };
                    List<Dictionary<string, string>> values = DB.SELECT(rows, "tradeoffers", "ORDER BY ID DESC LIMIT 4");
                    
                    for (int i = 0; i < values.Count; i++)
                    {
                        TradeOffer to;
                        if (TradeOfferManager.TryGetOffer(values[i][rows[0]], out to))
                        {
                            SteamProfile.Infos spi = GetSteamProfileInfo(to.PartnerSteamId);

                            lastTradeInfos.Add(new TradeOfferInfo(spi.CustomURL, spi.Name, spi.AvatarFull, to.TradeOfferId, to.OfferState));
                        }
                        else
                        {
                            LastTradeInfos.Add(new TradeOfferInfo(null, null, null, null, TradeOfferState.TradeOfferStateUnknown));
                        }
                    }
                }
                
                return lastTradeInfos;
            }
            private set
            {
                lastTradeInfos = value;
            }
        }

        private double inventoryValue;
        public double InventoryValue
        {
            get
            {
                TimeSpan ts = DateTime.Now.Subtract(lastInventoryCheck);
                if (ts.TotalMinutes >= 1)
                {
                    lastInventoryCheck = DateTime.Now;
                    InventoryValue = GetInventoryValue();
                }

                return inventoryValue;
            }
            private set
            {
                inventoryValue = value;
            }
        }

        public Bot(Manager manager, LoginInfo loginInfo, Config config, TCPInterface socket)
        {
            this.socket = socket;
            this.Config = config;
            this.loginInfo = loginInfo;
            Manager = manager;
            SteamClient = new SteamClient();
            SteamGameCoordinator = SteamClient.GetHandler<SteamGameCoordinator>();
            finishedTO = new List<string>();
            tradetokens = new Dictionary<ulong, string>();
            messageHandler = new HandleMessage();
            SteamWeb = new SteamWebCustom();
            steamprofiles = new List<SteamProfile>();
            cbManager = new CallbackManager(SteamClient);
            SteamchatHandler = new HandleSteamChat(this);
            ChatListener = new Dictionary<SteamID, int>();
            TradeoffersGS = new Dictionary<string, string>();
            TradeOfferValue = new Dictionary<string, double>();
            MyGenericInventory = new GenericInventory(SteamClient.SteamID, SteamWeb);
            OtherGenericInventory = new GenericInventory(null, SteamWeb);
            TradeManager = new TradeManager(config.SteamAPIKey, SteamWeb);

            SteamUnifiedMessages? steamUnifiedMessages = SteamClient.GetHandler<SteamUnifiedMessages>();
            GSMH = new GenericSteamMessageHandler(steamUnifiedMessages, this);

            SteamClient.AddHandler(GSMH);

            if (Program.IsLinux())
                Thread.Sleep(3000);

            DB = new Database(config.DatabaseServer, config.DatabaseUser, config.DatabasePassword, config.DatabaseName, config.DatabasePort);

            if(DB.IsConnected)
                DB.InitialiseDatabase();

            botThread = new BackgroundWorker { WorkerSupportsCancellation = true };
            botThread.DoWork += BW_HandleSteamTradeOffer;
            botThread.RunWorkerCompleted += BW_SteamTradeOfferScanned;

            socket.MessageReceived += Socket_MessageReceived;

            TranslationAdmins = new Translation.Translation();
            TranslationPublic = new Translation.Translation();

            TranslationAdmins.Load(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/configs/steamchattexts.xml");
            TranslationPublic.Load(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/configs/steamchattexts_public.xml");
        }
        
        public void SubscribeToEvents()
        {
            //Connection events :
            cbManager.Subscribe<SteamClient.ConnectedCallback>(OnSteambotConnected);
            cbManager.Subscribe<SteamClient.DisconnectedCallback>(OnSteambotDisconnected);
            cbManager.Subscribe<SteamUser.LoggedOnCallback>(OnSteambotLoggedIn);
            cbManager.Subscribe<SteamUser.LoggedOffCallback>(OnSteambotLoggedOff);
            cbManager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);
            cbManager.Subscribe<SteamUser.LoginKeyCallback>(LoginKey);
            cbManager.Subscribe<SteamUser.WebAPIUserNonceCallback>(OnWebAPIUserNonce);

            //Steam events :
            cbManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
            cbManager.Subscribe<SteamFriends.FriendMsgCallback>(OnFriendMsgCallback);
            cbManager.Subscribe<SteamFriends.PersonaChangeCallback>(OnSteamNameChange);
            cbManager.Subscribe<SteamFriends.FriendsListCallback>(OnSteamFriendsList);
            cbManager.Subscribe<SteamFriends.PersonaStateCallback>(OnProfileInfo);

            //Custom events:
            //cbManager.Subscribe<GenericSteamMessageHandler.OnSteamMessageReceived>(OnGenericMessageReceived);
        }

        //*************//
        //   NETWORK   //
        //*************//

        private void Socket_MessageReceived(object sender, EventArgGameServer e)
        {
            messageHandler.Execute(this, e.GetGameServerRequest);
        }

        //*****************//
        //   TRADE OFFER   //
        //*****************//

        private void BW_HandleSteamTradeOffer(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            while (!botThread.CancellationPending)
            {
                try
                {
                    if (TradeOfferManager != null)
                        TradeOfferManager.HandleNextPendingTradeOfferUpdate();

                    Thread.Sleep(1);
                }
                catch (WebException e)
                {
                    string data = String.Format("URI: {0} >> {1}", (e.Response != null && e.Response.ResponseUri != null ? e.Response.ResponseUri.ToString() : "unknown"), e.ToString());
                    Console.WriteLine(data);
                    Thread.Sleep(45000);//Steam is down, retry in 45 seconds.
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unhandled exception occurred in bot: " + e);
                }
            }
        }

        private void BW_SteamTradeOfferScanned(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            if (runWorkerCompletedEventArgs.Error != null)
            {
                Exception ex = runWorkerCompletedEventArgs.Error;

                Console.WriteLine("Unhandled exceptions in bot " + Name + " callback thread: " + Environment.NewLine + ex);

                Console.WriteLine("This bot died. Stopping it..");

                Disconnect();
            }
        }

        public void CreateTradeOffer(string otherSteamID)
        {
            List<long> contextId = new List<long>();
            contextId.Add(2);

            MyGenericInventory.load((int)Games.TF2, contextId, SteamClient.SteamID);

            SteamID partenar = new SteamID(otherSteamID);
            TradeOffer to = TradeOfferManager.NewOffer(partenar);

            GenericInventory.Item test = (GenericInventory.Item)MyGenericInventory.items.FirstOrDefault().Value;

            to.Items.AddMyItem(test.appid, test.contextid, (long)test.assetid);

            string offerId;
            to.Send(out offerId, "Test trade offer");

            Console.WriteLine("Offer ID : " + offerId);

            AcceptMobileTradeConfirmation(offerId);
        }

        public string GetUniqueID()
        {
            return myUniqueId;
        }

        public void AcceptMobileTradeConfirmation(string offerId)
        {
            try
            {
                steamGuardAccount.Session.SteamLogin = SteamWeb.Token;
                steamGuardAccount.Session.SteamLoginSecure = SteamWeb.TokenSecure;

                try
                {
                    foreach (var confirmation in steamGuardAccount.FetchConfirmations())
                    {
                        if (confirmation.ConfType == Confirmation.ConfirmationType.Trade)
                        {
                            long confID = steamGuardAccount.GetConfirmationTradeOfferID(confirmation);
                            if (confID == long.Parse(offerId) && steamGuardAccount.AcceptConfirmation(confirmation))
                                Console.WriteLine("Confirmed trade. (Confirmation ID #" + confirmation.ID + ")");
                        }
                    }
                }
                catch (SteamGuardAccount.WGTokenInvalidException)
                {
                    Console.WriteLine("Invalid session when trying to fetch trade confirmations.");
                }
            }
            catch(Exception e)
            {
                Program.PrintErrorMessage("Couldn't find steam auth data. Did you linked the bot steam account to steamguard with ASteambot ?");
                Program.PrintErrorMessage(e.ToString());
            }
        }

        //**************//
        //   LOGIN IN   //
        //**************//

        public void Auth()
        {
            Running = false;
            loginInfo.LoginFailCount = 0;
            steamUser = SteamClient.GetHandler<SteamUser>();
            SteamFriends = SteamClient.GetHandler<SteamFriends>();

            SubscribeToEvents();

            SteamClient.Connect();
        }

        private string GetMobileAuthCode()
        {
            var authFile = String.Format("auth/{0}.auth", loginInfo.Username);
            if (File.Exists(authFile))
            {
                steamGuardAccount = Newtonsoft.Json.JsonConvert.DeserializeObject<SteamGuardAccount>(File.ReadAllText(authFile));

                steamGuardAccount.RefreshSession();

                string code = steamGuardAccount.GenerateSteamGuardCode();
                return code;
            }
            else
            {
                Program.PrintErrorMessage("Failed to generate 2FA code. Make sure you have linked the authenticator via ASteambot or exported the auth files from your phone !");
                Program.PrintErrorMessage("Or you can try to input a code now, leave empty to quit : ");
                string code = Console.ReadLine();
                if (code.Equals(String.Empty))
                {
                    Console.WriteLine("Bot will stop now.");
                    Disconnect();

                    return string.Empty;
                }
                else
                {
                    return code;
                }
            }
        }

        public void CreateQuickTrade(SteamID steamid, uint gameID, int serverID, int moduleID, string args, List<ulong> itemsToGive)
        {
            if (InTrade)
            {
                Manager.Send(serverID, moduleID, NetworkCode.ASteambotCode.CreateQuickTrade, steamid.ConvertToUInt64().ToString()+"/BUSY");
            }
            else
            {
                PlayGames(gameID).ConfigureAwait(false);

                Trade CurrentTrade = TradeManager.CreateTrade(GetUniqueID(), SteamWeb.Token, SteamWeb.TokenSecure, steamUser.SteamID, steamid);
                TradeHandler th = new TradeHandler(CurrentTrade, this, steamid, SteamWeb, serverID, moduleID, args, (int)gameID, itemsToGive);
                SubscribeTrade(CurrentTrade, th);
                TradeManager.StartTradeThread(CurrentTrade);

                var tradeReq = new ClientGCMsg<MsgGCTrading_InitiateTradeRequest>();
                tradeReq.SourceJobID = SteamClient.GetNextJobID();
                tradeReq.Body.OtherClient = steamid;

                SteamGameCoordinator.Send(tradeReq, gameID);

                Manager.Send(serverID, moduleID, NetworkCode.ASteambotCode.CreateQuickTrade, steamid.ConvertToUInt64().ToString() + "/OK");
            }
        }

        private void SubscribeTrade(Trade trade, TradeHandler handler)
        {
            InTrade = true;

            trade.OnAwaitingConfirmation += handler.OnTradeAwaitingConfirmation;
            trade.OnClose += handler.OnTradeClose;
            trade.OnError += handler.OnTradeError;
            trade.OnStatusError += handler.OnStatusError;
            trade.OnAfterInit += handler.OnTradeInit;
            trade.OnUserAddItem += handler.OnTradeAddItem;
            trade.OnUserRemoveItem += handler.OnTradeRemoveItem;
            trade.OnMessage += handler.OnTradeMessageHandler;
            trade.OnUserSetReady += handler.OnTradeReadyHandler;
            trade.OnUserAccept += handler.OnTradeAcceptHandler;
        }

        public void UnsubscribeTrade(TradeHandler handler, Trade trade)
        {
            InTrade = false;

            trade.OnAwaitingConfirmation -= handler.OnTradeAwaitingConfirmation;
            trade.OnClose -= handler.OnTradeClose;
            trade.OnError -= handler.OnTradeError;
            trade.OnStatusError -= handler.OnStatusError;
            trade.OnAfterInit -= handler.OnTradeInit;
            trade.OnUserAddItem -= handler.OnTradeAddItem;
            trade.OnUserRemoveItem -= handler.OnTradeRemoveItem;
            trade.OnMessage -= handler.OnTradeMessageHandler;
            trade.OnUserSetReady -= handler.OnTradeReadyHandler;
            trade.OnUserAccept -= handler.OnTradeAcceptHandler;
        }

        public void DeactivateAuthenticator()
        {
            if (steamGuardAccount == null)
            {
                Console.WriteLine("Unable to unlink mobile authenticator, is it really linked ?");
            }
            else
            {
                steamGuardAccount.DeactivateAuthenticator();
                Console.WriteLine("Done !");
            }
        }

        public void GenerateCode()
        {
            Console.WriteLine(steamGuardAccount.GenerateSteamGuardCode());
        }

        public void LinkMobileAuth()
        {
            var login = new UserLogin(loginInfo.Username, loginInfo.Password);
            var loginResult = login.DoLogin();
            if (loginResult == LoginResult.NeedEmail)
            {
                while (loginResult == LoginResult.NeedEmail)
                {
                    Console.WriteLine("Enter Steam Guard code from email :");
                    var emailCode = Console.ReadLine();
                    login.EmailCode = emailCode;
                    loginResult = login.DoLogin();
                }
            }
            if (loginResult == LoginResult.LoginOkay)
            {
                Console.WriteLine("Linking mobile authenticator...");
                var authLinker = new AuthenticatorLinker(login.Session);
                var addAuthResult = authLinker.AddAuthenticator();
                if (addAuthResult == AuthenticatorLinker.LinkResult.MustProvidePhoneNumber)
                {
                    while (addAuthResult == AuthenticatorLinker.LinkResult.MustProvidePhoneNumber)
                    {
                        Console.WriteLine("Enter phone number with country code, e.g. +1XXXXXXXXXXX :");
                        var phoneNumber = Console.ReadLine();
                        authLinker.PhoneNumber = phoneNumber;
                        addAuthResult = authLinker.AddAuthenticator();
                    }
                }
                if (addAuthResult == AuthenticatorLinker.LinkResult.AwaitingFinalization)
                {
                    steamGuardAccount = authLinker.LinkedAccount;
                    try
                    {
                        var authFile = String.Format("auth/{0}.auth", loginInfo.Username); 
                        //Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "authfiles"));
                        File.WriteAllText(authFile, Newtonsoft.Json.JsonConvert.SerializeObject(steamGuardAccount));
                        Console.WriteLine("Enter SMS code :");
                        var smsCode = Console.ReadLine();
                        var authResult = authLinker.FinalizeAddAuthenticator(smsCode);
                        if (authResult == SteamAuth.AuthenticatorLinker.FinalizeResult.Success)
                        {
                            Console.WriteLine("Linked authenticator.");
                        }
                        else
                        {
                            Console.WriteLine("Error linking authenticator: " + authResult);
                        }
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Failed to save auth file. Aborting authentication.");
                    }
                }
                else
                {
                    Console.WriteLine("Error adding authenticator: " + addAuthResult);
                }
            }
            else
            {
                if (loginResult == SteamAuth.LoginResult.Need2FA)
                {
                    Console.WriteLine("Mobile authenticator has already been linked!");
                }
                else
                {
                    Console.WriteLine("Error performing mobile login: " + loginResult);
                }
            }
        }

        private bool UserWebLogOn()
        {
            //do
            //{
                //WebLoggedIn = SteamWeb.DoLogin(Config.SteamUsername, Config.SteamPassword);
                WebLoggedIn = SteamWeb.Authenticate(myUniqueId, SteamClient, myUserNonce);
                /*Task<bool> task = SteamWeb.DoLoginCustomRSA(loginInfo.Username, loginInfo.Password);
                task.Wait();
                WebLoggedIn = task.GetAwaiter().GetResult();*/
                //WebLoggedIn = SteamWeb.DoLoginCustomRSA(loginInfo.Username, loginInfo.Password, steamGuardAccount);

                if (!WebLoggedIn)
                {
                    Console.WriteLine("Authentication failed. Can't continue. Retry again later.");
                    /*Thread.Sleep(2000);
                    WebLoggedIn = SteamWeb.DoLoginCustomRSA(loginInfo.Username, loginInfo.Password, steamGuardAccount);*/
                    return false;
                }

            //} while (!WebLoggedIn);

            Console.WriteLine("User Authenticated!");

            SteamProfileInfo = GetSteamProfileInfo(SteamClient.SteamID);

            TradeOfferManager = new TradeOfferManager(loginInfo.API, SteamWeb);
            SubscribeTradeOffer(TradeOfferManager);

            SpawnTradeOfferPollingThread();

            return true;
        }

        public void Disconnect()
        {
            Running = false;
            steamUser.LogOff();
            SteamClient.Disconnect();

            CancelTradeOfferPollingThread();

            Console.Write("Stopping bot {0} ...", Name);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[PLEASE WAIT]");
            Console.ForegroundColor = ConsoleColor.White;
        }

        //*******************//
        //   STEAM FRIENDS   //
        //*******************//

        private void OnSteamFriendsList(SteamFriends.FriendsListCallback obj)
        {
            List<SteamID> newFriends = new List<SteamID>();

            foreach (SteamFriends.FriendsListCallback.Friend friend in obj.FriendList)
            {

                switch (friend.SteamID.AccountType)
                {
                    case EAccountType.Clan:
                        if (friend.Relationship == EFriendRelationship.RequestRecipient)
                            DeclineGroupInvite(friend.SteamID);
                        break;

                    case EAccountType.Individual:
                        CreateFriendsListIfNecessary();

                        if (friend.Relationship == EFriendRelationship.None)
                        {
                            Friends.Remove(friend.SteamID);
                        }
                        else if (friend.Relationship == EFriendRelationship.RequestRecipient)
                        {
                            if (!Friends.Contains(friend.SteamID))
                            {
                                Friends.Add(friend.SteamID);
                                newFriends.Add(friend.SteamID);
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Couldn't add friend because already in friend list ?!!");
                                Console.ForegroundColor = ConsoleColor.White;
                            }
                        }
                        else if (friend.Relationship == EFriendRelationship.RequestInitiator)
                        {
                            if (!Friends.Contains(friend.SteamID))
                            {
                                Friends.Add(friend.SteamID);
                                newFriends.Add(friend.SteamID);
                            }
                        }
                        else if (friend.Relationship == EFriendRelationship.Blocked)
                        {
                            Program.PrintErrorMessage(friend.SteamID + " blocked OR manually removed bot, steam won't allow me to add him back as friend for a moment !");
                            if (Friends.Contains(friend.SteamID))
                            {
                                Friends.Remove(friend.SteamID);
                                newFriends.Remove(friend.SteamID);
                            }
                        }

                        if (friend.Relationship != EFriendRelationship.Friend)
                            Console.WriteLine(">> " + friend.Relationship);

                        break;
                }

                SteamFriends.RequestFriendInfo(friend.SteamID, EClientPersonaStateFlag.LastSeen);
            }
            
            Console.WriteLine("Recorded steam friends : {0} / {1}", SteamFriends.GetFriendCount(), maxfriendCount);

            if (SteamFriends.GetFriendCount() == maxfriendCount)
            {
                Console.WriteLine("Too much friends. Removing one.");

                Random rnd = new Random();
                int unluckyDude = 0;
                SteamID steamID = Friends[unluckyDude];
                while (newFriends.Contains(steamID) && !Config.IsAdmin(steamID))
                {
                    unluckyDude = rnd.Next(Friends.Count);
                    steamID = Friends[unluckyDude];
                }

                SteamFriends.SendChatMessage(steamID, EChatEntryType.ChatMsg, "Sorry, I had to remove you because my friend list is too small ! Feel free to add me back anytime !");
                SteamFriends.RemoveFriend(steamID);
                Friends.Remove(steamID);
            }

            foreach (SteamID neoFriend in newFriends)
            {
                SteamFriends.AddFriend(neoFriend);
                //Manager.Send();
            }
        }

        private void OnProfileInfo(SteamFriends.PersonaStateCallback obj)
        {
            if (obj == null)
            {
                Program.PrintErrorMessage("Obj was null ?! -> Bot:587");
            }
            else if (obj.LastLogOn == null)
            {
                Program.PrintErrorMessage("LastLogOn was null ?! -> Bot:590");
            }
            else if (obj.FriendID == null)
            {
                Program.PrintErrorMessage("FriendID was null ?! -> Bot:593");
            }
            else if (obj.LastLogOn != NullDate && (DateTime.Now - obj.LastLogOn).TotalDays > 4)
            {
                SteamFriends.RemoveFriend(obj.FriendID);
                Friends.Remove(obj.FriendID);
            }
        }

        private void GetMaxFriends()
        {
            //SteamFriends.RequestProfileInfo(steamClient.SteamID.ConvertToUInt64());
            if (maxfriendCount == 0)
            {
                string json = "";
                try
                {
                    string profileLink = "http://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key="+ Config.SteamAPIKey +"&steamid=" + SteamClient.SteamID.ConvertToUInt64();
                    json = SteamWeb.Fetch(profileLink, "GET");
                    JObject parsedJson = JObject.Parse(json);

                    if (parsedJson.Count > 0 && parsedJson.HasValues)
                    {
                        JToken value = parsedJson["response"]["player_level"];
                        maxfriendCount = (value != null) ? (Int32.Parse(value.ToString())*5) + 250 : 250;
                    }
                }
                catch (Exception ex)
                {
                    Program.PrintErrorMessage("Error while reading the steam level of own profile.");
                    Program.PrintErrorMessage("JSON:\n" + json);
                    Program.PrintErrorMessage("Is steam profile configured ?");
                    maxfriendCount = 250;
                }
            }
        }

        private void CreateFriendsListIfNecessary()
        {
            if (Friends != null)
                return;

            Friends = new List<SteamID>();
            for (int i = 0; i < SteamFriends.GetFriendCount(); i++)
                Friends.Add(SteamFriends.GetFriendByIndex(i));
        }

        //******************//
        //   STEAM GROUPS   //
        //******************//

        private void AcceptGroupInvite(SteamID group)
        {
            var AcceptInvite = new ClientMsg<CMsgGroupInviteAction>((int)EMsg.ClientAcknowledgeClanInvite);

            AcceptInvite.Body.GroupID = group.ConvertToUInt64();
            AcceptInvite.Body.AcceptInvite = true;

            SteamClient.Send(AcceptInvite);
        }

        private void DeclineGroupInvite(SteamID group)
        {
            var DeclineInvite = new ClientMsg<CMsgGroupInviteAction>((int)EMsg.ClientAcknowledgeClanInvite);

            DeclineInvite.Body.GroupID = group.ConvertToUInt64();
            DeclineInvite.Body.AcceptInvite = false;

            SteamClient.Send(DeclineInvite);
        }

        public void InviteUserToGroup(SteamID user, SteamID groupId)
        {
            var InviteUser = new ClientMsg<CMsgInviteUserToGroup>((int)EMsg.ClientInviteUserToClan);

            InviteUser.Body.GroupID = groupId.ConvertToUInt64();
            InviteUser.Body.Invitee = user.ConvertToUInt64();
            InviteUser.Body.UnknownInfo = true;

            this.SteamClient.Send(InviteUser);
        }

        //*******************//
        //  HELPER FUNCTION  //
        //*******************//

        private double GetInventoryValue()
        {
            long[] contextID = new long[1];
            contextID[0] = 2;

            SteamInventoryItemCount = 0;
            double value = 0.0;
            int backup = 0;

            SteamInventoryTF2Items = 0;
            MyGenericInventory.load((int)Games.TF2, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                GenericInventory.ItemDescription description = (ItemDescription)MyGenericInventory.getDescription(item.assetid);

                ASteambot.SteamMarketUtility.Item i = SteamMarket.GetItemByName(description.market_hash_name, item.appid);
                if (description.tradable)
                {
                    if (i != null)// && i.Value != 0)
                        value += i.Value;

                    SteamInventoryItemCount++;
                }
            }
            SteamInventoryTF2Items = SteamInventoryItemCount;
            backup = SteamInventoryItemCount;

            SteamInventoryCSGOItems = 0;
            MyGenericInventory.load((int)Games.CSGO, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                ItemDescription description = (ItemDescription)MyGenericInventory.getDescription(item.assetid);

                ASteambot.SteamMarketUtility.Item i = SteamMarket.GetItemByName(description.market_hash_name, item.appid);
                if (description.tradable)
                {
                    if (i != null)// && i.Value != 0)
                        value += i.Value;

                    SteamInventoryItemCount++;
                }
            }

            int ic = SteamInventoryItemCount - backup;
            if (ic < 0)
                ic = 0;

            steamInventoryCSGOItems = ic;
            backup += SteamInventoryItemCount;

            SteamInventoryDOTA2Items = 0;
            MyGenericInventory.load((int)Games.Dota2, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                GenericInventory.ItemDescription description = (ItemDescription)MyGenericInventory.getDescription(item.assetid);

                ASteambot.SteamMarketUtility.Item i = SteamMarket.GetItemByName(description.market_hash_name, item.appid);
                if (description.tradable)
                {
                    if (i != null)// && i.Value != 0)
                        value += i.Value;

                    SteamInventoryItemCount++;
                }
            }
            ic = SteamInventoryItemCount - backup;
            if (ic < 0)
                ic = 0;

            steamInventoryDOTA2Items = ic;

            return value;
        }

        public int GetNumberOfTrades()
        {
            string[] rows = { "tradeOfferID" };
            List<Dictionary<string, string>> values = DB.SELECT(rows, "tradeoffers");

            return values.Count;
        }

        public void WithDrawn(string steamid)
        {
            SteamID steamID = new SteamID(steamid);
            if (!Friends.Contains(steamID.ConvertToUInt64()))
            {
                Console.WriteLine("This user is not in your friend list, unable to send trade offer.");
                return;
            }

            SteamProfile sp = new SteamProfile(SteamWeb, steamID);

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("You are about to send ALL the bot's items to");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(" {0} ({1} - {2}) ", sp.Informations.Name, steamid, "http://steamcommunity.com/profiles/" + steamID.ConvertToUInt64());
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("via a trade offer, do you confirm ? (YES / NO)");
            Console.WriteLine();
            string answer = Console.ReadLine();

            if (!answer.Equals("YES"))
            {
                Console.WriteLine("Operation cancelled. Nothing traded.");
                return;
            }

            TradeOffer to = TradeOfferManager.NewOffer(steamID);
            long[] contextID = new long[1];
            contextID[0] = 2;

            MyGenericInventory.load((int)Games.TF2, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                GenericInventory.ItemDescription description = (ItemDescription)MyGenericInventory.getDescription(item.assetid);
                if (description.tradable)
                    to.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
            }

            MyGenericInventory.load((int)Games.CSGO, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                GenericInventory.ItemDescription description = (ItemDescription)MyGenericInventory.getDescription(item.assetid);
                if (description.tradable)
                    to.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
            }

            MyGenericInventory.load((int)Games.Dota2, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                GenericInventory.ItemDescription description = (ItemDescription)MyGenericInventory.getDescription(item.assetid);
                if (description.tradable)
                    to.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
            }

            if (to.Items.GetMyItems().Count <= 0)
            {
                Program.PrintErrorMessage("Couldn't send trade offer, inventory is empty.");
            }
            else
            {
                string offerId;
                to.Send(out offerId, "Backpack withdrawn");

                AcceptMobileTradeConfirmation(offerId);

                Console.WriteLine("Whitdrawn offer sent !");
            }
        }

        public void ChangeName(string newname)
        {
            renaming = true;
            SteamFriends.SetPersonaName(newname);
        }

        public void Run()
        {
            Running = true;
            cbManager.RunWaitAllCallbacks(TimeSpan.FromSeconds(1));
        }

        public bool LoginInfoMatch(LoginInfo loginfo)
        {
            if (this.loginInfo.Username == loginfo.Username && this.loginInfo.Password == loginfo.Password)
                return true;
            else
                return false;
        }

        private void SendTradeOfferConfirmationToGameServers(string id, int serverID, int moduleID, NetworkCode.ASteambotCode code, string data)
        {
            GameServer gameServer = Manager.GetServerByID(serverID);
            if (gameServer == null)
            {
                foreach (GameServer gs in Manager.Servers)
                {
                    if (TradeoffersGS.ContainsKey(id))
                        Manager.Send(gs.ServerID, moduleID, code, data);
                    else
                        Manager.Send(gs.ServerID, -2, code, data); //should never ever go here !
                }
                finishedTO.Add(id);
                TradeoffersGS.Remove(id);
            }
            else
            {
                if (TradeoffersGS.ContainsKey(id))
                    Manager.Send(gameServer.ServerID, moduleID, code, data);
                else
                    Manager.Send(gameServer.ServerID, -2, code, data); //should never ever go here !

                finishedTO.Add(id);
                TradeoffersGS.Remove(id);
            }
        }

        public void UpdateUserTradeToken(int srvID, int mID, SteamID steamID, string token)
        {
            if (!steamID.IsValid)
            {
                Manager.Send(srvID, mID, NetworkCode.ASteambotCode.TradeToken, "invalid_steam_id");
                return;
            }

            string[] rows = new string[2];
            string[] values = new string[2];

            rows[0] = "steamID";
            rows[1] = "tradetoken";

            ulong steamid_long = steamID.ConvertToUInt64();
            values[0] = steamid_long.ToString();
            values[1] = token;

            if (GetToken(steamID) == null)
            {
                DB.INSERT("tradeusertoken", rows, values);
            }
            else
            {
                string query = String.Format("UPDATE tradeusertoken SET `tradetoken`=\"{0}\" WHERE `"+ rows[0] + "`=\"{1}\";", values[1], values[0]);
                DB.QUERY(query);
            }

            if (tradetokens.ContainsKey(steamid_long))
                tradetokens[steamid_long] = values[1];
            else
                tradetokens.Add(steamid_long, values[1]);

            Manager.Send(srvID, mID, NetworkCode.ASteambotCode.TradeToken, values[0]+"/"+"ok/"+ values[1]);
        }

        public string GetToken(SteamID steamID)
        {
            if (tradetokens.ContainsKey(steamID.ConvertToUInt64()))
                return tradetokens[steamID.ConvertToUInt64()];

            string[] rows = new string[1];
            rows[0] = "tradetoken";

            var output = DB.SELECT(rows, "tradeusertoken", "WHERE `steamID`=\"" + steamID.ConvertToUInt64() + "\"").FirstOrDefault();
            if (output == null)
            {
                return null;
            }
            else
            {
                tradetokens[steamID] = output[rows[0]];
                return output[rows[0]];
            }
        }

        public void UpdateTradeOfferInDatabase(TradeOffer to, double value)
        {
            if (to.TradeOfferId == null)
                return;

            string[] rows = new string[4];
            string[] values = new string[4];

            rows[0] = "steamID";
            rows[1] = "tradeOfferID";
            rows[2] = "tradeValue";
            rows[3] = "tradeStatus";

            if(DB.SELECT(rows, "tradeofferv2", "WHERE `tradeOfferID`=\"" + to.TradeOfferId + "\"").FirstOrDefault() == null)
            {
                values[0] = to.PartnerSteamId.ConvertToUInt64().ToString();
                values[1] = to.TradeOfferId;
                values[2] = value.ToString().Replace(",", ".");
                values[3] = (to.OfferState).ToString();

                DB.INSERT("tradeofferv2", rows, values);
            }
            else
            {
                string query = String.Format("UPDATE tradeofferv2 SET `tradeStatus`=\"{0}\" WHERE `tradeOfferID`=\"{1}\";", (to.OfferState), to.TradeOfferId);
                DB.QUERY(query);
            }
        }

        private double GetTradeOfferValue(SteamID partenar, List<TradeOffer.TradeStatusUser.TradeAsset> list)
        {
            double cent = 0;

            Thread.CurrentThread.IsBackground = true;
            long[] contextID = new long[1];
            contextID[0] = 2;

            List<long> appIDs = new List<long>();
            GenericInventory gi = new GenericInventory(partenar, SteamWeb);

            foreach (TradeOffer.TradeStatusUser.TradeAsset item in list)
            {
                if (!appIDs.Contains(item.AppId))
                    appIDs.Add(item.AppId);
            }

            cent = 0;
            foreach (int appID in appIDs)
            {
                gi.load(appID, contextID, partenar);
                foreach (TradeOffer.TradeStatusUser.TradeAsset item in list)
                {
                    if (item.AppId != appID)
                        continue;

                    ItemDescription ides = (ItemDescription)gi.getDescription((ulong)item.AssetId);

                    if (ides == null)
                    {
                        Console.WriteLine("Warning, items description for item " + item.AssetId + " not found !");
                    }
                    else
                    {
                        SteamMarketUtility.Item itemInfo = SteamMarket.GetItemByName(ides.market_hash_name, appID);
                        if (itemInfo != null)
                            cent += itemInfo.Value;
                    }
                }
            }

            return cent;
        }

        public void SubscribeTradeOffer(TradeOfferManager tradeOfferManager)
        {
            tradeOfferManager.OnTradeOfferUpdated += OnTradeOfferUpdated;
        }

        protected void SpawnTradeOfferPollingThread()
        {
            if (tradeOfferThread == null)
            {
                tradeOfferThread = new Thread(TradeOfferPollingFunction);
                tradeOfferThread.CurrentUICulture = new CultureInfo("en-US");
                tradeOfferThread.Start();
            }
        }

        protected void CancelTradeOfferPollingThread()
        {
            tradeOfferThread = null;
        }

        protected void TradeOfferPollingFunction()
        {
            while (tradeOfferThread == Thread.CurrentThread)
            {
                try
                {
                    TradeOfferManager.EnqueueUpdatedOffers();
                }
                catch (Exception e)
                {
                    //Sucks
                    Console.WriteLine("Error while polling trade offers: ");
                    if (e.Message.Contains("403"))
                        Console.WriteLine("Access not allowed. Check your steam API key.");
                    else
                        Console.WriteLine(e.Message);
                }

                Thread.Sleep(30 * 1000);//tradeOfferPollingIntervalSecs * 1000);
            }
        }

        public SteamID getSteamID()
        {
            return SteamClient.SteamID;
        }

        //***********//
        //   EVENTS  //
        //***********//

        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            SteamFriends.SetPersonaState(EPersonaState.Online);

            Name = SteamFriends.GetPersonaName();
        }

        private void OnWebAPIUserNonce(SteamUser.WebAPIUserNonceCallback callback)
        {
            Console.WriteLine("Received new WebAPIUserNonce.");

            if (callback.Result == EResult.OK)
            {
                myUserNonce = callback.Nonce;
                LoggedIn = UserWebLogOn();
            }
            else
            {
                Console.WriteLine("WebAPIUserNonce Error: " + callback.Result);
            }
        }

        private void LoginKey(SteamUser.LoginKeyCallback callback)
        {
            //byte[] data = Encoding.ASCII.GetBytes(callback.UniqueID.ToString());
            //myUniqueId = Convert.ToBase64String(data);

            myUniqueId = callback.UniqueID.ToString();

            LoggedIn = UserWebLogOn();

            if (LoggedIn)
            {
                Console.WriteLine(Name + " logged in completly !");

                if (!botThread.IsBusy)
                    botThread.RunWorkerAsync();
            }
        }

        private void OnSteamNameChange(SteamFriends.PersonaChangeCallback callback)
        {
            if (renaming)
            {
                renaming = false;
                Name = SteamFriends.GetPersonaName();
                Console.Title = "Akarr's steambot - [" + Name + "]";
                Console.WriteLine("Steambot renamed sucessfully !");
            }
        }

        private void OnFriendMsgCallback(SteamFriends.FriendMsgCallback callback)
        {
            object[] args = new object[4];
            args[0] = SteamFriends;
            args[1] = callback.Sender;
            args[2] = callback.EntryType;
            args[3] = callback.Message;

            List<bool> results = Program.ExecuteModuleFonction("HandleMessage", args);

            bool block = false;
            foreach (bool output in results)
            {
                block = output;
                if(block)
                    break;
            }

            if (!block && callback.EntryType == EChatEntryType.ChatMsg)
                SteamchatHandler.HandleMessage(callback.Sender, callback.Message);
        }

        private void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine("Updating sentryfile...");
            int fileSize;
            byte[] sentryHash;

            using (var fs = File.Open("auth/"+loginInfo.SentryFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.Seek(callback.Offset, SeekOrigin.Begin);
                fs.Write(callback.Data, 0, callback.BytesToWrite);
                fileSize = (int)fs.Length;

                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = SHA1.Create())
                    sentryHash = sha.ComputeHash(fs);
            }

            Console.WriteLine("Done!");

            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,

                FileName = callback.FileName,

                BytesWritten = callback.BytesToWrite,
                FileSize = fileSize,
                Offset = callback.Offset,

                Result = EResult.OK,
                LastError = 0,

                OneTimePassword = callback.OneTimePassword,

                SentryFileHash = sentryHash,
            });
        }

        public void OnSteambotDisconnected(SteamClient.DisconnectedCallback callback)
        {
            if (Running)
            {
                Console.WriteLine("Disconnected from Steam, reconnecting in 3 seconds...");
                Thread.Sleep(TimeSpan.FromSeconds(3));

                SteamClient.Connect();
            }
        }

        public void OnSteambotConnected(SteamClient.ConnectedCallback callback)
        {
            /*if (callback.JobID != JobID.Invalid)
            {*/
            Console.WriteLine("Connected to steam network !");
            Console.WriteLine("Logging in...");

            byte[] test = null;
            if (File.Exists(loginInfo.SentryFileName))
            {
                byte[] sentryFile = File.ReadAllBytes(loginInfo.SentryFileName);
                test = CryptoHelper.SHAHash(sentryFile);
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = loginInfo.Username,
                Password = loginInfo.Password,
                AuthCode = loginInfo.AuthCode,
                TwoFactorCode = loginInfo.TwoFactorCode,
                SentryFileHash = test,
            });
            /*}
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unable to connect to steamnetwork ! Try again in a few minutes.");
                Console.ForegroundColor = ConsoleColor.White;
                stop = true;
            }*/
        }

        public void OnSteambotLoggedIn(SteamUser.LoggedOnCallback callback)
        {
            switch (callback.Result)
            {
                case EResult.OK:
                    myUserNonce = callback.WebAPIUserNonce;
                    Console.WriteLine("Logged in to steam !");
                    
                    GetMaxFriends();

                    MyGenericInventory = new GenericInventory(SteamClient.SteamID, SteamWeb);
                    break;

                case EResult.AccountLoginDeniedNeedTwoFactor:
                case EResult.AccountLogonDenied:

                    bool isSteamGuard = callback.Result == EResult.AccountLogonDenied;
                    bool is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;

                    if (isSteamGuard || is2FA)
                    {
                        Console.WriteLine("This account is SteamGuard protected!");

                        if (is2FA)
                        {
                            Console.WriteLine("Generating 2 factor auth code...");

                            string authCode = GetMobileAuthCode();
                            loginInfo.SetTwoFactorCode(authCode);
                        }
                        else
                        {
                            Console.WriteLine("Please enter the auth code sent to the email at {0}: ", callback.EmailDomain);
                            loginInfo.SetAuthCode(Console.ReadLine());
                        }
                    }
                    break;

                case EResult.TwoFactorCodeMismatch:
                case EResult.TwoFactorActivationCodeMismatch:
                    //stop = true;
                    Console.WriteLine("The 2 factor auth code is wrong ! Reloading...");
                    loginInfo.SetTwoFactorCode(GetMobileAuthCode());
                    break;

                case EResult.InvalidLoginAuthCode:
                    //stop = true;
                    Console.WriteLine("The auth code (email) is wrong ! Asking it again : ");
                    loginInfo.SetAuthCode(Console.ReadLine());
                    break;

                default:
                    loginInfo.LoginFailCount++;
                    Console.WriteLine("Loggin failed ({0} times) ! {1}", loginInfo.LoginFailCount, callback.Result);

                    if (loginInfo.LoginFailCount == 3)
                        Running = false;
                    break;
            }
        }

        private void OnSteambotLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            LoggedIn = false;
            Console.WriteLine("Logged off of Steam: {0}", callback.Result);

            if(callback.Result == EResult.ServiceUnavailable)
            {
                Console.WriteLine("Attempting to reconnect...");
                Thread.Sleep(TimeSpan.FromSeconds(30));
                Auth();
            }
        }

        private void OnTradeOfferUpdated(TradeOffer offer)
        {
            TradeOffer to;
            TradeOfferManager.TryGetOffer(offer.TradeOfferId, out to);

            if (to == null)
                return;

            double tradeValue = GetTradeOfferValue(to.PartnerSteamId, to.Items.GetTheirItems());
            UpdateTradeOfferInDatabase(to, tradeValue);

            if (offer.IsOurOffer)
            {
                OnOwnTradeOfferUpdated(offer);
            }
            else
            {
                if (offer.OfferState == TradeOfferState.TradeOfferStateActive && Config.IsAdmin(offer.PartnerSteamId))
                    offer.Accept();
                else if (offer.OfferState == TradeOfferState.TradeOfferStateActive)
                    offer.Decline();

                OnPartenarTradeOfferUpdated(offer);
            }
        }

        private void OnOwnTradeOfferUpdated(TradeOffer offer)
        {
            if (offer.OfferState == TradeOfferState.TradeOfferStateAccepted && TradeOfferValue.ContainsKey(offer.TradeOfferId))
            {
                string[] srvID_mID_value = TradeoffersGS[offer.TradeOfferId].Split('|');
                string msg = offer.PartnerSteamId.ConvertToUInt64() + "/" + offer.TradeOfferId + "/" + TradeOfferValue[offer.TradeOfferId] + "/" + srvID_mID_value[3];
                TradeOfferValue.Remove(offer.TradeOfferId);

                SendTradeOfferConfirmationToGameServers(offer.TradeOfferId, Int32.Parse(srvID_mID_value[0]), Int32.Parse(srvID_mID_value[1]), NetworkCode.ASteambotCode.TradeOfferSuccess, msg);
            }
            else if (offer.OfferState == TradeOfferState.TradeOfferStateDeclined && TradeOfferValue.ContainsKey(offer.TradeOfferId))
            {
                string[] srvID_mID_value = TradeoffersGS[offer.TradeOfferId].Split('|');
                string msg = offer.PartnerSteamId.ConvertToUInt64() + "/" + offer.TradeOfferId + "/" + TradeOfferValue[offer.TradeOfferId] + "/" + srvID_mID_value[3];
                TradeOfferValue.Remove(offer.TradeOfferId);

                SendTradeOfferConfirmationToGameServers(offer.TradeOfferId, Int32.Parse(srvID_mID_value[0]), Int32.Parse(srvID_mID_value[1]), NetworkCode.ASteambotCode.TradeOfferDecline, msg);
            }
        }

        private void OnPartenarTradeOfferUpdated(TradeOffer offer)
        {
            if (offer.OfferState == TradeOfferState.TradeOfferStateAccepted)
            {
                double value = GetTradeOfferValue(offer.PartnerSteamId.ConvertToUInt64(), offer.Items.GetTheirItems());


                if (TradeoffersGS.ContainsKey(offer.TradeOfferId))
                {
                    string[] mID_value = TradeoffersGS[offer.TradeOfferId].Split('|');
                    string msg = offer.PartnerSteamId.ConvertToUInt64() + "/" + offer.TradeOfferId + "/" + value + "/" + mID_value[3];
                    SendTradeOfferConfirmationToGameServers(offer.TradeOfferId, Int32.Parse(mID_value[0]), Int32.Parse(mID_value[1]), NetworkCode.ASteambotCode.TradeOfferSuccess, msg);
                }
                else
                {
                    Console.WriteLine("Arkarr has been lazy again!");
                    //Oh shit!
                    //SendTradeOfferConfirmationToGameServers(offer.TradeOfferId, (int)Math.Round(value), NetworkCode.ASteambotCode.TradeOfferSuccess, msg);
                }
            }
            else if (offer.OfferState == TradeOfferState.TradeOfferStateDeclined)
            {
                double value = GetTradeOfferValue(offer.PartnerSteamId.ConvertToUInt64(), offer.Items.GetTheirItems());


                if (TradeoffersGS.ContainsKey(offer.TradeOfferId))
                {
                    string[] mID_value = TradeoffersGS[offer.TradeOfferId].Split('|');
                    string msg = offer.PartnerSteamId.ConvertToUInt64() + "/" + offer.TradeOfferId + "/" + value + "/" + mID_value[3];
                    SendTradeOfferConfirmationToGameServers(offer.TradeOfferId, Int32.Parse(mID_value[0]), Int32.Parse(mID_value[1]), NetworkCode.ASteambotCode.TradeOfferDecline, msg);
                }
                else
                {
                    Console.WriteLine("Arkarr has been lazy again !");
                    //Oh shit !
                    //SendTradeOfferConfirmationToGameServers(offer.TradeOfferId, (int)Math.Round(value), NetworkCode.ASteambotCode.TradeOfferSuccess, msg);
                }
            }
        }

        public SteamProfile.Infos GetSteamProfileInfo(SteamID steamID)
        {
            SteamProfile sp = steamprofiles.Find(x => x.Informations != null && x.Informations.SteamID64 != null && x.Informations.SteamID64.Equals(steamID));
            if (sp == null)
            {
                SteamProfile steamProfile = new SteamProfile(SteamWeb, steamID);
                SteamProfile.Infos spi = steamProfile.Informations;
                steamprofiles.Add(steamProfile);

                return spi;
            }
            else
            {
                return sp.Informations;
            }
        }

        public async Task PlayGames(uint gameID)
        {
            IList<uint> games = new List<uint>();
            games.Add(gameID);
            await GSMH.PlayGames(games);
        }
    }
}
