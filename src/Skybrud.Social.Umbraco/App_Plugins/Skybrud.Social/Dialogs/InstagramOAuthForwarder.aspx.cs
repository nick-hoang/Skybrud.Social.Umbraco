using Skybrud.Social.Facebook;
using Skybrud.Social.Facebook.Fields;
using Skybrud.Social.Facebook.OAuth;
using Skybrud.Social.Facebook.Objects.Pages;
using Skybrud.Social.Facebook.Options.Pages;
using Skybrud.Social.Facebook.Responses.Pages;
using Skybrud.Social.Instagram;
using Skybrud.Social.Instagram.OAuth;
using Skybrud.Social.Instagram.Objects;
using Skybrud.Social.Instagram.Responses;
using Skybrud.Social.Umbraco.Instagram;
using Skybrud.Social.Umbraco.Instagram.PropertyEditors.OAuth;
using System;
using System.Linq;
using System.Web;
using System.Web.Security;
using Umbraco.Core.Security;

namespace Skybrud.Social.Umbraco.App_Plugins.Skybrud.Social.Dialogs
{

    public partial class InstagramOAuthForwarder : System.Web.UI.Page
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
        /// short-live accesstoken from proxy
        /// </summary>
        public string AccessToken
        {
            get { return Request.QueryString["accessToken"]; }
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

        /// <summary>
        /// The dev page
        /// </summary>
        public bool IsForwardAction
        {
            get
            {
                return Request.QueryString["action"] == "forward";                
            }
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
                var options = InstagramOAuthPreValueOptions.Get("Instagram OAuth - SME");

                if (AuthState != null)
                {                    
                    string[] stateValue = Session["Skybrud.Social_" + AuthState] as string[];
                    if (stateValue != null && stateValue.Length == 4)
                    {
                        Callback = stateValue[0];
                        ContentTypeAlias = stateValue[1];
                        PropertyAlias = stateValue[2];
                        PageName = stateValue[3];
                    }
                }

                // Session expired?
                if (AuthState != null && Session["Skybrud.Social_" + AuthState] == null)
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
                
                InstagramBasicApi();                
            }
        }

        private void InstagramBasicApi()
        {
            // Redirect the user to the Instagram login dialog
            if (string.IsNullOrEmpty(AccessToken))
            {

                // Generate a new unique/random state
                string state = Guid.NewGuid().ToString();

                // Save the state in the current user session
                Session["Skybrud.Social_" + state] = new[] { Callback, ContentTypeAlias, PropertyAlias, /*Do not use here but have to keep this*/ PageName };
                var callback = $"{Request.Url.Scheme}://{Request.Url.Authority}{Request.Url.AbsolutePath}";
                var url = $"https://dev.easybrew.com.au/App_Plugins/Skybrud.Social/Dialogs/InstagramOAuth.aspx?action=forward&state={state}&callback={callback}";
                // Redirect the user
                Response.RedirectPermanent(url, true);
                return;

            }

            // Get the prevalue options
            var options = InstagramOAuthPreValueOptions.Get(ContentTypeAlias, PropertyAlias);
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
                RedirectUri = options.RedirectUri
            };

            try
            {
                // Initialize the Instagram service
                InstagramService service = InstagramService.CreateFromAccessToken(AccessToken, PackageHelpers.FacebookApiVersion);

                // Get information about the authenticated user
                InstagramUser user = service.Users.GetSelf().Body.Data;



                Content.Text += "<p>Hi <strong>" + (user.FullName ?? user.Username) + "</strong></p>";
                Content.Text += "<p>Please wait while you're being redirected...</p>";

                //Exchange to long-lived access token
                var longLivedAccessToken = client.GetLongLivedAccessToken(AccessToken);

                // Set the callback data
                InstagramOAuthData data = new InstagramOAuthData
                {
                    Id = user.Id.ToString(),
                    BusinessId = user.Id.ToString(),
                    Username = user.Username,
                    FullName = user.FullName,
                    Name = user.FullName ?? user.Username,
                    Avatar = user.ProfilePicture,
                    AccessToken = longLivedAccessToken.Body.AccessToken, //long-lived access token
                    AccessTokenExpireDate = DateTime.Now.AddSeconds(longLivedAccessToken.Body.ExpiresIn)
                };

                // Update the UI and close the popup window
                Page.ClientScript.RegisterClientScriptBlock(GetType(), "callback", String.Format(
                    "self.opener." + Callback + "({0}); window.close();",
                    data.Serialize()
                ), true);

            }
            catch (Exception ex)
            {
                Content.Text = "<div class=\"error\"><b>Unable to get user information</b><br />" + ex.ToString() + "</div>";
            }
        }
        
    }
}