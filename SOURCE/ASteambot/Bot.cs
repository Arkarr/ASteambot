using System;
using SteamKit2;
using SteamAuth;
using System.IO;
using SteamTrade;
using System.Linq;
using System.Threading;
using System.ComponentModel;
using SteamTrade.TradeOffer;
using System.Collections.Generic;
using System.Security.Cryptography;
using ASteambot.SteamGroups;
using ASteambot.Networking;
using System.Net;
using System.Net.Sockets;
using SteamTrade.SteamMarket;

using static ASteambot.SteamProfile;
using System.Collections.Specialized;

namespace ASteambot
{
    public class Bot
    {
        public string Name { get; private set; }
        public bool Running { get; private set; }
        public Config Config { get; private set; }
        public bool LoggedIn { get; private set; }
        public bool WebLoggedIn { get; private set; }
        public Manager BotManager { get; private set; }
        public List<SteamID> Friends { get; private set; }
        public SteamMarket ArkarrSteamMarket { get; set; }
        public SteamFriends SteamFriends { get; private set; }
        public SteamTrade.SteamWeb SteamWeb { get; private set; }
        public HandleSteamChat SteamchatHandler { get; private set; }
        public GenericInventory MyGenericInventory { get; private set; }
        public TradeOfferManager TradeOfferManager { get; private set; }
        public Dictionary<string, int> TradeoffersGS { get; private set; }
        public Dictionary<SteamID, int> ChatListener { get; private set; }
        public GenericInventory OtherGenericInventory { get; private set; }

        private bool stop;
        private Database DB;
        private bool renaming;
        private string myUniqueId;
        private string myUserNonce;
        private LoginInfo loginInfo;
        private SteamUser steamUser;
        private List<string> finishedTO;
        private SteamClient steamClient;
        private CallbackManager manager;
        private Thread tradeOfferThread;
        private BackgroundWorker botThread;
        private HandleMessage messageHandler;
        private AsynchronousSocketListener socket;
        private SteamGuardAccount steamGuardAccount;
        private Dictionary<string, double> tradeOfferValue;

        public Bot(Manager botManager, LoginInfo loginInfo, Config Config, AsynchronousSocketListener socket)
        {
            this.socket = socket;
            this.Config = Config;
            BotManager = botManager;
            this.loginInfo = loginInfo;
            steamClient = new SteamClient();
            finishedTO = new List<string>();
            messageHandler = new HandleMessage();
            SteamWeb = new SteamTrade.SteamWeb();
            manager = new CallbackManager(steamClient);
            ChatListener = new Dictionary<SteamID, int>();
            TradeoffersGS = new Dictionary<string, int>();
            TradeoffersGS = new Dictionary<string, int>();
            tradeOfferValue = new Dictionary<string, double>();
            MyGenericInventory = new GenericInventory(SteamWeb);
            OtherGenericInventory = new GenericInventory(SteamWeb);

            if (Program.IsLinux())
                Thread.Sleep(3000);

            DB = new Database(Config.DatabaseServer, Config.DatabaseUser, Config.DatabasePassword, Config.DatabaseName, Config.DatabasePort);
            DB.InitialiseDatabase();

            botThread = new BackgroundWorker { WorkerSupportsCancellation = true };
            botThread.DoWork += BackgroundWorkerOnDoWork;
            botThread.RunWorkerCompleted += BackgroundWorkerOnRunWorkerCompleted;

            socket.MessageReceived += Socket_MessageReceived;
        }

        private void Socket_MessageReceived(object sender, EventArgGameServer e)
        {
            messageHandler.Execute(this, e.GetGameServerRequest);
        }

        private void SaveItemInDB(SteamTrade.SteamMarket.Item item)
        {
            string[] rows = new string[5];
            rows[0] = "itemName";
            rows[1] = "value";
            rows[2] = "quantity";
            rows[3] = "last_updated";
            rows[4] = "gameid";

            string[] values = new string[5];
            values[0] = item.Name;
            values[1] = item.Value.ToString();
            values[2] = item.Quantity.ToString();
            values[3] = item.LastUpdated;
            values[4] = item.AppID.ToString();

            if (DB.SELECT(rows, "smitems", "WHERE itemName=\"" + item.Name + "\"" + ";").Count > 0)
                DB.QUERY("UPDATE smitems SET value='" + item.Value + "',quantity='" + item.Quantity + "',last_updated = '" + item.LastUpdated + "' WHERE itemName=\"" + item.Name + "\"" + ";");
            else
                DB.INSERT("smitems", rows, values);
        }

        private void BackgroundWorkerOnDoWork(object sender, DoWorkEventArgs doWorkEventArgs)
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

        private void BackgroundWorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            if (runWorkerCompletedEventArgs.Error != null)
            {
                Exception ex = runWorkerCompletedEventArgs.Error;

                Console.WriteLine("Unhandled exceptions in bot " + Name + " callback thread: " + Environment.NewLine + ex);

                Console.WriteLine("This bot died. Stopping it..");

                Disconnect();
            }
        }

        public void Auth()
        {
            stop = false;
            loginInfo.LoginFailCount = 0;
            steamUser = steamClient.GetHandler<SteamUser>();
            SteamFriends = steamClient.GetHandler<SteamFriends>();

            SubscribeToEvents();

            steamClient.Connect();
        }

        private string GetMobileAuthCode()
        {
            var authFile = String.Format("{0}.auth", loginInfo.Username);
            if (File.Exists(authFile))
            {
                steamGuardAccount = Newtonsoft.Json.JsonConvert.DeserializeObject<SteamAuth.SteamGuardAccount>(File.ReadAllText(authFile));
                string code = steamGuardAccount.GenerateSteamGuardCode();
                Console.WriteLine("2FA code : " + code);
                return code;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("Failed to generate 2FA code. Make sure you have linked the authenticator via SteamBot or exported the auth files from your phone !");
                Console.WriteLine("Or you can try to input a code now, leave empty to quit : ");
                string code = Console.ReadLine();
                if (code.Equals(String.Empty))
                {
                    Console.WriteLine("Bot will stop now.");
                    Disconnect();

                    stop = true;
                    Running = false;

                    return string.Empty;
                }
                else
                {
                    return code;
                }
            }
        }

        public void SubscribeToEvents()
        {
            //Connection events :
            manager.Subscribe<SteamClient.ConnectedCallback>(OnSteambotConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnSteambotDisconnected);
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnSteambotLoggedIn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnSteambotLoggedOff);
            manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);
            manager.Subscribe<SteamUser.LoginKeyCallback>(LoginKey);
            manager.Subscribe<SteamUser.WebAPIUserNonceCallback>(WebAPIUserNonce);

            //Steam events :
            manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
            manager.Subscribe<SteamFriends.FriendMsgCallback>(OnSteamFriendMessage);
            manager.Subscribe<SteamFriends.PersonaChangeCallback>(OnSteamNameChange);
            manager.Subscribe<SteamFriends.FriendsListCallback>(OnSteamFriendsList);
        }

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

                    default:
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
                        }
                        else if (friend.Relationship == EFriendRelationship.RequestInitiator)
                        {
                            if (!Friends.Contains(friend.SteamID))
                            {
                                Friends.Add(friend.SteamID);
                                newFriends.Add(friend.SteamID);
                            }
                        }
                        break;
                }
            }

            Console.WriteLine("Recorded steam friends : {0} / {1}", SteamFriends.GetFriendCount(), getMaxFriends());

            if (SteamFriends.GetFriendCount() == getMaxFriends())
            {
                Console.WriteLine("Too much friends. Removing one.");

                Random rnd = new Random();
                int unluckyDude = 0;
                SteamID steamID = Friends[unluckyDude];
                while (newFriends.Contains(steamID))
                {
                    unluckyDude = rnd.Next(Friends.Count);
                    steamID = Friends[unluckyDude];
                }

                SteamFriends.SendChatMessage(steamID, EChatEntryType.ChatMsg, "Sorry, I had to remove you because my friend list is too small ! Feel free to add me back anytime !");
                SteamFriends.RemoveFriend(steamID);
            }
        }

        private int getMaxFriends()
        {
            int baseFriend = 250;
            //Get steam level ....
            return baseFriend;
        }

        private void CreateFriendsListIfNecessary()
        {
            if (Friends != null)
                return;

            Friends = new List<SteamID>();
            for (int i = 0; i < SteamFriends.GetFriendCount(); i++)
                Friends.Add(SteamFriends.GetFriendByIndex(i));
        }

        private void AcceptGroupInvite(SteamID group)
        {
            var AcceptInvite = new ClientMsg<CMsgGroupInviteAction>((int)EMsg.ClientAcknowledgeClanInvite);

            AcceptInvite.Body.GroupID = group.ConvertToUInt64();
            AcceptInvite.Body.AcceptInvite = true;

            steamClient.Send(AcceptInvite);
        }

        private void DeclineGroupInvite(SteamID group)
        {
            var DeclineInvite = new ClientMsg<CMsgGroupInviteAction>((int)EMsg.ClientAcknowledgeClanInvite);

            DeclineInvite.Body.GroupID = group.ConvertToUInt64();
            DeclineInvite.Body.AcceptInvite = false;

            steamClient.Send(DeclineInvite);
        }

        public void InviteUserToGroup(SteamID user, SteamID groupId)
        {
            var InviteUser = new ClientMsg<CMsgInviteUserToGroup>((int)EMsg.ClientInviteUserToClan);

            InviteUser.Body.GroupID = groupId.ConvertToUInt64();
            InviteUser.Body.Invitee = user.ConvertToUInt64();
            InviteUser.Body.UnknownInfo = true;

            this.steamClient.Send(InviteUser);
        }

        public void CreateTradeOffer(string otherSteamID)
        {
            List<long> contextId = new List<long>();
            contextId.Add(2);
            MyGenericInventory.load((int)SteamTrade.SteamMarket.Games.TF2, contextId, steamClient.SteamID);

            SteamID partenar = new SteamID(otherSteamID);
            TradeOffer to = TradeOfferManager.NewOffer(partenar);

            GenericInventory.Item test = MyGenericInventory.items.FirstOrDefault().Value;

            to.Items.AddMyItem(test.appid, test.contextid, (long)test.assetid);

            string offerId;
            to.Send(out offerId, "Test trade offer");

            Console.WriteLine("Offer ID : " + offerId);

            AcceptMobileTradeConfirmation(offerId);
        }

        public void AcceptMobileTradeConfirmation(string offerId)
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
                            Console.WriteLine("Confirmed " + confirmation.Description + ". (Confirmation ID #" + confirmation.ID + ")");
                    }
                }
            }
            catch (SteamGuardAccount.WGTokenInvalidException)
            {
                Console.WriteLine("Invalid session when trying to fetch trade confirmations.");
            }
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

        public GameServer GetServerByID(int serverID)
        {
            GameServer g = BotManager.Servers.Find(gs => gs.ServerID == serverID);
            if (g == null)
                return null;

            if (!g.SocketConnected())
            {
                foreach (GameServer gs in BotManager.Servers)
                {
                    if (gs.ServerID != serverID && gs.Name == g.Name)
                    {
                        BotManager.RefreshServers();
                        return gs;
                    }
                }

                BotManager.RefreshServers();
            }
            else
            {
                return g;
            }

            return null;
        }

        public void GenerateCode()
        {
            Console.WriteLine(steamGuardAccount.GenerateSteamGuardCode());
        }

        public void WithDrawn(string steamid)
        {
            SteamID steamID = new SteamID(steamid);
            if (!Friends.Contains(steamID))
            {
                Console.WriteLine("This user is not in your friend list, unable to send trade offer.");
                return;
            }

            SteamProfileInfos sp = SteamProfile.LoadSteamProfile(SteamWeb, steamID);

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("You are about to send ALL the bot's items to");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(" {0} ({1}) ", sp.Name, steamid);
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

            MyGenericInventory.load((int)SteamTrade.SteamMarket.Games.TF2, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                GenericInventory.ItemDescription description = MyGenericInventory.getDescription(item.assetid);
                if (description.tradable)
                    to.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
            }

            MyGenericInventory.load((int)SteamTrade.SteamMarket.Games.CSGO, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                GenericInventory.ItemDescription description = MyGenericInventory.getDescription(item.assetid);
                if (description.tradable)
                    to.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
            }

            MyGenericInventory.load((int)SteamTrade.SteamMarket.Games.Dota2, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                GenericInventory.ItemDescription description = MyGenericInventory.getDescription(item.assetid);
                if (description.tradable)
                    to.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
            }

            if (to.Items.GetMyItems().Count <= 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Couldn't send trade offer, inventory is empty.");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                string offerId;
                to.Send(out offerId, "Backpack withdrawn");

                AcceptMobileTradeConfirmation(offerId);

                Console.WriteLine("Whitdrawn offer sent !");
            }
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
            if (loginResult == SteamAuth.LoginResult.LoginOkay)
            {
                Console.WriteLine("Linking mobile authenticator...");
                var authLinker = new SteamAuth.AuthenticatorLinker(login.Session);
                var addAuthResult = authLinker.AddAuthenticator();
                if (addAuthResult == SteamAuth.AuthenticatorLinker.LinkResult.MustProvidePhoneNumber)
                {
                    while (addAuthResult == SteamAuth.AuthenticatorLinker.LinkResult.MustProvidePhoneNumber)
                    {
                        Console.WriteLine("Enter phone number with country code, e.g. +1XXXXXXXXXXX :");
                        var phoneNumber = Console.ReadLine();
                        authLinker.PhoneNumber = phoneNumber;
                        addAuthResult = authLinker.AddAuthenticator();
                    }
                }
                if (addAuthResult == SteamAuth.AuthenticatorLinker.LinkResult.AwaitingFinalization)
                {
                    steamGuardAccount = authLinker.LinkedAccount;
                    try
                    {
                        var authFile = String.Format("{0}.auth", loginInfo.Username);
                        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "authfiles"));
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

        public void ChangeName(string newname)
        {
            renaming = true;
            SteamFriends.SetPersonaName(newname);
        }

        public void Disconnect()
        {
            stop = true;
            steamUser.LogOff();
            steamClient.Disconnect();
            CancelTradeOfferPollingThread();

            botThread.CancelAsync();

            Console.WriteLine("stopping bot {0} ...", Name);
        }

        //Events :
        #region Events callback


        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            SteamFriends.SetPersonaState(EPersonaState.Online);

            Name = SteamFriends.GetPersonaName();
        }

        private void WebAPIUserNonce(SteamUser.WebAPIUserNonceCallback callback)
        {
            Console.WriteLine("Received new WebAPIUserNonce.");

            if (callback.Result == EResult.OK)
            {
                myUserNonce = callback.Nonce;
                UserWebLogOn();
            }
            else
            {
                Console.WriteLine("WebAPIUserNonce Error: " + callback.Result);
            }
        }

        private void LoginKey(SteamUser.LoginKeyCallback callback)
        {
            myUniqueId = callback.UniqueID.ToString();
            UserWebLogOn();

            Console.WriteLine("Steam Bot Logged In Completely!");

            LoggedIn = true;

            if (!botThread.IsBusy)
                botThread.RunWorkerAsync();
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

        private void OnSteamFriendMessage(SteamFriends.FriendMsgCallback callback)
        {
            if (callback.EntryType == EChatEntryType.ChatMsg && Config.SteamAdmins.Contains(callback.Sender.ToString()))
                SteamchatHandler.HandleMessage(callback.Sender, callback.Message);
        }

        private void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine("Updating sentryfile...");
            int fileSize;
            byte[] sentryHash;
            using (var fs = File.Open(loginInfo.SentryFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.Seek(callback.Offset, SeekOrigin.Begin);
                fs.Write(callback.Data, 0, callback.BytesToWrite);
                fileSize = (int)fs.Length;

                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = SHA1.Create())
                    sentryHash = sha.ComputeHash(fs);
            }

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

            Console.WriteLine("Done!");
        }

        public void OnSteambotDisconnected(SteamClient.DisconnectedCallback callback)
        {
            if (stop == false)
            {
                Console.WriteLine("Disconnected from Steam, reconnecting in 3 seconds...");
                Thread.Sleep(TimeSpan.FromSeconds(3));

                steamClient.Connect();
            }
        }

        public void OnSteambotConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result == EResult.OK)
            {
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
            }
            else
            {
                Console.WriteLine("Unable to connect to steamnetwork !");
                stop = true;
            }

            Console.WriteLine("Return code : {0}", callback.Result);
        }

        public void OnSteambotLoggedIn(SteamUser.LoggedOnCallback callback)
        {
            switch (callback.Result)
            {
                case EResult.OK:
                    myUserNonce = callback.WebAPIUserNonce;
                    Console.WriteLine("Logged in to steam !");
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
                    stop = true;
                    Console.WriteLine("The 2 factor auth code is wrong ! Reloading...");
                    loginInfo.SetTwoFactorCode(GetMobileAuthCode());

                    break;

                case EResult.InvalidLoginAuthCode:
                    stop = true;
                    Console.WriteLine("The auth code (email) is wrong ! Asking it again : ");
                    loginInfo.SetAuthCode(Console.ReadLine());
                    break;

                default:
                    loginInfo.LoginFailCount++;
                    Console.WriteLine("Loggin failed ({0} times) ! {1}", loginInfo.LoginFailCount, callback.Result);

                    if (loginInfo.LoginFailCount == 3)
                        stop = true;
                    break;
            }
        }

        private void OnSteambotLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            stop = true;
            LoggedIn = false;
            Console.WriteLine("Logged off of Steam: {0}", callback.Result);
        }
        #endregion

        /////////////////////////////////////////////////////////////////////
        private void UserWebLogOn()
        {
            do
            {
                WebLoggedIn = SteamWeb.Authenticate(myUniqueId, steamClient, myUserNonce);

                if (!WebLoggedIn)
                {
                    Console.WriteLine("Authentication failed, retrying in 2s...");
                    Thread.Sleep(2000);
                }
            } while (!WebLoggedIn);

            Console.WriteLine("User Authenticated!");

            ArkarrSteamMarket = new SteamMarket(Config.ArkarrAPIKey, Config.DisableMarketScan);

            TradeOfferManager = new TradeOfferManager(loginInfo.API, SteamWeb);
            SubscribeTradeOffer(TradeOfferManager);

            SpawnTradeOfferPollingThread();

            //smp.ItemUpdated += Smp_ItemUpdated;

            /*string[] row = new string[5];
            row[0] = "itemName";
            row[1] = "last_updated";
            row[2] = "value";
            row[3] = "quantity";
            row[4] = "gameid";
            List<Dictionary<string, string>> items = DB.SELECT(row, "smitems");
            if (items != null)
            {
                foreach (Dictionary<string, string> item in items)
                {
                    smp.AddItem(item["itemName"], item["last_updated"], Int32.Parse(item["quantity"]), Double.Parse(item["value"]), Int32.Parse(item["gameid"]));
                }
            }
            smp.ScanMarket();*/
        }

        /*private void Smp_ItemUpdated(object sender, EventArgItemScanned e)
        {
            Item i = e.GetItem;

            if (Program.DEBUG)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Item " + i.Name + " updated (Price : " + i.Value + ") !");
                Console.ForegroundColor = ConsoleColor.White;
            }

            /*string[] rows = new string[1];
            rows[0] = "tradeOfferID";

            List<Dictionary<string, string>> list = DB.SELECT(rows, "tradeoffers", "WHERE `tradeStatus`=\"" + (int)TradeOfferState.TradeOfferStateActive + "\"");
            foreach (Dictionary<string, string> tradeInfo in list)
            {
                TradeOffer to;
                tradeOfferManager.TryGetOffer(tradeInfo["tradeOfferID"], out to);

                if (to != null && to.OfferState == TradeOfferState.TradeOfferStateActive)
                {
                    double cent = GetTradeOfferValue(to.PartnerSteamId, to.Items.GetTheirItems());
                    UpdateTradeOfferInDatabase(to, cent);
                }
            }

            SaveItemInDB(i);
        }*/

        private void TradeOfferUpdated(TradeOffer offer)
        {
            //UpdateTradeOfferInDatabase(offer, cent);

            TradeOffer to;
            TradeOfferManager.TryGetOffer(offer.TradeOfferId, out to);

            if (to != null && ArkarrSteamMarket.IsAvailable())
            {
                double cent = GetTradeOfferValue(to.PartnerSteamId, to.Items.GetTheirItems());
                UpdateTradeOfferInDatabase(to, cent);
            }

            if (offer.IsOurOffer)
                OwnTradeOfferUpdated(offer);
            else
                PartenarTradeOfferUpdated(offer);
        }

        private void OwnTradeOfferUpdated(TradeOffer offer)
        {
            //Console.WriteLine("Sent offer {0} has been updated, status : {1}", offer.TradeOfferId, offer.OfferState.ToString());

            if (offer.OfferState == TradeOfferState.TradeOfferStateNeedsConfirmation)
            {
                AcceptMobileTradeConfirmation(offer.TradeOfferId);
            }

            if (offer.OfferState == TradeOfferState.TradeOfferStateActive)
            {
                double value = GetTradeOfferValue(offer.PartnerSteamId, offer.Items.GetTheirItems());
                tradeOfferValue.Add(offer.TradeOfferId, value);
            }

            if (offer.OfferState == TradeOfferState.TradeOfferStateAccepted && tradeOfferValue.ContainsKey(offer.TradeOfferId))
            {
                string msg = offer.PartnerSteamId + "/" + offer.TradeOfferId + "/" + tradeOfferValue[offer.TradeOfferId];
                SendTradeOfferConfirmationToGameServers(offer.TradeOfferId, NetworkCode.ASteambotCode.TradeOfferSuccess, msg);
            }
            else if (offer.OfferState == TradeOfferState.TradeOfferStateDeclined && tradeOfferValue.ContainsKey(offer.TradeOfferId))
            {
                string msg = offer.PartnerSteamId + "/" + offer.TradeOfferId + "/" + tradeOfferValue[offer.TradeOfferId];
                SendTradeOfferConfirmationToGameServers(offer.TradeOfferId, NetworkCode.ASteambotCode.TradeOfferDecline, msg);
            }
        }

        private void PartenarTradeOfferUpdated(TradeOffer offer)
        {
            //Console.WriteLine("Received offer {0} has been updated, status : {1}", offer.TradeOfferId, offer.OfferState.ToString());

            if (offer.OfferState == TradeOfferState.TradeOfferStateActive)
            {
                double value = GetTradeOfferValue(offer.PartnerSteamId, offer.Items.GetTheirItems());

                string msg = offer.PartnerSteamId + "/" + offer.TradeOfferId + "/" + value;

                if (offer.Items.GetMyItems().Count == 0)
                {
                    offer.Accept();
                    SendTradeOfferConfirmationToGameServers(offer.TradeOfferId, NetworkCode.ASteambotCode.TradeOfferSuccess, msg);
                }
                else
                {
                    offer.Decline();
                    SendTradeOfferConfirmationToGameServers(offer.TradeOfferId, NetworkCode.ASteambotCode.TradeOfferDecline, msg);
                }
            }
        }

        private void SendTradeOfferConfirmationToGameServers(string id, NetworkCode.ASteambotCode code, string data)
        {
            foreach (GameServer gs in BotManager.Servers)
            {
                if (TradeoffersGS.ContainsKey(id))
                {
                    gs.Send(TradeoffersGS[id], code, data);
                    TradeoffersGS.Remove(id);
                    finishedTO.Add(id);
                }
                else
                {
                    if (!finishedTO.Contains(id))
                        gs.Send(-2, code, data); //should never ever go here !
                }
            }
        }

        private void UpdateTradeOfferInDatabase(TradeOffer to, double value)
        {
            string[] rows = new string[4];
            string[] values = new string[4];

            rows[0] = "steamID";
            rows[1] = "tradeOfferID";
            rows[2] = "tradeValue";
            rows[3] = "tradeStatus";

            if (DB.SELECT(rows, "tradeoffers", "WHERE `tradeOfferID`=\"" + to.TradeOfferId + "\"").FirstOrDefault() == null)
            {
                values[0] = to.PartnerSteamId.ToString();
                values[1] = to.TradeOfferId;
                values[2] = value.ToString();
                values[3] = ((int)to.OfferState).ToString();

                DB.INSERT("tradeoffers", rows, values);
            }
            else
            {
                if (to.OfferState != TradeOfferState.TradeOfferStateAccepted &&
                    to.OfferState != TradeOfferState.TradeOfferStateDeclined &&
                    to.OfferState != TradeOfferState.TradeOfferStateCanceled)
                {
                    string query = String.Format("UPDATE tradeoffers SET `tradeStatus`=\"{0}\", `tradeValue`=\"{1}\" WHERE `tradeOfferID`=\"{2}\";", ((int)to.OfferState), value.ToString(), to.TradeOfferId);
                    DB.QUERY(query);
                }
            }
        }

        private double GetTradeOfferValue(SteamID partenar, List<TradeOffer.TradeStatusUser.TradeAsset> list)
        {
            double cent = 0;

            Thread.CurrentThread.IsBackground = true;
            long[] contextID = new long[1];
            contextID[0] = 2;

            List<long> appIDs = new List<long>();
            GenericInventory gi = new GenericInventory(SteamWeb);

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

                    GenericInventory.ItemDescription ides = gi.getDescription((ulong)item.AssetId);

                    if (ides == null)
                    {
                        Console.WriteLine("Warning, items description for item " + item.AssetId + " not found !");
                    }
                    else
                    {
                        Item itemInfo = ArkarrSteamMarket.GetItemByName(ides.market_hash_name);
                        if (itemInfo != null)
                            cent += itemInfo.Value;
                    }
                }
            }

            return cent;
        }

        public void SubscribeTradeOffer(TradeOfferManager tradeOfferManager)
        {
            tradeOfferManager.OnTradeOfferUpdated += TradeOfferUpdated;
        }

        protected void SpawnTradeOfferPollingThread()
        {
            if (tradeOfferThread == null)
            {
                tradeOfferThread = new Thread(TradeOfferPollingFunction);
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

        //Helper function :
        #region Helper function
        public void Run()
        {
            Running = true;

            if (!stop)
                manager.RunWaitAllCallbacks(TimeSpan.FromSeconds(1));
        }

        public bool LoginInfoMatch(LoginInfo loginfo)
        {
            if (this.loginInfo.Username == loginfo.Username && this.loginInfo.Password == loginfo.Password)
                return true;
            else
                return false;
        }
        #endregion
    }
}
