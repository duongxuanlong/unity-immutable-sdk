using System;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using VoltstroStudios.UnityWebBrowser.Core;
#endif
using Immutable.Passport.Model;
using Immutable.Passport.Core;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace Immutable.Passport
{
    public class Passport
    {
        private const string TAG = "[Passport]";

        public static Passport? Instance { get; private set; }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private readonly WebBrowserClient webBrowserClient = new();
#endif

        private static bool? readySignalReceived = null;
        private PassportImpl? passportImpl = null;

        /// <summary>
        /// <param name="productName">The name of the game. This is used to clean up resources associated with
        /// Passport when the game quits. Use Application.productName.</param>
        /// </summary> 
        private Passport()
        {
#if UNITY_EDITOR_WIN
            Application.quitting += OnQuit;
#endif
        }

        public static UniTask<Passport> Init(string clientId)
        {
            if (Instance == null)
            {
                Instance = new Passport();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                // Wait until we get a ready signal
                return Instance.Initialise()
                    .ContinueWith(async () =>
                    {
                        await UniTask.WaitUntil(() => readySignalReceived != null);
                    })
                    .ContinueWith(async () =>
                    {
                        if (readySignalReceived == true)
                        {
                            await Instance.GetPassportImpl().Init(clientId);
                            return Instance;
                        }
                        else
                        {
                            throw new PassportException("Failed to initiliase Passport", PassportErrorType.INITALISATION_ERROR);
                        }
                    });
#else
                readySignalReceived = true;
                return Instance.Initialise()
                    .ContinueWith(() =>
                    {
                        return Instance;
                    });
#endif
            }
            else
            {
                readySignalReceived = true;
                return UniTask.FromResult(Instance);
            }
        }

        private async UniTask Initialise()
        {
            try
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                BrowserCommunicationsManager communicationsManager = new(webBrowserClient);
                communicationsManager.OnReady += () => readySignalReceived = true;
                await webBrowserClient.Init();
#else
                BrowserCommunicationsManager communicationsManager = new();
#endif
                passportImpl = new PassportImpl(communicationsManager);
            }
            catch (Exception)
            {
                // Reset values
                readySignalReceived = false;
                Instance = null;
            }
        }

#if UNITY_EDITOR_WIN
        private void OnQuit()
        {
            // Need to clean up UWB resources when quitting the game in the editor
            // as the child engine process would still be alive
            Debug.Log($"{TAG} Quitting the Player");
            webBrowserClient.Dispose();
        }
#endif

        /// <summary>
        /// Sets the timeout time for` waiting for each call to respond (in milliseconds).
        /// This only applies to functions that use the browser communications manager.
        /// </summary>
        public void SetCallTimeout(int ms)
        {
            GetPassportImpl().communicationsManager.SetCallTimeout(ms);
        }

        /// <summary>
        /// Connects the user into Passport via device code auth and sets up the IMX provider.
        ///
        /// The user does not need to go through the device code auth flow if the saved access token is still valid or
        /// the refresh token can be used to get a new access token.
        /// <returns>
        /// The end-user verification code and url if the user has to go through device code auth, otherwise null
        /// </returns>
        /// </summary>
        public async UniTask<ConnectResponse?> Connect()
        {
            return await GetPassportImpl().Connect();
        }

        /// <summary>
        /// Similar to Connect, however if the saved access token is no longer valid and the refresh token cannot be used,
        /// it will not fallback to device code.
        /// <returns>
        /// True if the user is connected to Passport
        /// </returns>
        /// </summary>
        public async UniTask<bool> ConnectSilent()
        {
            return await GetPassportImpl().ConnectSilent();
        }

        /// <summary>
        /// Completes the device code auth flow. This will open the user's default browser and take them through Passport login.
        /// Once authenticated, this function will set up the IMX provider.
        /// </summary>
        public async UniTask ConfirmCode(long? timeoutMs = null)
        {
            await GetPassportImpl().ConfirmCode(timeoutMs);
        }

        /// <summary>
        /// Gets the wallet address of the logged in user.
        /// <returns>
        /// The wallet address, otherwise null
        /// </returns>
        /// </summary>
        public async UniTask<string?> GetAddress()
        {
            return await GetPassportImpl().GetAddress();
        }

        /// <summary>
        /// Logs the user out of Passport and removes any stored credentials
        /// </summary>
        public async UniTask Logout()
        {
            await GetPassportImpl().Logout();
        }

        /// <summary>
        /// Checks if credentials exist but does not check if they're valid
        /// <returns>
        /// True if there are crendentials saved
        /// </returns>
        /// </summary>
        public UniTask<bool> HasCredentialsSaved()
        {
            return GetPassportImpl().HasCredentialsSaved();
        }

        /// <summary>
        /// Retrieves the email address of the user whose credentials are currently stored.
        /// <returns>
        /// The email address, otherwise null
        /// </returns>
        /// </summary>
        public async UniTask<string?> GetEmail()
        {
            string? email = await GetPassportImpl().GetEmail();
            return email;
        }

        /// <summary>
        /// Gets the currently saved access token without verifying its validity.
        /// <returns>
        /// The access token, otherwise null
        /// </returns>
        /// </summary>
        public UniTask<string?> GetAccessToken()
        {
            return GetPassportImpl().GetAccessToken();
        }

        /// <summary>
        /// Gets the currently saved ID token without verifying its validity.
        /// <returns>
        /// The ID token, otherwise null
        /// </returns>
        /// </summary>
        public UniTask<string?> GetIdToken()
        {
            return GetPassportImpl().GetIdToken();
        }

        private PassportImpl GetPassportImpl()
        {
            if (passportImpl != null)
            {
                return passportImpl;
            }
            throw new PassportException("Passport not initialised");
        }
    }
}
