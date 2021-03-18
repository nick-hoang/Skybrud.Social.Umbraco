using System;
using System.Linq;
using System.Web;
using System.Web.Security;
using Skybrud.Social.Facebook;
using Skybrud.Social.Facebook.OAuth;
using Skybrud.Social.Facebook.Objects.Debug;
using Skybrud.Social.Facebook.Objects.Users;
using Skybrud.Social.Facebook.Options.Pages;
using Skybrud.Social.Facebook.Responses.Accounts;
using Skybrud.Social.Instagram;
using Skybrud.Social.Instagram.OAuth;
using Skybrud.Social.Instagram.Objects;
using Skybrud.Social.Instagram.Responses;
using Skybrud.Social.Umbraco.Instagram;
using Skybrud.Social.Umbraco.Instagram.PropertyEditors.OAuth;
using Skybrud.Social.Umbraco.Facebook;
using Skybrud.Social.Umbraco.Facebook.PropertyEditors.OAuth;
using Umbraco.Core.Security;
using Skybrud.Social.Facebook.Fields;
using Skybrud.Social.Facebook.Objects.Pages;
using Skybrud.Social.Facebook.Responses.Pages;
using Newtonsoft.Json.Linq;
using Skybrud.Social.Json.Extensions.JObject;

namespace Skybrud.Social.Umbraco.App_Plugins.Skybrud.Social.Dialogs {

    public partial class InstagramOAuth : System.Web.UI.Page
    {

        #region Umbraco related properties

        public string Callback { get; private set; }

        public string ContentTypeAlias { get; private set; }

        public string PropertyAlias { get; private set; }

        /// <summary>
        /// Facebook page name that accosicated with instagram business account
        /// </summary>
        public string PageName { get; private set; }

        #endregion

        /// <summary>
        /// Gets the authorizing code from the query string (if specified).
        /// </summary>
        public string AuthCode
        {
            get { return Request.QueryString["code"]; }
        }

        /// <summary>
        /// An optional value indicating a server-specific state. This is a session key in this case
        /// </summary>
        public string AuthState
        {
            get { return Request.QueryString["state"]; }
        }

        public string AuthErrorReason
        {
            get { return Request.QueryString["error_reason"]; }
        }

        public string AuthError
        {
            get
            {
                var error = Request.QueryString["error"];
                if (string.IsNullOrEmpty(error))
                {
                    error = Request.QueryString["error_code"];
                }
                return error;
            }
        }

        public string AuthErrorDescription
        {
            get
            {
                var errorDescription = Request.QueryString["error_description"];
                if (string.IsNullOrEmpty(errorDescription))
                {
                    errorDescription = Request.QueryString["error_message"];
                }
                return errorDescription;
            }
        }

        public object SessionSocial
        {
            get
            {
                return Session["Skybrud.Social_" + AuthState];
            }            
        }

        public void SetSessionSocial(string authState, object value)
        {
            Session["Skybrud.Social_" + authState] = value;
        }

        public object SessionSocialForward
        {
            get
            {
                return Session["Skybrud.Social_Forward_" + AuthState];
            }            
        }

        public void SetSessionSocialForward(string authState, object value)
        {
            Session["Skybrud.Social_Forward_" + authState] = value;
        }

        protected override void OnPreInit(EventArgs e)
        {

            base.OnPreInit(e);

            if (PackageHelpers.UmbracoVersion != "7.2.2") return;

            // Handle authentication stuff to counteract bug in Umbraco 7.2.2 (see U4-6342)
            HttpContextWrapper http = new HttpContextWrapper(Context);
            FormsAuthenticationTicket ticket = http.GetUmbracoAuthTicket();
            http.AuthenticateCurrentRequest(ticket, true);

        }

        protected void Page_Load(object sender, EventArgs e)
        {

            Callback = Request.QueryString["callback"];
            ContentTypeAlias = Request.QueryString["contentTypeAlias"];
            PropertyAlias = Request.QueryString["propertyAlias"];

            if (!IsPostBack)
            {
                var action = Request.QueryString["action"];
                if (action == "forward")
                {
                    DoAction_Forward();
                    return;
                }

                if (AuthCode != null && AuthState != null && SessionSocialForward != null)
                {
                    if (CheckAction_Forward())
                    {
                        //stop here if successed, because we just redirect back to original url
                        return;
                    }
                }

                if (AuthState != null)
                {                    
                    string[] stateValue = SessionSocial as string[];
                    if (stateValue != null && stateValue.Length == 4)
                    {
                        Callback = stateValue[0];
                        ContentTypeAlias = stateValue[1];
                        PropertyAlias = stateValue[2];
                        PageName = stateValue[3];
                    }
                }                

                // Session expired?
                if (AuthState != null && SessionSocial == null)
                {
                    Content.Text = "<div class=\"error\">Session expired?</div>";
                    return;
                }

                // Check whether an error response was received from Instagram
                if (AuthError != null)
                {
                    Content.Text = "<div class=\"error\">Error: " + AuthErrorDescription + "</div>";
                    return;
                }

                // Get the prevalue options
                var options = InstagramOAuthPreValueOptions.Get(ContentTypeAlias, PropertyAlias);
                if (!options.IsValid)
                {
                    Content.Text = "Hold on now! The options of the underlying prevalue editor isn't valid.";
                    return;
                }

                if (options.NeedProfessionalAccount)
                {
                    if (string.IsNullOrEmpty(AuthCode))
                    {
                        pnlInstagramPageName.Visible = true;
                    }
                    FacebookGraphApi(options);
                }
                else
                {
                    InstagramBasicApi(options);
                }
            }
        }

        private void InstagramBasicApi(InstagramOAuthPreValueOptions options)
        {

            // Configure the OAuth client based on the options of the prevalue options
            InstagramOAuthClient client = new InstagramOAuthClient
            {
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret,
                RedirectUri = options.RedirectUri
            };

            // Redirect the user to the Instagram login dialog
            if (AuthCode == null)
            {

                // Generate a new unique/random state
                string state = Guid.NewGuid().ToString();

                // Save the state in the current user session
                SetSessionSocial(state, new[] { Callback, ContentTypeAlias, PropertyAlias, /*Do not use here but have to keep this*/ PageName });

                // Construct the authorization URL                
                string url = client.GetAuthorizationUrl(state, options.ScopeStr);

                // Append the scope to the authorization URL
                //if (!String.IsNullOrWhiteSpace(options.ScopeStr))
                //{
                //    url += "&scope=" + options.ScopeStr.Replace(",", "+");
                //}                

                // Redirect the user
                Response.Redirect(url, true);
                return;

            }            

            // Exchange the authorization code for an access token
            InstagramAccessTokenResponse accessToken;
            try
            {
                accessToken = client.GetAccessTokenFromAuthCode(AuthCode);
            }
            catch (Exception ex)
            {
                Content.Text = "<div class=\"error\"><b>Unable to acquire access token</b><br />" + ex.Message + "</div>";
                return;
            }

            try
            {

                // Initialize the Instagram service
                InstagramService service = InstagramService.CreateFromAccessToken(accessToken.Body.AccessToken);

                // Get information about the authenticated user
                InstagramUser user = service.Users.GetSelf().Body.Data;



                Content.Text += "<p>Hi <strong>" + (user.FullName ?? user.Username) + "</strong></p>";
                Content.Text += "<p>Please wait while you're being redirected...</p>";

                //Exchange to long-lived access token
                accessToken = client.GetLongLivedAccessToken(accessToken.Body.AccessToken);

                // Set the callback data
                InstagramOAuthData data = new InstagramOAuthData
                {
                    Id = user.Id.ToString(),
                    BusinessId = user.Id.ToString(),
                    Username = user.Username,
                    FullName = user.FullName,
                    Name = user.FullName ?? user.Username,
                    Avatar = user.ProfilePicture,
                    AccessToken = accessToken.Body.AccessToken, //long-lived access token
                    AccessTokenExpireDate = DateTime.Now.AddSeconds(accessToken.Body.ExpiresIn)
                };

                // Update the UI and close the popup window
                Page.ClientScript.RegisterClientScriptBlock(GetType(), "callback", String.Format(
                    "self.opener." + Callback + "({0}); window.close();",
                    data.Serialize()
                ), true);

            }
            catch (Exception ex)
            {
                Content.Text = "<div class=\"error\"><b>Unable to get user information</b><br />" + ex.Message + "</div>";
            }
        }

        /// <summary>
        /// Process callback only
        /// </summary>
        /// <param name="options"></param>
        private void FacebookGraphApi(InstagramOAuthPreValueOptions options)
        {
            //don't do anything on first load (asking page name screen)
            if (string.IsNullOrEmpty(AuthCode))
            {
                return;
            }
            // Configure the OAuth client based on the options of the prevalue options
            var client = new FacebookOAuthClient
            {
                AppId = options.ClientId,
                AppSecret = options.ClientSecret,
                RedirectUri = options.RedirectUri,
                Version = "v10.0"
            };

            // Exchange the authorization code for a user access token
            string shortLivedAccessToken;
            try
            {
                shortLivedAccessToken = client.GetAccessTokenFromAuthCode(AuthCode);
            }
            catch (Exception ex)
            {
                Content.Text = "<div class=\"error\"><b>Unable to acquire access token</b><br />" + ex.Message + "</div>";
                return;
            }

            client.AccessToken = shortLivedAccessToken;

            try
            {
                // Initialize the Facebook service (no calls are made here)
                FacebookService service = FacebookService.CreateFromOAuthClient(client);

                var pageId = FindFacebookPageByName(service, PageName);
                if (pageId == null)
                {
                    Content.Text = "Your facebook page name is not exists, please correct it!";
                    return;
                }

                var instagramBusinessAccountField = "instagram_business_account";
                var getPageOptions = new FacebookGetPageOptions(pageId.Id);
                getPageOptions.Fields.Add(new FacebookField(instagramBusinessAccountField));

                var page = service.Pages.GetPage(getPageOptions);
                var instagramBusinessAccount = page.Body.JsonObject.GetObject(instagramBusinessAccountField);
                if (instagramBusinessAccount == null)
                {
                    Content.Text = "instagram_business_account is not exists in the response, please try again!";
                    return;
                }

                //get facebook user infor
                var me = service.Users.GetUser("me").Body;


                //Exchange to long-lived access token
                var longLivedAccessToken = client.RenewAccessToken(shortLivedAccessToken);

                // Set the callback data

                InstagramOAuthData data = new InstagramOAuthData
                {
                    Id = me.Id,// facebook user id,
                    BusinessId = instagramBusinessAccount.GetString("id"),
                    Username = me.Name,
                    FullName = me.FirstName + " " + me.LastName,
                    Name = me.Name,
                    Avatar = string.Empty,
                    AccessToken = longLivedAccessToken, //long-lived access token
                    AccessTokenExpireDate = DateTime.Now.AddYears(10), //facebook access token doesn't have an expired date, if no requests are made, the token will expire after about 60 days
                    UseInstagramGraphAPI = true
                };

                Content.Text += "<p>Hi <strong>" + data.Username + "</strong></p>";
                Content.Text += "<p>Please wait while you're being redirected...</p>";


                // Update the UI and close the popup window
                Page.ClientScript.RegisterClientScriptBlock(GetType(), "callback", String.Format(
                    "self.opener." + Callback + "({0}); window.close();",
                    data.Serialize()//Save JSON data through callback parameter
                ), true);

            }
            catch (Exception ex)
            {
                Content.Text = "<div class=\"error\"><b>Unable to get user information</b><br />" + ex.Message + "</div>";
            }
        }

        protected void btnInstagramPageNameSubmit_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                Content.Text = "Please enter your facebook page name!";
                return;
            }
            var options = InstagramOAuthPreValueOptions.Get(ContentTypeAlias, PropertyAlias);
            if (!options.IsValid)
            {
                Content.Text = "Hold on now! The options of the underlying prevalue editor isn't valid.";
                return;
            }

            // Configure the OAuth client based on the options of the prevalue options
            var client = new FacebookOAuthClient
            {
                AppId = options.ClientId,
                AppSecret = options.ClientSecret,
                RedirectUri = options.RedirectUri + "&pageName=" + Server.UrlEncode(txtInstagramPageName.Text),
                Version = "v10.0"
            };

            // Redirect the user to the Facebook login dialog


            // Generate a new unique/random state
            string state = Guid.NewGuid().ToString();

            // Save the state in the current user session
            var sessionData = new[] { Callback, ContentTypeAlias, PropertyAlias, txtInstagramPageName.Text };
            SetSessionSocial(state, sessionData);

            // Construct the authorization URL            
            string url = client.GetAuthorizationUrl(state, options.ScopeStr);

            // Redirect the user
            Response.Redirect(url, true);
            return;
        }

        private FacebookPage FindFacebookPageByName(FacebookService service, string name)
        {
            var normallizeName = name.Trim().ToLower();
            var result = service.Pages.GetUserPages();
            var pageId = result.Body.Data.FirstOrDefault(e => e.Name.ToLower().Trim() == normallizeName);
            while (pageId == null && result.Body.Paging != null && !string.IsNullOrEmpty(result.Body.Paging.Next))
            {
                result = FacebookPagesResponse.ParseResponse(service.Pages.Raw.Client.DoAuthenticatedGetRequest(result.Body.Paging.Next));
                pageId = result.Body.Data.FirstOrDefault(e => e.Name.ToLower().Trim() == normallizeName);
            }

            return pageId;
        }

        private void DoAction_Forward()
        {                                   
            var state = AuthState;//original sessionId
            var dataTypeName = "Instagram OAuth - SME";// Request.QueryString["datatype"];//Data type name of Instagram basic API in redirect site

            var options = InstagramOAuthPreValueOptions.Get(dataTypeName);
            if (!options.IsValid)
            {
                Content.Text = "Hold on now! The options of the underlying prevalue editor isn't valid.";
                return;
            }

            // Configure the OAuth client based on the options of the prevalue options
            InstagramOAuthClient client = new InstagramOAuthClient
            {
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret,
                RedirectUri = options.RedirectUri,
            };

            var callback = Request.QueryString["callback"];
            SetSessionSocialForward(state, new[] { dataTypeName, callback });
            string url = client.GetAuthorizationUrl(state, options.ScopeStr);

            //redirect to Instagram API
            Response.RedirectPermanent(url, true);
        }

        private bool CheckAction_Forward()
        {
            var sessionValue = SessionSocialForward as string[];

            string errorMessage = string.Empty;
            // Session expired?
            if (sessionValue == null)
            {                
                return false;
            }
            string dataTypeName = string.Empty, callbackUrl = string.Empty;
            if (sessionValue != null && sessionValue.Length == 2)
            {
                dataTypeName = sessionValue[0];
                callbackUrl = sessionValue[1];                
            }
            var options = InstagramOAuthPreValueOptions.Get(dataTypeName);
            if (!options.IsValid)
            {
                errorMessage = $"Hold on now! The {dataTypeName}'s options isn't valid.";
                Response.RedirectPermanent($"{callbackUrl}?state={AuthState}&error_message={errorMessage}", true);
                return false;
            }
            // Configure the OAuth client based on the options of the prevalue options
            InstagramOAuthClient client = new InstagramOAuthClient
            {
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret,
                RedirectUri = options.RedirectUri
            };
            // Exchange the authorization code for an access token
            InstagramAccessTokenResponse accessToken;
            try
            {
                accessToken = client.GetAccessTokenFromAuthCode(AuthCode);
            }
            catch (Exception ex)
            {
                errorMessage = "Unable to acquire access token:" + ex.Message;
                return false;
            }

            try
            {                
                Response.RedirectPermanent($"{callbackUrl}?accessToken={accessToken.Body.AccessToken}&state={AuthState}&error_message={errorMessage}", true);
                return true;

            }
            catch (Exception ex)
            {
                Content.Text = "<div class=\"error\"><b>Unable to get user information</b><br />" + ex.Message + "</div>";
            }
            return false;
        }
    }
}