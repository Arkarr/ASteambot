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

namespace ASteambot
{
    public class Bot
    {
        public bool LoggedIn { get; private set; }
        public bool WebLoggedIn { get; private set; }
        public string Name { get; private set; }
        public GenericInventory MyGenericInventory { get; private set; }

        private bool stop;
        private bool renaming;
        private string myUserNonce;
        private string myUniqueId;
        private LoginInfo loginInfo;
        private SteamUser steamUser;
        private SteamClient steamClient;
        private CallbackManager manager;
        private Thread tradeOfferThread;
        private SteamFriends steamFriends;
        private BackgroundWorker botThread;
        private SteamTrade.SteamWeb steamWeb;
        private TradeOfferManager tradeOfferManager;
        private SteamGuardAccount steamGuardAccount;
        private CallbackManager steamCallbackManager;

        public Bot(LoginInfo loginInfo)
        {
            this.loginInfo = loginInfo;
            steamClient = new SteamClient();
            manager = new CallbackManager(steamClient);
            steamWeb = new SteamTrade.SteamWeb();
            MyGenericInventory = new GenericInventory(steamWeb);

            steamCallbackManager = new CallbackManager(steamClient);

            botThread = new BackgroundWorker { WorkerSupportsCancellation = true };
            botThread.DoWork += BackgroundWorkerOnDoWork;
            botThread.RunWorkerCompleted += BackgroundWorkerOnRunWorkerCompleted;
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
            steamFriends = steamClient.GetHandler<SteamFriends>();

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
            manager.Subscribe<SteamFriends.FriendsListCallback>(OnFriendsList);
            manager.Subscribe<SteamFriends.FriendMsgCallback>(OnSteamFriendMessage);
            manager.Subscribe<SteamFriends.PersonaChangeCallback>(OnSteamNameChange); 
        }

        /// ///////////////////////////////////////////////////////////////
        public void CreateTradeOffer(string otherSteamID)
        {
            List<long> contextId = new List<long>();
            contextId.Add(2);
            MyGenericInventory.load(440, contextId, steamClient.SteamID);

            SteamID partenar = new SteamID(otherSteamID);
            TradeOffer to = tradeOfferManager.NewOffer(partenar);

            GenericInventory.Item test = MyGenericInventory.items.FirstOrDefault().Value;

            to.Items.AddMyItem(test.appid, test.contextid, (long)test.assetid);

            string offerId;
            to.Send(out offerId, "Test trade offer");

            Console.WriteLine("Offer ID : {0}", offerId);

            AcceptAllMobileTradeConfirmations();
        }

        public void AcceptAllMobileTradeConfirmations()
        {
            steamGuardAccount.Session.SteamLogin = steamWeb.Token;
            steamGuardAccount.Session.SteamLoginSecure = steamWeb.TokenSecure;
            try
            {
                foreach (var confirmation in steamGuardAccount.FetchConfirmations())
                {
                    if (steamGuardAccount.AcceptConfirmation(confirmation))
                    {
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
            steamGuardAccount.DeactivateAuthenticator();
        }

        public void WithDrawn(string steamid)
        {
            SteamID steamID = new SteamID(steamid);
            string name = steamFriends.GetFriendPersonaName(steamID);

            Console.WriteLine("You are about to send ALL the bot's items to {0} ({1}) via a trade offer, do you confirm ? (YES/NO)", name, steamid);
            string answer = Console.ReadLine();

            if (!answer.Equals("YES"))
            {
                Console.WriteLine("Operation cancelled. Nothing traded.");
                return;
            }
            
            TradeOffer to = tradeOfferManager.NewOffer(steamID);

            Inventory inventory = Inventory.FetchInventory(steamUser.SteamID, loginInfo.API, steamWeb);
            foreach (Inventory.Item item in inventory.Items)
            {
                if(item.IsNotTradeable == false)
                    to.Items.AddMyItem(item.AppId, item.ContextId, (long)item.Id);
            }

            string offerId;
            to.Send(out offerId, "Backpack withdrawn");

            to.Accept();
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
            steamFriends.SetPersonaName(newname);
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

        private Inventory FetchBotsInventory()
        {
            var inventory = Inventory.FetchInventory(steamClient.SteamID, loginInfo.API, steamWeb);
            if (inventory.IsPrivate)
                Console.WriteLine("The bot's backpack is private! If your bot adds any items it will fail! Your bot's backpack should be Public.");

            return inventory;
        }

        //Events :
        #region Events callback
        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            steamFriends.SetPersonaState(EPersonaState.Online);

            Name = steamFriends.GetPersonaName();
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
                Name = steamFriends.GetPersonaName();
                Console.Title = "Akarr's steambot - [" + Name + "]";
                Console.WriteLine("Steambot renamed sucessfully !");
            }
        }

        private void OnSteamFriendMessage(SteamFriends.FriendMsgCallback callback)
        {
            if(callback.EntryType == EChatEntryType.ChatMsg)
                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Sorry I don't understand you. Yet.");
        }   

        private void OnFriendsList(SteamFriends.FriendsListCallback callback)
        {
            foreach (var friend in callback.FriendList)
            {
                if (friend.Relationship == EFriendRelationship.RequestRecipient)
                    steamFriends.AddFriend(friend.SteamID);
            }

            Console.WriteLine("Recorded steam friends : {0}", steamFriends.GetFriendCount());
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

            tradeOfferManager = new TradeOfferManager(loginInfo.API, steamWeb);
            SubscribeTradeOffer(tradeOfferManager);

            // Success, check trade offers which we have received while we were offline
            SpawnTradeOfferPollingThread();
        }

        private void TradeOfferUpdated(TradeOffer offer)
        {
            if (offer.IsOurOffer)
                OwnTradeOfferUpdated(offer);
            else
                PartenarTradeOfferUpdated(offer);
        }

        private void OwnTradeOfferUpdated(TradeOffer offer)
        {
            Console.WriteLine("Sent offer {0} has been updated, status : {1}", offer.TradeOfferId, offer.OfferState.ToString());
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
