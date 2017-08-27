using SteamKit2;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
using SteamTrade;
using SteamTrade.TradeOffer;
using SteamAuth;

namespace ASteambot
{
    public class BotListener : IDebugListener
    {
        public void WriteLine(string category, string msg)
        {
            Console.WriteLine("[DEBUG] {0}: {1}", category, msg);
        }
    }

    public class Bot
    {
        public bool LoggedIn { get; private set; }
        public bool WebLoggedIn { get; private set; }
        public string Name { get; private set; }
        public GenericInventory MyInventory { get; private set; }

        private bool Stop;
        private string myUserNonce;
        private string myUniqueId;
        private SteamTrade.SteamWeb steamWeb;
        private LoginInfo loginInfo;
        private SteamUser steamUser;
        private SteamClient steamClient;
        private CallbackManager manager;
        private Thread tradeOfferThread;
        private SteamFriends steamFriends;
        private TradeOfferManager tradeOfferManager;
        public SteamAuth.SteamGuardAccount SteamGuardAccount;

        public Bot(LoginInfo loginInfo)
        {
            this.loginInfo = loginInfo;
            steamClient = new SteamClient();
            manager = new CallbackManager(steamClient);
            steamWeb = new SteamTrade.SteamWeb();
            MyInventory = new GenericInventory(steamWeb);
            tradeOfferManager = new TradeOfferManager(loginInfo.API, steamWeb);

            DebugLog.AddListener(new BotListener());
            DebugLog.Enabled = false;
        }

        public void Auth()
        {
            Stop = false;
            loginInfo.LoginFailCount = 0;
            steamUser = steamClient.GetHandler<SteamUser>();
            steamFriends = steamClient.GetHandler<SteamFriends>();

            SubscribeToEvents();

            steamClient.Connect();
        }

        private string GetMobileAuthCode()
        {
            var authFile = Path.Combine("authfiles", String.Format("{0}.auth", loginInfo.Username));
            if (File.Exists(authFile))
            {
                SteamGuardAccount = Newtonsoft.Json.JsonConvert.DeserializeObject<SteamAuth.SteamGuardAccount>(File.ReadAllText(authFile));
                return SteamGuardAccount.GenerateSteamGuardCode();
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

        public void CreateTradeOffer(string otherSteamID)
        {

            SteamID partenar = new SteamID(otherSteamID);
            TradeOffer to = tradeOfferManager.NewOffer(partenar);

            GenericInventory.Item test = MyInventory.items.FirstOrDefault().Value;

            to.Items.AddMyItem(test.appid, test.contextid, (long)test.assetid);

            string offerId;
            to.Send(out offerId, "Test trade offer");

            Console.WriteLine("Offer ID : {0}", offerId);

            AcceptAllMobileTradeConfirmations();
        }

        public void AcceptAllMobileTradeConfirmations()
        {
            SteamGuardAccount.Session.SteamLogin = steamWeb.Token;
            SteamGuardAccount.Session.SteamLoginSecure = steamWeb.TokenSecure;
            try
            {
                foreach (var confirmation in SteamGuardAccount.FetchConfirmations())
                {
                    if (SteamGuardAccount.AcceptConfirmation(confirmation))
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

        public void ChangeName(string newname)
        {
            steamFriends.SetPersonaName(newname);
        }

        public void Disconnect()
        {
            Stop = true;
            steamUser.LogOff();
            steamClient.Disconnect();
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
        }


        private void OnSteamNameChange(SteamFriends.PersonaChangeCallback callback)
        {
            Name = steamFriends.GetPersonaName();
            Console.Title = "Akarr's steambot - [" + Name + "]";
            Console.WriteLine("Steambot renamed sucessfully !");
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
            if (Stop == false)
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
                Stop = true;
            }

            Console.WriteLine("Return code : {0}", callback.Result);
        }

        public void OnSteambotLoggedIn(SteamUser.LoggedOnCallback callback)
        {
            switch (callback.Result)
            {
                case EResult.OK:
                    LoggedIn = true;
                    myUserNonce = callback.WebAPIUserNonce;
                    Console.WriteLine("Logged in to steam !");
                    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    List<long> contextId = new List<long>();
                    contextId.Add(2);
                    MyInventory.load(440, contextId, steamClient.SteamID);
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
                            Console.WriteLine("Please enter your 2 factor auth code from your authenticator app: ");
                            loginInfo.SetTwoFactorCode(Console.ReadLine());
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
                    Stop = true;
                    Console.WriteLine("The 2 factor auth code is wrong ! Asking it again : ");
                    loginInfo.SetTwoFactorCode(Console.ReadLine());
                break;

                case EResult.InvalidLoginAuthCode:
                    Stop = true;
                    Console.WriteLine("The auth code (email) is wrong ! Asking it again : ");
                    loginInfo.SetAuthCode(Console.ReadLine());
                break;

                default:
                    loginInfo.LoginFailCount++;
                    Console.WriteLine("Loggin failed ({0} times) ! {1}", loginInfo.LoginFailCount, callback.Result);

                    if (loginInfo.LoginFailCount == 3)
                        Stop = true;
                break;
            }
        }

        private void OnSteambotLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Stop = true;
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

        public void TradeOfferRouter(TradeOffer offer)
        {
            //manage offer:
        }

        public void SubscribeTradeOffer(TradeOfferManager tradeOfferManager)
        {
            tradeOfferManager.OnTradeOfferUpdated += TradeOfferRouter;
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
                    Console.WriteLine("Error while polling trade offers: " + e);
                }

                Thread.Sleep(30 * 1000);//tradeOfferPollingIntervalSecs * 1000);
            }
        }

        /////////////////////////////////////////////////////////////////////

        //Helper function :
        #region Helper function
        //Helper function
        public void Run()
        {
            if (!Stop)
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
