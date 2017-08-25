using System;
using SteamKit2;
using SteamAuth;
using System.IO;
using SteamTrade;
using System.Net;
using System.Linq;
using System.Threading;
using System.ComponentModel;
using SteamTrade.TradeOffer;
using System.Collections.Generic;
using System.Security.Cryptography;
using ASteambot.SteamGroups;
using System.Timers;
using ASteambot.Networking;
using System.Threading.Tasks;
using SteamTrade.TradeWebAPI;

namespace ASteambot
{
    public class Bot
    {
        public string Name { get; private set; }
        public bool LoggedIn { get; private set; }
        public bool WebLoggedIn { get; private set; }
        public Manager botManager { get; private set; }
        public SteamFriends SteamFriends { get; private set; }
        public GenericInventory MyGenericInventory { get; private set; }

        enum Games
        {
            TF2 = 440,
            CSGO = 730,
            Dota2 = 570
        };

        private bool stop;
        private Database DB;
        private int gameScan;
        private Config config;
        private bool renaming;
        private Games currentGame;
        private string myUniqueId;
        private string myUserNonce;
        private LoginInfo loginInfo;
        private SteamUser steamUser;
        private List<SteamID> friends;
        private SteamMarketPrices smp;
        private SteamClient steamClient;
        private CallbackManager manager;
        private Thread tradeOfferThread;
        private BackgroundWorker botThread;
        private HandleMessage messageHandler;
        private SteamTrade.SteamWeb steamWeb;
        private HandleSteamChat steamchatHandler;
        private AsynchronousSocketListener socket;
        private TradeOfferManager tradeOfferManager;
        private SteamGuardAccount steamGuardAccount;
        private CallbackManager steamCallbackManager;

        public Bot(Manager botManager, LoginInfo loginInfo, Config config, AsynchronousSocketListener socket)
        {
            this.socket = socket;
            this.config = config;
            this.loginInfo = loginInfo;
            this.botManager = botManager;
            steamClient = new SteamClient();
            messageHandler = new HandleMessage();
            steamWeb = new SteamTrade.SteamWeb();
            manager = new CallbackManager(steamClient);
            steamchatHandler = new HandleSteamChat(this);
            MyGenericInventory = new GenericInventory(steamWeb);
            steamCallbackManager = new CallbackManager(steamClient);

            this.socket.MessageReceived += Socket_MessageReceived;

            DB = new Database(config.DatabaseServer, config.DatabaseUser, config.DatabasePassword, config.DatabaseName, config.DatabasePort);
            DB.InitialiseDatabase();

            botThread = new BackgroundWorker { WorkerSupportsCancellation = true };
            botThread.DoWork += BackgroundWorkerOnDoWork;
            botThread.RunWorkerCompleted += BackgroundWorkerOnRunWorkerCompleted;

            System.Timers.Timer refreshMarket = new System.Timers.Timer(421000);//15*1000*60);
            refreshMarket.Elapsed += UpdateMarketItems;
            refreshMarket.AutoReset = true;
            refreshMarket.Enabled = true;
        }

        private void Socket_MessageReceived(object sender, EventArgGameServer e)
        {
            string code = e.GetNetworkCode;
            string args = e.GetArguments;
            IPAddress ip = e.GetIP;
            int port = e.GetPort;

            messageHandler.Execute(this, ip, port, code, args);
        }

        private void ScanMarket()
        {
            switch(gameScan)
            {
                case 0:
                    gameScan++;
                    smp.ScanMarket(config.BackpacktfAPIKey, (int)Games.CSGO);
                    currentGame = Games.CSGO;
                    break;
                case 1:
                    gameScan++;
                    smp.ScanMarket(config.BackpacktfAPIKey, (int)Games.TF2);
                    currentGame = Games.TF2;
                    break;
                case 2:
                    gameScan = 0;
                    smp.ScanMarket(config.BackpacktfAPIKey, (int)Games.Dota2);
                    currentGame = Games.Dota2;
                    break;
            }

            if (smp.Items.Count == 0 && smp.ResponseCode == 0)
            {
                Console.WriteLine(">>> ERROR while scanning market :");
                Console.WriteLine(smp.ErrorMessage);
                Console.WriteLine(">>> Prices NOT updated.");
                string[] row = new string[4];
                row[0] = "itemName";
                row[1] = "last_updated";
                row[2] = "value";
                row[3] = "quantity";
                List<Dictionary<string, string>> items = DB.SELECT(row, "smitems");
                if (items != null)
                {
                    foreach (Dictionary<string, string> item in items)
                    {
                        smp.AddItem(new SteamMarketPrices.Item(item["itemName"], Int32.Parse(item["last_updated"]), Int32.Parse(item["quantity"]), Double.Parse(item["value"])));
                    }
                }
                Console.WriteLine("Found " + items.Count + " backup items in database !");
            }
            else
            {
                Console.WriteLine("Item list updated !");

                string[] rows = new string[1];                
                rows[0] = "tradeOfferID";

                List<Dictionary<string, string>> list = DB.SELECT(rows, "tradeoffers");//, "WHERE `tradeStatus`=\"" + TradeOfferState.TradeOfferStateActive + "\"");
                foreach (Dictionary<string, string> tradeInfo in list)
                {
                    TradeOffer to;
                    tradeOfferManager.TryGetOffer(tradeInfo["tradeOfferID"], out to);

                    double cent = GetTradeOfferValue(to.PartnerSteamId, to.Items.GetTheirItems());

                    UpdateTradeOfferInDatabase(to, cent);
                }

                SaveMarketInDB(smp.Items);
            }
        }

        private void SaveMarketInDB(List<SteamMarketPrices.Item> list)
        {
            SteamFriends.SetPersonaState(EPersonaState.Busy);
            BackgroundWorker bw = new BackgroundWorker();

            bw.DoWork += new DoWorkEventHandler(delegate (object o, DoWorkEventArgs args)
            {
                BackgroundWorker b = o as BackgroundWorker;

                if (list == null || list.Count == 0)
                    return;

                Console.WriteLine("Saving steam market items into database...");
                Console.WriteLine("This may take severals minutes !");

                string[] rows = new string[5];
                rows[0] = "itemName";
                rows[1] = "last_updated";
                rows[2] = "value";
                rows[3] = "quantity";
                rows[4] = "gameid";

                List<Dictionary<string, string>> items = null;
                do
                {
                    items = DB.SELECT(rows, "smitems");
                    if (items == null)
                    {
                        Thread.Sleep(3000);
                        Console.WriteLine("You should put more delay between steam market scan.");
                    }
                }
                while (items == null);

                int itemCount = 0;
                foreach (SteamMarketPrices.Item item in list)
                {
                    string[] values = new string[5];
                    values[0] = item.name.Replace(@"\", string.Empty);
                    values[1] = item.last_updated.ToString();
                    values[2] = item.value.ToString();
                    values[3] = item.quantity.ToString();
                    values[4] = ((int)currentGame).ToString();

                    if (items.Count > 0)// && BotGameUsage == Int32.Parse(items[0]["gameid"]))
                    {
                        if (!items.Any(tr => tr["itemName"].Equals(item.name)))
                        {
                            DB.INSERT("smitems", rows, values);
                        }
                        else
                        {
                            Dictionary<string, string> fitem = items.Find(tr => tr["itemName"].Equals(item.name));
                            if (Double.Parse(fitem["last_updated"]) != item.last_updated)
                                DB.QUERY("UPDATE smitems SET value='" + item.value + "',quantity='" + item.quantity + "',last_updated = '" + item.last_updated + "' WHERE itemName=\"" + item.name + "\"" + ";");
                        }
                    }
                    else
                    {
                        DB.INSERT("smitems", rows, values);
                    }
                    itemCount++;

                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    string strItem = String.Format("{0} / {1} - {2}", itemCount, list.Count, values[0]);
                    int length = Console.WindowWidth - strItem.Length - 1;
                    if (length < 0)
                    {
                        strItem = strItem.Substring(0, Console.WindowWidth - 4) + "...";
                        length = 0;
                    }
                    Console.WriteLine(strItem + new string(' ', length));
                    if (itemCount != list.Count)
                    {
                        int pos = Console.CursorTop - 1;
                        if (pos < 0) pos = 0;
                        Console.SetCursorPosition(0, pos);
                    }
                }

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Done !");

                SteamFriends.SetPersonaState(EPersonaState.Online);
            });
            bw.RunWorkerAsync();
        }


        private void UpdateMarketItems(object sender, ElapsedEventArgs e)
        {
            ScanMarket();
        }

        private void BackgroundWorkerOnDoWork(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            while (!botThread.CancellationPending)
            {
                try
                {
                    steamCallbackManager.RunCallbacks();

                    if (tradeOfferManager != null)
                    {
                        tradeOfferManager.HandleNextPendingTradeOfferUpdate();
                    }

                    Thread.Sleep(1);
                }
                catch (WebException e)
                {
                    Console.WriteLine("URI: {0} >> {1}", (e.Response != null && e.Response.ResponseUri != null ? e.Response.ResponseUri.ToString() : "unknown"), e.ToString());
                    System.Threading.Thread.Sleep(45000);//Steam is down, retry in 45 seconds.
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

                Console.WriteLine("Unhandled exceptions in bot {0} callback thread: {1} {2}", Name, Environment.NewLine, ex);

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
                return steamGuardAccount.GenerateSteamGuardCode();
            }
            return string.Empty;
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
                            friends.Remove(friend.SteamID);
                        }
                        else if (friend.Relationship == EFriendRelationship.RequestRecipient)
                        {
                            if (!friends.Contains(friend.SteamID))
                            {
                                friends.Add(friend.SteamID);
                                newFriends.Add(friend.SteamID);
                            }
                        }
                        else if (friend.Relationship == EFriendRelationship.RequestInitiator)
                        {
                            if (!friends.Contains(friend.SteamID))
                            {
                                friends.Add(friend.SteamID);
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
                SteamID steamID = friends[unluckyDude];
                while (newFriends.Contains(steamID))
                {
                    unluckyDude = rnd.Next(friends.Count);
                    steamID = friends[unluckyDude];
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
            if (friends != null)
                return;

            friends = new List<SteamID>();
            for (int i = 0; i < SteamFriends.GetFriendCount(); i++)
                friends.Add(SteamFriends.GetFriendByIndex(i));
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

        /// ///////////////////////////////////////////////////////////////
        /*public void CreateTradeOffer(string otherSteamID)
        {
            List<long> contextId = new List<long>();
            contextId.Add(2);
            MyGenericInventory.load((int)Games.TF2, contextId, steamClient.SteamID);

            SteamID partenar = new SteamID(otherSteamID);
            TradeOffer to = tradeOfferManager.NewOffer(partenar);

            GenericInventory.Item test = MyGenericInventory.items.FirstOrDefault().Value;

            to.Items.AddMyItem(test.appid, test.contextid, (long)test.assetid);

            string offerId;
            to.Send(out offerId, "Test trade offer");

            Console.WriteLine("Offer ID : {0}", offerId);

            AcceptMobileTradeConfirmation(offerId);
        }*/
        ////////////////////////////////////////////////////////////////////
        public void AcceptMobileTradeConfirmation(string offerId)
        {
            steamGuardAccount.Session.SteamLogin = steamWeb.Token;
            steamGuardAccount.Session.SteamLoginSecure = steamWeb.TokenSecure;
            try
            {
                foreach (var confirmation in steamGuardAccount.FetchConfirmations())
                {
                    if (confirmation.ConfType == Confirmation.ConfirmationType.Trade)
                    {
                        long confID = steamGuardAccount.GetConfirmationTradeOfferID(confirmation);
                        if (confID == long.Parse(offerId) && steamGuardAccount.AcceptConfirmation(confirmation))
                            Console.WriteLine("Confirmed {0}. (Confirmation ID #{1})", confirmation.Description, confirmation.ID);
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

        public void WithDrawn(string steamid)
        {
            SteamID steamID = new SteamID(steamid);
            if (!friends.Contains(steamID))
            {
                Console.WriteLine("This user is not in your friend list, unable to send trade offer.");
                return;
            }

            string name = SteamFriends.GetFriendPersonaName(steamID);

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("You are about to send ALL the bot's items to");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(" {0} ({1}) ", name, steamid);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("via a trade offer, do you confirm ? (YES / NO)");
            Console.WriteLine();
            string answer = Console.ReadLine();

            if (!answer.Equals("YES"))
            {
                Console.WriteLine("Operation cancelled. Nothing traded.");
                return;
            }
            
            TradeOffer to = tradeOfferManager.NewOffer(steamID);
            long[] contextID = new long[1];
            contextID[0] = 2;

            MyGenericInventory.load((int)Games.TF2, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                GenericInventory.ItemDescription description = MyGenericInventory.getDescription(item.assetid);
                if (description.tradable)
                    to.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
            }

            MyGenericInventory.load((int)Games.CSGO, contextID, steamUser.SteamID);
            foreach (GenericInventory.Item item in MyGenericInventory.items.Values)
            {
                GenericInventory.ItemDescription description = MyGenericInventory.getDescription(item.assetid);
                if (description.tradable)
                    to.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
            }

            MyGenericInventory.load((int)Games.Dota2, contextID, steamUser.SteamID);
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
            while (botThread.IsBusy)
                Thread.Yield();

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

            ScanMarket();
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
            if (callback.EntryType == EChatEntryType.ChatMsg)
                steamchatHandler.HandleMessage(callback.Sender, callback.Message);
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
                Console.WriteLine("Disconnected from Steam, reconnecting in 5 seconds...");
                Thread.Sleep(TimeSpan.FromSeconds(5));

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
                    Console.WriteLine("The 2 factor auth code is wrong ! Asking it again : ");
                    loginInfo.SetTwoFactorCode(Console.ReadLine());
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
                WebLoggedIn = steamWeb.Authenticate(myUniqueId, steamClient, myUserNonce);

                if (!WebLoggedIn)
                {
                    Console.WriteLine("Authentication failed, retrying in 2s...");
                    Thread.Sleep(2000);
                }
            } while (!WebLoggedIn);

            Console.WriteLine("User Authenticated!");

            smp = new SteamMarketPrices(steamWeb);

            string[] row = new string[4];
            row[0] = "itemName";
            row[1] = "last_updated";
            row[2] = "value";
            row[3] = "quantity";
            List<Dictionary<string, string>> items = DB.SELECT(row, "smitems");
            if (items != null)
            {
                foreach (Dictionary<string, string> item in items)
                {
                    smp.AddItem(new SteamMarketPrices.Item(item["itemName"], Int32.Parse(item["last_updated"]), Int32.Parse(item["quantity"]), Double.Parse(item["value"])));
                }
            }

            tradeOfferManager = new TradeOfferManager(loginInfo.API, steamWeb);
            SubscribeTradeOffer(tradeOfferManager);

            // Success, check trade offers which we have received while we were offline
            SpawnTradeOfferPollingThread();
        }

        private void TradeOfferUpdated(TradeOffer offer)
        {
            double cent = GetTradeOfferValue(offer.PartnerSteamId, offer.Items.GetTheirItems());

            UpdateTradeOfferInDatabase(offer, cent);

            if (offer.IsOurOffer)
                OwnTradeOfferUpdated(offer);
            else
                PartenarTradeOfferUpdated(offer);
        }

        private void OwnTradeOfferUpdated(TradeOffer offer)
        {
            Console.WriteLine("Sent offer {0} has been updated, status : {1}", offer.TradeOfferId, offer.OfferState.ToString());
            
            if(offer.OfferState == TradeOfferState.TradeOfferStateNeedsConfirmation)
                AcceptMobileTradeConfirmation(offer.TradeOfferId);
        }

        private void PartenarTradeOfferUpdated(TradeOffer offer)
        {
            Console.WriteLine("Received offer {0} has been updated, status : {1}", offer.TradeOfferId, offer.OfferState.ToString());

            if (offer.OfferState == TradeOfferState.TradeOfferStateActive)
            {
                if (offer.Items.GetMyItems().Count == 0)
                    offer.Accept();
                else
                    offer.Decline();
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

            if (DB.SELECT(rows, "tradeoffers", "WHERE `tradeOfferID`=\""+ to.TradeOfferId + "\"").FirstOrDefault() == null)
            {
                values[0] = to.PartnerSteamId.ToString();
                values[1] = to.TradeOfferId;
                values[2] = value.ToString();
                values[3] = ((int)to.OfferState).ToString();

                DB.INSERT("tradeoffers", rows, values);
            }
            else
            {
                string query = String.Format("UPDATE tradeoffers SET `tradeStatus`=\"{0}\", `tradeValue`=\"{1}\" WHERE `tradeOfferID`=\"{2}\";", ((int)to.OfferState), value.ToString(), to.TradeOfferId);
                DB.QUERY(query);
            }
        }

        private double GetTradeOfferValue(SteamID partenar, List<TradeOffer.TradeStatusUser.TradeAsset> list)
        {
            if (list.Count > 0)
                Console.WriteLine(list.Count);

            double cent = 0;
            long[] contextID = new long[1];
            contextID[0] = 2;

            List<long> appIDs = new List<long>();
            GenericInventory gi = new GenericInventory(steamWeb);
            
            foreach(TradeOffer.TradeStatusUser.TradeAsset item in list)
            {
                if (!appIDs.Contains(item.AppId))
                    appIDs.Add(item.AppId);
            }

            foreach (int appID in appIDs)
            {
                gi.load(appID, contextID, partenar);
                foreach (TradeOffer.TradeStatusUser.TradeAsset item in list)
                {
                    if (item.AppId != appID)
                        continue;

                    GenericInventory.ItemDescription ides = gi.getDescription((ulong)item.AssetId);

                    SteamMarketPrices.Item itemInfo = smp.Items.Find(i => i.name == ides.market_hash_name);
                    if (itemInfo != null)
                        cent += (itemInfo.value / 100.0);
                    else
                        Console.WriteLine("Item " + ides.market_hash_name + " not found !");
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
                    tradeOfferManager.EnqueueUpdatedOffers();
                }
                catch (Exception e)
                {
                    //Sucks
                    Console.WriteLine("Error while polling trade offers: ");
                    if(e.Message.Contains("403"))
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
